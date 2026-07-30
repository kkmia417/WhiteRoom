# Voice support policy

## Release decision

WhiteRoom does not support character voice recordings in the current release.
This is option 4 from Issue #15. The repository contains no approved performer
recordings, usage terms, locale coverage, or credits, and silent placeholder
clips are explicitly not an acceptable substitute.

The Talk System `Voice` schema, `AudioDatabase.voice` catalog, playback channel,
save snapshot field, and backlog hooks remain available for a future release.
Keeping those dormant contracts avoids a schema migration while ensuring the
current product does not imply voice support that it cannot lawfully or
consistently deliver.

## Enforced current-release contract

- Every row of `r00_escape_talksystem.csv` has an empty `Voice` field.
- `WhiteRoomAudioDatabase.voice` has zero entries.
- Empty Voice fields are intentional and do not produce validation or runtime
  missing-asset warnings.
- Starting any line stops the previous Voice channel. Advancing and Skip mode
  therefore cannot leave a recording playing after its line.
- Save, Load, and Rollback snapshots carry an empty voice key. Restore calls
  `StopVoice` and never starts or duplicates playback.
- Auto mode uses `DialogueSettings.AutoAdvanceDelay`; explicit row auto timing
  remains text/scenario timing. Voice duration never extends either delay in
  this release.
- Backlog voice replay controls remain hidden because entries have no voice key.

These rules follow the deterministic cancellation and restore policy in
[ADR-0009](../adr/0009-deterministic-presentation-runtime.md). The release
scope itself is reversible product configuration, so it does not introduce a
new architecture decision.

## Reserved future key rule

If approved voice is introduced, keys use lowercase ASCII in the form
`voice_r00_<dialogue-id>_<speaker-slug>`, for example
`voice_r00_0003_researcher`. Dialogue IDs are stable; localized catalogs may
resolve the same semantic key to locale-specific clips under ADR-0007 and
ADR-0006. No key is reserved by adding an empty or silent clip now.

## Credits and license status

There are no performers, voice recordings, or voice licenses in this release,
so the voice credit list is intentionally empty. BGM and SE provenance is
recorded separately in [audio.md](audio.md).

## Reconsideration gate

Voice support requires a new bounded Issue and all of the following before any
CSV cue is added:

- approved final recordings rather than silence placeholders;
- performer names or approved pseudonyms and credit wording;
- explicit usage, distribution, editing, and platform terms;
- locale coverage and fallback policy;
- catalog registration and missing-reference validation;
- tests for line advance, Skip, Auto timing, backlog replay, Save/Load,
  Rollback, and cancellation without double playback.
