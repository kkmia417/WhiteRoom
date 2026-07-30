# ADR-0011: 到達済み境界snapshotでdialogueを移動する

ステータス: Accepted<br>
日付: 2026-07-30<br>
関連: [Issue #40](https://github.com/kkmia417/WhiteRoom/issues/40)<br>
English canonical file: [英語正本](0011-reached-boundary-navigation.md)

## コンテキストと問題提起

Issue #40は前後のscene/choice commandを追加する。Dialogue row indexだけでは、branch、
重複するchapter key、変化するchoice conditionを扱えず、rowを開始し直すjumpはeventや
progress unlockを再実行してしまう。Jump時にはdialogue history、stage、music、voiceなどの
save contributorも一貫して復元する必要がある。

Talk Systemはdialogue graph/save primitiveを所有し、WhiteRoomはproduct navigation policyを
所有する。既存saveを維持しpackageへWhiteRoom固有概念を持ち込まない永続的な境界契約が必要である。

## 決定要因

- 現在の保存済みjourneyで到達した範囲を越えないこと
- jumpでchoiceを選択せずline side effectを再実行しないこと
- dialogueとpresentation contributorを一つのcoherent snapshotとして復元すること
- scene/choice boundaryへstableかつdeterministicなidentityを与えること
- 通常Save/Loadを越えてavailabilityを保持し、旧saveを壊さないこと
- invalid/unavailable jumpを分類済みnon-blocking failureとして返すこと

## 決定結果

WhiteRoomはplayerがscene/choice boundaryへ到達した時にTalk Systemのcoherent save snapshotを
記録し、その記録済みsnapshot間だけを移動する。

### UI位置でなくdialogue identityからboundaryを定義する

`ChapterKey`を持つrowをscene boundaryとし`scene:<chapter-key>:<dialogue-id>`を使う。
Choiceを持つrowをchoice boundaryとし`choice:<dialogue-id>`を使う。Repository row orderを
deterministic catalog orderとし、実際のjourney orderはreached checkpoint orderとする。

**根拠**: Dialogue ID/chapter keyはUI layout変更に耐え、dialogue IDがbranch間で再利用された
chapter keyを区別する。
**影響**: WhiteRoom serviceは`IDialogueRepository`へ問い合わせ、UI controllerはrowを変更せず
button indexからtargetを導出しない。

### persistence I/Oなしでcontributorをcapture/restoreする

Talk Systemは`DialogueSaveSystem`へin-memory capture/restoreを公開する。両操作は登録済み
`IDialogueSaveContributor`を含み、再帰snapshotを防ぐためcaller contributorを除外できる。

**根拠**: 既存save envelopeはnarrative、Backlog、stage、BGM、voiceを一貫して復元し、
`DialogueManager.RestoreState`はline-start/event side effectを発生させない。
**影響**: Slot Save/Loadとnavigationは同一capture/restore pathを共有する。APIはfileを書かず、
slotを割り当てず、WhiteRoom typeを導入しない。

### reached checkpointをlinear journey timelineとして扱う

Previous/next commandは要求kindの最も近いcheckpointを選ぶ。Forward navigationはcursorより後に
既に存在するsnapshotだけを使う。Backward jump後の通常進行は新boundaryを記録する前にforward
tailを切り捨てる。Choice boundary復元後はpendingのままとしoptionを選ばない。

**根拠**: Linear cursorにより未読continuationへの移動や非互換branch stateの混在を防ぐ。
**影響**: Stable boundary再訪時は現在timeline位置のcheckpointを置換する。Cycle/active ID重複は
無制限historyでなく分類済みfailureを返す。

### navigation stateをoptional contributor dataとして永続化する

WhiteRoomは`IDialogueSaveContributor`を実装し、versioned JSON payloadを
`DialogueSaveData.ExtraState`へ格納する。Payloadはreached timeline、cursor、boundary metadata、
navigation contributorを除外して取得したsnapshotを持つ。

**根拠**: `ExtraState`はADR-0008が定める既存のadditive extension seamである。
**影響**: Core save schema bumpは不要。Keyがないsaveはempty timelineとして読み込み、復元後の
現在位置から記録を開始する。Malformed/future payloadはwarning付きで無視し、基礎saveを妨げない。

### jumpをexclusiveかつobservableなoperationにする

JumpはAuto/Skipとbackward skipを停止し、競合overlayを閉じ、restore完了までdialogue/background
inputとsaveをblockする。Resultをsuccess、no target、busy、missing target、condition failure、
cycle、invalid snapshot、restore failureへ分類する。

**根拠**: Concurrent input/automationはtarget決定とrestoreの間にstateを進めたり上書きできる。
**影響**: Command availability/tooltipは現在candidateとbusy stateを反映する。Failureはplayerへ
通知し、現在stateを利用可能なまま残す。

## 利点

- 4 commandすべてがstory-awareで到達済みtargetだけを扱う。
- PresentationとBacklogが復元dialogueと一致する。
- Backward jumpでevent/durable unlockを再適用しない。
- 既存save slotを読み込める。
- Generic package APIをcheckpoint、rewind、preview toolでも再利用できる。

## トレードオフ

- Snapshot historyはrow IDだけの保存よりmemory/save容量を使う。
  → Boundaryは疎で、分岐時にforward tailを削除し、将来の計測結果からidentityを変えず上限を追加できる。
- Linear timelineは過去に訪問した全branchを同時表示しない。
  → 将来のflowchartは別graph/history modelを所有し、navigationはactive journeyとの一貫性を保つ。
- Contributor restore orderはregistration orderに依存する。
  → Package testでorderを固定し、product compositionはpresentation後にnavigationを登録する。

## 不採用の選択肢と根拠

| 選択肢 | 不採用理由 |
| --- | --- |
| target dialogue rowを直接開始する | eventを再実行し、過去choice、progress、Backlog、presentation stateを失う。 |
| command bar/choice button indexからtargetを計算する | UI layoutはdialogue identityでなく独立して変わる。 |
| 後方repository rowすべてへforward jumpを許す | 未読contentを公開し、必須choice/conditionを迂回できる。 |
| save dataへboundary IDだけ保存する | 過去branch/presentation復元にside effect replayが必要になる。 |
| WhiteRoom command policyをTalk Systemへ置く | ADR-0001に反し、reusable packageがproduct behaviorへ依存する。 |

## 関連するADR

- [ADR-0001](0001-talk-system-boundary.ja.md) — package/product boundary
- [ADR-0002](0002-runtime-responsibility-split.ja.md) — use case serviceとcomposition root
- [ADR-0008](0008-versioned-save-compatibility.ja.md) — additive save stateとcompatibility
- [ADR-0009](0009-deterministic-presentation-runtime.ja.md) — coherent/cancellable presentation restore

## 開発ルール連携

- Package testでcontributor exclusion、order、side-effect-free in-memory restoreをcoverする。
- WhiteRoom EditMode testでID、target選択、tail truncation、persistence、compatibility、全failure classをcoverする。
- PlayMode testでbranch route上のprevious/next scene/choiceを実行しdialogue、choice、Backlog、stage、audioの一貫性をassertする。
- Command barはoperation unavailable時に必ず理由を表示する。

## 注記

- Flowchart、scene replay、favorite voice behaviorは定義しない。
- Boundary IDはcontent compatibility identifierとなる。Dialogue IDまたは関連chapter key変更時はmigration reviewを要する。
- 実測したsave-size/memory evidence、またはTalk Systemへのside-effect-free deterministic replay engine追加時だけsnapshot storageを再検討する。
