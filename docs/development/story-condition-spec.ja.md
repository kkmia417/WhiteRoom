# R00 第一章〜第十四章 組み込み・分岐仕様

ステータス: [Issue #65](https://github.com/kkmia417/WhiteRoom/issues/65) として実装済み<br>
原稿: `WHITE_ROOM_第一章〜第十四章_全編_会話調整版.docx`（作者提供、repository外）<br>
English canonical file: [英語正本](story-condition-spec.md)

## 成果とスコープ

ライトノベル原稿をTalk System用の
`Assets/Resources/Dialogue/r00_escape_talksystem.csv`へ組み込む。全十四章を順番どおり収録し、
人物の変化と中心テーマを保ったまま、段落のリズムをメッセージウィンドウ単位へ調整する。

旧199行の脱出prototypeと14 Endingは置き換える。新しいdialogue schema、condition文法、score
system、大規模route、独自runtime managerは追加しない。現prototypeとの互換のため
`R00EscapeStart` triggerとResources pathは維持する。

話者監査とside ending仕上げの実装契約はIssue #65である。Issue #22は置き換え前prototypeの
condition分岐を扱うため、現在のscenarioの実装契約ではない。

## 公開scenario契約

| 項目 | 公開値 |
| --- | ---: |
| Dialogue row | 9,904 |
| First dialogue ID | 1,000,001 |
| 章 | 14 |
| Choice node | 2 |
| Unique ending | 4 |
| `ConditionKey` | 0 |
| Voice cue | 0 |

`ChapterKey`は`chapter_01`から`chapter_14`まで。正史pathは`RouteKey=main`、別分岐は
`bad_return`、`managed_future`、`single_answer`を使う。これらは到達contentを示すprogress
markerであり、非表示の好感度・morality scoreではない。

新ID rangeは廃止したprototypeのID 1〜880と重複せず、save content versionは
`r00_chapters_01_14_v2`である。旧prototype saveは別の本文へ誤復元せず、missing contentとして
安全に失敗する。

## レビュー済み分岐表

| Choice | 選択肢 | 結果 |
| --- | --- | --- |
| 第一章の脱出 | 未知の設備通路へ入る／警備へ投降する | 通路へ入ると全編へ進む。投降すると`ending_return_to_white_room`へ到達する。 |
| 第十二章の中枢判断 | A: ナギと管理／B: レイが新規規則を決定／AとBの前提を拒否 | Aは`ending_managed_future`、Bは`ending_single_answer`、前提拒否は第十三〜十四章へ進み`ending_beyond_correctness`へ到達する。 |

過去stateで後続choiceをfilterしない。全optionが即時のauthored consequenceを持つため、不可視flag
依存、choice 0件、Save/Load時だけ成立するrouteはない。

## 編集ルール

- 全十四章の境界と順序を維持する
- 会話、plot情報、伏線、人物の決断、第十四章の結末を維持する
- ラノベの短い地の文を一段落一クリックにせず、約76文字を目安にwindow単位へ結合する
- scene区切り記号、隣接duplicate段落、nameplateで代替できる短い発話帰属を削る
- 原稿上で確実に判定できる場合だけnameplateへ話者を設定する。高速な掛け合いで曖昧な箇所は、
  誤った話者を推測せず、鉤括弧を保って地の文identityへ割り当てる
- 全引用段落について、原稿index、dialogue ID、confidence、根拠、未確定理由を
  [`white-room-speaker-audit.json`](white-room-speaker-audit.json)へ記録する。人物の動作は発話帰属と
  みなさず、遠隔回線の継続も明示またはreview済みanchorから狭い範囲だけに適用する
- 承認済み収録、出演者条件、locale coverageが揃うまでVoiceを空にする

決定的変換scriptは`scripts/import_white_room_novel.py`。Repository rootから次でCSVとreview済み
route fixtureを再生成する。

```powershell
python -X utf8 .\scripts\import_white_room_novel.py `
  <path-to-manuscript.docx> `
  .\Assets\Resources\Dialogue\r00_escape_talksystem.csv `
  --route-matrix .\Assets\Tests\Fixtures\r00_ending_routes.json `
  --speaker-audit .\docs\development\white-room-speaker-audit.json
```

原稿はrepositoryへ含めないため、作者提供DOCXが必要である。

## Validation契約

- IDは一意で、全`NextId`とchoice targetが存在する
- Chapter marker 14件、choice node 2件、unique ending 4件、condition 0件である
- Route fixtureがcycleや未使用choiceなしに全endingへ到達する
- Presentation keyが既存background/character/audio databaseで解決し、全Voiceが空である
- Release前にgovernance、Python test、Talk System validation、Unity batch compileが成功する

## AAA production passで必要なcontent

十四章の物語は正史endingまで完結しており、不足章や必須のplot bridgeは見つからなかった。
A/Bのside endingも要約だけで終わらず、選択後の短い後日談を体験できる形へ増補した。

ただし原稿の引用段落7,250件中6,505件には話者の明示がない。これらは鉤括弧付きの地の文として
読める状態を保ち、全件を監査表で分類した。全台詞をnameplate化するには、作者による話者注釈が
必要である。単純な交互割当や人物動作からの断定は誤帰属を生むため採用しない。

また拡張castのportrait、新location専用background、final BGM/SE、CG演出、収録voiceが未制作である。
Dialogue playbackを止めないため、現状は透明nameplate placeholderと既存prototypeのpresentation
libraryを使う。

9,904行はTalk Systemの単一file目安（約5,000行）を超える。Content importの副作用として
prototypeのResources loadingを拡張せず、ADR-0006で計画済みのcontent serviceから章単位scenarioを
配信するべきである。
