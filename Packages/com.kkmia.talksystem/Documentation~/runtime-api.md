# Runtime API

## Extension Points

- `IDialogueConditionEvaluator`
- `IDialogueVariableResolver`
- `IDialogueTextResolver`
- `IDialogueEventDispatcher`
- `IDialogueView`

## Progress Metadata

CSV columns `ChapterKey`, `RouteKey`, and `EndingKey` are generic markers. `DialoguePresenter.ProgressMarkerReached` and `DialogueManager.ProgressMarkerReached` fire when a shown line contains a marker, and `DialogueManager.Progress` returns the current recorded state.

```csharp
DialogueManager.Instance.ProgressMarkerReached += context =>
{
    if (context.Marker.Type == DialogueProgressMarkerType.Ending &&
        context.Marker.IsFirstReach)
    {
        UnlockEnding(context.Marker.Key);
    }
};
```

Progress state is included in `DialogueSaveData.Progress`. The package records and restores keys only; your game layer owns route selection rules, chapter select UI, gallery unlocks, achievements, and any product-specific meaning attached to those keys.

## Unlock Registry

Use `DialogueUnlockRegistry` for persistent gallery, scene replay, achievement, or bonus-content flags that should live outside an individual save slot. IDs are plain strings. Categories are also strings and are inferred from the prefix before `:`, so `cg:hero_smile` belongs to `cg` and `scene:prologue` belongs to `scene`.

```csharp
var unlocks = new DialogueUnlockRegistry();
unlocks.MarkUnlocked("cg:hero_smile");
unlocks.MarkUnlocked("scene:prologue");

if (unlocks.IsUnlocked("cg:hero_smile"))
{
    ShowGalleryThumbnail("cg:hero_smile");
}
```

To unlock from dialogue data, wrap your existing event dispatcher:

```csharp
var unlocks = new DialogueUnlockRegistry();
var gameEvents = new DelegateDialogueEventDispatcher(HandleGameEvent);
DialogueManager.Instance.SetEventDispatcher(
    new DialogueUnlockEventDispatcher(unlocks, gameEvents));
```

With the default prefix, an `EventKey` of `unlock:cg:hero_smile` marks `cg:hero_smile` as unlocked and still forwards the original event to your game dispatcher. Pass an empty prefix only if every `EventKey` should be treated as an unlock ID.

For global persistence, store unlocks separately from per-slot dialogue progress:

```csharp
var unlockStorage = new FileDialogueUnlockStorage();
var unlockSaves = new DialogueUnlockSaveService(unlockStorage);

unlockSaves.LoadInto(unlocks);
unlocks.MarkUnlocked("scene:after_story");
unlockSaves.Save(unlocks);
```

Talk System does not implement a CG gallery or replay menu. Build those screens in your game UI by querying `ListUnlockedIds("cg")`, `ListUnlockedIds("scene")`, or custom categories and mapping those IDs to your own assets.

## Save Data

Use `DialogueManager.CaptureState()` and `DialogueManager.RestoreState(saveData)` to integrate with your game's save system.

Talk System gives you serializable state and an optional multi-slot persistence layer. Use `DialogueSaveSystem` when you want package-managed JSON slots, thumbnails, contributors, and restore orchestration. Use `DialogueManager.CaptureState()` directly when your game already owns every save file.

Stage and audio binders implement `IDialogueSaveContributor`. Register or assign them to `DialogueSaveSystem` so background, visible characters, current BGM, and current voice restore with the dialogue state. Restore uses zero-duration presentation changes and does not replay one-shot SE.

Default presentation components implement `IDialoguePresentationIssueSource`:

```csharp
var issueSource = audioPlayer as IDialoguePresentationIssueSource;
issueSource.PresentationIssueRaised += context =>
{
    Debug.LogWarning($"{context.Kind}: {context.Key}");
};
```

Use this for UI errors or telemetry when a background, audio, stage slot, sprite, or model key cannot be resolved at runtime.

### Storage Injection

