#!/usr/bin/env python3
"""Convert the WhiteRoom chapter 1-14 manuscript DOCX into Talk System CSV.

The importer performs deterministic novel-to-VN adaptation: narration is grouped
and split into message-window units of at most 40 visible characters, every quote
uses the checked-in source-indexed speaker ledger, scene dividers are removed, and
two reviewed choice points are inserted. The source manuscript remains the
authority for prose and chapter order; the generated source map records every
emitted or intentionally omitted paragraph.
"""

from __future__ import annotations

import argparse
import hashlib
import csv
import json
import math
import re
import zipfile
from collections import Counter, defaultdict
from copy import copy
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
FRAGMENT_DIALOGUE_ID = 1_200_000
TARGET_TEXT_CHARS = 36
MAX_TEXT_CHARS = 40
EXPECTED_SOURCE_SIZE = 252_068
EXPECTED_SOURCE_SHA256 = "e023ed7c5a896abbc36663c10d89eb40f3338083bee2b82e6a88edefcbeccbb6"
DEFAULT_SPEAKER_LEDGER = (
    Path(__file__).resolve().parents[1]
    / "docs"
    / "development"
    / "white-room-speaker-ledger.json"
)

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

ARTED_SPEAKERS = {"レイ", "ナギ", "研究員"}
NON_VISUAL_SPEAKERS = {"地の文", "システム音声"}

BACKWARD_SPEECH_VERBS = (
    "言った",
    "聞いた",
    "訊いた",
    "答えた",
    "呟いた",
    "つぶやいた",
    "叫んだ",
    "返した",
    "続けた",
    "遮った",
    "尋ねた",
    "問いかけた",
    "言い直した",
    "言い切った",
    "付け足した",
    "呼んだ",
    "口にした",
    "声を上げた",
)

FORWARD_SPEECH_VERBS = (
    "言う",
    "聞く",
    "訊く",
    "答える",
    "呟く",
    "つぶやく",
    "叫ぶ",
    "返す",
    "続ける",
    "尋ねる",
    "問いかける",
    "言い直す",
    "呼ぶ",
    "口にする",
    "声を上げる",
)

SPEECH_VERBS = BACKWARD_SPEECH_VERBS + FORWARD_SPEECH_VERBS

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
    # Opening and academy scenes were checked line by line against the source.
    42: "少女",
    47: "レイ",
    48: "少女",
    51: "少女",
    52: "レイ",
    69: "少女",
    71: "レイ",
    72: "少女",
    74: "少女",
    75: "レイ",
    76: "少女",
    78: "レイ",
    79: "少女",
    82: "少女",
    83: "レイ",
    84: "少女",
    88: "少女",
    147: "アサヒ",
    150: "レイ",
    151: "アサヒ",
    152: "レイ",
    153: "アサヒ",
    154: "レイ",
    155: "アサヒ",
    158: "アサヒ",
    160: "ユイ",
    161: "アサヒ",
    162: "ユイ",
    163: "アサヒ",
    164: "レイ",
    165: "アサヒ",
    166: "ユイ",
    167: "アサヒ",
    169: "ユイ",
    170: "レイ",
    171: "ユイ",
    172: "アサヒ",
    173: "レイ",
    352: "教官",
    357: "レイ",
    358: "教官",
    359: "レイ",
    362: "教官",
    363: "レイ",
    364: "教官",
    365: "レイ",
    366: "教官",
    369: "教官",
    370: "レイ",
    371: "教官",
    373: "教官",
    376: "レイ",
    377: "教官",
    454: "ユイ",
    456: "レイ",
    457: "ユイ",
    465: "レイ",
    467: "ユイ",
    468: "レイ",
    471: "ユイ",
    472: "レイ",
    473: "ユイ",
    474: "レイ",
    475: "ユイ",
    476: "レイ",
    477: "ユイ",
    479: "ユイ",
    481: "ユイ",
    482: "レイ",
    485: "ユイ",
    486: "レイ",
    1181: "レイ",
    1184: "ナギ",
    1186: "レイ",
    1187: "ナギ",
    1188: "レイ",
    1189: "ナギ",
    1191: "レイ",
    1192: "ナギ",
    1193: "レイ",
    1194: "ナギ",
    1195: "レイ",
    1197: "ナギ",
    1199: "レイ",
    1200: "ナギ",
    1208: "ナギ",
    1209: "レイ",
    1210: "ナギ",
    1214: "ナギ",
    1215: "レイ",
    1216: "ナギ",
    1217: "レイ",
    1218: "ナギ",
    # Multi-party vehicle scene: alternating inference alone cannot distinguish
    # the leader, driver, boy, and Nagi reliably.
    1462: "男",
    1463: "ナギ",
    1466: "男",
    1467: "ナギ",
    1468: "男",
    1477: "運転手",
    1478: "男",
    1479: "運転手",
    1481: "男",
    1482: "運転手",
    1483: "男",
    1484: "運転手",
    1487: "男",
    1488: "ナギ",
    1489: "男",
    1490: "ナギ",
    1491: "男",
    1492: "ナギ",
    1495: "運転手",
    1496: "ナギ",
    1497: "男",
    1498: "ナギ",
    1499: "男",
    1500: "ナギ",
    1503: "運転手",
    1505: "運転手",
    1506: "ナギ",
    1507: "運転手",
    1508: "男",
    1511: "男",
    1512: "レイ",
    1513: "男",
    1514: "レイ",
    1515: "男",
    1519: "レイ",
    1521: "男",
    1522: "レイ",
    1523: "男",
    1524: "レイ",
    1525: "男",
    1527: "少年",
    1530: "男",
    1531: "レイ",
    1532: "男",
    1536: "レイ",
    1538: "女性",
    # Later reviewed exchanges and the final hospital montage.
    3293: "レイ",
    3295: "班長",
    3296: "レイ",
    3297: "班長",
    3299: "レイ",
    3300: "班長",
    12835: "レイ",
    13595: "医師",
    13596: "医師",
    13597: "医師",
    13603: "ナギ",
    13605: "老婆",
    13607: "老婆",
    13608: "ナギ",
    13609: "老婆",
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

