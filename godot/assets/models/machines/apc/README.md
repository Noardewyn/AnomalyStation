# APC — original-atlas power controller

19 GLB presets and matching editable Blender files, with shared geometry in
`docs/ai/art/apc_preview.png`. No animations, collisions or gameplay integration.

## Source and dimensions

Only the untouched `res://assets/tg/icons/obj/machines/wallmounts.png` is used.
The 640 × 608 atlas has 32 × 32 tiles. PNG pixels match the original
`tgstation/icons/obj/machines/wallmounts.dmi`; see `source_audit.json`.
UVs address that atlas directly, with nearest filtering and flat shading.
Backs, sides and cavity liners reuse opaque original trim and panel pixels;
these hidden views are reconstructions, not additional source artwork.

Scale: 0.90 / 32 m per pixel. Closed body width 0.478125 m, height 0.39375 m,
depth approximately 0.125 m. Origin is the wall-mount reference at sprite
(16.5, 16); back plane Blender Y = 0. Front faces Blender -Y / Godot +Z.
Rotate one reusable model to face each wall.

## Source behavior and presets

Reference: `code/modules/power/apc/apc_appearance.dm` and `apc_tool_act.dm`.
`apc1` is open without a cell; `apc2` is open with a cell. The lower wire panel
is independently accessible. `-nocover` states omit both cover assemblies and
the lower extension, matching their original silhouette.

| File suffix (`apc_`) | Base | Overlays |
|---|---|---|
| closed_base | apc0 | none; maintenance/disabled appearance |
| charging | apc0 | locked, charging, all channels auto-on |
| charged | apc0 | locked, charged, all channels auto-on |
| not_charging | apc0 | locked, not charging, all channels auto-on |
| unlocked | apc0 | unlocked, charged, all channels auto-on |
| channels_off | apc0 | locked, not charging, all channels off |
| channels_auto_off | apc0 | locked, not charging, all channels auto-off |
| channels_manual_on | apc0 | locked, charged, all channels on |
| breaker_off | apc0 | lock and charge only |
| open_cell / open_empty | apc2 / apc1 | none |
| cover_removed_cell / cover_removed_empty | apc2-nocover / apc1-nocover | none |
| maintenance | apcmaint | none; no cell or removable control board |
| wiring_open | apcewires | none |
| broken | apc-b | none; damaged cover and hanging wiring |
| broken_removed_cell / broken_removed_empty | apc2-b-nocover / apc1-b-nocover | none |
| hacked_flicker | apc0 | apcemag, first frame only |

Lock (`apcox-0/1`), charge (`apco3-0/1/2`) and equipment, lighting and environment
(`apco0/1/2-0/1/2/3`) remain separate overlay meshes. Channel values are off,
auto-off, on and auto-on. Mix these layers rather than duplicating geometry for
every combination. Broken, maintenance and open/panel states suppress overlays;
when the breaker is off, only lock and charge overlays remain.
All source frames and rectangles are recorded in `source_states.json`.
Animated charge and hacking textures use frame zero. `apc-spark` is an effect
sequence retained as metadata, not an animated mesh. `apc` is not selected by
the inspected normal appearance procedure.

## Parts and slots

- `Lined_Enclosure`: continuous closed shell, cavity walls and hinge supports.
- `Interior_Backplate`, `Removable_Control_Board`, `Removable_Power_Cell`:
  separate solids, with texture on every face.
- `Main_Cover`, `Display_Back`, `Display_Glass`: children of
  `Main_Cover_Hinge`; local X axis, pivot (-0.2390625, -0.11390625, 0.196875) m
  in Blender. Open preset: -165 degrees.
- `Lower_Control_Housing`, `Wire_Terminal_Block`, `Wiring_Cover`:
  lower access; `Wiring_Cover_Hinge` local X pivot
  (-0.1828125, -0.1125, -0.196875) m; open preset: +110 degrees.
- Named lock, charge and three channel overlay meshes, separately replaceable.

Each part uses one material slot: `APC_Housing_Original` (opaque),
`APC_Display_Glass_Original` (original tint, alpha 0.94, closed thickness), or
`APC_Overlay_Original` (source alpha mask, emission strength 1).

## Validation

`mesh_integrity.json`: every solid is manifold and flat-shaded; overlay sheets
are intentionally open. `export_validation.json`: original embedded PNG bytes,
nearest samplers, no degenerate triangles and no sampled transparent pixels on
opaque surfaces. Presets have 60–266 triangles. The closed and open assemblies
were inspected from front, rear, side, top and angled views. Godot runtime was
not run. Build through Blender MCP with `tools/art/build_apc.py`, followed by
`tools/art/export_apc.py`.
