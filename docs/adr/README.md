# Architecture Decision Records

[日本語版](README.ja.md)

This directory preserves WhiteRoom's durable architecture decisions, their
rationale, and the constraints that future implementation must maintain.

English ADRs are the canonical machine-readable source for agents and
automation. Every English ADR has a Japanese `.ja.md` counterpart for human
review. The pair represents one decision: it shares the same number, status,
date, related work, and meaning.

## Index

| ADR | English decision | Japanese decision | Status | Date |
| --- | --- | --- | --- | --- |
| ADR-0001 | [Keep WhiteRoom product policy outside Talk System](0001-talk-system-boundary.md) | [Talk SystemからWhiteRoom固有の方針を分離する](0001-talk-system-boundary.ja.md) | Accepted | 2026-07-18 |
| ADR-0002 | [Use an explicit composition root and split runtime responsibilities](0002-runtime-responsibility-split.md) | [明示的なComposition Rootとランタイム責務分割を採用する](0002-runtime-responsibility-split.ja.md) | Accepted | 2026-07-18 |
| ADR-0003 | [Use Issue-driven delivery and bilingual ADR pairs](0003-issue-driven-bilingual-adrs.md) | [Issue駆動開発と英日ADRペアを採用する](0003-issue-driven-bilingual-adrs.ja.md) | Accepted | 2026-07-18 |
| ADR-0004 | [Structure the game as a modular monolith with enforced assembly boundaries](0004-modular-monolith-boundaries.md) | [強制可能なAssembly境界を持つモジュラーモノリスを採用する](0004-modular-monolith-boundaries.ja.md) | Accepted | 2026-07-18 |
| ADR-0005 | [Standardize the client on Unity 6.3 LTS, URP, and uGUI](0005-unity-urp-runtime-baseline.md) | [Unity 6.3 LTS、URP、uGUIをclient標準にする](0005-unity-urp-runtime-baseline.ja.md) | Accepted | 2026-07-18 |
| ADR-0006 | [Deliver production content through Addressables and immutable content identities](0006-addressable-content-delivery.md) | [Addressablesと不変content identityでproduction contentを配信する](0006-addressable-content-delivery.ja.md) | Accepted | 2026-07-18 |
| ADR-0007 | [Separate narrative localization from product UI localization](0007-localization-source-contract.md) | [Narrative localizationとproduct UI localizationを分離する](0007-localization-source-contract.ja.md) | Accepted | 2026-07-18 |
| ADR-0008 | [Preserve player progress with a versioned save envelope and explicit migrations](0008-versioned-save-compatibility.md) | [Versioned save envelopeと明示migrationでplayer progressを守る](0008-versioned-save-compatibility.ja.md) | Accepted | 2026-07-18 |
| ADR-0009 | [Drive presentation through a deterministic, cancellable cue runtime](0009-deterministic-presentation-runtime.md) | [Deterministicかつcancel可能なcue runtimeでpresentationを駆動する](0009-deterministic-presentation-runtime.ja.md) | Accepted | 2026-07-18 |
| ADR-0010 | [Gate releases with automated quality evidence and platform adapters](0010-release-quality-platform-boundary.md) | [自動品質evidenceとplatform adapterでreleaseをgateする](0010-release-quality-platform-boundary.ja.md) | Accepted | 2026-07-18 |

`0000-template.md` and `0000-template.ja.md` are copyable templates, not
decisions.

## When an ADR is required

Create an ADR when a proposed change affects at least one of these:

- dependency direction, ownership, trust, or failure boundaries;
- a public API, dialogue schema, save-data format, or migration strategy;
- scene lifecycle, object composition, persistence, or cross-scene state;
- a new package, service, framework, build system, or deployment mechanism;
- a cross-cutting quality attribute such as security, performance, reliability,
  observability, accessibility, or testability;
- a decision that is expensive to reverse or likely to be questioned again.

Do not create an ADR for a routine bug fix, local implementation detail,
reversible refactor, or a choice already governed by an accepted ADR.

## Required granularity

An ADR must record more than a conclusion. Both language versions contain:

- context and a concrete problem statement;
- prioritized decision drivers;
- explicit decision clauses, each with rationale and implementation impact;
- benefits and accepted trade-offs with mitigations;
- credible rejected alternatives and reasons;
- related ADRs and the boundary between their decisions;
- development rules that enforce the decision through code, tests, CI, or
  review;
- exclusions, unresolved questions, and reconsideration triggers.

One ADR owns one decision. Split language/runtime selection, module boundaries,
persistence, delivery process, and other independently reversible choices into
separate records.

## Bilingual source contract

- `NNNN-short-title.md` is the canonical English record for agents.
- `NNNN-short-title.ja.md` is the Japanese human-facing counterpart.
- Create, review, update, supersede, and delete the pair atomically.
- Keep ADR number, status, date, related Issue/PR links, decision clauses, and
  reconsideration conditions semantically equivalent.
- A translation must not add an independent decision. Change the English
  canonical record and its Japanese counterpart in the same PR.
- `README.md` and `README.ja.md`, and both templates, follow the same pairing
  rule.

## Lifecycle

1. Open an Issue with the problem, constraints, alternatives, and acceptance
   criteria.
2. Copy both `0000-template.md` and `0000-template.ja.md` to the next unused
   zero-padded number and identical lowercase kebab-case stem.
3. Set both statuses to `Proposed` and link the same Issue.
4. Compare credible alternatives and record validation evidence.
5. Review decision meaning and translation consistency.
6. Change both statuses to `Accepted` before implementation depends on the
   decision.
7. Preserve accepted ADRs as history. Add a successor ADR and mark both language
   versions of the old record `Superseded`.

Allowed statuses are `Proposed`, `Accepted`, `Rejected`, `Deprecated`, and
`Superseded`.

## Validation

From the repository root:

```powershell
python .\scripts\validate_governance.py --root .
python -m unittest discover -s .\scripts\tests -p "test_*.py"
```

The checker validates contiguous numbering, language pairs, metadata parity,
required detail sections, pair/index links, and repository-relative links. A
human reviewer remains responsible for semantic translation accuracy.
