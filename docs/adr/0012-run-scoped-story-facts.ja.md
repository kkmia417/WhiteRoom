# ADR-0012: Run単位のstory factを一貫したdialogue snapshotへ保存する

ステータス: Proposed<br>
日付: 2026-07-30<br>
関連: [Issue #22](https://github.com/kkmia417/WhiteRoom/issues/22) / [Issue #41](https://github.com/kkmia417/WhiteRoom/issues/41)<br>
English canonical file: [English](0012-run-scoped-story-facts.md)

## コンテキストと問題提起

出荷scenarioはstableな`EventKey` factを発行し、`DialogueProgressService`は
`event:` conditionとして評価できる。しかし現在のfactはmemory上の`HashSet`にしか
存在しない。slot Load、到達boundary jump、New Game transition後に、同じchoiceが
その状態を生んだstoryと異なる評価になる可能性がある。Chapter、route、endingの
global unlockは別に永続化されており、1 runのfactと混同してはならない。

Issue #22がconditional choiceを安全に導入するには、run factのlifetime、snapshot、
rollback、migration、unknown keyの動作を先に明示する必要がある。Issue #41も到達node
jump時に一貫したcondition stateを必要とする。

## 決定要因

- Save/Loadと到達node navigationはcapture時に表示されたchoiceを再現する
- New Gameは以前のrunの判断を引き継がない
- globalなchapter、route、ending unlockはNew Game後も保持する
- story-fact dataを持たない既存save slotを引き続きloadできる
- Talk SystemをWhiteRoom固有story semanticsから独立させる
- Rollbackと不正condition keyで意図しないbranchをunlockしない

## 決定結果

WhiteRoomは発行済みstory `EventKey`をrun単位のfactとして扱い、version付きの
製品所有save contributorで永続化する。Global progress markerは既存unlock registryに
残す。

### Run factを一貫したsnapshotへ保存する

`DialogueProgressService`は`IDialogueSaveContributor`を実装し、sort済み・重複なしの
fact listを`DialogueSaveData.ExtraState`の`whiteroom.story-facts.v1`へ保存する。
Capture/restoreは通常slot saveと到達boundary snapshotの両方へ参加する。

**根拠**: 既存contributor境界により、WhiteRoom policyをTalk Systemへ追加せず、
dialogue/presentationと同じtransactionでapplication stateをcaptureできる。
**影響**: Restoreはmemory上のfact set全体をatomicに置換する。payload欠損はempty setと
する。Malformed/future-version payloadはwarning付きで無視し、基礎saveは利用可能なままに
する。Payloadにはstable keyを保存し、dialogue text、row order、Unity objectは保存しない。

### Run factとglobal unlockを分離する

New Gameはscenario開始前にrun factをclearするが、chapter、route、ending、galleryなどの
global unlock recordはclearしない。Slot Loadはrun factをそのslotのsnapshotで置換する。

**根拠**: 1 playthroughのchoiceはそのplaythroughへ影響し、collection/completion progressは
意図的に複数playthroughをまたぐ。
**影響**: Conditionはrun factへ`event:<key>`、global progressへ`chapter:`、`route:`、
`ending:`または`unlock:<id>`を使う。Scenario reviewでは新conditionを必ずいずれかの
lifetimeへ分類する。

### Rollbackでcondition truthを復元する

Product rollbackはTalk Systemが直前line snapshotをrestoreした後、復元済みdialogue history
からrun factを再構築する。到達boundary/Flowchart jumpはcontributor restoreを使う。いずれの
restoreでもevent dispatchを再実行しない。

**根拠**: 破棄されたfuture lineだけで到達したfactを保持すると、復元runに存在しないbranch
からchoiceをunlockできる。
**影響**: Player rollbackはBootstrap adapter経由にする。Choice/event rollback、slot restore、
boundary restore、event side effect非重複をtestする。

### Unknown condition namespaceはfail closedとし、fallback choiceを残す

Scenario conditionは明示的にsupportするnamespaceを使う。Unknownまたはemptyなpositive keyは
falseを返し、validation evidenceを生成する。Filtered choice nodeには、すべての到達可能な
fact setで最低1つのunconditional choiceを残す。

**根拠**: Typoでcontentを開示したり、choice 0件のsoft lockを起こしてはならない。
**影響**: Route simulationは関連fact stateを列挙し、positive/negative conditionと全14
published endingが最低1つのreview済みrouteで到達可能であることを検証する。

## 利点

- Save、Load、rollback、boundary navigation、将来Flowchart jumpでchoice評価が一致する
- optional payload欠損として既存saveを安全にmigrateできる
- Talk System schemaを変えずstory conditionをdata-drivenに保てる
- Global completion progressのcross-run動作を維持できる

## トレードオフ

- Event factはdialogue historyにも含まれる情報を重複保持する。
  → 明示setは評価がdeterministicかつcompactで、package rollback snapshotがcontributorを
  実行しないためrollback時だけhistoryから再構築する。
- 公開済みEventKeyのrenameがsave互換問題になる。
  → 公開keyをstable IDとし、renameにはaliasまたはpayload migrationを要求する。
- Condition filterによりroute testの組合せが増える。
  → review済みcondition表でconditional siteを限定し、無制限探索ではなくcanonical fixtureを持つ。

## 不採用の選択肢と根拠

| 選択肢 | 不採用理由 |
| --- | --- |
| EventKeyをmemory-onlyのままにする | Load/navigation後のchoiceがcapture済みrunと不一致になる。 |
| 全story factをglobal unlockとして保存する | New Gameが以前の判断を継承し、run固有branchが崩れる。 |
| Current rowだけからfactを推測する | 合流routeでは後続conditionに必要な以前のchoice contextを失う。 |
| Talk System save DTOへWhiteRoom fact fieldを追加する | package依存境界を逆転し、再利用infraへproduct semanticsを持ち込む。 |
| Unknown keyをpassさせる | Typoでbranchが開きvalidationが非決定的になる。 |

## 関連するADR

- [ADR-0001](0001-talk-system-boundary.ja.md)はWhiteRoom policyをTalk System外に保つ。
- [ADR-0008](0008-versioned-save-compatibility.ja.md)はversion付きoptional product stateと
  safe migrationを定義する。
- [ADR-0011](0011-reached-boundary-navigation.ja.md)はこのcontributorを含む一貫した到達node
  snapshotを定義する。

## 開発ルール連携

- 承認済みcondition表とcanonical all-ending routeをsource管理する
- New-game reset、Save/Load置換、legacy/malformed/future payload、rollback再構築、boundary
  restore、undefined key、zero-choice guardをtestする
- Review済み表から得た明示catalogに対して全condition keyをvalidateする
- ADR-0012とIssue #22 condition表が承認されるまでscenario conditionを変更しない

## 注記

- どのauthored branchへconditionを使うかは本ADRでは決めず、対となるcondition仕様の
  story-owner判断とする。
- Cross-device global progression、cloud conflict、新condition-expression grammarは対象外。
- Talk Systemがproduct-neutralでcontributor-awareなrollbackを提供する場合、またはstory factが
  boolean presenceでなくtyped valueを必要とする場合に再検討する。
