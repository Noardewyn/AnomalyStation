# Computers and construction

Five base models, in metres, with matching editable Blender files. Front is Godot +Z.
Original TG atlas pixels are unchanged; textures use nearest sampling.

| File | Placement | States |
|---|---|---|
| medical_laptop.glb | Tabletop origin; 0.80 m wide, 0.62 m above table | powered, unpowered, broken |
| modular_computer.glb | Floor; 2 m tile | powered, standby, unpowered, broken; 58 screen choices |
| computer_frame.glb | Floor | empty, board_installed, board_secured, wired, glassed |
| machine_frame.glb | Floor | empty, wired, board_installed |
| airlock_assembly.glb | Floor; 2 m wide, 2 m high | needs_wires, needs_electronics, needs_screwdriver; independent solid/glass |

Construction clips are discrete visibility presets, not interpolated assembly motions.
Anchoring does not change the original sprite. Completion replaces the frame with the
completed machine. Airlock construction is the partially assembled door, not a bare jamb.
Medical laptops are bolted to tables in TG and have no source folding mechanic.

## Screen variants

The same modular-computer geometry serves all 24 placed departmental PCs. Department
presets load programs; they do not change the chassis. `atlas_bindings.json` maps every
screen state and animation frame to a Godot material UV offset, relative to its default.
The full atlas stays in place. Duplicate the screen material per instance before applying
`uv1_offset`; keep the original UVs and texture. Optional `screen_atlas.gd` implements this
operation without changing the game's registry or gameplay. Advance animated frames using
the metadata's decisecond delays. Selecting `standby` also needs the standby screen binding.

`animation_clips.json` lists exported visibility clips. Run just one construction stage
at a time; airlock solid/glass is an independent choice. Hide source-only overlap planes
through the supplied clips. Screen/keyboard planes intentionally have no thickness and
are offset from closed backing surfaces. All opaque mesh surfaces are closed.

## Sources and rebuilding

- `code/game/machinery/computer/records/medical.dm`, `_computer.dm`
- `code/modules/modular_computers/computers/machinery/modular_computer.dm`, `console_presets.dm`
- `code/game/machinery/constructable_frame.dm`, `machine_frame.dm`, `computer/buildandrepair.dm`
- `code/game/objects/structures/door_assembly.dm`
- `code/__DEFINES/construction/structures.dm`

Run `tools/art/prepare_computers_construction.py`, then run
`tools/art/build_computers_construction.py` in Blender via MCP. Review images and audit
output go to the system temporary directory, not the repository. The combined review
Runtime placement, power dispatch and
construction logic are not connected by this asset batch.

## Matching chassis revision (2026-10-27)

Standard console, modular console and computer_frame use identical body vertices,
1.56 m width / 1.18 m depth / 1.70 m height. The substantial lower cabinet, screen
slope and keyboard depth are reconstructed from all four original oblique directions.
Frame stages retain the same silhouette; only original surface states and side wiring change.
The shared closed body is 76 triangles. All stages retain their existing clip names.
Run tools/art/rebuild_matching_computers.py through Blender MCP to rebuild only these
three assets, update their metadata, and save the front/rear comparison scene.
Other models and their states are preserved. Preview images are stored in system TEMP.

The shared chassis now includes flat thin trim on side, rear and top panels, using
the original screen-frame highlight row, retaining visible pixel variations. Trim is
part of the closed mesh, with no overlapping faces.

Trim UVs use affine planar coordinates so triangulated mitres do not shear pixels.
The inner black outline is removed. Texel spacing is approximately 65–71 mm.

The labeled comparison now includes STANDARD CONSOLE, BRONZE CONSOLE, MODULAR COMPUTER
and CONSTRUCTION FRAME. computer_frame.glb is the computer construction asset, not
machine_frame.glb (the latter is a generic machine skeleton). It provides empty,
board_installed, board_secured, wired and glassed clips; select only one at a time.

## Generic machine frame: orthographic reconstruction

machine_frame.glb is 1.68 m wide, 0.84 m deep, 1.26 m high (202 triangles).
Original box_0..2 rows 4..14 map to the horizontal top opening/rim, while rows
14..29 describe the vertical front. The installed upper board is horizontal and
recessed below the rim. There is no upright monitor or keyboard deck.
Closed beams, side/back panels, floor and recessed front backing surround the
intentional top opening. Clips: empty, wired, board_installed.
Run tools/art/rebuild_machine_frame.py through Blender MCP for this asset only.
The dedicated scene is machine_frame_review.blend; temporary previews and check
reports remain in %TEMP%/station_art_computers. Existing computer review files
are independent and do not contain this generic machine frame.
