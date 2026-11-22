# Station prop assets

Individual GLBs and editable BLENDs are authoritative. Superseded aggregate libraries, intermediate saves and temporary backups have been removed. Source sprite PNGs are unchanged.

## Current entry points

  model is [canister](canister/README.md) (2026-10-20); the generator scripts (`tools/art/build_station_props.py` and its revisions) still hold the old
  textured variants, so running them again writes those files back, unused by the catalog.
- [Storage rack](../furniture/storage_rack/storage_rack.blend): rebuilt connected rack with its own validation and preview.

Open `project.godot` for the independent machinery/tank preview. `previews/station_props_preview.tscn` also references the individual current GLBs. Godot import/runtime has not been verified in this art pass.

The moved rack and stationary tanks are registered in `../models.json`; their paths were updated during cleanup; the rack was subsequently rebuilt. Stationary tank geometry remains unchanged. `source_audit.json` preserves original source metadata. `export_validation.json` and `material_slots_and_mesh_health.json` include the current vending and machinery batches. Rack checks are in its own folder; stationary tanks retain their original geometry.

## Model files

| Model | GLB | Editable source |
|---|---|---|
| storage_rack | [storage_rack.glb](../furniture/storage_rack/storage_rack.glb) | [BLEND](../furniture/storage_rack/storage_rack.blend) |
| solar_panel | [solar_panel.glb](power/solar_panel.glb) | [BLEND](power/solar_panel.blend) |
| solar_panel_frame | [solar_panel_frame.glb](power/solar_panel_frame.glb) | [BLEND](power/solar_panel_frame.blend) |
| holopad | [holopad.glb](machines/holopad.glb) | [BLEND](machines/holopad.blend) |
| holopad_active | [holopad_active.glb](machines/holopad_active.glb) | [BLEND](machines/holopad_active.blend) |
| vending_atmosdrobe | [vending_atmosdrobe.glb](vending/vending_atmosdrobe.glb) | [BLEND](vending/vending_atmosdrobe.blend) |
| vending_bardrobe | [vending_bardrobe.glb](vending/vending_bardrobe.glb) | [BLEND](vending/vending_bardrobe.blend) |
| vending_boozeomat | [vending_boozeomat.glb](vending/vending_boozeomat.glb) | [BLEND](vending/vending_boozeomat.blend) |
| vending_cargodrobe | [vending_cargodrobe.glb](vending/vending_cargodrobe.glb) | [BLEND](vending/vending_cargodrobe.blend) |
| vending_cart | [vending_cart.glb](vending/vending_cart.glb) | [BLEND](vending/vending_cart.blend) |
| vending_chapdrobe | [vending_chapdrobe.glb](vending/vending_chapdrobe.glb) | [BLEND](vending/vending_chapdrobe.blend) |
| vending_chefdrobe | [vending_chefdrobe.glb](vending/vending_chefdrobe.glb) | [BLEND](vending/vending_chefdrobe.blend) |
| vending_chemdrobe | [vending_chemdrobe.glb](vending/vending_chemdrobe.glb) | [BLEND](vending/vending_chemdrobe.blend) |
| vending_cigs | [vending_cigs.glb](vending/vending_cigs.glb) | [BLEND](vending/vending_cigs.blend) |
| vending_clothes | [vending_clothes.glb](vending/vending_clothes.glb) | [BLEND](vending/vending_clothes.blend) |
| vending_coffee | [vending_coffee.glb](vending/vending_coffee.glb) | [BLEND](vending/vending_coffee.blend) |
| vending_coroner_drobe | [vending_coroner_drobe.glb](vending/vending_coroner_drobe.glb) | [BLEND](vending/vending_coroner_drobe.blend) |
| vending_curadrobe | [vending_curadrobe.glb](vending/vending_curadrobe.glb) | [BLEND](vending/vending_curadrobe.blend) |
| vending_cytopro | [vending_cytopro.glb](vending/vending_cytopro.glb) | [BLEND](vending/vending_cytopro.blend) |
| vending_detdrobe | [vending_detdrobe.glb](vending/vending_detdrobe.glb) | [BLEND](vending/vending_detdrobe.blend) |
| vending_dinnerware | [vending_dinnerware.glb](vending/vending_dinnerware.glb) | [BLEND](vending/vending_dinnerware.blend) |
| vending_drug | [vending_drug.glb](vending/vending_drug.glb) | [BLEND](vending/vending_drug.blend) |
| vending_engidrobe | [vending_engidrobe.glb](vending/vending_engidrobe.glb) | [BLEND](vending/vending_engidrobe.blend) |
| vending_engivend | [vending_engivend.glb](vending/vending_engivend.glb) | [BLEND](vending/vending_engivend.blend) |
| vending_games | [vending_games.glb](vending/vending_games.glb) | [BLEND](vending/vending_games.blend) |
| vending_genedrobe | [vending_genedrobe.glb](vending/vending_genedrobe.glb) | [BLEND](vending/vending_genedrobe.blend) |
| vending_hydrobe | [vending_hydrobe.glb](vending/vending_hydrobe.glb) | [BLEND](vending/vending_hydrobe.blend) |
| vending_janidrobe | [vending_janidrobe.glb](vending/vending_janidrobe.glb) | [BLEND](vending/vending_janidrobe.blend) |
| vending_lawdrobe | [vending_lawdrobe.glb](vending/vending_lawdrobe.glb) | [BLEND](vending/vending_lawdrobe.blend) |
| vending_med | [vending_med.glb](vending/vending_med.glb) | [BLEND](vending/vending_med.blend) |
| vending_medidrobe | [vending_medidrobe.glb](vending/vending_medidrobe.glb) | [BLEND](vending/vending_medidrobe.blend) |
| vending_modularpc | [vending_modularpc.glb](vending/vending_modularpc.glb) | [BLEND](vending/vending_modularpc.blend) |
| vending_nutri | [vending_nutri.glb](vending/vending_nutri.glb) | [BLEND](vending/vending_nutri.blend) |
| vending_parts | [vending_parts.glb](vending/vending_parts.glb) | [BLEND](vending/vending_parts.blend) |
| vending_red_cola | [vending_red_cola.glb](vending/vending_red_cola.glb) | [BLEND](vending/vending_red_cola.blend) |
| vending_robodrobe | [vending_robodrobe.glb](vending/vending_robodrobe.glb) | [BLEND](vending/vending_robodrobe.blend) |
| vending_scidrobe | [vending_scidrobe.glb](vending/vending_scidrobe.glb) | [BLEND](vending/vending_scidrobe.blend) |
| vending_sec | [vending_sec.glb](vending/vending_sec.glb) | [BLEND](vending/vending_sec.blend) |
| vending_secdrobe | [vending_secdrobe.glb](vending/vending_secdrobe.glb) | [BLEND](vending/vending_secdrobe.blend) |
| vending_seeds | [vending_seeds.glb](vending/vending_seeds.glb) | [BLEND](vending/vending_seeds.blend) |
| vending_sustenance | [vending_sustenance.glb](vending/vending_sustenance.glb) | [BLEND](vending/vending_sustenance.blend) |
| vending_theater | [vending_theater.glb](vending/vending_theater.glb) | [BLEND](vending/vending_theater.blend) |
| vending_tool | [vending_tool.glb](vending/vending_tool.glb) | [BLEND](vending/vending_tool.blend) |
| vending_virodrobe | [vending_virodrobe.glb](vending/vending_virodrobe.glb) | [BLEND](vending/vending_virodrobe.blend) |
| disposal_unit | [disposal_unit.glb](utility/disposal_unit.glb) | [BLEND](utility/disposal_unit.blend) |
| washbasin | [washbasin.glb](bathroom/washbasin.glb) | [BLEND](bathroom/washbasin.blend) |
| kitchen_sink | [kitchen_sink.glb](bathroom/kitchen_sink.glb) | [BLEND](bathroom/kitchen_sink.blend) |
| wall_shower | [wall_shower.glb](bathroom/wall_shower.glb) | [BLEND](bathroom/wall_shower.blend) |
| toilet | [toilet.glb](bathroom/toilet.glb) | [BLEND](bathroom/toilet.blend) |
| urinal | [urinal.glb](bathroom/urinal.glb) | [BLEND](bathroom/urinal.blend) |
| wall_mirror | [wall_mirror.glb](bathroom/wall_mirror.glb) | [BLEND](bathroom/wall_mirror.blend) |
| chemical_tanks (eight bodies + mixture overlay) | [family](chemical_tanks/README.md) | [BLEND](chemical_tanks/chemical_tanks.blend) |
| station_bed | [station_bed.glb](medical/station_bed.glb) | [BLEND](medical/station_bed.blend) |
| emergency_stretcher | [emergency_stretcher.glb](medical/emergency_stretcher.glb) | [BLEND](medical/emergency_stretcher.blend) |
| hydroponics_tray | [hydroponics_tray.glb](hydroponics/hydroponics_tray.glb) | [BLEND](hydroponics/hydroponics_tray.blend) |
| soil_bed | [soil_bed.glb](hydroponics/soil_bed.glb) | [BLEND](hydroponics/soil_bed.blend) |
| suit_storage_classic | [suit_storage_classic.glb](machines/suit_storage_classic.glb) | [BLEND](machines/suit_storage_classic.blend) |
| pacman_generator | [pacman_generator.glb](power/pacman_generator.glb) | [BLEND](power/pacman_generator.blend) |
| old_cyborg_showcase | [old_cyborg_showcase.glb](showcase/old_cyborg_showcase.glb) | [BLEND](showcase/old_cyborg_showcase.blend) |
| vending_cola_machine | [GLB](vending/vending_cola_machine.glb) | [BLEND](vending/vending_cola_machine.blend) |
| vending_magivend | [GLB](vending/vending_magivend.glb) | [BLEND](vending/vending_magivend.blend) |
| vending_runicvendor | [GLB](vending/vending_runicvendor.glb) | [BLEND](vending/vending_runicvendor.blend) |
| vending_centdrobe | [GLB](vending/vending_centdrobe.glb) | [BLEND](vending/vending_centdrobe.blend) |
| vending_cola_black | [GLB](vending/vending_cola_black.glb) | [BLEND](vending/vending_cola_black.blend) |
| vending_custom | [GLB](vending/vending_custom.glb) | [BLEND](vending/vending_custom.blend) |
| vending_engi | [GLB](vending/vending_engi.glb) | [BLEND](vending/vending_engi.blend) |
| vending_generic | [GLB](vending/vending_generic.glb) | [BLEND](vending/vending_generic.blend) |
| vending_greed | [GLB](vending/vending_greed.glb) | [BLEND](vending/vending_greed.blend) |
| vending_hotdog_vendor | [GLB](vending/vending_hotdog_vendor.glb) | [BLEND](vending/vending_hotdog_vendor.blend) |
| vending_liberationstation | [GLB](vending/vending_liberationstation.glb) | [BLEND](vending/vending_liberationstation.blend) |
| vending_nt_donk | [GLB](vending/vending_nt_donk.glb) | [BLEND](vending/vending_nt_donk.blend) |
| vending_pwr_game | [GLB](vending/vending_pwr_game.glb) | [BLEND](vending/vending_pwr_game.blend) |
| vending_robotics | [GLB](vending/vending_robotics.glb) | [BLEND](vending/vending_robotics.blend) |
| vending_shamblers_juice | [GLB](vending/vending_shamblers_juice.glb) | [BLEND](vending/vending_shamblers_juice.blend) |
| vending_snack | [GLB](vending/vending_snack.glb) | [BLEND](vending/vending_snack.blend) |
| vending_snackblue | [GLB](vending/vending_snackblue.glb) | [BLEND](vending/vending_snackblue.blend) |
| vending_snackdonk | [GLB](vending/vending_snackdonk.glb) | [BLEND](vending/vending_snackdonk.blend) |
| vending_snackgreen | [GLB](vending/vending_snackgreen.glb) | [BLEND](vending/vending_snackgreen.blend) |
| vending_snackorange | [GLB](vending/vending_snackorange.glb) | [BLEND](vending/vending_snackorange.blend) |
| vending_snackteal | [GLB](vending/vending_snackteal.glb) | [BLEND](vending/vending_snackteal.blend) |
| vending_soda | [GLB](vending/vending_soda.glb) | [BLEND](vending/vending_soda.blend) |
| vending_sovietsoda | [GLB](vending/vending_sovietsoda.glb) | [BLEND](vending/vending_sovietsoda.blend) |
| vending_space_up | [GLB](vending/vending_space_up.glb) | [BLEND](vending/vending_space_up.blend) |
| vending_starkist | [GLB](vending/vending_starkist.glb) | [BLEND](vending/vending_starkist.blend) |
| vending_sustenance_labor | [GLB](vending/vending_sustenance_labor.glb) | [BLEND](vending/vending_sustenance_labor.blend) |
| vending_syndi | [GLB](vending/vending_syndi.glb) | [BLEND](vending/vending_syndi.blend) |
| vending_wallmed | [GLB](vending/vending_wallmed.glb) | [BLEND](vending/vending_wallmed.blend) |
