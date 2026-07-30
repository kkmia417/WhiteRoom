# WhiteRoom background assets

The production dialogue backgrounds under `Assets/Presentation/Backgrounds/` use a
single 1920 x 1080 (16:9) canvas. Unity imports them as single sprites with bilinear
filtering, clamp wrapping, no mipmaps, high-quality compression, and a 2048 maximum
texture size.

At runtime, the background `Image` preserves the sprite aspect ratio. A 16:9 display
fills exactly; wider or narrower displays use letterboxing rather than stretching the
environment. Important composition remains inside the centered 16:9 safe area.

## Source and permission

All fourteen images were generated specifically for WhiteRoom on 2026-07-30 with
OpenAI's built-in image generation tool. They are original project outputs and do not
incorporate external stock, third-party game, or trademarked assets. They may be used
and modified as part of WhiteRoom; reuse outside WhiteRoom requires permission from
the repository owner. OpenAI service terms also apply to the generated outputs.

The shared art brief was: cinematic semi-realistic 2D visual-novel environment art,
an underground institutional sci-fi escape thriller, empty environments, 16:9,
character-safe foreground space, and no people, text, logos, trademarks, or
watermarks. `lab_room_night` and `lab_room_alarm` were lighting edits derived from
the generated `lab_room_white` composition; every other image was independently
generated from a scene-specific prompt.

## Catalog

| CSV key | File | Scene/source note |
| --- | --- | --- |
| `back_corridor` | `back_corridor.png` | Rear laboratory service corridor |
| `drain_dark` | `drain_dark.png` | Abandoned concrete drainage channel |
| `duct_dark` | `duct_dark.png` | Dark branching ventilation passage |
| `duct_entry` | `duct_entry.png` | Concealed wall-panel duct entrance |
| `duct_inner` | `duct_inner.png` | Furnace-side inner ventilation duct |
| `furnace_gate` | `furnace_gate.png` | Furnace loading gate and escape opening |
| `lab_room_alarm` | `lab_room_alarm.png` | Red security-alarm lighting edit |
| `lab_room_night` | `lab_room_night.png` | Night/power-saving lighting edit |
| `lab_room_white` | `lab_room_white.png` | Sterile containment laboratory master |
| `maintenance_corridor` | `maintenance_corridor.png` | Deep industrial maintenance passage |
| `outside_wall_night` | `outside_wall_night.png` | Secured exterior facility wall at night |
| `soft_cell` | `soft_cell.png` | Soft-walled containment cell |
| `stairwell_down` | `stairwell_down.png` | Stairwell descending to machinery levels |
| `waste_furnace` | `waste_furnace.png` | Waste-processing furnace chamber |