# Chapter 1 is the player's first and longest introduction to each voice.  The
# original inferred ledger confused participants whenever narration interrupted
# an exchange, so these source-indexed assignments were re-read against the full
# manuscript context rather than inferred from wording alone.
for _speaker, _source_indices in {
    "職員": (180, 182, 185, 187, 189),
    "教官": (
        210, 212, 242, 244, 247, 249, 251, 255, 259, 261, 264,
        352, 358, 362, 364, 366, 369, 371, 373, 377, 380, 382,
        384, 386, 397, 399, 401, 403, 405, 407, 509, 535, 538,
        518, 521, 523, 560, 562, 567, 574, 576,
    ),
    "システム音声": (234,),
    "アサヒ": (
        147, 151, 153, 155, 158, 161, 163, 165, 167, 172,
        176, 197, 270, 273, 275, 277, 280, 282, 321, 331,
        343, 345, 347, 349, 500, 503, 504, 510, 516,
    ),
    "ユイ": (
        160, 162, 166, 169, 171, 177, 181, 186, 188, 190,
        193, 196, 199, 243, 246, 248, 250, 252, 256, 260,
        262, 265, 272, 274, 276, 279, 281, 283, 287, 290,
        292, 294, 296, 298, 300, 302, 304, 306, 316, 327,
        329, 333, 335, 337, 340, 400, 404, 417, 419, 422,
        424, 428, 430, 432, 434, 439, 441, 443, 446, 448,
        451, 454, 457, 467, 471, 473, 475, 477, 479, 481,
        485,
    ),
    "レイ": (
        47, 52, 71, 75, 78, 90, 92, 108, 115, 150, 152,
        154, 164, 170, 289, 291, 293, 295, 299, 303, 305,
        311, 314, 317, 325, 328, 330, 334, 336, 344, 346,
        357, 359, 363, 365, 370, 376, 383, 385, 392, 398,
        410, 418, 420, 423, 427, 429, 431, 438, 440, 442,
        447, 450, 456, 465, 468, 472, 474, 476, 482, 486,
        505, 515, 537, 539,
        609, 613, 616, 618, 622, 638, 640, 642, 649, 652,
        670, 672, 676, 678, 683, 690, 692, 705, 712, 714,
        718, 720, 741, 744, 750, 755, 757, 760, 762, 766,
        770, 774, 776, 781, 783, 785, 789, 791, 795, 798,
        802, 808, 810, 815, 817, 822, 826, 835, 837, 840,
        842, 850, 853, 856, 858, 862, 865, 868, 873, 875,
        878, 886, 889, 892, 897, 921, 923, 926, 928, 931,
        934, 936, 938, 941, 943, 945, 947, 954, 956, 958,
        976, 978, 980, 986, 995, 1006, 1009, 1011, 1015,
        1017, 1025, 1027, 1029, 1031, 1038, 1043, 1045,
        1047, 1053, 1057, 1058, 1060, 1068, 1070, 1082,
        1084, 1092, 1094, 1097, 1099, 1101, 1103, 1105,
        1107, 1109, 1114, 1116, 1133, 1137, 1139, 1143,
        1148, 1150, 1152, 1154, 1230, 1238, 1240, 1243, 1248,
        1250,
    ),
    "少女": (
        42, 48, 51, 53, 55, 69, 72, 74, 76, 79, 82, 84,
        88, 91, 93, 106, 111, 588, 591, 593, 596, 605, 608,
        610, 612, 615, 617, 619, 621, 623, 637, 639, 641,
        646, 648, 650, 653, 667, 669, 671, 673, 675, 677,
        679, 682, 684, 691, 693, 695, 702, 704, 706, 711,
        713, 715, 717, 719, 721, 725, 728, 736, 738, 743,
        745, 749, 751, 756, 758, 761, 763, 765, 767, 769,
        771, 773, 775, 777,
    ),
    "ナギ": (
        779, 782, 784, 786, 788, 790, 792, 796, 797, 799,
        801, 803, 807, 809, 811, 814, 816, 819, 823, 825,
        836, 838, 841, 843, 845, 847, 851, 854, 857, 859,
        861, 863, 911, 919, 922, 924, 927, 929, 933, 935,
        937, 939, 942, 944, 946, 949, 955, 957, 959, 977,
        979, 982, 985, 987, 994, 996, 1000, 1005, 1007,
        1010, 1012, 1014, 1016, 1018, 1024, 1026, 1028,
        1030, 1032, 1037, 1044, 1046, 1048, 1054, 1059,
        1061, 1067, 1069, 1071, 1072, 1073, 1075, 1081,
        1083, 1085, 1091, 1093, 1095, 1098, 1100, 1102,
        1104, 1106, 1108, 1110, 1115, 1117, 1124, 1132,
        1134, 1147, 1149, 1151, 1153, 1155, 1162, 1239, 1242,
        1247, 1249, 1251, 1253,
    ),
    "警備員": (
        828, 829, 830, 831, 867, 869, 874, 877, 879, 884,
        887, 888, 893, 900, 902, 903, 904, 905, 915,
    ),
}.items():
    for _source_index in _source_indices:
        REVIEWED_SPEAKER_OVERRIDES[_source_index] = _speaker

