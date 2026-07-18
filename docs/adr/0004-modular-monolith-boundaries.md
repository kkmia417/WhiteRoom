# ADR-0004: Structure the game as a modular monolith with enforced assembly boundaries

Status: Accepted<br>
Date: 2026-07-18<br>
Related: [Issue #26](https://github.com/kkmia417/WhiteRoom/issues/26) / [Issue #8](https://github.com/kkmia417/WhiteRoom/issues/8)<br>
Japanese counterpart: [日本語版](0004-modular-monolith-boundaries.ja.md)

## Context and problem statement

WhiteRoom is currently a single Unity application whose game scripts compile into
`Assembly-CSharp`. The source directories and `NovelGameBootstrap` already express
useful ownership boundaries, while Talk System is an independently testable package.
That structure is appropriate for a vertical slice but does not protect a large team
from circular dependencies, accidental Unity coupling, slow compilation, or feature
code reaching directly into platform and storage SDKs.

An AAA visual novel needs many disciplines to work in parallel on narrative,
presentation, content, persistence, UI, platform integration, and release tooling.
The game still ships as one client process, so distributed services or independently
deployed runtime components would add operational cost without isolating useful
failure domains.

## Decision drivers

- Preserve one deployable game while allowing teams to work behind stable contracts.
- Make narrative and product rules testable without loading scenes.
- Keep Unity, Talk System, storage, content, and platform SDK dependencies explicit.
- Prevent cyclic dependencies and service-locator access.
- Support incremental compilation and ownership review at assembly granularity.
- Retain the explicit composition model established by ADR-0002.

## Decision outcome

WhiteRoom will be a modular monolith inside one Unity project. Runtime modules use
assembly definitions and communicate through narrow C# contracts. Unity-facing and
vendor-facing code remains at the edges, and `NovelGameBootstrap` is the composition
root that selects concrete adapters.

### Create product modules with one-way dependencies

The target runtime assemblies are `WhiteRoom.Core`, `WhiteRoom.Narrative`,
`WhiteRoom.Content`, `WhiteRoom.Persistence`, `WhiteRoom.Presentation`,
`WhiteRoom.Platform`, and `WhiteRoom.Bootstrap`.

**Rationale**: These boundaries follow independently changing product
responsibilities rather than scenes or asset folders.
**Impact**: `Core` has no dependency on Unity, Talk System, or vendor SDKs.
`Narrative` may adapt Talk System contracts. Content, persistence, presentation, and
platform modules may depend on core contracts but not on each other's concrete
implementations. `Bootstrap` may depend on every composition-time assembly.

### Keep policy in pure C# and effects behind ports

Progress rules, content identity, save compatibility decisions, release-channel
rules, and use-case orchestration are plain C# wherever Unity objects are not
required. Files, clocks, telemetry, content loading, cloud saves, achievements, and
platform capabilities are accessed through product-owned interfaces.

**Rationale**: Pure policy can be tested quickly and cannot silently acquire scene
or SDK lifetime assumptions.
**Impact**: `MonoBehaviour`, `ScriptableObject`, `UnityEngine.Object`, static SDK
singletons, and direct filesystem calls are prohibited in `WhiteRoom.Core`.
Adapters translate failures into product-owned result types.

### Compose concrete adapters only at the application edge

`NovelGameBootstrap` and focused bootstrap installers construct the object graph.
Runtime code receives required collaborators through constructors or serialized
composition references.

**Rationale**: A visible object graph makes startup order, ownership, disposal, and
test substitution reviewable.
**Impact**: Global service locators, ad-hoc singleton discovery, and a general
dependency-injection container are not application APIs. Unity object lookup is
confined to bootstrap/setup compatibility seams and removed when prefabs provide
explicit references.

### Expose contracts intentionally across assemblies

Each module defaults to internal implementation and publishes only the contracts
needed by consumers. Cross-module events are typed and owned by the module that
defines their meaning.

**Rationale**: A directory convention alone cannot stop an AAA codebase from
becoming a shared mutable object graph.
**Impact**: New assembly references require architecture review. Shared mutable
state, string-based global event buses, and public fields added only to bypass an
assembly boundary are rejected in review.

### Migrate by vertical slice without stopping feature delivery

The first migration establishes `Core`, one use case, its adapters, and tests before
moving the remaining scripts. Existing behavior may remain in `Assembly-CSharp`
temporarily, but no new cross-cutting subsystem is added there.

**Rationale**: A flag-day assembly migration would create large Unity serialization
and merge risk.
**Impact**: This clause supersedes ADR-0002's temporary decision to defer application
assembly definitions. Migration work must preserve `.meta` files, serialized type
identity, and scene references.

## Benefits

- Compile-time dependency enforcement replaces directory-only conventions.
- Most product rules can run in fast EditMode or plain C# tests.
- Platform and vendor changes remain replaceable adapters.
- Teams can own modules without splitting the shipped client into services.
- Bootstrap and lifetime behavior remain explicit.

## Trade-offs

- More assemblies and contracts add design and compilation overhead.
  → Add a boundary only for a responsibility with a distinct reason to change.
- Unity serialization makes type moves risky.
  → Migrate in small slices and verify scene/prefab references after every move.
- Port interfaces can become abstractions without evidence.
  → Introduce a port only at a real Unity, package, storage, content, or vendor edge.

## Rejected alternatives

| Alternative | Why rejected |
| --- | --- |
| Keep all game code in `Assembly-CSharp` | It provides no enforceable dependency or ownership boundary as the team and feature count grow. |
| Split the client into network microservices | A single-player narrative runtime gains deployment and failure complexity without a useful runtime isolation benefit. |
| Put all product code into Talk System | It couples reusable dialogue infrastructure to WhiteRoom routes, assets, platforms, and commercial policy. |
| Adopt a global DI container or service locator | It hides object ownership and makes scene startup and tests dependent on ambient state. |

## Related ADRs

- [ADR-0001](0001-talk-system-boundary.md) defines the package/product boundary.
- [ADR-0002](0002-runtime-responsibility-split.md) defines composition and current
  runtime responsibilities; this ADR supersedes only its asmdef deferral.
- [ADR-0003](0003-issue-driven-bilingual-adrs.md) governs boundary changes.

## Development rule integration

- Add an architecture test that rejects forbidden assembly references and cycles.
- Require a focused test for every new core policy or adapter failure mapping.
- Keep Unity and vendor types out of `WhiteRoom.Core` public contracts.
- Treat a new assembly reference or shared global service as architecture impact.
- Track the migration as Issue-sized vertical slices, not a folder-wide rewrite.

## Notes

- This ADR defines target boundaries, not the entire migration implementation.
- Editor tooling may have separate editor-only assemblies that depend on runtime
  contracts; runtime assemblies never depend on editor assemblies.
- Reconsider the modular monolith only if a separately operated backend becomes a
  product requirement with an independent trust or scaling boundary.
