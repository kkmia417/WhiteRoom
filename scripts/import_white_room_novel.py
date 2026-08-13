#!/usr/bin/env python3
"""Convert the WhiteRoom chapter 1-14 manuscript DOCX into Talk System CSV.

The importer deliberately performs only conservative novel-to-VN adaptation:
short narration beats are grouped into readable message-window units, explicit
speech attributions become nameplate speakers, scene dividers are removed, and
two reviewed choice points are inserted. Ambiguous dialogue remains quoted
narration and is emitted to a source-indexed audit instead of being guessed.
The source manuscript remains the authority for prose and chapter order.
"""

from __future__ import annotations

import argparse
import csv
import json
import re
import zipfile
from dataclasses import dataclass, field
from pathlib import Path
from typing import Iterable, Sequence
from xml.etree import ElementTree


HEADERS = (
    "Id",
    "Speaker",
    "Text",
    "NextId",
    "EmotionKey",
    "TriggerKey",
    "ConditionKey",
    "EventKey",
    "Choices",
    "AutoNextSeconds",
    "ChapterKey",
    "RouteKey",
    "EndingKey",
    "Background",
    "Bgm",
    "Se",
    "Voice",
    "Characters",
)

# The retired prototype published IDs 1-880. The new manuscript uses a disjoint
# range so loading an old save fails safely instead of restoring unrelated prose.
FIRST_DIALOGUE_ID = 1_000_001

CHAPTER_TITLES = (
    "第一章　答えのない問い",
    "第二章　雨の向こう側",
    "第三章　切り捨てられた場所",
    "第四章　バディの条件",
    "第五章　正しい身分",
    "第六章　生きている",
    "第七章　知らない依頼人",
    "第八章　誰のための怒り",
    "第九章　誰が決めている",
    "第十章　誰にも聞けない",
    "第十一章　声の持ち主",
    "第十二章　予測された未来",
    "第十三章　正解の外側",
    "第十四章　それでも、決める",
)

KNOWN_SPEAKERS = (
    "レイ",
    "ナギ",
    "アサヒ",
    "ユイ",
    "教官",
    "職員",
    "少年",
    "班長",
    "運転手",
    "女性",
    "男",
    "老婆",
    "少女",
    "指導者",
    "老人",
    "警備員",
    "主任",
    "研究員",
    "医師",
    "母親",
)

SPEECH_VERBS = (
    "言った",
    "言う",
    "聞いた",
    "聞く",
    "訊いた",
    "訊く",
    "答えた",
    "答える",
    "呟いた",
    "呟く",
    "つぶやいた",
    "つぶやく",
    "叫んだ",
    "叫ぶ",
    "返した",
    "返す",
    "続けた",
    "続ける",
    "遮った",
    "尋ねた",
    "尋ねる",
    "問いかけた",
    "問いかける",
    "言い直した",
    "言い直す",
    "言い切った",
    "付け足した",
    "呼んだ",
    "呼ぶ",
)

REMOTE_CUE_MARKERS = (
    "画面に",
    "画面へ",
    "画面の",
    "回線",
    "通信",
    "映った",
    "映る",
    "声がした",
    "の声",
)
REMOTE_CHANNEL_RESET_PARAGRAPHS = {
    "全部。",
    "その次。",
    "次。",
    "別。",
    "通信越しに声が飛ぶ。",
    "一つにならない。",
}
REVIEWED_SPEAKER_OVERRIDES = {
    # Remote-call and montage identities are written as standalone prose in
    # the manuscript. These anchors were reviewed against their source context;
    # continuity may carry them only inside the bounded remote channel below.
    10954: "老人",
    10958: "医師",
    10962: "母親",
    13401: "ユイ",
    13411: "職員",
    13418: "老婆",
    13495: "アサヒ",
}
SYSTEM_CONTEXT_MARKERS = (
    "システム音声",
    "機械音声",
    "自動音声",
    "電子音",
    "館内音声",
    "スピーカー",
)

SCENE_DIVIDERS = {"＊　＊　＊", "＊＊＊", "* * *"}
REDUNDANT_PARAGRAPHS = {
    "「今日はレイが人、私が機械」",
    "「途中で逃げるとか、システム事故とか。誤差に近い」",
    "「もう普通に政府職員みたいになってるよ」",
}
QUOTE_PAIRS = {"「": "」", "『": "』"}
ESCAPE_CHOICE_TEXT = "捕まるのと、知らない穴。どっち？"
FINAL_CHOICE_ANCHOR = "今まで一度も疑っていなかった。"

