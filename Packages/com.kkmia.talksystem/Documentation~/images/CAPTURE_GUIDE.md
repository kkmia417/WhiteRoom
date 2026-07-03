# Screenshot & GIF Capture Guide

This folder holds the media referenced by the root `README.md`. The README already
links to the file names below; capture each asset, drop it here with the exact name,
then uncomment the matching `<img>`/`![]()` block in the README.

> Why this is a manual step: these assets are screenshots and screen recordings of the
> live Unity Editor and the running sample. They cannot be generated headlessly — open
> the Editor, follow the shot list, and record.

## Shot list

| File name | What to capture | Source |
| --- | --- | --- |
| `graph-editor.png` | Full Graph Editor window showing the sample dialogue as connected nodes | `Tools/kkmia/Dialogue Graph Editor` with the Feature Tour CSV loaded |
| `graph-editor-node.png` | A single node selected with its inspector/edit fields visible (Speaker, Text, NextId, Choices) | Same window, one node focused |
| `validator.png` | Dialogue Validator window after a run, showing at least one passing and (ideally) one warning row | `Tools/kkmia/Dialogue Validator` |
| `demo-runtime.gif` | The dialogue playing in-game: typewriter text advancing across 2–3 lines | Open `FeatureTour.unity`, press Play |
| `demo-choices.gif` | A choice line appearing and the branch taken after selecting an option | Same sample, reach a row with `Choices` |

Optional extras (nice to have, not required by #23):

| File name | What to capture |
| --- | --- |
| `csv-editor.png` | `Tools/kkmia/Dialogue CSV Editor` editing the sample table |
| `import-export.png` | `Tools/kkmia/Dialogue Import Export` window |

## Recommended capture settings

- **Resolution**: capture at the editor's native size, then downscale so the longest
  edge is ~1280px. Keep PNGs under ~500 KB and GIFs under ~5 MB so the README stays light.
- **Theme**: use the Unity dark theme for consistency with most screenshots online.
- **Framing**: crop tight to the relevant window; avoid capturing the whole desktop.

## Making the GIFs

Record a short MP4/MOV (e.g. Windows Game Bar `Win+Alt+R`, or OBS), keep it to ~4–6
seconds, then convert. Two common options:

Using **ffmpeg** + a palette (good quality, small size):

```bash
ffmpeg -i demo-runtime.mp4 -vf "fps=15,scale=1280:-1:flags=lanczos,palettegen" palette.png
ffmpeg -i demo-runtime.mp4 -i palette.png -vf "fps=15,scale=1280:-1:flags=lanczos,paletteuse" demo-runtime.gif
```

Using **gifski** (often the smallest high-quality output):

```bash
gifski --fps 15 --width 1280 -o demo-runtime.gif demo-runtime.mp4
```

## After capturing

1. Save each file in this folder with the exact name from the shot list.
2. In `README.md`, find the `Screenshots & Demo` section and uncomment the block(s)
   for the assets you added.
3. Update the status column in that section from ⏳ to ✅.
4. Commit the images and the README change together.
