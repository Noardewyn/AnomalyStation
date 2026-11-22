# Furniture assets

25 individual GLB and Blender files.
Curtains remain 2D at the user's request.

Grid pitch is 2 metres. Floor origin is Z=0 in Blender, Y=0 in Godot.
Front is Blender -Y / Godot +Z. Bell is placed on a counter by its root.
Geometry is flat shaded. Original DMI atlas pixels are copied losslessly to PNG;
GLB embeds those full atlases and uses nearest filtering. No source sprites changed.

## Modules and moving parts

- `bench_middle/left/right/solo/corner`: 2m pitch. Corner connects local -X and -Y
  (Blender); place a middle at (-2,0), another at (0,-2) rotated -90 degrees.
  `Tintable_Cover` uses original greyscale GAGS texels. Apply department colour to
  its material; the rainbow mapping icon is deliberately not used.
- `filing_cabinet`, `filing_cabinet_white`, `employment_cabinet`, `drawer_cabinet`:
  three separate hollow drawers. `open`, `close`, `interact` move the middle drawer.
  `interact` is 0.5s, corresponding to the source's brief open icon.
- `vault_safe`: hinged door, `open`/`close`, real interior and Contents attachment.
- `display_case`, `food_display_case`: separate transparent hood and Showpiece
  attachment. `open`/`close`, `broken`/`intact`; broken reveals glass remnants.
- `linen_bin`: `full`, `half`, `empty` change the independent linen volume.
- `water_cooler`: `full`, `half`, `empty`; `jug_removed`, `jug_installed`.
  Reagent liquid material is separate and tintable. When reinstalling a jug, also
  select the correct fill state. Tipping is a whole-root gameplay transform.
- `mop_bucket`, `janitorial_cart`: `empty`/`filled`, separate water surface.
  Cart equipment is not baked in: attach game objects at the named sockets.
- `desk_bell`: `ring`; `punching_bag`: `punch`; press machines: `workout` (0.8s).
- `training_machine`: Target_Attachment and Rider sockets. Movement is game driven.
- `easel`: Canvas socket. Altar has Sect_Cloth and candle sockets.

Each clip should be played separately. Start/stop, sound, permissions, collision,
contents, lighting and effect dispatch are gameplay responsibilities. Blender NLA
tracks are muted in saved files to present a neutral pose: unmute one named track
to inspect it. GLBs already contain exported clips.

## Source usage and remaining integration

Source behaviour checked in filingcabinet.dm, dresser.dm, displaycase.dm,
bedsheet_bin.dm, sofa.dm and GAGS bench JSONs, paintings.dm, desk_bell.dm,
reagent_dispenser.dm, janitor.dm, safe.dm, gym/weight_machine.dm,
gym/punching_bag.dm, training_machine.dm and religion_structures.dm.

`source_states.json` retains atlas rectangles, direction counts, frame counts and
timing. It also contains states not used by the default meshes: for-sale case
stock lights, altar sect designs, cart attachments and source sprite flick frames.
These are reference metadata, not automatically wired Godot animations. No claim
of complete gameplay integration or every sprite effect is made.

`models.json` lists parts and triangle counts. `animation_clips.json` lists clips.
`original_atlas_verification.json` verifies source pixels, nearest sampling and actual
movement in animated GLB channels. `geometry_animation_checks.json` records sampled
clip bounds. State preview also checks the modular corner visually.