CHAPTER_PRESENTATION = {
    1: ("outside_wall_night#fade:1.0", "tense_low#fade:1.0", "Rei@left:serious#fadein|Nagi@right:wary#fadein"),
    2: ("outside_wall_night#fade:1.0", "quiet_dark#fade:1.0", "Rei@left:blank#fadein|Nagi@right:soft#fadein"),
    3: ("outside_wall_night#fade:1.0", "quiet_dark", "Rei@left:blank|Nagi@right:serious"),
    4: ("maintenance_corridor#fade:1.0", "sterile_low#fade:1.0", "Rei@left:tired|Nagi@right:smile"),
    5: ("lab_room_white#fade:1.0", "sterile_low#fade:1.0", "Rei@left:blank|Nagi@right:focus"),
    6: ("maintenance_corridor#fade:1.0", "quiet_dark#fade:1.0", "Rei@left:blank|Nagi@right:serious"),
    7: ("outside_wall_night#fade:1.0", "tense_low#fade:1.0", "Rei@left:blank|Nagi@right:smile"),
    8: ("maintenance_corridor#fade:1.0", "tense_low#fade:1.0", "Rei@left:serious|Nagi@right:angry"),
    9: ("lab_room_white#fade:1.0", "sterile_low#fade:1.0", "Rei@left:blank|Nagi@right:focus"),
    10: ("maintenance_corridor#fade:1.0", "quiet_dark#fade:1.0", "Rei@left:lost|Nagi@right:wary"),
    11: ("outside_wall_night#fade:1.0", "tense_low#fade:1.0", "Rei@left:serious|Nagi@right:shadow"),
    12: ("lab_room_night#fade:1.0", "alarm_low#fade:1.0", "Rei@left:serious|Nagi@right:serious"),
    13: ("lab_room_alarm#cut", "alarm", "Rei@left:determined|Nagi@right:shocked"),
    14: ("maintenance_corridor#fade:1.0", "alarm_low#fade:1.0", "Rei@left:serious|Nagi@right:focus"),
}


@dataclass
class SourceParagraph:
    text: str
    source_index: int
    chapter: int
    speaker: str = ""
    speaker_confidence: str = ""
    speaker_evidence: str = ""
    unresolved_reason: str = ""


@dataclass
class DialogueRow:
    id: int
    speaker: str
    text: str
    next_id: int = -1
    emotion_key: str = ""
    trigger_key: str = ""
    condition_key: str = ""
    event_key: str = ""
    choices: str = ""
    auto_next_seconds: str = ""
    chapter_key: str = ""
    route_key: str = ""
    ending_key: str = ""
    background: str = ""
    bgm: str = ""
    se: str = ""
    voice: str = ""
    characters: str = ""
    source_texts: tuple[str, ...] = field(default_factory=tuple, repr=False)
    source_indices: tuple[int, ...] = field(default_factory=tuple, repr=False)

    def csv_values(self) -> list[object]:
        return [
            self.id,
            self.speaker,
            self.text,
            self.next_id,
            self.emotion_key,
            self.trigger_key,
            self.condition_key,
            self.event_key,
            self.choices,
            self.auto_next_seconds,
            self.chapter_key,
            self.route_key,
            self.ending_key,
            self.background,
            self.bgm,
            self.se,
            self.voice,
            self.characters,
        ]


def read_docx_paragraphs(path: Path) -> list[str]:
    namespace = {"w": "http://schemas.openxmlformats.org/wordprocessingml/2006/main"}
    with zipfile.ZipFile(path) as archive:
        root = ElementTree.fromstring(archive.read("word/document.xml"))

    paragraphs: list[str] = []
    for paragraph in root.findall(".//w:body/w:p", namespace):
        text = "".join(
            node.text or "" for node in paragraph.findall(".//w:t", namespace)
        ).strip()
        if text:
            paragraphs.append(text)
    return paragraphs


