# Medical equipment

Tile pitch 2m; floor origin,
front +Z in Godot. Original full sprite atlases, nearest sampling, flat shading.
BCI and cryogenic equipment correctly use 32x64 states; BCI body starts at row 33.
All other included source states are 32x32.

## Main parts and clips

- IV drip: injection/donation and idle indicator states, Reagent_Container and
  Patient_Line attachment sockets. No permanently attached patient or bag.
- Cryo cell, DNA scanner, DNA infuser: hollow chambers, separate opening doors
  and Patient sockets. Curved doors slide around the vertical axis to fit below
  a 2m ceiling. Occupant geometry comes from the game.
- BCI implanter: separate hood retracts inside its frame; Patient and Implant sockets.
- Chemical dispenser: power and dispensing display clips, maintenance hatch,
  Beaker socket. Soda and alcohol variants use tap/rack geometry and Cup sockets.
- ChemMaster/CondiMaster: original idle/loaded/printing screen overlays, service
  hatch, separate Beaker and Product_Output sockets.
- Reaction chamber: actual runtime mixer0b family, hollow chamber and glass door.
- HPLC: original analysis/idle graph frames, two beaker sockets, service hatch.
- Medical kiosk: power/active display states and service hatch.
- Smart fridge: one shared body with shelves and glass. Original overlays supply
  eight content categories, each with three levels, plus empty. No duplicate
  cabinet is needed for chemistry, virology, organs, drinks, food, plants, Petri
  dishes or slime extracts. Play only one content-category clip at a time.
- Dehydrator: separate shorter body and on/off control indicator.
- Biological vat: open vessel, separate tintable reagent surface, empty/filled states.
- Synthesizer: reagent output socket; underfloor plumbing excluded.
- Operating table: patient anchor at surface height.
- Morgue/crematorium: hollow shell, separate sliding tray and front. Body socket
  follows the tray. Opening uses the adjacent front tile: reserve that cell.
- Manual separator and microscope: small counter props with attachment sockets.

Source type/file inventory: `docs/ai/art/medical_source_inventory.json` in the project.
Relevant update_icon_state/update_overlays logic checked before modelling.
`source_states.json` includes state rectangles, directions, frame counts and timing.

Not all source overlays are wired into these base meshes: morgue status lights,
cremation glow, BCI lock/busy lights, reagent-fill thresholds and some broken
variants remain integration work. Runtime state dispatch, collision, patients,
contents, liquids, sounds and permissions are not part of the model exports.
Original sprite metadata is provided for that work; no claim of complete gameplay
integration is made.

Blender NLA tracks are muted for the default pose; unmute one to preview.
GLBs contain exported clips. Pixel/filter, actual motion-channel and sampled
floor/ceiling checks are recorded alongside `models.json` and `animation_clips.json`.
