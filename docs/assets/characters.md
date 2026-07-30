# Character sprite assets

Issue #13 adds the production-ready static character sprites used by the
`Characters` column in `r00_escape_talksystem.csv`.

## Provenance and permission

The Rei, Nagi, and Researcher images were generated specifically for WhiteRoom
with OpenAI's built-in image generation on 2026-07-30. They were not copied
from a third-party artwork or asset pack. The repository owner requested and
authorized their creation and use in this project. The checked-in PNG files
are the canonical project assets; the transient generator outputs are not
required to build the game.

`NonVisual/Transparent.png` is a programmatically created fully transparent
utility sprite used for narration, system voice, and the unseen guard. It has
no external source material.

## Generation prompt set

Each character first used a full-body anime visual-novel prompt on a flat
`#00F000` chroma-key background. Rei is a white-haired six-year-old in fully
covered, age-appropriate white practical clothing; Nagi is a black-haired
resourceful teenager in a charcoal utility outfit with a wrist terminal; the
Researcher is an adult Japanese woman in a white lab coat. All prompts required
the complete figure inside the frame, no text or scenery, and family-friendly
presentation.

Expression variants edited their character's anchor image while preserving
identity, face structure, hairstyle, clothing, proportions, camera, and canvas.
The edited cues were:

- Rei: blank, determined, frozen, lost, running, serious, shocked, soft,
  surprise, tired
- Nagi: angry, focus, running, serious, shadow, shocked, smile, soft, tired,
  wary
- Researcher: guilty, nervous, neutral

The generated green background was removed with the Codex image-generation
chroma-key helper using border auto-keying, soft matte, despill, and a one-pixel
edge contraction. Every output was then normalized to a transparent
1024 x 1536 canvas.

## Runtime keys and aliases

The canonical Stage keys are `Rei`, `Nagi`, and `Researcher`. Japanese Speaker
aliases (`レイ`, `ナギ`, `研究員`, and `若い研究員`) reference the same Sprite
objects as their canonical definitions. `？？？` resolves to Nagi for the
pre-reveal line. Non-visual Speaker keys use the transparent utility sprite so
CSV character validation remains exhaustive without displaying an unrelated
portrait.

All character textures use a bottom-center pivot, 100 pixels per unit, source
alpha, no mipmaps, clamp wrapping, bilinear filtering, and a maximum texture
size of 2048 through `WhiteRoomCharacterImportSettings`.
