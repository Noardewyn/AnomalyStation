# Microwave family

21 static presets with editable BLEND and GLB files. Nine standard presets: idle, open, cooking, charging, dirty, dirty_open, dirty_operating, broken and service. Twelve engineering presets: the same nine states plus cell_0, cell_25 and cell_75; normal engineering presets show cell_100. Engineering cell-less and no-power appearances can be obtained by hiding the separate cell/emissive layers. No time/state logic is included.

Source: tgstation code/modules/food_and_drinks/machinery/microwave.dm and original microwave.png with adjacent JSON. 32 x 32 tiles, 288 x 256 atlas. The exact original PNG is referenced in BLEND and embedded in GLB; no derived textures. This object supports tabletop placement (anchored_tabletop_offset = 6 and PASSTABLE).

## Structure and source layers

Standard cabinet has a left-hinged door and right control panel. Engineering cabinet has a wider door, top controls and lower battery/service compartment; this is a different silhouette, not a recolor. Compatible meshes are shared within the variants library. Both have continuous closed cabinets, lined interior cavities, separate chamber backing, closed glass and separate handles. Physical damage is represented by stepped openings with closed glass edges.

Standard main hinge: Blender (-0.2953125,-0.253125,0.05625) m, Z axis, -105 degrees. Engineering hinge: (-0.0140625,-0.253125,0.478125) m, X axis, -110 degrees. Door frames, handles and glass are parented to the hinge. Main-door opening axes and exact 3D depths are interpretations of the source sprite views. Standard service cover hinges at its right edge; engineering service cover folds down from the lower edge. Service recesses have their own original-atlas wiring boards.

Source update_overlays separates chamber back, door, border and handle. Full dirty, broken and maintenance icons bypass ordinary overlays. The model keeps the corresponding appearance as independent physical parts; dirty-open door glass reuses the dirty closed-door patch, since the open sprite compresses that surface. There is no door_open atlas state despite the source reference: the open door is reconstructed using the closed-door surfaces and a real pivot. Ingredients are not included.

Emissive masks (emissive_mw/mwo/mw1 and engineering counterparts) are independent Emission_Overlay sheets. They follow the top controls or front face and use original source pixels. Cell overlays use engi_cell_0/25/75/100, kept separate as Battery_Level_Overlay. Source interval labels are 0 below 25%, 25 for 25-50%, 75 for 50-75%, 100 for 75-100%; shared interval endpoints follow source switch behavior. Source suppresses normal overlays for dirty, broken and maintenance states; those presets omit emissive and battery sheets.

Animated sprite states use their first static frame; source metadata retains all frame rectangles. No animation clips, engine lights, collisions, gameplay or food effects are exported.

## Materials and scale

- Microwave_Original_Atlas: original opaque atlas, flat shading, nearest filtering.
- Microwave_Door_Glass: same atlas, alpha 0.62, roughness 0.25, blended closed thickness 0.0039375 m.
- Microwave_Emission_Overlay: original atlas pixels, separate effect sheets with emission strength 1.0 (presentation value).

All physical sides have atlas UVs. The original top perspective area maps onto the top; sides/rear/underside and chamber lining reuse suitable original trim regions because no independent side/rear sprite exists. No replacement PNGs or flat-color-only materials.

Scale: 0.90 m per 32 pixels. Standard cabinet 0.703125 m wide, 0.50625 m deep, 0.45 m high. Engineering cabinet 0.675 m wide, 0.50625 m deep, 0.478125 m high. Doors/handles extend beyond the cabinet depth. Support-level origin, front Blender -Y / Godot +Z. Place on a 0.75 m table as appropriate.

## Files and verification

Exact triangles, source states and materials: model_info.json. Physical mesh checks: mesh_integrity.json. UV/source-byte checks: export_validation.json. Source evidence: source_audit.json. Godot runtime/import not tested.

Build and export through live Blender MCP: tools/art/build_microwave.py and tools/art/export_microwave.py. The global validator also reports older unrelated treasure_chest, locker_freezer and mini_fridge defects; scoped microwave checks are separate.

Final scoped validation: all 21 exports passed, 25,760 opaque UV samples, zero transparent-source hits and zero degenerate triangles.
