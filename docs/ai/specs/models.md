# 3D models that replace tg's pictures

Experimental (branch `feature/airlock-model`). The client draws tg's things with tg's sprites (billboards, wall decals, and the
shaped pieces of `WorldGeometry`). This pipeline lets 2D pictures be replaced by 3D models one at a time, with no code per model
as long as one of the rules below fits. A thing whose icon no model names keeps its 2D picture.

## Files

- `godot/assets/models/models.json`: the catalog, the one place a replacement is declared.
- `godot/assets/models/**.glb`: the models (binary glTF, textures embedded or a uri to tg's converted sheet: `../../../tg/icons/obj/x.png`, `GlbFile.TgIcon`). The `.blend` sources and the art READMEs sit beside them.
- The folder is `.gdignore`d, as `godot/assets/tg` is: Godot never imports or recompresses the models; the client reads the files
  itself (`Render/ModelLibrary.cs`, `ANOMALY_MODEL_ROOT` overrides the folder). `godot/tools/export-windows.sh` copies the folder next to
  the executable without the `.blend` files and the READMEs.

## A catalog entry

```jsonc
{
  "id": "metal_table",
  "icons": [ "icons/obj/smooth_structures/table.dmi" ],   // exact icons, a folder ending with '/', or a prefix ending with '*' (GAGS keys)
  "exceptIcons": [],                                       // icons of such a folder it does not draw
  "states": [],                                            // icon states it draws (empty: any)
  "overlays": [],                                          // optional: overlay states of which the appearance needs one (tried first: a lamp lit, at night, in an emergency)
  "allOverlays": [],                                       // optional: overlay states the appearance must all have (a newscaster's wanted screen with its alert); with "overlays" both hold
  "rule": "smooth4",                                       // single | dir4 | airlock | smooth4 | closet | crate | console | dir8 | wall | attach
  "texture": "model",                                      // model | sprite | cell
  "image": "icons/obj/smooth_structures/wood_table.dmi", // optional, model texture: a tg icon's sheet (or a PNG in the models folder) in place of the files' embedded one (one kit, many variants)
  "offset": [ 0, 0, 0.3 ],                                 // optional: metres from the tile's centre in the model's frame (a firelock, a windoor)
  "turn": 0,                                               // optional: degrees added to the rule's turn (a model made looking another way)
  "rest": "shelves",                                       // optional: things on its tile rest on every upward level of it (a rack); "top": a lying body
                                                           //   lies on its top (a bed); "ramp": what stands on it is lifted a level over the tile in its dir (stairs, DV370)
  "overlayParts": true,                                     // optional: a material showing a frame of an overlay state of its sheet is drawn only while the
                                                           //   appearance has that overlay (a suit storage unit's lights and stored suit)
  "source": { "width": 0.9, "height": 0.75 },              // the model's frame in its own metres
  "fit": { "width": "tile", "height": "table" },           // width: tile | uniform; height: wall | table | metres
  "rigidBelow": 0.72,                                      // optional: pieces wholly below this height keep their proportions
  "scale": 1.0,                                            // optional: a size multiplier after the fit (the chair's is 1.5)
  "slots": [ [ 0, 1.2, 0 ] ],                              // optional: where things on its tile rest (source metres); else found from its shelves
  "open": { "degrees": -140, "seconds": 0.2 },             // closet: the door's swing (tg's door_anim_angle, door_anim_time); crate: the lid's seconds,
                                                           //   "states" and "overlays" that show it open besides [base]open, "closedState"
  "cell": [ 32, 48 ],                                      // cell texture: the icon frame its uv point into
  "dir": { "awayFromWall": true, "keepOnPush": true },     // read by the SERVER (DV56): the way the things it draws face, see "The way a thing faces"
  "flickFrame": 0,                                         // optional: this entry draws that frame (from 0) of a flick of its state (a mounted flash's mflash_flash):
                                                           //   while tg flicks it on a thing a model draws, each frame's entry stands in, then its own model comes back
  "parts": { "single": "furniture/.../table_single_4legs.glb", ... }
}
```

The first entry whose icons (and states) match a thing's appearance draws it.

### Rules

| Rule | Parts | What it does |
|---|---|---|
| `single` | `model`, any others | One part, turned by its `dir` as `dir4` (DV56): the dev `rotate` turns any of them (a microwave on a table, the owner 2026-10-20); a `"dir"` rule only sets that dir on the server (a vending machine faces away from the wall). Its other parts are drawn with it, each while the appearance meets its `"overlays"` and `"exceptOverlays"` (a canister: the level bar's lights by tg's `can-0` .. `can-3`, the open door and the held tank by `can-open`, the shut door without it). |
| `dir4` | `model` | Turned by the entity's dir; the model looks south (+Z in Godot, -Y in Blender) unturned. A dir change rebuilds it. |
| `airlock` | any (`left`, `right`; a hatch's four sections; an external airlock's `frame`, `upper`, `lower`) | Turned across the door's passage (the planner's face). A part's `"slide"` is how far it moves when the door is fully open, in source metres: a number across the door, or `[across, up]` (fitted as the model is); the door's animation (`DoorAnimation`) drives it. A sliding part is not drawn past the jamb (the tile's edge), under the floor or above the ceiling; a part without a slide (a frame) stays. A part's `"picture"` other than `closed` shows that state of the door's icon in place of its composed picture (the external airlock's frame shows `open`, which has no leaves). A thing the door animation does not drive (a firelock) slides by its own state: open while it ends with `open`, over `open.seconds`. Shutters and the morgue door rise with a lip left under the ceiling, the blast door's leaves part sideways (D121); a windoor is offset onto its tile's edge. |
| `smooth4` | `single`, `end`, `straight`, `corner`, `cornerOpen`, `tee`, `center` | tg's bitmask smoothing (`table-38`): the four cardinal joins pick a module and its quarter turn; a corner is `corner` when the diagonal between its joins is joined too (a block) and `cornerOpen` when not (an L). Unturned the modules join: an end east, a straight run east and west, a corner north and east, a tee north, east and west; a positive turn takes east to north. When a neighbour is built or removed, tg's smoothing changes the junction in the appearance and the module is picked again. |

