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


class UnityMetadataTests(unittest.TestCase):
    def test_accepts_files_and_directories_with_metadata(self) -> None:
        paths = [
            "Assets/Scenes.meta",
            "Assets/Scenes/Title.unity",
            "Assets/Scenes/Title.unity.meta",
        ]
        self.assertEqual([], cross_platform.find_unity_meta_issues(paths))

    def test_rejects_missing_file_metadata(self) -> None:
        errors = cross_platform.find_unity_meta_issues(
            ["Assets/Scenes.meta", "Assets/Scenes/Title.unity"]
        )
        self.assertTrue(any("Title.unity.meta" in error for error in errors))

    def test_rejects_missing_directory_metadata(self) -> None:
        errors = cross_platform.find_unity_meta_issues(
            ["Assets/Scenes/Title.unity", "Assets/Scenes/Title.unity.meta"]
        )
        self.assertTrue(any("Assets/Scenes.meta" in error for error in errors))

    def test_rejects_orphaned_metadata(self) -> None:
        errors = cross_platform.find_unity_meta_issues(["Assets/Class.meta"])
        self.assertEqual(
            ["Assets/Class.meta: orphaned Unity metadata has no target Assets/Class"],
            errors,
        )

    def test_rejects_duplicate_unity_guids(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            scenes = root / "Assets" / "Scenes"
            scenes.mkdir(parents=True)
            duplicate_guid = "0123456789abcdef0123456789abcdef"
            (scenes / "Title.unity.meta").write_text(
                f"fileFormatVersion: 2\nguid: {duplicate_guid}\n",
                encoding="utf-8",
            )
            (scenes / "Main.unity.meta").write_text(
                f"fileFormatVersion: 2\nguid: {duplicate_guid}\n",
                encoding="utf-8",
            )
            errors = cross_platform.find_unity_guid_issues(
                root,
                [
                    "Assets/Scenes/Title.unity.meta",
                    "Assets/Scenes/Main.unity.meta",
                ],
            )
            self.assertEqual(1, len(errors))
            self.assertIn("duplicates Unity guid", errors[0])


class BuildSettingsTests(unittest.TestCase):
    def write_build_settings(
        self,
        root: Path,
        *,
        title_enabled: bool = True,
        main_enabled: bool = True,
    ) -> None:
        settings = root / "ProjectSettings"
        settings.mkdir()
        scenes = root / "Assets" / "Scenes"
        scenes.mkdir(parents=True)
        title_guid = "11111111111111111111111111111111"
        main_guid = "22222222222222222222222222222222"
        (scenes / "Title.unity.meta").write_text(
            f"fileFormatVersion: 2\nguid: {title_guid}\n",
            encoding="utf-8",
        )
        (scenes / "Main.unity.meta").write_text(
            f"fileFormatVersion: 2\nguid: {main_guid}\n",
            encoding="utf-8",
        )
        (settings / "EditorBuildSettings.asset").write_text(
            "EditorBuildSettings:\n"
            "  m_Scenes:\n"
            f"  - enabled: {int(title_enabled)}\n"
            "    path: Assets/Scenes/Title.unity\n"
            f"    guid: {title_guid}\n"
            f"  - enabled: {int(main_enabled)}\n"
            "    path: Assets/Scenes/Main.unity\n"
            f"    guid: {main_guid}\n",
            encoding="utf-8",
        )

    def test_accepts_enabled_title_and_main_scenes(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.write_build_settings(root)
            self.assertEqual(
                [],
                cross_platform.find_build_setting_issues(root),
            )

    def test_rejects_disabled_required_scene(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.write_build_settings(root, main_enabled=False)
            errors = cross_platform.find_build_setting_issues(root)
            self.assertEqual(1, len(errors))
            self.assertIn("Main.unity", errors[0])

    def test_rejects_scene_guid_mismatch(self) -> None:
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            self.write_build_settings(root)
            settings = root / "ProjectSettings" / "EditorBuildSettings.asset"
            content = settings.read_text(encoding="utf-8")
            settings.write_text(
                content.replace(
                    "guid: 22222222222222222222222222222222",
                    "guid: 33333333333333333333333333333333",
                ),
                encoding="utf-8",
            )
            errors = cross_platform.find_build_setting_issues(root)
            self.assertEqual(1, len(errors))
            self.assertIn("expected 22222222222222222222222222222222", errors[0])


if __name__ == "__main__":
    unittest.main()
