# Tabletop weapon recharger

## Source and placement

Reference: tgstation code/game/machinery/recharger.dm. MetaStation dictionary audit found 19 entries with rechargers, all sharing a tile with a table (docs/ai/art/weapon_recharger_map_audit.json). Eligible under the current rule to skip floor-mounted objects. No existing recharger model found.

## Presets and original layers

Base state recharger is common to every preset. weapon_recharger_off has no overlay (no power, broken or unanchored). empty, charging and full use corresponding recharger-* overlays plus emission. open uses recharger-open with service cover opened and electronics separate; it takes precedence over charging when powered. fail preserves recharger-fail as an auxiliary atlas preset: this state exists in the atlas but is not selected by the inspected update_overlays() code.

The individual empty/full/fail presets each include their two-frame indicator clip. Charging has a ten-frame, one-second clip. Off and open are static. Playback uses the original delays and step interpolation. The overview and shared-geometry library show the static poses. all original frame rectangles, directions and timings are retained in source_states.json. The top green/yellow contact lights sit at the bottom of the actual recessed charging slot, while front indicators stay on the front panel. No inserted weapon is included. Charging_Item_Mount indicates the insertion slot; the source supports weapons, batons, rechargeable magazines and modular computers.

## Geometry and materials

Continuous closed dock shell with lined slot and closed bottom, separate Service_Panel and Service_Electronics. Service_Panel_Hinge uses X, 105 degrees open; this is a 3D interpretation of the source's open-overlay state, not a source animation. Status_Overlay remains separate geometry. No directional duplicates.

Only original res://assets/tg/icons/obj/machines/sec.png. UVs cover top, sides, rear, underside and recess walls. Nearest filtering, flat shading, no derived textures. Material slots Recharger_Original_Atlas (opaque solids) and Recharger_Indicator_Original (emissive, pixel-clipped overlay). The original sprite's top perspective is unfolded onto a horizontal top; its lower rows form the front vertical face.

Dimensions: 0.28125 m width/depth, 0.1125 m height; slot width 0.05625 m, length 0.16875 m, depth 0.05625 m. Origin on support surface, front Blender -Y / Godot +Z. Place at tabletop height 0.75 m for the existing table kit. Open lid extends beyond the front footprint. Solid covers have thickness; no unnecessary glass layer was invented.

## Delivery and verification

Six GLB/blend pairs, shared variants library, preview blend and docs/ai/art/weapon_recharger_preview.png. 80-100 triangles per preset. Solid parts manifold; indicator sheets are intentional. 4,920 opaque UV samples, zero transparent hits or degenerate triangles. Each GLB references the original TG sec.png with nearest filtering. Reviewed front, rear, right, top, underside and three-quarter views. Global validation retains unrelated pre-existing failures; export_validation.json is this batch only. Godot runtime not exercised. Model clips are supplied for runtime state dispatch.
