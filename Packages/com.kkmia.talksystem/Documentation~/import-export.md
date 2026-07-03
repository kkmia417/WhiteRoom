# Import and Export

Planned authoring workflows:

- Google Sheets CSV workflow
- JSON import/export
- Yarn-style plain text import
- Translation CSV export/import

All import paths should validate data through `DialogueValidator` before saving changes.

## Translation CSV

Recommended translation files use one row per scenario `Id`. Keep writer context in metadata columns and put one column per language after that:

```csv
Id,Speaker,Source,ja,en
1,Guide,"Hello {playerName}.",,"Hello {playerName}."
```

`Speaker`, `Source`, `Text`, `Notes`, and `Comment` are metadata columns. They are ignored by `DialogueTranslationTable.FromCsv`; every other header is treated as a language key.

Use `Tools/kkmia/Dialogue Import Export` to generate a translation template from the scenario CSV. Re-exporting with an existing translation CSV preserves translated cells while refreshing `Speaker` and `Source` from the current scenario data.

Configure `DialogueValidationProfile` with translation CSV files, language keys, and an optional fallback language. Validation reports:

- missing translations per configured language
- extra translation `Id` values not present in scenario data
- variable placeholders missing from localized text
- extra placeholders not present in source text unless approved in the variable catalog
- explicit fallback-language usage

Scenario CSV/JSON round-trips use the full Talk System header set, so presentation columns such as `Background`, `Bgm`, `Se`, `Voice`, and `Characters` are preserved.
