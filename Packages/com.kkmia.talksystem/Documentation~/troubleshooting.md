# Troubleshooting

## Dialogue Does Not Start

- Confirm `DialogueManager` has a CSV assigned.
- Confirm the start ID exists.
- Run `Tools/kkmia/Dialogue Validator`.

## Text Columns Shift After Editing

Use the built-in CSV editor or another RFC4180-compatible CSV editor. Talk System supports quoted commas and multiline text, but spreadsheet export settings still matter.

## Choices Do Not Appear

- Confirm the `Choices` column exists.
- Confirm the syntax is `Label->NextId`.
- Confirm optional choice conditions evaluate to true.
