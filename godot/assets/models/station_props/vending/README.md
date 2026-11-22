# Vending machines

67 appearances, 21 construction families, 29 distinct cabinet meshes. Individual GLB and BLEND files are listed below.

See [classification](CLASSIFICATION.md) for compatible texture families, [mesh reuse](mesh_reuse.json) for actual linked mesh data and [material slots](material_slots.json) for every part and pivot.

## Source and reconstruction

Original `vending.png` and adjacent JSON are unchanged: 800 x 800 atlas, 32 x 32 tiles, 287 named states. `source_catalog.json` records all 67 appearance states resolved from tgstation vending DM inheritance, source rectangles and light masks. Off, broken, deny and animation frames remain in the source atlas; no animations are created.

The upper sprite band is reconstructed as the physical roof. The frontal artwork starts below that band; the right edge strip is excluded from the front projection. Pixel-aligned recesses have real depth and lined walls. All textured surfaces now sample the unchanged original vending atlas. Side and rear UVs reuse opaque strips and panel regions from the same source state. No painted service-panel or replacement textures are used. Material transforms select each appearance's atlas tile.

Scale is 0.06 m per front pixel: common cabinets are 1.20 m wide, 1.56 m tall and 0.66 m deep; wider source silhouettes remain wider. Booze-O-Mat is 1.80 m wide and 0.84 m deep. Wallmed has a shallow 0.30 m body and a 0.90 m mounting offset. Per-model dimensions are in `model_info.json`.

## Materials and moving parts

| Part | Slot | Material / texture |
|---|---:|---|
| Cabinet | 0 | `Vending_Front_*`: original atlas, tile transform, opaque |
| Cabinet | 1 | `Original_Vending_Service_*`: original atlas strips for side, rear, recess and underside |
| Display insert, delivery flap, raised sign | 0 | Original atlas patch |
| Display glass | 0 | Slight cyan tint, alpha 0.15, closed 0.012 m thickness |
| Light overlay sections | 0 | Original base tile plus original light-mask tile as emission; separate geometry, emission 0.25 |

All textured materials use nearest filtering. Surfaces are flat shaded. Display, glass and delivery flaps are separate closed meshes; flap pivots lie on their upper edge. Light sections follow the actual display and delivery depths. Geometry and UV data are shared across compatible appearances in the catalog; object material overrides select the appearance. Standalone GLBs necessarily contain their own geometry.

## Validation

9,180 triangles across 67 machines; range 60–212 per machine. 85,320 opaque UV samples: no transparent pixel hits, collapsed UVs, degenerate triangles or non-manifold edges. Embedded image bytes match the original atlas exactly; nearest samplers were checked independently in every GLB.

The updated atlas-only overview is `docs/ai/art/vending_original_atlas_preview.png`. Earlier inspection renders under `docs/ai/art/quality/vending/` predate the atlas-only migration and are historical. Blender source and exported GLBs are checked; Godot runtime/import has not been verified. No collisions, animations or gameplay integration were added.

## Files

