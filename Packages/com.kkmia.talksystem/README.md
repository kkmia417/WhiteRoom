# Talk System

[![Unity Tests](https://github.com/kkmia417/TalkSystem/actions/workflows/unity-tests.yml/badge.svg)](https://github.com/kkmia417/TalkSystem/actions/workflows/unity-tests.yml)
[![Package Validation](https://github.com/kkmia417/TalkSystem/actions/workflows/package-validation.yml/badge.svg)](https://github.com/kkmia417/TalkSystem/actions/workflows/package-validation.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Unity](https://img.shields.io/badge/Unity-6000.0%2B-black.svg)](https://unity.com/)

CSV-driven dialogue tooling for Unity games. Talk System gives you branching dialogue, validation, preview playback, runtime extension points, and a visual graph editor while keeping dialogue data easy to edit in CSV or external tools.

## Why Use It

- Author dialogue in CSV, Google Sheets exports, JSON, or writer-friendly text imports.
- Validate duplicate IDs, broken links, unreachable rows, cycles, and malformed data before Play Mode.
- Preview and edit dialogue from Unity editor tools.
- Build production flows with choices, conditions, variables, events, save data, backlog/history, and custom views.
- Install through Unity Package Manager and import samples directly into a project.

## Screenshots & Demo

> 📸 Media capture in progress. These assets are recordings of the live Unity Editor and
> the running sample, so they are captured by hand — see
> [Capture Guide](Documentation~/images/CAPTURE_GUIDE.md) for the shot list and steps.
> When an asset is added, uncomment its block below and flip its status to ✅.

| Asset | Shows | Status |
| --- | --- | --- |
| `graph-editor.png` | Graph Editor with the sample dialogue as nodes | ⏳ |
| `graph-editor-node.png` | Editing a single dialogue node | ⏳ |
| `validator.png` | Dialogue Validator results | ⏳ |
| `demo-runtime.gif` | Dialogue playing in-game (typewriter) | ⏳ |
| `demo-choices.gif` | Choice branching in-game | ⏳ |

<!-- Uncomment each block once the matching file exists in Documentation~/images/

### Graph Editor

![Graph Editor showing the sample dialogue as connected nodes](Documentation~/images/graph-editor.png)

![Editing a single dialogue node](Documentation~/images/graph-editor-node.png)

### Validator

![Dialogue Validator results](Documentation~/images/validator.png)

### Runtime demo

![Dialogue playing in-game with typewriter text](Documentation~/images/demo-runtime.gif)

![Choice branching in-game](Documentation~/images/demo-choices.gif)

-->

## Installation

Open **Window > Package Manager > Add package from git URL**:

```text
https://github.com/kkmia417/TalkSystem.git
```

For production projects, pin a release tag or commit:

```text
https://github.com/kkmia417/TalkSystem.git#v0.2.0
https://github.com/kkmia417/TalkSystem.git#<commit-sha>
```

Minimum Unity version: `6000.0`. The package declares `com.unity.ugui@2.0.0`, which provides the UGUI and TextMeshPro assemblies used by the runtime UI. Live2D Cubism and Spine integrations are optional and stay behind scripting defines plus their external SDKs.

## Quick Start

1. Install the package from the Git URL.
2. Import the **Feature Tour** sample from Package Manager.
3. Open `FeatureTour.unity` and press Play.
4. Open `Tools/kkmia/Dialogue Graph Editor` to inspect the sample dialogue as nodes.
5. Run `Tools/kkmia/Dialogue Validator` before shipping dialogue changes.

Start dialogue from code:

```csharp
using kkmia.TalkSystem;

DialogueManager.Instance.StartDialogue(1);
DialogueManager.Instance.StartDialogueForState("QuestStart");
```

## CSV Format

Required columns:

```csv
Id,Speaker,Text,NextId,EmotionKey,TriggerKey,ConditionKey
```

Optional production columns:

```csv
EventKey,Choices,AutoNextSeconds
```

Optional progress metadata columns:

```csv
ChapterKey,RouteKey,EndingKey
```

Example:

```csv
Id,Speaker,Text,NextId,EmotionKey,TriggerKey,ConditionKey,EventKey,Choices,AutoNextSeconds
1,Guide,"Hello, {playerName}. Choose a path.",-1,happy,QuestStart,,quest_started,"Left->2?can_go_left|Right->3",
```

Choices use `Label->NextId` entries separated by `|`. Add `?conditionKey` to hide a choice unless your `IDialogueConditionEvaluator` returns true.

`ChapterKey`, `RouteKey`, and `EndingKey` are generic progress markers. Talk System records reached keys, includes them in `DialogueSaveData.Progress`, and raises `ProgressMarkerReached`; your game owns the meaning of those keys, such as gallery unlocks, route UI, achievements, or chapter select rules.

For persistent gallery/replay flags, use `DialogueUnlockRegistry` with IDs such as `cg:opening`, `scene:prologue`, or any custom string. Unlocks can be marked directly from game code or from `EventKey` values through `DialogueUnlockEventDispatcher`, then saved to a separate global file with `DialogueUnlockSaveService` and `IDialogueUnlockStorage`.

## Runtime Extension Points

- `IDialogueConditionEvaluator`: evaluates `ConditionKey`
- `IDialogueVariableResolver`: resolves placeholders such as `{playerName}`
- `IDialogueTextResolver`: supports localization keys or external text databases
- `IDialogueEventDispatcher`: reacts to `EventKey`
- `IDialogueView`: swaps UGUI for custom UI Toolkit, Timeline, or in-game terminal views
- `DialogueUnlockRegistry` / `IDialogueUnlockStorage`: generic CG gallery, replay, and bonus-content unlock state without a built-in gallery UI
- `DialogueSaveSystem` / `IDialogueSaveStorage`: multi-slot saves, thumbnails, storage injection, failure reporting, and schema migration hooks
- `DialogueSaveData`: integrates current dialogue state with your own save system

Localization uses translation CSV files shaped as `Id,Speaker,Source,ja,en`. Configure those files and required language keys in a `DialogueValidationProfile` to report missing/extra rows, placeholder mismatches, and fallback-language usage.

## Editor Tools

Open from the Unity menu:

- `Tools/kkmia/Dialogue Graph Editor`
- `Tools/kkmia/Dialogue CSV Editor`
- `Tools/kkmia/Dialogue Validator`
- `Tools/kkmia/Dialogue Preview`
- `Tools/kkmia/Dialogue Import Export`

## Documentation

- [Installation](Documentation~/installation.md)
- [Quick Start](Documentation~/quick-start.md)
- [CSV Schema](Documentation~/csv-schema.md)
- [Runtime API](Documentation~/runtime-api.md)
- [Editor Tools](Documentation~/editor-tools.md)
- [Import and Export](Documentation~/import-export.md)
- [Migration Guide](Documentation~/migration-guide.md)
- [Packaging](Documentation~/packaging.md)
- [Performance and Limits](Documentation~/performance-limits.md)
- [Release Checklist](Documentation~/release-checklist.md)
- [Troubleshooting](Documentation~/troubleshooting.md)

## Roadmap

The current focus is package polish, graph editing, samples, import/export workflows, and CI-backed releases. See open GitHub issues for implementation tasks.

## License

MIT. See [LICENSE](LICENSE).
