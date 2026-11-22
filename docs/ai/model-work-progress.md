# Equipment model pass — 2026-11-16

Scope: buildings and equipment from `required-models.md`. Models only; catalog bindings and game checks are not part of this pass. Items, characters and space hazards are excluded.

## Saved models

Directory: `godot/assets/models/machines/required_models/`. Each entry has a `.blend` source and `.glb` export.

| Model | Triangles | States |
| --- | ---: | --- |
| `dna_vault` | 536 | Seven-frame `Vault pulse`, 10 fps; U-shaped 6 × 6 m footprint |
| `meteor_shield_satellite` | 264 | `Active` bobbing and `Inactive` sideways pose |
| `bsa_generator` | 60 | Static construction part |
| `bsa_bore` | 60 | Static construction part |
| `bsa_fusor` | 60 | Static construction part |
| `bsa_control_console` | 200 | Static console |
| `bsa_cannon` | 1568 | Complete stationary cannon, single EAST model rotated for WEST; muzzle FX anchor |

Also saved `godot/assets/models/furniture/required_models/metal_table_flipped.blend` and `.glb`: 76 triangles, one model rotated for all four directions. Reuses the existing metal table proportions (0.90 m square tabletop, original upright height 0.75 m), with a floor-level flipped pose, four connected legs and a textured underside. The original upright asset is unchanged. Other flipped table material/furniture families are not yet included.

The cannon fits the 11 × 3 tile envelope, with a maximum height of 2 m. Original `cannon.png` supplies its surface regions. The original chamber artwork includes partial-alpha glow pixels; the solid chamber deliberately uses their RGB without opening holes. The muzzle has a recessed, closed throat. Reviewed all sides and top, checked mesh closure and coplanar intersections; no game integration or beam/impact effects were added.

Created and inspected through Blender MCP. Reviewed front, rear, left, right and top; also the vault pulse and satellite state poses. Solid meshes passed closure and volume checks. Cross-object coplanar overlap check found no remaining overlaps. Console supports and overlapping vault apparatus were united into continuous meshes. Original source screen pixels have partial alpha; the console material deliberately uses their color with opaque solid surfaces.

Exports reference unchanged original TG atlases (`dna_vault.png`, `satellite.png`, `particle_accelerator.png`) with nearest sampling. No generated replacement atlases and no game integration changes.

Preview renders, temporary backups and validation output: `../AnomalyStation3D_offrepo/audits/required_models16/`.

## Remaining work

### 2026-11-17 — limb grower

Added `machines/required_models/limb_grower.blend` and `.glb` (430 triangles including all mutually exclusive display frames). Studied the original machinery code and all six atlas states. Model has a 12-sided closed glass sleeve with wall thickness, violet liquid from the original artwork, connected control cabinet and chamber, and a roof service recess with a separately hinged cover. Five clips cover `limbgrower_fill`, `limbgrower_unfill`, `limbgrower_idleoff`, `limbgrower_idleon` and `limbgrower_panelopen`; source display timing is retained. The differently drawn legacy `limbgrower_openpanel` flick is not called by the inspected machinery code and has not been reproduced. No limb/item models are included.

Reviewed front, rear, sides, top, half/full liquid and open-panel poses through Blender MCP. Solid geometry passed closure/normals and cross-object coplanar checks. Glass is explicitly BLEND (alpha 0.24); opaque solids do not use texture alpha. Display planes are deliberate offset exceptions. Export references unchanged `limbgrower.png`, nearest filtering; five clips verified in GLB. Integration/catalog unchanged. Previews and checks: `../AnomalyStation3D_offrepo/audits/required_models17/`.

### 2026-11-17 — stacking machine console

Revised the existing `station_props/station_equipment/stacking_machine_console.blend` and `.glb` rather than adding a duplicate. Added original-atlas keyboard and square control details, panel borders on the rear, sides, roof, pedestal and foot. Preserved original proportions and the eight-frame `console` animation (1.6 seconds). Solid chassis surfaces use opaque materials; animated screen and keyboard details are intentional offset planes. Reviewed front, rear, sides, top and an alternate screen frame in Blender MCP. Solid geometry passed closure checks; cross-object coplanar check found no overlaps. Original `mining_machines.png` is unchanged, with nearest sampling. No catalog bindings or game checks changed. Backups and previews are outside the repository in `../AnomalyStation3D_offrepo/audits/required_models17/`.

