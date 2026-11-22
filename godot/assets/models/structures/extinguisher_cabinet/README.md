# Extinguisher cabinet

Original atlas: `res://assets/tg/icons/obj/wallmounts.png` (288 × 288,
32 × 32 tiles). Its pixels match `tgstation/icons/obj/wallmounts.dmi` exactly.
The PNG is unchanged, referenced directly by Blender and embedded unchanged
in GLB. No derived textures. Nearest filtering and flat shading throughout.

## Files and states

`extinguisher_cabinet_{empty,default,advanced,mini,crafted}_{closed,open}`:
ten presets, each in GLB and Blender format. `extinguisher_cabinet_frame`
is the construction frame without its door. The variants library shares
identical geometry. The preview scene and PNG show all four extinguisher types.

Source behavior: `tgstation/code/game/objects/structures/extinguisher.dm`,
`update_icon_state`, `update_overlays`, `atom_break`, and item interactions.
The base state is `extinguisher` or the matching `_default`, `_advanced`,
`_mini`, `_crafted` state. The door is an independent `door` / `door_open`
overlay in tg; here the same physical door rotates on its right hinge.
Breaking the cabinet opens it and ejects its contents. Reuse `empty_open`:
there is no distinct damaged source texture. The construction item reuses the
empty base without a door. Frames and rectangles are in `source_states.json`.

## Geometry

0.90 / 32 m per pixel; outer body 0.3375 m wide, 0.478125 m high,
0.16875 m deep. Closed handle reaches 0.196875 m from the back plane.
The source's top perspective strip becomes the horizontal roof surface.
Front faces Blender -Y / Godot +Z. Rotate the reusable assembly for walls.

`Lined_Cabinet` is a single continuous closed mesh with cavity walls.
`Bottom_Cradle` and `Mounting_Clip` parts support the removable contents.
Rear and side surfaces reuse original outer trim pixels, carrying the red
band around the perimeter. Hidden views are reconstructions from the atlas.

`Door_Frame`, `Window_Glass`, and `Door_Handle` belong to `Door_Hinge`.
Hinge pivot in Blender: (0.16875, -0.17015625, -0.05625) m, local Z axis;
the open preset is +105 degrees. The glass has closed 0.00421875 m thickness.
The opening is lined by the frame; the door has texture on both sides.

`Removable_Extinguisher` groups the stepped cylindrical body, valve, trigger
and hose. Its pivot is at the base of the cylinder. `Trigger_Handle` has its
own lever pivot and local X rotation axis. Default and advanced variants share
shape; the smaller mini has a different height. Cylinder cross-sections follow
pixel steps, and all perimeter faces have original-atlas UVs.

## Material slots and checks

Each part has one slot: `Cabinet_Original` (opaque) or
`Cabinet_Glass_Original` (original gray tint, alpha 0.18). Only the glass is
transparent. No animations, collisions or gameplay integration were added.

11 presets, 72–264 triangles each. All solids are manifold and flat-shaded.
Export checks sample 23,520 opaque UV points with zero transparent-pixel hits;
no degenerate triangles. Original embedded image bytes and nearest samplers
are verified. See `mesh_integrity.json`, `export_validation.json` and
`source_audit.json`. Inspected front, back, side, top and angled views in Blender;
Godot runtime was not run.

Build through Blender MCP: `tools/art/build_extinguisher_cabinet.py`, then
`tools/art/export_extinguisher_cabinet.py`. Preview PNG:
`docs/ai/art/extinguisher_cabinet_preview.png`.