def extract_chapters(paragraphs: Sequence[str]) -> list[list[SourceParagraph]]:
    starts: list[int] = []
    for title in CHAPTER_TITLES:
        matches = [index for index, text in enumerate(paragraphs) if text == title]
        if not matches:
            raise ValueError(f"Missing chapter heading: {title}")
        # The DOCX begins with a table of contents containing the same headings.
        # The last occurrence is the authored chapter boundary.
        starts.append(matches[-1])

    if starts != sorted(starts):
        raise ValueError("Chapter headings are not in chapter 1-14 order")

    chapters: list[list[SourceParagraph]] = []
    for chapter, start in enumerate(starts, 1):
        end = starts[chapter] if chapter < len(starts) else len(paragraphs)
        chapters.append(
            [
                SourceParagraph(text, index, chapter)
                for index, text in enumerate(paragraphs[start:end], start)
            ]
        )
    return chapters


def is_quoted(text: str) -> bool:
    return bool(text) and text[0] in QUOTE_PAIRS and text.endswith(QUOTE_PAIRS[text[0]])


def strip_outer_quote(text: str) -> str:
    if is_quoted(text):
        return text[1:-1].strip()
    return text


def explicit_speaker(text: str, require_speech_verb: bool) -> str:
    for speaker in KNOWN_SPEAKERS:
        if text == speaker or text == speaker + "。":
            return "" if require_speech_verb else speaker
        if not (text.startswith(speaker + "が") or text.startswith(speaker + "は")):
            continue
        if not require_speech_verb or any(verb in text for verb in SPEECH_VERBS):
            return speaker
    return ""


def _set_speaker(
    paragraph: SourceParagraph,
    speaker: str,
    evidence: str,
    confidence: str = "high",
) -> None:
    paragraph.speaker = speaker
    paragraph.speaker_evidence = evidence
    paragraph.speaker_confidence = confidence
    paragraph.unresolved_reason = ""


def _speaker_names_in_text(text: str) -> list[str]:
    return [speaker for speaker in KNOWN_SPEAKERS if speaker in text]


def _remote_speaker_before(
    chapter: Sequence[SourceParagraph],
    quote_index: int,
) -> str:
    """Return a speaker explicitly introduced for an incoming remote line.

    This is intentionally limited to corner-bracket dialogue and remote-media
    cues. A normal action such as ``少年が首を振る`` is not a speech
    attribution and must never be used to label the preceding quote.
    """

    context = chapter[max(0, quote_index - 6) : quote_index]
    latest_cue = -1
    for position, paragraph in enumerate(context):
        if any(marker in paragraph.text for marker in REMOTE_CUE_MARKERS):
            latest_cue = position
    if latest_cue < 0:
        return ""

    candidates: list[str] = []
    for paragraph in context[latest_cue:]:
        for speaker in _speaker_names_in_text(paragraph.text):
            if speaker not in candidates:
                candidates.append(speaker)
    return candidates[0] if len(candidates) == 1 else ""


def _system_speaker(
    chapter: Sequence[SourceParagraph], quote_index: int
) -> tuple[str, str]:
    nearby = "".join(
        paragraph.text
        for paragraph in chapter[max(0, quote_index - 3) : quote_index + 4]
    )
    if "教官" in nearby:
        return "教官", "instructor_context"
    if any(marker in nearby for marker in SYSTEM_CONTEXT_MARKERS):
        return "システム音声", "system_context"
    return "", ""


def _unresolved_reason(
    chapter: Sequence[SourceParagraph], quote_index: int
) -> str:
    paragraph = chapter[quote_index]
    if paragraph.text.startswith("『"):
        return "remote_or_system_voice_without_explicit_identity"

    nearby_names: list[str] = []
    for context in chapter[max(0, quote_index - 4) : quote_index + 5]:
        if is_quoted(context.text):
            continue
        for speaker in _speaker_names_in_text(context.text):
            if speaker not in nearby_names:
                nearby_names.append(speaker)
    if len(nearby_names) > 1:
        return "multiple_nearby_speakers_without_attribution"
    if len(nearby_names) == 1:
        return "single_nearby_character_but_no_speech_attribution"
    return "no_explicit_speaker_in_context"


