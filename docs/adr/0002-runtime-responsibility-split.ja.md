# ADR-0002: 明示的なComposition Rootとランタイム責務分割を採用する

ステータス: Accepted<br>
日付: 2026-07-18（2026-07-18 ADR-0001責務レビューから分割）<br>
関連: [Issue #8](https://github.com/kkmia417/WhiteRoom/issues/8) / [PR #25](https://github.com/kkmia417/WhiteRoom/pull/25)<br>
English: [English canonical version](0002-runtime-responsibility-split.md)

## コンテキストと問題提起

WhiteRoomはUnityシーンライフサイクル、Talk Systemランタイムオブジェクト、会話リソース、
Title・Save/Load UI、入力ルーティング、表示、進行解放、永続化を接続する必要がある。
初期実装はこれらを `NovelGameBootstrap` に集中させた。後にcommit履歴の
`Refactor NovelGameBootstrap god class into focused classes` で `Setup`、`Services`、`UI`
のcollaboratorへ分割した。

この分割には永続的な規則が必要である。フォルダ名だけでは、Unity objectを誰が生成するか、
永続化方針を誰が所有するか、controllerがTalk Systemを直接呼べるか、互換reflectionをどこへ
置くかが決まらない。規則がなければ、bootstrapが再び動作を抱え、serviceがwidgetを生成し、
factoryがゲーム方針を所有する。

アプリケーションは専用asmdefではなくUnity生成の `Assembly-CSharp` でコンパイルしている。
Talk SystemはRuntimeとEditorのassemblyを持つ。したがって、compile-time境界が既に存在する
ふりをせず、現在のコードに一致し検証可能な論理境界を定義する必要がある。

## 決定要因

- Unity起動、`DontDestroyOnLoad`、scene eventの所有者を1か所にすること
- Save/Load、進行、条件判定をUI生成詳細なしで理解できること
- UI controllerがfile storageを所有せずapplication operationを呼べること
- Talk System object生成とserialized field互換処理を隔離すること
- 現在の小さなobject graphにDI frameworkのlifecycle・debug costを持ち込まないこと
- application asmdef導入前でも現行コードに一致する境界を検証できること
- 各classの主要な変更理由と検証surfaceを絞ること

## 決定結果

`NovelGameBootstrap` を明示的なComposition Root兼Unity lifecycle adapterとする。
アプリケーションのランタイム責務を `Setup`、`Services`、`UI` へ分け、Talk SystemとUnityへ
向かう一方向依存にする。

```text
Unity Scenes / RuntimeInitializeOnLoad
                │ lifecycle
                ▼
       NovelGameBootstrap
          ├────► Setup factories / compatibility adapters
          ├────► Services / product use cases
          └────► UI controllers / runtime views
                         │
                         ▼
                 Talk System public API
                         │
                         ▼
                       Unity
```

### lifecycleとobject graph構築を `NovelGameBootstrap` に置く

**根拠**: Unity callbackとscene eventはframework entry pointであり、完全なruntime graphを
1か所で確認できる必要がある。
**影響**: Bootstrapはcollaborator生成、event接続、会話resource load、scene lifecycle変換、
operation委譲を行う。save algorithm、unlock rule、widget layout、再利用会話動作を実装しない。
private method追加はlifecycle変換またはcomposition可読性向上に限る。

### Unity object生成とTalk System配線を `Setup` に置く

**根拠**: `GameObject`・`MonoBehaviour`生成、component設定、fallback view構築、package adapter接続は
Unity固有の失敗特性を持つconstruction concernである。
**影響**: `DialogueRuntimeFactory`、`DialoguePresentationFactory`、`DialogueViewFactory`、
`NovelUiFactory`、狭いbinderがobjectを生成・検索する。設定済みcollaboratorを返すが、進行、
save可否、scene outcome、製品use caseを決めない。

### WhiteRoom use caseと永続方針を `Services` に置く

**根拠**: Save/Continue、progress marker、unlock永続化、variable resolveは具体的画面から独立した
application behaviorである。
**影響**: ServiceはTalk System公開契約と、現行storage・loggingに必要な最小Unity機能へ依存できる。
controllerへoperation/eventを公開するが、visual objectを生成せず、後続ADRなしにscene lifecycleを
購読しない。

### presentation調停を `UI` に置く

**根拠**: Title、Backlog、Save/Load、auto-advance停止、fallback layoutは表示理由で変更され、
storageや会話engine方針を所有すべきでない。
**影響**: UI controllerはserviceとTalk System presentation interfaceを呼び、view stateを描画し、
user actionをapplication operationへ変換する。save fileを直接読み書きせず、unlock semanticsを
決定しない。code-driven fallback UIはdomain modelではなくpresentation実装とする。

### 一方向依存を維持し、UIからSetupの再構築を禁止する

**根拠**: 各領域が互いを生成・再設定できると、hidden object graphと循環ownershipが生じる。
**影響**: Bootstrapは全application areaへ依存できる。UIはserviceとTalk System公開presentation
APIへ依存できる。composition後にUI・ServiceからSetup factoryを呼ばない。Setupは製品方針を
決めるためにcontrollerやserviceへ依存しない。

### reflectionを `Setup` の明示的compatibility seamへ限定する

**根拠**: runtime生成するTalk System componentは現在、serialized private fieldとnon-public input
handlerへのaccessを必要とする。reflectionは脆弱だが、分散させるとpackage upgradeを監査できない。
**影響**: `RuntimeFieldBinder` と `DialogueRuntimeFactory` だけを承認reflection領域とする。
member不足時は明確なwarningを出す。新規reflectionにはIssue、公開package APIを使えない理由、
focused validation、撤去条件を要求し、Services・UI・Setup外consumerには追加しない。

### DI frameworkではなく明示的constructionを使う

**根拠**: 現在のgraphは1回だけ構築され、`BuildRuntime` で確認でき、大部分のcollaboratorに
複数implementationやruntime scopeがない。
**影響**: constructorとfactory methodを明示する。DI framework導入には、計測されたgraph複雑性、
test friction、複数lifecycle scopeを根拠とする後継ADRを要求する。

### 境界がtest-readyになるまでapplication asmdefを延期する

**根拠**: asmdef移行はUnity compileとreference behaviorを変える。application専用test assemblyが
まだなく、directory・namespace検査がより小さな初期guardになる。
**影響**: 当面 `Assets/Scripts` を `Assembly-CSharp` に残す。application asmdef Issueには
EditMode test、明示reference、Unity compile検証、影響するEditor/Runtime code移行を含める。

本条項は初期migration guardの履歴であり、module/test contractをacceptedにしてvertical-slice
asmdef移行を開始するADR-0004によって置き換えられた。

## 利点

- Unity lifecycleと完全なobject graphを発見しやすい。
- Save、進行、UI動作の変更理由を狭められる。
- 製品serviceを全画面生成なしでfocused testできる。
- Unity construction failureをapplication policyから隔離できる。
- reflectionとpackage-private couplingを1か所で監査できる。
- 将来のasmdef移行でencodeすべき論理境界が明確になる。

## トレードオフ

- Composition Rootは全具体collaboratorを知る。
  → 宣言的に保ち、construction visibilityではなくbehaviorを外へ移す。
- static factoryはisolated testで差し替えにくい。
  → service/controllerをconstructor inputでtestし、testまたは複数実装が必要な場合だけinterface化する。
- directory境界にcompile-time enforcementがない。
  → namespace/reflection検査を維持し、専用Issueでtest付きasmdefを追加する。
- code-driven fallback UIは大型化し視覚的に脆くなる。
  → production surfaceはauthoring済みprefabを優先し、factoryは明示的fallback/setupに限定する。
- Talk System private member変更でreflectionが壊れる。
  → seamを狭くし、欠落をlogし、統合検証し、契約確定後に公開設定APIへ置換する。

## 不採用の選択肢と根拠

| 選択肢 | 不採用理由 |
| --- | --- |
| すべてのruntime動作を1つのMonoBehaviourに置く | lifecycle、storage、進行、表示、constructionが結合し、履歴上既に発生したgod class問題を再現する。 |
| 各controllerがdependencyを検索・生成する | object graphを隠し、Unity lookupを重複させ、ownershipとcleanupを曖昧にする。 |
| 現時点でDI containerを導入する | 複数scopeも十分大きなgraphもない段階でpackage、lifecycle、registration、debug costを増やす。 |
| 全collaboratorをMonoBehaviourにする | 製品logicがUnity lifecycleへ結合し、focused testにsceneやGameObjectが必要になる。 |
| runtime setupをprefabだけに置く | production compositionには有効だが、fallback constructionとoptional componentには明示的setup codeが必要である。 |
| application testなしにasmdefを追加する | regression harnessなしのcompile-boundary移行となり、意図した依存を証明せずiterationを遅くし得る。 |

## 関連するADR

- [ADR-0001](0001-talk-system-boundary.ja.md) — 全application areaが利用する
  package/application所有境界
- [ADR-0003](0003-issue-driven-bilingual-adrs.ja.md) — この責務分割を変更するための
  Issue・レビュー手順
- [ADR-0004](0004-modular-monolith-boundaries.ja.md) — 一時的asmdef延期を置き換え、
  target application assemblyを定義する

## 開発ルール連携

- `Assets/Scripts` のC#は `WhiteRoom.Novel` namespaceを使用する。
- `Assets/Scripts/Setup` 外のreflection importをGovernance CIで拒否する。
- Bootstrap変更がlifecycle/composition、Setup変更がconstruction、Services変更がuse case/policy、
  UI変更がpresentationであることをレビューする。
- 変更動作へ最も狭い境界のfocused testを追加し、Unity-facing変更ではbatchmode compileを実行する。
- 依存方向を変えるPRは、実装が新方向に依存する前に本ADRペアを更新するか後継ADRを追加する。

## 注記

- 本ADRはscene内容、visual design、save-data format、dialogue schema、将来のDI/asmdef実装を決めない。
- 現在のpackage-private reflectionは制限された負債として受け入れ、推奨統合方法にはしない。
- ADR-0004が置き換えるのはasmdef延期条項だけである。Composition、Setup/Services/UI ownership、
  reflection、明示的constructionの判断はAcceptedのまま維持する。
- 計測したgraph複雑性、複数runtime scope、test差し替えcostがcontainer overheadを上回る場合に
  明示的constructionを再検討する。
