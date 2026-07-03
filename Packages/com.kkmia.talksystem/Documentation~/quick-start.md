# Quick Start

1. Import the **Feature Tour** sample.
2. Open `FeatureTour.unity`.
3. Press Play.
4. Inspect the sample CSV and the `DialogueManager` object.

## Start Dialogue From Code

```csharp
using kkmia.TalkSystem;

DialogueManager.Instance.StartDialogue(1);
DialogueManager.Instance.StartDialogueForState("QuestStart");
```

## Variables

```csharp
DialogueManager.Instance.SetVariableResolver(new MyVariableResolver());
```

Implement `IDialogueVariableResolver` to resolve `{playerName}` or other placeholders immediately before display.
