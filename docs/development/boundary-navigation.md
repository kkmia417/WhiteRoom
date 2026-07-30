# Reached scene and choice navigation

Japanese counterpart: [日本語版](boundary-navigation.ja.md)

WhiteRoom's command bar exposes four reached-boundary operations:

- `<C`: previous reached choice
- `<S`: previous reached scene
- `S>`: next reached scene
- `C>`: next reached choice

A scene boundary is a dialogue row with `ChapterKey` and has identity
`scene:<chapter-key>:<dialogue-id>`. A choice boundary is a row with choices and
has identity `choice:<dialogue-id>`. These identities come from Talk System
scenario data; command buttons and rendered choice indices are never navigation
identities.

## Reached-range behavior

The application records a coherent in-memory snapshot at each reached boundary.
Previous and next select only the nearest recorded boundary of the requested
kind. A next command never enters unread content and restoring a choice leaves it
pending without selecting an option.

After a backward jump, existing later checkpoints remain available until normal
dialogue progress resumes. Advancing or choosing from that earlier point truncates
the old forward tail before new checkpoints are recorded, so incompatible branch
state cannot be combined.

The four buttons are disabled when no matching target exists. Their tooltip says
whether no previous/next reached scene/choice exists, navigation is busy, the
runtime is not ready, or dialogue input is blocked.

## Coherent restore and failures

`DialogueBoundaryNavigationService` uses
`DialogueSaveSystem.CaptureState(excludedContributor)` and `RestoreState(...)`.
The snapshot includes dialogue choices, seen lines, progress, Backlog, stage, BGM,
and voice contributors. Restore does not start a row and therefore does not replay
line events, one-shot SE, or durable unlock side effects.

Jump execution stops Auto, Skip, and backward skip; closes conflicting overlays;
and blocks dialogue, command-bar, and save input until restore completes. Missing
rows, failed conditions, repeated cycles, invalid snapshots, contributor failures,
and missing targets return classified results and leave the current dialogue usable.

## Save compatibility

Reached checkpoints, cursor position, and cycle markers are stored as versioned
JSON under `DialogueSaveData.ExtraState` key
`whiteroom.boundary-navigation.v1`. Nested checkpoints exclude the navigation
contributor itself. Existing saves without that key remain valid and begin a new
timeline from the restored current boundary. Malformed or future payloads are
ignored with a warning without rejecting the underlying save.

See [ADR-0011](../adr/0011-reached-boundary-navigation.md) for the durable
architecture contract.
