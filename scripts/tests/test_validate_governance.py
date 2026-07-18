from __future__ import annotations

import json
import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPT_DIR))

import validate_governance as governance


class PullRequestValidationTests(unittest.TestCase):
    def write_event(self, directory: str, body: str) -> Path:
        path = Path(directory) / "event.json"
        path.write_text(json.dumps({"pull_request": {"body": body}}), encoding="utf-8")
        return path

    def test_accepts_closing_issue_reference(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            event = self.write_event(directory, "Closes #123\n\n## Outcome\nDone")
            self.assertEqual([], governance.check_pr_event(event))

    def test_accepts_non_closing_issue_reference(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            event = self.write_event(directory, "Refs owner/repository#42")
            self.assertEqual([], governance.check_pr_event(event))

    def test_rejects_missing_issue_reference(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            event = self.write_event(directory, "## Outcome\nUntraceable change")
            errors = governance.check_pr_event(event)
            self.assertEqual(1, len(errors))


class ArchitectureBoundaryTests(unittest.TestCase):
    def test_rejects_package_dependency_on_application(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            package = root / "Packages" / "com.kkmia.talksystem"
            package.mkdir(parents=True)
            (package / "Invalid.cs").write_text(
                "using WhiteRoom.Novel;\nnamespace TalkSystem {}\n",
                encoding="utf-8",
            )
            errors = governance.check_architecture_boundaries(root)
            self.assertTrue(any("reverse dependency" in error for error in errors))

    def test_allows_reflection_inside_setup(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            setup = root / "Assets" / "Scripts" / "Setup"
            setup.mkdir(parents=True)
            (setup / "Binder.cs").write_text(
                "using System.Reflection;\nnamespace WhiteRoom.Novel {}\n",
                encoding="utf-8",
            )
            self.assertEqual([], governance.check_architecture_boundaries(root))

    def test_rejects_reflection_outside_setup(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            services = root / "Assets" / "Scripts" / "Services"
            services.mkdir(parents=True)
            (services / "Invalid.cs").write_text(
                "using System.Reflection;\nnamespace WhiteRoom.Novel {}\n",
                encoding="utf-8",
            )
            errors = governance.check_architecture_boundaries(root)
            self.assertTrue(any("outside Assets/Scripts/Setup" in error for error in errors))


class AdrValidationTests(unittest.TestCase):
    def write_record(
        self,
        root: Path,
        *,
        include_japanese: bool = True,
        japanese_status: str = "Accepted",
        include_notes: bool = True,
    ) -> None:
        adr_dir = root / "docs" / "adr"
        adr_dir.mkdir(parents=True)
        index = (
            "[English](0001-example-decision.md) "
            "[Japanese](0001-example-decision.ja.md)\n"
        )
        (adr_dir / "README.md").write_text(index, encoding="utf-8")
        (adr_dir / "README.ja.md").write_text(index, encoding="utf-8")

        english_sections = [
            "# ADR-0001: Example decision",
            "",
            "Status: Accepted",
            "Date: 2026-07-18",
            "Related: [Issue #123](https://example.com/issues/123)",
            "Japanese: [日本語版](0001-example-decision.ja.md)",
            "",
            "## Context and problem statement",
            "Context.",
            "",
            "## Decision drivers",
            "- Driver.",
            "",
            "## Decision outcome",
            "Decision.",
            "",
            "### First clause",
            "**Rationale**: Reason.",
            "**Impact**: Impact.",
            "",
            "### Second clause",
            "**Rationale**: Reason.",
            "**Impact**: Impact.",
            "",
            "## Benefits",
            "- Benefit.",
            "",
            "## Trade-offs",
            "- Cost. → Mitigation.",
            "",
            "## Rejected alternatives",
            "| Alternative | Why rejected |",
            "| --- | --- |",
            "| Other | Driver mismatch. |",
            "",
            "## Related ADRs",
            "- None.",
            "",
            "## Development rule integration",
            "- Test it.",
        ]
        if include_notes:
            english_sections.extend(("", "## Notes", "- None."))
        (adr_dir / "0001-example-decision.md").write_text(
            "\n".join(english_sections) + "\n",
            encoding="utf-8",
        )

        if not include_japanese:
            return

        japanese_sections = [
            "# ADR-0001: 判断例",
            "",
            f"ステータス: {japanese_status}",
            "日付: 2026-07-18",
            "関連: [Issue #123](https://example.com/issues/123)",
            "English: [English canonical version](0001-example-decision.md)",
            "",
            "## コンテキストと問題提起",
            "コンテキスト。",
            "",
            "## 決定要因",
            "- 要因。",
            "",
            "## 決定結果",
            "判断。",
            "",
            "### 1つ目の決定事項",
            "**根拠**: 理由。",
            "**影響**: 影響。",
            "",
            "### 2つ目の決定事項",
            "**根拠**: 理由。",
            "**影響**: 影響。",
            "",
            "## 利点",
            "- 利点。",
            "",
            "## トレードオフ",
            "- コスト。→ 緩和策。",
            "",
            "## 不採用の選択肢と根拠",
            "| 選択肢 | 不採用理由 |",
            "| --- | --- |",
            "| その他 | 要因に合わない。 |",
            "",
            "## 関連するADR",
            "- なし。",
            "",
            "## 開発ルール連携",
            "- テストする。",
            "",
            "## 注記",
            "- なし。",
        ]
        (adr_dir / "0001-example-decision.ja.md").write_text(
            "\n".join(japanese_sections) + "\n",
            encoding="utf-8",
        )

    def test_accepts_complete_indexed_record(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.write_record(root)
            self.assertEqual([], governance.check_adrs(root))

    def test_rejects_missing_required_section(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.write_record(root, include_notes=False)
            errors = governance.check_adrs(root)
            self.assertTrue(any("## Notes" in error for error in errors))

    def test_rejects_missing_japanese_pair(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.write_record(root, include_japanese=False)
            errors = governance.check_adrs(root)
            self.assertTrue(any("missing Japanese pair" in error for error in errors))

    def test_rejects_status_mismatch(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.write_record(root, japanese_status="Proposed")
            errors = governance.check_adrs(root)
            self.assertTrue(any("statuses differ" in error for error in errors))

    def test_rejects_decision_clause_count_mismatch(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.write_record(root)
            japanese_path = (
                root / "docs" / "adr" / "0001-example-decision.ja.md"
            )
            content = japanese_path.read_text(encoding="utf-8")
            japanese_path.write_text(
                content.replace("### 2つ目の決定事項", "#### 2つ目の決定事項"),
                encoding="utf-8",
            )
            errors = governance.check_adrs(root)
            self.assertTrue(any("decision clause counts differ" in error for error in errors))


if __name__ == "__main__":
    unittest.main()