| Source state | Family | GLB | BLEND | Triangles |
|---|---|---|---|---:|
| Cola_Machine | branded_drink | [vending_cola_machine.glb](vending_cola_machine.glb) | [BLEND](vending_cola_machine.blend) | 84 |
| MagiVend | magic | [vending_magivend.glb](vending_magivend.glb) | [BLEND](vending_magivend.blend) | 144 |
| RunicVendor | magic | [vending_runicvendor.glb](vending_runicvendor.glb) | [BLEND](vending_runicvendor.blend) | 168 |
| atmosdrobe | wardrobe | [vending_atmosdrobe.glb](vending_atmosdrobe.glb) | [BLEND](vending_atmosdrobe.blend) | 156 |
| bardrobe | wardrobe | [vending_bardrobe.glb](vending_bardrobe.glb) | [BLEND](vending_bardrobe.blend) | 156 |
| boozeomat | boozeomat | [vending_boozeomat.glb](vending_boozeomat.glb) | [BLEND](vending_boozeomat.blend) | 200 |
| cargodrobe | wardrobe | [vending_cargodrobe.glb](vending_cargodrobe.glb) | [BLEND](vending_cargodrobe.blend) | 156 |
| cart | wide_service | [vending_cart.glb](vending_cart.glb) | [BLEND](vending_cart.blend) | 132 |
| centdrobe | wardrobe | [vending_centdrobe.glb](vending_centdrobe.glb) | [BLEND](vending_centdrobe.blend) | 156 |
| chapdrobe | wardrobe | [vending_chapdrobe.glb](vending_chapdrobe.glb) | [BLEND](vending_chapdrobe.blend) | 156 |
| chefdrobe | wardrobe | [vending_chefdrobe.glb](vending_chefdrobe.glb) | [BLEND](vending_chefdrobe.blend) | 156 |
| chemdrobe | wardrobe | [vending_chemdrobe.glb](vending_chemdrobe.glb) | [BLEND](vending_chemdrobe.blend) | 156 |
| cigs | compact_product | [vending_cigs.glb](vending_cigs.glb) | [BLEND](vending_cigs.blend) | 128 |
| clothes | wardrobe | [vending_clothes.glb](vending_clothes.glb) | [BLEND](vending_clothes.blend) | 156 |
| coffee | coffee | [vending_coffee.glb](vending_coffee.glb) | [BLEND](vending_coffee.blend) | 168 |
| cola_black | branded_drink | [vending_cola_black.glb](vending_cola_black.glb) | [BLEND](vending_cola_black.blend) | 84 |
| coroner_drobe | wardrobe | [vending_coroner_drobe.glb](vending_coroner_drobe.glb) | [BLEND](vending_coroner_drobe.blend) | 156 |
| curadrobe | wardrobe | [vending_curadrobe.glb](vending_curadrobe.glb) | [BLEND](vending_curadrobe.blend) | 156 |
| custom | custom | [vending_custom.glb](vending_custom.glb) | [BLEND](vending_custom.blend) | 128 |
| cytopro | cytopro | [vending_cytopro.glb](vending_cytopro.glb) | [BLEND](vending_cytopro.blend) | 132 |
| detdrobe | wardrobe | [vending_detdrobe.glb](vending_detdrobe.glb) | [BLEND](vending_detdrobe.blend) | 156 |
| dinnerware | dinnerware | [vending_dinnerware.glb](vending_dinnerware.glb) | [BLEND](vending_dinnerware.blend) | 120 |
| drug | wide_medical | [vending_drug.glb](vending_drug.glb) | [BLEND](vending_drug.blend) | 96 |
| engi | engi | [vending_engi.glb](vending_engi.glb) | [BLEND](vending_engi.blend) | 152 |
| engidrobe | wardrobe | [vending_engidrobe.glb](vending_engidrobe.glb) | [BLEND](vending_engidrobe.blend) | 156 |
| engivend | industrial | [vending_engivend.glb](vending_engivend.glb) | [BLEND](vending_engivend.blend) | 132 |
| games | compact_product | [vending_games.glb](vending_games.glb) | [BLEND](vending_games.blend) | 192 |
| genedrobe | wardrobe | [vending_genedrobe.glb](vending_genedrobe.glb) | [BLEND](vending_genedrobe.blend) | 156 |
| generic | compact_product | [vending_generic.glb](vending_generic.glb) | [BLEND](vending_generic.blend) | 168 |
| greed | custom | [vending_greed.glb](vending_greed.glb) | [BLEND](vending_greed.blend) | 132 |
| hotdog-vendor | hotdog | [vending_hotdog_vendor.glb](vending_hotdog_vendor.glb) | [BLEND](vending_hotdog_vendor.blend) | 212 |
| hydrobe | wardrobe | [vending_hydrobe.glb](vending_hydrobe.glb) | [BLEND](vending_hydrobe.blend) | 156 |
| janidrobe | wardrobe | [vending_janidrobe.glb](vending_janidrobe.glb) | [BLEND](vending_janidrobe.blend) | 156 |
| lawdrobe | wardrobe | [vending_lawdrobe.glb](vending_lawdrobe.glb) | [BLEND](vending_lawdrobe.blend) | 156 |
| liberationstation | wide_food | [vending_liberationstation.glb](vending_liberationstation.glb) | [BLEND](vending_liberationstation.blend) | 84 |
| med | wide_medical | [vending_med.glb](vending_med.glb) | [BLEND](vending_med.blend) | 132 |
| medidrobe | wardrobe | [vending_medidrobe.glb](vending_medidrobe.glb) | [BLEND](vending_medidrobe.blend) | 156 |
| modularpc | wide_electronics | [vending_modularpc.glb](vending_modularpc.glb) | [BLEND](vending_modularpc.blend) | 192 |
| nt-donk | wide_food | [vending_nt_donk.glb](vending_nt_donk.glb) | [BLEND](vending_nt_donk.blend) | 72 |
| nutri | snack | [vending_nutri.glb](vending_nutri.glb) | [BLEND](vending_nutri.blend) | 120 |
| parts | compact_product | [vending_parts.glb](vending_parts.glb) | [BLEND](vending_parts.blend) | 128 |
| pwr_game | branded_drink | [vending_pwr_game.glb](vending_pwr_game.glb) | [BLEND](vending_pwr_game.blend) | 84 |
| red_cola | branded_drink | [vending_red_cola.glb](vending_red_cola.glb) | [BLEND](vending_red_cola.blend) | 84 |
| robodrobe | wardrobe | [vending_robodrobe.glb](vending_robodrobe.glb) | [BLEND](vending_robodrobe.blend) | 156 |
| robotics | wide_electronics | [vending_robotics.glb](vending_robotics.glb) | [BLEND](vending_robotics.blend) | 168 |
| scidrobe | wardrobe | [vending_scidrobe.glb](vending_scidrobe.glb) | [BLEND](vending_scidrobe.blend) | 156 |
| sec | wide_service | [vending_sec.glb](vending_sec.glb) | [BLEND](vending_sec.blend) | 132 |
| secdrobe | wardrobe | [vending_secdrobe.glb](vending_secdrobe.glb) | [BLEND](vending_secdrobe.blend) | 156 |
| seeds | snack | [vending_seeds.glb](vending_seeds.glb) | [BLEND](vending_seeds.blend) | 108 |
| shamblers_juice | branded_drink | [vending_shamblers_juice.glb](vending_shamblers_juice.glb) | [BLEND](vending_shamblers_juice.blend) | 84 |
| snack | snack | [vending_snack.glb](vending_snack.glb) | [BLEND](vending_snack.blend) | 120 |
| snackblue | snack | [vending_snackblue.glb](vending_snackblue.glb) | [BLEND](vending_snackblue.blend) | 120 |
| snackdonk | wide_food | [vending_snackdonk.glb](vending_snackdonk.glb) | [BLEND](vending_snackdonk.blend) | 72 |
| snackgreen | snack | [vending_snackgreen.glb](vending_snackgreen.glb) | [BLEND](vending_snackgreen.blend) | 120 |
| snackorange | snack | [vending_snackorange.glb](vending_snackorange.glb) | [BLEND](vending_snackorange.blend) | 120 |
| snackteal | snack | [vending_snackteal.glb](vending_snackteal.glb) | [BLEND](vending_snackteal.blend) | 120 |
| soda | drink_display | [vending_soda.glb](vending_soda.glb) | [BLEND](vending_soda.blend) | 164 |
| sovietsoda | sovietsoda | [vending_sovietsoda.glb](vending_sovietsoda.glb) | [BLEND](vending_sovietsoda.blend) | 180 |
| space_up | branded_drink | [vending_space_up.glb](vending_space_up.glb) | [BLEND](vending_space_up.blend) | 84 |
| starkist | drink_display | [vending_starkist.glb](vending_starkist.glb) | [BLEND](vending_starkist.blend) | 128 |
| sustenance | snack | [vending_sustenance.glb](vending_sustenance.glb) | [BLEND](vending_sustenance.blend) | 120 |
| sustenance_labor | snack | [vending_sustenance_labor.glb](vending_sustenance_labor.glb) | [BLEND](vending_sustenance_labor.blend) | 120 |
| syndi | wide_food | [vending_syndi.glb](vending_syndi.glb) | [BLEND](vending_syndi.blend) | 72 |
| theater | theater | [vending_theater.glb](vending_theater.glb) | [BLEND](vending_theater.blend) | 180 |
| tool | industrial | [vending_tool.glb](vending_tool.glb) | [BLEND](vending_tool.blend) | 60 |
| virodrobe | wardrobe | [vending_virodrobe.glb](vending_virodrobe.glb) | [BLEND](vending_virodrobe.blend) | 156 |
| wallmed | wall_medical | [vending_wallmed.glb](vending_wallmed.glb) | [BLEND](vending_wallmed.blend) | 164 |
