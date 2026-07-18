# ADR-0008: Preserve player progress with a versioned save envelope and explicit migrations

Status: Accepted<br>
Date: 2026-07-18<br>
Related: [Issue #26](https://github.com/kkmia417/WhiteRoom/issues/26) / [Issue #8](https://github.com/kkmia417/WhiteRoom/issues/8) / [Issue #19](https://github.com/kkmia417/WhiteRoom/issues/19) / [Issue #20](https://github.com/kkmia417/WhiteRoom/issues/20)<br>
Japanese counterpart: [日本語版](0008-versioned-save-compatibility.ja.md)

## Context and problem statement

Commercial players expect manual saves, autosaves, settings, unlocks, demo-to-full
progression, DLC compatibility, and cloud synchronization to survive updates. Talk
System already captures dialogue state, presentation contributors, progress markers,
schema metadata, content version, product channel, slot JSON, thumbnails, atomic
replacement, failure results, and migration extension points.

WhiteRoom still owns the meaning of product channels, content versions, game flags,
settings, platform users, and conflict policy. Serializing scene objects or treating
the current JSON shape as permanent would make content corrections and post-launch
updates unsafe.

## Decision drivers

- Never silently lose paid-product progress.
- Restore narrative and presentation to a coherent checkpoint.
- Support sequential migration across player, Talk System, and content versions.
- Keep local, cloud, demo, full, DLC, and store channels explicit.
- Recover safely from interrupted writes, corruption, and unknown future saves.
- Avoid coupling game policy to a cloud or platform SDK.

## Decision outcome

WhiteRoom owns a versioned save envelope containing Talk System state and
product-owned sections. Saves are written transactionally, validated before use, and
migrated through an explicit registry. Cloud synchronization is a replaceable storage
adapter with product-owned conflict rules.

### Define a product-owned save envelope

Each slot records product save version, Talk System schema version, content version,
product channel, build ID, slot identity, save kind, timestamp, play time, locale, and
typed sections for dialogue, WhiteRoom state, presentation checkpoint, and optional
extension data. Thumbnail data remains a sidecar.

**Rationale**: One version number cannot describe independent product, package, and
content compatibility.
**Impact**: Runtime models are converted to dedicated save DTOs; Unity object
references, scene instance IDs, and vendor objects are never serialized. Unknown
optional fields are preserved where the format permits.

### Save only at coherent checkpoints

Manual and automatic saves capture dialogue state and all registered presentation
contributors within one paused save transaction.

**Rationale**: Capturing between a line transition and an asynchronous visual update
can restore a state the player never saw.
**Impact**: Save requests coordinate with the narrative/presentation state machine,
suspend auto/skip, and either commit a complete snapshot or report failure. Autosave
points are explicit story/product events, not arbitrary frame timers.

### Migrate deterministically and never overwrite the only recoverable copy

Migrations are ordered, one-way transforms from a known version/channel/content range
to the current format. The original or previous valid generation is retained until
the migrated save has passed validation and been committed.

**Rationale**: A failed migration must be diagnosable and recoverable.
**Impact**: Missing migration paths, future schema versions, removed dialogue IDs, and
incompatible channels produce typed load outcomes. They never trigger a silent reset.
Migration tests use frozen fixtures from every shipped save version.

### Use generation-based transactional storage

Local storage writes a temporary generation, flushes it, validates it, and atomically
promotes it while retaining at least one previous valid generation. An integrity
digest detects corruption but is not treated as anti-tamper security.

**Rationale**: Process termination, disk exhaustion, and partial synchronization are
normal failure modes.
**Impact**: The UI exposes retry, previous-generation recovery, and actionable failure
messages. Secrets are never stored in the save. Encryption or signing requires a
separate threat-model decision.

### Keep cloud and platform identity behind a storage port

The persistence service uses product-owned local and cloud storage interfaces.
Conflict resolution compares platform user, slot lineage/generation, timestamp, play
time, content compatibility, and explicit player choice when neither side dominates.

**Rationale**: "Newest timestamp wins" can discard offline progress and platform SDK
types should not enter the save model.
**Impact**: Cloud is optional by capability and channel. Upload/download is
idempotent, observable, cancellable, and never blocks access to a valid local save.

## Benefits

- Released saves remain testable and migratable across updates.
- Interrupted writes and failed migrations preserve a recovery path.
- Talk System capabilities are reused without giving it product policy.
- Cloud vendors and platform SDKs remain replaceable.
- Save/load failures become explicit user and telemetry outcomes.

## Trade-offs

- Frozen fixtures and migrations create permanent maintenance work.
  → Treat compatibility as a commercial contract and retire paths only by policy.
- Checkpoint coordination adds latency to save operations.
  → Capture compact state on the main thread, then perform storage work asynchronously.
- Previous generations consume storage.
  → Bound retained generations and thumbnails per platform budget.

## Rejected alternatives

| Alternative | Why rejected |
| --- | --- |
| Serialize active Unity scenes or objects | Scene identity and object graphs are unstable across content and engine changes. |
| Replace old saves when the schema changes | It violates the player-progress contract and makes post-launch updates unsafe. |
| Use only Talk System slot JSON as the product format | It cannot own WhiteRoom channels, settings, platform identity, conflict policy, or future product sections. |
| Integrate one cloud SDK directly into save services | It leaks vendor lifecycles and error types into product policy and tests. |

## Related ADRs

- [ADR-0001](0001-talk-system-boundary.md) separates package state from product policy.
- [ADR-0004](0004-modular-monolith-boundaries.md) defines persistence ports.
- [ADR-0006](0006-addressable-content-delivery.md) defines content versions.
- [ADR-0009](0009-deterministic-presentation-runtime.md) defines coherent checkpoints.

## Development rule integration

- Keep immutable fixture files for every shipped save format and channel.
- Test round-trip, sequential migration, interrupted write, corrupt current
  generation, missing content, future schema, and cloud conflict cases.
- Require an explicit compatibility decision for removed dialogue/content IDs.
- Redact save contents and player identifiers from logs and telemetry.

## Notes

- Talk System's `DialogueSaveSystem`, contributors, failure results, and migration
  interfaces remain the implementation foundation.
- Exact cloud providers, encryption, and cross-store synchronization are outside this
  decision.
- Reconsider the envelope only if a platform-mandated format can preserve the same
  compatibility, recovery, and test-fixture guarantees.
