# R00 chapter 1-14 full scenario and branch specification

Status: Implemented for [Issue #69](https://github.com/kkmia417/WhiteRoom/issues/69)<br>
Source: `WHITE_ROOM_第一章〜第十四章_全編_会話調整版.docx` (author-supplied, outside the repository)<br>
Japanese counterpart: [日本語版](story-condition-spec.ja.md)

## Outcome and scope

The shipped Talk System scenario is
`Assets/Resources/Dialogue/r00_escape_talksystem.csv`. It contains the complete
fourteen-chapter manuscript adapted into visual-novel turns. The importer preserves
the two decisions and four endings while placing attributed speech in `Speaker` and
its quote-free contents in `Text`.

This change does not add a dialogue schema, condition grammar, score system, route,
or ending. It adds one visible generic placeholder portrait for speakers whose
final artwork is not available. Separate left/right presentation keys allow two
such speakers to remain visible at the same time. The save content version is
`r00_chapters_01_14_v4`, preventing old concise-scenario positions from restoring
into unrelated full-manuscript prose.

## Published scenario contract

| Property | Published value |
| --- | ---: |
| Dialogue rows | 10,648 |
| Preferred split target | 24-36 characters |
| Enforced `Text` ceiling | 40 characters |
| First dialogue ID | 1,000,001 |
| Source paragraphs | 14,156 |
| Quoted source paragraphs | 7,250 |
| Unresolved quoted speakers | 0 |
| Chapters | 14 |
| Choice nodes | 2 |
| Unique endings | 4 |
| `ConditionKey` values | 0 |
| Voice cues | 0 |

`ChapterKey` values are `chapter_01` through `chapter_14`. The canonical path uses
`RouteKey=main`; alternate branches start with `bad_return`, `managed_future`, and
`single_answer`.

## Reviewed branch table

| Choice | Options | Consequence |
| --- | --- | --- |
| Chapter 1 escape | Enter the unknown passage / surrender to security | Entering continues the main story. Surrender reaches `ending_return_to_white_room`. |
| Chapter 12 central decision | Govern with Nagi / let Rei define one rule / reject both | The first two options reach `ending_managed_future` and `ending_single_answer`; rejecting both continues through chapters 13-14 to `ending_beyond_correctness`. |

No later choice is filtered by prior state. Every option has an authored consequence,
so there is no invisible flag dependency, zero-choice state, or Save/Load-only route.

## Import, editorial, and presentation rules

- Validate the source byte size and SHA-256 before importing.
- Track every source paragraph in
  `docs/development/white-room-source-map.json`; untracked paragraphs are forbidden.
- Keep deterministic assignments for every emitted quote in
  `docs/development/white-room-speaker-ledger.json` and publish the corresponding
  audit in `white-room-speaker-audit.json`.
- Source-indexed reviewed assignments take precedence over an older inferred ledger.
  All 575 quoted paragraphs in chapter 1 are context-reviewed; narration gaps must
  never shift the speaker name to the preceding or following participant.
- Prefer 24-36 visible characters per turn and never exceed 40. Split at complete
  sentence endings before considering character count; a continued clause must use
  an explicit em dash and must never end a turn with a comma. Preserve the first
  fragment's public row ID and allocate continuation fragments from the reserved
  1,200,000 range.
- Put a spoken line's identity only in `Speaker`; remove its outer Japanese quote
  marks from `Text`. Narration uses `Speaker=地の文`.
- Start every chapter row with the full-stage clear directive `*` before showing its
  cast. End every route with `*` and `Bgm=stop`.
- Keep both participants in the left and right slots during a local conversation.
  Use the checked-in Rei, Nagi, and Researcher art where available; every other
  spoken role uses the visible placeholder asset through distinct
  `PlaceholderLeft` and `PlaceholderRight` stage identities. Chapter 1 opens with
  Rei and the unidentified girl's placeholder; it must not reveal Nagi early.
- Keep Voice empty until approved recordings exist.

The deterministic importer at `scripts/import_white_room_novel.py` is the source of
the shipping CSV. Re-running it with the same manuscript and checked-in speaker
ledger must produce identical scenario, audit, and source-map artifacts.

## Validation contract

- IDs are unique and every `NextId` and choice target resolves.
- The file has exactly 10,648 rows, fourteen chapter markers, two choice nodes, four
  unique endings, zero conditions, and no `Text` longer than 40 characters.
- All 14,156 source paragraphs are emitted or carry an explicit omission reason.
- All 7,250 quoted paragraphs are named or explicitly omitted; unresolved count is 0.
- The real prefab and runtime fallback keep `SpeakerText` in a dedicated top name
  region and `BodyText` below it.
- Every stage directive resolves, including the visible generic placeholder.
- Talk System validation and the cleared Unity Console report no warnings or errors
  across all four ending routes and Save/Load journeys.

## Remaining production content

The full scenario is playable with the existing character art and the generic
placeholder. Replacing the placeholder with final role-specific portraits,
scene-specific CGs, expanded backgrounds, final audio, and recorded voice remains
future production work.
