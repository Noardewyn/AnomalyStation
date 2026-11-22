# Wall bar signs

51 static presets cover every artwork state in the original atlas, including
empty, syndicate, EMP and chalkboard appearances. Each uses source frame zero.
All remaining animation frames, directions and delays remain in source_states.json
and the canonical atlas JSON; no animation clips were added.

Source /obj/machinery/barsign in code/game/machinery/barsigns.dm explicitly mounts
on a wall and occupies two map tiles. Power loss or broken state selects empty.
Selected artwork changes the whole face. Light masks are emission masks rather
than white artwork. No collision, gameplay integration or emitted scene lights.

Original barsigns.dmi is PNG encoded: godot/assets/tg/icons/obj/machines/barsigns.png
is an exact byte copy of that source, not a re-render. Verified tiles 64 x 32,
atlas 2624 x 1312. JSON preserves the complete original Description text.
GLBs embed those unchanged bytes; BLEND references the canonical atlas.
Nearest filtering and flat shading on all parts. No derived textures.

Nineteen previously built rectangular variants share one closed housing. The
remaining 32 use original-alpha outlines with pixel-aligned steps, protrusions
and openings. Identical outlines share housing meshes. See
 docs/ai/art/bar_sign_geometry_groups.json for actual shared mesh membership.
Little Treats has diagonal point contacts: its boundary builder keeps separate
vertex sectors at those contacts instead of welding non-manifold edges or
cutting diagonally through pixels. Physical shells remain closed/manifold.

Depth is three pixels (0.084375 m). Standard housing width 1.6875 m and height
0.703125 m; special bounds follow each source silhouette at 0.90/32 m per pixel.
Front Blender -Y / Godot +Z, wall at Y=0. No directional duplicates or moving
parts. Rectangular roof maps source perspective rows; shaped artwork retains
its complete face and source casing colors on depth faces. Rear/edge surfaces
reuse opaque casing strips near each source's left border. Hidden views are
interpretations of existing pixels, not newly painted images. No glass.

Material slot 0: Bar_Sign_<state>_Original for physical housing, and
Bar_Sign_<state>_Light_Overlay for optional emission sheets. Atlas offsets use
KHR_texture_transform. Light-mask alpha >=0.5 selects separate planar geometry
with original artwork colors and emission strength 1. Regions are merged to
reduce triangles. This is a static binary-mask interpretation; no bloom added.

12-617 triangles per preset. All 51 pass original PNG bytes, nearest samplers,
opaque UV and nondegenerate triangle checks. Physical shells are manifold;
light sheets are intentionally open. All designs visually reviewed from front
and above, with representative side/rear checks in each batch. Godot import
and runtime have not been tested. Unrelated legacy models still fail global
validation; all bar-sign exports pass. See export_validation.json.

Final 24: bar_sign_remaining_1_preview through bar_sign_remaining_3_preview,
with matching BLEND scenes and PNGs in docs/ai/art.

Build through Blender Lab MCP using tools/art/build_bar_sign.py and
export_bar_sign.py. Additional render scripts: render_bar_sign_shaped.py and
render_bar_sign_remaining.py. No other asset family was modified.