This is a partial pass, not completion of `required-models.md`. Continue with the remaining equipment in the source inventory, checking existing assets first (digital clocks and teleporter models already exist). The tram spoiler and shower water placeholders still require review. Supply pod styles beyond the earlier Syndicate model, medical and kitchen equipment, other flipped table families, and the other equipment/state entries remain to be handled.

## Queue reconciliation — 2026-11-19
Tram model/state pass is recorded in tram-model-inventory.md, including combined-state corrections. Do not restart tram wall work. required-models.md mixes client integration gaps with missing model assets and is not by itself proof that geometry is absent.

Next equipment candidates after filename/source review: (1) organ harvester, with lid, charging/active and service states; (2) jukebox; (3) radiant dance machine, sharing source sheet with jukebox but requiring distinct geometry. No dedicated files were found for these names; inspect original sprites and existing batch contents before creating duplicates.

Existing assets to review/reuse later: digital_clock variants, teleporter_hub/station, bookcase, basketball_hoop and Syndicate supply pod. Other pod styles may reuse geometry and need source/state comparison. Curtains remain permitted 2D; characters, loose items, construction frames and hazards stay outside current equipment scope. No claim that all remaining required-models entries have been visually reviewed.

## Organ harvester — 2026-11-19
Created machines/required_models/organ_harvester.blend/.glb through interactive Blender MCP. Studied harvester.dm and all four original directions across idle, open, charging, active and service states. Low horizontal chamber, finished inner well and ribs, connected hinge, separate lid rim and closed glass, recessed service board with independent cover. Footprint 1.125 x 1.75 m, rear pedestal 1.04 m; lid open 65 degrees remains below 2 m. 308 triangles including mutually exclusive glass variants.

Five original state clips: harvester, harvester-open, harvester-charging, harvester-active, harvester-o. Active alternates two original frames at 0.1 s each. Native DMI frame-major layout converted to direction-major JSON metadata; active SOUTH second frame is native cell12, not cell9. Open/service poses are supplied without inventing transition duration; service cover opens 165 degrees for unobstructed access. Original unchanged harvester atlas external, nearest filtering; explicit glass alpha0.55 in export. No occupants, organs or item geometry.

Reviewed front/rear/left/right/top, open lid, service well, charging and both active frames. Solid boundaries closed, positive volumes, final inter-object coplanar scan empty. Source-alpha sample on an opaque housing region is deliberately RGB-only, not a geometry hole. No game launch or integration/catalog edits. Scratch reviews outside repository at audits/tram17/harvester_delivery_review.png. Next queue: jukebox and radiant dance machine.

## Remaining piano types — 2026-11-19
Prioritized at user request before jukebox/dance machine. Reviewed code/modules/instruments/stationary.dm and full original musician atlas: remaining physical subtype is space minimoog with minimoog/minimoogbroken states. Added both .blend/.glb pairs under station_props/piano_and_slots; intact/broken grand piano already existed. Unanchored piano reuses existing geometry; audio instrument selections and portable item synths are not additional stationary piano models. Both new variants reviewed in interactive Blender from front, rear, sides and top; unchanged original atlas nearest sampling. No game checks/integration. Next queue remains jukebox and radiant dance machine.

### Minimoog height/depth correction
After user feedback and explicit approval, replaced low floor-level presentation with a full connected stand in both minimoog variants. Keyboard now 0.80 m, total height 1.1225 m; body depth reduced to 65 percent. Existing .blend/.glb overwritten after off-repository backup. Solid closure and overlap checks passed; all five viewing directions reviewed. Surface artwork planes remain deliberate offset details; original TG atlas unchanged. No game integration changes.

### 2026-11-20 — jukebox and radiant dance machine (Blender MCP)

