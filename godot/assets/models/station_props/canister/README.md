# Portable canister

tg's portable canister (`code/modules/atmospherics/machinery/portable/canister.dm`), built to the owner's four reference renders. One file per GAGS
pattern of tg (`code/datums/greyscale/json_configs/canister_*.json`): `canister_default`, `canister_stripe`, `canister_double_stripe`, `canister_hazard`, each a
`.glb` with its editable `.blend`. Build and export: `tools/art/build_canister.py` via the live Blender MCP (`exec(open(path).read(), globals())`).
Previews: `docs/ai/art/canister_preview.png` (Blender), `docs/ai/art/canister_ingame.png` (the game).

Scale: 0.85 m tall in the file, body 0.46 m across; the catalog fits it to 1.7 m (tg's sprite is 27 of 32 pixels tall). Origin on the floor at the centre;
front Blender -Y / Godot +Z.

Nodes (the catalog's parts, `models.json` `canister_*`):
- `Canister_Body` — everything static: base with notches, the shell in the pattern's bands, the tank pocket, the cell's bay, head, handle, the gas window's
  ring, the level bar's bed, gas port, valve boss, door frame.
- `Canister_TankDoor` / `Canister_TankDoorOpen` — the tank slot's door shut (no `can-open` overlay) and swung out 100° on its rear hinge (`can-open`: tg's
  `holding`). Both nodes share one mesh; its origin is the hinge, so a later animation turns it about local Z (Blender) / local Y (Godot).
- `Canister_HeldTank` — the tank standing in the pocket while `can-open` is shown. Its `Canister_Flat_Tint_Tank` takes the held tank's own sprite's main
  colour (DV73: the server adds the tank's picture as an invisible overlay; the catalog's `"tintIcon": "icons/obj/canisters.dmi"`).
- `Canister_Window` — the gas window's glass in the head's ring (`Canister_Flat_Tint_Window`): tg's `update_window` lays the gases' pictures inside
  `window-base`, and the glass is blended toward their colours by their alpha and their sprites' coverage (`"tintVisuals": "window-base"`); clear
  without a visible gas (air, oxygen, nitrogen).
- `Canister_CellHatch` / `Canister_CellHatchOpen` — the power cell's hatch on the front's upper body, shut, and hanging open 160° on its bottom hinge
  while tg's `cell_hatch` overlay is shown (`panel_open`, a screwdriver); `Canister_Cell` — the cell in its bay with tg's two lights, drawn with it.
- `Canister_Shield` / `Canister_ShieldBands` — the powered shielding (tg's `shielding` overlay): a see-through glowing shell (glTF BLEND, the game's
  stipple) and two light bands that flicker (`"blink"`). 84 vertices of its own, drawn only while it is on.
- `Canister_ValveWheel` — the valve handwheel, origin at its centre, spins about local Y (Blender) / local Z (Godot). tg shows no valve state; it is static.
- `Canister_Level0` .. `Canister_Level3` — the level bar's four lights, left to right (red, amber, olive, green): tg's `get_pressure_state` `can-0` lights
  the first, `can-1` two, `can-2` three, `can-3` all; none under 10 kPa. At `can-0` the first light blinks as tg's two-frame state does (0.2 s lit, 0.2 s
  dark: the catalog's `level0Blink`, the same node with `"blink"`).
- `Canister_PortLightRed` / `Canister_PortLightGreen` — the light on the head: red while the canister is off a connector port, green while it is on one
  (tg's `can-connector` overlay, `connected_port`; the owner, 2026-10-20).

At most 778 vertices drawn at once (default 742) without the shielding. No textures: flat materials named `Canister_Flat_*` (drawn in their authored
colour and shaded by their normals in the game's prop shader, which has no directional light); the hazard pattern (plasma, BZ) has two thick solid stripes as
double_stripe's, not tg's diagonal hazard stripes (the owner, 2026-10-20); the shell is `Canister_Flat_Gags1` and the stripes `Canister_Flat_Gags2`, which the game draws in the thing's own GAGS colours (their colours in the
files are only a preview: air, oxygen, nitrogen, plasma). The lights' materials have `Emission` in their names: bright in the dark.

Not shown yet: tg's `broken` (a broken body, after the owner's concept art). The shielding cannot stay on in the game yet: its power cell is a GAP
(canister.cs, internal_cell), so process() turns it off. The reference views disagree in places (door angle, the back hook, the valve hidden in the front view): the door sits at 45° (the front view
says ~34°, the side ~57°), the port follows the front view, the handle and valve follow the side views.

The earlier textured canisters (`station_props/tanks/portable_canister_*.glb`) were removed on 2026-10-20.
