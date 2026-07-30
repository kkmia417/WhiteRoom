# Autosave checkpoints and Continue selection

Japanese counterpart: [日本語版](autosave-checkpoints.ja.md)

Thumbnail capture details: [Save thumbnails](save-thumbnails.md)

WhiteRoom uses one reserved autosave slot: Talk System slot `0`. Every successful
autosave replaces that slot; autosave rotation is intentionally out of scope.

## Checkpoints

The application requests an autosave only at these story events:

- the first ready line of a newly reached chapter;
- the first ready destination line after a choice is confirmed; and
- the confirmed final line after an ending unlock has been persisted.

Chapter and post-choice requests remain pending while text is typing or a choice UI
is visible. They write once when the destination line reaches
`WaitingForInput`. Ending autosave writes from `LineCompleted`, after the final text
is confirmed and before `DialogueEnded` clears the current row. Auto/Skip is
temporarily normalized during capture. A load always resumes in Normal mode; the
player's persistent text/audio/Auto/Skip settings remain unchanged.

Each checkpoint captures Talk System narrative state plus every registered save
contributor. This includes choice records, progress markers, stage state, and audio
state. Restore redraw does not dispatch line events or progress markers, so confirmed
choices and ending unlocks are not applied twice.

Autosave failure publishes UI feedback and a warning, consumes that request, and
does not stop dialogue or start a per-frame retry loop. A later distinct checkpoint
may try again.

## Continue candidate

Continue considers every loadable manual, quick, and autosave and selects the newest
timestamp. Category does not override a newer timestamp. If timestamps are equal to
the second, the existing Talk System slot ordering decides: manual slot (highest slot
index) first, autosave slot `0` second, quick-save slot `-1` last. Corrupt,
incompatible, or otherwise unloadable slots are excluded.

This policy is shared by the Title Continue availability check and the actual load.
