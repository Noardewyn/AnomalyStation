# Remaining station props

23 base GLB models with matching editable Blender files. Original TG atlases are
preserved unchanged; nearest sampling, metre units, 2 m tile pitch, Godot +Z front.
Screenshots and diagnostic
reports are generated in the system temporary directory, outside the project.

| File stem | Object and supported asset features |
|---|---|
| industrial_suit_storage | Industrial orange cabinet, retracting shutter, interior, service hatch, status variants, item sockets |
| drone_shell_dispenser | Correct drone machine, recessed tray, material hatch, off/on/make/recharge atlas frames |
| weapon_scanner_gate | Open arch, service hatch, separate passive/scanning/alarm effect |
| training_bomb | Small finned training device, inactive/active/wires panel variants |
| gulag_teleporter | Chair, controller, overhead frame, retracting transparent canopy, patient socket |
| gulag_item_reclaimer | Wall terminal, item slot, off/on screen |
| monkey_recycler | Open hopper, two independent rotating rollers, service hatch |
| basketball_hoop | Stand, backboard, circular rim, open net, ball and score anchors |
| skeleton_model | Skeletal mannequin, skull, ribs, limbs, equipment anchors |
| quartermaster_statue | Gold statue with hat, briefcase and pedestal |
| snowman | Three rounded snow volumes, coal details and carrot |
| cloning_pod_exhibit | Static retired vertical cloning capsule |
| damaged_cryo_exhibit | Static horizontal sleeper with raised transparent lid |
| fermenting_barrel | Hollow barrel, hoops, removable lid, contents and label anchors |
| meat_spike | Shared geometry for unfinished frame and completed meat spike, hooks toggle separately |
| ripley_wreckage | Collapsed loader, splayed limbs, damaged cockpit, salvage anchor |
| tachyon_doppler_array | Directional detector and adjacent console, off/broken screen, service panel |
| ectoscopic_sniffer | Small upright detector, measurement frames, service panel |
| exploration_scanner | Tilted concave dish, pedestal, status states, service panel |
| fishing_portal_generator | Oval basin, separate destination surface, maintenance hatch |
| broadcast_camera | Tripod, projecting lens, on/off lamp, broadcast target anchor |
| restaurant_portal | Shared bar/restaurant gateway, separate venue shutter, service hatch |
| television_exhibit | Vintage TV with feet, antenna, screen/broken screen |

## State and content bindings

`source_types.json` maps models to exact TG types. `source_states.json` records full
atlas frame/direction metadata. `atlas_bindings.json` maps supported panel variants
to Godot UV offsets, including original animation frame delays in deciseconds.
Use a per-instance material duplicate when changing a screen. The standalone utility
`../../machines/computers_construction/screen_atlas.gd` applies a binding frame.

`animation_clips.json` lists the GLB clips. Mechanical movements are suggested 3D
transitions; their lengths do not replace TG processing/cooldown durations. The drone
machine makes a shell in 3 seconds and then recharges for 3 minutes; do not confuse
these gameplay durations with the short repeating sprite animation.

Scanner `scanning`/`alarm` clips control visibility; also set the matching atlas
binding. Restaurant `venue_open`/`venue_closed` are discrete shutter presets. Meat
spike `frame`/`assembled` are construction presets. Fishing `on`/`off` controls the
surface; destination selection uses its own atlas binding. The enlarged rift overlay
is excluded from the normal basin's UV mapping because it extends outside the rim.

Patients, suits, helmets, spawned drone shells, attached animals, salvage, fruit,
liquid, video feeds and score text remain separate runtime content. The skeleton's
source Hawaiian shirt and eyepatch are not baked into the bones. Construction,
power, access, scan detection, teleportation and inventory logic are not integrated
by this asset batch. Source metadata is retained for overlays not yet mapped to a
specific 3D surface (such as blood, worn equipment and special effects).

The hoop is shortened to fit the single-floor 2 m ceiling. Old museum capsules are
static exhibits, not working medical machines. Back and side surfaces with no usable
directional artwork use calm colors sampled from the original atlas.

## Source audit and rebuilding

Source code reviewed: `game/machinery/{drone_dispenser,suit_storage_unit,scanner_gate,
syndicatebomb,gulag_teleporter,gulag_item_reclaimer,ecto_sniffer,status_display}.dm`,
`game/objects/structures/{mannequin,showcase,kitchen_spike}.dm`,
`modules/{basketball/hoop,art/statues,vehicles/mecha/mecha_wreckage,
hydroponics/fermenting_barrel,research/ordnance/doppler_array,
explorer_drone/scanner_array,fishing/fishing_portal_machine}.dm`, and
`modules/food_and_drinks/{machinery/monkeyrecycler,restaurant/_venue}.dm`.

Run `tools/art/prepare_station_extras.py`, then `tools/art/build_station_extras.py`
through Blender MCP. Both use the original DMI atlases from the sibling tgstation
repository. `tools/art/review_new_station_assets.py` renders front/rear/state pages.

Underfloor/floor beacons, gas pipes, wiring, vents and small floor sensors remain
excluded by the modelling scope. The dedicated large detector machines above were
part of the separately requested device list.
