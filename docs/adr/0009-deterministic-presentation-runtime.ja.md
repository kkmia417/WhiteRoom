# ADR-0009: Deterministicかつcancel可能なcue runtimeでpresentationを駆動する

ステータス: Accepted<br>
日付: 2026-07-18<br>
関連: [Issue #26](https://github.com/kkmia417/WhiteRoom/issues/26) / [Issue #8](https://github.com/kkmia417/WhiteRoom/issues/8) / [Issue #11](https://github.com/kkmia417/WhiteRoom/issues/11) / [Issue #15](https://github.com/kkmia417/WhiteRoom/issues/15)<br>
English canonical file: [英語正本](0009-deterministic-presentation-runtime.md)

## コンテキストと問題提起

Premium visual novelはdialogue、choice、background、layered character/model、facial animation、
camera、transition、BGM、SE、voice、subtitle、movie、authored set pieceを組み合わせる。Effectの
実行中でもplayerはadvance、skip、auto-play、backlog、save、load、locale変更、application
suspend、scene exitを行える。

Talk Systemはcue key、stage/audio binder、save contributor、restore時のone-shot SE非再生、
presentation issue reportを既に提供する。WhiteRoomにはその上のdeterministic orchestrationが
必要である。Fire-and-forget coroutineまたは全lineのTimeline化はskip/load時にraceし、
save restoreをanimation timingへ依存させる。

## 決定要因

- advance、skip、auto、load、scene exit時もnarrative correctnessを維持すること
- scenario dataへengine objectを埋めずhigh presentation qualityを実現すること
- memoryを制限しlarge asset/voiceをpreloadすること
- transient effectを再生せずdurable visual/audio stateをrestoreすること
- Talk Systemのnarrative authorityを置き換えずauthored cinematicを支えること
- missing cue/timingのactionable telemetry

## 決定結果

Talk Systemをnarrative progressionのauthorityとして維持する。WhiteRoomは各rowのsemantic
cue keyをtyped presentation planへ変換し、1つのdeterministic/cancellable presentation
state machineで実行する。

### semantic cue keyをtyped catalogで解決する

Background、character、expression、stage slot、camera、transition、BGM、SE、voice、movie、
set-piece keyをversioned catalogからcontent ID/presentation parameterへ解決する。

**根拠**: Scenario authorが必要とするのはstable intentでありprefab path/component命令ではない。
**影響**: CSVにscene path、Addressable address、Animator state hash、vendor component referenceを
保存しない。Catalog validationでunresolved keyとinvalid combinationをbuild前に拒否する。

### 1 lineをphased transactionとして実行する

Presentationはline/session cancellation scopeの下で `Resolve`、`Preload`、
`ApplyPersistentState`、`PlayTransientCues`、`Ready` phaseを進む。

**根拠**: 明示phaseがinput、save、next lineを安全に行える時点を定義する。
**影響**: 別line開始、load、title帰還、scene破棄でin-flight workをcancel/joinする。Cancellation後に
presentationを変更するdetached coroutine/unobserved async taskを禁止する。

### durable stateとtransient effectを区別する

Background、visible cast、pose、camera state、environment、current BGM、resumable voiceを
durable checkpoint stateにする。Transition、shake、particle、one-shot SEをtransientにする。

**根拠**: Save restoreは継続stateを再構築し、重複またはtimingを漏らすeffectを再生してはならない。
**影響**: Durable presenterはsave contributorまたはproduct checkpoint contractを実装する。
Restoreは最初にzero-duration stateを適用し、明示的にresumableなmediaだけを再開する。
Transient completionはnarrative truthを変更しない。

### Timeline等はset-piece cueの背後だけで使う

Complex cinematicはdeclared completion/skip contractを持つ `SetPiece` adapter経由でTimeline、
Animator、shader sequence、lip-sync、model systemを利用できる。

**根拠**: Specialized toolはauthored shotに有用だがbranching narrative databaseには適さない。
**影響**: Set pieceはrouteを直接advanceせず、completionまたはtyped eventをnarrative applicationへ
返す。全set pieceがfast-forward、skip-to-end、cancel、restore、missing-content behaviorを
定義する。

### narrative look-aheadでpreloadしlifetime budgetを強制する

Runtimeはcurrent planとbounded look-ahead windowをpreloadし、narrative/session ownership終了時に
content handleをreleaseする。

**根拠**: Voice、video、high-resolution CG、modelは表示deadlineにsynchronous loadできない。
**影響**: Look-aheadはcondition/eventをspeculative実行せずreachable next rowから導く。Memory
pressureでwindowを縮小できる。Loading latency、cache hit、cancellation、missing contentを
測定可能にする。

## 利点

- Skip、load、scene変更後にstale effectが次stateを変更しない。
- Writerはstable semantic cueを使い、presentation teamはhigh-end toolを利用できる。
- Saveからcoherent stage/audio stateへrestoreできる。
- Async content loadingにexplicit ownership/cancellationがある。
- Missing content/slow cueをcontent/build version付きで診断できる。

## トレードオフ

- State machine/typed catalogにauthoring infrastructureが必要になる。
  → catalogを生成しeditor toolingでplanをpreviewする。
- Deterministic skip contractがbespoke effectを制約する。
  → bespoke workを禁止せずskip-to-end実装を要求する。
- Look-aheadが後続conditionで使わないassetをloadし得る。
  → speculationを制限しmemory/load telemetryから調整する。

## 不採用の選択肢と根拠

| 選択肢 | 不採用理由 |
| --- | --- |
| 各dialogue rowからcue coroutineを直接実行する | cancellation、ordering、ownership、save safetyが暗黙でraceしやすい。 |
| Timelineをnarrative engineにする | branch、localization、progress、save authorityをTalk System外へ重複させる。 |
| Unity object path/animation detailをCSVへ置く | narrative contentをscene/implementation構造へ結合する。 |
| presentation completionをroute stateのauthorityにする | visual failure/skipがnarrative progressionを破損し得る。 |

## 関連するADR

- [ADR-0001](0001-talk-system-boundary.ja.md) — Talk Systemのnarrative authority
- [ADR-0006](0006-addressable-content-delivery.ja.md) — content handle/preload
- [ADR-0007](0007-localization-source-contract.ja.md) — subtitle/voice locale
- [ADR-0008](0008-versioned-save-compatibility.ja.md) — checkpoint persistence

## 開発ルール連携

- deterministic clockを使い全presenterのcomplete、skip、cancel、load、scene-exit、
  missing-asset pathをtestする。
- 全production cue key/set-piece contractをbuild gateでvalidateする。
- dialogue textを記録せずline ID、cue ID、content version、phase、duration、classified failureを
  development diagnosticsへ記録する。
- 代表PlayMode visual/audio restoreとmemory-lifetime testを実行する。

## 注記

- 本ADRはTimeline、Cinemachine、Live2D、Spine、lip-sync vendorを必須にしない。
- Talk System既存stage/audio binderとpresentation issue sourceを最初のadapterとして使い、
  parallel dialogue presenterで置き換えない。
- Production set pieceの測定evidenceと同等のcancellation/save保証がある場合だけtransaction
  phaseを再検討する。
