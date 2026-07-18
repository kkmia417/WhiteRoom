# ADR-0004: 強制可能なAssembly境界を持つモジュラーモノリスを採用する

ステータス: Accepted<br>
日付: 2026-07-18<br>
関連: [Issue #26](https://github.com/kkmia417/WhiteRoom/issues/26) / [Issue #8](https://github.com/kkmia417/WhiteRoom/issues/8)<br>
English canonical file: [英語正本](0004-modular-monolith-boundaries.md)

## コンテキストと問題提起

WhiteRoomは現在、ゲーム固有スクリプトを `Assembly-CSharp` へコンパイルする単一Unity
アプリケーションである。ソースディレクトリと `NovelGameBootstrap` は既に有用な責務境界を
表現し、Talk Systemは独立検証可能なpackageになっている。この構成はvertical sliceには適するが、
大規模チームで循環依存、意図しないUnity依存、コンパイル時間増大、platform/storage SDKへの
直接依存を防げない。

AAAビジュアルノベルでは、narrative、presentation、content、persistence、UI、platform統合、
release toolingを多数の担当が並行制作する。一方、ゲームは単一client processとして出荷されるため、
分散serviceや独立deploy componentは、有効なfailure boundaryを作らず運用コストだけを増やす。

## 決定要因

- 1つの出荷物を維持しながら、stable contractの背後でteamが並行作業できること
- sceneをロードせずnarrativeとproduct ruleをテストできること
- Unity、Talk System、storage、content、platform SDK依存を明示すること
- 循環依存とservice locator accessを禁止すること
- assembly単位でincremental compilationとownership reviewを行えること
- ADR-0002の明示的composition modelを維持すること

## 決定結果

WhiteRoomを1つのUnity project内のモジュラーモノリスとして構成する。runtime moduleは
assembly definitionを持ち、狭いC# contractで通信する。Unityおよびvendor依存はedgeに置き、
`NovelGameBootstrap` がconcrete adapterを選ぶComposition Rootになる。

### 一方向依存を持つproduct moduleを作る

target runtime assemblyを `WhiteRoom.Core`、`WhiteRoom.Narrative`、
`WhiteRoom.Content`、`WhiteRoom.Persistence`、`WhiteRoom.Presentation`、
`WhiteRoom.Platform`、`WhiteRoom.Bootstrap` とする。

**根拠**: sceneやasset folderではなく、独立して変化するproduct責務に沿う境界になる。
**影響**: `Core` はUnity、Talk System、vendor SDKに依存しない。`Narrative` はTalk System
contractをadaptできる。Content、persistence、presentation、platform moduleはcore contractへ
依存できるが、互いのconcrete implementationへ依存しない。`Bootstrap` はcomposition時に
必要な全assemblyへ依存できる。

### policyをpure C#に置き、effectをportの背後へ置く

進行rule、content identity、save互換判断、release channel rule、use case orchestrationは、
Unity objectが不要な限りplain C#で実装する。file、clock、telemetry、content load、
cloud save、achievement、platform capabilityはproduct所有interfaceから利用する。

**根拠**: pure policyは高速にtestでき、sceneやSDK lifecycleへの暗黙依存を持たない。
**影響**: `WhiteRoom.Core` では `MonoBehaviour`、`ScriptableObject`、
`UnityEngine.Object`、static SDK singleton、直接filesystem accessを禁止する。Adapterは
failureをproduct所有result typeへ変換する。

### concrete adapterはapplication edgeだけで構成する

`NovelGameBootstrap` とfocused bootstrap installerがobject graphを構築する。runtime codeは
constructorまたはserialized composition referenceから必須collaboratorを受け取る。

**根拠**: 可視のobject graphにより、startup順序、ownership、dispose、test substitutionを
reviewできる。
**影響**: global service locator、場当たり的singleton discovery、汎用DI containerを
application APIにしない。Unity object検索はbootstrap/setupのcompatibility seamに限定し、
prefabが明示referenceを持った時点で除去する。

### assembly間contractを意図的に公開する

各moduleはimplementationを既定でinternalにし、consumerが必要とするcontractだけを公開する。
cross-module eventはtypedとし、その意味を定義するmoduleが所有する。

**根拠**: directory規約だけではAAA規模のcodebaseがshared mutable object graphになることを
防げない。
**影響**: 新しいassembly referenceにはarchitecture reviewが必要になる。Shared mutable state、
string-based global event bus、assembly境界を迂回するだけのpublic field追加をreviewで拒否する。

### feature deliveryを止めずvertical sliceで移行する

最初の移行で `Core`、1つのuse case、そのadapter、testを確立してから残りを移す。既存behaviorは
一時的に `Assembly-CSharp` に残せるが、新しいcross-cutting subsystemは追加しない。

**根拠**: 一括assembly移行はUnity serializationとmergeに大きなriskを生む。
**影響**: 本条項はADR-0002のapplication asmdef延期を置き換える。移行では `.meta` file、
serialized type identity、scene referenceを維持する。

## 利点

- directory規約ではなくcompile時にdependencyを強制できる。
- 多くのproduct ruleを高速なEditModeまたはplain C# testで確認できる。
- platform/vendor変更をreplaceable adapterへ閉じ込められる。
- 出荷clientをservice分割せずteam ownershipを確立できる。
- bootstrapとlifetime behaviorが明示される。

## トレードオフ

- assemblyとcontractに設計・compile overheadが生じる。
  → 独立した変更理由を持つ責務だけに境界を作る。
- Unity serializationではtype移動が危険になる。
  → 小さいsliceで移行し、毎回scene/prefab referenceを検証する。
- port interfaceが根拠のない抽象化になり得る。
  → 実在するUnity、package、storage、content、vendor edgeだけに導入する。

## 不採用の選択肢と根拠

| 選択肢 | 不採用理由 |
| --- | --- |
| 全game codeを `Assembly-CSharp` に維持する | teamとfeatureが増えたときdependencyとownershipを強制できない。 |
| clientをnetwork microserviceへ分割する | single-player narrative runtimeにdeployment/failure complexityを加えるが、有効なruntime isolationを得られない。 |
| 全product codeをTalk Systemへ置く | reusable dialogue infrastructureをWhiteRoomのroute、asset、platform、商用policyへ結合する。 |
| global DI containerまたはservice locatorを採用する | object ownershipを隠し、scene startupとtestをambient stateへ依存させる。 |

## 関連するADR

- [ADR-0001](0001-talk-system-boundary.ja.md) — packageとproductの境界
- [ADR-0002](0002-runtime-responsibility-split.ja.md) — compositionと現行runtime責務。
  本ADRはasmdef延期だけを置き換える
- [ADR-0003](0003-issue-driven-bilingual-adrs.ja.md) — 境界変更の進め方

## 開発ルール連携

- 禁止assembly referenceとcycleを検出するarchitecture testを追加する。
- 新しいcore policyとadapter failure mappingにはfocused testを要求する。
- `WhiteRoom.Core` のpublic contractへUnity/vendor typeを入れない。
- 新しいassembly referenceまたはshared global serviceをarchitecture impactとして扱う。
- 移行をfolder一括rewriteではなくIssue単位のvertical sliceで進める。

## 注記

- 本ADRはtarget boundaryを決めるもので、移行実装全体は含まない。
- Editor toolingはruntime contractへ依存するeditor-only assemblyを持てるが、runtime assemblyは
  editor assemblyへ依存しない。
- 独立運用backendがproduct要件となり、別のtrust/scaling boundaryを持つ場合だけ
  モジュラーモノリスを再検討する。
