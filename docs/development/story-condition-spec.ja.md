# R00 第一章〜第十四章 全編シナリオ・分岐仕様

ステータス: [Issue #69](https://github.com/kkmia417/WhiteRoom/issues/69) として実装<br>
原稿: `WHITE_ROOM_第一章〜第十四章_全編_会話調整版.docx`（作者提供、repository外）<br>
English canonical file: [英語正本](story-condition-spec.md)

## 成果とスコープ

公開中のTalk Systemシナリオは
`Assets/Resources/Dialogue/r00_escape_talksystem.csv`である。全十四章の原稿を
ノベルゲーム向けのturnへ変換し、二つの選択と四つのEndingを維持する。発話は話者を`Speaker`、
かぎ括弧を除いた本文を`Text`へ格納する。

Dialogue schema、condition文法、score system、route、Endingは追加しない。正式立ち絵がない話者には、
画面に表示される共通の仮立ち絵を使う。左右で別の表示キーを割り当て、素材がない二人も同時表示する。
短編版の保存位置を無関係な本文へ復元しないよう、
Save content versionは`r00_chapters_01_14_v4`とする。

## 公開シナリオ契約

| 項目 | 公開値 |
| --- | ---: |
| Dialogue row | 10,648 |
| 分割時の目安 | 24〜36文字 |
| `Text`の絶対上限 | 40文字 |
| First dialogue ID | 1,000,001 |
| 原稿paragraph | 14,156 |
| 引用paragraph | 7,250 |
| 未解決の引用話者 | 0 |
| 章 | 14 |
| Choice node | 2 |
| Unique ending | 4 |
| `ConditionKey` | 0 |
| Voice cue | 0 |

`ChapterKey`は`chapter_01`から`chapter_14`まで。正史pathは`RouteKey=main`、別分岐の開始点は
`bad_return`、`managed_future`、`single_answer`を使う。

## レビュー済み分岐表

| Choice | 選択肢 | 結果 |
| --- | --- | --- |
| 第一章の脱出 | 未知の通路へ入る／警備へ投降する | 通路は本編を継続し、投降は`ending_return_to_white_room`へ進む。 |
| 第十二章の中枢判断 | ナギと管理／レイが一人で規則を決定／両方を拒否 | 最初の二つは`ending_managed_future`と`ending_single_answer`へ、拒否は第十三〜十四章を経て`ending_beyond_correctness`へ進む。 |

過去stateで後続choiceをfilterしない。全optionに明示的な結果があり、不可視flag依存、choice 0件、
Save/Load時だけ成立するrouteはない。

## Import・編集・演出ルール

- Import前に原稿のbyte数とSHA-256を検証する
- 全原稿paragraphを`docs/development/white-room-source-map.json`で追跡し、未追跡を許可しない
- 全引用の決定的な話者割当を`white-room-speaker-ledger.json`へ保存し、結果を
  `white-room-speaker-audit.json`へ出力する
- source index単位のreview済み話者は、古い推論ledgerより優先する。第一章の引用575件は
  全て前後の文脈で確認し、途中に地の文が入っても前後の人物へ話者名をずらさない
- 1 turnは24〜36文字を目安にし、40文字を絶対に超えない。文字数より文末を優先して分割し、
  文の途中を続ける場合は明示的に`――`を使い、読点でturnを終えない。先頭fragmentの公開IDを維持し、
  続きには予約済みの1,200,000番台を使う
- 発話の人物名は`Speaker`だけに、内容は外側のかぎ括弧を除いて`Text`へ入れる。地の文は
  `Speaker=地の文`とする
- 全chapter rowは`*`でstageをclearしてから必要なcastを表示し、全routeの最終行は`*`と
  `Bgm=stop`で終了する
- 同じ会話の参加者二人を左右slotへ並べる。レイ、ナギ、研究員には既存立ち絵を使い、
  それ以外の発話者には`PlaceholderLeft`と`PlaceholderRight`を使って共通の仮素材を左右同時に表示する。
  第一章冒頭はレイと正体不明の少女の仮素材を表示し、ナギを先に見せない
- 承認済み音声が揃うまでVoiceは空にする

`scripts/import_white_room_novel.py`を公開CSVの生成元とする。同じ原稿とrepository内の話者ledgerを使った
再実行は、scenario、監査、source mapを同一内容で生成しなければならない。

## Validation契約

- IDは一意で、全`NextId`とchoice targetが存在する
- 10,648行、chapter marker 14件、choice node 2件、unique ending 4件、condition 0件を維持し、
  40文字を超える`Text`を許可しない
- 原稿14,156 paragraphの全てを出力するか、明示的な省略理由を記録する
- 引用7,250件は全て話者を確定するか明示的に省略し、未解決を0件にする
- 本番prefabとruntime fallbackの両方で`SpeakerText`を上部の名前欄、`BodyText`をその下の本文欄に置く
- 共通代替素材を含む全stage directiveが解決する
- 全四Ending routeとSave/Loadを通したとき、Talk System validationとclear済みUnity Consoleに
  warning/errorを残さない

## 今後のproduction content

全編シナリオは既存立ち絵と共通placeholderで最後までplayできる。役別の正式立ち絵、Scene専用CG、
background追加、final music/SE、収録voiceへの差し替えは今後のproduction作業とする。
