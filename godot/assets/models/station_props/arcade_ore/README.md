# Arcade cabinets and ore box

Created and reviewed through interactive Blender MCP. Models only; no game bindings changed.

- `arcade.glb` / `arcade.blend`: shared Battle Arcade / Orion Trail cabinet. 54 triangles including the separate 2-triangle `Screen`. Dimensions 1.28 x 1.13 x 1.95 m; fits one 2 m cell. Front is Blender -Y / Godot +Z.
- `ore_box.glb` / `ore_box.blend`: open wooden box, 1.52 x 1.36 x 1.12 m, 28 triangles. Continuous external walls, rim, internal walls and floor; no lid.
- `screen_variants.json`: original directional frame rectangles, frame delays and state mapping for future runtime implementation.

Original atlases are referenced from `assets/tg/icons/obj/machines/computer.png` and `assets/tg/icons/obj/mining.png`, with nearest filtering. They are not modified. GLBs embed their textures.

## Arcade states

In Blender, assign `Arcade_fighters` or `Arcade_invaders` to `Screen` and play the timeline at 30 fps. Original frames change every 9 Blender frames (0.3 seconds), with constant interpolation and looping. `Arcade_arcade_broken` is the static broken overlay. Hide `Screen` for the unpowered appearance. The screen is offset 4 mm from the opaque cabinet surface, not coplanar.

`Original_Atlas_UV` addresses the first SOUTH fighters frame. The Blender material applies the atlas offset for the selected state and frame. `Screen_UV` is a normalized second UV set for a future custom shader. GLB contains the first battle frame, not the Blender node animation: runtime screen animation still needs implementation using the supplied state metadata. Do not add the state offset twice.

## Source audit

Read original TG sources in `../tgstation`:

- `code/game/machinery/computer/arcade/_arcade.dm`: shared `arcade` body, no keyboard overlay, default `invaders` screen.
- `code/game/machinery/computer/arcade/battle.dm`: `fighters` screen, four frames.
- `code/game/machinery/computer/arcade/orion.dm`: inherits the shared body and six-frame invaders screen.
- `code/game/machinery/computer/_computer.dm`: broken overlay and unpowered screen suppression; screwdriver deconstructs the monitor, no separate hinged service panel state.
- `code/modules/mining/satchel_ore_box.dm`: one `orebox` icon state; no visual fill levels or lid animation. Stores ores/boulders, can dump contents or be dismantled.

All four arcade directions and the single ore-box direction were inspected. The elevated sprite roof regions were interpreted as horizontal surfaces. Side monitor/control fragments were excluded from the cabinet side finish. The ore-box sides reuse its wooden panel and corner hardware; unseen interior surfaces use restrained wood patches from the same sprite.

Checks: positive closed-shell volumes, no nonmanifold body edges or degenerate triangles; front/rear/side/top and open interior review; working, broken and unpowered display review. The display intentionally has an open perimeter because it is an overlay plane. No Godot testing performed.

Service source inspection and initial builder are outside the repository in `../AnomalyStation3D_offrepo/tools/art/build_arcade_ore.py`; final UV corrections and animated materials were applied interactively through MCP and saved in the blend files. The blend files are authoritative for these finishing edits. Diagnostic renders are in the system temporary directory.