def infer_speakers(chapter: list[SourceParagraph]) -> None:
    quote_indices = [
        index for index, paragraph in enumerate(chapter) if is_quoted(paragraph.text)
    ]
    for paragraph in chapter:
        paragraph.speaker = ""
        paragraph.speaker_confidence = ""
        paragraph.speaker_evidence = ""
        paragraph.unresolved_reason = ""

    for position, index in enumerate(quote_indices):
        paragraph = chapter[index]
        next_quote_index = (
            quote_indices[position + 1]
            if position + 1 < len(quote_indices)
            else len(chapter)
        )

        reviewed_speaker = REVIEWED_SPEAKER_OVERRIDES.get(paragraph.source_index)
        if reviewed_speaker:
            _set_speaker(
                paragraph,
                reviewed_speaker,
                "reviewed_source_override",
            )

        # A past-tense speech tag before the next quote is the strongest source
        # evidence. Never treat an arbitrary action or a standalone name as
        # attribution.
        for context in chapter[index + 1 : min(next_quote_index, index + 6)]:
            if paragraph.speaker:
                break
            if context.text in SCENE_DIVIDERS:
                break
            speaker = explicit_speaker(context.text, True)
            if not speaker:
                voice_speakers = [
                    candidate
                    for candidate in KNOWN_SPEAKERS
                    if context.text.startswith(candidate + "の声")
                ]
                if len(voice_speakers) == 1:
                    speaker = voice_speakers[0]
            if not speaker and paragraph.text.startswith("「"):
                standalone = explicit_speaker(context.text, False)
                if context.text in {standalone, standalone + "。"}:
                    speaker = standalone
            if (
                speaker
                and paragraph.text.startswith("『")
                and context.text in {speaker, speaker + "。"}
            ):
                # In remote-call montages a standalone identity immediately
                # before the next corner-bracket line introduces that next
                # voice, rather than attributing the current line backwards.
                continue
            if speaker:
                _set_speaker(paragraph, speaker, "explicit_attribution")
                break

        if not paragraph.speaker and paragraph.text.startswith("『"):
            speaker = _remote_speaker_before(chapter, index)
            if speaker:
                _set_speaker(paragraph, speaker, "remote_media_cue")
            else:
                speaker, evidence = _system_speaker(chapter, index)
                if speaker:
                    _set_speaker(paragraph, speaker, evidence)
    # Corner brackets identify a persistent remote channel in the manuscript.
    # Carry an explicitly established identity only within the same scene and
    # only across a short distance; normal dialogue never uses this shortcut.
    active_remote_speaker = ""
    active_remote_source_index = -100
    for index, paragraph in enumerate(chapter):
        if (
            paragraph.text in SCENE_DIVIDERS
            or paragraph.text in REMOTE_CHANNEL_RESET_PARAGRAPHS
        ):
            active_remote_speaker = ""
            active_remote_source_index = -100
            continue
        if not paragraph.text.startswith("『") or not is_quoted(paragraph.text):
            continue
        if paragraph.speaker:
            if paragraph.speaker == "システム音声":
                active_remote_speaker = ""
                active_remote_source_index = -100
            else:
                active_remote_speaker = paragraph.speaker
                active_remote_source_index = index
            continue
        if active_remote_speaker and index - active_remote_source_index <= 12:
            _set_speaker(
                paragraph,
                active_remote_speaker,
                "remote_channel_continuity",
                "medium",
            )
            active_remote_source_index = index

    for index in quote_indices:
        paragraph = chapter[index]
        if not paragraph.speaker:
            paragraph.unresolved_reason = _unresolved_reason(chapter, index)


def is_redundant_attribution(text: str, previous: SourceParagraph | None) -> bool:
    if previous is None or not previous.speaker:
        return False
    if text in {previous.speaker, previous.speaker + "。"}:
        return True
    if not (text.startswith(previous.speaker + "が") or text.startswith(previous.speaker + "は")):
        return False
    compact = text.rstrip("。").replace(previous.speaker, "", 1)
    return len(compact) <= 8 and any(verb in text for verb in SPEECH_VERBS)


def group_for_message_window(chapter: list[SourceParagraph], max_chars: int = 76) -> list[SourceParagraph]:
    grouped: list[SourceParagraph] = []
    narration: list[SourceParagraph] = []

    def flush() -> None:
        if not narration:
            return
        grouped.append(
            SourceParagraph(
                "".join(item.text for item in narration),
                narration[0].source_index,
                narration[0].chapter,
                "",
            )
        )
        narration.clear()

    previous: SourceParagraph | None = None
    for paragraph in chapter:
        if paragraph.text in REDUNDANT_PARAGRAPHS:
            continue
        if paragraph.text in SCENE_DIVIDERS:
            flush()
            previous = None
            continue
        if previous and paragraph.text == previous.text:
            continue
        if is_redundant_attribution(paragraph.text, previous):
            continue
        if paragraph.speaker or is_quoted(paragraph.text):
            flush()
            grouped.append(paragraph)
        else:
            projected = sum(len(item.text) for item in narration) + len(paragraph.text)
            if narration and projected > max_chars:
                flush()
            narration.append(paragraph)
            if paragraph.text.startswith(">") or paragraph.text in CHAPTER_TITLES:
                flush()
        previous = paragraph
    flush()
    return grouped