| `console` | `body`, `screen`, `keyboard` | tg's computer (_computer.dm). Turned by its dir as `dir4`. The body shows the icon state, `screen` the appearance's screen overlay (`icon_screen`; not drawn without one, as tg's unpowered computer), `keyboard` its keyboard overlay (`icon_keyboard` or its `_off`); with a `cell` texture each part is mapped over the frame its uv point into. |
| `dir8` | `model`, `corner` | tg's eight directions (a conveyor, a railing): a cardinal dir draws `model` turned as `dir4`, a diagonal one `corner` (made for southeast) turned from there. |
| `wall` | `model` | A wall fixture (a lamp, an air alarm, a fire alarm, a camera, an APC, a light switch, a newscaster, a requests console, a status display, an extinguisher cabinet, an intercom, a door button): its back on the wall at -Z unturned, on the wall the planner puts the thing on (its face: a lamp's piece, the others' wall decal, which only a `wall` model replaces; one in the wall cell stands on the open tile before its face), at its mount height and its offset along the wall; `offset` takes it to the wall's face. A material whose name has `Overlay` or `Emission` (a door button's light mask) but not `Unlit` (a lamp's glow layer, an alarm's light) is bright in the dark, brighter than white, so the game's environment glow (bloom, threshold 1.3) haloes it as tg's emissive plane does. |
| `attach` | `model` | An overlay drawn as a model on what it is stuck to (C-4's charge, `plastic-explosive2`, on a wall, a floor, a door, a window, a grille, a locker, a crate, a table, any thing drawn in 3D): at the thing's attach point that faces the overlay's dir (the planter's side, DV63; see "Attach points"), its back on the thing, front out; on a top point lying front up, its top toward the planter. The turf's picture leaves the overlay out (`TileKit.Hidden`), so a wall shows it on one face only; a thing drawn as a picture (an item, a mob, a sign) keeps it in its picture. Pieces whose material is a frame of the sheet show the overlay's own picture and play its animation (the charge's blinking). The entry's `"flash": { "material": "C4_Display", "frames": [ 1 ] }` makes the pieces of that material glow on those frames of the animation (DV64: the charge's timer window lights on tg's bright frame). |
| `sun` | `head...` (turning), any others (standing) | tg's solar panel and tracker (solar.dm, tracker.dm): tg turns two overlay objects of the thing's `vis_contents` (the glass and its edge, the dish and its edge) toward the sun by a transform (`get_panel_transform`, `get_tracker_transform`: a turn by the azimuth, then a shear and a squash of the vertical axis). The parts named `head...` turn about the tile's vertical axis to the azimuth that the transform of the appearance's overlay carries (the entry's `"azimuth"`: the state prefix of that overlay, `solar_panel_` or `tracker`; the first row of the matrix is the cosine and the sine of the angle, `ModelRules.AzimuthOf`); the pedestal parts stand. The model's front looks south unturned, so azimuth 180 does not turn the head; a head never turned faces azimuth 0, as tg's does. It turns in one step (DV74). A broken panel is its own entry (`"overlays": ["solar_panel_glass-b"]`), turned to the angle it broke at. |
| `crate` | `body`, any others | tg's crate (crates.dm). Every part is drawn, turned to the crate's `dir` in the middle of its tile. It is open while its icon state is `[base]open` (update_icon_state), one of `open.states` (the mail crate's filled `mail`) or it has one of `open.overlays` (the cardboard box's lid, the critter crate's `crittercrate_door_open`); its closed state is the state without `open` (or `open.closedState`, the mail crate's `mailsealed`). A part's `"picture"`: `closed` (the composed crate while it is shut, with its lock lights, manifest and weld; the closed state while it is open), `open` (the open state) or a state of the sheet. A part with `"pivot"`, `"axis"` (Godot's, the model unturned) and `"degrees"` swings that far about its node's origin while the crate is open, over `open.seconds` (tg does not animate it: DV57). Things on its tile rest on the floor of its `inside` part. |
| `closet` | `body`, `doorFront` (`doorInside` the door's back; more leaves `doorFront2`, `doorInside2` ...) | tg's closet (closets.dm). The door is open while the appearance has the overlay `[state]_open`, shut while it has `[door]_door`; with neither (tg's `is_animating_door`) it keeps moving away from the last of those it showed, and a closet that never showed one stands shut. The door swings about its hinge by `open.degrees` over `open.seconds`, as `animate_door`; a leaf with its own `axis` and `degrees` swings so (a cabinet's two doors, a box's flaps). The closet stands in the middle of its tile and faces its `dir` (see "Map patches"). Things on its tile rest on its shelves. |

A part is a file (`"file.glb"`), or `{ "file": ..., "node": "Door_Left", "slide": -0.9375 }` for one node of a file. A `single` entry's part may have `"overlays"` (overlay
states of which the appearance must have one for it to be drawn) and `"exceptOverlays"` (of which it must have none), and `"blink": [on, off]` (seconds: shown, then
hidden, by the renderer's clock, `MapRenderer.StepBlinks`; a canister's `can-0` light, tg's two frames of 2 deciseconds, the second empty), `"tintIcon"`
(its `Tint_` materials take the main colour of the appearance's overlay of that icon: DV73, a canister's held tank) or `"tintVisuals"` (they are blended
toward the pictures inside the overlay of that icon state, each by its alpha and its sprite's coverage: a canister's gas window, tg's `window-base`;
`SpriteColors`, `ModelColors.Blend`). `"material": "Atlas_front_"`
takes only the faces whose material name starts with it (a closet door's front and its back are two parts, each with its own picture), and `"pivot": true` builds
the part about its node's origin (the hinge the renderer turns it on). `"turn"` (degrees) turns one part besides its rule's turn: the stationary tanks' kit
joins its end and straight modules north where `smooth4` joins them east (-90).

A part's `"shader"` draws its pieces by a shader of their own instead of the prop shader (`ModelLibrary.MaterialAt`: one material per mesh, the chunk's MultiMesh of
that mesh takes it): `crystal` (`prop_crystal.gdshader`: a see-through gem that shows the room behind it, bent by each facet and tinted by its colour, its facets
shaded by their normals, a band of light running up them, sparkles, a halo at the silhouette, and a glow of its own as tg's emissive plane; drawn opaque with depth, in
the transparent pass) or `field` (`prop_field.gdshader`: a blended see-through box of tg's two-pixel checker, drawn after the crystal it holds). Its colour is the
material's glTF base colour (a field's alpha its opacity). `"pulse": [[seconds, strength], [seconds, strength, r, g, b], ...]` (at most 8) are the frames of the tg
icon state the part plays, looped by the shader's clock: tg's delays, the frames' strength (a crystal's glow, a field's alpha) and an optional colour factor (linear)
on the part's own colour; `"halo": "#rrggbb"` a crystal's silhouette colour (sRGB). The supermatter's entries play `sm-emissive`'s, `sm-glow`'s and `causality_field`'s
frames this way.

### Textures

- `model`: the model's own embedded textures, in the model's uv (the same image in several files is stored once). A part is drawn as one piece per material it
  uses: its image with glTF's `KHR_texture_transform` (uv local to a 32 x 32 tile that the transform selects in tg's sheet), a second sheet (a vendor's light layer),
  or a swatch of a plain material's colour (glass, water). A plain material named `Gags1`, `Gags2` ... takes that colour of the thing's GAGS key (`ModelColors`: a
  canister's shell and stripes by its gas, so one entry per GAGS config draws every colour of it); one named `Flat` is its authored colour (glTF's linear factor
  back to sRGB) (one named `Tint_` is then blended toward its part's tint: `tintIcon`, `tintVisuals`) and, having no shading painted into it, is shaded by its faces' normals in the prop shader (`ModelPiece.Shaded`, instance custom w 2: toward the
  eye and up lighter, the silhouette's faces darker; the world has no directional light); any other keeps the linear factor as its swatch. A material whose name has `Tint` is multiplied by the appearance's `color` (tg's comfy chairs);
  any other image is multiplied by its material's `baseColorFactor` (glTF's linear factor, back to sRGB: a first aid station's reagent bars).
  Every model's images are tg's own sheets, embedded byte for byte (docs/ai/art/original_atlas_report.md); the canisters have no image, only flat colours (the owner, 2026-10-20).
- Live frames: a `model` material whose image is the sheet of one of the entry's own icons and whose uv lie in one frame of it (selected by its transform, or
  as they are) (`ModelFrame`) is drawn
  with the thing's picture as tg composes it now, for that frame's dir: its icon state, its overlays, its state's animation and its flicks. It does so while the
  thing shows the frame's state or a variant named after it (`[state]-off`, `-broken`, `-deny`, `[state]_broke`, `-alt`); a state of another shape
  (`toilet01` on a `toilet00` model) keeps the frame the model was made from. A vending machine's front, sides, flap and light layer are such frames: off or broken
  it shows tg's off or broken front, and its deny and vend flicks play on it. An entry's `frameSheet` (a `.dmi`) names a sheet that stands for its picture: a GAGS thing's
  model may take its pixels from tg's map icon of it (`icons/map_icons/objects.dmi`) and show its GAGS picture with tg's overlays in every state.
- `sprite`: the entity's composed picture, with tg's overlays: every face takes the pixel of the front view of the source frame it
  stands on, half a pixel inside the face, so side faces continue the sprite's edge colours. The station airlocks use it, so the
  fill or the glass, the bolt and emergency lights, the panel and the weld are tg's overlays drawn on the model.
- `cell`: the model's uv point into one icon frame each of a tg sheet it embeds (`cell`, 32 x 48 for the closets' `closet.png` and the crates' `crates.png`); each part
  is mapped over that frame and given the picture of the state its rule picks (so one crate model draws every paint job of tg's sheet that shares its shape). A closet's body shows its `icon_state`; the door's front shows the closet's composed picture while
  it is shut (the door overlay with the lock lights and the weld, tg's overlays) and its `[door]_door` state while it swings or stands open; the door's back
  shows `[state]_back`.
- A material with glTF `alphaMode` BLEND is glass: drawn see-through by the picture's alpha as an ordered stipple in the opaque pass. With a `model` or `cell` texture the
  glass's own material alpha multiplies it (a glass table's 0.42); a `sprite` door's glass is tg's glass overlay alone.
- Tables: one modular kit per family (`furniture/table_kits`: wood, cloth, glass, framed glass, reinforced, rolling; the metal one in `furniture/metal_table/modular`),
  one entry per tg variant with its `image`, its own tg icon (`icons/obj/smooth_structures/<variant>.dmi`: an `image` that is a `.dmi` is its converted sheet,
  whose layout the family's kit uv point into), all joined by `smooth4`.

### Fitting

`fit.height` maps `source.height` to the wall's height, the table top (`WorldScale.TableTop`, which items on tables rest on) or a
number of metres. `fit.width: "tile"` stretches `source.width` over the 2 m tile; `"uniform"` scales the width as the height. With
a stretch, `rigidBelow` keeps a table's legs from growing fat: each piece wholly below that height is scaled as the height is about
its own centre, and only its centre follows the stretch.


## Filters

BYOND filters on a thing a model draws (`ModelFilters`, `MapRenderer.Filters`), from its appearance's filters with their `animate()` chains played at the render
tick (`FilterPlayback`): the first `rays` is a quad facing the eye at the model's middle (half its top, moved by the filter's x and y), drawn by
`prop_rays.gdshader` with the 2D client's rays pattern (`AppearanceCompositor.Rays`: size in pixels of the 2 m, 32-pixel tile, density, factor, threshold,
offset, colour), blended under the crystal; the first `outline` is an inverted hull round the entry's crystal parts (`"shader": "crystal"`): their meshes
with smooth normals (`ModelMesh.SmoothNormals`, `ModelLibrary.HullAt`: the flat faces' split corners joined, so the hull stays closed) pushed out by the
line's size in pixels, drawn from their back faces in the transparent pass before the crystal (`prop_outline.gdshader`), lit so a dark colour reads. The supermatter has both
(its energy and damage set the rays; the singularity delamination adds the outline). Other filters, and `layer` filters of another icon (the singularity's
and the energy ball's over and under the crystal), are not drawn on a model.

## Attach points

Where a thing stuck to another goes (`ModelRule.Attach`, `Shared/Models/AttachPoints.cs`). A point is a position and the way out of the thing there (its normal; up for a top).

- A model names its own points: empties (or any nodes) named `Attach_<name>` in its .glb, whose +Z in glTF is the way out (in Blender, whose -Y is: the models' front convention), scaled as the model's meshes are; or the catalog's `"attachPoints": [ { "name": "panel", "at": [x, y, z], "normal": [x, y, z] } ]` in the fitted model's own frame (metres, before it is turned and placed). Both are turned and moved with the model (its origin and yaw).
- A thing without its own points gets its bounds' (its model parts' meshes where they stand, or a shaped piece's pick boxes): the middle of each side at an alarm's height (`WorldScale.PanelHeight`, kept 0.25 m inside the face) and its top; a thing lower than 1 m has only its top (a table, a crate). A wall cell has its open sides only; a floor its middle.
- What is stuck takes the side point that faces its planter best (`AttachPoints.Choose`; it must face it within about 75 degrees), else the top, else the best side anyway.
## Where things rest

A thing at rest on a tile is drawn on what stands there (`MapRenderer.TryRest`, for the map's static things and for the ones that moved, which the entity layer
draws): on a closet's shelf or a crate's floor (the slot is picked by the thing's id among the closet's slots; its own pixel offset is not used), else on a table's top (a table
model's own top, which its fit may set higher than `WorldScale.TableTop`; the pixel offset still applies). The slots are the catalog's `slots`, else the
shelves found in the body: its upward faces below the top, grouped by height, up to four places along each, the highest shelf first.
A body lying on a tile is drawn on the top of a model whose entry says `"rest": "top"` (the beds and stretchers: a patient buckled to one) or of a
table (`MapRenderer.TryLieOn`, DV127), by the tile it is drawn over, so a patient wheeled on a stretcher stays on it. tg keeps a closet's
contents inside it while it is shut and drops them on its turf when it opens, so the things on the shelves are the ones tg shows in an open closet.

The scripted world opens and shuts a closet or a crate of the map with the console's `door X Y open|close` (the door overlay or the open state tg would set).

## The way a thing faces (DV56, DV508)

tg draws a closet's sprite facing the viewer and never sets its `dir`, so the way a 3D closet faces is new data. It is the closet's `dir`, owned by the
server and replicated as any dir. The server reads the catalog's `"dir"` rules (`ModelDirRules`; `--models-catalog`, by default the repository's
catalog found upward) and applies them to every atom tg initializes whose icon and state an entry draws (tg's
`COMSIG_GLOB_ATOM_AFTER_POST_INIT`: the map's things and the ones made later; a GAGS thing by its key): `awayFromWall` makes it face away from the wall
behind it (north first, then west, east, south; south when free-standing); every entry's things get `set_dir_on_move` FALSE, so that a push, a bump or a
pull does not turn them (`"keepOnPush": false` opts out; never a vehicle); a free-standing entry's things (rules single, dir4, dir8, closet, crate,
console; `"rotate": false` opts out) get tg's `simple_rotation`, Alt-click and Alt-right-click, the radial's "Rotate left" and "Rotate right" (a thing
bolted down says "need to unwrench!"), unless the thing answers an Alt-click itself (a signal handler, a chair's own element, or a ported `click_alt`);
one that answers only Alt-click (a canister: its tank comes out) turns by Alt-right-click alone, a quarter clockwise. The client
draws the thing by its dir.

A thing whose map says the way it faces keeps it (DV508): `awayFromWall` leaves a thing alone whose `dir` its map set (tg's preloader notes it,
`MapSeam.NoteMapDir`), or the map of the random spawner that made it (tg's spawner gives its `dir` to its pick; the pick is made with it, through
the preloader, so a spawner among the loot hands it on). So a mapper turns a closet in the map editor (docs/ai/specs/map-editor.md 5.14) and it stays
turned, and one never turned faces the room by itself.

Until 2026-11-17 the owner's turns were kept beside tg's maps in patch files (`map_patches/<map>.json`: the ways things face by type and cell,
wall mounts moved, things taken away, a turf type changed everywhere), applied by the server once the map was loaded and written by the dev
commands under `--map-edit`. With the map editor the maps are the source and are checked in (D337): the patches were folded into the maps' own
files (docs/ai/specs/map-editor.md 5.15) and the patch files, their code and `--map-edit` are gone. The dev commands `rotate #ID
[cw|ccw|north|south|east|west]` and `mount #ID X Y [Z] north|south|east|west` (the dev menu's rotate and mount tools) still turn a thing and hang a
wall mount on another wall, for the round: a mount on a floor cell goes to the floor cell in front of that side, keeps the size of its pixel offset
and takes the dir of tg's directional helpers; one in a wall cell goes to that wall cell, facing out. tg's `atom_mounted` deconstructs a mounted
thing that moves, so the component goes before the move and the thing mounts again after it. An APC is refused (its terminal and area). The
client's scripted map applies the same catalog rule, and leaves a thing alone whose map sets its `dir`.

## How it is drawn

- `Shared/Models/` (engine-free, tested in `tests/Anomaly.Server.Tests/Client/Models/`): `GlbReader` reads the files, `ModelCatalog`
  the catalog, `ModelRules` picks the parts and turns, `ModelMesh` fits a part to the world and gives its uv.
- `Render/ModelLibrary` builds each part's mesh once, on first use, and puts each texture in the prop atlas once.
- `MapRenderer.TryBuildModel` places the parts of a static thing the catalog names, in place of its piece or billboard.
- A thing a model draws is picked on its model (`MapRenderer.AddModelBoxes`): one box per drawn mesh, where it stands, a little thicker than drawn; a
  shaped piece (a door, a window, a lamp) keeps its piece boxes. A model's box (`PickShape.Yields`, also a table's top) gives the click to any other thing
  of its tile the ray meets: a mob on a chair, a microwave or an item on a table, as tg's layers do.
- Each chunk (`ChunkBatch`) has one MultiMesh per model part it holds, with the one prop material (`prop_atlas.gdshader`, mode 6):
  the light grid, the deny flash and the door's open fraction work as for the pieces, and an instance only pays for its own part's
  vertices (the shared piece mesh draws every part's vertices for every piece).

## Tests

`tests/Anomaly.Server.Tests/Client/Models/ModelTests.cs` builds every part of every catalog entry from the repository's files and checks the rules;
`tests/Anomaly.Server.Tests/Integration/ClosetGameplayTests.cs` uses a closet on the real server (open, put an item in, shut, open) and reads its door as the
renderer does; `CrateGameplayTests.cs` does the same with a crate.

Known server issue: a closet made at the map load (or spawned) has no door overlay until something updates its appearance (its first open or close); the
renderer takes such a closet as shut and draws its state's `_door`.

## Adding a model

### Independent fixture states (2026-10-23)

The wall rule draws every declared part, with the same conditional part selection as a single model. A part's `allOverlays` requires every named overlay;
`overlays` requires any one and `exceptOverlays` excludes them. Nested overlays are traversed too (the defibrillator's charge, safety and clamps).
`overlayPixelW` selects a particular source display slot when several overlays share a state: the four clock digits or four medical reagent bars. A part's
`offset` translates an authored mesh in source metres for reuse in another slot. `live: false` preserves its authored pixels rather than projecting the
whole composed appearance onto it. Existing `image`, `frameSheet`, cell textures and atlas transforms continue to reuse one geometry kit across sprite variants.

Computer screens and keyboards use `picture: "appearance"` on model-textured parts: the original frame UVs sample the current composed appearance regardless
of the authored state's name. This includes program, standby, power and damage overlays. Their bodies use `live: false` so front overlays do not leak onto
the sides and rear. Atlas identity compares decoded pixels and dimensions, allowing Blender's lossless PNG re-encoding without modifying the original sheets.
Standard, bronze and modular consoles and both construction frames keep their authored metre scale. Computer construction states 0..4 and generic machine
states box_0..2 select mutually exclusive parts; the generic machine chassis is not the computer chassis.

Entries with `overlayFrames: true` play matching overlay surfaces independently, updating atlas IDs in place. Wide frames keep their original dimensions
(bar signs use 64 x 32). Explicit glTF emission works even without an Overlay/Emission material name; a part's `glow` overrides it and `glowOverlays`
enables it only while one of the specified source overlays is present. Source entity lights still supply room illumination.

A wall fixture with `open.overlays` animates its `pivot` parts by their `axis` and `degrees` over `open.seconds`, retaining the current angle across appearance
rebuilds (including damage or removal of its contents). An entry's `family` prepares the meshes and textures for all its variants and hidden parts on first
use, before an in-game state change. Each batch replaces its previous instances together after flushing new atlas pixels; switching does not expose an empty
model or a temporary sprite. Floor-decal placements can now use catalog models while keeping the planner's floor lift and direction.

The integrated families include wall controls, sensors, keycard readers, TVs, turret controls, emergency cabinets, notice boards, all bar-sign
artwork, warning signs, picture frames (with the live photo surface), wall safes, ticket machines, gas pumps and portable connectors.
Air vents, scrubbers and security cameras remain animated 2D sprites per the owner's 2026-10-23 instruction. Their authoring assets are retained without
active catalog entries; the vents keep their floor decals, and cameras keep their wall fins, source overlays and scan/EMP animations.
Clocks and wall healers compose their slots independently rather than selecting representative whole-display snapshots. Inspection-only poses and alternative
static animation samples remain authoring assets; live source frames supply their runtime animation.

`FixtureModelTests` covers source-state selection, all cabinet damage/content/lock combinations, nested defibrillator states, repeated clock digits,
mixed reagent levels, mount placement, mesh reuse offsets and explicit emission. `ModelShowcase.tscn` uses the real replica and map renderer, switches
its appearances every two seconds and checks the model count every frame. Run it in Godot with screenshots, or headless with `-- --quit-after=9`.
`ANOMALY_SHOWCASE_CAM=x,y,eye,yaw,pitch` selects a close view; `ANOMALY_MODEL_SHOWCASE_DARK=1` enables the normal light grid without lamps to inspect emission.

### Export checklist

1. Export a `.glb` with its texture embedded (nearest-filtered pixel art), origin on the floor at the cell's centre, front looking
   south (Blender -Y) for a turned thing.
2. Put it under `godot/assets/models/` and add a catalog entry; pick the rule and the texture source.
3. Run the client: the thing is drawn by the model wherever it is static (a thing that moves leaves the batch and is a billboard
   again, as before). `dotnet test tests/Anomaly.Server.Tests --filter Client.Models` builds every part of every entry.

A new kind of variant choice (another smoothing, a state-driven model) is a new rule in `ModelRules`; the rest stays as it is.

### Transit tubes (D276)

The `transit_*` entries draw `transit_tube.dmi` with the kit in `transit_tubes/` (its README): rule `single` with every mesh node a part, so the pod
turns to its diagonal headings too. tg's curve, junction, station, terminus and dispenser states connect their ports for dir NORTH as the kit's
files have them, so those entries carry `"turn": 180` (the rule's unturned dir is SOUTH). A station's boarding cover is a pivoted part that rolls
120 degrees about the tube axis while tg shows `opening_` or `open_` (`open.states`, 0.6 s); station0's is the north (back) one, since its
`boarding_dir` is `REVERSE_DIR(dir)`; a dispenser leaves its boarding cover out; a junction shows the arrow of its preferred exit (junction0 the
west one, SW); the pod's light part follows `pod` / `pod_occupied`. `TransitTubeModelTests` turns each state's ports (`connections.json`) by the
rule for the four dirs and checks them against tg's `init_tube_dirs`. A pod moving through the tubes is carried by the batch (`StepCarried`); a
model rebuilt on the way (the pod turned by a curve) glides from its new transforms.

### Chemical tanks and SMES

Untextured parts may set `color: "#rrggbb"` to override their authored colour and local spill. A blinking part with `offColor: "#rrggbb"` keeps its geometry during the off phase and switches to that dark swatch with emission disabled; without `offColor`, blinking retains its existing hide/show behavior. The indicator's local light uses the same blink clock and active colour.

Chemical tanks bind eight distinct `chemical_tanks.dmi` body states to their coloured models. The stationary storage body's `mixture` part requires `tank_chem_overlay`; `tintOverlay` selects that overlay's replicated RGBA for its `Tint_` material. The body remains unchanged when the mixture colour changes. The authored overlay alpha is preserved; the source has a colour overlay, not a volume-dependent liquid level.

`flatColors: true` makes untextured materials use their authored sRGB palette and face shading even when their material names do not include `Flat`. glTF primitives without `NORMAL` get generated flat face normals.

A `single` or `dir4` model may use `open.states` / `open.overlays` and pivoted parts with `axis` / `degrees`, like wall cabinets. This lets SMES respond to `smes-o` while its charge/input/output parts remain selected independently from appearance overlays. Blinking parts continue to follow their hinge during movement.

A part may declare `light: { position: [x,y,z], color: "#rrggbb", range: 0.9, energy: 0.18 }`. Position and range use source metres and the model's fit. The small local light exists only while its part is selected, follows its pivot and entity movement, and uses the same blink timeline. Rebuild/despawn removes it. SMES has no active lights when its display overlays are absent; all variants and charge combinations share the same body and origin.

### Machine and furniture waves (D277)

The wave folders (`machines/*_wave`, `machines/computers_construction`, `machines/gravity_and_tanks`, `station_props/furniture_wave`,
`station_props/shuttle_furniture`, `station_props/station_extras`, `machines/stasis`, `machines/suit_storage_classic`) are written into the catalog by `tools/art/catalog_waves.py` from `tools/art/catalog_wave_specs.py`: the entries after
the catalog's "Generated by" comment are its output, so edit the specs and run it again. Each model's glTF clips say which mesh nodes a state shows (a
node's scale keys) and which move (its rotation and translation from the clip's first key to its last); a spec maps each clip to tg's condition, read in
the machine's `update_icon_state` / `update_overlays`: an icon state, an overlay, the entry's open channel, or nothing. Every mesh node becomes a part;
of a node's animation frames (`*_Frame_N`) only the first is drawn, as a live frame (it plays tg's animation, or the overlay's with `overlayFrames`).

New part fields: `states` / `exceptStates` (the icon states the thing must show, or must not, for the part to be drawn: a heater's core panel by its mode);
`move` (a pivoted part's slide in source metres at open fraction 1, with or besides its swing: a drawer, a morgue's tray); `spin` (a pivoted part turns
steadily about its `axis`, degrees per second of the renderer's clock: the gravity generator's field ring, the gas turbine's rotors, the blackbox reels,
the washing machine's drum; `MapRenderer.StepSpins`); `nodeScale` (the node's scale in place of its rest pose's: the cyborg charger's shutter, stored
rolled up). A node whose rest scale is zero (how the exporter hides the other states' meshes) is read with scale 1 (`GlbReader`). A second mover beside
the open channel (a maintenance hatch next to a door) is not drawn while tg shows it open, which shows tg's open panel on the live body behind it.

An entry may be `"hidden": true` with no parts: the things it matches draw neither a model nor their picture, for a machine another thing's model draws
whole (the gravity generator's eight outer parts; its main part, the bottom middle one, draws the low generator 2 m north of its tile). `fit.width`
`"row"` stretches only the model's x over the tile and keeps its depth at the height's scale (the 0.9 m sofa and pew modules join along a row).

Placement follows tg's sprites: machines whose dir is a pipe or duct port (the crystallizer, the cryo cell, the thermomachine, the tank compressor,
plumbing) or a thrust or conveyor (engines, the recycler, the turbine) do not turn by hand (`"rotate": false`); free-standing machines face away from
the wall behind them. The experimentor's 64 pixel sprite covers its tile and the one east of it, the gateway's its row and the one north of it, so their
models are offset there. Wall devices are made from the floor: their offset takes back the generic mount height (1.2 m at a 2 m wall) and their back.
Not drawn by a wave model: the detective board (tg's notice board state, drawn by the notice board's model), the airlock assembly (the station airlock
entry takes every state of its icons), the telecomms relay, message server and the morgue's status lights (no model or overlay for them); the bench
covers keep their own grey instead of their GAGS colour.
