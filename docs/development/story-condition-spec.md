# R00 chapter 1-14 concise scenario and branch specification

Status: Implemented for [Issue #65](https://github.com/kkmia417/WhiteRoom/issues/65), refined by [Issue #68](https://github.com/kkmia417/WhiteRoom/issues/68)<br>
Long-form source: `WHITE_ROOM_第一章〜第十四章_全編_会話調整版.docx` (author-supplied, outside the repository)<br>
Japanese counterpart: [日本語版](story-condition-spec.ja.md)

## Outcome and scope

The shipped Talk System scenario is
`Assets/Resources/Dialogue/r00_escape_talksystem.csv`. It is a concise interactive
adaptation of all fourteen chapters, retaining the central conflict, Rei and Nagi's
relationship, both decisions, and all four endings without reproducing the full
light-novel manuscript turn by turn.

Title `NEW GAME` loads `Main` first and starts `R00EscapeStart` only after the scene
load completes. This change does not add a dialogue schema, condition grammar, score
system, route, character, or presentation asset.

## Published scenario contract

| Property | Published value |
| --- | ---: |
| Dialogue rows | 134 |
| Maximum authored `Text` length | 23 characters |
| Enforced per-turn ceiling | 52 characters |
| First dialogue ID | 1,000,001 |
| Chapters | 14 |
| Choice nodes | 2 |
| Unique endings | 4 |
| `ConditionKey` values | 0 |
| Voice cues | 0 |

`ChapterKey` values are `chapter_01` through `chapter_14`. The canonical path uses
`RouteKey=main`; alternate branches start with `bad_return`, `managed_future`, and
`single_answer`. The stable start, chapter, choice-target, and ending IDs remain in
the 1,000,001-1,009,892 range. The save content version is
`r00_chapters_01_14_v3`.

## Reviewed branch table

| Choice | Options | Consequence |
| --- | --- | --- |
| Chapter 1 escape | Enter the unknown passage / surrender to security | Entering continues the main story. Surrender reaches `ending_return_to_white_room`. |
| Chapter 12 central decision | Govern with Nagi / let Rei define one rule / reject both | The first two options reach `ending_managed_future` and `ending_single_answer`; rejecting both continues through chapters 13-14 to `ending_beyond_correctness`. |

No later choice is filtered by prior state. Every option has an authored consequence,
so there is no invisible flag dependency, zero-choice state, or Save/Load-only route.

## Editorial and presentation rules

- Preserve all fourteen chapter boundaries, the main plot spine, the two decisions,
  and the four ending meanings.
- Keep every dialogue turn at or below 52 Japanese characters. Prefer one complete
  beat per click and remove repeated explanation.
- Start every chapter row with the full-stage clear directive `*` before showing the
  scene's intended cast.
- Explicitly clear or exit portraits at major location changes. In particular,
  dialogue ID 1,000,004 exits Nagi before showing Rei alone; chapters 5, 6, and 10
  also open with Rei alone.
- End every route with `*` and `Bgm=stop` so portraits and audio do not leak into the
  result screen.
- Keep all presentation keys resolvable through the checked-in background,
  character, and audio databases. Keep Voice empty until approved recordings exist.

The deterministic importer at `scripts/import_white_room_novel.py` and its speaker
audit remain historical tools for reviewing the long-form manuscript. They do not
generate the current concise shipping CSV; running the importer against that path
would replace the reviewed 134-row adaptation and requires a new content review.

## Validation contract

- IDs are unique and every `NextId` and choice target resolves.
- The file has exactly 134 rows, fourteen chapter markers, two choice nodes, four
  unique endings, zero conditions, and no turn longer than 52 characters.
- Every chapter and ending resets portrait state; Rei-only scene boundaries cannot
  inherit Nagi.
- The route fixture reaches every ending without a cycle or unused choice.
- Talk System validation reports no missing speaker, expression, background, BGM,
  or SE key.
- PlayMode clicks the production `NewGameButton`, observes `Main`, and confirms that
  dialogue ID 1,000,001 has started.

## Remaining production content

The concise scenario is complete and playable with the existing Rei, Nagi, and
Researcher portraits and the current prototype background/audio library. No new
image is required for the Title transition or portrait-state repair. Scene-specific
CGs, a broader background set, final music and sound design, and recorded voice
remain optional future production work rather than blockers for this flow.
