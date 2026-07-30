# ADR-0012: Persist run-scoped story facts in coherent dialogue snapshots

Status: Proposed<br>
Date: 2026-07-30<br>
Related: [Issue #22](https://github.com/kkmia417/WhiteRoom/issues/22) / [Issue #41](https://github.com/kkmia417/WhiteRoom/issues/41)<br>
Japanese counterpart: [日本語版](0012-run-scoped-story-facts.ja.md)

## Context and problem statement

The shipped scenario emits stable `EventKey` facts, and
`DialogueProgressService` can evaluate them through `event:` conditions. Those
facts currently live only in an in-memory `HashSet`. A slot load, reached-boundary
jump, or new-game transition can therefore evaluate the same choice differently
from the story state that produced it. Global chapter, route, and ending unlocks
already persist separately and must not be confused with facts from one run.

Issue #22 cannot safely introduce conditional choices until the lifetime,
snapshot, rollback, migration, and unknown-key behavior of run facts is explicit.
Issue #41 also needs coherent condition state when it jumps to a reached node.

## Decision drivers

- Save/Load and reached-node navigation must reproduce the choices visible at the
  captured checkpoint.
- New Game must not inherit decisions from an earlier run.
- Global chapter, route, and ending unlocks must survive New Game.
- Existing save slots without story-fact data must remain loadable.
- Talk System must remain independent of WhiteRoom story semantics.
- Rollback and invalid condition keys must not unlock unintended branches.

## Decision outcome

WhiteRoom will treat emitted story `EventKey` values as run-scoped facts and
persist them as a versioned product-owned save contributor. Global progress
markers remain in the existing unlock registry.

### Store run facts in coherent snapshots

`DialogueProgressService` will implement `IDialogueSaveContributor` and store a
sorted, duplicate-free fact list in `DialogueSaveData.ExtraState` under
`whiteroom.story-facts.v1`. Capture and restore participate in normal slot saves
and in reached-boundary snapshots.

**Rationale**: The existing contributor boundary captures application state in
the same transaction as dialogue and presentation without adding WhiteRoom
policy to Talk System.
**Impact**: Restore replaces the complete in-memory fact set atomically. Missing
payloads mean an empty set. Malformed or future-version payloads are ignored with
a warning while the base save remains usable. The payload stores stable keys,
not dialogue text, row order, or Unity objects.

### Separate run facts from global unlocks

New Game clears run facts before starting the scenario. It does not clear
chapter, route, ending, gallery, or other global unlock records. Loading a slot
replaces run facts with that slot's snapshot.

**Rationale**: A choice made in one playthrough should affect that playthrough,
while collection and completion progress intentionally spans playthroughs.
**Impact**: Conditions use `event:<key>` for run facts and `chapter:`, `route:`,
`ending:`, or `unlock:<id>` for global progress. Scenario review must classify
every new condition into one of these lifetimes.

### Make rollback restore condition truth

Product rollback will rebuild run facts from the restored dialogue history after
Talk System restores its previous line snapshot. Reached-boundary and Flowchart
jumps use contributor restore instead. Event dispatch is not replayed during any
restore operation.

**Rationale**: Keeping facts reached only on discarded future lines would allow a
player to unlock choices from a branch that no longer exists in the restored run.
**Impact**: Product code must route player rollback through the bootstrap adapter.
Tests cover choice/event rollback, slot restore, boundary restore, and the absence
of duplicate event side effects.

### Fail closed for unknown condition namespaces and preserve a fallback choice

Scenario conditions use explicit supported namespaces. Unknown or empty positive
keys evaluate false and produce validation evidence. Every filtered choice node
must retain at least one unconditional choice for every reachable fact set.

**Rationale**: A typo must not reveal content or produce a zero-choice soft lock.
**Impact**: Route simulation enumerates relevant fact states, validates positive
and negative conditions, and proves all 14 published endings remain reachable by
at least one reviewed route.

## Benefits

- Save, Load, rollback, boundary navigation, and future Flowchart jumps agree on
  the run facts used to render choices.
- Existing saves migrate safely through an absent optional payload.
- Story conditions remain data-driven without changing the Talk System schema.
- Global completion progress retains its current cross-run behavior.

## Trade-offs

- Event facts duplicate information present in dialogue history.
  → The explicit set makes evaluation deterministic and compact; rollback alone
  rebuilds from history because package rollback snapshots do not run contributors.
- Renaming a published EventKey becomes a save-compatibility concern.
  → Published keys are stable IDs; renames require an alias or payload migration.
- Condition filtering increases route-test combinations.
  → The reviewed condition table limits conditional sites and supplies canonical
  fixtures rather than attempting an unbounded state search.

## Rejected alternatives

| Alternative | Why rejected |
| --- | --- |
| Keep EventKeys memory-only | Load and navigation would show choices inconsistent with the captured run. |
| Store all story facts as global unlocks | New Game would inherit prior decisions and collapse playthrough-specific branching. |
| Infer all facts from current row only | Rejoined routes deliberately lose the earlier choice context needed by later conditions. |
| Add WhiteRoom fact fields to Talk System save DTOs | It reverses the package dependency boundary and requires reusable infrastructure to know product semantics. |
| Let unknown keys pass | Typos could reveal branches and make validation nondeterministic. |

## Related ADRs

- [ADR-0001](0001-talk-system-boundary.md) keeps WhiteRoom policy outside Talk System.
- [ADR-0008](0008-versioned-save-compatibility.md) defines versioned optional
  product state and safe migration.
- [ADR-0011](0011-reached-boundary-navigation.md) defines coherent reached-node
  snapshots that will include this contributor.

## Development rule integration

- Keep the approved condition table and canonical all-ending routes in source.
- Test new-game reset, Save/Load replacement, legacy/malformed/future payloads,
  rollback reconstruction, boundary restore, undefined keys, and zero-choice guards.
- Validate every condition key against an explicit catalog derived from the
  reviewed table.
- Do not change scenario conditions until ADR-0012 and the Issue #22 condition
  table are approved.

## Notes

- This decision does not choose which authored branches use conditions; that is
  the story-owner decision in the paired condition specification.
- Cross-device global progression, cloud conflicts, and new condition-expression
  grammar are outside this decision.
- Reconsider if Talk System later provides product-neutral contributor-aware
  rollback or if story facts require typed values rather than boolean presence.
