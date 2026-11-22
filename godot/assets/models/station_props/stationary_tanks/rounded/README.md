# Rounded stationary pressure tanks

2026-10-24. Created in the connected Blender through Blender MCP. Replaces the
old sprite-textured box kit in `models.json`; old files remain available.

## Design and assembly

Horizontal grey pressure vessel, rounded ends, circular section on straight runs,
small round portholes, separate gas-colour discs and glass, simple saddle supports.
Every cell is exactly 2 metres. Height is 1.83 metres; source and fit heights are
equal so the circular profile is not stretched. Origin is floor-centred.
Blender +Y is north; glTF/Godot -Z is north.

The 47 `canister-N` states are geometry connectivity, not gas types. There are 25
canonical mesh files: seven basic roles plus variants for diagonal-dependent
concave corners in tees and four-way cells. `model_info.json` lists every state.
`models.json` uses the existing `smooth4` rule and quarter turns, with exact state
groups. No new renderer rule or network protocol is required.

The shell uses the same offset contour and seven circular-section height levels
in all modules. Joined sides are open. Coincident boundary positions and matched
seam normals allow neighbouring cells to become one vessel. Diagonal adjacency
distinguishes a filled 2x2 block from a hollow inner corner; interior cells have no
windows or feet. The library scene shows single, 1x4, 2x3, L and T assemblies.

Geometry is at most 460 Blender mesh vertices per module, including portholes,
glass and supports. Flat normals/material boundaries may increase GPU vertices.
No external textures, subdivision or runtime modifiers. `pressure_NNN.glb` is the
game export; `stationary_pressure_tanks.blend` contains hidden library modules and
visible assembly examples. Preview-coloured windows are not baked into exports.

## Source properties and limits

Checked against local tgstation `code/modules/atmospherics/machinery/components/tank.dm`
and project `src/Core/Tg/Generated/Types/obj.machinery.atmospherics.components.g.cs`.
Volume is 2500 litres per cell; maximum pressure 46000 kPa; initial pressure fraction
0.5. Connected source tanks merge their gas storage. Gas type comes from the map
subtype; it does not change shell geometry.

Only `Pressure_Flat_Tint_Gas` should receive a gas-colour override. Each exterior
porthole has a separate `Gas_Window`, `Window_Glass`, `Glass_Glint` and rim mesh.
The source uses `gas_holder.vis_contents` masked by `window-bg`, with separate
`window-fg` glass. It is not the nested `window-base` overlay used by portable
canisters. Do not bind the portable canister tint reader without adapting this
source data path. Air/oxygen need not have visible colour in tgstation; preview
colours demonstrate art capability, not a physical or source gas-colour claim.

The project's pressure tank classes are still `DmInert`. This change replaces
their models and state selection; it does not implement gas simulation, dynamic
colour, pipe-port changes or damage. The source controls pipe ports independently
of smoothing, using `open_ports` relative to `dir` and `initialize_directions`.
No permanently open connection pipes were added to the shell. The source's ten
crack overlays and gas leaks remain outside this asset replacement.

## Verification

- `tools/art/validate_stationary_pressure_tanks.py`: parses exported GLBs, rejects
  degenerate/nonmanifold shell triangles and unexpected open edges, compares all
  shared boundaries across every occupied 4x3 layout: 4095 layouts, 17408 seams.
- `StationaryPressureTankModelTests`: checks all 47 source states through the real
  C# catalog and placement rules, fitting, module rotation and exposed windows.
- Blender rendered assembly sheet reviewed. No live in-game visual run performed.

Rebuild with `tools/art/build_stationary_pressure_tanks.py` through Blender MCP.
It writes `catalog_entries.json`; replace only this family's catalog entries when
regenerating. Never overwrite unrelated user changes to the registry.
