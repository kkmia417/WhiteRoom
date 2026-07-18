# ADR-0005: Unity 6.3 LTS、URP、uGUIをclient標準にする

ステータス: Accepted<br>
日付: 2026-07-18<br>
関連: [Issue #26](https://github.com/kkmia417/WhiteRoom/issues/26) / [Issue #8](https://github.com/kkmia417/WhiteRoom/issues/8) / [Issue #24](https://github.com/kkmia417/WhiteRoom/issues/24)<br>
English canonical file: [英語正本](0005-unity-urp-runtime-baseline.md)

## コンテキストと問題提起

WhiteRoomは既にUnity `6000.3.7f1`を使用し、Talk SystemはUnity 6000.0を対象とし、
rendering作業はURPとVRM MToonを参照している。商用productionには1つのsupport対象editor line、
1つのruntime UI技術、1つのrender pipelineが必要である。Built-in、URP、HDRP、uGUI、
UI Toolkitをfeatureごとに競合させると、shader、prefab、test matrix、platform defectが増大する。

Unityは6.3を2027年12月までsupportするLTS releaseとしている。Production固定に適するが、
patch upgradeでもserialized asset、package、shader、platform toolchainが変化し得るため、
evidenceなしで更新できない。

## 決定要因

- 安定したproduction supportと再現可能build
- Windows PC品質とconsole・追加desktop targetへの現実的な経路
- 高品質2D/2.5D presentation、VRM character、post-processing、video
- Talk System既存uGUI runtimeとの互換性
- 1つのshader/UI test matrix
- editor、package、SDK upgradeの統制

## 決定結果

出荷clientはpatch versionを固定したUnity 6.3 LTS、Universal Render Pipeline（URP）、
全player-facing runtime UIにuGUIを使用する。UI Toolkitは後続ADRでruntime migrationが
実証されるまでeditor tool専用とする。

### editorとproduction packageを固定する

正確なeditor patchを `ProjectSettings/ProjectVersion.txt` にcommitし、package versionと
platform SDK versionをrelease branchごとにlockする。

**根拠**: 「Unity 6.3」だけでは再現可能toolchain identifierにならない。
**影響**: Editor/package upgradeには専用Issue、release note/known issue review、clean import、
compile、test、代表content build、save compatibility確認、platform smoke testが必要になる。
DeveloperとCIのversionを一致させる。

### URPを唯一のshipping render pipelineにする

全production shader、material、post-processing、camera stack、quality profileをURP向けにする。
Built-in Render PipelineとHDRP variantは維持しない。

**根拠**: URPは2D/2.5Dビジュアルノベルに必要なcross-platform performance rangeを、
parallel pipelineのmemory/shader variant costなしで提供する。
**影響**: Art ingestionでURP shader互換性を検証する。非対応assetはimport時に変換するか、
documented adapterの背後へ隔離する。Pipeline migrationには後継ADRと完全な
visual-regression baselineが必要になる。

### player-facing runtime UIをuGUIに統一する

Dialogue、choice、backlog、save/load、settings、gallery、subtitle、accessibility surfaceは
prefab-authored uGUIを使う。UI Toolkitはeditor toolingで利用できる。

**根拠**: Talk Systemと現行runtime presentationはuGUIを使用しており、runtime UI stackの
混在はnavigation、localization、focus、animation、自動test infrastructureを二重化する。
**影響**: Runtime screenはshared navigation、typography、safe-area、input、
accessibility componentを利用する。Runtime UI Toolkit採用にはfeature局所判断ではなく、
別途accepted migration ADRを要求する。

### platform差分をquality/capability profileで表現する

Resolution scale、texture tier、shadow/post-processing quality、video format、input prompt、
optional featureをversioned profileから選ぶ。

**根拠**: platform差分は避けられないが、分散したcompile directiveではbehaviorを把握できない。
**影響**: 各release targetはcontent lock前にframe time、memory、loading、downloadの数値budgetを
定義する。Platform固有codeはadapterへ置き、content/gameplayはplatform名でなくcapabilityを
queryする。

## 利点

- support対象editor、renderer、runtime UI pathが1つになる。
- 既存Talk System UIとVRM/URP作業を利用できる。
- Art、QA、build teamがshader/prefab前提を共有できる。
- platform tuningでnarrative/product logicをforkしない。

## トレードオフ

- HDRP専用effect/assetは変換が必要になる。
  → 明示shot要件をURPで満たせない場合だけ例外を承認する。
- uGUIは一部の新しいUI Toolkit workflowを持たない。
  → 2 stackを持たずshared prefab、navigation、test utilityへ投資する。
- version固定により新engine feature導入が遅れる。
  → content lock中の更新ではなくevidence-based upgrade windowを設ける。

## 不採用の選択肢と根拠

| 選択肢 | 不採用理由 |
| --- | --- |
| URP版とHDRP版を維持する | 現在のproduct要件なしにshader、lighting、asset、performance、certification作業を二重化する。 |
| Built-in Render Pipelineを使う | 現在のURP方針と一致せず、長期rendering baselineに適さない。 |
| screenごとにuGUIとUI Toolkitを混在させる | runtime navigation、input、localization、style、QA infrastructureを二重化する。 |
| 最新Unity patchへ自動追随する | buildを再現不能にし、未reviewのserialization/platform変更を持ち込む。 |

## 関連するADR

- [ADR-0002](0002-runtime-responsibility-split.ja.md) — runtime composition
- [ADR-0004](0004-modular-monolith-boundaries.ja.md) — Unity-facing codeの隔離
- [ADR-0006](0006-addressable-content-delivery.ja.md) — production asset loading

## 開発ルール連携

- CIで予期しない `ProjectVersion.txt` またはpackage lock変更を拒否する。
- Art validationでBuilt-in/HDRP shaderと欠損URP variantをreportする。
- player-facing UI PRにkeyboard/controller navigation、safe-area、localization、
  代表resolutionのevidenceを要求する。
- Release profileに明示的performance budgetとquality settingsを含める。

## 注記

- repositoryはまだ完全準拠していない。URPはarchitecture targetであり、package導入とasset移行は
  個別implementation Issueで扱う。
- 現行support情報は
  [Unity 6 Releases & Support](https://unity.com/releases/unity-6/support)を参照する。
- 測定したtarget-platform要件またはproduct-defining rendering featureをprototype後も
  満たせない場合だけURPを再検討する。