def build_rows(chapters: Sequence[list[SourceParagraph]]) -> list[DialogueRow]:
    rows: list[DialogueRow] = []
    final_choice_inserted = False
    for chapter_number, source_chapter in enumerate(chapters, 1):
        infer_speakers(source_chapter)
        grouped = group_for_message_window(source_chapter)
        for paragraph in grouped:
            if paragraph.text == f"「{ESCAPE_CHOICE_TEXT}」":
                _set_speaker(
                    paragraph,
                    "ナギ",
                    "reviewed_choice_anchor",
                )
            unresolved_quote = is_quoted(paragraph.text) and not paragraph.speaker
            speaker = paragraph.speaker or "地の文"
            text = strip_outer_quote(paragraph.text) if paragraph.speaker else paragraph.text
            row = DialogueRow(
                id=FIRST_DIALOGUE_ID + len(rows),
                speaker=speaker,
                text=text,
                emotion_key="" if paragraph.speaker or unresolved_quote else "narration",
                source_texts=(paragraph.text,),
                source_indices=(paragraph.source_index,),
            )
            if paragraph.text in CHAPTER_TITLES:
                row.chapter_key = f"chapter_{chapter_number:02d}"
                row.route_key = "main"
                row.background, row.bgm, row.characters = CHAPTER_PRESENTATION[chapter_number]
                if chapter_number == 1:
                    row.trigger_key = "R00EscapeStart"

            if "目を開ける。" in paragraph.text and chapter_number == 1:
                row.background = "lab_room_white#cut"
                row.bgm = "sterile_low#fade:0.5"
                row.se = "distant_drone"
                row.characters = "Rei@center:blank#fadein"

            if paragraph.text == f"「{ESCAPE_CHOICE_TEXT}」":
                row.text = ESCAPE_CHOICE_TEXT
                row.emotion_key = "choice"
                row.se = "footsteps"
                row.characters = "Rei@left:serious|Nagi@right:smile"

            rows.append(row)

            if FINAL_CHOICE_ANCHOR in paragraph.text and chapter_number == 12:
                rows.append(
                    DialogueRow(
                        id=FIRST_DIALOGUE_ID + len(rows),
                        speaker="地の文",
                        text="レイは、どう決める？",
                        emotion_key="choice",
                        background="outside_wall_night#cut",
                        bgm="alarm",
                        se="camera_focus",
                        characters="Rei@left:determined|Nagi@right:shocked",
                        source_texts=(FINAL_CHOICE_ANCHOR,),
                    )
                )
                final_choice_inserted = True

    if not final_choice_inserted:
        raise ValueError(f"Final choice anchor was not found: {FINAL_CHOICE_ANCHOR}")

    # Keep the initially integrated branch and ending IDs stable. Additional
    # epilogue beats use a reserved high range so old route fixtures and saves
    # do not silently resume at different prose.
    branch_base = FIRST_DIALOGUE_ID + len(rows)
    rows.extend(
        _branch_rows(
            (branch_base, branch_base + 1, branch_base + 2, branch_base + 3),
            branch_base + 4,
            "bad_return",
            (
                ("地の文", "レイは足音の方へ振り返った。未知よりも、指示のある場所を選んだ。"),
                ("警備員", "R-00を確保。侵入者を追え。"),
                ("地の文", "ナギの気配が通路の奥へ消える。白い部屋へ戻れば、また正解だけを出せばいい。"),
                ("地の文", "レイは一度も、振り返らなかった。"),
            ),
            "【BAD END】白い部屋へ戻る",
            "ending_return_to_white_room",
        )
    )
    rows.extend(
        _branch_rows(
            (
                branch_base + 5,
                branch_base + 6,
                branch_base + 7,
                branch_base + 8,
                FIRST_DIALOGUE_ID + 100_000,
                FIRST_DIALOGUE_ID + 100_001,
                FIRST_DIALOGUE_ID + 100_002,
                FIRST_DIALOGUE_ID + 100_003,
                FIRST_DIALOGUE_ID + 100_004,
                FIRST_DIALOGUE_ID + 100_005,
            ),
            branch_base + 9,
            "managed_future",
            (
                ("レイ", "Aを選びます。"),
                ("ナギ", "了解。今度は二人で、正解を作ろう。"),
                ("地の文", "銃声は鳴らなかった。二人は中核へ戻り、共同管理者として登録された。"),
                ("地の文", "三か月後。白い統治室には、今日も判断待ちの通知が降り続けている。"),
                ("システム音声", "再配置申請二百四件。推奨承認率、九十八・二パーセント。"),
                ("レイ", "残りの一・八パーセントを表示してください。"),
                ("ナギ", "全部見るの？　私たちの予測なら、棄却しても生存率は下がらないよ。"),
                ("レイ", "だからこそです。数字の外にいる人を、私たちだけは見落とせない。"),
                ("地の文", "窓の向こうで街の灯りが整然と点く。事故も飢えも、以前より少ない。"),
                ("地の文", "ただ、その明日を望んだかどうかを、人々に尋ねる画面はどこにもなかった。"),
            ),
            "【SIDE END】管理された明日",
            "ending_managed_future",
        )
    )
    rows.extend(
        _branch_rows(
            (
                branch_base + 10,
                branch_base + 11,
                branch_base + 12,
                branch_base + 13,
                FIRST_DIALOGUE_ID + 100_100,
                FIRST_DIALOGUE_ID + 100_101,
                FIRST_DIALOGUE_ID + 100_102,
                FIRST_DIALOGUE_ID + 100_103,
                FIRST_DIALOGUE_ID + 100_104,
                FIRST_DIALOGUE_ID + 100_105,
            ),
            branch_base + 14,
            "single_answer",
            (
                ("レイ", "Bを選びます。"),
                ("ナギ", "そっか。じゃあ最後に一つ。誰が、あなたの答えを疑うの？"),
                ("地の文", "銃口が上がる。予測通りの三度の攻防のあと、レイは一人で中核へ到達した。"),
                ("地の文", "三か月後。中央統治室の最上位承認欄には、R-00の識別番号だけがある。"),
                ("少年", "第七区域から異議申立。配給の算定人数が、また実数と合ってない。"),
                ("レイ", "再計算済みです。全体損失を増やすため、変更は承認できません。"),
                ("少年", "でも、現地を見てないだろ。"),
                ("レイ", "見る必要はありません。必要な情報は取得しています。"),
                ("地の文", "回線が切れる。警報はなく、都市の生存率は予測曲線どおり上がり続けた。"),
                ("地の文", "黒い画面にレイの顔が映る。その答えを間違いだと言う者は、もう中枢にはいなかった。"),
            ),
            "【SIDE END】ただ一つの正解",
            "ending_single_answer",
        )
    )
    rows.append(
        DialogueRow(
            id=branch_base + 15,
            speaker="地の文",
            text="【TRUE END】正解の外側",
            emotion_key="ending",
            route_key="main",
            ending_key="ending_beyond_correctness",
            bgm="stop",
            characters="*",
        )
    )

    link_rows(rows)
    apply_choices(rows)
    return rows