`DialogueSaveSystem` uses `FileDialogueSaveStorage` by default and writes under `Application.persistentDataPath/dialogue_saves`. Games can replace that storage without subclassing package code:

```csharp
var saveSystem = GetComponent<DialogueSaveSystem>();
saveSystem.SetStorage(new MySteamCloudSaveStorage());
```

For scene-driven setup, assign a `MonoBehaviour` that implements `IDialogueSaveStorageProvider` to the storage provider field:

```csharp
public sealed class SteamSaveStorageProvider : MonoBehaviour, IDialogueSaveStorageProvider
{
    public IDialogueSaveStorage CreateStorage()
    {
        return new MySteamCloudSaveStorage();
    }
}
```

To keep the file storage but move the root directory, call `ConfigureFileStorageRoot(path)` or set the directory override in the inspector.

`FileDialogueSaveStorage` writes slot JSON and thumbnail PNG files through a temp-file replacement step so interrupted writes do not leave half-written target files.

### Failure Reporting

`DialogueSaveService` and `DialogueSaveSystem` expose `OperationCompleted`, `OperationFailed`, and `LastOperationResult`. Storage adapters may throw `IOException` or platform-specific exceptions; the service catches them and reports a `DialogueSaveOperationResult` instead of silently swallowing failures.

```csharp
saveSystem.OperationFailed += result =>
{
    Debug.LogWarning($"Save operation failed: {result.Operation} slot={result.SlotIndex} {result.Message}");
};
```

### Versioning And Migration

Saved slots include Talk System schema metadata plus game-owned `ContentVersion` and `ProductChannel` strings on both `DialogueSaveSlot` and `DialogueSaveData`.

```csharp
saveSystem.SetSaveMetadata("chapter-2", "demo");
```

Talk System owns `DialogueSaveSchema.CurrentVersion`. Your game owns the meaning of content versions and product channels, such as demo/full compatibility, route pack names, or store-specific build channels.

Register migrations when an older save needs game-specific changes:

```csharp
public sealed class DemoToFullSaveMigration : IDialogueSaveDataMigration
{
    public int FromSchemaVersion { get { return 0; } }
    public int ToSchemaVersion { get { return DialogueSaveSchema.CurrentVersion; } }

    public void Migrate(DialogueSaveData data, DialogueSaveMigrationContext context)
    {
        // Preserve ExtraState unless your game explicitly rewrites a key.
        data.SetExtra("convertedFrom", context.ProductChannel);
    }
}

saveSystem.RegisterDataMigration(new DemoToFullSaveMigration());
```

Unknown future schema versions fail to load. If a saved dialogue ID no longer exists in the active repository, configure the policy explicitly:

```csharp
saveSystem.SetMissingDialoguePolicy(DialogueMissingDialoguePolicy.RestoreEnded);
// or:
saveSystem.SetMissingDialoguePolicy(DialogueMissingDialoguePolicy.UseFallbackDialogueId, fallbackId: 1000);
```

## Async Loading And Large Projects

Use `IDialogueRepositoryLoader` when dialogue data is loaded after scene boot, from remote content, or from Addressables.

```csharp
DialogueManager.Instance.LoadRepository(
    new TextAssetDialogueRepositoryLoader(dialogueCsv));
```

For large games, split dialogue into multiple databases and merge them after loading:

```csharp
var loader = new CompositeDialogueRepositoryLoader(new IDialogueRepositoryLoader[]
{
    new TextAssetDialogueRepositoryLoader(commonCsv),
    new TextAssetDialogueRepositoryLoader(chapterCsv)
});

DialogueManager.Instance.LoadRepository(loader);
```

Addressables support is intentionally dependency-free in the runtime package. Load your Addressable `TextAsset` in project code, then pass it into `TextAssetDialogueRepositoryLoader`, or implement `IDialogueRepositoryLoader` directly for custom caching, patching, or remote delivery.

Missing data should call the loader `onError` callback with a clear message; `DialogueManager.ErrorRaised` can route that message into your telemetry or debug UI.