# Correct the opening of the Rei/Yui room scene explicitly; these four adjacent
# lines were reversed in the former inferred ledger.
REVIEWED_SPEAKER_OVERRIDES.update(
    {
        417: "レイ",
        418: "ユイ",
        419: "レイ",
        420: "ユイ",
        # Later exchanges whose alternating Rei/Yui voices were reversed by
        # the same narration-gap inference bug.
        4839: "レイ",
        5449: "ユイ",
        5450: "レイ",
        5451: "ユイ",
        5452: "レイ",
        5453: "ユイ",
        5456: "ユイ",
        5457: "レイ",
        5458: "ユイ",
        5459: "レイ",
        5460: "ユイ",
        5522: "ユイ",
        5524: "ユイ",
        5557: "ユイ",
        5558: "レイ",
        5559: "ユイ",
        5560: "レイ",
        5561: "ユイ",
        5562: "レイ",
        5563: "ユイ",
        5564: "レイ",
        5565: "ユイ",
        5566: "レイ",
        5567: "ユイ",
        5578: "ユイ",
        5580: "ユイ",
        5581: "レイ",
        5582: "ユイ",
        5583: "レイ",
        5584: "ユイ",
        5591: "ユイ",
        5592: "レイ",
        5593: "ユイ",
        5594: "レイ",
        5595: "ユイ",
        5596: "レイ",
        5597: "ユイ",
        5598: "レイ",
        5599: "ユイ",
        5600: "レイ",
        5601: "ユイ",
        5602: "レイ",
        5603: "ユイ",
    }
)
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
REVIEWED_OMISSION_SOURCE_INDICES = {
    # 「ないよ」「正解」の間に挟まる受け返しは、名前表示付きの
    # ノベルゲームでは会話の勢いを止めるため省く。
    83,
}
REVIEWED_PROSE_MERGE_SOURCE_INDICES = {
    # The meaning of these terse fragments is retained in the preceding
    # reviewed rewrite, avoiding a machine-like chain of one-word sentences.
    23, 24, 27, 29, 34, 36, 37, 39, 41, 44, 46, 50,
    57, 59, 60, 62, 65, 67, 68, 81, 86, 87, 95, 97, 98, 99, 100,
}
REVIEWED_OMITTED_SOURCE_INDICES = (
    REVIEWED_OMISSION_SOURCE_INDICES | REVIEWED_PROSE_MERGE_SOURCE_INDICES
)
REVIEWED_TEXT_OVERRIDES = {
    19: "少女には、レイを殺せる機会が三度あった。",
    20: "けれど、引き金は一度も引かなかった。",
    21: "雨音が、壊れた街を満たしている。",
    22: "傾いた高架の先に、窓を失った建物が並ぶ。",
    26: "レイは瓦礫の陰に身を伏せた。左肩に熱が走る。",
    28: "触れた指先が、赤く濡れていた。",
    # 地の文で音の種類を説明せず、画面で読める効果音にする。
    31: "パンッ！",
    32: "頭のすぐ横で、コンクリートが弾けた。",
    33: "レイは身を低くして走り、倒れた標識を蹴り上げる。",
    35: "少女の視界を遮った隙に距離を詰め、手首を取って銃口を逸らした。",
    38: "少女の膝が腹にめり込み、息が詰まる。",
    40: "それでも腕を捻り上げると、銃が水たまりへ落ちた。",
    43: "少女は笑ったが、顔だけがぼやけて見えない。",
    45: "雨のせいではない。目を凝らすほど、輪郭が崩れていく。",
    49: "少女が距離を取り、レイも構え直した。",
    56: "最後まで――その言葉の意味を、レイは理解できなかった。",
    58: "少女の右足は半歩後ろ。左手は空いている。",
    61: "呼吸も乱れていない。次に踏み込む可能性は低い。",
    63: "動きは読めても、それだけだった。",
    64: "少女が何を望み、どんな言葉なら戦いをやめるのか。",
    66: "どれを選べば正しいのか、レイには何も読めなかった。",
    73: "少女が、さらに一歩近づく。",
    77: "少女が、また一歩近づく。",
    80: "レイは答えない。少女の声がすぐそばまで近づいてくる。",
    85: "少女は地面に落ちた銃を一瞥し、拾わずに両手を広げた。",
    89: "雨だけが、二人の間を流れ落ちる。",
    94: "答えられない。視界の端に、白い光が二つ浮かんだ。",
    96: "左右に文字があるが、ノイズに潰れて読めない。",
}
QUOTE_PAIRS = {"「": "」", "『": "』"}
ESCAPE_CHOICE_TEXT = "捕まるのと、知らない穴。どっち？"
FINAL_CHOICE_ANCHOR = "今まで一度も疑っていなかった。"

