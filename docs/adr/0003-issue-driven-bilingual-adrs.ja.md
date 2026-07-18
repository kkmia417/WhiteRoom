# ADR-0003: Issue駆動開発と英日ADRペアを採用する

ステータス: Accepted<br>
日付: 2026-07-18（2026-07-18 英日正本規則を追加）<br>
関連: [Issue #8](https://github.com/kkmia417/WhiteRoom/issues/8) / [PR #25](https://github.com/kkmia417/WhiteRoom/pull/25)<br>
English: [English canonical version](0003-issue-driven-bilingual-adrs.md)

## コンテキストと問題提起

WhiteRoomは小規模なscene実験から、埋め込みpackage、application runtime、UI、永続化、
documentation、Unity assetをまたぐ変更へ進んだ。既存GitHub履歴には広いIssueと本文が空のまま
mergeされたPRがあり、変更理由、受入条件、architecture判断、検証証拠が一貫して接続されていない。

開発は日本語話者の人間と、英語を中心に動作するcoding agentが行う。日本語ADRだけではmaintainerが
reviewしやすい一方、agentの検索精度と再現性が下がる。英語ADRだけではagentに効率的だが、重要判断を
人間がreviewする負担が不必要に上がる。英語・日本語をpair契約なしに独立管理すると、矛盾する2つの
source of truthになる。

したがって、作業をIssueへ結び付けるdelivery protocolと、意味を分岐させず両方の読者へ提供する
ADR protocolが必要である。

## 決定要因

- 重要変更に安定した問題提起、non-goal、受入条件、検証計画があること
- 実装とtestを1つのprimary Issueへ追跡できること
- 永続判断を依存実装より先に行い、Issue close後も履歴として保つこと
- 英語architecture contextをagentが直接利用できること
- 日本語architecture contextを人間が直接reviewできること
- file pair・metadata差異を低コストで失敗させ、意味差異をreviewで可視化すること
- 小規模Unity projectでも維持可能な軽さであること
- 無関係なworktree変更を別deliveryへ混入させないこと

## 決定結果

`Issue -> 必要なADR -> branch -> 実装とtest -> linked PR -> merge` のdelivery chainを採用する。
すべてのADRをagent向け英語正本と、人間向け日本語対訳のpairで保存する。

### 1つのprimary Issueをscope境界にする

**根拠**: 安定したoutcomeと明示的non-goalは、実装が無関係なrefactorや発見事項を吸収することを防ぐ。
**影響**: Feature、Bug、Architecture、TaskのIssue formでoutcome、scope、受入条件、
architecture impact、validation planを要求する。follow-upは別Issueにする。Issueがないagentは
番号を捏造せずIssue-ready scopeを作り、traceability未完了を明示する。

### PRにIssue参照を要求する

**根拠**: branch名やcommit messageだけでは、製品理由、議論、受入契約を保存できない。
**影響**: 完了PRは `Closes #<number>` を使う。spikeまたは部分変更は `Refs #<number>` と、
Issueをopenのままにする理由を記載する。Governance CIは認識可能なIssue参照のないPR eventを拒否する。

### 永続判断へ実装が依存する前にADRを作る

**根拠**: code完成後のarchitecture記録は、現実的な代替案比較ではなく実装の事後正当化になりやすい。
**影響**: ADR条件に該当する変更では、同じIssueをlinkした英日 `Proposed` pairを先に作る。
依存実装を完了扱いする前に両方を `Accepted` にする。判断変更は後継ADRを作り、旧英日fileを
`Superseded` にする。

### 英語をagent向け正本にする

**根拠**: Repository agentと自動化は、安定した英語技術用語と予測可能なheadingで最も再現性が高い。
**影響**: `NNNN-short-title.md` をagent向けcanonical decisionとする。`AGENTS.md`、Codex skill、
自動architecture参照は既定で英語版へlinkする。

### 日本語の人間向け対訳を維持する

**根拠**: Architecture承認と長期保守では、人間がlanguage barrierなしにcontext、alternative、
consequence、constraintをreviewできる必要がある。
**影響**: `NNNN-short-title.ja.md` に忠実な日本語訳を置き、indexから両言語を公開する。
日本語fileは可読性のため表現を調整できるが、decision clauseを追加・削除・弱体化しない。

### language pairをatomicに更新する

**根拠**: 独立更新すると、選択言語によって異なるarchitectureになる。
**影響**: 作成、status変更、decision変更、supersession、削除を同じPRで両fileへ適用する。
番号、status、date、関連Issue/PR、decision meaning、development rule、再検討条件を同等に保つ。

### 判断として十分な粒度を要求する

**根拠**: 短いcontext・decision・consequenceだけでは、代替案がなぜ負けたか、codeが判断をどう
維持するかを説明できない。
**影響**: 両言語にcontext/problem、decision driver、根拠・影響付きdecision clause、benefit、
緩和策付きtrade-off、rejected alternative、related ADR、development-rule integration、
対象外・再検討条件を含むnotesを要求する。

### 構造検査を自動化し、意味reviewを人間に残す

**根拠**: file pair、metadata、heading、link、依存規則は決定論的だが、翻訳の意味とarchitecture品質は
決定論的でない。
**影響**: `scripts/validate_governance.py` が両言語file、metadata parity、必須粒度、連番、
index link、Markdown link、PR traceability、選択したcode boundaryを検査する。Reviewerは判断意味を
比較し、浅い記述または意味がずれた翻訳を拒否する。

## 利点

- Issue、ADR、code、test、PR evidenceが1つのtraceable chainになる。
- Agentが実行時翻訳なしに安定した英語architecture instructionを得られる。
- Human maintainerが判断全体を日本語でreviewできる。
- 代替案とtrade-offの必須化によりtool-first・implementation-first判断を減らせる。
- pair自動検査で欠落または古い言語variantを防げる。
- Issueや実装詳細が変化してもAccepted ADRが有効な履歴として残る。
- 無関係なlocal Unity作業をgovernance・feature PRへ混入させにくい。

## トレードオフ

- ADRごとに2文書を保守する必要がある。
  → ADRを永続判断に限定し、pairを1回のfocused passで更新する。
- 構造一致では翻訳の意味一致を証明できない。
  → decision変更に人間のbilingual reviewを要求し、事故差異の解消では英語正本を基準にする。
- Issue・PR templateは小変更にもceremonyを追加する。
  → traceabilityを維持したまま、保守にはbounded Task formを使う。
- Policy以前の作業には専用governance Issueがない場合がある。
  → 最も近いoriginating IssueとPRをlinkしてbootstrap contextを開示し、以後の判断へ完全規則を適用する。
- 詳細ADRがimplementation documentation化し得る。
  → 安定constraintとrationaleを記録し、task progressと変動code detailはIssue・architecture guideへ置く。

## 不採用の選択肢と根拠

| 選択肢 | 不採用理由 |
| --- | --- |
| 日本語ADRだけを管理する | 人間reviewには強いが、agentがtaskごとにarchitectureを翻訳し解釈が変動する。 |
| 英語ADRだけを管理する | Agent利用は単純だが、人間の承認と長期保守が不必要に難しくなる。 |
| 1fileに両言語を書く | 全sectionが読みにくく、anchorとdiffも追いにくく、agentが正本言語だけをloadできない。 |
| 英語ADR群と日本語ADR群を独立管理する | 番号、status、decisionの分岐を許し、2つのsource of truthを作る。 |
| ArchitectureをIssueだけに記録する | Active discussionには適するが、close後のimmutable indexとsupersession管理に弱い。 |
| 実装後にdecisionを記録する | 現実的な代替案評価を失い、ADRを事後正当化へ変える。 |
| CIなしでtemplateだけに依存する | Author支援にはなるが、pair欠落、index stale、metadata drift、unlinked PRを防げない。 |

## 関連するADR

- [ADR-0001](0001-talk-system-boundary.ja.md) — この英日processで管理する最初の
  package/application判断
- [ADR-0002](0002-runtime-responsibility-split.ja.md) — 広すぎるruntime境界判断を、
  独立して変更可能な責務判断へ分割した例

## 開発ルール連携

- `.github/ISSUE_TEMPLATE` で実装前にoutcome、non-goal、受入条件、architecture impact、
  validationを取得する。
- `.github/pull_request_template.md` でIssue link、evidence、ADR impact、validation、risk、
  rollbackを取得する。
- Governance CIでADR pair、link、boundary、PR referenceを検査する。
- `AGENTS.md` と `$feature-delivery` で実装前のIssue抽出とADR reviewを要求する。
- ADR PR reviewではmetadataだけでなく英日判断意味を比較する。
- 無関係なworktree変更がある場合、commitとstageで明示pathを使う。

## 注記

- 英語はmachine consumptionの正本であり、maintainerより上位の権限を持つ意味ではない。
  Acceptanceは人間の承認に従う。
- 本ADRはbranch protection、merge strategy、label taxonomy、project board、release cadence、
  required reviewer数を決定しない。
- source fileを重複させず検証済みtranslation viewを提供できるtoolingが導入された場合、
  2file方式を再検討する。
