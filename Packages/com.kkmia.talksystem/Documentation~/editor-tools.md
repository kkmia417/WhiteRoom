# Editor Tools

## Compiled Dialogue Sources

Create a UTF-8 `.dialogue` JSON asset listing chapter CSV sources in explicit order:

```json
{"sources":["Assets/Dialogue/chapter01.csv","Assets/Dialogue/chapter02.csv"]}
```

All sources must share identical headers. The importer uses `DialogueCsvCodec` and
`DialogueValidator` to validate the combined graph, so cross-file links are legal
and duplicate IDs are rejected. Source changes trigger automatic reimport. The
result is a `CompiledDialogueAsset`; generated data lives in Unity's import cache.
Keep source CSVs outside Resources to avoid shipping unused raw manuscript text.
Missing sources, invalid graphs and failed imports block repository creation/builds.
Builds revalidate every `.dialogue` manifest under Assets.

Validation profiles accept **Compiled Dialogues** in addition to **CSV Files**;
profile validation re-reads manifest sources and validates presentation/localization
references across the combined rows. Keep CSV Files for independent, complete CSVs.

Select a compiled asset and use `Assets > Talk System > Export Combined CSV for Preview`
to generate a temporary CSV under `Assets/Editor/DialoguePreview`. Use it with the
existing preview/graph/validator windows. Export again after editing sources; the
preview is not an authoring authority. Graph round trips only preserve known schema
columns, so do not overwrite custom-column sources with Graph Editor output.

Open tools from the Unity menu:

- `Tools/kkmia/Dialogue CSV Editor`
- `Tools/kkmia/Dialogue Validator`
- `Tools/kkmia/Dialogue Preview`
- `Tools/kkmia/Dialogue Graph Editor`

The Graph Editor is intended for visual authoring. The CSV Editor remains available for table-style editing.

## Dialogue Preview

`Tools/kkmia/Dialogue Preview` can simulate a CSV without entering Play Mode. Assign the scenario CSV, choose a start ID or `TriggerKey`, and optionally assign a translation CSV plus language/fallback keys.

The preview window exposes condition toggles discovered from `ConditionKey` and choice conditions, and variable text fields discovered from `{variable}` placeholders. The current line shows resolved text, raw text, active choices, hidden choices, `EventKey`, background/audio/voice cues, and character stage directives.

Assign a `DialogueValidationProfile` to surface missing asset and localization warnings while previewing. The window intentionally reports keys and warnings only; project-specific gameplay, gallery UI, and asset loading remain in game code.

## Validation Profiles

Create `DialogueValidationProfile` assets for production scenarios that must pass before release. A profile can include scenario CSV files, `CharacterExpressionDatabase`, `BackgroundDatabase`, `AudioDatabase`, event/condition/variable/progress key catalogs, translation CSV files, required language keys, and severity settings.

Run validation from `Tools/kkmia/Dialogue Validator` or `Tools/kkmia/Validate Dialogue Profiles`. Profiles with `Run As Build Gate` enabled are checked by Unity's build preprocess hook, and `Fail Build On Errors` blocks the build when errors are reported.

For CI, call Unity with `-executeMethod kkmia.TalkSystem.Editor.DialogueValidationRunner.ValidateFromCommandLine`. Optional arguments:

```powershell
-talkSystemValidationProfile Assets/Path/ValidationProfile.asset
-talkSystemValidationReport Temp/talksystem-validation-report.json
```

The JSON report contains `messages` and `hasErrors`, so CI jobs can archive the exact validation findings.

## Graph Round Trip

The Graph Editor reads and writes `DialogueSchema.FullHeaders`, including choices, auto-advance, presentation cues, and progress keys. Use this path when authors need visual editing without losing runtime or localization-relevant CSV fields.
