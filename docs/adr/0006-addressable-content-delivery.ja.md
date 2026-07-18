# ADR-0006: Addressablesと不変content identityでproduction contentを配信する

ステータス: Accepted<br>
日付: 2026-07-18<br>
関連: [Issue #26](https://github.com/kkmia417/WhiteRoom/issues/26) / [Issue #8](https://github.com/kkmia417/WhiteRoom/issues/8)<br>
English canonical file: [英語正本](0006-addressable-content-delivery.md)

## コンテキストと問題提起

Prototypeは1つのdialogue CSVとfallback fontを `Resources` からloadする。AAAビジュアルノベルは
数千の画像、character variant、voice、music、video、localized asset、chapter databaseを
持ち得る。Direct referenceと単一 `Resources` archiveは全assetをbuildへ強制的に含め、
ownershipを不透明にし、小さな修正にもfull player releaseを要求する。

Talk Systemは `IDialogueRepositoryLoader`、composite repository、project所有async loadingを
既に提供する。Unity Addressablesはlocal/remote assetのasync解決とcontent-update buildを
提供する。Narrative codeをaddress、bundle topology、特定CDNへ依存させないため、
Addressablesの上にproduct contractが必要である。

## 決定要因

- 全contentを一度にload/shipせずvolumeを拡大できること
- art、audio、narrative、localization teamが独立content unitをpublishできること
- file移動/bundle再編後もsemantic referenceを安定させること
- 同じcode pathでlocal-only platformとoptional remote contentを支えること
- installed player codeと既存saveに対してcontent-only updateを安全にすること
- missing/incompatible/corrupt contentをplayer到達前に診断すること

## 決定結果

最小boot/recovery shell以外の全production narrative/presentation assetを、
Addressables-backed WhiteRoom content serviceからloadする。Stable product IDとversioned
content manifestをpublic contractにし、Addressable addressとbundle layoutをbuild detailにする。

### pathでなく不変semantic IDを使う

Scenario unit、background、character expression、CG、voice、BGM、SE、video、font、
localized variantへ `scenario:r01:chapter03`、`voice:ja:r01:004210` のようなnamespace IDを付ける。

**根拠**: Asset path/bundle addressはproduction中に変わるが、dialogue row、save、telemetry、
localizationは安定しなければならない。
**影響**: publish後のIDを再利用しない。Renameはalias/migration mapで扱う。Validationでduplicate、
missing reference、case collision、型を跨ぐID再利用を拒否する。

### Addressablesをasync content portの背後へ置く

Product codeは `IContentService` へtyped handleを要求し、Addressables adapterがinitialize、
dependency download、load、reference count、cancel、releaseを所有する。

**根拠**: Narrative/UI behaviorはcatalog/bundle APIを知る必要がない。
**影響**: Feature codeからAddressablesまたは `Resources.Load` を直接呼ばない。Talk Systemへは
load済み `TextAsset` を `IDialogueRepositoryLoader` 経由で渡す。全load operationはprogress、
cancellation、必要なtimeout、classified failureを公開する。

### 変更頻度とruntime localityでcontentを分割する

Bundle/catalogをsource folderだけでなく、platform、chapter/route unit、locale、content type、
update cadenceでgroup化する。

**根拠**: script/voiceの1行修正で無関係なmulti-gigabyte bundleをinvalidateしてはならない。
**影響**: Shared dependencyを測定し意図的にdeduplicateする。各releaseでAddressables content
state、catalog、manifest、build profile、dependency reportをarchiveする。Bundle sizeと
duplication budgetをbuild gateにする。

### player-code releaseとcontent-only releaseを分離する

Content-only updateはdataと互換assetを変更できるが、新しいmanaged code、serialization type、
非対応schemaを要求してはならない。

**根拠**: Installed codeが許可された全catalogを解釈できる必要がある。
**影響**: Manifestにminimum/maximum compatible player build、content version、product channel、
required packを記録する。Immutable version pathをpublishし、development、QA、production間で
promotionする。Rollbackは上書きでなく以前のvalidated manifestを選択する。

### 最小local boot/recovery setを保持する

Startup UI、download前に必要なlegal/privacy notice、可読fallback font、content repair UI、
fatal-error presentationをplayerへ同梱する。

**根拠**: Remote outageまたはcache破損時にblank screenでplayerを停止させない。
**影響**: Remote deliveryはplatform/channelごとにoptionalとする。Boot shellはtitle/storyへ
入る前にspace、connectivity、catalog compatibility、required packを検証できる。

## 利点

- monolithic player buildなしにcontent volumeとteam throughputを拡張できる。
- Stable IDがscenario、save、telemetry、localizationをfile移動から守る。
- Local、DLC、remote packが1つのproduct-facing loading contractを共有する。
- Content updateをversion管理、test、promotion、rollbackできる。
- Talk SystemをAddressablesから独立させられる。

## トレードオフ

- Addressablesにcatalog、lifetime、cache、build-state complexityがある。
  → 1つのadapterへ集中しrelease artifactをarchiveする。
- 細かいbundleはrequest/catalog overheadを増やす。
  → measured load traceとexplicit budgetからgroupingを調整する。
- Immutable IDにはgovernanceが必要になる。
  → catalogを生成しduplicate/missing/recycled IDでCIをfailさせる。

## 不採用の選択肢と根拠

| 選択肢 | 不採用理由 |
| --- | --- |
| production contentを `Resources` に置き続ける | explicit lifetime、scalable partition、content-update workflowを持たない。 |
| 全prefab/sceneからassetを直接参照する | content lifetime/packageをscene serializationへ結合しdependencyを不透明にする。 |
| custom AssetBundle/patch systemを作る | Addressablesのcatalog、dependency、cache、update behaviorを高riskで再実装する。 |
| 全contentをremote必須にする | store、console、offline mode、recovery pathの一部はlocal contentを必要とする。 |

## 関連するADR

- [ADR-0001](0001-talk-system-boundary.ja.md) — Addressables統合をTalk System外へ置く
- [ADR-0004](0004-modular-monolith-boundaries.ja.md) — content port boundary
- [ADR-0007](0007-localization-source-contract.ja.md) — localized content authority
- [ADR-0008](0008-versioned-save-compatibility.ja.md) — saveとcontent versionの関係

## 開発ルール連携

- boot/recovery adapter外への新規production `Resources.Load` を禁止する。
- CIでdialogue、content ID、Addressables Analyze、duplicate dependency、
  missing reference checkを実行する。
- cache-empty/warm、offline、cancel、insufficient-space、corrupt download、
  incompatible-catalog、rollbackをtestする。
- releaseした各player buildのcontent-state/manifest artifactをarchiveする。

## 注記

- Addressablesは現在未導入であり、採用実装は個別Issueで行う。
- Unityはcontent-update buildで以前の `addressables_content_state.bin` 保存とcode変更禁止を
  要求している:
  [Addressables 2.9 content update overview](https://docs.unity3d.com/Packages/com.unity.addressables@2.9/manual/content-update-builds-overview.html)
- CDN/hosting vendorは本ADRで選定しない。
