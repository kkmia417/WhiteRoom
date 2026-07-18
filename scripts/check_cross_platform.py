"""Validate repository rules that keep Windows and macOS checkouts compatible."""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
import unicodedata
from pathlib import Path
from typing import Iterable, Mapping, Sequence


BINARY_SUFFIXES = frozenset(
    {
        ".7z",
        ".aif",
        ".aiff",
        ".avi",
        ".bin",
        ".blend",
        ".bmp",
        ".bundle",
        ".dll",
        ".dylib",
        ".exe",
        ".exr",
        ".fbx",
        ".flac",
        ".gif",
        ".gz",
        ".hdr",
        ".ico",
        ".jpeg",
        ".jpg",
        ".m4a",
        ".mov",
        ".mp3",
        ".mp4",
        ".ogg",
        ".otf",
        ".pdf",
        ".png",
        ".psb",
        ".psd",
        ".so",
        ".tga",
        ".tif",
        ".tiff",
        ".ttf",
        ".unitypackage",
        ".wav",
        ".webm",
        ".webp",
        ".woff",
        ".woff2",
        ".zip",
    }
)
WINDOWS_COMMAND_SUFFIXES = frozenset({".bat", ".cmd"})
WINDOWS_FORBIDDEN_CHARACTERS = frozenset('<>:"\\|?*')
WINDOWS_RESERVED_NAME = re.compile(
    r"^(?:CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])(?:\..*)?$",
    re.IGNORECASE,
)


class GitCommandError(RuntimeError):
    """Raised when a required Git query cannot be completed."""


def run_git(root: Path, arguments: Sequence[str], stdin: bytes | None = None) -> bytes:
    result = subprocess.run(
        ["git", "-C", str(root), *arguments],
        input=stdin,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
    )
    if result.returncode != 0:
        detail = result.stderr.decode("utf-8", errors="replace").strip()
        raise GitCommandError(f"git {' '.join(arguments)} failed: {detail}")
    return result.stdout


def repository_paths(root: Path) -> list[str]:
    output = run_git(
        root,
        ["ls-files", "--cached", "--others", "--exclude-standard", "-z"],
    )
    return sorted(
        path
        for path in output.decode("utf-8", errors="surrogateescape").split("\0")
        if path and (root / path).exists()
    )


def find_path_issues(paths: Iterable[str]) -> list[str]:
    issues: list[str] = []
    portable_names: dict[str, str] = {}

    for path in sorted(paths):
        normalized_path = unicodedata.normalize("NFC", path)
        if normalized_path != path:
            issues.append(f"{path}: path must use Unicode NFC normalization")

        portable_key = normalized_path.casefold()
        previous = portable_names.get(portable_key)
        if previous is not None and previous != path:
            issues.append(
                f"{path}: collides with {previous} after Unicode normalization "
                "and case folding"
            )
        else:
            portable_names[portable_key] = path

        for component in path.split("/"):
            if not component or component in {".", ".."}:
                issues.append(f"{path}: contains an empty or relative path component")
                continue
            if component.endswith((" ", ".")):
                issues.append(
                    f"{path}: path component {component!r} ends with a space or dot"
                )
            if WINDOWS_RESERVED_NAME.fullmatch(component):
                issues.append(
                    f"{path}: path component {component!r} is reserved on Windows"
                )
            invalid = sorted(
                {
                    character
                    for character in component
                    if character in WINDOWS_FORBIDDEN_CHARACTERS
                    or ord(character) < 32
                }
            )
            if invalid:
                rendered = ", ".join(repr(character) for character in invalid)
                issues.append(
                    f"{path}: path component {component!r} contains "
                    f"Windows-incompatible character(s): {rendered}"
                )

    return issues


def parse_attribute_output(output: bytes) -> dict[str, dict[str, str]]:
    fields = output.decode("utf-8", errors="surrogateescape").split("\0")
    if fields and fields[-1] == "":
        fields.pop()
    if len(fields) % 3:
        raise GitCommandError("git check-attr returned an unexpected response")

    attributes: dict[str, dict[str, str]] = {}
    for index in range(0, len(fields), 3):
        path, attribute, value = fields[index : index + 3]
        attributes.setdefault(path, {})[attribute] = value
    return attributes


def repository_attributes(
    root: Path,
    paths: Sequence[str],
) -> dict[str, dict[str, str]]:
    if not paths:
        return {}
    stdin = b"".join(
        path.encode("utf-8", errors="surrogateescape") + b"\0" for path in paths
    )
    output = run_git(root, ["check-attr", "-z", "--stdin", "text", "eol"], stdin)
    return parse_attribute_output(output)


def find_attribute_issues(
    paths: Iterable[str],
    attributes: Mapping[str, Mapping[str, str]],
) -> list[str]:
    issues: list[str] = []
    for path in sorted(paths):
        values = attributes.get(path, {})
        text = values.get("text", "unspecified")
        eol = values.get("eol", "unspecified")
        suffix = Path(path).suffix.lower()

        if suffix in BINARY_SUFFIXES:
            if text != "unset":
                issues.append(
                    f"{path}: binary extension {suffix} must have the text "
                    "attribute unset"
                )
            continue

        if suffix in WINDOWS_COMMAND_SUFFIXES:
            if text != "set" or eol != "crlf":
                issues.append(
                    f"{path}: Windows command files must use text eol=crlf "
                    f"(found text={text}, eol={eol})"
                )
            continue

        if text not in {"auto", "set"} or eol != "lf":
            issues.append(
                f"{path}: text files must use LF normalization "
                f"(found text={text}, eol={eol})"
            )
    return issues


