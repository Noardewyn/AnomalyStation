# Station devices — model revision

Created via interactive Blender MCP; models only. No Godot launch or integration/catalog changes.

## New assets

Each basename has a GLB and an editable Blender scene in this directory.

| Asset | Triangles |
| --- | ---: |
| portable_air_scrubber | 124 |
| receive_only_intercom | 44 |
| civilian_bounty_pad | 228 |
| disposal_outlet | 152 |
| mining_equipment_order_console | 36 |
| bitrunning_supplies_order_console | 12 |


## Revised files in existing locations

- `../service_wave/coffee_machine_impressa.glb` and `.blend`: continuous housing, finished brewing recess, separate pot/handle, hopper and brew head (364 triangles).
- `../resources_wave/boulder_smelter.glb` and `.blend`: closed housing and finished recesses, joined furnace windows (384 triangles).
- `../../station_props/shuttle_furniture/bookcase.glb` and `.blend`: thin top cap, continuous cabinet, restrained wood sides/back, separate three book rows (628 triangles).

## Coordinates and sources

Meters; station tile 2 x 2 m. Blender Z up, front -Y; glTF Y up, front +Z. Intercom is positioned at wall mounting height. Original sprite PNGs are unchanged; nearest texture sampling is set in Blender and GLB. Original source states, directions, frames and timing are recorded in `source_states.json`.

Source atlases under `godot/assets/tg/icons/obj`: `pipes_n_cables/atmos`, `machines/wallmounts`, `machines/telepad`, `pipes_n_cables/disposal`, `machines/mining_machines`, `machines/bitrunning`, `machines/coffeemaker`, and `service/library`.

## States and animation scope

- Disposal GLB includes one `Outlet_Open_Close` clip, two independent side hinges, closed at frame 1, open at 16, closed at 31, 30 fps. Closed/open/intermediate poses reviewed in Blender.
- Blender files retain alternate state materials and stepped source-frame animation where applicable. Shader/atlas animation does not export to GLB: GLB shows the default material frame. Runtime state switching remains integration work.
- Bounty pad maintenance state is a material variant, not a separately hinged physical flap.
- Scrubber has tank port and connector; an inserted gas tank is not supplied.
- Coffee pot is separate. Individual cup/sugar supply stacks and every fill state are not fully modelled; source overlays are catalogued.
- Book contents are grouped into three removable rows rather than six exact source fill states.
- Smelter off state now uses an alternate material on `Furnace_Windows_smelter`, not the previous separate off mesh. Coffee has additional separate components. Existing game bindings will need a separate integration update.

## Review and maintenance

All nine models checked for closed mesh components, zero-area faces and positive volume; front, back, sides and top reviewed in Blender. No game validation performed. Screenshots/logs remain outside the game directory in TEMP. Service scripts are in `../AnomalyStation3D_offrepo/tools/art/` (`build_station_devices_revision.py`, `finish_station_devices_revision.py`, `device_state_materials.py`, `export_station_devices_revision.py`). Final Blender files are authoritative: some final UV refinements were applied interactively after the builder scripts.

Side/rear refinement: original directional artwork retained; blank casing faces now have restrained panel borders sampled from original atlases. These refinements are saved in the final Blender files.
