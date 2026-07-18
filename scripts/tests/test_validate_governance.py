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
    def write_record(self, root: Path, *, include_follow_up: bool = True) -> None:
        adr_dir = root / "docs" / "adr"
        adr_dir.mkdir(parents=True)
        (adr_dir / "README.md").write_text(
            "[0001](0001-example-decision.md)\n",
            encoding="utf-8",
        )
        sections = [
            "# ADR 0001: Example decision",
            "",
            "- Status: Accepted",
            "- Date: 2026-07-18",
            "- Owners: Maintainers",
            "- Related issues: #123",
            "",
            "## Context",
            "Context.",
            "",
            "## Decision",
            "Decision.",
            "",
            "## Alternatives considered",
            "Alternative.",
            "",
            "## Consequences",
            "Consequences.",
            "",
            "## Validation",
            "Evidence.",
        ]
        if include_follow_up:
            sections.extend(("", "## Follow-up", "None."))
        (adr_dir / "0001-example-decision.md").write_text(
            "\n".join(sections) + "\n",
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
            self.write_record(root, include_follow_up=False)
            errors = governance.check_adrs(root)
            self.assertTrue(any("## Follow-up" in error for error in errors))


if __name__ == "__main__":
    unittest.main()