def find_unity_meta_issues(paths: Iterable[str]) -> list[str]:
    asset_paths = {
        path
        for path in paths
        if path.startswith("Assets/") and path != "Assets"
    }
    asset_directories: set[str] = set()
    for path in asset_paths:
        target = path.removesuffix(".meta")
        parent = Path(target).parent
        while parent.as_posix() != "Assets":
            asset_directories.add(parent.as_posix())
            parent = parent.parent

    issues: list[str] = []
    targets = {
        path
        for path in asset_paths
        if not path.endswith(".meta")
    }
    targets.update(asset_directories)

    for target in sorted(targets):
        meta = f"{target}.meta"
        if meta not in asset_paths:
            issues.append(f"{target}: missing Unity metadata file {meta}")

    for meta in sorted(path for path in asset_paths if path.endswith(".meta")):
        target = meta.removesuffix(".meta")
        if target not in targets:
            issues.append(f"{meta}: orphaned Unity metadata has no target {target}")

    return issues


def read_unity_guid(path: Path) -> str | None:
    try:
        content = path.read_text(encoding="utf-8-sig")
    except OSError:
        return None
    match = re.search(r"^guid:\s*([0-9a-fA-F]{32})\s*$", content, re.MULTILINE)
    return match.group(1).lower() if match else None


def find_unity_guid_issues(root: Path, paths: Iterable[str]) -> list[str]:
    issues: list[str] = []
    owners: dict[str, str] = {}
    meta_paths = sorted(
        path
        for path in paths
        if path.startswith("Assets/") and path.endswith(".meta")
    )
    for meta in meta_paths:
        guid = read_unity_guid(root / meta)
        if guid is None:
            issues.append(f"{meta}: missing or invalid Unity guid")
            continue
        previous = owners.get(guid)
        if previous is not None:
            issues.append(f"{meta}: duplicates Unity guid {guid} from {previous}")
        else:
            owners[guid] = meta
    return issues


def find_unity_setting_issues(root: Path) -> list[str]:
    expected = {
        Path("ProjectSettings/EditorSettings.asset"): "m_SerializationMode: 2",
        Path("ProjectSettings/VersionControlSettings.asset"): (
            "m_Mode: Visible Meta Files"
        ),
    }
    issues: list[str] = []
    for relative, required_line in expected.items():
        path = root / relative
        try:
            content = path.read_text(encoding="utf-8")
        except OSError as error:
            issues.append(f"{relative.as_posix()}: cannot read Unity setting: {error}")
            continue
        if required_line not in content:
            issues.append(
                f"{relative.as_posix()}: expected {required_line!r} for "
                "mergeable Unity assets"
            )
    return issues


def find_build_setting_issues(root: Path) -> list[str]:
    relative = Path("ProjectSettings/EditorBuildSettings.asset")
    path = root / relative
    try:
        content = path.read_text(encoding="utf-8")
    except OSError as error:
        return [f"{relative.as_posix()}: cannot read Unity setting: {error}"]

    issues: list[str] = []
    for scene in ("Title", "Main"):
        entry = re.compile(
            rf"- enabled: 1\s+path: Assets/Scenes/{scene}\.unity\s+"
            rf"guid: ([0-9a-fA-F]{{32}})(?:\s|$)"
        )
        match = entry.search(content)
        if match is None:
            issues.append(
                f"{relative.as_posix()}: Assets/Scenes/{scene}.unity "
                "must be enabled in Build Settings"
            )
            continue

        meta = Path(f"Assets/Scenes/{scene}.unity.meta")
        scene_guid = read_unity_guid(root / meta)
        if scene_guid is None:
            issues.append(f"{meta.as_posix()}: missing or invalid Unity guid")
        elif match.group(1).lower() != scene_guid:
            issues.append(
                f"{relative.as_posix()}: Assets/Scenes/{scene}.unity uses guid "
                f"{match.group(1).lower()}, expected {scene_guid}"
            )
    return issues


def validate(root: Path) -> tuple[list[str], int]:
    paths = repository_paths(root)
    issues = find_path_issues(paths)
    issues.extend(find_attribute_issues(paths, repository_attributes(root, paths)))
    issues.extend(find_unity_meta_issues(paths))
    issues.extend(find_unity_guid_issues(root, paths))
    issues.extend(find_unity_setting_issues(root))
    issues.extend(find_build_setting_issues(root))
    return issues, len(paths)


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--root",
        type=Path,
        default=Path.cwd(),
        help="repository root (default: current directory)",
    )
    return parser.parse_args()


def main() -> int:
    root = parse_args().root.resolve()
    try:
        issues, path_count = validate(root)
    except GitCommandError as error:
        print(f"ERROR: {error}", file=sys.stderr)
        return 1

    if issues:
        for issue in issues:
            print(f"ERROR: {issue}", file=sys.stderr)
        print(
            f"Cross-platform validation failed with {len(issues)} error(s).",
            file=sys.stderr,
        )
        return 1

    print(f"Cross-platform validation passed for {path_count} repository files.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
