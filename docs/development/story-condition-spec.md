# R00 chapter 1-14 adaptation and branch specification

Status: Implemented for [Issue #65](https://github.com/kkmia417/WhiteRoom/issues/65)<br>
Source: `WHITE_ROOM_第一章〜第十四章_全編_会話調整版.docx` (author-supplied, outside the repository)<br>
Japanese counterpart: [日本語版](story-condition-spec.ja.md)

## Outcome and scope

The light-novel manuscript is adapted into the shipped Talk System scenario at
`Assets/Resources/Dialogue/r00_escape_talksystem.csv`. All fourteen chapters are
present in order. The adaptation keeps the manuscript's character arcs and central
question while changing paragraph rhythm into message-window-sized units.

The previous 199-row escape prototype and its fourteen endings are replaced. This
change does not add a new dialogue schema, condition grammar, score system, major
route, or runtime manager. The existing `R00EscapeStart` trigger and Resources path
remain stable for the current prototype integration.

Issue #65 is the implementation contract for the final dialogue-attribution audit
and side-ending pass. Issue #22 describes condition-gated branches for the replaced
prototype and is no longer a contract for this scenario.

## Published scenario contract

| Property | Published value |
| --- | ---: |
| Dialogue rows | 9,904 |
| First dialogue ID | 1,000,001 |
| Chapters | 14 |
| Choice nodes | 2 |
| Unique endings | 4 |
| `ConditionKey` values | 0 |
| Voice cues | 0 |

`ChapterKey` values are `chapter_01` through `chapter_14`. The canonical path uses
`RouteKey=main`; alternate branches use `bad_return`, `managed_future`, and
`single_answer`. These progress markers describe reached content and are not hidden
relationship or morality scores.

The new ID range is disjoint from the retired prototype's IDs 1-880, and the save
content version is `r00_chapters_01_14_v2`. An old prototype save therefore fails as
missing content instead of silently restoring a different line with a recycled ID.

## Reviewed branch table

| Choice | Options | Consequence |
| --- | --- | --- |
| Chapter 1 escape | Enter the unknown service passage / surrender to security | Entering continues the complete manuscript. Surrender reaches `ending_return_to_white_room`. |
| Chapter 12 central decision | A: govern with Nagi / B: let Rei define the new rules / reject both A and B | A reaches `ending_managed_future`; B reaches `ending_single_answer`; rejecting the premise continues chapters 13-14 and reaches `ending_beyond_correctness`. |

No later choice is filtered by prior state. Every option has an immediate authored
consequence, so there is no invisible flag dependency, zero-choice state, or
Save/Load-only route requirement.

## Editorial adaptation rules

- Preserve all fourteen chapter boundaries and their order.
- Preserve dialogue, plot information, foreshadowing, character decisions, and the
  chapter 14 ending.
- Join short narration fragments into message-window units of roughly 76 Japanese
  characters instead of presenting every light-novel fragment as a separate click.
- Remove scene-divider glyphs, adjacent duplicate paragraphs, and compact speech
  attributions when the nameplate already carries the same information.
- Assign a nameplate only when the manuscript contains a reliable attribution.
  Ambiguous rapid exchanges retain Japanese quotation marks under the narration
  identity rather than guessing the wrong speaker.
- Record every quoted source paragraph, source index, dialogue ID, confidence,
  evidence, and unresolved reason in
  [`white-room-speaker-audit.json`](white-room-speaker-audit.json). Character actions
  are not treated as speech attribution. Remote-channel continuity is bounded and
  starts only from an explicit or reviewed source anchor.
- Keep voice fields empty until approved recordings, performer terms, and locale
  coverage exist.

The deterministic importer is `scripts/import_white_room_novel.py`. From the
repository root, regenerate the CSV and reviewed route fixture with:

```powershell
python -X utf8 .\scripts\import_white_room_novel.py `
  <path-to-manuscript.docx> `
  .\Assets\Resources\Dialogue\r00_escape_talksystem.csv `
  --route-matrix .\Assets\Tests\Fixtures\r00_ending_routes.json `
  --speaker-audit .\docs\development\white-room-speaker-audit.json
```

The manuscript is not checked into the repository, so this command requires the
author-supplied DOCX.

## Validation contract

- IDs are unique and every `NextId` and choice target resolves.
- Exactly fourteen chapter markers, two choice nodes, four unique endings, and zero
  conditions are present.
- The route fixture reaches every ending without a cycle or unused choice.
- Presentation keys resolve through the existing background, character, and audio
  databases; every Voice field remains empty.
- Repository governance, Python tests, Talk System validation, and Unity batch-mode
  compilation pass before release.

## Content still needed for an AAA production pass

The fourteen-chapter narrative is complete through its canonical ending; no missing
chapter or required plot bridge was found. The A and B side endings now include a
short dramatized aftermath rather than stopping at a summary.

The manuscript does not identify a speaker for 6,505 of its 7,250 quoted paragraphs.
Those lines remain readable as quoted narration and are individually classified in
the audit. Converting all of them to nameplates requires author-supplied speaker
annotations; alternation or character actions are not reliable enough to invent
them. This is the only story-source input still needed for a fully named script.

Production also lacks portraits for the extended cast, scene-specific backgrounds
for the new locations, final BGM/SE, CG direction, and recorded voice. Transparent
nameplate placeholders and the existing prototype presentation library are used so
missing art cannot block dialogue playback.

At 9,904 rows, the scenario also exceeds Talk System's roughly 5,000-row single-file
authoring recommendation. Chapter-level scenario units should be delivered through
the content service planned by ADR-0006 instead of expanding prototype Resources
loading as a side effect of this content import.
