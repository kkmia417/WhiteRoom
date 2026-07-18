# ADR-0001: Keep WhiteRoom product policy outside Talk System

Status: Accepted<br>
Date: 2026-07-18 (2026-07-18 split from the former runtime-boundaries ADR)<br>
Related: [Issue #8](https://github.com/kkmia417/WhiteRoom/issues/8) / [PR #25](https://github.com/kkmia417/WhiteRoom/pull/25)<br>
Japanese: [日本語版](0001-talk-system-boundary.ja.md)

## Context and problem statement

WhiteRoom is a Unity visual novel and escape game. Issue #8 requires a complete
title-to-ending loop: scene transitions, a specific dialogue resource and start
trigger, save/continue behavior, persistent chapter/route/ending unlocks,
product UI, and graceful behavior while production media is incomplete.

Dialogue execution is supplied by the embedded
`Packages/com.kkmia.talksystem` package. Talk System is also an independently
versioned Unity package (`com.kkmia.talksystem`, currently 0.2.0) with Runtime,
Editor, EditMode test, and PlayMode test assemblies, package documentation, and
a Feature Tour sample. Its public purpose is reusable CSV-driven dialogue,
branching, validation, save data, presentation primitives, and extension APIs.

WhiteRoom must configure and extend those capabilities without turning Talk
System into a package that only works for WhiteRoom. If scene names, the
`R00EscapeStart` trigger, title-menu rules, save-slot presentation, product
fonts, or WhiteRoom progression policy enter the package, package releases can
break the game and game changes can break unrelated package consumers. If all
shared behavior is copied into `Assets/Scripts`, defects and improvements will
instead diverge from the tested package implementation.

The repository therefore needs a precise ownership rule for deciding whether a
change belongs in the reusable package or the WhiteRoom application.

## Decision drivers

- Talk System must remain reusable by projects that do not know WhiteRoom
  scenes, resources, routes, endings, art, or product UI.
- WhiteRoom must be able to implement product behavior without forking or
  editing package internals for every feature.
- Dialogue schemas, save primitives, validation, and presentation contracts
  should have one tested implementation.
- Package changes need package-level tests and documentation; application
  changes need WhiteRoom acceptance evidence.
- Product delivery must not be blocked while generic APIs are being designed.
- The dependency direction must be cheap to verify in local checks and CI.

## Decision outcome

WhiteRoom application code may depend on Talk System's public runtime
contracts. Talk System must not depend on `WhiteRoom.Novel`, WhiteRoom scenes,
WhiteRoom resources, or WhiteRoom product policy.

### Keep reusable dialogue capabilities in Talk System

**Rationale**: Dialogue parsing, branching, validation, save primitives,
backlog, presentation state, input routing, and extension interfaces are useful
to multiple Unity projects and already have package assemblies and tests.
**Impact**: Generic behavior changes go under `Packages/com.kkmia.talksystem`,
retain the `kkmia.TalkSystem` namespace, update package tests and documentation,
and avoid assumptions about WhiteRoom resource keys or scene flow.

### Keep game policy and product composition in `Assets/Scripts`

**Rationale**: Scene names, title behavior, save-slot choices, player naming,
unlock policy, UI styling, and the selected dialogue resource express
WhiteRoom's product, not a general dialogue engine.
**Impact**: These behaviors remain in the `WhiteRoom.Novel` namespace.
`NovelGameBootstrap`, `Services`, `UI`, and `Setup` may call public Talk System
APIs but do not move product conditions into the package.

### Integrate through public contracts and narrow adapters

**Rationale**: Talk System already exposes extension points such as
`IDialogueConditionEvaluator`, `IDialogueVariableResolver`,
`IDialogueSaveStorage`, and presentation interfaces. Depending on these
contracts preserves package ownership while allowing product behavior.
**Impact**: WhiteRoom implements product adapters such as
`PlayerNameVariableResolver` and `DialogueProgressService`. New integration
needs should first seek a small public package contract. WhiteRoom-specific
types must not be added to that contract.

### Promote behavior to the package only when it is demonstrably generic

**Rationale**: Premature generalization creates configuration and API surface
without a second use case; never promoting behavior creates duplicated engines.
**Impact**: A promotion proposal identifies at least one non-WhiteRoom use case,
defines a product-neutral API, adds package tests, updates package
documentation/changelog as appropriate, and leaves WhiteRoom configuration in
the application.

### Treat the embedded package as an independently verifiable boundary

**Rationale**: The package is checked into this repository for coordinated
development, but its `package.json`, assembly definitions, tests, sample, and
documentation describe a separately consumable artifact.
**Impact**: A change that touches both sides explains the package contract first
and the WhiteRoom consumer second. Package tests validate the producer;
WhiteRoom compile or integration checks validate the consumer.

### Enforce the reverse-dependency prohibition automatically

**Rationale**: Directory guidance alone can decay during fast feature work.
**Impact**: `scripts/validate_governance.py` rejects C# source under
`Packages/com.kkmia.talksystem` that references `WhiteRoom.Novel`. Reviews also
reject package dependencies on WhiteRoom scene names, asset paths, and product
configuration even when no namespace reference reveals them.

## Benefits

- Talk System remains independently reusable, testable, documented, and
  releasable.
- WhiteRoom can evolve product behavior without destabilizing the generic
  dialogue engine.
- Shared dialogue behavior has one implementation and one package test suite.
- Integration points become explicit adapters instead of hidden package edits.
- Reviewers can decide ownership using product specificity rather than file
  convenience.
- Package and application failures can be diagnosed on the correct side of the
  contract.

## Trade-offs

- WhiteRoom needs adapter and orchestration code around package primitives.
  → Keep adapters narrow and product-named; promote only proven reusable logic.
- Some features require coordinated package and application changes.
  → Deliver the public contract and its package tests before updating the
  WhiteRoom consumer in the same Issue or an explicitly linked dependency.
- An embedded package can tempt developers to use internal fields and methods.
  → Prefer public contracts; isolate temporary compatibility seams in `Setup`
  under ADR-0002 and track their removal.
- Namespace scanning cannot detect every semantic product dependency.
  → Combine CI enforcement with the ownership questions in the PR template and
  architecture review.

## Rejected alternatives

| Alternative | Why rejected |
| --- | --- |
| Put all WhiteRoom dialogue behavior in Talk System | It couples the package to one game's scenes, resource keys, progression, and UI, preventing independent reuse and release. |
| Copy Talk System code into `Assets/Scripts` | It removes the package boundary but duplicates tested behavior, causes fixes to diverge, and loses package-level documentation and samples. |
| Fork a WhiteRoom-specific Talk System package | It creates two engines and a recurring merge burden before an actual incompatible product requirement exists. |
| Forbid all package changes from WhiteRoom work | It preserves isolation but blocks legitimate improvements to generic contracts discovered by a real consumer. |
| Generalize every WhiteRoom feature immediately | It increases API surface and configuration complexity without evidence that another consumer needs the abstraction. |

## Related ADRs

- [ADR-0002](0002-runtime-responsibility-split.md) defines responsibility and
  dependency direction inside the WhiteRoom application side of this boundary.
- [ADR-0003](0003-issue-driven-bilingual-adrs.md) defines how package/application
  contract changes are proposed, reviewed, documented, and validated.

## Development rule integration

- Package C# must not reference `WhiteRoom.Novel`; governance CI scans this
  direction.
- Product scene names, dialogue resource paths, trigger keys, fonts, title
  behavior, and unlock policy remain under `Assets/`.
- Generic package changes include focused package tests and update relevant
  `Packages/com.kkmia.talksystem/Documentation~` pages.
- Cross-boundary PRs state the producer contract, consumer change, and
  validation on both sides.
- Direct use of package non-public members requires a documented compatibility
  seam and follow-up removal condition under ADR-0002.

## Notes

- This ADR does not choose Unity, the dialogue CSV schema, a save-data format,
  package distribution repository, or application folder/assembly layout.
- ADR-0002 decides application composition and the temporary reflection seam.
- Reconsider this boundary only if Talk System stops being independently
  consumed, or WhiteRoom requires an intentionally incompatible dialogue engine.
