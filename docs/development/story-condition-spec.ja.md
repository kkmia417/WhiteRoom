# R00 第一章〜第十四章 短編scenario・分岐仕様

ステータス: [Issue #65](https://github.com/kkmia417/WhiteRoom/issues/65) として実装し、[Issue #68](https://github.com/kkmia417/WhiteRoom/issues/68) で短編化<br>
長編原稿: `WHITE_ROOM_第一章〜第十四章_全編_会話調整版.docx`（作者提供、repository外）<br>
English canonical file: [英語正本](story-condition-spec.md)

## 成果とスコープ

公開中のTalk System scenarioは
`Assets/Resources/Dialogue/r00_escape_talksystem.csv`である。全十四章の中心対立、レイとナギの関係、
二つの選択、四つのEndingを保ちつつ、長編原稿を一文ずつ再生しない短いインタラクティブ版とする。

Titleの`NEW GAME`は先に`Main`をloadし、scene load完了後にだけ`R00EscapeStart`を開始する。
Dialogue schema、condition文法、score system、route、character、presentation assetは追加しない。

## 公開scenario契約

| 項目 | 公開値 |
| --- | ---: |
| Dialogue row | 134 |
| 現在の`Text`最大長 | 23文字 |
| 1 turnの上限 | 52文字 |
| First dialogue ID | 1,000,001 |
| 章 | 14 |
| Choice node | 2 |
| Unique ending | 4 |
| `ConditionKey` | 0 |
| Voice cue | 0 |

`ChapterKey`は`chapter_01`から`chapter_14`まで。正史pathは`RouteKey=main`、別分岐の開始点は
`bad_return`、`managed_future`、`single_answer`を使う。開始・章・choice target・endingの主要IDは
1,000,001〜1,009,892の範囲で維持する。Save content versionは`r00_chapters_01_14_v3`である。

## レビュー済み分岐表

| Choice | 選択肢 | 結果 |
| --- | --- | --- |
| 第一章の脱出 | 未知の通路へ入る／警備へ投降する | 通路は本編を継続し、投降は`ending_return_to_white_room`へ進む。 |
| 第十二章の中枢判断 | ナギと管理／レイが一人で規則を決定／両方を拒否 | 最初の二つは`ending_managed_future`と`ending_single_answer`へ、拒否は第十三〜十四章を経て`ending_beyond_correctness`へ進む。 |

過去stateで後続choiceをfilterしない。全optionに明示的な結果があり、不可視flag依存、choice 0件、
Save/Load時だけ成立するrouteはない。

## 編集・演出ルール

- 全十四章の境界、main plot、二つの判断、四Endingの意味を維持する
- 1 turnを52文字以内にし、1 clickにつき一つの完結したbeatを置き、重複説明を削る
- 全chapter rowは`*`でstageをclearしてから、そのsceneに必要なcastだけを表示する
- 主要な場所変更では立ち絵を明示的にclearまたはexitする。特にID 1,000,004ではナギを退場させて
  レイだけを表示し、第5・6・10章もレイ単独で開始する
- 全routeの最終行で`*`と`Bgm=stop`を指定し、result screenへ立ち絵や音を残さない
- Presentation keyはrepository内のbackground・character・audio databaseで解決可能にする。
  承認済み音声が揃うまでVoiceは空にする

`scripts/import_white_room_novel.py`と話者監査は長編原稿を確認するための履歴toolとして維持する。
現在の短編CSVは生成対象ではない。同scriptを公開CSVへ実行するとreview済み134行版を置き換えるため、
再実行には新しいcontent reviewが必要である。

## Validation契約

- IDは一意で、全`NextId`とchoice targetが存在する
- 134行、chapter marker 14件、choice node 2件、unique ending 4件、condition 0件を維持し、
  52文字を超えるturnを許可しない
- 全chapterとendingで立ち絵状態をresetし、レイ単独sceneへナギを引き継がない
- Route fixtureがcycleや未使用choiceなしに全endingへ到達する
- Talk System validationでspeaker、expression、background、BGM、SEの欠損を出さない
- PlayModeで本番`NewGameButton`をclickし、`Main`とdialogue ID 1,000,001の開始を確認する

## 今後のproduction content

短編scenarioは既存のレイ・ナギ・研究員のportraitとprototype用background/audioで最後までplayできる。
Title遷移と立ち絵残留の修正に新規画像は不要である。Scene専用CG、background追加、final music/SE、
収録voiceは今後の品質向上候補であり、現在の導線を妨げるblockerではない。
