# Favorite voice replay and persistence

Japanese counterpart: [日本語版](favorite-voices.ja.md)

WhiteRoom owns current-line voice replay and favorite-voice policy. The reusable
Talk System continues to own voice cues, clip lookup, and the single Voice
playback channel; it has no knowledge of favorites.

## Player behavior

- `VOICE` and `+FAV` are enabled only when the current dialogue row has a Voice
  key that resolves through the active `AudioDatabase`.
- Replay and list playback stop the Voice channel before starting the requested
  clip, so repeated input never layers multiple voice sources.
- `FAV` opens only when at least one valid favorite exists. The list shows a
  stable registration-order number, speaker, current localized dialogue text,
  and dialogue ID. Play, Stop, Remove, and Back are normal selectable buttons;
  Escape closes the screen and S stops playback.
- Opening the list suspends Auto, Skip, and background dialogue input. Closing,
  loading, or changing scenes stops favorite playback before gameplay resumes.
- Removing the final entry leaves the open list on its empty state. With no
  favorites at command-bar creation or refresh time, `FAV` is disabled.

## Durable data

`FavoriteVoiceService` stores a versioned JSON document under the PlayerPrefs
key `WhiteRoom.FavoriteVoices.Json`. Identity is `DialogueId + VoiceKey`; display
text and asset paths are never persisted. On every list build, speaker and text
are resolved again from the active localized dialogue repository.

Schema version 1 stores the stable registration order. Version 0 is migrated by
using source order. Duplicate records are collapsed. Unknown dialogue IDs and
records whose current Voice key no longer matches are ignored. A known record
whose clip is temporarily missing remains visible but its Play button is
disabled, allowing the player to remove it. Corrupt, future-version, unreadable,
or unwritable data produces a warning and does not block dialogue startup.

This is a product-owned versioned preference, not a Talk System save contributor
or save-slot section. It follows the migration and safe-failure principles of
[ADR-0008](../adr/0008-versioned-save-compatibility.md) without changing the
package API or save-envelope schema.

## Current release voice policy

The shipped scenario deliberately contains no Voice cues and the shipped audio
database contains no voice clips under [the current voice policy](../assets/voice.md).
Consequently all three favorite/replay commands are disabled in the current
content build. The implementation and fixture tests are ready for a later
release with approved recordings.
