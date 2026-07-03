# Feature Tour Sample

This sample demonstrates the intended production workflow:

- Branching choices
- Conditions
- Variables
- Event keys
- Save data capture/restore
- Rollback (step back to a previous line)
- Inline text tags: ruby (furigana), color, and inline wait
- Localization with runtime language switching
- Character expression metadata
- CSV validation
- Dialogue history/backlog display
- Event-driven audio, quest flag, and timeline-like hooks
- Reusable UI prefab starting points

## Sample controls (keyboard)

The controller wires these for the demo (assign them to `DialogueInputRouter` or
UI buttons in a real project):

| Key | Action |
| --- | --- |
| `PageUp` | Rollback to the previous line |
| `L` | Toggle language (CSV/English ⇄ Japanese) |
| `F5` / `F9` | Save / Restore dialogue state |

Line advance and choices use the dialogue UI's own buttons/input.

## Files

- `dialogue_feature_tour.csv`: compact data set covering linear flow, branching, conditions, variables, events, and auto/manual advance.
- `FeatureTour.unity`: ready-to-run sample scene using the packaged manager, UI, CSV, and controller.
- `FeatureTourSampleController.cs`: copy-friendly runtime integration for variables, conditions, save/restore, rollback, localization, backlog, and sample gameplay state.
- `FeatureTourDialogueEventRouter.cs`: data-driven `EventKey` to `UnityEvent` bridge for teams that prefer Inspector wiring.
- `Editor/FeatureTourSceneBuilder.cs`: recreates a wired sample scene from the imported assets.
- `Prefabs/VisualNovelDialogueUI.prefab`: sample copy of the default dialogue UI for visual-novel-style layouts.
- `Prefabs/CompactRpgDialogueUI.prefab`: sample copy of the default dialogue UI for compact RPG-style layouts.

## Quick Setup

1. Import the sample from Unity Package Manager.
2. Open `FeatureTour.unity`.
3. Press Play. The controller starts `SampleStart`, resolves `{playerName}`, evaluates `can_take_left`, records history, and reacts to `EventKey` values.

Optional fields such as audio, animator, log text, and backlog text can be left empty. The sample degrades gracefully and still runs the dialogue flow. If you want to regenerate the scene in a project folder, run `Tools/kkmia/Samples/Create Feature Tour Scene`.