CHAPTER_PRESENTATION = {
    1: ("outside_wall_night#fade:1.0", "tense_low#fade:1.0", "*|Rei@left:serious#fadein|PlaceholderRight@right:neutral#fadein"),
    2: ("outside_wall_night#fade:1.0", "quiet_dark#fade:1.0", "*|Rei@left:blank#fadein|Nagi@right:soft#fadein"),
    3: ("outside_wall_night#fade:1.0", "quiet_dark", "*|Rei@left:blank|Nagi@right:serious"),
    4: ("maintenance_corridor#fade:1.0", "sterile_low#fade:1.0", "*|Rei@left:tired|Nagi@right:smile"),
    5: ("lab_room_white#fade:1.0", "sterile_low#fade:1.0", "*|Rei@left:blank"),
    6: ("maintenance_corridor#fade:1.0", "quiet_dark#fade:1.0", "*|Rei@left:blank"),
    7: ("outside_wall_night#fade:1.0", "tense_low#fade:1.0", "*|Rei@left:blank|Nagi@right:smile"),
    8: ("maintenance_corridor#fade:1.0", "tense_low#fade:1.0", "*|Rei@left:serious|Nagi@right:angry"),
    9: ("lab_room_white#fade:1.0", "sterile_low#fade:1.0", "*|Rei@left:blank|Nagi@right:focus"),
    10: ("maintenance_corridor#fade:1.0", "quiet_dark#fade:1.0", "*|Rei@left:lost"),
    11: ("outside_wall_night#fade:1.0", "tense_low#fade:1.0", "*|Rei@left:serious|Nagi@right:shadow"),
    12: ("lab_room_night#fade:1.0", "alarm_low#fade:1.0", "*|Rei@left:serious|Nagi@right:serious"),
    13: ("lab_room_alarm#cut", "alarm", "*|Rei@left:determined|Nagi@right:shocked"),
    14: ("maintenance_corridor#fade:1.0", "alarm_low#fade:1.0", "*|Rei@left:serious|Nagi@right:focus"),
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
    source_indices: tuple[int, ...] = field(default_factory=tuple)
    source_texts: tuple[str, ...] = field(default_factory=tuple)
    omission_reason: str = ""

    def tracked_indices(self) -> tuple[int, ...]:
        return self.source_indices or (self.source_index,)

    def tracked_texts(self) -> tuple[str, ...]:
        return self.source_texts or (self.text,)


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


def validate_source_document(path: Path) -> None:
    size = path.stat().st_size
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    if size != EXPECTED_SOURCE_SIZE or digest != EXPECTED_SOURCE_SHA256:
        raise ValueError(
            "The manuscript does not match the reviewed source: "
            f"size={size}, sha256={digest}"
        )


def load_speaker_ledger(path: Path) -> dict[int, str]:
    if not path.is_file():
        return {}
    document = json.loads(path.read_text(encoding="utf-8"))
    if document.get("sourceSha256", "").lower() != EXPECTED_SOURCE_SHA256:
        raise ValueError(f"Speaker ledger source hash does not match: {path}")
    assignments = document.get("assignments", [])
    ledger: dict[int, str] = {}
    for entry in assignments:
        source_index = int(entry["sourceIndex"])
        speaker = str(entry["speaker"]).strip()
        if not speaker:
            raise ValueError(f"Speaker ledger has an empty speaker at {source_index}")
        if source_index in ledger:
            raise ValueError(f"Speaker ledger repeats source index {source_index}")
        ledger[source_index] = speaker
    return ledger


def write_speaker_ledger(
    path: Path,
    source_name: str,
    chapters: Sequence[Sequence[SourceParagraph]],
) -> None:
    assignments = []
    for chapter in chapters:
        for paragraph in chapter:
            if not is_quoted(paragraph.text) or paragraph.text in REDUNDANT_PARAGRAPHS:
                continue
            if not paragraph.speaker:
                raise ValueError(
                    f"Cannot write ledger with unresolved speaker at {paragraph.source_index}"
                )
            assignments.append(
                {
                    "sourceIndex": paragraph.source_index,
                    "speaker": paragraph.speaker,
                    "evidence": paragraph.speaker_evidence,
                }
            )
    document = {
        "schemaVersion": 1,
        "source": source_name,
        "sourceSize": EXPECTED_SOURCE_SIZE,
        "sourceSha256": EXPECTED_SOURCE_SHA256,
        "assignmentCount": len(assignments),
        "assignments": assignments,
    }
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        json.dumps(document, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


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


def explicit_speaker(
    text: str,
    require_speech_verb: bool,
    verbs: Sequence[str] = SPEECH_VERBS,
) -> str:
    if require_speech_verb and any(verb in text for verb in verbs):
        actor = _named_actor(text)
        if actor:
            return actor
    for speaker in KNOWN_SPEAKERS:
        if text == speaker or text == speaker + "。":
            return "" if require_speech_verb else speaker
        if not (text.startswith(speaker + "が") or text.startswith(speaker + "は")):
            continue
        if not require_speech_verb or any(verb in text for verb in verbs):
            return speaker
    return ""


def _named_actor(text: str) -> str:
    """Return a single character grammatically acting in a prose paragraph."""

    actors: list[str] = []
    for speaker in KNOWN_SPEAKERS:
        if any(
            token in text
            for token in (
                speaker + "が",
                speaker + "は",
                speaker + "も",
            )
        ):
            actors.append(speaker)
    # Avoid treating ``男`` as a second actor in ``少年が`` and similar labels.
    actors = [
        speaker
        for speaker in actors
        if not any(speaker != other and speaker in other for other in actors)
    ]
    return actors[0] if len(actors) == 1 else ""


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

        # Only a completed (past-tense) speech tag after a quote attributes that
        # quote. A present-tense tag such as ``レイは答える。`` introduces the
        # next quote and must not be pulled backwards.
        for context in chapter[index + 1 : min(next_quote_index, index + 6)]:
            if paragraph.speaker:
                break
            if context.text in SCENE_DIVIDERS:
                break
            speaker = explicit_speaker(
                context.text, True, BACKWARD_SPEECH_VERBS
            )
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


def _quote_ngrams(text: str) -> list[str]:
    body = strip_outer_quote(text)
    grams: list[str] = []
    for size in (1, 2, 3):
        grams.extend(body[index : index + size] for index in range(len(body) - size + 1))
    return grams


def _build_speaker_language_model(
    chapters: Sequence[Sequence[SourceParagraph]],
) -> tuple[dict[str, Counter[str]], dict[str, int], int]:
    profiles: dict[str, Counter[str]] = defaultdict(Counter)
    totals: dict[str, int] = defaultdict(int)
    vocabulary: set[str] = set()
    for chapter in chapters:
        for paragraph in chapter:
            if not paragraph.speaker or not is_quoted(paragraph.text):
                continue
            grams = _quote_ngrams(paragraph.text)
            profiles[paragraph.speaker].update(grams)
            totals[paragraph.speaker] += len(grams)
            vocabulary.update(grams)
    return dict(profiles), dict(totals), max(1, len(vocabulary))


def _language_score(
    text: str,
    speaker: str,
    profiles: dict[str, Counter[str]],
    totals: dict[str, int],
    vocabulary_size: int,
) -> float:
    grams = _quote_ngrams(text)
    if not grams or speaker not in profiles:
        return -8.0
    counts = profiles[speaker]
    denominator = totals[speaker] + vocabulary_size
    # Average rather than sum so long dialogue does not dominate sequence cues.
    return sum(
        math.log((counts.get(gram, 0) + 1) / denominator) for gram in grams
    ) / len(grams)


def _quote_blocks(chapter: Sequence[SourceParagraph]) -> list[list[int]]:
    blocks: list[list[int]] = []
    current: list[int] = []
    non_quote_count = 0
    for index, paragraph in enumerate(chapter):
        if paragraph.text in SCENE_DIVIDERS or paragraph.text in CHAPTER_TITLES:
            if current:
                blocks.append(current)
                current = []
            non_quote_count = 0
            continue
        if is_quoted(paragraph.text):
            current.append(index)
            non_quote_count = 0
            continue
        if current:
            non_quote_count += 1
            if non_quote_count > 3:
                blocks.append(current)
                current = []
                non_quote_count = 0
    if current:
        blocks.append(current)
    return blocks


def _block_candidates(
    chapter: Sequence[SourceParagraph], quote_indices: Sequence[int]
) -> list[str]:
    counts: Counter[str] = Counter()
    anchors: list[str] = []
    for index in quote_indices:
        speaker = chapter[index].speaker
        if speaker:
            counts[speaker] += 8
            if speaker not in anchors:
                anchors.append(speaker)

    start = max(0, quote_indices[0] - 10)
    end = min(len(chapter), quote_indices[-1] + 11)
    for paragraph in chapter[start:end]:
        if is_quoted(paragraph.text):
            continue
        for speaker in _speaker_names_in_text(paragraph.text):
            counts[speaker] += 1

    candidates = list(anchors)
    for speaker, _ in counts.most_common(5):
        if speaker not in candidates:
            candidates.append(speaker)
    if not candidates:
        candidates = ["レイ", "ナギ"]
    elif len(candidates) == 1:
        fallback = "ナギ" if candidates[0] == "レイ" else "レイ"
        candidates.append(fallback)
    return candidates[:6]


def _context_speaker_before(
    chapter: Sequence[SourceParagraph], quote_index: int
) -> str:
    for index in range(quote_index - 1, max(-1, quote_index - 5), -1):
        text = chapter[index].text
        if is_quoted(text) or text in SCENE_DIVIDERS:
            break
        speaker = explicit_speaker(text, True, FORWARD_SPEECH_VERBS)
        if speaker:
            return speaker
        voice_speakers = [
            candidate
            for candidate in KNOWN_SPEAKERS
            if text.startswith(candidate + "の声")
        ]
        if len(voice_speakers) == 1:
            return voice_speakers[0]
    return ""


def _context_actor_before(
    chapter: Sequence[SourceParagraph], quote_index: int
) -> str:
    """Return the latest named actor between the previous quote and this one."""

    for index in range(quote_index - 1, max(-1, quote_index - 5), -1):
        text = chapter[index].text
        if is_quoted(text) or text in SCENE_DIVIDERS:
            break
        actor = _named_actor(text)
        if actor:
            return actor
    return ""


def _context_actor_after(
    chapter: Sequence[SourceParagraph],
    quote_index: int,
    next_quote_index: int,
) -> str:
    """Return an actor introduced after a quote but before the next quote."""

    for index in range(quote_index + 1, min(next_quote_index, quote_index + 5)):
        text = chapter[index].text
        if text in SCENE_DIVIDERS:
            break
        # A completed speech tag belongs to the current quote, not the next.
        if explicit_speaker(text, True, BACKWARD_SPEECH_VERBS):
            continue
        actor = _named_actor(text)
        if actor:
            return actor
    return ""


def _assign_block_speakers(
    chapter: list[SourceParagraph],
    quote_indices: Sequence[int],
    profiles: dict[str, Counter[str]],
    totals: dict[str, int],
    vocabulary_size: int,
) -> None:
    candidates = _block_candidates(chapter, quote_indices)
    # Add an explicit cue written before a quote. The conservative first pass
    # already handles the more common attribution written after a quote.
    for index in quote_indices:
        paragraph = chapter[index]
        if paragraph.speaker:
            continue
        speaker = _context_speaker_before(chapter, index)
        if speaker:
            _set_speaker(paragraph, speaker, "explicit_attribution_before")
            if speaker not in candidates:
                candidates.append(speaker)

    # Viterbi labeling combines author-confirmed anchors, local cast names,
    # character language profiles, and the strong VN convention that directly
    # adjacent dialogue paragraphs normally alternate speakers.
    scores: list[dict[str, tuple[float, str]]] = []
    for position, index in enumerate(quote_indices):
        paragraph = chapter[index]
        allowed = [paragraph.speaker] if paragraph.speaker else candidates
        current: dict[str, tuple[float, str]] = {}
        for speaker in allowed:
            emission = _language_score(
                paragraph.text, speaker, profiles, totals, vocabulary_size
            )
            before_actor = _context_actor_before(chapter, index)
            next_quote_index = (
                quote_indices[position + 1]
                if position + 1 < len(quote_indices)
                else len(chapter)
            )
            after_actor = _context_actor_after(
                chapter, index, next_quote_index
            )
            if after_actor:
                # An actor introduced after this quote usually owns the next
                # line. This disambiguates patterns such as quote -> Yui acts
                # -> Yui speaks without mislabeling the first quote as Yui.
                emission += -3.5 if speaker == after_actor else 0.5
            if speaker in paragraph.text and len(paragraph.text) > len(speaker) + 2:
                emission -= 2.5  # Usually a vocative addressed to somebody else.
            if paragraph.text.startswith("『") and speaker == "システム音声":
                emission += 1.5

            if position == 0:
                current[speaker] = (emission, "")
                continue

            previous_index = quote_indices[position - 1]
            direct = index == previous_index + 1
            best_score = -math.inf
            best_previous = ""
            for previous_speaker, (previous_score, _) in scores[-1].items():
                transition = 0.0
                if before_actor:
                    if speaker == before_actor and previous_speaker != before_actor:
                        transition += 5.0
                    elif previous_speaker == before_actor and speaker != before_actor:
                        transition += 3.0
                if (
                    after_actor
                    and previous_speaker != after_actor
                    and speaker == previous_speaker
                ):
                    transition += 3.5
                if previous_speaker == speaker:
                    transition += -4.0 if direct else -0.35
                else:
                    transition += 4.0 if direct else 0.35
                value = previous_score + emission + transition
                if value > best_score:
                    best_score = value
                    best_previous = previous_speaker
            current[speaker] = (best_score, best_previous)
        scores.append(current)

    final_speaker = max(scores[-1], key=lambda key: scores[-1][key][0])
    labels = [final_speaker]
    for position in range(len(scores) - 1, 0, -1):
        labels.append(scores[position][labels[-1]][1])
    labels.reverse()

    for index, speaker in zip(quote_indices, labels):
        paragraph = chapter[index]
        if not paragraph.speaker:
            _set_speaker(paragraph, speaker, "reviewed_sequence_inference", "reviewed")


def complete_speaker_assignments(
    chapters: Sequence[list[SourceParagraph]],
    ledger: dict[int, str] | None = None,
) -> None:
    for chapter in chapters:
        infer_speakers(chapter)

    if ledger:
        quote_indices = {
            paragraph.source_index
            for chapter in chapters
            for paragraph in chapter
            if is_quoted(paragraph.text) and paragraph.text not in REDUNDANT_PARAGRAPHS
        }
        missing = sorted(quote_indices - set(ledger))
        extra = sorted(set(ledger) - quote_indices)
        if missing or extra:
            raise ValueError(
                "Speaker ledger coverage mismatch: "
                f"missing={len(missing)}, extra={len(extra)}"
            )
        for chapter in chapters:
            for paragraph in chapter:
                speaker = ledger.get(paragraph.source_index)
                if speaker:
                    reviewed_speaker = REVIEWED_SPEAKER_OVERRIDES.get(
                        paragraph.source_index
                    )
                    effective_speaker = reviewed_speaker or speaker
                    if paragraph.speaker == effective_speaker:
                        continue
                    _set_speaker(
                        paragraph,
                        effective_speaker,
                        (
                            "reviewed_source_override"
                            if reviewed_speaker
                            else "reviewed_speaker_ledger"
                        ),
                        "reviewed",
                    )
    else:
        profiles, totals, vocabulary_size = _build_speaker_language_model(chapters)
        for chapter in chapters:
            for block in _quote_blocks(chapter):
                _assign_block_speakers(
                    chapter, block, profiles, totals, vocabulary_size
                )

    unresolved = [
        paragraph.source_index
        for chapter in chapters
        for paragraph in chapter
        if is_quoted(paragraph.text)
        and paragraph.text not in REDUNDANT_PARAGRAPHS
        and not paragraph.speaker
    ]
    if unresolved:
        raise ValueError(f"Unresolved quoted speakers remain: {len(unresolved)}")


def is_redundant_attribution(text: str, previous: SourceParagraph | None) -> bool:
    if previous is None or not previous.speaker:
        return False
    if previous.speaker_evidence not in {
        "explicit_attribution",
        "explicit_attribution_before",
        "reviewed_choice_anchor",
        "reviewed_source_override",
    }:
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
                source_indices=tuple(
                    source_index
                    for item in narration
                    for source_index in item.tracked_indices()
                ),
                source_texts=tuple(
                    source_text
                    for item in narration
                    for source_text in item.tracked_texts()
                ),
            )
        )
        narration.clear()

    previous: SourceParagraph | None = None
    for paragraph in chapter:
        if paragraph.source_index in REVIEWED_OMITTED_SOURCE_INDICES:
            paragraph.omission_reason = (
                "reviewed_dialogue_pacing"
                if paragraph.source_index in REVIEWED_OMISSION_SOURCE_INDICES
                else "reviewed_prose_merge"
            )
            continue
        if paragraph.text in REDUNDANT_PARAGRAPHS:
            paragraph.omission_reason = "reviewed_redundant_dialogue"
            continue
        if paragraph.text in SCENE_DIVIDERS:
            paragraph.omission_reason = "scene_divider"
            flush()
            previous = None
            continue
        if previous and paragraph.text == previous.text:
            paragraph.omission_reason = "duplicate_paragraph"
            continue
        if is_redundant_attribution(paragraph.text, previous):
            paragraph.omission_reason = "nameplate_replaces_attribution"
            continue
        reviewed_text = REVIEWED_TEXT_OVERRIDES.get(paragraph.source_index)
        if reviewed_text is not None:
            flush()
            grouped.append(
                SourceParagraph(
                    reviewed_text,
                    paragraph.source_index,
                    paragraph.chapter,
                    paragraph.speaker,
                    paragraph.speaker_confidence,
                    paragraph.speaker_evidence,
                    paragraph.unresolved_reason,
                    source_indices=paragraph.tracked_indices(),
                    source_texts=paragraph.tracked_texts(),
                )
            )
            previous = paragraph
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


