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
        quote.speaker_evidence = "explicit_attribution"

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
            MODULE.SourceParagraph("レイが言った。", 101, 12),
        ]

        MODULE.infer_speakers(chapter)

        self.assertEqual(chapter[0].speaker, "レイ")
        self.assertEqual(chapter[0].speaker_confidence, "high")
        self.assertEqual(chapter[0].speaker_evidence, "explicit_attribution")

    def test_present_tense_speech_tag_introduces_the_following_quote(self):
        chapter = [
            MODULE.SourceParagraph("「初めて？」", 100, 2),
            MODULE.SourceParagraph("レイは空を見たまま答える。", 101, 2),
            MODULE.SourceParagraph("「はい」", 102, 2),
        ]

        MODULE.complete_speaker_assignments([chapter])

        self.assertNotEqual(chapter[0].speaker, "レイ")
        self.assertEqual(chapter[2].speaker, "レイ")

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
        self.assertEqual(document["entries"][0]["dialogueIds"], [MODULE.FIRST_DIALOGUE_ID])

    def test_splits_long_turns_without_changing_the_published_row_id(self):
        row = MODULE.DialogueRow(
            id=MODULE.FIRST_DIALOGUE_ID,
            speaker="レイ",
            text="これは会話ウィンドウへ収めるために分割される、とても長い台詞です。"
                 "一度のクリックで読み切れる長さを守ります。",
            next_id=MODULE.FIRST_DIALOGUE_ID + 1,
        )

        rows = MODULE.split_rows_for_window([row])

        self.assertEqual(rows[0].id, MODULE.FIRST_DIALOGUE_ID)
        self.assertEqual(rows[-1].next_id, MODULE.FIRST_DIALOGUE_ID + 1)
        self.assertTrue(all(MODULE.visible_text_length(item.text) <= 40 for item in rows))

    def test_split_prefers_complete_sentences_over_mechanical_target_length(self):
        text = (
            "道路には水が溜まり、遠くで何かが燃えている。"
            "黒い煙の向こうに、青白く光る塔が一本だけ立っていた。"
        )

        fragments = MODULE.split_text_for_window(text)

        self.assertEqual(fragments, [
            "道路には水が溜まり、遠くで何かが燃えている。",
            "黒い煙の向こうに、青白く光る塔が一本だけ立っていた。",
        ])

    def test_long_single_sentence_marks_continuation_without_comma_ending(self):
        text = "評価のためだけに体調を隠してはいけません。あなたは優秀ですが、判断が遅れるほど危険が増えていきます。"

        fragments = MODULE.split_text_for_window(text)

        self.assertTrue(all(len(fragment) <= 40 for fragment in fragments))
        self.assertFalse(any(fragment.endswith("、") for fragment in fragments))

    def test_reviewed_sound_and_redundant_reply_are_adapted_for_game_pacing(self):
        chapter = [
            MODULE.SourceParagraph("夢の中なのに、痛みだけは妙にはっきりしていた。", 30, 1),
            MODULE.SourceParagraph("銃声。", 31, 1),
            MODULE.SourceParagraph("「ないよ」", 82, 1, "少女"),
            MODULE.SourceParagraph("「何が」", 83, 1, "レイ"),
            MODULE.SourceParagraph("「正解」", 84, 1, "少女"),
        ]

        grouped = MODULE.group_for_message_window(chapter)

        self.assertEqual([item.text for item in grouped], [
            "夢の中なのに、痛みだけは妙にはっきりしていた。",
            "パンッ！",
            "「ないよ」",
            "「正解」",
        ])
        self.assertEqual(chapter[3].omission_reason, "reviewed_dialogue_pacing")

    def test_reviewed_opening_prose_replaces_terse_fragment_chain(self):
        chapter = [
            MODULE.SourceParagraph("壊れた街を、音が埋め尽くしていた。", 22, 1),
            MODULE.SourceParagraph("傾いた高架。", 23, 1),
            MODULE.SourceParagraph("窓の抜け落ちた建物。", 24, 1),
        ]

        grouped = MODULE.group_for_message_window(chapter)

        self.assertEqual(
            [item.text for item in grouped],
            ["傾いた高架の先に、窓を失った建物が並ぶ。"],
        )
        self.assertEqual(chapter[1].omission_reason, "reviewed_prose_merge")
        self.assertEqual(chapter[2].omission_reason, "reviewed_prose_merge")

    def test_conversation_stage_keeps_both_speakers_and_placeholder(self):
        chapter = [
            MODULE.SourceParagraph("「本気ですね」", 47, 1, "レイ"),
            MODULE.SourceParagraph("「そっちがでしょ」", 48, 1, "少女"),
        ]

        partner = MODULE.conversation_partner(chapter, chapter[0])
        directives = MODULE.character_stage_directives(chapter[0].speaker, partner)

        self.assertEqual(partner, "少女")
        self.assertEqual(
            directives,
            "*|Rei@left:blank|PlaceholderRight@right:neutral",
        )

    def test_conversation_between_two_missing_characters_uses_two_placeholders(self):
        directives = MODULE.character_stage_directives("アサヒ", "ユイ")

        self.assertEqual(
            directives,
            "*|PlaceholderLeft@left:neutral|PlaceholderRight@right:neutral",
        )

    def test_reviewed_source_assignment_overrides_stale_ledger_value(self):
        chapter = [
            MODULE.SourceParagraph("「即答なんだ」", 53, 1),
        ]

        MODULE.complete_speaker_assignments([chapter], {53: "レイ"})

        self.assertEqual(chapter[0].speaker, "少女")
        self.assertEqual(chapter[0].speaker_evidence, "reviewed_source_override")


if __name__ == "__main__":
    unittest.main()
