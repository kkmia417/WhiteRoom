# Architecture Decision Records

[English canonical version](README.md)

このディレクトリは、WhiteRoomの長期的なアーキテクチャ判断、その根拠、今後の実装が
維持すべき制約を保存する。

英語ADRをエージェントと自動化が参照する正本とし、各英語ADRに人間のレビュー用となる
日本語の `.ja.md` 対訳を置く。英語と日本語は別々の判断ではなく、同じ番号、ステータス、
日付、関連作業、意味を持つ1件の判断である。

## インデックス

| ADR | 日本語の決定 | 英語の決定 | ステータス | 日付 |
| --- | --- | --- | --- | --- |
| ADR-0001 | [Talk SystemからWhiteRoom固有の方針を分離する](0001-talk-system-boundary.ja.md) | [Keep WhiteRoom product policy outside Talk System](0001-talk-system-boundary.md) | Accepted | 2026-07-18 |
| ADR-0002 | [明示的なComposition Rootとランタイム責務分割を採用する](0002-runtime-responsibility-split.ja.md) | [Use an explicit composition root and split runtime responsibilities](0002-runtime-responsibility-split.md) | Accepted | 2026-07-18 |
| ADR-0003 | [Issue駆動開発と英日ADRペアを採用する](0003-issue-driven-bilingual-adrs.ja.md) | [Use Issue-driven delivery and bilingual ADR pairs](0003-issue-driven-bilingual-adrs.md) | Accepted | 2026-07-18 |

`0000-template.md` と `0000-template.ja.md` は複製用テンプレートであり、判断には数えない。

## ADRが必要な変更

次のいずれかへ影響する変更ではADRを作成する。

- モジュール間の依存方向、所有権、信頼境界、障害境界
- 公開API、会話スキーマ、セーブデータ形式、移行戦略
- シーンライフサイクル、オブジェクト構築、永続化、シーンをまたぐ状態
- 新しいパッケージ、サービス、フレームワーク、ビルド方式、デプロイ方式
- セキュリティ、性能、信頼性、可観測性、アクセシビリティ、テスト容易性などの横断的品質
- 取り消しコストが高い、または将来繰り返し議論される可能性が高い判断

通常のバグ修正、局所的な実装詳細、容易に戻せるリファクタリング、既存ADRが既に規定する
選択には新しいADRを作成しない。

## 必須の粒度

ADRには結論だけでなく、英語版と日本語版の両方で次を記録する。

- 具体的なコンテキストと問題提起
- 優先順位を持つ決定要因
- 個別の決定事項と、それぞれの根拠・実装への影響
- 利点と、受け入れるトレードオフおよび緩和策
- 現実的な不採用案と不採用理由
- 関連ADR、および各ADRが決める範囲の境界
- コード、テスト、CI、レビューで決定を維持する開発ルール
- 対象外、未解決事項、再検討条件

1件のADRは1つの判断だけを所有する。言語・ランタイム選定、モジュール境界、永続化、
開発プロセスなど、独立して変更可能な判断は別のADRへ分割する。

## 英日正本契約

- `NNNN-short-title.md` をエージェント向け英語正本とする。
- `NNNN-short-title.ja.md` を人間向け日本語対訳とする。
- 作成、レビュー、更新、Superseded化、削除は必ずペアで行う。
- ADR番号、ステータス、日付、関連Issue/PR、決定事項、再検討条件を意味上同一に保つ。
- 日本語訳だけに独立した判断を追加しない。判断を変える場合は英語正本と日本語対訳を
  同じPRで更新する。
- `README.md` と `README.ja.md`、英日テンプレートにも同じ規則を適用する。

## ライフサイクル

1. 問題、制約、代替案、受入条件を記載したIssueを作成する。
2. `0000-template.md` と `0000-template.ja.md` の両方を、次の未使用4桁番号と
   同じlowercase kebab-caseの語幹へ複製する。
3. 両方のステータスを `Proposed` にし、同じIssueをリンクする。
4. 現実的な代替案を比較し、検証証拠を記録する。
5. 判断内容と翻訳の意味が一致していることをレビューする。
6. 実装が前提として依存する前に、両方を `Accepted` にする。
7. Accepted ADRを履歴として保持する。後継ADRを追加し、旧ADRの英日両方を
   `Superseded` にする。

利用可能なステータスは `Proposed`、`Accepted`、`Rejected`、`Deprecated`、
`Superseded` とする。

## 検証

リポジトリルートから実行する。

```powershell
python .\scripts\validate_governance.py --root .
python -m unittest discover -s .\scripts\tests -p "test_*.py"
```

検証スクリプトは連番、英日ペア、メタデータ一致、必須詳細セクション、ペアとインデックスの
リンク、リポジトリ相対リンクを検査する。翻訳内容の意味上の一致は人間がレビューする。
