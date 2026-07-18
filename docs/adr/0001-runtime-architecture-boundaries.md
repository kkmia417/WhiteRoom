# ADR 0001: Define runtime architecture boundaries

- Status: Accepted
- Date: 2026-07-18
- Owners: WhiteRoom maintainers
- Related issues: #8

## Context

Issue #8 grows WhiteRoom from a minimal Unity project into a complete
title-to-ending loop. The application now coordinates scene lifecycle, dialogue
runtime setup, save/load, progress tracking, and runtime-generated UI while also
embedding the reusable `com.kkmia.talksystem` package.

Without explicit boundaries, application policy can leak into the package,
`NovelGameBootstrap` can become a god object again, and Unity-specific
construction details can spread across services and controllers.

The current implementation already exposes useful seams:

- `NovelGameBootstrap` owns startup and scene events;
- `Assets/Scripts/Setup` creates and wires runtime objects;
- `Assets/Scripts/Services` coordinates save and progress behavior;
- `Assets/Scripts/UI` controls title, backlog, and save/load presentation;
- Talk System owns reusable dialogue runtime and presentation primitives.

## Decision

Use a composition-root architecture with one-way dependencies:

1. `NovelGameBootstrap` is the composition root and Unity scene-lifecycle
   adapter. It delegates behavior after constructing collaborators.
2. `Setup` owns Unity object creation, runtime fallback views, and adapters that
   bind Talk System components.
3. `Services` owns WhiteRoom use cases and persistence policy. Services may
   depend on Talk System abstractions but not on UI controllers.
4. `UI` owns presentation controllers and runtime UI construction. Controllers
   call application services and do not own persistence.
5. `Packages/com.kkmia.talksystem` remains reusable infrastructure. It must not
   reference the `WhiteRoom.Novel` namespace or project scenes.
6. Reflection used to bridge serialized private fields is a compatibility seam
   and stays inside `Assets/Scripts/Setup`.

The intended dependency flow is:

`Scenes -> Bootstrap -> UI / Services / Setup -> Talk System -> Unity`

Dependencies may skip a layer in that direction when the simpler design is
clearer. Reverse dependencies are not allowed.

## Alternatives considered

### Keep all orchestration in one MonoBehaviour

This minimizes the initial file count but couples UI, persistence, scene
lifecycle, and package setup. It makes isolated tests and safe changes harder.

### Add a dependency-injection framework

A container could automate construction, but the current application graph is
small and Unity lifecycle integration would add package and debugging cost.
Explicit construction remains easier to inspect.

### Move WhiteRoom behavior into Talk System

This would make immediate wiring convenient but would couple the reusable
package to one game's scenes, save policy, and UI, preventing independent reuse.

### Introduce application assembly definitions immediately

Assembly definitions could enforce some boundaries at compile time, but the
current application has no dedicated test assembly and still changes rapidly.
The repository will add them through a separate Issue when compile-time
isolation produces more value than migration cost.

## Consequences

Positive consequences:

- ownership is visible from the directory structure;
- the reusable package remains independent;
- pure controllers and services can be tested without adding MonoBehaviours;
- runtime fallback UI remains available without becoming persistence policy.

Negative consequences:

- the composition root still knows the full object graph;
- explicit factories add indirection;
- reflection remains fragile against upstream private-field renames;
- directory boundaries are partly enforced by review and governance checks
  until assembly definitions are introduced.

## Validation

- `scripts/validate_governance.py` rejects Talk System source references to
  `WhiteRoom.Novel`.
- The same check requires application C# files to use the `WhiteRoom.Novel`
  namespace and confines `System.Reflection` imports to `Setup`.
- Unity batch-mode compilation validates the constructed object graph after
  application or package changes.
- Focused tests must cover service and controller behavior added by future
  Issues.

## Follow-up

- Create an Issue for WhiteRoom EditMode tests and application assembly
  definitions when the runtime boundaries stabilize.
- Replace reflection seams with public Talk System configuration APIs when
  their required shape is known.
