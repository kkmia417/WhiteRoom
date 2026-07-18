# ADR-0005: Standardize the client on Unity 6.3 LTS, URP, and uGUI

Status: Accepted<br>
Date: 2026-07-18<br>
Related: [Issue #26](https://github.com/kkmia417/WhiteRoom/issues/26) / [Issue #8](https://github.com/kkmia417/WhiteRoom/issues/8) / [Issue #24](https://github.com/kkmia417/WhiteRoom/issues/24)<br>
Japanese counterpart: [日本語版](0005-unity-urp-runtime-baseline.ja.md)

## Context and problem statement

WhiteRoom already uses Unity `6000.3.7f1`, Talk System targets Unity 6000.0, and
rendering work references URP and VRM MToon. A commercial production needs one
supported editor line, one runtime UI technology, and one rendering pipeline.
Allowing Built-in, URP, HDRP, uGUI, and UI Toolkit to compete per feature would
multiply shaders, prefabs, test matrices, and platform defects.

Unity identifies 6.3 as an LTS release supported through December 2027. The version
is suitable for locking production, but patch upgrades still require evidence because
serialized assets, packages, shaders, and platform toolchains can change.

## Decision drivers

- Stable production support and repeatable builds.
- Windows PC quality with a credible path to console and additional desktop targets.
- High-quality 2D/2.5D presentation, VRM characters, post-processing, and video.
- Compatibility with Talk System's existing uGUI runtime.
- One shader and UI test matrix.
- Controlled editor, package, and SDK upgrades.

## Decision outcome

The shipping client uses a pinned Unity 6.3 LTS patch, Universal Render Pipeline
(URP), and uGUI for all player-facing runtime UI. UI Toolkit is reserved for editor
tools unless a later ADR proves a runtime migration.

### Pin the editor and production packages

The exact editor patch is committed in `ProjectSettings/ProjectVersion.txt`; package
versions and platform SDK versions are locked per release branch.

**Rationale**: "Unity 6.3" is not a reproducible toolchain identifier.
**Impact**: Editor or package upgrades require a dedicated Issue, release-note and
known-issue review, clean import, compile, tests, representative content build, save
compatibility check, and platform smoke test. Developer and CI versions must match.

### Use URP as the only shipping render pipeline

All production shaders, materials, post-processing, camera stacks, and quality
profiles target URP. Built-in Render Pipeline and HDRP variants are not maintained.

**Rationale**: URP supplies the cross-platform performance range needed by a
2D/2.5D visual novel without the memory and shader-variant cost of parallel pipelines.
**Impact**: Art ingestion validates URP shader compatibility. Unsupported assets are
converted at import or isolated behind a documented adapter. A pipeline migration
requires a successor ADR and a full visual-regression baseline.

### Keep player-facing runtime UI on uGUI

Dialogue, choices, backlog, save/load, settings, gallery, subtitles, and accessibility
surfaces use prefab-authored uGUI. UI Toolkit remains valid for editor tooling.

**Rationale**: Talk System and current runtime presentation already use uGUI; a
mixed runtime UI stack would duplicate navigation, localization, focus, animation,
and automated test infrastructure.
**Impact**: Runtime screens use shared navigation, typography, safe-area, input, and
accessibility components. Runtime UI Toolkit requires a separately accepted migration
ADR rather than feature-local adoption.

### Express platform variation through quality and capability profiles

Resolution scale, texture tier, shadow/post-processing quality, video format, input
prompts, and optional features are selected from versioned profiles.

**Rationale**: Platform variation is unavoidable, but scattered compile directives
make behavior impossible to reason about.
**Impact**: Each release target defines numeric frame-time, memory, loading, and
download budgets before content lock. Platform-specific code stays in adapters;
content and gameplay query capabilities rather than platform names.

## Benefits

- One supported editor, renderer, and runtime UI path.
- Existing Talk System UI and VRM/URP work remain usable.
- Art, QA, and build teams share the same shader and prefab assumptions.
- Platform tuning does not fork narrative or product logic.

## Trade-offs

- HDRP-only effects and assets require conversion.
  → Approve exceptions only when a URP implementation cannot meet an explicit shot.
- uGUI lacks some newer UI Toolkit workflows.
  → Invest in shared prefabs, navigation, and test utilities rather than two stacks.
- Pinned versions delay new engine features.
  → Schedule evidence-based upgrade windows instead of upgrading during content lock.

## Rejected alternatives

| Alternative | Why rejected |
| --- | --- |
| Maintain URP and HDRP editions | It doubles shader, lighting, asset, performance, and certification work for no current product requirement. |
| Use Built-in Render Pipeline | It does not match the current URP direction or provide the desired long-term rendering baseline. |
| Mix uGUI and UI Toolkit by screen | It duplicates runtime navigation, input, localization, styling, and QA infrastructure. |
| Follow the latest Unity patch automatically | It makes builds non-reproducible and can introduce unreviewed serialization or platform changes. |

## Related ADRs

- [ADR-0002](0002-runtime-responsibility-split.md) owns runtime composition.
- [ADR-0004](0004-modular-monolith-boundaries.md) isolates Unity-facing code.
- [ADR-0006](0006-addressable-content-delivery.md) owns production asset loading.

## Development rule integration

- CI rejects an unexpected `ProjectVersion.txt` or package-lock change.
- Art validation reports Built-in/HDRP shaders and missing URP variants.
- Player-facing UI PRs include keyboard/controller navigation, safe-area,
  localization, and representative-resolution evidence.
- Release profiles contain explicit performance budgets and quality settings.

## Notes

- The repository is not yet fully compliant: URP is an architectural target whose
  package installation and asset migration require separate implementation Issues.
- Unity's current support statement is
  [Unity 6 Releases & Support](https://unity.com/releases/unity-6/support).
- Reconsider URP only if measured target-platform requirements or a product-defining
  rendering feature cannot be met after a documented prototype.
