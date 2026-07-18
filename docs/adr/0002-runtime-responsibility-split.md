# ADR-0002: Use an explicit composition root and split runtime responsibilities

Status: Accepted<br>
Date: 2026-07-18 (2026-07-18 extracted from ADR-0001 responsibility review)<br>
Related: [Issue #8](https://github.com/kkmia417/WhiteRoom/issues/8) / [PR #25](https://github.com/kkmia417/WhiteRoom/pull/25)<br>
Japanese: [日本語版](0002-runtime-responsibility-split.ja.md)

## Context and problem statement

WhiteRoom must connect Unity scene lifecycle, Talk System runtime objects,
dialogue resources, title and save/load UI, input routing, presentation,
progress unlocks, and persistence. The first implementation concentrated these
responsibilities in `NovelGameBootstrap`. The commit history later includes
`Refactor NovelGameBootstrap god class into focused classes`, which introduced
`Setup`, `Services`, and `UI` collaborators.

The split needs a durable rule. Folder names alone do not answer who may create
Unity objects, who owns persistence policy, whether controllers can call Talk
System directly, or where compatibility reflection belongs. Without that rule,
the bootstrap can regain behavior, services can construct widgets, and
factories can acquire game policy.

The application currently compiles in Unity's generated `Assembly-CSharp`
rather than application-specific assembly definitions. Talk System has its own
Runtime and Editor assemblies. The architecture must therefore be explicit and
testable now without pretending that compile-time boundaries already exist.

## Decision drivers

- Unity startup, `DontDestroyOnLoad`, and scene events need one visible owner.
- Save/load, progression, and conditions must be understandable without UI
  construction details.
- UI controllers need application operations without owning file storage.
- Talk System object creation and serialized-field compatibility need isolation.
- The object graph is small enough that a DI framework would add more
  lifecycle and debugging cost than value.
- Boundary rules must reflect the current code and remain enforceable before
  application assembly definitions are introduced.
- Each class should have one primary reason to change and a focused validation
  surface.

## Decision outcome

Use `NovelGameBootstrap` as an explicit composition root and Unity lifecycle
adapter. Split application runtime responsibilities into `Setup`, `Services`,
and `UI`, with one-way dependencies toward Talk System and Unity.

```text
Unity Scenes / RuntimeInitializeOnLoad
                │ lifecycle
                ▼
       NovelGameBootstrap
          ├────► Setup factories / compatibility adapters
          ├────► Services / product use cases
          └────► UI controllers / runtime views
                         │
                         ▼
                 Talk System public API
                         │
                         ▼
                       Unity
```

### Keep lifecycle and object-graph assembly in `NovelGameBootstrap`

**Rationale**: Unity callbacks and scene events are framework entry points, and
the complete runtime graph must be discoverable in one place.
**Impact**: The bootstrap creates collaborators, connects events, loads the
dialogue resource, translates scene lifecycle, and delegates operations. It
does not implement save algorithms, unlock rules, widget layout, or reusable
dialogue behavior. New private methods are acceptable only when they translate
lifecycle or make composition readable.

### Put Unity object creation and Talk System wiring in `Setup`

**Rationale**: Creating `GameObject` and `MonoBehaviour` instances, assigning
components, building fallback views, and connecting package adapters are
construction concerns with Unity-specific failure modes.
**Impact**: `DialogueRuntimeFactory`, `DialoguePresentationFactory`,
`DialogueViewFactory`, `NovelUiFactory`, and narrow binders construct or locate
objects. Setup code returns configured collaborators; it does not decide
progression, save eligibility, scene outcomes, or product use cases.

### Put WhiteRoom use cases and durable policy in `Services`

**Rationale**: Save/continue behavior, progress markers, unlock persistence, and
variable resolution describe application behavior independent of concrete
screens.
**Impact**: Services may depend on Talk System public contracts and minimal
Unity facilities required by current storage or logging. They expose
operations/events to controllers, do not create visual objects, and do not
subscribe to scene lifecycle unless explicitly elevated through a later ADR.

### Put presentation coordination in `UI`

**Rationale**: Title, backlog, save/load screens, auto-advance suspension, and
fallback layout change for presentation reasons and should not own storage or
dialogue-engine policy.
**Impact**: UI controllers call services and Talk System presentation
interfaces, render view state, and translate user actions into application
operations. They do not read/write save files or decide unlock semantics.
Code-driven fallback UI remains a presentation implementation, not a domain
model.

### Preserve one-way dependency and prohibit UI-to-Setup coordination

**Rationale**: Allowing every area to construct or reconfigure every other area
creates hidden object graphs and circular ownership.
**Impact**: The bootstrap may depend on all application areas. UI may depend on
services and public Talk System presentation APIs. Services and UI do not call
Setup factories after composition. Setup does not depend on product
controllers or services to make policy decisions.

### Confine reflection to an explicit compatibility seam in `Setup`

**Rationale**: Runtime-created Talk System components currently require access
to serialized private fields and a non-public input handler. Reflection is
fragile but spreading it makes package upgrades impossible to audit.
**Impact**: `RuntimeFieldBinder` and `DialogueRuntimeFactory` are the only
approved reflection area. Missing members log a clear warning. New reflection
requires an Issue, a reason a public package API cannot be used, focused
validation, and a removal condition. Services, UI, and the package consumer
outside `Setup` may not add reflection.

### Use explicit construction instead of a dependency-injection framework

**Rationale**: The current graph is assembled once, is visible in
`BuildRuntime`, and does not need runtime scopes or multiple implementations for
most collaborators.
**Impact**: Constructors and factory methods remain explicit. A DI framework
requires a successor ADR based on measured graph complexity, test friction, or
multiple lifecycle scopes.

### Defer application assembly definitions until the boundary is test-ready

**Rationale**: An asmdef migration changes Unity compilation and reference
behavior. The application currently has no dedicated test assembly, while
directory and namespace checks already provide a smaller first guard.
**Impact**: `Assets/Scripts` remains in `Assembly-CSharp` for now. An Issue for
application asmdefs includes EditMode tests, explicit references, Unity compile
validation, and migration of affected editor/runtime code.

## Benefits

- Unity lifecycle and the full object graph remain discoverable.
- Save, progress, and UI behavior have narrower reasons to change.
- Product services can gain focused tests without constructing every screen.
- Unity construction failures are isolated from application policy.
- Reflection and package-private coupling have one auditable location.
- A future asmdef migration has named logical boundaries to encode.

## Trade-offs

- The composition root still knows every concrete collaborator.
  → Keep it declarative and move behavior, not construction visibility, out.
- Static factories are harder to replace in isolated tests.
  → Test services/controllers through constructor inputs; introduce factory
  interfaces only when a test or multiple implementation requires them.
- Directory boundaries lack compile-time enforcement.
  → Maintain namespace/reflection scans and add asmdefs with tests through a
  dedicated Issue.
- Code-driven fallback UI can become large and visually fragile.
  → Prefer authored prefabs for production surfaces and retain factories as
  explicit fallback/setup mechanisms.
- Reflection can break when Talk System private members change.
  → Keep the seam small, log missing members, validate integration, and replace
  it with public configuration APIs when their contract is known.

## Rejected alternatives

| Alternative | Why rejected |
| --- | --- |
| Keep all runtime behavior in one MonoBehaviour | It couples lifecycle, storage, progression, presentation, and construction, recreating the god-class problem already observed in history. |
| Let each controller locate and construct its own dependencies | It hides the object graph, duplicates Unity lookup, and creates ambiguous ownership and cleanup. |
| Introduce a DI container now | It adds package, lifecycle, registration, and debugging cost without multiple scopes or a graph large enough to justify it. |
| Make every collaborator a MonoBehaviour | It ties product logic to Unity lifecycle and makes focused tests require scenes or GameObjects. |
| Put all runtime setup in prefabs only | Prefabs are useful production composition, but current fallback construction and optional components still need explicit, testable setup code. |
| Add asmdefs without application tests | It creates a compile-boundary migration without a regression harness and can make iteration slower without proving the intended dependencies. |

## Related ADRs

- [ADR-0001](0001-talk-system-boundary.md) defines the package/application
  ownership boundary that all application areas consume.
- [ADR-0003](0003-issue-driven-bilingual-adrs.md) defines the Issue and review
  process required to change these responsibilities.

## Development rule integration

- `Assets/Scripts` C# uses the `WhiteRoom.Novel` namespace.
- Reflection imports are rejected outside `Assets/Scripts/Setup`.
- Review checks that bootstrap changes are lifecycle/composition, Setup changes
  are construction, Services changes are use cases/policy, and UI changes are
  presentation.
- Behavior changes add focused tests at the narrowest boundary available and
  run Unity batch-mode compilation for Unity-facing changes.
- A PR that changes dependency direction updates this ADR pair or adds a
  successor before implementation relies on the new direction.

## Notes

- This ADR does not select scene content, visual design, save-data format,
  dialogue schema, or the future DI/asmdef implementation.
- Current package-private reflection is accepted as constrained debt, not a
  preferred integration technique.
- Reconsider explicit construction when measured graph complexity, multiple
  runtime scopes, or test substitution cost outweighs container overhead.
