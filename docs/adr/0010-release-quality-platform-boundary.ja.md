# ADR-0010: 自動品質evidenceとplatform adapterでreleaseをgateする

ステータス: Accepted<br>
日付: 2026-07-18<br>
関連: [Issue #26](https://github.com/kkmia417/WhiteRoom/issues/26) / [Issue #8](https://github.com/kkmia417/WhiteRoom/issues/8) / [Issue #23](https://github.com/kkmia417/WhiteRoom/issues/23)<br>
English canonical file: [英語正本](0010-release-quality-platform-boundary.md)

## コンテキストと問題提起

AAA商用ビジュアルノベルはUnity executableであると同時に大規模data productである。Buildが
compileしてもunreachable ending、missing voice、誤subtitle language、controller focus破損、
corrupt content catalog、incompatible save、memory spike、platform policy違反を含み得る。
全route、locale、channel、content pack、support hardwareを毎変更でmanual playthroughできない。

Cloud save、achievement、entitlement、input、user identity、store overlay、telemetry、
crash reportingのplatform SDKはそれぞれ異なるlifecycle/privacy obligationを持つ。Narrative/UIから
SDKを直接呼ぶとtest matrixが増え、offline/unsupported capabilityが予測不能に失敗する。

## 決定要因

- 全release candidateへrepeatable evidenceを作ること
- 高価なdevice certification前にnarrative/data defectを捕捉すること
- platform/observability vendorをproduct rule外へ置くこと
- targetごとにperformance、memory、loading、download budgetを定義すること
- story textを収集せずbuild/content versionからfield failureを診断すること
- offline play、consent、least-data privacy behavior

## 決定結果

WhiteRoomはlayered quality pipelineとimmutable release promotionを採用する。Platform、
telemetry、crash capabilityをproduct所有portとtarget-specific adapterで実装する。Release
candidateはtarget profileが宣言するautomated/manual evidenceを満たした場合だけ昇格する。

### layered automated test portfolioを構築する

Pure policy test、module integration/EditMode test、Talk System/content validation、
PlayMode flow test、headless route simulation、save-fixture compatibility、代表visual/input test、
target-device smoke/soak testを持つ。

**根拠**: 各layerが異なるfailureを適切なcostで検出する。
**影響**: 各acceptance criterionを最も低costで信頼できるlayerへ割り当てる。Route simulationは
renderingせず全trigger、choice target、condition outcome、ending、content referenceをcoverする。
Device testは全text line再生でなくrendering、input、lifecycle、storage、SDK behaviorへ集中する。

### build/content validationをblockingにする

Compiler/test error、dialogue graph error、missing/duplicate content ID、localization gap、
unsupported shader、Addressables analysis violation、incompatible save fixture、missing license、
budget超過でrelease profileをfailさせる。

**根拠**: 毎回release判断が必要なwarningは常態化し無視される。
**影響**: Waiverは期限、owner、Issue linkを持ちrelease manifestへ記録する。Required Talk System
validation profileをcommand-line build gateで実行しmachine-readable reportを出力する。

### immutableでtraceableなrelease artifactを作る

各player/content buildにsource commit、Unity/package lock、platform toolchain、build profile、
product channel、build ID、content manifest/version、validation report、symbol、notice/license、
checksumを記録する。

**根拠**: 出荷defectは正確なinput/outputが分からなければdiagnose/rollbackできない。
**影響**: Platformが許す限りdevelopment、QA、certification、productionで同一immutable artifactを
promotionする。同じbranchからのrebuildはpromotionとみなさない。Credentialをsource/artifactへ
入れない。

### platform capabilityをproduct portへ隔離する

Cloud save、achievement、entitlement、platform user、rich presence、overlay、controller ownership、
platform lifecycleをcapability-based interfaceから利用する。

**根拠**: Narrative applicationが必要とするのはoutcomeでありSDK type/platform名ではない。
**影響**: Adapterはunavailable、offline、cancelled、retryable、fatal resultをnormalizeする。
Unsupported optional capabilityは明示的にdegradeする。Entitlement failureでlocal progressを
削除せず、platform-user変更はcontrolled session transitionを発生させる。

### observability/privacyをarchitectureとして扱う

Structured diagnosticにbuild ID、content version、product channel、platform class、operation、
duration、result、non-sensitive correlation IDを含める。Crash symbol/breadcrumbをdocumented
policyの下で保持する。

**根拠**: Content/async failureにはfield evidenceが必要だが、narrative text、choice、name、
save payloadはsensitiveになり得る。
**影響**: Shipping telemetryをallowlist/schema-versioned/consent・region awareにし、安全な
offline bufferとrate limitを持たせ、必要地域ではdisableする。Dialogue text、player-entered name、
raw save、token、filesystem pathを送信しない。

### target-specific performance/resilience budgetを強制する

各release targetにframe-time percentile、peak/steady memory、startup、chapter transition、
save/load、content download、install size、long-session stability budgetを宣言する。

**根拠**: 「よく動く」はtest可能なAAA quality attributeではない。
**影響**: Stable reference hardwareでCI trendを記録し、release candidateでdevice smoke/soak suiteを
実行する。Budget regressionはfailさせるか、Issue-linkedで期限付きwaiverを必要とする。

## 利点

- route、content、save、platform、performance evidenceを再現できる。
- certificationで予防可能defectが減る。
- vendor SDK変更でnarrative/UI policyを書き換えない。
- Field diagnosticから正確なplayer/content artifactを特定できる。
- Privacy constraintをreviewer memoryでなくschemaで強制できる。

## トレードオフ

- Test infrastructure/reference hardwareへ継続投資が必要になる。
  → defect costでautomationを優先しtarget profileをrelease間で再利用する。
- Strict gateがscheduleをblockし得る。
  → baselineを暗黙に弱めずexplicit/expiring waiverを使う。
- Adapter normalizationがvendor detailの一部を隠す。
  → product branchingでなくredacted diagnosticへvendor codeを保持する。

## 不採用の選択肢と根拠

| 選択肢 | 不採用理由 |
| --- | --- |
| manual full playthroughへ依存する | route、locale、content-pack、save-version、platform組み合わせをcoverできない。 |
| featureからplatform SDKを直接呼ぶ | product ruleをtarget lifecycle、error type、test environmentへ結合する。 |
| QA用とproduction用を別々にrebuildする | tested artifactに集めたevidenceを無効にする。 |
| broad logを収集して後からfilterする | filter/consent前にsensitive narrative/player dataがdevice外へ出得る。 |

## 関連するADR

- [ADR-0003](0003-issue-driven-bilingual-adrs.ja.md) — evidence/waiverとIssueの連携
- [ADR-0004](0004-modular-monolith-boundaries.ja.md) — platform port
- [ADR-0005](0005-unity-urp-runtime-baseline.ja.md) — target profile/toolchain
- [ADR-0006](0006-addressable-content-delivery.ja.md) — immutable content artifact
- [ADR-0008](0008-versioned-save-compatibility.ja.md) — compatibility fixture

## 開発ルール連携

- 全feature PRにacceptance-criterion-to-evidence mappingを要求する。
- PRでgovernance/fast testを実行し、変更path/release stageに応じcontent、route、build、
  selected device suiteを実行する。
- Machine-readable report/artifact manifestをproduct support policyに合う期間保持する。
- 新event/vendorをenableする前にtelemetry schema、SDK permission、data retention、
  regional behaviorをreviewする。

## 注記

- [Unity 6.3 Test Framework](https://docs.unity3d.com/6000.3/Documentation/Manual/com.unity.test-framework.html)は
  EditMode/PlayMode testを支えるが、target-device supportと正確なCI orchestrationは
  個別implementation判断である。
- Build farm、analytics、crash、cloud-save、storefront、certification vendorは選定しない。
- Deadlineだけを理由にgateを再検討せず、measured false-positive costとescaped-defect evidenceを
  必要とする。