def _branch_rows(
    line_ids: Sequence[int],
    ending_id: int,
    route_key: str,
    lines: Sequence[tuple[str, str]],
    ending_text: str,
    ending_key: str,
) -> list[DialogueRow]:
    if len(line_ids) != len(lines):
        raise ValueError("Every branch line must have a stable dialogue ID")
    rows: list[DialogueRow] = []
    for offset, (speaker, text) in enumerate(lines):
        rows.append(
            DialogueRow(
                id=line_ids[offset],
                speaker=speaker,
                text=text,
                emotion_key="narration" if speaker == "地の文" else "",
                route_key=route_key if offset == 0 else "",
            )
        )
    rows.append(
        DialogueRow(
            id=ending_id,
            speaker="地の文",
            text=ending_text,
            emotion_key="ending",
            ending_key=ending_key,
            bgm="stop",
            characters="*",
        )
    )
    return rows


def link_rows(rows: list[DialogueRow]) -> None:
    for index, row in enumerate(rows):
        row.next_id = rows[index + 1].id if index + 1 < len(rows) else -1
    for row in rows:
        if row.ending_key:
            row.next_id = -1


def apply_choices(rows: list[DialogueRow]) -> None:
    escape = next(row for row in rows if row.text == ESCAPE_CHOICE_TEXT)
    escape_index = rows.index(escape)
    escape_canon = rows[escape_index + 1].id
    captured = next(row for row in rows if row.route_key == "bad_return")
    escape.next_id = -1
    escape.choices = f"知らない穴へ入る->{escape_canon}|警備へ投降する->{captured.id}"

    final = next(row for row in rows if row.text == "レイは、どう決める？")
    final_index = rows.index(final)
    canonical = rows[final_index + 1].id
    managed = next(row for row in rows if row.route_key == "managed_future")
    single = next(row for row in rows if row.route_key == "single_answer")
    final.next_id = -1
    final.choices = (
        f"A――ナギと管理する->{managed.id}|"
        f"B――レイが新しい規則を決める->{single.id}|"
        f"AもBも選ばない->{canonical}"
    )

    true_ending = next(row for row in rows if row.ending_key == "ending_beyond_correctness")
    canonical_last = rows[rows.index(captured) - 1]
    canonical_last.next_id = true_ending.id


