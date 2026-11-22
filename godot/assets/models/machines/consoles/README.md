# Original-atlas computer consoles

Standard console revised 2026-10-27 from the four original oblique directional sprites.
It uses the exact same closed body geometry as modular_computer and computer_frame:
1.56 m wide, 1.18 m deep, 1.70 m high, with a substantial lower cabinet and inclined
screen/keyboard. The body is 76 triangles; standard console including overlays is 80.
The bronze console is rebuilt separately (1078 triangles, 1.68 m wide, 1.70 m high),
with two connected supports and an open lower plinth. Its four texture variants share
one model. The original atlas supplies only one direction for these bronze states.
Origins are on the floor. Front is Godot +Z.

Original computer.png pixels are unchanged. Nearest sampling is retained. Body UVs
stay within the front tile, compatible with runtime CellUvs remapping. Sides and back
use quiet opaque colors from the original palette instead of stretched front details.
Screen_Overlay and Keyboard_Overlay remain independent; UV2 stores full-tile coordinates
for console_overlays.gd and console_overlay.gdshader. Do not generate lightmap UVs over UV2.
Default states are computer/generic/generic_key. Bronze uses ratvarcomputer1/ratvar1/ratvar_key1.

Matching editable .blend and .glb files are supplied. The current comparison scene is
front and rear).
Review images and checks are in the system temporary directory.

Run tools/art/rebuild_matching_computers.py in Blender via MCP to rebuild the standard,
bronze, modular and frame together. Run this AFTER the older build_space_consoles.py pipeline,
which otherwise restores the previous standard body. The bronze geometry is defined in tools/art/build_bronze_console.py.

Validated closed opaque geometry, no degenerate triangles, no coplanar overlap,
original atlas pixels, nearest filtering and opaque body UV samples after cell remapping.
Godot runtime was not launched for this revision; gameplay integration is unchanged.

Side, rear and top panels have a 65 mm flat border without a dark inner outline,
UV-mapped to the original highlight row above the screen, with varying
pixel brightness and approximately matching front-face texel density. Borders subdivide the closed surface;
there are no overlapping decal faces or new textures.

Trim UVs use affine planar coordinates so triangulated mitres do not shear pixels.
The inner black outline is removed. Texel spacing is approximately 65–71 mm.

Bronze base revision: plinth height 0.40 m, supports 0.30 m wide,
with a deeper rear brace. Overall screen/keyboard height stays unchanged.

Bronze keyboard nose has a 0.12 m vertical apron and 0.04 m lower bevel,
with supports terminating inside the closed housing instead of intersecting its deck.

Bronze side/rear/top panels and base use flat pixel-block trim sampled from three
bronze pixels of the original atlas. No dark inner border or generated texture.
Panel subdivisions keep the blocks square; all edge splits are shared and welded.
