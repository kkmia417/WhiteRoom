# Dialogue presentation motion specification

Status: Implemented for [Issue #71](https://github.com/kkmia417/WhiteRoom/issues/71), extended by [Issue #73](https://github.com/kkmia417/WhiteRoom/issues/73)<br>
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

## Cancellation and restore

Every new line increments a generation token and stops the prior line coroutine.
Dialogue end, view disable, destruction, and a completed load restore the baseline
window, nameplate, choices, background, transition veil, chapter title, and portrait
transforms. Load then reapplies the current line's final focus and chapter-title state
without replaying a durable stage cue or transition.

Motion completion never advances dialogue or writes save state. All timing uses
`Time.unscaledDeltaTime`, so Auto and Skip do not leave a paused tween behind.

## Validation

- `NovelDialogueMotionControllerTests` covers active-slot, transition mood, and chapter
  title parsing policy,
  production/fallback factory wiring, singleton attachment, non-blocking overlay
  configuration, safe-area top-right anchoring, and pointer/controller choice parity.
- `WhiteRoomPlayModeStartupSmokeTests` checks Rei-to-girl focus switching, narration
  neutral state, two simultaneous placeholders, choice reveal completion, chapter-title
  suppression/restoration, and zero unexpected logs.
- Visual captures cover Rei focus, girl focus, two placeholder speakers, a choice,
  a cold chapter reveal, and an alarm chapter reveal.

This implementation conforms to ADR-0009. A future Timeline, Live2D, Spine,
Cinemachine, shader, or post-processing integration requires a separate Issue and
must preserve the same cancellation and restore contract.
