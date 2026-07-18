# ADR-0003: Use Issue-driven delivery and bilingual ADR pairs

Status: Accepted<br>
Date: 2026-07-18 (2026-07-18 expanded with bilingual source rules)<br>
Related: [Issue #8](https://github.com/kkmia417/WhiteRoom/issues/8) / [PR #25](https://github.com/kkmia417/WhiteRoom/pull/25)<br>
Japanese: [日本語版](0003-issue-driven-bilingual-adrs.ja.md)

## Context and problem statement

WhiteRoom has moved from small scene experiments to changes that span the
embedded package, application runtime, UI, persistence, documentation, and
Unity assets. Existing GitHub history contains broad Issues and several merged
PRs with empty bodies. The reason for a change, its acceptance criteria, the
architecture decision, and the evidence used to validate it are therefore not
consistently connected.

Development is performed by Japanese-speaking humans and English-oriented
coding agents. Japanese-only ADRs are reviewable by maintainers but reduce the
precision and reliability of agent retrieval. English-only ADRs are efficient
for agents but make high-impact decisions unnecessarily difficult for human
maintainers to review. Maintaining independent English and Japanese documents
without a pairing contract would create two conflicting sources of truth.

The repository needs one delivery protocol that binds work to an Issue and one
ADR protocol that serves both audiences without allowing semantic drift.

## Decision drivers

- Every non-trivial change needs a stable problem statement, non-goals,
  acceptance criteria, and validation plan.
- Reviewers must trace implementation and tests back to one primary Issue.
- Durable decisions must be made before dependent implementation and preserved
  after the Issue closes.
- English architecture context must be directly consumable by agents.
- Japanese architecture context must be directly reviewable by humans.
- Translation drift must fail cheaply where metadata or file pairing differs
  and remain visible for semantic review.
- The process must stay lightweight enough for a small Unity project.
- Existing worktree changes must never be swept into an unrelated delivery.

## Decision outcome

Use the delivery chain `Issue -> ADR when required -> branch -> implementation
and tests -> linked PR -> merge`. Store every ADR as a canonical English record
and a paired Japanese human-facing translation.

### Make one primary Issue the scope boundary

**Rationale**: A stable outcome and explicit non-goals prevent implementation
from absorbing unrelated refactors and discoveries.
**Impact**: Feature, Bug, Architecture, and Task Issue forms require an outcome,
scope, acceptance criteria, architecture impact, and validation plan. Follow-up
work receives a separate Issue. Agents without an Issue prepare Issue-ready
scope and state that traceability is incomplete rather than inventing a number.

### Require PRs to reference their Issue

**Rationale**: A branch or commit message alone does not preserve the product
reason, discussion, and acceptance contract.
**Impact**: A completing PR uses `Closes #<number>`. A spike or partial change
uses `Refs #<number>` and explains why the Issue remains open. Governance CI
rejects a PR event without a recognized Issue reference.

### Create an ADR before implementation depends on a durable decision

**Rationale**: Writing architecture after code encourages documentation that
justifies the implementation instead of comparing real alternatives.
**Impact**: Changes matching the ADR criteria begin with a `Proposed` English
and Japanese pair linked to the Issue. Both become `Accepted` before dependent
implementation is treated as complete. Reversal uses a successor ADR and marks
both old files `Superseded`.

### Make English the canonical agent source

**Rationale**: Repository agents and automation operate most reliably with
stable English technical vocabulary and predictable headings.
**Impact**: `NNNN-short-title.md` contains the canonical decision for agents.
`AGENTS.md`, Codex skills, and automated architecture references link to the
English record by default.

### Maintain a Japanese human-facing counterpart

**Rationale**: Architecture approval and long-term maintenance require humans
to review context, alternatives, consequences, and constraints without a
language barrier.
**Impact**: `NNNN-short-title.ja.md` contains a faithful Japanese translation.
The index exposes both languages. Japanese files may adapt phrasing for
readability but cannot add, remove, or weaken a decision clause.

### Update the language pair atomically

**Rationale**: Independent updates produce conflicting architecture depending
on which language a reader chooses.
**Impact**: Creation, status changes, decision changes, supersession, and
deletion include both files in one PR. Number, status, date, related Issue/PR,
decision meaning, development rules, and reconsideration triggers remain
equivalent.

### Require decision-grade detail

**Rationale**: A short context/decision/consequence note does not explain why an
alternative lost or how code must preserve the decision.
**Impact**: Both languages include context and problem, decision drivers,
decision clauses with Rationale/Impact, benefits, mitigated trade-offs,
rejected alternatives, related ADRs, development-rule integration, and notes
with exclusions or reconsideration conditions.

### Automate structural checks and keep semantic review human

**Rationale**: File pairing, metadata, headings, links, and dependency rules are
deterministic; translation meaning and architectural quality are not.
**Impact**: `scripts/validate_governance.py` checks both language files,
metadata parity, required detail, numbering, index links, Markdown links, PR
traceability, and selected code boundaries. Reviewers compare decision meaning
and reject shallow or divergent translations.

## Benefits

- Issues, ADRs, code, tests, and PR evidence form one traceable chain.
- Agents receive stable English architecture instructions without translation
  at execution time.
- Human maintainers can review the full decision in Japanese.
- Required alternatives and trade-offs reduce tool-first or implementation-first
  decisions.
- Automated pair checks prevent missing or stale language variants.
- Accepted ADRs remain useful after Issues and implementation details change.
- Unrelated local Unity work is less likely to enter a governance or feature PR.

## Trade-offs

- Every ADR requires two maintained documents.
  → Reserve ADRs for durable decisions and update the pair in one focused pass.
- Structural parity does not prove semantic translation parity.
  → Require human bilingual review for decision changes and keep English
  canonical when resolving an accidental mismatch.
- Issue and PR templates add ceremony to small changes.
  → Use the bounded Task form for maintenance while retaining traceability.
- Existing work predating the policy may lack a dedicated governance Issue.
  → Link the closest originating Issue and PR, disclose bootstrap context, and
  apply the full rule to subsequent decisions.
- Detailed ADRs can become implementation documentation.
  → Record stable constraints and rationale; keep task progress and volatile
  code details in Issues and architecture guides.

## Rejected alternatives

| Alternative | Why rejected |
| --- | --- |
| Maintain Japanese ADRs only | Human review is strong, but agents must translate architecture on every task and may vary technical interpretation. |
| Maintain English ADRs only | Agent use is simple, but human approval and maintenance become unnecessarily difficult. |
| Put both languages in one file | Every section becomes noisy, anchors and diffs are harder to scan, and agents cannot load only the canonical language. |
| Maintain independent English and Japanese ADR collections | It permits different numbering, status, and decisions, creating two sources of truth. |
| Record architecture only in Issues | Issues are suitable for active discussion but poor immutable indexes after closure and do not define supersession. |
| Record decisions after implementation | It removes genuine alternative evaluation and turns ADRs into retrospective justification. |
| Rely on templates without CI | Templates help authors but do not prevent missing pairs, stale indexes, metadata drift, or unlinked PRs. |

## Related ADRs

- [ADR-0001](0001-talk-system-boundary.md) is the first package/application
  decision governed by this bilingual process.
- [ADR-0002](0002-runtime-responsibility-split.md) demonstrates splitting a broad
  runtime-boundaries decision into one independently reversible responsibility
  decision.

## Development rule integration

- `.github/ISSUE_TEMPLATE` captures outcome, non-goals, acceptance criteria,
  architecture impact, and validation before implementation.
- `.github/pull_request_template.md` captures Issue linkage, evidence, ADR
  impact, validation, risk, and rollback.
- Governance CI runs ADR-pair, link, boundary, and PR-reference checks.
- `AGENTS.md` and `$feature-delivery` require Issue extraction and ADR review
  before implementation.
- ADR PR review compares English and Japanese meaning, not only metadata.
- Commits and staging use explicit paths when unrelated worktree changes exist.

## Notes

- English is canonical for machine consumption, not more authoritative than
  maintainers. Human approval still governs acceptance.
- This ADR does not choose branch protection rules, merge strategy, label
  taxonomy, project boards, release cadence, or required reviewer count.
- Reconsider the two-file language model if repository tooling can provide
  verified translation views without duplicate source files.
