# Migration Guide

## From Copied Assets to UPM

1. Back up your project.
2. Remove old copied Talk System scripts from `Assets/kkmia/TalkSystem`.
3. Install the package from the Git URL.
4. Import samples only if you need them.
5. Reassign scene references if Unity reports missing scripts.

Keep your dialogue CSV and project-specific prefabs outside the package folder.

## DialoguePresenter.Session

`DialoguePresenter.Session` now returns `IReadOnlyDialogueSession`. Code that only reads state, current line, choices, history, progress, or trigger key can keep using the property. Code that called `Advance`, `SelectChoice`, or `End` on the session should route those operations through `DialoguePresenter`, `DialogueManager`, or the bound view/input APIs so rendering and events stay consistent.

## Save Schema Changes

New saves include `DialogueSaveSchema.CurrentVersion`, `ContentVersion`, and `ProductChannel` metadata. Talk System owns the schema version. Your game owns content and channel values, and should register `IDialogueSaveDataMigration` / `IDialogueSaveSlotMigration` steps when old saves need project-specific conversion.

If removed dialogue rows can exist in player saves, configure `DialogueSaveSystem.SetMissingDialoguePolicy(...)` before loading. The default is fail-fast so unsupported saves do not restore into the wrong scene state silently.
