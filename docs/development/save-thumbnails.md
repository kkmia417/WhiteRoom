# Save thumbnails

Japanese counterpart: [日本語版](save-thumbnails.ja.md)

WhiteRoom enables thumbnail capture by default for manual, quick, and autosave.
The save payload is committed first; the thumbnail is a failure-isolated sidecar and
never determines whether that payload can be loaded.

## Capture contract

- Output is a center-cropped `320 x 180` lossless PNG.
- A PNG larger than `512 KiB` is rejected rather than written without a bound.
- The image includes the game stage, dialogue window, and command bar.
- The Save/Load overlay and transient save notification are hidden for capture and
  restored afterward.
- One capture job may run at a time. Another save request is rejected until the job
  completes, preventing sidecars from being assigned to the wrong slot.
- Interactive players capture at the rendered frame end. Headless adapters can use
  the capture-provider seam because Unity has no rendered game frame in batch mode.

After the save payload succeeds, the previous thumbnail is deleted before the new
asynchronous capture starts. If capture, encoding, size validation, or sidecar storage
fails, the new save remains valid and the UI shows a missing-image placeholder instead
of the old image. Slot deletion removes both payload and sidecar. File storage derives
the sidecar path only from the integer slot convention (`slot_<index>.png`).

## Load UI and memory lifecycle

Load pages include the reserved Auto and Quick rows followed by manual slots. Save
pages contain manual slots only. Each row renders one of `Image`, `Missing`, `Corrupt`,
`Empty`, or `Unavailable`; missing or corrupt image bytes never disable an otherwise
loadable save.

`NovelSaveService` caches slot view models and thumbnail bytes, so reopening or
refreshing the overlay does not reread every sidecar synchronously. A visible row
decodes at most one `Texture2D`/`Sprite` for a given byte array and reuses it across
refreshes. Replaced images release the old Unity objects, and controller disposal
releases all remaining row images.
