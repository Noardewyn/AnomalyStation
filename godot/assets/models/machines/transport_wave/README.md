# Engineering and transport

6 base models. Each has a GLB and editable Blender file.

Use the GLB in Godot. Keep original texture sampling at nearest. Inspect named child meshes and anchors before wiring animations to gameplay; mutually exclusive state clips must not play together. Dynamic contents are deliberately separate.

Source atlas metadata: `source_states.json`. Source audit and specific limitations are recorded below. Atlas verification checks RGBA identity and glTF sampler settings; geometry checks sample exported animation poses against floor and ceiling. They do not certify gameplay completeness.

| Model | Triangles including hidden state meshes | Coverage |
|---|---:|---|
| [pipe_dispenser.glb](pipe_dispenser.glb) | 48 | Atmospheric, disposal and transit dispensers share pipe_d in source. Static source appearance; dispensing is item spawning, no invented mechanism animation. No pipe networks included. |
| [orbital_mech_pad.glb](orbital_mech_pad.glb) | 46 | One 2m cell, low heavy slab. Launch warning uses original DMI frames/delays. Separate top hatch/wiring. Actual transport pod and mech are separate runtime objects, not part of the pad. |
| [teleporter_hub.glb](teleporter_hub.glb) | 260 | Walk-through rectangular portal frame, tele0/tele1 source coil effects, separate coil maintenance covers. Companion station must be placed in an adjacent cell; trigger belongs to gameplay. |
| [teleporter_station.glb](teleporter_station.glb) | 92 | Separate power control unit with source powered/unpowered/calibration display and maintenance wiring. Hub and console link in adjacent cardinal cells; anchors illustrate one valid arrangement. |
| [gateway_low_profile.glb](gateway_low_profile.glb) | 2724 | Source gateway footprint is 3x2, atlas 96x96 with -32 pixel offsets. Low horizontal adaptation for a single floor, actual pixel-contour holes, original ready/transit layers. Destination camera view and teleport collision are gameplay-owned. |
| [exploration_drone_launcher.glb](exploration_drone_launcher.glb) | 66 | Low cradle with separate three-color original fuel overlays, empty state and drone/fuel anchors. Source launch emits smoke and moves the drone; pad itself has no moving launch animation. Smoke must be a runtime particle effect. |
