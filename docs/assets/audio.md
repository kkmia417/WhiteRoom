# Audio assets

Issue #14 registers every BGM and sound-effect key referenced by
`r00_escape_talksystem.csv`.

## Provenance and permission

No approved production audio was supplied with the repository. Following the
Issue's placeholder policy, all checked-in WAV files are deterministic
synthetic placeholders created specifically for WhiteRoom on 2026-07-30. They
contain only mathematically generated oscillators and seeded noise; no sample,
recording, composition, or asset from a third party is included. The repository
owner requested and authorized their creation and use in this project.

These files deliberately preserve stable runtime keys and may be replaced by
approved production recordings later without changing CSV content or database
references.

## Generation and normalization

All source files are mono, 16-bit PCM WAV at 44.1 kHz.

- BGM: twelve seamless eight-second synthesized ambience loops. Their RMS level
  is approximately 0.09 (-20.9 dBFS), with peaks between 0.25 and 0.36.
- SE: twenty-one short synthesized mechanical, impact, movement, alarm, spark,
  and splash cues. Their RMS values are 0.038 to 0.105, with peaks between 0.50
  and 0.55.

Every waveform is DC-centered and peak-limited below -5 dBFS. The focused Unity
test reads the source PCM directly and rejects unexpected channel count, sample
rate, duration, clipping, or normalization drift.

## Import and playback policy

`WhiteRoomAudioImportSettings` applies category-specific settings:

- BGM uses mono Vorbis streaming, background loading, no preload, and preserved
  sample rate.
- SE uses mono ADPCM, decompress-on-load, preload, and preserved sample rate.

`DialogueAudioPlayer` uses independent BGM, SE, and Voice `AudioSource`
instances. BGM is looped; CSV `#fade:*` and `stop` remain control cues rather
than database keys; SE uses one-shot playback and therefore does not replace or
interrupt the active BGM clip.

## Registered keys

BGM: `alarm`, `alarm_low`, `corridor_rush`, `duct_alarm`, `duct_tension`,
`escape_begin`, `escape_final`, `furnace_rumble`, `quiet_dark`,
`stair_descent`, `sterile_low`, `tense_low`.

SE: `body_fall`, `camera_down`, `camera_focus`, `distant_door`,
`distant_drone`, `door_close`, `door_grind`, `door_open`, `drone_alert`,
`footsteps`, `furnace_start`, `gate_open`, `grab`, `inject`, `lock`,
`metal_crash`, `screw`, `spark`, `splash`, `vent_close`, `vent_open`.
