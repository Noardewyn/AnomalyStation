# Wall-mounted keycard authentication reader

Two static presets: keycard_auth_wall_idle and keycard_auth_wall_confirmation.
Each includes an editable BLEND and Godot GLB. Shared geometry library:

Source: tgstation code/modules/security_levels/keycard_authentication.dm.
All eight MetaStation map records are wall_mounted variants, using wallmounts.dmi.
The priority table previously listed the tabletop atlas; that reference is corrected.
No previous matching model was found. Tabletop geometry is outside this batch.

Textures use the unmodified original godot/assets/tg/icons/obj/machines/wallmounts.png
(640 x 608; 32 x 32 tiles). Original PNG pixels match the DMI and GLB embeds the
original PNG bytes. No derived texture. Closest/nearest filtering, flat shading.

auth_off is the base at (544,64). auth_on has two frames: frame 0 is empty;
frame 1 supplies the blue confirmation light. Source adds this overlay and
its emissive appearance only with event_source and without NOPOWER/BROKEN.
The confirmation preset samples frame 1. The dark frame, unpowered and broken
appearances reuse idle. There are no animation clips or gameplay implementations.

The continuous closed housing has recessed display and card-swipe details,
lined with original atlas regions. All sides, top, bottom and rear are textured.
Depth is a 3D interpretation; no separate glass or invented removable controls.
Physical dimensions: 0.365625 m wide, 0.28125 m high, 0.084375 m deep.
Scale: 0.90 m / 32 pixels. Origin at rear center, front Blender -Y / Godot +Z.
Rotate the same model for directional placement.

Materials: housing slot 0 Keycard_Reader_Original_Atlas (opaque).
Confirmation layer slot 0 Keycard_Confirmation_Overlay (alpha blend, emission).
The original half-alpha halo is retained. The overlay consists of pixel-aligned
surface patches following the recessed face. Only this effect layer is open;
the physical housing is closed/manifold and shares one mesh between presets.

56 triangles idle; 138 with confirmation effect. Both exports passed original
PNG identity, nearest sampler, opaque UV and degenerate-triangle checks.
See export_validation.json and mesh_integrity.json. Front, side, top and rear
were visually reviewed. Godot runtime/import was not tested.
Unrelated legacy models still fail the global validation; this family passes.

Preview PNG: docs/ai/art/keycard_auth_preview.png.
Build/export: tools/art/build_keycard_auth.py and export_keycard_auth.py
executed through Blender Lab MCP in Blender 5.2.2 LTS.
