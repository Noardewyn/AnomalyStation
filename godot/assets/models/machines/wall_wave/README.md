# Wall devices and AI law rack

8 base models. Each has a GLB and editable Blender file.

Use the GLB in Godot. Keep original texture sampling at nearest. Inspect named child meshes and anchors before wiring animations to gameplay; mutually exclusive state clips must not play together. Dynamic contents are deliberately separate.

Source atlas metadata: `source_states.json`. Source audit and specific limitations are recorded below. Atlas verification checks RGBA identity and glTF sampler settings; geometry checks sample exported animation poses against floor and ceiling. They do not certify gameplay completeness.

| Model | Triangles including hidden state meshes | Coverage |
|---|---:|---|
| [vitals_monitor.glb](vitals_monitor.glb) | 160 | Original frame and independent EKG/respiration/body-part overlays. Overlay materials can be tinted per source health logic; bars/digits require patient-driven runtime data. Wall back at Y=.08; floor origin retained. |
| [incident_display.glb](incident_display.glb) | 48 | 64x32 source atlas, display itself occupies pixels12..52. Normal/shame/broken states, two digit anchors; days/incident counters must be rendered dynamically, not baked as arbitrary numbers. |
| [mining_weather_monitor.glb](mining_weather_monitor.glb) | 102 | Original normal/warning/danger weather overlays with off state. One model rotated to wall direction; no separate cardinal duplicates. |
| [conveyor_switch.glb](conveyor_switch.glb) | 24 | Source one-way icon is map placeholder; runtime uses switch-off/fwd/rev. Separate rotating lever with three poses; gameplay enforces one-way restriction. No conveyor belt meshes. |
| [virus_food_dispenser.glb](virus_food_dispenser.glb) | 36 | Wall chemical cabinet and recessed bottle space/nozzle; original virus_food artwork. Reagent transfer and container are runtime attachments. |
| [detective_board.glb](detective_board.glb) | 28 | Original 1-8 notice overlays, empty state and dynamic evidence surface anchor. Case contents and red-string links are runtime UI/data, not fixed into geometry. |
| [fish_trophy_mount.glb](fish_trophy_mount.glb) | 44 | Original oval wood plaque. Source bar mount loads a persistent or random fish, so no particular fish is baked into the base model; attach selected fish at Mounted_Fish. |
| [ai_core_law_rack.glb](ai_core_law_rack.glb) | 132 | 32x48 atlas core rack with ten independently visible original PCIe cards and slot anchors. Source first-slot offset34, three-pixel spacing; card security and law logic are runtime data. |
