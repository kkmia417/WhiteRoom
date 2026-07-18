from __future__ import annotations

import sys
import tempfile
import unittest
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parents[1]
REPOSITORY_ROOT = SCRIPT_DIR.parent
sys.path.insert(0, str(SCRIPT_DIR))

import check_cross_platform as cross_platform


class PortablePathTests(unittest.TestCase):
    def test_accepts_portable_paths(self) -> None:
        paths = [
            "Assets/Scenes/Title.unity",
            "Assets/物語/第一章.csv",
            "scripts/check_cross_platform.py",
        ]
        self.assertEqual([], cross_platform.find_path_issues(paths))

    def test_rejects_case_insensitive_collision(self) -> None:
        errors = cross_platform.find_path_issues(
            ["Assets/Scenes/Title.unity", "assets/scenes/title.unity"]
        )
        self.assertTrue(any("collides with" in error for error in errors))

    def test_rejects_non_nfc_unicode(self) -> None:
        errors = cross_platform.find_path_issues(["docs/Cafe\u0301.md"])
        self.assertTrue(any("Unicode NFC" in error for error in errors))

    def test_rejects_windows_incompatible_components(self) -> None:
        invalid_paths = [
            "Assets/CON.txt",
            "Assets/trailing.",
            "Assets/invalid:name.txt",
            "Assets/back\\slash.txt",
        ]
        errors = cross_platform.find_path_issues(invalid_paths)
        for path in invalid_paths:
            with self.subTest(path=path):
                self.assertTrue(any(error.startswith(path) for error in errors))


class AttributePolicyTests(unittest.TestCase):
    def test_binary_suffix_contract_matches_gitattributes(self) -> None:
        rules = (REPOSITORY_ROOT / ".gitattributes").read_text(
            encoding="utf-8"
        ).splitlines()
        binary_suffixes = frozenset(
            fields[0].removeprefix("*")
            for line in rules
            if len(fields := line.split()) == 2 and fields[1] == "binary"
        )
        self.assertEqual(cross_platform.BINARY_SUFFIXES, binary_suffixes)

    def test_windows_command_contract_matches_gitattributes(self) -> None:
        rules = (REPOSITORY_ROOT / ".gitattributes").read_text(
            encoding="utf-8"
        ).splitlines()
        command_suffixes = frozenset(
            fields[0].removeprefix("*")
            for line in rules
            if len(fields := line.split()) == 3
            and fields[1:] == ["text", "eol=crlf"]
        )
        self.assertEqual(
            cross_platform.WINDOWS_COMMAND_SUFFIXES,
            command_suffixes,
        )

    def test_parses_null_delimited_git_output(self) -> None:
        output = (
            b"README.md\0text\0auto\0"
            b"README.md\0eol\0lf\0"
            b"image.png\0text\0unset\0"
            b"image.png\0eol\0lf\0"
        )
        self.assertEqual(
            {
                "README.md": {"text": "auto", "eol": "lf"},
                "image.png": {"text": "unset", "eol": "lf"},
            },
            cross_platform.parse_attribute_output(output),
        )

    def test_rejects_incorrect_text_and_binary_attributes(self) -> None:
        paths = ["README.md", "image.png", "setup.cmd"]
        attributes = {
            "README.md": {"text": "auto", "eol": "crlf"},
            "image.png": {"text": "auto", "eol": "lf"},
            "setup.cmd": {"text": "set", "eol": "lf"},
        }
        errors = cross_platform.find_attribute_issues(paths, attributes)
        self.assertEqual(3, len(errors))

    def test_repository_attributes_cover_representative_files(self) -> None:
        paths = [
            ".gitattributes",
            "README.md",
            "Assets/Scripts/NovelGameBootstrap.cs",
            "Assets/Scenes/Main.unity",
            "Assets/WhiteRoom_UI.png",
            "Assets/Resources/Fonts/LogoTypeGothicCondense/"
            "LogoTypeGothicCondense.otf",
        ]
        attributes = cross_platform.repository_attributes(REPOSITORY_ROOT, paths)
        self.assertEqual(
            [],
            cross_platform.find_attribute_issues(paths, attributes),
        )


class UnitySettingsTests(unittest.TestCase):
    def test_accepts_force_text_and_visible_meta_files(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            settings = root / "ProjectSettings"
            settings.mkdir()
            (settings / "EditorSettings.asset").write_text(
                "EditorSettings:\n  m_SerializationMode: 2\n",
                encoding="utf-8",
            )
            (settings / "VersionControlSettings.asset").write_text(
                "VersionControlSettings:\n  m_Mode: Visible Meta Files\n",
                encoding="utf-8",
            )
            self.assertEqual(
                [],
                cross_platform.find_unity_setting_issues(root),
            )

    def test_rejects_non_mergeable_unity_settings(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            settings = root / "ProjectSettings"
            settings.mkdir()
            (settings / "EditorSettings.asset").write_text(
                "EditorSettings:\n  m_SerializationMode: 0\n",
                encoding="utf-8",
            )
            (settings / "VersionControlSettings.asset").write_text(
                "VersionControlSettings:\n  m_Mode: Hidden Meta Files\n",
                encoding="utf-8",
            )
            errors = cross_platform.find_unity_setting_issues(root)
            self.assertEqual(2, len(errors))


if __name__ == "__main__":
    unittest.main()
