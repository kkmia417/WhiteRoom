# ADR-0009: Drive presentation through a deterministic, cancellable cue runtime

Status: Accepted<br>
Date: 2026-07-18<br>
Related: [Issue #26](https://github.com/kkmia417/WhiteRoom/issues/26) / [Issue #8](https://github.com/kkmia417/WhiteRoom/issues/8) / [Issue #11](https://github.com/kkmia417/WhiteRoom/issues/11) / [Issue #15](https://github.com/kkmia417/WhiteRoom/issues/15)<br>
Japanese counterpart: [日本語版](0009-deterministic-presentation-runtime.ja.md)

## Context and problem statement

A premium visual novel combines dialogue, choices, backgrounds, layered characters
or models, facial animation, cameras, transitions, BGM, SE, voice, subtitles, movies,
and authored set pieces. Players can advance, skip, auto-play, open backlog, save,
load, change locale, suspend the application, or leave the scene while those effects
are running.

Talk System already emits cue keys, provides stage/audio binders and save contributors,
does not replay one-shot SE on restore, and reports presentation issues. WhiteRoom
needs deterministic orchestration above these primitives. Fire-and-forget coroutines
or a Timeline for every line would race under skip/load and make save restoration
depend on animation timing.

## Decision drivers

- Preserve narrative correctness under advance, skip, auto, load, and scene exit.
- Reach high presentation quality without embedding engine objects in scenario data.
- Preload large assets and voice while keeping memory bounded.
- Restore the exact durable visual/audio state without replaying transient effects.
- Support authored cinematics without replacing Talk System as narrative authority.
- Produce actionable missing-cue and timing telemetry.

## Decision outcome

Talk System remains authoritative for narrative progression. WhiteRoom translates each
row's semantic cue keys into a typed presentation plan executed by one deterministic,
cancellable presentation state machine.

### Resolve semantic cue keys through typed catalogs

Background, character, expression, stage slot, camera, transition, BGM, SE, voice,
movie, and set-piece keys resolve through versioned catalogs to content IDs and
presentation parameters.

**Rationale**: Scenario authors need stable intent, not prefab paths or component
instructions.
**Impact**: CSV never stores scene paths, Addressable addresses, Animator state hashes,
or vendor component references. Catalog validation rejects unresolved keys and invalid
combinations before build.

### Execute one line as a phased transaction

Presentation moves through `Resolve`, `Preload`, `ApplyPersistentState`,
`PlayTransientCues`, and `Ready` phases under a line/session cancellation scope.

**Rationale**: Explicit phases define when input, saving, and the next line are safe.
**Impact**: Starting another line, loading, returning to title, or destroying the
scene cancels and joins in-flight work. No detached coroutine or unobserved async task
may mutate presentation after cancellation.

### Distinguish durable state from transient effects

Background, visible cast, poses, camera state, environment, current BGM, and resumable
voice are durable checkpoint state. Transitions, shakes, particles, and one-shot SE
are transient.

**Rationale**: Save restoration must reconstruct what persists without replaying
effects that would duplicate or reveal timing.
**Impact**: Durable presenters implement save contributors or product checkpoint
contracts. Restore applies zero-duration state first, then resumes only explicitly
resumable media. Transient completion never changes narrative truth.

### Use Timeline or specialized animation only behind set-piece cues

Complex cinematics may use Timeline, Animator, shader sequences, lip-sync, or model
systems through a `SetPiece` adapter with a declared completion and skip contract.

**Rationale**: Specialized tools are valuable for authored shots but unsuitable as
the branching narrative database.
**Impact**: A set piece cannot advance routes directly; it reports completion or a
typed event to the narrative application. Every set piece defines fast-forward,
skip-to-end, cancellation, restore, and missing-content behavior.

### Preload from narrative look-ahead and enforce lifetime budgets

The runtime preloads the current plan and a bounded look-ahead window, then releases
content handles when the narrative/session ownership ends.

**Rationale**: Voice, video, high-resolution CG, and models cannot be synchronously
loaded at the display deadline.
**Impact**: Look-ahead is derived from reachable next rows without speculatively
executing conditions or events. Memory pressure can reduce the window. Loading
latency, cache hit rate, cancellations, and missing content are measurable.

## Benefits

- Skip, load, and scene changes cannot leave stale effects mutating the next state.
- Writers use stable semantic cues while presentation teams retain high-end tools.
- Saves restore a coherent stage and audio state.
- Async content loading has explicit ownership and cancellation.
- Missing content and slow cues are diagnosable by content/build version.

## Trade-offs

- A state machine and typed catalogs add authoring infrastructure.
  → Generate catalogs and preview plans in editor tooling.
- Deterministic skip contracts constrain bespoke effects.
  → Require set pieces to provide skip-to-end rather than banning bespoke work.
- Look-ahead can load assets that a later condition does not use.
  → Bound speculation and tune from memory/load telemetry.

## Rejected alternatives

| Alternative | Why rejected |
| --- | --- |
| Run cue coroutines directly from each dialogue row | Cancellation, ordering, ownership, and save safety become implicit and race-prone. |
| Use Timeline as the narrative engine | Branching, localization, progress, and save authority would be duplicated outside Talk System. |
| Put Unity object paths and animation details in CSV | It couples narrative content to scene and implementation structure. |
| Make presentation completion authoritative for route state | Visual failures or skips could corrupt narrative progression. |

## Related ADRs

- [ADR-0001](0001-talk-system-boundary.md) makes Talk System narrative authority.
- [ADR-0006](0006-addressable-content-delivery.md) owns content handles and preloading.
- [ADR-0007](0007-localization-source-contract.md) coordinates subtitle and voice locale.
- [ADR-0008](0008-versioned-save-compatibility.md) owns checkpoint persistence.

## Development rule integration

- Test every presenter with complete, skip, cancel, load, scene-exit, and missing-asset
  paths using deterministic clocks.
- Validate every production cue key and set-piece contract in the build gate.
- Record line ID, cue ID, content version, phase, duration, and classified failure in
  development diagnostics without recording dialogue text.
- Run representative PlayMode visual/audio restore and memory-lifetime tests.

## Notes

- This ADR does not mandate Timeline, Cinemachine, Live2D, Spine, or a lip-sync vendor.
- Talk System's existing stage/audio binders and presentation issue sources are the
  first adapters; they are not replaced by a parallel dialogue presenter.
- Reconsider the transaction phases only with measured evidence from a production
  set piece and equivalent cancellation/save guarantees.
