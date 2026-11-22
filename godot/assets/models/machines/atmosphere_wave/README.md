# Atmospheric machines

Eight GLB/blend assets, 2m tile pitch, floor origin. Godot front +Z.
Original full DMI atlas pixels
are embedded unchanged, using nearest sampling. The crystallizer uses a 32x64 state.

Source usage checked in portable/pump.dm, spaceheater.dm, electrolyzer.dm,
gas_recipe_machines/crystallizer.dm, ordnance/tank_compressor.dm,
machinery/shieldgen.dm and atmos_field_gen.dm.

Named source-state clips switch independent panels/indicators. Heaters have
off/heat/cool/standby states. Electrolyzer has off/standby/working; working sparks
use transparent original atlas frames. Atmospheric shield rail has inactive,
active and wantpower states; waiting blink preserves DMI frame delays. Play
each state clip alone, loop effect clips while active. Mechanical maintenance
open/close clips are separate. Compressor has a separate chamber door.

Inserted tanks, crystallized products and gas connection points are named empty
attachment nodes. Pipe networks, underfloor connections, electrical arcs between
machines, generated fields and gas simulation remain gameplay-controlled.
The atmospheric generator is a thin rail in source, not a freestanding tower.

`source_states.json`, `models.json`, `animation_clips.json` describe source frames,
mesh parts and clips. `original_atlas_verification.json` and
`geometry_animation_checks.json` record pixel/filter and geometry/motion checks.
No runtime catalog or state-machine integration is included. Blender NLA tracks
are muted for neutral display; unmute one to inspect animation.