- Studied `tgstation/code/game/machinery/dance_machine.dm` and every state/frame in `icons/obj/machines/music.dmi` (one direction). Jukebox/no_access share geometry; disco/indestructible share geometry.
- Added `godot/assets/models/machines/required_models/jukebox.blend` and `.glb`: arched wooden cabinet, recessed front, feet, rear service panel, side joinery and original-atlas wood detailing. Idle / broken / active clips; active uses the seven original frames with delays 2,2,2,2,8,4,2 deciseconds (2.2 seconds).
- Added `godot/assets/models/machines/required_models/radiant_dance_machine.blend` and `.glb`: low-poly mirror globe, connected stand/cradle, control socket and red lead. Closed cut surfaces in the chipped globe variant. Idle / broken / active clips; twelve original active frames at 0.1 second each (1.2 seconds). Mark IV and V use the same model.
- Both files reference the unchanged shared original `tg/icons/obj/machines/music.png`; verified pixel identity with the TG DMI and nearest filtering in GLB. No private generated textures.
- Reviewed front, rear, both sides and top in Blender, plus every active frame and broken poses. Solid meshes passed closure/normals checks and no coplanar overlaps were reported. Explicit offset artwork surfaces are intentionally open; state alternatives are mutually exclusive and must be switched rather than blended together.
- Jukebox: 376 visible idle triangles / 504 total including state artwork. Dance machine: 268 visible idle triangles / 1896 total including exclusive globe frames.
- Scratch renders: `../AnomalyStation3D_offrepo/audits/tram17/jukebox_states_review.png` and `disco_review.png`. No Godot run or catalog/integration edits. Dance-floor spotlights, orbital sparkles and character dances are runtime effects, not baked into the model.

### 2026-11-20 — disk compartmentalizer

Created `machines/required_models/disk_compartmentalizer.blend` and `.glb` through interactive Blender MCP. Checked the disk subtype and inherited appearance/service logic in `tgstation/code/modules/food_and_drinks/machinery/smartfridge.dm`, all four disktoaster states plus light mask; artwork has one direction. The elevated sprite is interpreted as a horizontal slotted roof above a low front wall, not a tall front panel. Body is 1 x 0.75 x 0.5625 m (small seam details protrude slightly).

Two recessed disk slots have finished walls and bottoms. Front service recess contains original board artwork and a separate bottom-hinged cover opening 105 degrees. Deliberate side rails, rear panel seams and source gray variation. Five exported pose clips: unpowered, powered, broken, panel_closed, panel_open. Power/damage and service pose channels can be combined; alternatives in each channel must be switched, not blended. Original powered artwork is provided as an optional asset state: the inspected disk subtype sets has_emissive FALSE, so the inherited source code does not currently request its powered overlay. No speculative game behavior was added.

346 total triangles including exclusive offset state artwork. Reviewed front/rear/both sides/top, powered, broken and open service poses in Blender. Closed solid boundaries and positive volume; no cross-object coplanar overlap reported. Solid materials ignore source alpha intentionally; explicitly offset artwork surfaces are open exceptions. Verified original smartfridge PNG pixel identity with TG DMI and external original-atlas GLB reference with nearest sampling. No game launch or integration changes.

Previews remain outside the repository: `../AnomalyStation3D_offrepo/audits/tram17/disk_review.png`. Next candidate: drying rack (no dedicated model found), then reconcile existing kitchen/display-case assets and missing states rather than duplicating them. Nebula shielders are deferred in the source inventory until the nebula system is ported.

## 2026-11-21 — Emergency NanoMed

Rebuilt existing station_props/vending/vending_wallmed.blend and .glb through interactive Blender MCP. Wall cabinet 0.875 m wide, 1.0625 m high, 0.3125 m deep including rear mounts. Original unmodified vending atlas, nearest sampling, recessed screen, finished side/rear panels and connected wall rails. 424 stored triangles including exclusive state skins. Clips: wallmed (0.8 s), wallmed-off, wallmed-broken, wallmed-deny (1.2 s), panel_closed/panel_open (source wiring overlay). No tipping: source tiltable=FALSE. Shared static shell, exclusive front-art frames; no game/catalog binding changes. Solid shell: zero nonmanifold edges, positive volume; coplanar inter-object overlap check empty. Front/rear/left/right/top and off/broken/deny/service/pulse reviewed in Blender. State overlays intentionally open and offset; solid backing opaque. Old assets/render audits outside repository: AnomalyStation3D_offrepo/audits/tram17.