def conversation_partner(
    chapter: Sequence[SourceParagraph],
    paragraph: SourceParagraph,
    search_distance: int = 12,
) -> str:
    """Find the nearest other speaker in the same local conversation."""
    if not paragraph.speaker or paragraph.speaker in NON_VISUAL_SPEAKERS:
        return ""

    anchor = paragraph.tracked_indices()[0]
    index = next(
        (position for position, item in enumerate(chapter) if item.source_index == anchor),
        -1,
    )
    if index < 0:
        return ""

    scene_start = index
    while scene_start > 0 and chapter[scene_start - 1].text not in SCENE_DIVIDERS:
        scene_start -= 1
    scene_end = index + 1
    while scene_end < len(chapter) and chapter[scene_end].text not in SCENE_DIVIDERS:
        scene_end += 1

    for distance in range(1, search_distance + 1):
        # Prefer the response that follows the current line, then the prompt.
        for candidate_index in (index + distance, index - distance):
            if candidate_index < scene_start or candidate_index >= scene_end:
                continue
            candidate = chapter[candidate_index]
            if candidate.source_index in REVIEWED_OMITTED_SOURCE_INDICES:
                continue
            if (
                candidate.speaker
                and candidate.speaker not in NON_VISUAL_SPEAKERS
                and candidate.speaker != paragraph.speaker
            ):
                return candidate.speaker
    return ""


