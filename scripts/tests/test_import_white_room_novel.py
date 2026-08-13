import importlib.util
import json
import pathlib
import sys
import tempfile
import unittest


ROOT = pathlib.Path(__file__).resolve().parents[2]
MODULE_PATH = ROOT / "scripts" / "import_white_room_novel.py"
SPEC = importlib.util.spec_from_file_location("import_white_room_novel", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


class ImportWhiteRoomNovelTests(unittest.TestCase):
    def test_reviewed_speaker_fixture(self):
        fixture_path = (
            ROOT / "scripts" / "tests" / "fixtures" / "white_room_speaker_cases.json"
        )
        cases = json.loads(fixture_path.read_text(encoding="utf-8"))

        for case_number, case in enumerate(cases):
            with self.subTest(case=case["name"]):
                chapter = [
                    MODULE.SourceParagraph(text, 50_000 + index, 1)
                    for index, text in enumerate(case["paragraphs"])
                ]
                MODULE.infer_speakers(chapter)
                target = chapter[case.get("targetOffset", 0)]
                self.assertEqual(target.speaker, case["expectedSpeaker"])
                if "expectedEvidence" in case:
                    self.assertEqual(
                        target.speaker_evidence,
                        case["expectedEvidence"],
                    )
                if "expectedReason" in case:
                    self.assertEqual(
                        target.unresolved_reason,
                        case["expectedReason"],
                    )

    def test_groups_short_narration_but_preserves_named_dialogue(self):
        chapter = [
            MODULE.SourceParagraph("第一章　答えのない問い", 0, 1),
            MODULE.SourceParagraph("雨。", 1, 1),
            MODULE.SourceParagraph("暗い。", 2, 1),
            MODULE.SourceParagraph("「行こう」", 3, 1, "ナギ"),
        ]

        grouped = MODULE.group_for_message_window(chapter)

        self.assertEqual([item.text for item in grouped], [
            "第一章　答えのない問い",
            "雨。暗い。",
            "「行こう」",
        ])
        self.assertEqual(grouped[-1].speaker, "ナギ")

    def test_removes_only_attribution_that_nameplate_replaces(self):
        quote = MODULE.SourceParagraph("「行こう」", 0, 1, "ナギ")

        self.assertTrue(MODULE.is_redundant_attribution("ナギが言った。", quote))
        self.assertFalse(MODULE.is_redundant_attribution("ナギは苦い顔をする。", quote))

    def test_explicit_speaker_requires_a_speech_verb_when_requested(self):
        self.assertEqual(MODULE.explicit_speaker("レイが答えた。", True), "レイ")
        self.assertEqual(MODULE.explicit_speaker("レイが扉を開けた。", True), "")

    def test_csv_contract_has_stable_headers(self):
        self.assertEqual(MODULE.HEADERS[0:4], ("Id", "Speaker", "Text", "NextId"))
        self.assertEqual(MODULE.HEADERS[-5:], ("Background", "Bgm", "Se", "Voice", "Characters"))

    def test_known_conversation_adjustment_duplicates_are_removed(self):
        duplicate = next(iter(MODULE.REDUNDANT_PARAGRAPHS))
        chapter = [
            MODULE.SourceParagraph("前の台詞", 0, 1),
            MODULE.SourceParagraph(duplicate, 1, 1),
            MODULE.SourceParagraph("次の台詞", 2, 1),
        ]

        grouped = MODULE.group_for_message_window(chapter)

        self.assertNotIn(duplicate, [item.text for item in grouped])

    def test_character_action_is_not_treated_as_speech_attribution(self):
        chapter = [
            MODULE.SourceParagraph("「隣の区域は？」", 100, 14),
            MODULE.SourceParagraph("少年が首を振る。", 101, 14),
            MODULE.SourceParagraph("「送電容量が足りない」", 102, 14),
        ]

        MODULE.infer_speakers(chapter)

        self.assertEqual(chapter[0].speaker, "")
        self.assertEqual(
            chapter[0].unresolved_reason,
            "single_nearby_character_but_no_speech_attribution",
        )

    def test_explicit_speech_tag_is_high_confidence(self):
        chapter = [
            MODULE.SourceParagraph("「まだ答えません」", 100, 12),
            MODULE.SourceParagraph("レイが言う。", 101, 12),
        ]

        MODULE.infer_speakers(chapter)

        self.assertEqual(chapter[0].speaker, "レイ")
        self.assertEqual(chapter[0].speaker_confidence, "high")
        self.assertEqual(chapter[0].speaker_evidence, "explicit_attribution")

    def test_standalone_name_attributes_normal_but_not_remote_dialogue(self):
        chapter = [
            MODULE.SourceParagraph("「時間、十一分」", 100, 14),
            MODULE.SourceParagraph("ナギ。", 101, 14),
            MODULE.SourceParagraph("『薬が来なくなって三人死んだ』", 102, 14),
            MODULE.SourceParagraph("母親。", 103, 14),
            MODULE.SourceParagraph("『息子は登録が切れた』", 104, 14),
        ]

        MODULE.infer_speakers(chapter)

        self.assertEqual(chapter[0].speaker, "ナギ")
        self.assertEqual(chapter[2].speaker, "")
        self.assertEqual(chapter[4].speaker, "")

    def test_remote_media_cue_and_channel_continuity_are_auditable(self):
        chapter = [
            MODULE.SourceParagraph("画面にユイが映った。", 100, 14),
            MODULE.SourceParagraph("『何でレイがいるの』", 101, 14),
            MODULE.SourceParagraph("レイは画面を見る。", 102, 14),
            MODULE.SourceParagraph("『そっちも？』", 103, 14),
        ]

        MODULE.infer_speakers(chapter)

        self.assertEqual(chapter[1].speaker, "ユイ")
        self.assertEqual(chapter[1].speaker_evidence, "remote_media_cue")
        self.assertEqual(chapter[3].speaker, "ユイ")
        self.assertEqual(chapter[3].speaker_confidence, "medium")
        self.assertEqual(chapter[3].speaker_evidence, "remote_channel_continuity")

    def test_speaker_audit_is_source_indexed_and_deterministic(self):
        chapter = [
            MODULE.SourceParagraph("「答えます」", 10, 1),
            MODULE.SourceParagraph("レイが言った。", 11, 1),
            MODULE.SourceParagraph("「誰？」", 12, 1),
        ]
        MODULE.infer_speakers(chapter)
        rows = [
            MODULE.DialogueRow(
                id=MODULE.FIRST_DIALOGUE_ID,
                speaker="レイ",
                text="答えます",
                source_indices=(10,),
            ),
            MODULE.DialogueRow(
                id=MODULE.FIRST_DIALOGUE_ID + 1,
                speaker="地の文",
                text="「誰？」",
                source_indices=(12,),
            ),
        ]

        with tempfile.TemporaryDirectory() as directory:
            first = pathlib.Path(directory) / "first.json"
            second = pathlib.Path(directory) / "second.json"
            MODULE.write_speaker_audit(first, "source.docx", [chapter], rows)
            MODULE.write_speaker_audit(second, "source.docx", [chapter], rows)

            self.assertEqual(first.read_bytes(), second.read_bytes())
            document = json.loads(first.read_text(encoding="utf-8"))

        self.assertEqual(document["summary"]["quotedParagraphs"], 2)
        self.assertEqual(document["summary"]["named"], 1)
        self.assertEqual(document["summary"]["unresolved"], 1)
        self.assertEqual(document["entries"][0]["sourceIndex"], 10)
        self.assertEqual(document["entries"][0]["dialogueId"], MODULE.FIRST_DIALOGUE_ID)


if __name__ == "__main__":
    unittest.main()
