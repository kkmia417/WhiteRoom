# Talk System Integration

Use this reference when changing WhiteRoom dialogue behavior, scenario data,
runtime bootstrapping, or related UI.

## Source Of Truth

The dialogue system is the local Unity package:

- `Packages/com.kkmia.talksystem/package.json`
- `Packages/com.kkmia.talksystem/README.md`
- `Packages/com.kkmia.talksystem/Documentation~/csv-schema.md`
- `Packages/com.kkmia.talksystem/Documentation~/runtime-api.md`
- `Packages/com.kkmia.talksystem/Documentation~/editor-tools.md`
- `Packages/com.kkmia.talksystem/Documentation~/troubleshooting.md`

Project-side code should integrate with this package instead of duplicating it.

## Project Map

- Runtime bootstrap: `Assets/Scripts/NovelGameBootstrap.cs`
- Scenario data: `Assets/Resources/Dialogue/*.csv`
- Current scenario resource path: `Dialogue/r00_escape_talksystem`
- Current default start trigger: `R00EscapeStart`
- Main scenes: `Assets/Scenes/Title.unity`, `Assets/Scenes/Main.unity`

`NovelGameBootstrap` currently creates or finds a `DialogueManager`, creates a
runtime `DialogueView` if none exists, loads the scenario CSV from Resources,
sets variable and condition resolvers, and routes `EventKey` values such as
scene transitions.

## Talk System Concepts To Prefer

- Start dialogue through `DialogueManager.StartDialogue(id)` or
  `DialogueManager.StartDialogueForState(triggerKey)`.
- Load CSV through `TextAssetDialogueRepositoryLoader` or another
  `IDialogueRepositoryLoader`.
- Resolve `{variables}` with `IDialogueVariableResolver`.
- Gate rows and choices with `IDialogueConditionEvaluator`.
- React to `EventKey` with `IDialogueEventDispatcher`.
- Use `DialogueManager.CaptureState()` and `RestoreState(...)` for integration
  with a game-owned save system.
- Use `DialogueSaveSystem` when package-managed multi-slot saves are wanted.
- Use progress marker columns `ChapterKey`, `RouteKey`, and `EndingKey` for
  route, chapter, ending, and gallery-facing identifiers.
- Use `DialogueUnlockRegistry` and related storage types for global unlock
  flags such as gallery or replay availability.

## CSV Rules

Talk System matches columns by header name. WhiteRoom scenario CSV files should
stay compatible with the package schema.

Required columns:

```csv
Id,Speaker,Text,NextId,EmotionKey,TriggerKey,ConditionKey
```

Common optional columns used by WhiteRoom:

```csv
EventKey,Choices,AutoNextSeconds,ChapterKey,RouteKey,EndingKey,Background,Bgm,Se,Voice,Characters
```

Choice syntax is `Label->NextId` with entries separated by `|`. Conditional
choices append `?conditionKey`.

When editing CSV:

- Keep `Id` values stable unless intentionally migrating references.
- Check every `NextId` and choice target exists, except intentional `-1` ends.
- Keep `TriggerKey` values unique enough for `StartDialogueForState`.
- Add conditions through `ConditionKey` or conditional choices, then implement
  the condition in project-side `IDialogueConditionEvaluator`.
- Add game reactions through `EventKey`, then route them in project-side
  `IDialogueEventDispatcher`.
- Preserve quoted CSV escaping for commas, quotes, and multiline text.

## Runtime Integration Rules

- Prefer package prefabs, public fields, or public APIs for `DialogueView`
  setup. Reflection against private fields is acceptable only as a temporary
  compatibility bridge and should not spread.
- Keep `NovelGameBootstrap` focused on composition: locating UI, loading data,
  setting resolvers, and dispatching game-specific events.
- Put story-specific condition and event names in WhiteRoom code or data, not in
  package code.
- Do not edit `Packages/com.kkmia.talksystem` for game-specific behavior unless
  the user explicitly wants to change the package itself.
- If package behavior looks wrong, first check package docs and tests, then
  propose a package fix separately from the WhiteRoom integration change.

## Validation Checklist

Use the strongest available checks for the touched surface:

- For C# changes, run available compile or Unity test checks if the environment
  supports them.
- For CSV changes, use `Tools/kkmia/Dialogue Validator` in Unity.
- For authoring checks, use `Tools/kkmia/Dialogue Preview` or
  `Tools/kkmia/Dialogue Graph Editor`.
- For branch work, manually trace `NextId`, `Choices`, `TriggerKey`,
  `ConditionKey`, and `EventKey` paths in the changed rows.
- For save/progress work, verify `ChapterKey`, `RouteKey`, `EndingKey`, and
  unlock IDs remain stable across saves.

## Anti-Patterns

- Adding a second CSV parser in `Assets/Scripts`.
- Reimplementing choice filtering outside `IDialogueConditionEvaluator`.
- Encoding route state only in scene objects when Talk System progress markers
  can preserve the dialogue-side identifier.
- Hard-coding package internals that have public extension points.
- Changing scenario IDs broadly to make a narrow feature easier.
- Treating package sample code as project behavior without checking the current
  package docs.

