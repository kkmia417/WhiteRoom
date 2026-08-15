# Dialogue presentation motion specification

Status: Implemented for [Issue #71](https://github.com/kkmia417/WhiteRoom/issues/71), extended by [Issue #73](https://github.com/kkmia417/WhiteRoom/issues/73), [Issue #75](https://github.com/kkmia417/WhiteRoom/issues/75), [Issue #78](https://github.com/kkmia417/WhiteRoom/issues/78), [Issue #80](https://github.com/kkmia417/WhiteRoom/issues/80), and [Issue #82](https://github.com/kkmia417/WhiteRoom/issues/82)<br>
Japanese counterpart: [日本語版](dialogue-motion-spec.ja.md)

## Outcome and ownership

WhiteRoom adds restrained cinematic motion above Talk System's existing dialogue,
stage, and choice behavior. `NovelDialogueMotionController` owns transient product
polish; Talk System remains authoritative for line progression, choices, stage
state, save data, and restoration.

`DialogueMotionFactory` attaches the same controller to the configured production
`DialogueView` prefab and the runtime fallback view. No scenario column, public
package API, save field, or route rule is added.

## Motion contract

- Each line begins with a 0.22-second unscaled eased window slide/fade and a small
  nameplate emphasis. Input and typewriter progression remain available.
- The speaking portrait uses full color, a 1.025 scale, and an 8-pixel lift. Visible
  listeners use a 0.985 scale and a cool dim tint. Narration returns all visible
  portraits to a neutral tint and baseline transform.
- The active slot is resolved from the line's `Speaker` and parsed `Characters`
  directives. Rei, Nagi, and Researcher use their canonical stage keys; an unarted
  speaker uses the first visible placeholder directive. Two placeholder identities
  therefore remain independently visible and focusable.
- Active choices reveal over 0.20 seconds with a 0.055-second stagger. Pointer hover,
  controller/keyboard selection, and press states use the same scale feedback.
- A background with a resolved sprite uses a 1.08 safety scale and a slow bounded
  unscaled drift. The scale prevents the motion from exposing an edge on the target
  16:9 view; intentional ultrawide letterboxing remains governed by the stage view.
- A background or chapter cue adds a non-blocking veil above the stage and below the
  dialogue UI. Chapter fades use a stronger 0.72--1.40 second reveal; ordinary fades
  use their authored duration (clamped to 0.25--1.40 seconds); cuts use a 0.16-second
  accent, or 0.48 seconds at a chapter boundary. Night and exterior keys use deep
  navy, white rooms use pale ice blue, and alarm keys use a restrained crimson pulse.
  The veil never intercepts pointer or navigation input.
- A row carrying `ChapterKey` separates the leading chapter ordinal (for example,
  `第一章`) from the remaining title and presents both in `NovelChapterTitleView` at
  the safe area's top-right. The normal window image, speaker, and body are suppressed
  for that row, so the chapter heading is not duplicated; the existing Next control,
  typewriter completion, Auto, Skip, keyboard, and controller paths remain authoritative.
  The title waits 0.10 seconds, reveals over 0.48 seconds from the right, and exits
  over 0.18 seconds. Accent colors follow the same cold, sterile, alarm, or neutral
  mood resolved for the stage transition. The overlay itself never receives raycasts.
- The Next indicator uses a small non-blocking pulse while it is visible.
- The optional custom CSV column `ScreenEffect` resolves semantic, composable cues:
  `shake_soft`, `shake_impact`, `flash_white`, `flash_alarm`, and `zoom_in`.
  Unknown tokens are ignored without diagnostics, and a row with no recognized token
  retains the existing presentation. Multiple tokens are separated with `|`.
- Shake and zoom affect the stage root, keeping the dialogue window, speaker name,
  body, choices, and Next control stable. Shake uses a deterministic multi-frequency
  waveform with attack and cubic damping: soft is capped at 6 px / 0.32 seconds and
  impact at 18 px / 0.42 seconds. Zoom is capped at 1.035x / 0.50 seconds and returns
  to the exact baseline after its short overshoot.
- White flash is capped at 0.72 alpha / 0.26 seconds and alarm flash at 0.48 alpha /
  0.34 seconds. Both use one rapid attack and one decay; they never strobe, and their
  overscanned overlay does not receive raycasts.
- The optional custom column `TransitionStyle` selects `wipe_left`, `wipe_right`,
  `iris`, or `match_fade`. Wipes move one overscanned veil toward the named side over
  0.58 seconds. Iris opens four synchronized non-blocking panels from a restrained
  center aperture over 0.72 seconds. Match fade holds its mood color briefly, then
  decays over 0.48 seconds. Authored background duration is clamped per style; unknown
  values silently use the existing fade/cut policy.
- The optional custom column `DepthStyle` selects `still`, `drift`, `tense`, or
  `intimate`. Runtime composition places the background and all portraits in separate,
  singleton full-stage layers. Background drift is capped at 5/3 px (8/5 px for tense)
  while portraits move in the opposite direction at 15-30 percent of that distance.
  Overscan is capped at 1.10x. `still` restores both layers exactly; missing or unknown
  values preserve the subtle `drift` baseline. Dialogue UI and transient overlays are
  outside both depth layers.
- The optional `CharacterMotion` column selects directional `enter_left`/`enter_right`,
  `react_soft`, `react_sharp`, or `idle_breathe`. Transient reactions apply only to the
  resolved active portrait and use anticipation, impact, and settle within the existing
  0.22-second line entrance. Idle breathing stays below 1.2 percent scale. Narration and
  unknown cues retain normal focus motion; cancellation restores portrait baselines.

## Cancellation and restore

Every new line increments a generation token and stops the prior line coroutine.
Dialogue end, view disable, destruction, and a completed load restore the baseline
window, nameplate, choices, stage transform, screen-effect overlay, background,
transition veil and iris panels, chapter title, and portrait
transforms. Load then reapplies the current line's final focus and chapter-title state
without replaying a durable stage cue or transition.

Motion completion never advances dialogue or writes save state. All timing uses
`Time.unscaledDeltaTime`, so Auto and Skip do not leave a paused tween behind.

## Validation

- `NovelDialogueMotionControllerTests` covers active-slot, transition mood, chapter
  title parsing, typed screen-effect resolution and safety clamps,
  production/fallback factory wiring, singleton attachment, non-blocking overlay
  configuration, safe-area top-right anchoring, and pointer/controller choice parity.
- `WhiteRoomPlayModeStartupSmokeTests` checks Rei-to-girl focus switching, narration
  neutral state, two simultaneous placeholders, choice reveal completion, chapter-title
  suppression/restoration, screen-effect playback/cancellation, and zero unexpected logs.
- Visual captures cover Rei focus, girl focus, two placeholder speakers, a choice,
  a cold chapter reveal, an alarm chapter reveal, impact flash/shake, alarm flash,
  and a short stage zoom.

This implementation conforms to ADR-0009. A future Timeline, Live2D, Spine,
Cinemachine, shader, or post-processing integration requires a separate Issue and
must preserve the same cancellation and restore contract.