def character_stage_directives(speaker: str, partner: str = "") -> str:
    """Build a complete, stale-portrait-free stage for a spoken turn."""
    if not speaker or speaker in NON_VISUAL_SPEAKERS:
        return ""

    participants = [speaker]
    if partner and partner not in NON_VISUAL_SPEAKERS and partner != speaker:
        participants.append(partner)

    if len(participants) == 1:
        slots = {participants[0]: "left" if participants[0] == "レイ" else
                 "right" if participants[0] in {"ナギ", "研究員"} else "center"}
    elif "レイ" in participants:
        other = next(item for item in participants if item != "レイ")
        slots = {"レイ": "left", other: "right"}
    elif "ナギ" in participants:
        other = next(item for item in participants if item != "ナギ")
        slots = {other: "left", "ナギ": "right"}
    else:
        slots = {participants[0]: "left", participants[1]: "right"}

    assets = {
        "レイ": ("Rei", "blank"),
        "ナギ": ("Nagi", "serious"),
        "研究員": ("Researcher", "neutral"),
    }
    directives = ["*"]
    for participant in participants:
        if participant in assets:
            character, expression = assets[participant]
        else:
            character = {
                "left": "PlaceholderLeft",
                "right": "PlaceholderRight",
            }.get(slots[participant], "Placeholder")
            expression = "neutral"
        directives.append(
            f"{character}@{slots[participant]}:{expression}"
        )
    return "|".join(directives)


