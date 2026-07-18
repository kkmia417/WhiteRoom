#!/usr/bin/env python3
"""Validate WhiteRoom architecture records and Issue-driven delivery contracts."""

from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path
from urllib.parse import unquote


REQUIRED_PATHS = (
    "AGENTS.md",
    "CONTRIBUTING.md",
    "docs/adr/README.md",
    "docs/adr/0000-template.md",
    "docs/architecture/README.md",
    "docs/development/issue-driven-development.md",
    ".github/ISSUE_TEMPLATE/feature.yml",
    ".github/ISSUE_TEMPLATE/bug.yml",
    ".github/ISSUE_TEMPLATE/architecture.yml",
    ".github/ISSUE_TEMPLATE/task.yml",
    ".github/ISSUE_TEMPLATE/config.yml",
    ".github/pull_request_template.md",
    ".github/workflows/governance.yml",
)
ADR_HEADINGS = (
    "## Context",
    "## Decision",
    "## Alternatives considered",
    "## Consequences",
    "## Validation",
    "## Follow-up",
)
ADR_STATUSES = {"Proposed", "Accepted", "Rejected", "Deprecated", "Superseded"}
ISSUE_REFERENCE = re.compile(
    r"\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?|refs?|relates?\s+to)"
    r"\s*:?\s+(?:[\w.-]+/[\w.-]+)?#\d+\b"
    r"|https://github\.com/[\w.-]+/[\w.-]+/issues/\d+",
    re.IGNORECASE,
)
MARKDOWN_LINK = re.compile(r"!?\[[^\]]*]\(([^)]+)\)")


def read_text(path: Path) -> str:
    return path.read_text(encoding="utf-8")


def check_required_paths(root: Path) -> list[str]:
    return [
        f"required governance artifact is missing: {relative}"
        for relative in REQUIRED_PATHS
        if not (root / relative).is_file()
    ]


def check_adrs(root: Path) -> list[str]:
    errors: list[str] = []
    adr_dir = root / "docs" / "adr"
    if not adr_dir.is_dir():
        return ["ADR directory is missing: docs/adr"]

    records = sorted(
        path
        for path in adr_dir.glob("*.md")
        if path.name not in {"README.md", "0000-template.md"}
    )
    expected_number = 1
    index = read_text(adr_dir / "README.md") if (adr_dir / "README.md").is_file() else ""

    for path in records:
        match = re.fullmatch(r"(\d{4})-[a-z0-9]+(?:-[a-z0-9]+)*\.md", path.name)
        if not match:
            errors.append(f"invalid ADR filename: {path.relative_to(root)}")
            continue

        number = int(match.group(1))
        if number != expected_number:
            errors.append(
                f"ADR numbering is not contiguous: expected {expected_number:04d}, "
                f"found {number:04d}"
            )
        expected_number = number + 1

        content = read_text(path)
        if not re.search(rf"^# ADR {number:04d}: .+", content, re.MULTILINE):
            errors.append(f"{path.relative_to(root)} has an invalid title")

        status = re.search(r"^- Status: (.+)$", content, re.MULTILINE)
        if status is None or status.group(1).strip() not in ADR_STATUSES:
            errors.append(f"{path.relative_to(root)} has an invalid ADR status")

        for metadata in ("- Date:", "- Owners:", "- Related issues:"):
            if not re.search(rf"^{re.escape(metadata)}\s+\S+", content, re.MULTILINE):
                errors.append(f"{path.relative_to(root)} is missing metadata: {metadata}")

        related = re.search(r"^- Related issues:\s+(.+)$", content, re.MULTILINE)
        if related is not None and not re.search(r"#\d+", related.group(1)):
            errors.append(f"{path.relative_to(root)} must link a GitHub Issue")

        for heading in ADR_HEADINGS:
            if heading not in content:
                errors.append(f"{path.relative_to(root)} is missing section: {heading}")

        if f"({path.name})" not in index:
            errors.append(f"docs/adr/README.md does not index {path.name}")

    if not records:
        errors.append("docs/adr contains no decision records")
    return errors


