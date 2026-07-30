# ADR-0011: Navigate dialogue through reached boundary snapshots

Status: Accepted<br>
Date: 2026-07-30<br>
Related: [Issue #40](https://github.com/kkmia417/WhiteRoom/issues/40)<br>
Japanese counterpart: [日本語版](0011-reached-boundary-navigation.ja.md)

## Context and problem statement

Issue #40 adds previous/next scene and choice commands. A dialogue row index is
not enough to implement them: routes can branch, chapter keys can repeat, choice
conditions can change, and jumping by starting a row would replay events and
progress unlocks. A jump also has to restore dialogue history, stage, music,
voice, and other save contributors coherently.

Talk System owns dialogue graph and save primitives, while WhiteRoom owns the
product navigation policy. The boundary between them needs a durable contract
that preserves old saves and does not expose WhiteRoom concepts from the package.

## Decision drivers

- Never jump beyond a boundary reached in the current saved journey.
- Never select a choice or replay line side effects as part of a jump.
- Restore dialogue and presentation contributors as one coherent snapshot.
- Give every scene and choice boundary a stable, deterministic identity.
- Preserve availability across ordinary Save/Load without breaking old saves.
- Return classified, non-blocking failures for invalid or unavailable jumps.

## Decision outcome

WhiteRoom records coherent Talk System save snapshots when the player reaches a
scene or choice boundary and navigates only among those recorded snapshots.

### Define boundaries from dialogue identity, not UI position

Scene boundaries are rows with `ChapterKey` and use
`scene:<chapter-key>:<dialogue-id>`. Choice boundaries are rows with choices and
use `choice:<dialogue-id>`. Repository row order is the deterministic catalog
order; actual journey order is the reached-checkpoint order.

**Rationale**: Dialogue IDs and chapter keys survive UI layout changes, and the
dialogue ID disambiguates a chapter key reused on multiple branches.
**Impact**: WhiteRoom services query `IDialogueRepository`; UI controllers never
mutate rows or derive targets from button indices.

### Capture and restore contributors without persistence I/O

Talk System exposes in-memory capture and restore on `DialogueSaveSystem`. Both
operations include registered `IDialogueSaveContributor` instances and may
exclude the caller contributor to prevent recursive snapshots.

**Rationale**: The existing save envelope already restores narrative, backlog,
stage, BGM, and voice coherently and `DialogueManager.RestoreState` does not emit
line-start or event side effects.
**Impact**: Slot Save/Load and navigation share one capture/restore path. The API
does not write files, allocate slots, or introduce WhiteRoom types.

### Treat reached checkpoints as a linear journey timeline

Previous and next commands select the nearest checkpoint of the requested kind.
Forward navigation only uses snapshots already present after the cursor. Normal
progress after a backward jump truncates that forward tail before recording new
boundaries. Restoring a choice boundary leaves it pending and never chooses an
option.

**Rationale**: A linear cursor prevents jumping into an unvisited continuation or
combining incompatible branch state.
**Impact**: Revisiting a stable boundary replaces its checkpoint at the current
timeline position; cycles and duplicate active IDs produce classified failures
instead of unbounded history.

### Persist navigation state as optional contributor data

WhiteRoom implements `IDialogueSaveContributor` and stores a versioned JSON
payload in `DialogueSaveData.ExtraState`. The payload contains the reached
timeline, cursor, boundary metadata, and snapshots captured with the navigation
contributor excluded.

**Rationale**: `ExtraState` is the existing additive extension seam governed by
ADR-0008.
**Impact**: No core save schema bump is required. Saves without the key load with
an empty timeline and begin recording from the restored current position.
Malformed or future payloads are ignored with a warning rather than blocking
the underlying save.

### Make a jump an exclusive, observable operation

A jump stops Auto and Skip, stops backward skip, closes conflicting overlays,
and blocks dialogue/background input and saves until restore completes. Results
are classified as success, no target, busy, missing target, condition failure,
cycle, invalid snapshot, or restore failure.

**Rationale**: Concurrent input and automation can advance or overwrite state
between target selection and restoration.
**Impact**: Command availability and tooltips reflect the current candidate and
busy state; failures notify the player and leave the current state usable.

## Benefits

- All four commands operate on story-aware, already reached targets.
- Presentation and Backlog stay aligned with the restored dialogue.
- Backward jumps do not reapply events or durable unlocks.
- Existing save slots remain loadable.
- The generic package API is reusable for checkpoint, rewind, and preview tools.

## Trade-offs

- Snapshot history uses more memory and save space than storing row IDs alone.
  → Boundaries are sparse, forward tails are truncated on divergence, and a
  future measured limit can be added without changing boundary identity.
- A linear timeline does not expose every historically visited branch at once.
  → A future flowchart may own a separate graph/history model; navigation stays
  coherent with the active journey.
- Contributor restore order remains registration order.
  → Package tests lock capture/restore ordering and product composition registers
  navigation after presentation contributors.

## Rejected alternatives

| Alternative | Why rejected |
| --- | --- |
| Start the target dialogue row directly | Replays events and loses prior choices, progress, Backlog, and presentation state. |
| Compute targets from command-bar or choice-button indices | UI layout is not dialogue identity and changes independently. |
| Allow forward jumps to any later repository row | It reveals unread content and can bypass required choices and conditions. |
| Store only boundary IDs in save data | Reconstructing past branch and presentation state would require replaying side effects. |
| Put WhiteRoom command policy in Talk System | It violates ADR-0001 and makes a reusable package depend on product behavior. |

## Related ADRs

- [ADR-0001](0001-talk-system-boundary.md) defines the package/product boundary.
- [ADR-0002](0002-runtime-responsibility-split.md) assigns use-case policy to
  services and composition to `NovelGameBootstrap`.
- [ADR-0008](0008-versioned-save-compatibility.md) defines additive save state
  and compatibility behavior.
- [ADR-0009](0009-deterministic-presentation-runtime.md) defines coherent,
  cancellable presentation restoration.

## Development rule integration

- Package tests cover contributor exclusion, ordering, and side-effect-free
  in-memory restore.
- WhiteRoom EditMode tests cover IDs, target selection, tail truncation,
  persistence, compatibility, and every failure class.
- PlayMode tests exercise previous/next scene and choice on a branching route and
  assert dialogue, choices, Backlog, stage, and audio coherence.
- The command bar exposes a reason whenever an operation is unavailable.

## Notes

- This ADR does not define flowchart, scene replay, or favorite-voice behavior.
- Boundary IDs become content compatibility identifiers; changing a dialogue ID
  or relevant chapter key requires migration review.
- Reconsider snapshot storage only after measured save-size or memory evidence,
  or if Talk System gains a side-effect-free deterministic replay engine.