def build_rows(chapters: Sequence[list[SourceParagraph]]) -> list[DialogueRow]:
    rows: list[DialogueRow] = []
    final_choice_inserted = False
    for chapter_number, source_chapter in enumerate(chapters, 1):
        grouped = group_for_message_window(source_chapter)
        for paragraph in grouped:
            if paragraph.text == f"「{ESCAPE_CHOICE_TEXT}」":
                _set_speaker(
                    paragraph,
                    "ナギ",
                    "reviewed_choice_anchor",
                )
            speaker = paragraph.speaker or "地の文"
            text = strip_outer_quote(paragraph.text) if paragraph.speaker else paragraph.text
            row = DialogueRow(
                id=FIRST_DIALOGUE_ID + len(rows),
                speaker=speaker,
                text=text,
                emotion_key="" if paragraph.speaker else "narration",
                source_texts=paragraph.tracked_texts(),
                source_indices=paragraph.tracked_indices(),
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
                row.characters = "*|Rei@center:blank#fadein"

            if paragraph.text == f"「{ESCAPE_CHOICE_TEXT}」":
                row.text = ESCAPE_CHOICE_TEXT
                row.emotion_key = "choice"
                row.se = "footsteps"
                row.characters = "Rei@left:serious|Nagi@right:smile"

            if not row.characters and speaker not in NON_VISUAL_SPEAKERS:
                row.characters = character_stage_directives(
                    speaker,
                    conversation_partner(source_chapter, paragraph),
                )

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
        partner = ""
        if speaker not in NON_VISUAL_SPEAKERS:
            for distance in range(1, len(lines)):
                candidates = (offset + distance, offset - distance)
                partner = next(
                    (
                        lines[index][0]
                        for index in candidates
                        if 0 <= index < len(lines)
                        and lines[index][0] not in NON_VISUAL_SPEAKERS
                        and lines[index][0] != speaker
                    ),
                    "",
                )
                if partner:
                    break
        rows.append(
            DialogueRow(
                id=line_ids[offset],
                speaker=speaker,
                text=text,
                emotion_key="narration" if speaker == "地の文" else "",
                route_key=route_key if offset == 0 else "",
                characters=character_stage_directives(speaker, partner),
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


def visible_text_length(text: str) -> int:
    return len(re.sub(r"<[^>]+>", "", text))


def split_text_for_window(
    text: str,
    target_chars: int = TARGET_TEXT_CHARS,
    max_chars: int = MAX_TEXT_CHARS,
) -> list[str]:
    if visible_text_length(text) <= max_chars:
        return [text]

    remaining = text.strip()
    fragments: list[str] = []
    strong_boundaries = "。！？!?」』"
    soft_boundaries = "、，,；;：:"
    while visible_text_length(remaining) > max_chars:
        limit = min(max_chars, len(remaining))
        split_at = -1
        # A complete sentence is always a better click boundary than a target
        # character count, even when that sentence is short.
        strong_candidates = [
            index + 1
            for index, character in enumerate(remaining[:limit])
            if character in strong_boundaries
        ]
        if strong_candidates:
            before_target = [value for value in strong_candidates if value <= target_chars]
            split_at = max(before_target) if before_target else min(strong_candidates)

        used_soft_boundary = False
        if split_at < 0:
            soft_candidates = [
                index + 1
                for index, character in enumerate(remaining[:limit])
                if character in soft_boundaries
            ]
            if soft_candidates:
                before_target = [value for value in soft_candidates if value <= target_chars]
                split_at = max(before_target) if before_target else max(soft_candidates)
                used_soft_boundary = True
        if split_at < 0:
            split_at = limit

        fragment = remaining[:split_at].strip()
        if used_soft_boundary and fragment[-1:] in soft_boundaries:
            # The sentence continues on the next click. An em dash signals that
            # continuation instead of presenting an accidental comma-ending.
            fragment = fragment[:-1].rstrip() + "――"
        if not fragment:
            raise ValueError(f"Could not split dialogue text: {text}")
        fragments.append(fragment)
        remaining = remaining[split_at:].strip()

    if remaining:
        fragments.append(remaining)
    if any(visible_text_length(fragment) > max_chars for fragment in fragments):
        raise ValueError(f"Dialogue split exceeded {max_chars} characters: {text}")
    return fragments


def split_rows_for_window(rows: Sequence[DialogueRow]) -> list[DialogueRow]:
    used_ids = {row.id for row in rows}
    next_fragment_id = FRAGMENT_DIALOGUE_ID
    result: list[DialogueRow] = []

    def allocate_fragment_id() -> int:
        nonlocal next_fragment_id
        while next_fragment_id in used_ids:
            next_fragment_id += 1
        value = next_fragment_id
        used_ids.add(value)
        next_fragment_id += 1
        return value

    for row in rows:
        fragments = split_text_for_window(row.text)
        if len(fragments) == 1:
            result.append(row)
            continue

        original_next_id = row.next_id
        fragment_ids = [row.id] + [allocate_fragment_id() for _ in fragments[1:]]
        for index, (fragment_id, fragment_text) in enumerate(
            zip(fragment_ids, fragments)
        ):
            fragment = copy(row)
            fragment.id = fragment_id
            fragment.text = fragment_text
            fragment.next_id = (
                fragment_ids[index + 1]
                if index + 1 < len(fragment_ids)
                else original_next_id
            )
            if index > 0:
                fragment.trigger_key = ""
                fragment.chapter_key = ""
                fragment.route_key = ""
                fragment.background = ""
                fragment.bgm = ""
                fragment.se = ""
                fragment.voice = ""
                fragment.characters = ""
            if index + 1 < len(fragment_ids):
                fragment.event_key = ""
                fragment.choices = ""
                fragment.ending_key = ""
            result.append(fragment)
    return result


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
        if not row.text:
            raise ValueError(f"Row {row.id} has empty Text")
        if visible_text_length(row.text) > MAX_TEXT_CHARS:
            raise ValueError(
                f"Row {row.id} exceeds {MAX_TEXT_CHARS} visible characters"
            )
        if row.speaker == "地の文" and is_quoted(row.text):
            raise ValueError(f"Row {row.id} keeps spoken dialogue as narration")
        if row.text.endswith(("、", "，", ",", "；", ";", "：", ":")):
            raise ValueError(f"Row {row.id} ends at an incomplete clause: {row.text}")
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
    dialogue_ids: dict[int, list[int]] = defaultdict(list)
    for row in rows:
        for source_index in row.source_indices:
            if row.id not in dialogue_ids[source_index]:
                dialogue_ids[source_index].append(row.id)
    entries: list[dict[str, object]] = []
    chapter_summaries: list[dict[str, object]] = []

    for chapter_number, chapter in enumerate(chapters, 1):
        chapter_entries: list[dict[str, object]] = []
        for paragraph in chapter:
            if not is_quoted(paragraph.text):
                continue
            if paragraph.source_index in REVIEWED_OMISSION_SOURCE_INDICES:
                status = "omitted_reviewed"
                speaker = ""
                confidence = "reviewed"
                evidence = "dialogue_pacing"
                unresolved_reason = ""
            elif paragraph.text in REDUNDANT_PARAGRAPHS:
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
                "dialogueIds": dialogue_ids.get(paragraph.source_index, []),
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
                "omittedReviewed": sum(
                    entry["status"] == "omitted_reviewed"
                    for entry in chapter_entries
                ),
            }
        )

    policy = (
        "Every quoted manuscript paragraph receives a deterministic speaker "
        "assignment from explicit attribution, reviewed anchors, bounded "
        "remote-channel continuity, or the source-indexed speaker ledger."
    )
    summary = {
        "quotedParagraphs": len(entries),
        "named": sum(entry["status"] == "named" for entry in entries),
        "unresolved": sum(entry["status"] == "unresolved" for entry in entries),
        "omittedRedundant": sum(
            entry["status"] == "omitted_redundant" for entry in entries
        ),
        "omittedReviewed": sum(
            entry["status"] == "omitted_reviewed" for entry in entries
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


def write_source_map(
    path: Path,
    source_name: str,
    source_paragraphs: Sequence[str],
    chapters: Sequence[Sequence[SourceParagraph]],
    rows: Sequence[DialogueRow],
) -> None:
    paragraph_by_index = {
        paragraph.source_index: paragraph
        for chapter in chapters
        for paragraph in chapter
    }
    dialogue_ids: dict[int, list[int]] = defaultdict(list)
    for row in rows:
        for source_index in row.source_indices:
            if row.id not in dialogue_ids[source_index]:
                dialogue_ids[source_index].append(row.id)

    first_story_index = min(paragraph_by_index)
    entries: list[dict[str, object]] = []
    for source_index, text in enumerate(source_paragraphs):
        paragraph = paragraph_by_index.get(source_index)
        ids = dialogue_ids.get(source_index, [])
        if ids:
            status = "emitted"
            reason = ""
        elif source_index < first_story_index:
            status = "omitted"
            reason = "front_matter"
        elif paragraph is not None and paragraph.omission_reason:
            status = "omitted"
            reason = paragraph.omission_reason
        else:
            status = "untracked"
            reason = ""
        entries.append(
            {
                "sourceIndex": source_index,
                "chapterKey": (
                    f"chapter_{paragraph.chapter:02d}" if paragraph is not None else ""
                ),
                "status": status,
                "reason": reason,
                "dialogueIds": ids,
                "text": text,
            }
        )

    untracked = sum(entry["status"] == "untracked" for entry in entries)
    if untracked:
        raise ValueError(f"Source map contains {untracked} untracked paragraphs")
    summary = {
        "paragraphs": len(entries),
        "emitted": sum(entry["status"] == "emitted" for entry in entries),
        "omitted": sum(entry["status"] == "omitted" for entry in entries),
        "untracked": untracked,
    }
    lines = [
        "{",
        '  "schemaVersion": 1,',
        f'  "source": {json.dumps(source_name, ensure_ascii=False)},',
        f'  "sourceSize": {EXPECTED_SOURCE_SIZE},',
        f'  "sourceSha256": "{EXPECTED_SOURCE_SHA256}",',
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
    path.parent.mkdir(parents=True, exist_ok=True)
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
    parser.add_argument(
        "--speaker-ledger",
        type=Path,
        default=DEFAULT_SPEAKER_LEDGER,
        help="Reviewed source-indexed speaker assignments",
    )
    parser.add_argument(
        "--write-speaker-ledger",
        type=Path,
        help="Write the completed speaker assignments for review",
    )
    parser.add_argument(
        "--source-map",
        type=Path,
        help="Optional source paragraph to dialogue-row coverage report",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    validate_source_document(args.docx)
    source_paragraphs = read_docx_paragraphs(args.docx)
    chapters = extract_chapters(source_paragraphs)
    ledger = load_speaker_ledger(args.speaker_ledger)
    complete_speaker_assignments(chapters, ledger)
    if args.write_speaker_ledger:
        write_speaker_ledger(args.write_speaker_ledger, args.docx.name, chapters)
    rows = split_rows_for_window(build_rows(chapters))
    validate_rows(rows)
    write_csv(args.output, rows)
    if args.route_matrix:
        write_route_matrix(args.route_matrix, rows)
    if args.speaker_audit:
        write_speaker_audit(args.speaker_audit, args.docx.name, chapters, rows)
    if args.source_map:
        write_source_map(
            args.source_map,
            args.docx.name,
            source_paragraphs,
            chapters,
            rows,
        )
    unresolved = sum(
        is_quoted(paragraph.text)
        and paragraph.text not in REDUNDANT_PARAGRAPHS
        and not paragraph.speaker
        for chapter in chapters
        for paragraph in chapter
    )
    print(
        f"Wrote {len(rows)} rows, 14 chapters, 2 choices, 4 endings "
        f"({unresolved} unresolved quoted speakers, max {MAX_TEXT_CHARS} chars) "
        f"to {args.output}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
