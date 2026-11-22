# Wall camera telescreen

Source: /obj/machinery/computer/security/telescreen in
code/game/machinery/computer/telescreen.dm. Wallframe and map initialization
explicitly mount this object on a wall. Department variants share artwork
and change camera networks. No existing telescreen model was found.
Entertainment monitors use a different enclosure/atlas and are not this asset.

Two static presets: normal (telescreen), broken (telescreenb).
update_icon_state appends b when BROKEN. The parent atom_break returns early
when circuit is null, so this does not assert ordinary attacks cause that state.
Both icon_keyboard and icon_screen are null: no additional source picture or
keyboard overlay exists. No invented power-off sprite. No runtime camera feed,
animations, collisions or gameplay integration.

Original icons/obj/wallmounts.png: 288 x 288, 32 x 32 tiles, four directions,
one frame per state. South tile is the construction reference; reuse the mesh
by rotation, without directional model duplicates. PNG pixels match DMI;
GLB embeds unchanged original bytes. BLEND references the original atlas.
Nearest filtering, flat shading. No derived textures.

One connected closed housing with stepped lower corners and a lined screen
recess. Screen_Panel is a separate closed slab with original screen artwork.
Closed_Screen_Glass has dark original tint, alpha 0.15 and 0.003375 m thickness.
The broken appearance uses the original damage artwork on the same geometry;
it does not add a physical hole. Roof uses the original top perspective band.
Sides, rear and underside reuse opaque casing pixels. Unseen depth and rear
mapping are interpretations of the sprite, not new painted textures.

All three meshes are shared between variants in the library. Material slot 0:
Telescreen_<normal|broken>_Original for housing and screen, and
Telescreen_Glass_Original for glass. State UV offset uses KHR_texture_transform.
No moving parts. Rear-center origin, front Blender -Y / Godot +Z.
Scale 0.90 / 32 m per pixel; housing 0.590625 m wide, 0.45 m tall,
0.084375 m deep. 76 triangles per preset.

Both presets pass opaque UV, original PNG bytes, nearest sampler and degenerate
triangle checks. All three meshes are closed/manifold. Front, above, rear and
side visually reviewed. Godot import/runtime not tested. The global validator
still reports unrelated older failures; this family passes.

Built through Blender Lab MCP using tools/art/build_telescreen.py and
export_telescreen.py. Source and validation details are in the adjacent JSON files.
