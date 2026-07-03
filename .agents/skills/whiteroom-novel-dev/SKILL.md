---
name: whiteroom-novel-dev
description: Implement, review, and evolve WhiteRoom Unity visual-novel and escape-game features using the repository's Talk System package. Use when asked to change dialogue CSV files, NovelGameBootstrap, scene dialogue wiring, branching choices, conditions, events, save/load, backlog, presentation cues, or Talk System integration behavior.
---

# WhiteRoom Novel Dev

Use this skill for WhiteRoom-specific Unity novel-game work. Treat
`Packages/com.kkmia.talksystem` as the dialogue runtime source of truth.

## Core Rule

Do not create a parallel dialogue manager, CSV parser, typewriter system,
choice router, save format, backlog/history model, or dialogue validation path
when Talk System already provides the needed concept. Prefer Talk System public
APIs, prefabs, documentation, editor tools, and extension points. If a package
API is missing, explain the gap before adding project-side code.

## Workflow

1. Classify the request:
   - dialogue content or CSV schema
   - runtime bootstrapping or scene wiring
   - choices, conditions, variables, events, or progress markers
   - save/load, unlocks, backlog, auto/skip, or presentation
   - docs, validation, or authoring workflow
2. Inspect the relevant source before editing:
   - `Assets/Scripts/NovelGameBootstrap.cs`
   - `Assets/Resources/Dialogue/*.csv`
   - `Assets/Scenes/Title.unity` and `Assets/Scenes/Main.unity` when scene behavior can change
   - `Packages/com.kkmia.talksystem/Documentation~/csv-schema.md`
   - `Packages/com.kkmia.talksystem/Documentation~/runtime-api.md`
   - `Packages/com.kkmia.talksystem/Documentation~/editor-tools.md`
3. Read `references/talksystem-integration.md` before changing dialogue runtime behavior, CSV structure, or save/progress features.
4. Keep project code thin:
   - WhiteRoom owns game-specific state, scene transitions, flags, assets, and UI composition.
   - Talk System owns dialogue progression, CSV decoding, validation, choices, events, saves, and runtime extension contracts.
5. Preserve existing content and user edits. Do not rewrite scenario text, IDs, or branch structure unless the task asks for it.
6. Validate changed surfaces:
   - run available C# or Unity checks when practical
   - run Talk System editor validation when dialogue CSV or validation profiles changed
   - inspect branch links, `TriggerKey`, `ConditionKey`, `EventKey`, and `Choices` manually if Unity cannot be run

## Output

When finishing, report:

- Files changed.
- Which Talk System APIs or docs guided the change.
- Validation commands or manual checks run.
- Any Unity Editor checks still needed.

