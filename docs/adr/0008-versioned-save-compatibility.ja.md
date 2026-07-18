# ADR-0008: Versioned save envelopeと明示migrationでplayer progressを守る

ステータス: Accepted<br>
日付: 2026-07-18<br>
関連: [Issue #26](https://github.com/kkmia417/WhiteRoom/issues/26) / [Issue #8](https://github.com/kkmia417/WhiteRoom/issues/8) / [Issue #19](https://github.com/kkmia417/WhiteRoom/issues/19) / [Issue #20](https://github.com/kkmia417/WhiteRoom/issues/20)<br>
English canonical file: [英語正本](0008-versioned-save-compatibility.md)

## コンテキストと問題提起

商用gameのplayerはmanual save、autosave、settings、unlock、demo-to-full progression、
DLC互換、cloud synchronizationがupdate後も残ると期待する。Talk Systemはdialogue state、
presentation contributor、progress marker、schema metadata、content version、product channel、
slot JSON、thumbnail、atomic replacement、failure result、migration extension pointを既に持つ。

WhiteRoomはproduct channel、content version、game flag、settings、platform user、conflict policyの
意味を所有する。Scene objectをserializeしたり、現在のJSON shapeを永久固定とみなしたりすると、
content修正とpost-launch updateを安全に行えない。

## 決定要因

- 有償productのprogressをsilent lossしないこと
- narrative/presentationをcoherent checkpointへrestoreすること
- player、Talk System、content versionを跨ぐsequential migration
- local、cloud、demo、full、DLC、store channelの明示
- interrupted write、corruption、unknown future saveから安全にrecoverすること
- game policyをcloud/platform SDKへ結合しないこと

## 決定結果

WhiteRoomがTalk System stateとproduct所有sectionを含むversioned save envelopeを所有する。
Saveはtransactionalにwriteし、利用前にvalidateし、明示registryでmigrateする。Cloud
synchronizationはproduct所有conflict ruleを持つreplaceable storage adapterにする。

### product所有save envelopeを定義する

各slotにproduct save version、Talk System schema version、content version、product channel、
build ID、slot identity、save kind、timestamp、play time、localeと、dialogue、WhiteRoom state、
presentation checkpoint、optional extension dataのtyped sectionを記録する。Thumbnailはsidecarにする。

**根拠**: 1つのversion numberでは独立したproduct/package/content互換を表現できない。
**影響**: Runtime modelを専用save DTOへ変換する。Unity object reference、scene instance ID、
vendor objectをserializeしない。Formatが許す限りunknown optional fieldを保持する。

### coherent checkpointだけをsaveする

Manual/autosaveは1つのpaused save transaction内でdialogue stateと全registered presentation
contributorをcaptureする。

**根拠**: Line transitionとasync visual updateの途中でcaptureするとplayerが見ていないstateへ
restoreし得る。
**影響**: Save requestはnarrative/presentation state machineと調停し、auto/skipをsuspendし、
完全snapshotをcommitするかfailureをreportする。Autosave pointは任意frame timerでなく
明示story/product eventにする。

### deterministicにmigrateし唯一のrecoverable copyを上書きしない

Migrationをknown version/channel/content rangeからcurrent formatへのordered one-way transformにする。
Migrate済みsaveがvalidation/commitに成功するまでoriginalまたはprevious valid generationを残す。

**根拠**: Failed migrationはdiagnose/recover可能でなければならない。
**影響**: Missing migration path、future schema version、removed dialogue ID、incompatible channelは
typed load outcomeを返し、silent resetしない。全shipped save versionのfrozen fixtureでmigrationを
testする。

### generation-based transactional storageを使う

Local storageはtemporary generationを書き、flush/validate後にatomic promoteし、少なくとも1つの
previous valid generationを保持する。Integrity digestはcorruption検出に使うがanti-tamper
securityとはみなさない。

**根拠**: Process termination、disk exhaustion、partial synchronizationは通常failure modeである。
**影響**: UIはretry、previous-generation recovery、actionable failure messageを提供する。
Saveへsecretを保存しない。Encryption/signingは別のthreat-model判断を必要とする。

### cloud/platform identityをstorage portの背後へ置く

Persistence serviceはproduct所有local/cloud storage interfaceを使う。Conflict resolutionは
platform user、slot lineage/generation、timestamp、play time、content compatibilityを比較し、
どちらも優先できない場合は明示player choiceを使う。

**根拠**: 「newest timestamp wins」はoffline progressを失い得て、platform SDK typeはsave modelに
入るべきでない。
**影響**: Cloudはcapability/channelごとにoptionalにする。Upload/downloadはidempotent、
observable、cancellableとし、valid local saveへのaccessをblockしない。

## 利点

- Released saveをupdate後もtest/migrateできる。
- Interrupted write/failed migrationでもrecovery pathを残せる。
- Talk System capabilityを再利用しつつproduct policyを渡さない。
- Cloud vendor/platform SDKをreplaceableにできる。
- Save/load failureが明示user/telemetry outcomeになる。

## トレードオフ

- Frozen fixture/migrationは恒久maintenanceになる。
  → Compatibilityを商用contractとして扱い、policyなしにpathを削除しない。
- Checkpoint調停がsave latencyを増やす。
  → main threadでcompact stateをcaptureしstorage workをasyncに行う。
- Previous generationがstorageを使う。
  → platform budgetごとにretained generation/thumbnail数を制限する。

## 不採用の選択肢と根拠

| 選択肢 | 不採用理由 |
| --- | --- |
| active Unity scene/objectをserializeする | scene identity/object graphはcontent/engine変更で不安定になる。 |
| schema変更時にold saveを置き換える | player-progress contractを破りpost-launch updateを危険にする。 |
| Talk System slot JSONだけをproduct formatにする | WhiteRoom channel、settings、platform identity、conflict policy、将来sectionを所有できない。 |
| 1つのcloud SDKをsave serviceへ直接統合する | vendor lifecycle/error typeをproduct policy/testへ漏らす。 |

## 関連するADR

- [ADR-0001](0001-talk-system-boundary.ja.md) — package stateとproduct policyの分離
- [ADR-0004](0004-modular-monolith-boundaries.ja.md) — persistence port
- [ADR-0006](0006-addressable-content-delivery.ja.md) — content version
- [ADR-0009](0009-deterministic-presentation-runtime.ja.md) — coherent checkpoint

## 開発ルール連携

- 全shipped save format/channelのimmutable fixture fileを保持する。
- round-trip、sequential migration、interrupted write、current generation破損、
  missing content、future schema、cloud conflictをtestする。
- dialogue/content ID削除にはexplicit compatibility decisionを要求する。
- log/telemetryからsave内容とplayer identifierをredactする。

## 注記

- Talk Systemの `DialogueSaveSystem`、contributor、failure result、migration interfaceを
  implementation foundationとして維持する。
- 特定cloud provider、encryption、cross-store synchronizationは対象外である。
- Platform-required formatが同等のcompatibility/recovery/test-fixture保証を維持できる場合だけ
  envelopeを再検討する。
