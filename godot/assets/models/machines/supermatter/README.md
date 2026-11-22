# Supermatter crystal

tg's supermatter crystal (`code/modules/power/supermatter/supermatter.dm`, `icons/obj/machines/engine/supermatter.dmi` state `sm`; the engine's and
the hugbox's too), built to the owner's four reference renders (top, bottom, left, right). `supermatter.glb` with its editable `supermatter.blend`.
Build and export: `tools/art/build_supermatter.py` via the live Blender MCP (`exec(open(path).read(), globals())`). Previews:
`docs/ai/art/supermatter_preview.png` (Blender, flat colours: the four sides and the looks), `docs/ai/art/supermatter_ingame.png` (the game, by its shaders:
`sm`, `sm-glow` under its red light, the causality field of the final countdown, its rays at 1500 MeV, the singularity outline).

Scale: base 1.48 m square, 0.24 m high; the main crystal's tip at 1.94 m, under the 2 m default wall (the catalog fits the file's 2.0 m to the wall's
height, the width uniform). Origin on the floor at the centre; it is round, so it does not turn (`"rotate": false`).

Nodes (the catalog's parts, `models.json` `supermatter` and `supermatter_glow`):
- `Supermatter_Base` — the base: chamfered edges, the frame round the top, the grating's slats, the four pads under it.
- `Supermatter_Crystal` — the crystal cluster: an octagonal main column and twelve hexagonal ones leaning out round it. The game draws it by the crystal
  shader (`"shader": "crystal"`, `godot/shaders/prop_crystal.gdshader`): see-through (the room behind shows through each facet, bent and tinted), its
  facets shaded and sparkling, a band of light running up, a halo, and a glow of its own whose peaks bloom softly (the owner, 2026-10-21). Its pulse plays tg's frames: `sm-emissive`'s three (0.07, 0.05 and
  0.1 s; their alpha 181, 280, 217 over the brightest) in state `sm` with tg's `SUPERMATTER_COLOUR` halo; in `sm-glow` (`update_icon`: heat power
  generation above 0.8, its own entry) the same node plays `sm-glow`'s four frames (0.1, 0.05, 0.01, 0.1 s), each frame's crystal colour over `sm`'s as a
  colour factor and its red halo's alpha as the strength, with tg's `SUPERMATTER_RED` halo.
- `Supermatter_CrystalPsy` — the crystal reddened by tg's `sm-psy` overlay (a psychologist near, `psy_coeff`), drawn in place of the plain one while the
  overlay is there, by the same shader and pulses. tg lays it at `psy_coeff`'s alpha; the renderer has no overlay alpha, so it is half way.
- `Supermatter_CausalityField` — tg's `causality_field` overlay (the final countdown, `sm_delam.overlays`): a see-through blue box round it, its top
  lighter as the sprite's, drawn by the field shader (`prop_field.gdshader`: tg's two-pixel checker, light running up, brighter edges), its alpha
  flickering by tg's four frames (175, 129, 73, 129 of 255 over 1, 0.1, 0.1, 0.1 s).
- `Supermatter_HolidayLights` / `Supermatter_HolidayLightsBlink` — tg's `holiday_lights` overlay (the festive season): a string of bulbs in the
  sprite's four colours round the foot. tg's emissive frames alternate 0.1 s with 36 lit pixels and 0.9 s with 90: half the bulbs (with the wire) stay
  lit, the other half go out for 0.1 s a second (`"blink": [0.9, 0.1]`).
- `Supermatter_SantaHat` — tg's `santa_hat` overlay (a santa hat used on it) on the main tip.

At most 508 vertices drawn at once (the base 112, a crystal 253, the field 8, the lights 96, the hat 39). No textures: flat materials named
`Supermatter_Flat_*`; the crystal and the field take their shaders' looks (their colours are the materials'), the rest is drawn in its authored colour and
shaded by its normals in the prop shader; the bulbs have `Emission` in their names (bright in the dark). The crystal's yellow is the references' mid tone.

Compromises: tg's sprite and the references stand the crystal 1.15 base widths high, and the default wall is a tile (2 m) high, so the base is 0.74
of a tile where tg's sprite fills 0.94. The views disagree on the cluster round the main crystal (each side view puts a tall second crystal behind the
main one, on opposite sides): there are two, back right and front right; the leaning crystals lean 9-20 degrees, between the top view's upright tips
and the side views' steeper ones. The see-through crystal is DV92 (tg's sprite is opaque). tg's filters (docs/ai/specs/models.md, "Filters"): its `rays` (their
size its energy, their colour yellow, red in sm-glow, dark red heading for a singularity, their density and factor its damage, turning back and forth over 10 s)
are a fan of rays facing the eye from its middle; its `outline` (the singularity delamination's purple line) is a rim round the crystal. The singularity's and
the energy ball's `icon` layer filters are not drawn on the model. The shard (`sm_shard`) and the small crystal (`sm_small`), on their pedestals, keep their pictures.
