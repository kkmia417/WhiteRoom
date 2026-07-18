# ADR-0001: Talk SystemからWhiteRoom固有の方針を分離する

ステータス: Accepted<br>
日付: 2026-07-18（2026-07-18 旧ランタイム境界ADRから分割）<br>
関連: [Issue #8](https://github.com/kkmia417/WhiteRoom/issues/8) / [PR #25](https://github.com/kkmia417/WhiteRoom/pull/25)<br>
English: [English canonical version](0001-talk-system-boundary.md)

## コンテキストと問題提起

WhiteRoomはUnityで実装するビジュアルノベル兼脱出ゲームである。Issue #8では、シーン遷移、
特定の会話リソースと開始トリガー、セーブ・Continue、章・ルート・エンディングの永続解放、
製品UI、本番素材が未完成でも進行を止めない動作を含む、TitleからEndingまでのゲームループが
必要になった。

会話実行は埋め込みパッケージ `Packages/com.kkmia.talksystem` が提供する。Talk Systemは、
Runtime、Editor、EditModeテスト、PlayModeテストの各assembly、パッケージドキュメント、
Feature Tourサンプルを持つ、独立バージョン管理されたUnityパッケージ
（`com.kkmia.talksystem`、現在0.2.0）でもある。公開目的は、CSV駆動会話、分岐、検証、
セーブデータ、表示プリミティブ、拡張APIを複数プロジェクトで再利用可能にすることである。

WhiteRoomは、Talk SystemをWhiteRoom専用にせず設定・拡張する必要がある。シーン名、
`R00EscapeStart`、Title表示規則、セーブスロット表示、製品フォント、WhiteRoom固有の進行方針を
パッケージへ入れると、パッケージリリースがゲームを壊し、ゲーム変更が他の利用者を壊す。
反対に、共有動作をすべて `Assets/Scripts` へ複製すると、修正と改善がテスト済みパッケージから
分岐する。

したがって、変更を再利用パッケージとWhiteRoomアプリケーションのどちらへ置くかを判断する、
明確な所有規則が必要である。

## 決定要因

- Talk SystemがWhiteRoomのシーン、リソース、ルート、エンディング、素材、製品UIを知らずに
  他プロジェクトで再利用できること
- WhiteRoomが機能ごとにパッケージ内部をfork・編集せず製品動作を実装できること
- 会話スキーマ、セーブプリミティブ、検証、表示契約をテスト済みの1実装へ集約すること
- パッケージ変更にはパッケージのテストと文書、アプリ変更にはWhiteRoomの受入証拠を要求すること
- 汎用API設計中でも製品開発を止めないこと
- 依存方向をローカルとCIで低コストに検証できること

## 決定結果

WhiteRoomアプリケーションはTalk Systemの公開ランタイム契約へ依存してよい。Talk Systemから
`WhiteRoom.Novel`、WhiteRoomのシーン・リソース・製品方針へは依存しない。

### 再利用可能な会話機能をTalk Systemに置く

**根拠**: 会話解析、分岐、検証、セーブプリミティブ、バックログ、表示状態、入力ルーティング、
拡張interfaceは複数Unityプロジェクトで利用でき、既にパッケージassemblyとテストを持つ。
**影響**: 汎用動作の変更は `Packages/com.kkmia.talksystem` に置き、
`kkmia.TalkSystem` namespaceを維持し、パッケージのテストと文書を更新する。WhiteRoomの
リソースキーやシーン進行を前提にしない。

### ゲーム方針と製品構築を `Assets/Scripts` に置く

**根拠**: シーン名、Title動作、セーブスロット選択、プレイヤー名、解放方針、UIスタイル、
利用する会話リソースは一般的な会話エンジンではなくWhiteRoom製品を表す。
**影響**: これらを `WhiteRoom.Novel` namespaceに保持する。`NovelGameBootstrap`、
`Services`、`UI`、`Setup` はTalk System公開APIを呼べるが、製品条件をパッケージへ移さない。

### 公開契約と狭いAdapterで統合する

**根拠**: Talk Systemは `IDialogueConditionEvaluator`、`IDialogueVariableResolver`、
`IDialogueSaveStorage`、表示interfaceなどの拡張点を既に提供する。これらへの依存は
パッケージ所有権を保ちながら製品動作を実現できる。
**影響**: WhiteRoomは `PlayerNameVariableResolver`、`DialogueProgressService` などの
製品Adapterを実装する。新しい統合要求ではまず小さな公開パッケージ契約を検討し、
WhiteRoom固有型をその契約へ追加しない。

### 再利用性が実証できる動作だけをパッケージへ昇格する

**根拠**: 早すぎる汎用化は第2の用途がない設定とAPIを増やし、昇格を一切しない方針は
会話エンジンを重複させる。
**影響**: 昇格提案ではWhiteRoom以外の利用例を少なくとも1つ示し、製品非依存APIを定義し、
パッケージテストと必要な文書・CHANGELOGを更新する。WhiteRoom設定はアプリ側へ残す。

### 埋め込みパッケージを独立検証可能な境界として扱う

**根拠**: 共同開発のため同じリポジトリへ配置しているが、`package.json`、assembly定義、
テスト、サンプル、文書は独立利用可能な成果物を表す。
**影響**: 両側を変更する場合、先にパッケージ契約、次にWhiteRoom利用側を説明する。
パッケージテストでproducerを、WhiteRoomのcompile・統合検証でconsumerを確認する。

### 逆依存禁止を自動検証する

**根拠**: ディレクトリ規約だけでは高速な機能開発中に劣化する。
**影響**: `scripts/validate_governance.py` は `Packages/com.kkmia.talksystem` 以下の
C#が `WhiteRoom.Novel` を参照した場合に失敗する。namespace参照に現れないシーン名、
asset path、製品設定への依存もレビューで拒否する。

## 利点

- Talk Systemを独立して再利用、テスト、文書化、リリースできる。
- WhiteRoomは汎用会話エンジンを不安定にせず製品動作を変更できる。
- 共有会話動作を1実装と1つのパッケージテスト群へ集約できる。
- 統合点が隠れたパッケージ編集ではなく明示的なAdapterになる。
- ファイル配置の都合ではなく製品固有性で所有先をレビューできる。
- 障害を契約の正しい側で診断できる。

## トレードオフ

- WhiteRoom側にパッケージプリミティブを包むAdapterと調停コードが必要になる。
  → Adapterを狭く製品名で表し、再利用性を実証したロジックだけを昇格する。
- 一部の機能はパッケージとアプリの協調変更になる。
  → 同じIssueまたは明示的に依存するIssueで、公開契約とパッケージテストをconsumer更新より先に届ける。
- 埋め込みパッケージはinternal field・method利用を誘発しやすい。
  → 公開契約を優先し、一時的互換seamはADR-0002に従い `Setup` へ隔離して撤去条件を追跡する。
- namespace検査だけでは意味上の製品依存をすべて検出できない。
  → CIに加え、PRテンプレートとアーキテクチャレビューで所有先を確認する。

## 不採用の選択肢と根拠

| 選択肢 | 不採用理由 |
| --- | --- |
| WhiteRoomの会話動作をすべてTalk Systemへ置く | 1ゲームのシーン、リソースキー、進行、UIへ結合し、独立した再利用とリリースができない。 |
| Talk Systemコードを `Assets/Scripts` へ複製する | パッケージ境界は消えるが、テスト済み動作が重複・分岐し、パッケージ文書とサンプルも失う。 |
| WhiteRoom専用Talk Systemをforkする | 非互換要件が実在しない段階で2つのエンジンと継続的なmerge負担を作る。 |
| WhiteRoom作業からパッケージ変更を全面禁止する | 分離は守れるが、実利用者が発見した正当な汎用契約の改善まで妨げる。 |
| WhiteRoom機能を常に即時汎用化する | 他利用者の証拠がないAPI surfaceと設定複雑性を増やす。 |

## 関連するADR

- [ADR-0002](0002-runtime-responsibility-split.ja.md) — この境界のWhiteRoom側における
  責務と依存方向
- [ADR-0003](0003-issue-driven-bilingual-adrs.ja.md) — パッケージ・アプリ間の契約変更を
  提案、レビュー、文書化、検証する方法

## 開発ルール連携

- パッケージC#から `WhiteRoom.Novel` を参照せず、Governance CIで検査する。
- 製品シーン名、会話resource path、trigger key、font、Title動作、unlock方針を `Assets/` に置く。
- 汎用パッケージ変更にはfocused package testと関連する `Documentation~` 更新を含める。
- 境界をまたぐPRはproducer契約、consumer変更、両側の検証を記載する。
- パッケージ非公開memberの利用には、ADR-0002に従うcompatibility seamと撤去条件を要求する。

## 注記

- 本ADRはUnity採用、会話CSVスキーマ、セーブ形式、パッケージ配布先、アプリのフォルダ・assembly
  レイアウトを決定しない。
- ADR-0002がアプリ構築と一時的reflection seamを決定する。
- Talk Systemを独立利用しなくなった場合、またはWhiteRoomが意図的に非互換な会話エンジンを
  必要とする場合にだけ再検討する。
