# Live2D / Spine Characters

The stage layer draws characters through a pluggable backend, so static sprites can be swapped for Live2D Cubism, Spine, or any prefab/Animator-driven model.

## How it fits

`DialogueStageView` renders the background and, by default, sprite slots. Assign a **character backend** (`IDialogueCharacterBackend`) and the view delegates all character drawing to it while still handling the background.

```
DialogueStageDirector → DialogueStageView → IDialogueCharacterBackend
                                            (default: built-in sprite slots)
                                            (model:   ModelDialogueCharacterBackend)
```

## Model backend (SDK-free)

1. Add `ModelDialogueCharacterBackend` to a GameObject.
2. For each character, add a `DialogueCharacterModel` (or a subclass) and set its `CharacterKey` to match the CSV `Speaker` / `Characters` key.
3. Register the models on the backend and map slots (`left`/`center`/`right`) to anchor `Transform`s.
4. Assign the backend to `DialogueStageView.characterBackend`.

`DialogueCharacterModel` drives, by default:
- **Show/Hide** via `GameObject.SetActive`.
- **Expression** via an Animator trigger named after the expression (e.g. `smile`).
- **Animation** via an Animator trigger named after the animation (e.g. `fadein`).
- **Mouth open** via an Animator float parameter (`MouthOpen`).

This works as-is for Live2D Cubism models animated through an Animator and for Spine `SkeletonMecanim`.

## Lip-sync

Add `DialogueModelLipSyncBinder` and assign the `DialogueLipSync` (Phase 3) and the `ModelDialogueCharacterBackend`. On each line it finds the speaking character's model and forwards the 0..1 openness to `IDialogueLipSyncTarget.SetMouthOpen`.

## Live2D Cubism (native parameters)

For direct Cubism parameter control:

1. Import the Cubism SDK for Unity.
2. Add `TALKSYSTEM_LIVE2D` to **Project Settings → Player → Scripting Define Symbols**.
3. Use `Live2DDialogueCharacterModel` on your Cubism model; set the mouth parameter id (default `ParamMouthOpenY`).

Without the define, the Live2D file is excluded from compilation, so the package builds with no SDK present.

## Spine

1. Import spine-unity.
2. Add `TALKSYSTEM_SPINE` to the Scripting Define Symbols.
3. Use `SpineDialogueCharacterModel` on a `SkeletonAnimation`; expressions and animations map to Spine animation names on separate tracks.

## Custom backends

Implement `IDialogueCharacterBackend` directly for full control, or subclass `DialogueCharacterModel` to override `Show`, `Hide`, `SetExpression`, `PlayAnimation`, or `SetMouthOpen`.
