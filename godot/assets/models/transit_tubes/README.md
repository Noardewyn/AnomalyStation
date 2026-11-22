# Transit tube kit

13 low-poly GLB models with editable Blender files. Original
`icons/obj/pipes_n_cables/transit_tube.dmi` pixels are retained in
`textures/transit.png`; nearest sampling is embedded in GLBs. Round tubes use
12 facets around the circumference, real wall thickness and transparent material.

| File | Use |
|---|---|
| `tube_straight.glb` | Cardinal straight section |
| `tube_diagonal.glb` | Diagonal neighbour connection |
| `tube_curve_left.glb` | North / southwest transition |
| `tube_curve_right.glb` | North / southeast transition |
| `tube_junction.glb` | North / southwest / southeast fork, selectable exit priority |
| `tube_straight_crossing.glb` | Source walkable two-port cardinal section |
| `tube_diagonal_crossing.glb` | Source walkable two-port diagonal section |
| `tube_station.glb` | Through boarding station, two animated side covers |
| `tube_terminus.glb` | Single-port reversing boarding station |
| `tube_station_dispenser.glb` | Through station that creates temporary pods |
| `tube_terminus_dispenser.glb` | Reversing temporary-pod dispenser |
| `transit_pod.glb` | Reusable capsule, separate occupant attachment |
| `transit_pod_temporary.glb` | Temporary capsule, materialized/dissolved states |

Construction/unattached states reuse the same geometry; there are no unnecessary
cardinal-rotation duplicates. Junction flipped state changes preferred routing and
arrow visibility, not the shell. A generic pipe dispenser already exists at
`../machines/transport_wave/pipe_dispenser.glb`; its transit variant uses the same
source body and does not need another model.

## Assembly

- Tile pitch: 2m. Cardinal ports are at cell edge centres; diagonal ports are at
  corners and diagonal neighbouring roots are separated by `(2,2)` metres.
- Outer tube radius .72m, inner .685m, tube axis .94m above root floor origin.
  Pod maximum radius .568m gives .117m radial clearance.
- `connections.json` records exact port positions and outgoing directions in Blender
  coordinates. glTF/Godot converts `(x,y,z)` to `(x,z,-y)`.
- Curved module end planes use exact cardinal/diagonal tangents, not approximate
  curve secants. Match ports and opposing tangents, then rotate whole modules.
- `Port_*`, `Boarding_front/back`, `Pod_Dock` and `Occupant` are attachment nodes.
  Pod path axis is Blender +Y (Godot -Z); move its floor-origin root along the path.
- Through stations connect east/west; terminus models expose only the east port.
  Rotate both the asset and its connection metadata together.
- The source `boarding_dir` is the actor's travel heading, not the side's location.
  Select the physically opposite boarding-side anchor when mapping this value.

## States and mechanics

Ordinary stations expose `open_front`, `close_front`, `open_back`, `close_back`, each
0.6 seconds like source OPEN_DURATION/CLOSE_DURATION. Doors roll about the tube axis
with an intentional radial offset from the fixed shell; only use the selected
boarding side. Source stations wait five seconds before automatic relaunch.

Dispenser stations stay open: source `close_animation()` does nothing. Select
`boarding_front` or `boarding_back` to expose the correct side; the other side remains
closed. Source dispenser creates a temporary capsule on boarding and deletes arriving
capsules. Temporary models provide `materialized` / `dissolved` visibility clips.

The fork exposes `prefer_left` / `prefer_right`. Source NORTH layout has ports N, SW,
SE; normal priority is SW and flipped priority SE. `crossing` is **not** a four-way
junction: it inherits the two straight ports and changes density to FALSE. Its
walkability requires runtime collision handling; geometry alone does not implement it.

Occupants, inventory, routing, air contents/equalization, cooldowns, spawning,
deconstruction and collisions remain gameplay responsibilities: they are tg's own code,
translated in `src/Core/Tg/game/objects/structures/transit_tubes/` (D276). The catalog's
`transit_*` entries (`../models.json`, docs/ai/specs/models.md "Transit tubes") draw the kit;
the clips are not played, their states are chosen by tg's icon states instead.

## Review and verification

  1–210 illustrate door operation and pod travel; this is a presentation animation,
  not a runtime network implementation.
- `assembly_checks.json`: eight exact connections, opposing tangents, actual curve
  end-plane vertex checks and pod radial clearance.
- `geometry_closure_checks.json`: exported shell boundary/degenerate-face checks.
- `geometry_animation_checks.json`: sampled floor/ceiling bounds for clips.
- `original_atlas_verification.json`: source RGBA identity and nearest sampling.
- `models.json` / `animation_clips.json`: triangle counts and clip inventory.

Source audit: `../tgstation/code/game/objects/structures/transit_tubes/`
(`transit_tube.dm`, `station.dm`, `transit_tube_pod.dm`,
`transit_tube_construction.dm`). Original sprites represent an oblique elevated view;
the round cross-section is an explicit user-requested 3D adaptation.
