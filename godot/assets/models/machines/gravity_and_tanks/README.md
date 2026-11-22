# Gravity generator, tank dispenser, temperature control unit

## 2026-10-29 peripheral and topology revision

The current low-profile GLB/Blender assembly includes four corner instances of `gravity_peripheral_module`, using shared mesh data and original on_7/off_7 artwork (on_9/off_9 is the mirrored source counterpart). This follows the requested four-corner 3D arrangement; the original game has numbered parts in a 3x3 assembly, not four independent portable generators.

- `gravity_peripheral_revision.json`: part counts, corner locations, validation.

Existing coil/case primitives were unioned to remove interior overlapping faces. The field ring was rebuilt as the boundary of its original occupied pixels, with disconnected diagonal contacts kept topologically separate. Floor conduits were trimmed where they overlap coil feet. All final mesh components are closed with no zero-area faces; same-facing coplanar triangle-overlap audit reports zero pairs. Reviewed front, rear, both sides and off state in Blender. This is not an in-game verification.

Original atlases and nearest filtering preserved. Game bindings were not modified; new corner nodes still need separate runtime integration to replace the visible 2D sprites. Startup/repair stages remain outside this revision. The old catalog also uses broken_* while current game repair states are fix0_* through fix3_*; that integration mismatch was identified, not changed.

Service scripts and the pre-revision backup are under `../AnomalyStation3D_offrepo/`. Final Blender files are authoritative; older builders predate these repairs and can restore old geometry. The older tall generator remains unchanged. The historical notes below describe the earlier version.


## Low-profile gravity generator (version 2)

Dimensions: 5.788 x 5.788 m footprint, 1.78 m height; 1924 triangles, nine mesh parts.
This fits 3x3 cells at `TileSpace.TileSize = 2m`, with 0.22m clearance under the default
2m wall height (`godot/scripts/Shared/WorldScale.cs`). The field ring and central core
are centred on the footprint. Shortened coils flank the core; the control pedestal is
at the front. A separate `Rear_Field_Stabilizer` fills the fifth equipment cell at the
rear, connected to the core by a floor conduit. The equipment now forms a five-cell
cross inside the 3x3 base; corner cells stay clear. This rear unit is a 3D layout
extension using original coil textures, not a separate device in the source game.
Original atlas pixels and nearest filtering are preserved.
The low-profile version's `Active_Field_Ring` now extrudes the exact occupied pixels
of frame 0 of `activated`, including its gaps and sparks. It is 0.07m thick and uses
61 rectangular prisms (732 triangles). Original atlas colour drives both base colour
and emission, including the mint/green shades. It remains a separate mesh to hide
when disabled. This is a static volumetric frame; the source's four-frame animation
is not implemented in this asset.
Origin: floor centre of the 3x3 footprint. The source main tile is 2m toward the front
(Godot +Z); place the model centre 2m north of that tile when wiring it to game logic.
The original tall version is preserved. No runtime catalog or collision changes.
Build with `tools/art/build_gravity_low_profile.py`; measured bounds are saved in the
adjacent `gravity_generator_low_profile.json`.

Each model has a GLB with embedded original atlas textures and a matching Blender source.
Flat shading; nearest filtering.
Original DMI pixel data is preserved in `textures/`; UV rectangles and states are in
`source_states.json`. Models are visual assets; no gameplay/catalog changes or animations.

| File | Triangles | Parts |
|---|---:|---|
| tank_dispenser.glb | 392 | Cabinet, fixed reservoirs, four oxygen and five plasma inventory meshes |
| temperature_control_unit.glb | 100 | Body, front conduit, tintable temperature indicator, network pipe |

## Game behaviour and assembly

Gravity generator source: `tgstation/code/modules/power/gravitygenerator.dm`.
`setup_parts()` builds a fixed 3x3 tile footprint, with its main tile at bottom middle.
The nine `off_N` sprites form one machine, not interchangeable modules. Geometry reconstructs
the assembled silhouette in depth, approximately 5.2 m wide and 4 m tall. Place the model
relative to the footprint centre; its origin is not the original bottom-middle logic tile.
Hide `Active_Field_Ring` when off. The green ring is a static low-poly interpretation of
the animated `activated` overlay; startup and repair stages are not separately modeled.

Tank dispenser source: `tgstation/code/game/objects/structures/tank_dispenser.dm`, also
translated in this project's `src/Core/Tg/game/objects/structures/tank_dispenser.cs`.
It stores 10 oxygen and 10 plasma tanks. The visual display caps at 4 oxygen and 5 plasma.
Show `Oxygen_Tank_1` through `min(oxygen_count,4)` and `Plasma_Tank_1` through
`min(plasma_count,5)`. Hide the other numbered meshes; keep `Fixed_Reservoirs` visible.
The two inventory columns are independent. Width approximately 1.7 m, height 1.85 m.

Temperature unit source: `tgstation/code/modules/atmospherics/machinery/components/unary_devices/thermomachine.dm`
and `code/datums/greyscale/json_configs/thermomachine.json`. This model assumes the requested
device is the unary Temperature Control Unit, which heats/cools connected gas. It does not
represent the thermoelectric power generator with two circulators. The original body,
temperature-meter and pipe states are used separately. Indicator material can be tinted
according to temperature; default green. Body height 1.75 m, width 1.60 m. The pipe is a
separate mesh for connection placement/rotation. Operational animation and maintenance
states are not implemented. The previous concept in `output/models/thermomachine` is unchanged.

Blender axes: Z up, front -Y. Exported glTF/Godot axes: Y up, front +Z.
All mesh components have closed manifold edges (touching/intersecting components can remain
separate within a mesh). Exports were checked for embedded original image bytes and nearest
samplers, and reviewed with Blender renders. In-game rendering has not been tested.

Generator: `tools/art/build_gravity_tanks.py`; it reuses the mesh/export helpers from
`tools/art/build_shuttle_furniture.py`. Source atlas PNG pixels were checked against the DMI files.

Height update: all three large columns and the small central column now reach exactly 2.00 m above the floor. Bases, corner modules, field ring and grid footprint unchanged. Side braces follow the taller side coils. Closure and coplanar overlap checks rerun successfully.

## Repair and startup models (2026-11-04)

New files: gravity_generator_low_profile_off, _fix0, _fix1, _fix2, _fix3, and _operational (GLB + Blender). The approved base file remains unchanged. Repair variants share its closed 2 m geometry and use original repair-state atlas artwork; they do not add detached wreckage or physical missing plating.

Operational clips: field_off, startup (1 s), idle (0.7 s), activating (0.3 s), activated (0.4 s). All original overlay frames are extruded into separate low-poly field meshes, with mutually exclusive step animation. The approved activated frame 0 geometry is reused. Blender NLA tracks are muted in the saved pose; unmute one track to preview. Charge thresholds and repair tools are documented in gravity_state_models.json. Runtime switches between models/clips are not integrated.

Closed geometry and visible coplanar overlap checks pass for all six state assemblies. These new files supersede the old notes about missing startup/repair visuals only for the low-profile version.