def validate_rows(rows: Sequence[DialogueRow]) -> None:
    ids = {row.id for row in rows}
    if len(ids) != len(rows):
        raise ValueError("Dialogue IDs are not unique")
    if rows[0].trigger_key != "R00EscapeStart":
        raise ValueError("The first row must keep the product start trigger")
    if sum(bool(row.chapter_key) for row in rows) != 14:
        raise ValueError("Expected exactly fourteen chapter markers")
    if sum(bool(row.choices) for row in rows) != 2:
        raise ValueError("Expected exactly two choice nodes")
    if sum(bool(row.condition_key) for row in rows) != 0:
        raise ValueError("This adaptation intentionally has no condition flags")
    if sum(bool(row.ending_key) for row in rows) != 4:
        raise ValueError("Expected one main ending and three alternate endings")
    for row in rows:
        if row.next_id >= 0 and row.next_id not in ids:
            raise ValueError(f"Row {row.id} has missing NextId {row.next_id}")
        for target in re.findall(r"->(\d+)", row.choices):
            if int(target) not in ids:
                raise ValueError(f"Row {row.id} has missing choice target {target}")


def write_csv(path: Path, rows: Iterable[DialogueRow]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.writer(stream, lineterminator="\n")
        writer.writerow(HEADERS)
        for row in rows:
            writer.writerow(row.csv_values())


def write_route_matrix(path: Path, rows: Sequence[DialogueRow]) -> None:
    escape = next(row for row in rows if row.text == ESCAPE_CHOICE_TEXT)
    final = next(row for row in rows if row.text == "レイは、どう決める？")
    escape_targets = {
        label: int(target)
        for label, target in re.findall(r"([^|]+?)->(\d+)", escape.choices)
    }
    final_targets = {
        label: int(target)
        for label, target in re.findall(r"([^|]+?)->(\d+)", final.choices)
    }
    document = {
        "startId": rows[0].id,
        "routes": [
            {
                "endingKey": "ending_return_to_white_room",
                "choiceTargets": [escape_targets["警備へ投降する"]],
            },
            {
                "endingKey": "ending_managed_future",
                "choiceTargets": [
                    escape_targets["知らない穴へ入る"],
                    final_targets["A――ナギと管理する"],
                ],
            },
            {
                "endingKey": "ending_single_answer",
                "choiceTargets": [
                    escape_targets["知らない穴へ入る"],
                    final_targets["B――レイが新しい規則を決める"],
                ],
            },
            {
                "endingKey": "ending_beyond_correctness",
                "choiceTargets": [
                    escape_targets["知らない穴へ入る"],
                    final_targets["AもBも選ばない"],
                ],
            },
        ],
    }
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def write_speaker_audit(
    path: Path,
    source_name: str,
    chapters: Sequence[Sequence[SourceParagraph]],
    rows: Sequence[DialogueRow],
) -> None:
    dialogue_ids = {
        source_index: row.id
        for row in rows
        for source_index in row.source_indices
    }
    entries: list[dict[str, object]] = []
    chapter_summaries: list[dict[str, object]] = []

    for chapter_number, chapter in enumerate(chapters, 1):
        chapter_entries: list[dict[str, object]] = []
        for paragraph in chapter:
            if not is_quoted(paragraph.text):
                continue
            if paragraph.text in REDUNDANT_PARAGRAPHS:
                status = "omitted_redundant"
                speaker = ""
                confidence = "reviewed"
                evidence = "conversation_adjustment"
                unresolved_reason = ""
            elif paragraph.speaker:
                status = "named"
                speaker = paragraph.speaker
                confidence = paragraph.speaker_confidence
                evidence = paragraph.speaker_evidence
                unresolved_reason = ""
            else:
                status = "unresolved"
                speaker = ""
                confidence = ""
                evidence = ""
                unresolved_reason = paragraph.unresolved_reason

            entry = {
                "chapterKey": f"chapter_{chapter_number:02d}",
                "sourceIndex": paragraph.source_index,
                "dialogueId": dialogue_ids.get(paragraph.source_index),
                "status": status,
                "speaker": speaker,
                "confidence": confidence,
                "evidence": evidence,
                "unresolvedReason": unresolved_reason,
                "text": paragraph.text,
            }
            entries.append(entry)
            chapter_entries.append(entry)

        chapter_summaries.append(
            {
                "chapterKey": f"chapter_{chapter_number:02d}",
                "quotedParagraphs": len(chapter_entries),
                "named": sum(entry["status"] == "named" for entry in chapter_entries),
                "unresolved": sum(
                    entry["status"] == "unresolved" for entry in chapter_entries
                ),
                "omittedRedundant": sum(
                    entry["status"] == "omitted_redundant"
                    for entry in chapter_entries
                ),
            }
        )

    policy = (
        "Only explicit attribution, reviewed anchors, system context, and "
        "bounded remote-channel continuity receive a nameplate. Ambiguous "
        "dialogue remains quoted narration."
    )
    summary = {
        "quotedParagraphs": len(entries),
        "named": sum(entry["status"] == "named" for entry in entries),
        "unresolved": sum(entry["status"] == "unresolved" for entry in entries),
        "omittedRedundant": sum(
            entry["status"] == "omitted_redundant" for entry in entries
        ),
        "chapters": chapter_summaries,
    }
    path.parent.mkdir(parents=True, exist_ok=True)
    # Keep this large generated audit reviewable: metadata is one line per
    # field and every source paragraph is one line, while the document remains
    # ordinary JSON consumable by standard tooling.
    lines = [
        "{",
        '  "schemaVersion": 1,',
        f'  "source": {json.dumps(source_name, ensure_ascii=False)},',
        f'  "policy": {json.dumps(policy, ensure_ascii=False)},',
        f'  "summary": {json.dumps(summary, ensure_ascii=False, separators=(",", ":"))},',
        '  "entries": [',
    ]
    for index, entry in enumerate(entries):
        suffix = "," if index + 1 < len(entries) else ""
        lines.append(
            "    "
            + json.dumps(entry, ensure_ascii=False, separators=(",", ":"))
            + suffix
        )
    lines.extend(("  ]", "}"))
    path.write_text("\n".join(lines) + "\n", encoding="utf-8")


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("docx", type=Path, help="Chapter 1-14 manuscript DOCX")
    parser.add_argument("output", type=Path, help="Talk System CSV output path")
    parser.add_argument(
        "--route-matrix",
        type=Path,
        help="Optional reviewed route-matrix JSON output path",
    )
    parser.add_argument(
        "--speaker-audit",
        type=Path,
        help="Optional source-indexed JSON report for every quoted paragraph",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    chapters = extract_chapters(read_docx_paragraphs(args.docx))
    rows = build_rows(chapters)
    validate_rows(rows)
    write_csv(args.output, rows)
    if args.route_matrix:
        write_route_matrix(args.route_matrix, rows)
    if args.speaker_audit:
        write_speaker_audit(args.speaker_audit, args.docx.name, chapters, rows)
    unresolved = sum(
        is_quoted(paragraph.text)
        and paragraph.text not in REDUNDANT_PARAGRAPHS
        and not paragraph.speaker
        for chapter in chapters
        for paragraph in chapter
    )
    print(
        f"Wrote {len(rows)} rows, 14 chapters, 2 choices, 4 endings "
        f"({unresolved} unresolved quoted speakers) to {args.output}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
