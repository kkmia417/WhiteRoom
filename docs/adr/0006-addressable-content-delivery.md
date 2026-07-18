# ADR-0006: Deliver production content through Addressables and immutable content identities

Status: Accepted<br>
Date: 2026-07-18<br>
Related: [Issue #26](https://github.com/kkmia417/WhiteRoom/issues/26) / [Issue #8](https://github.com/kkmia417/WhiteRoom/issues/8)<br>
Japanese counterpart: [日本語版](0006-addressable-content-delivery.ja.md)

## Context and problem statement

The prototype loads one dialogue CSV and a fallback font from `Resources`. An AAA
visual novel can contain thousands of images, character variants, voice files, music
tracks, videos, localized assets, and chapter databases. Direct references and a
single `Resources` archive force eager inclusion, obscure ownership, and make a
small correction require a full player release.

Talk System already accepts `IDialogueRepositoryLoader`, composite repositories, and
project-owned asynchronous loading. Unity Addressables provides asynchronous local or
remote asset resolution and content-update builds. WhiteRoom needs a product contract
above Addressables so narrative code does not depend on addresses, bundle topology, or
a particular CDN.

## Decision drivers

- Scale content volume without loading or shipping everything at once.
- Let art, audio, narrative, and localization teams publish independent content units.
- Keep stable semantic references across file moves and bundle repacking.
- Support local-only platforms and optional remote content from the same code path.
- Make content-only updates safe for installed player code and existing saves.
- Diagnose missing, incompatible, or corrupt content before it reaches players.

## Decision outcome

All production narrative and presentation assets outside the minimal boot/recovery
shell are loaded through a WhiteRoom content service backed by Addressables. Stable
product IDs and a versioned content manifest are the public contract; Addressable
addresses and bundle layout are build details.

### Use immutable semantic IDs instead of paths

Scenario units, backgrounds, character expressions, CGs, voice, BGM, SE, videos,
fonts, and localized variants receive namespaced IDs such as
`scenario:r01:chapter03` and `voice:ja:r01:004210`.

**Rationale**: Asset paths and bundle addresses change during production, while
dialogue rows, saves, telemetry, and localization must remain stable.
**Impact**: IDs are never recycled after publication. Renames use an alias/migration
map. Validation rejects duplicates, missing references, case collisions, and
cross-type ID reuse.

### Put Addressables behind an asynchronous content port

Product code requests typed handles from `IContentService`; the Addressables adapter
owns initialization, dependency download, loading, reference counts, cancellation,
and release.

**Rationale**: Narrative and UI behavior should not know catalog or bundle APIs.
**Impact**: No feature code calls Addressables or `Resources.Load` directly.
Talk System receives loaded `TextAsset` objects through
`IDialogueRepositoryLoader`. All load operations expose progress, cancellation,
timeouts where meaningful, and classified failures.

### Partition content by change cadence and runtime locality

Bundles and catalogs are grouped by platform, chapter/route unit, locale, content
type, and update cadence rather than by source folder alone.

**Rationale**: A one-line script or voice correction must not invalidate a
multi-gigabyte unrelated bundle.
**Impact**: Shared dependencies are measured and deduplicated deliberately.
Every release archives its Addressables content state, catalog, manifest, build
profile, and dependency report. Bundle-size and duplication budgets are build gates.

### Separate player-code releases from content-only releases

A content-only update may change data and compatible assets but may not require new
managed code, new serialization types, or an unsupported schema.

**Rationale**: Installed code must be able to interpret every catalog it is allowed
to load.
**Impact**: The manifest declares minimum/maximum compatible player build, content
version, product channel, and required packs. Publication uses immutable versioned
paths and promotion between development, QA, and production; rollback selects a
previous validated manifest instead of overwriting it.

### Keep a minimal local boot and recovery set

Startup UI, legal/privacy notices required before download, a readable fallback font,
content repair UI, and fatal-error presentation ship in the player.

**Rationale**: A remote outage or damaged cache must not strand the player on a blank
screen.
**Impact**: Remote delivery is optional per platform and channel. The boot shell can
verify space, connectivity, catalog compatibility, and required packs before entering
the title or story.

## Benefits

- Content volume and team throughput can grow without a monolithic player build.
- Stable IDs protect scenarios, saves, telemetry, and localization from file moves.
- Local, DLC, and remote packs share one product-facing loading contract.
- Content updates are versioned, testable, promotable, and reversible.
- Talk System remains independent of Addressables.

## Trade-offs

- Addressables adds catalog, lifetime, cache, and build-state complexity.
  → Centralize it in one adapter and archive release artifacts.
- Fine-grained bundles can increase requests and catalog overhead.
  → Tune grouping from measured load traces and explicit budgets.
- Immutable IDs require governance.
  → Generate catalogs and fail CI on duplicate, missing, or recycled IDs.

## Rejected alternatives

| Alternative | Why rejected |
| --- | --- |
| Keep production content in `Resources` | It provides no explicit lifetime, scalable partitioning, or content-update workflow. |
| Reference assets directly from every prefab and scene | It couples content lifetime and packaging to scene serialization and makes dependencies opaque. |
| Build a custom AssetBundle and patch system | It duplicates Addressables catalog, dependency, cache, and update behavior with higher operational risk. |
| Require all content to be remote | Some stores, consoles, offline modes, and recovery paths require local content. |

## Related ADRs

- [ADR-0001](0001-talk-system-boundary.md) keeps Addressables integration outside
  Talk System.
- [ADR-0004](0004-modular-monolith-boundaries.md) defines the content port boundary.
- [ADR-0007](0007-localization-source-contract.md) defines localized content authority.
- [ADR-0008](0008-versioned-save-compatibility.md) binds saves to content versions.

## Development rule integration

- Ban new production `Resources.Load` calls outside the boot/recovery adapter.
- Run dialogue, content-ID, Addressables Analyze, duplicate dependency, and missing
  reference checks in CI.
- Test cache-empty, cache-warm, offline, cancellation, insufficient-space, corrupt
  download, incompatible-catalog, and rollback paths.
- Archive content-state and manifest artifacts for every released player build.

## Notes

- Addressables is not currently installed; adoption is separate implementation work.
- Unity documents that content-update builds require preserving the prior
  `addressables_content_state.bin` and cannot carry code changes:
  [Addressables 2.9 content update overview](https://docs.unity3d.com/Packages/com.unity.addressables@2.9/manual/content-update-builds-overview.html).
- A CDN or hosting vendor is deliberately not selected by this ADR.
