# Tabletop fax

13 static presets with matching GLB and editable BLEND: idle, idle_alternate, service, sending_paper, receiving_paper and loaded paper/photo/food/star/id/tcg/cash/pbiscuit. No previous fax model was found.

Source: code/modules/paperwork/fax.dm and original fax.png / JSON. Atlas 480 x 448, tiles 32 x 32. PNG RGBA matches the tgstation DMI; exact PNG bytes are embedded in GLB. No derived texture. Tabletop placement is explicit through anchored_tabletop_offset=6 and PASSTABLE. Administrative and department variants use the same appearance.

Source update_overlays adds fax_panel when servicing and contain_* for the loaded item. All eight contain families are preserved as separate thin closed silhouettes using their original pixels; these are appearance layers, not standalone detailed food/card/item assets. The paper feeding/receiving samples use overlay frame 5. Both source send and receive paths currently call fax_receive, so these examples use its frame 12. The unused fax_send and other item transition frames remain recorded in source_states.json. Idle has two frames, exported separately. No animation clips or transport logic.

Continuous stepped housing; real lined output slot, separate raised handset, solid rear paper guide with physical supports (inner opening closed on request), and a shallow output tray. Service cover has its own hinge at Blender (0.3375,-0.3375,0.05625) m, opened 105 degrees around Z; wiring board is separate. This hinge and hidden depths are 3D interpretations of the source. Handset is separate for editing; source does not implement removing/using it. The screen and keypad are original base texture regions on the upper deck, not invented overlays.

Scale 0.90 m / 32 pixels. Main housing width 0.7875 m, depth 0.5625 m, upper deck height 0.225 m; guide reaches 0.421875 m, loaded paper up to 0.50625 m. Tray extends forward. Support-plane origin, front Blender -Y / Godot +Z; place on 0.75 m table.

All meshes use material slot 0: Fax_Original_Atlas, opaque, flat shaded, nearest filtering. Top artwork is on upper surfaces; side/rear/underside use original opaque trim regions. There are no new PNGs, flat-color-only materials or transparent housing faces. Papers and overlay silhouettes have 0.003375 m thickness.

Every physical part is closed/manifold. Multi-angle material review completed. All 13 GLBs passed original PNG identity, nearest sampler, opaque UV and degenerate-triangle checks; exact counts in export_validation.json and model_info.json. Godot runtime/import not tested. No animation, collision or gameplay integration. Global validator still flags unrelated older treasure_chest, locker_freezer and mini_fridge models.

Build/export: tools/art/build_fax.py and tools/art/export_fax.py via live Blender MCP. PNG: docs/ai/art/fax_preview.png.

Opacity/contact revision (2026-11-06): all 13 presets and both Blender libraries updated through live Blender MCP. Underside UVs use an opaque original tray patch; paper-guide UVs avoid transparent silhouette pixels. Housing materials export OPAQUE with nearest sampling and the canonical TG fax.png URI. Guide contact edges now embed into the housing without coplanar faces; loaded-item silhouettes have a deliberate 4.5 mm separation from the guide. All 13 geometry audits report closed meshes and zero same-facing coplanar overlaps. Bottom/front/rear and loaded/service appearances reviewed in Blender. Backups and audit images are offrepo.