def check_artifact_contracts(root: Path) -> list[str]:
    errors: list[str] = []
    expected_fragments = {
        ".github/ISSUE_TEMPLATE/feature.yml": ("id: acceptance", "id: architecture", "id: validation"),
        ".github/ISSUE_TEMPLATE/bug.yml": ("id: reproduction", "id: expected", "id: actual"),
        ".github/ISSUE_TEMPLATE/architecture.yml": ("id: options", "id: adr", "id: evidence"),
        ".github/ISSUE_TEMPLATE/task.yml": ("id: scope", "id: done", "id: validation"),
        ".github/pull_request_template.md": ("Closes #", "## Architecture", "## Validation"),
        ".github/workflows/governance.yml": (
            "pull_request:",
            "python -m unittest discover",
            "python scripts/validate_governance.py --root . --pr-event",
        ),
    }
    for relative, fragments in expected_fragments.items():
        path = root / relative
        if not path.is_file():
            continue
        content = read_text(path)
        for fragment in fragments:
            if fragment not in content:
                errors.append(f"{relative} is missing required contract text: {fragment}")
    return errors


def check_markdown_links(root: Path) -> list[str]:
    errors: list[str] = []
    documents = [root / "README.md", root / "CONTRIBUTING.md", root / "AGENTS.md"]
    documents.extend((root / "docs").rglob("*.md"))

    for document in sorted(path for path in documents if path.is_file()):
        content = read_text(document)
        for raw_target in MARKDOWN_LINK.findall(content):
            target = raw_target.strip().strip("<>")
            if target.startswith(("http://", "https://", "mailto:", "#")):
                continue
            target = unquote(target.split("#", 1)[0])
            if not target:
                continue
            resolved = (root / target.lstrip("/")) if target.startswith("/") else (document.parent / target)
            if not resolved.exists():
                errors.append(
                    f"{document.relative_to(root)} links to missing path: {raw_target}"
                )
    return errors


def check_architecture_boundaries(root: Path) -> list[str]:
    errors: list[str] = []
    app_root = root / "Assets" / "Scripts"
    package_root = root / "Packages" / "com.kkmia.talksystem"

    if app_root.is_dir():
        for source in app_root.rglob("*.cs"):
            content = read_text(source)
            relative = source.relative_to(root)
            if not re.search(r"^namespace WhiteRoom\.Novel\b", content, re.MULTILINE):
                errors.append(f"{relative} must use the WhiteRoom.Novel namespace")
            if "using System.Reflection;" in content:
                setup_root = app_root / "Setup"
                if setup_root not in source.parents:
                    errors.append(f"{relative} uses reflection outside Assets/Scripts/Setup")

    if package_root.is_dir():
        for source in package_root.rglob("*.cs"):
            if re.search(r"\bWhiteRoom\.Novel\b", read_text(source)):
                errors.append(
                    f"{source.relative_to(root)} creates a reverse dependency on WhiteRoom.Novel"
                )
    return errors


def check_pr_event(event_path: Path) -> list[str]:
    try:
        event = json.loads(read_text(event_path))
    except (OSError, json.JSONDecodeError) as exc:
        return [f"cannot read GitHub event {event_path}: {exc}"]

    pull_request = event.get("pull_request")
    if pull_request is None:
        return []

    body = pull_request.get("body") or ""
    if not ISSUE_REFERENCE.search(body):
        return [
            "pull request body must reference an Issue with "
            "'Closes #123', 'Refs #123', or an Issue URL"
        ]
    return []


def validate(root: Path, pr_event: Path | None = None) -> list[str]:
    checks = (
        check_required_paths,
        check_adrs,
        check_artifact_contracts,
        check_markdown_links,
        check_architecture_boundaries,
    )
    errors = [error for check in checks for error in check(root)]
    if pr_event is not None:
        errors.extend(check_pr_event(pr_event))
    return errors


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--root",
        type=Path,
        default=Path.cwd(),
        help="repository root (default: current directory)",
    )
    parser.add_argument(
        "--pr-event",
        type=Path,
        help="optional GITHUB_EVENT_PATH used to validate PR Issue traceability",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    root = args.root.resolve()
    errors = validate(root, args.pr_event)
    if errors:
        for error in errors:
            print(f"ERROR: {error}", file=sys.stderr)
        print(f"Governance validation failed with {len(errors)} error(s).", file=sys.stderr)
        return 1

    print("Governance validation passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
