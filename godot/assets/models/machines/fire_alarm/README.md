# Fire alarm

image: `docs/ai/art/fire_alarm_preview.png`.

## Original atlas and states

Only `godot/assets/tg/icons/obj/machines/wallmounts.png` is used. Tiles are 32x32. The visible normal housing occupies x12..20, y12..26 in `fire0`. Original PNG bytes are embedded unchanged in GLB; Blender references the original file. No derived textures, nearest filtering, flat shading.

Checked against tgstation `code/game/machinery/firealarm.dm`, `update_icon_state` / `update_overlays`, and construction constants in `code/__DEFINES/atmospherics/atmos_machinery.dm`.

| Export suffix | Base state | Color overlay | Emission mask |
|---|---|---|---|
| unpowered | fire0 | none | none |
| level_0 through level_3 | fire0 | fire_0 through fire_3 | fire_level_e |
| alerting | fire0 | fire_alerting | fire_alerting_e |
| detection_disabled | fire0 | fire_disabled | fire_level_e |
| offstation | fire0 | fire_offstation | fire_level_e |
| emagged | fire0 | fire_emag | fire_emag_e |
| broken | firex | none in this unpowered broken preset | none |
| frame | fire_b0 | none | none |
| circuit_unwired | fire_b1 | none | none |
| panel_open | fire_b2 | none | none |

The game suppresses overlays when unpowered or when the panel is open. A broken base may still receive a powered overlay in tg; combine the separate overlay with that base as needed. The exported broken preset is unpowered, not a claim that breakage always suppresses overlays.

Overlays remain separate meshes. `Original_Emission_Mask` stores original mask UVs; each overlay pixel selects emissive or unlit material according to that mask, avoiding texture baking. In particular, `fire_alerting_e` frame 0 is empty, so the static first alert frame is colored but unlit. Frames and all four directions are preserved in `source_states.json`; no animation is created. Reuse one model by rotation for other wall directions.

## Geometry and parts

Scale is 0.90/32 m per pixel: ordinary closed asset is 0.225 m wide, 0.39375 m high and 0.1125 m deep including the handle. Origin is the centre of the rear wall mounting face. Blender Z is up, negative Y faces the room. Godot transforms (x,y,z) to (x,z,-y).

- `Continuous_Housing`: closed solid with a lined service recess and solid back. Original front silhouette is retained; the damaged lower edge follows the `firex` alpha boundary. Side/top/rear faces reuse original red casing regions, rather than untextured material colors.
- `Service_Cover_Hinge` and `Service_Cover`: separate cover, hinged about local Z at Blender (-0.084375, -0.07875, 0.05625). Open preset rotates -105 degrees.
- `Indicator_Glass`: separate closed transparent volume, alpha 0.88, attached to the cover.
- `Status_Overlay`: separate color/emission state, attached to the cover.
- `Removable_Circuit_Board`: original `fire_b1` circuit region. Absent from the empty frame.
- `Pull_Handle`: the orange lower region interpreted as a projecting pull handle, consistent with the source description. Pivot is its upper edge, Blender (0, -0.084375, -0.084375), local X axis. All exports leave it at rest; no unsupported latched animation is invented.
- `Service_Wire_Left` / `Right`: closed stepped wires matching the open-state pixels, with orthogonal bridges at diagonal pixel contacts. Their color boundaries are preserved during simplification.

Housing depth, cover hinge placement and the projecting pull handle are 3D interpretations; the atlas itself supplies only an oblique 2D view. Internal circuit and wire details use the real construction states rather than newly drawn circuitry.

## Slots and verification

Materials: `Fire_Alarm_Housing_Original`, `Fire_Alarm_Glass_Original`, `Fire_Alarm_Overlay_Original`, `Fire_Alarm_Overlay_Unlit_Original`. Actual Blender suffixes, triangle counts, source states and part hierarchy are recorded in `model_info.json`. Identical bodies and parts share datablocks across states.

All 13 exports pass original-PNG hash verification, nearest filtering, UV sampling and degenerate-triangle checks: 10,420 opaque UV samples, zero transparent-source hits, 50–182 triangles per variant. `mesh_integrity.json` verifies manifold solid parts; open sheets are intentional status overlays. `source_audit.json` verifies pixel equality against the tgstation DMI. Front, side, rear, top and oblique views, plus open and damaged states, were inspected. The broader project validator still reports unrelated old storage defects outside this batch.

No animations, collisions, lights, audio or gameplay integration. Rebuild via Blender MCP using `tools/art/build_fire_alarm.py`, then `tools/art/export_fire_alarm.py`.
