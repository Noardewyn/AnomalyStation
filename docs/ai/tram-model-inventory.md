# Tram model inventory — 2026-11-17

Research scope: original `../tgstation/code/modules/transport/tram/`, main `.dmm` files in `_maps/map_files/tramstation/`, original exported sprite metadata, existing model files and catalog. Models only; no integration changes.

## New dedicated assets to author

| Priority | Object/type | Original atlas | Requirements |
| --- | --- | --- | --- |
| 1 | `/obj/machinery/door/airlock/tram` | `obj/doors/airlocks/tram/tram`, `tram-overlays` | Two-tile/4 m width, 2 m station height; glass with thickness; separate sliding leaves; opening 1.8 s and closing 2.5 s; power/access lights, bolts, weld, seal and service-panel overlays. Construction sprite exists; do not add construction frame work without renewed scope. |
| 2 | `/obj/machinery/computer/tram_controls` | `obj/machines/computer` | Window/split-window mounting and standalone `tram_alt_controls`; original route, direction, NIS and broken displays. Split mounting is a placement variant, not a rotated duplicate model. |
| 3 | `/obj/machinery/button/transport/tram` | `obj/machines/wallmounts` | Installed call button/speaker; preserve service-panel and powered/pressed states from parent button. Handheld assembly excluded. |
| 4 | `/obj/machinery/transport/destination_sign` | `obj/tram/tram_display` | Vehicle route/destination sign, blank/NIS and scrolling destination states; emission layers. |
| 5 | `/obj/machinery/transport/destination_sign/indicator` | `obj/tram/tram_indicator` | Platform indicator; different artwork/form from vehicle sign. |
| 6 | `/obj/machinery/transport/tram_controller` and `/tcomms` | `obj/tram/tram_controllers` | Local and central controller cabinets; open/closed/locked panel and power, doors, communications, active, emergency stop and fault indications. Check whether geometry can be shared after visual comparison. |
| 7 | `/obj/machinery/transport/power_rectifier` | `obj/tram/tram_controllers` | Four-direction rectifier artwork; power/active overlays. |
| 8 | `/obj/machinery/transport/crossing_signal` | `obj/tram/crossing_signal` | Crossing signal, inbound/outbound housings and separate signal/status lights. |
| 9 | `/obj/structure/tram/spoiler` | `obj/tram/tram_structure` | Replace documented placeholder; retracted/deployed/welded poses and transitions. |

Dedicated files for these new machinery assets were not found in the model filename/catalog search. This is an inventory result, not a claim that every generic model has been visually audited.

## Existing/reuse candidates

- `structures/multilevel/tram_window_wall`, `tram_subfloor`, `tram_subfloor_corner`, `tram_rail`, `tram_rail_floor`, `tram_railend`, `tram_anchor`: existing models. Catalog currently maps many wall junctions to a single straight window model, so corners/split-wall joins need an assembly review.
- `station_props/furniture_wave/bench_{middle,left,right,corner,solo}`: candidates for tram benches; compare original tram subtype material and geometry before duplicating.
- `machines/wall_wave/incident_display`: reuse candidate for the tram incident counter, with appropriate original face artwork.
- Solid tram wall variants use existing wall atlases (titanium, plastitanium, metals, wood and others); investigate reuse rather than making one unique mesh per material.
- Platform/guideway/induction-plate floor textures live in `turf/tram`; retain the 2 m tile grid and inspect interfaces with existing rails/subfloor.
- Tram number plates, plaques and floor trim are signs/decals; do not automatically turn each into a separate substantial model.

## Not part of the immediate model batch

- The map also contains `/obj/machinery/transport/guideway_sensor` (`obj/tram/tram_sensor`). Recorded for completeness; sensor modeling remains outside the standing scope until explicitly requested.
- Invisible navigation/platform/transport-ID landmarks and transport controller datums need no meshes.
- Remote controls, loose circuitboards, wallframe items, paper and characters remain excluded.

Start modeling with the door and control console, using Blender MCP and original unchanged atlases. Inspect rear faces, both sides, open poses and assembly clearance before delivery.

## Authored 2026-11-17 — first batch

- `structures/required_models/tram_door.blend/.glb`: 4 m × 2 m frame, 0.26 m depth; two sliding leaves, four closed glass solids. 312 triangles in default geometry, 388 including mutually exclusive state overlays. Opening 1.8 s, closing 2.5 s; open/closed clips. Opening/closing lights use original frame sequences. Bolts, denied, emergency, weld, seal and service-panel variants are included as static original overlay states. Denied/emergency flicker, panel transition sprites and spark effects remain pending. Construction frame excluded.
- Door leaves travel 1.87 m each into adjacent wall pockets. The open assembly envelope is approximately 7.74 m wide; do not place fixed geometry in the leaf travel volume. Wall pocket assembly has not been integrated or reviewed in-game.
- `machines/required_models/tram_control_standalone.blend/.glb`: compact original dark housing with rear attachment lugs; 66 default triangles, 140 including display states.
- `machines/required_models/tram_control_window.blend/.glb`: transparent interface plate with rear window attachments; 54 default triangles, 128 including display states. Split-window mounting reuses this geometry with placement offsets.
- Both controls include the original three stop displays, NIS, broken, power-off and all eight frames of the four directional movement displays. States are mutually exclusive; do not blend their visibility clips. Window interface plate is an authored support for the otherwise overlay-only source control.
- Modeling and reviews performed in interactive Blender MCP. Front, rear, left, right and top inspected; door open pose, console NIS and movement poses inspected. Solid parts have closed boundaries and positive volumes; cross-object coplanar-overlap check is empty in default pose. Glass and explicit alpha overlay planes are intentional exceptions to opaque-UV checks.
- Original PNG atlases referenced externally with nearest sampling; no source artwork changed. No catalog/integration edits or game launches. Review images: `../AnomalyStation3D_offrepo/audits/tram17/`.
- Next batch: installed call button, vehicle destination sign and platform indicator, followed by controller cabinets/rectifier/crossing signals/spoiler.

### Door glass lattice revision

Restored source alpha on all four glass solids and added the original dark pixel lattice as a double-sided detail inside each pane, parented to its sliding leaf. Lattice geometry uses only matching dark pixels from the unchanged original atlas, with merged rectangular runs (242 additional triangles). Door now has 554 default triangles / 630 including state overlays. Checked front, rear, both sides, top and open pose in Blender; no inter-object coplanar overlap. Updated the same `.blend` and `.glb`. Pre-revision backup and review images remain outside the repository in `audits/tram17`.

## Second tram batch — 2026-11-17

Saved `.blend` and `.glb` pairs in `godot/assets/models/machines/required_models/`:

- `tram_destination_sign`: 2 m wide, 0.625 m high, 0.25 m deep housing (plus rear mounts). Source top strip is mapped to the horizontal roof; scrolling display occupies the front wall. 46 default triangles / 272 including all mutually exclusive animation frames. Original destination states, NIS and blank state included; template artwork excluded.
- `tram_platform_indicator`: 2 m wide, 0.875 m high framed display with stepped source corners, finished rear and wall brackets. 78 default triangles / 258 including animation frames. All original destination states, NIS and blank included.
- `tram_call_button`: recessed wall housing, separate service cover and hinge, finished inner well, rear fixings, original installed-device and optional access-board details. 96 default triangles / 104 total. Seven states: powered, success, error, no power, open with installed call module, open with access board, empty service well. Circuit artwork uses deliberate alpha detail planes; loose items are not modeled.

Both signs use original sprite frame delays at 30 fps. Front display materials use restrained emission from original colored artwork; dedicated source `_e` lighting-mask integration is not implemented. State visibility clips must be exclusive. No game/catalog integration changes.

Reviewed front, rear, both sides and top in Blender, plus scrolling text/NIS and the button's open cover. Solid meshes have closed boundaries and positive volumes; default-pose inter-object coplanar overlap checks are empty. All original atlases remain unchanged and referenced externally with nearest filtering. Scratch renders stay under the off-repository `audits/tram17/` directory.

Remaining tram queue: local/central controllers, power rectifier, crossing signals, spoiler; existing wall/bench/rail assembly reuse review remains pending.

## Third tram batch — 2026-11-17

Created with interactive Blender MCP, `.blend` / `.glb` pairs under `godot/assets/models/machines/required_models/`:

- `tram_local_controller`: wall-mounted chassis with rear fixing rails, original control face, separate left-hinged protective cover and lock overlay. Open/closed cover poses plus independent original power, normal, doors, emergency stop, fault, fatal, communications and activity layers. 92 default triangles / 118 including state layers.
- `tram_central_controller`: distinct stepped floor unit with lower side bay, taller control tower and upper two-prong head; no protective door, matching `has_cover = FALSE`. Height 1.875 m. Original animated status layers, finished rear panels and ventilation. 80 default triangles / 128 including state layers.
- `tram_power_rectifier`: shallow wall-mounted case with horizontal projecting hood, directional source side vents and rear brackets. Combined unpowered / idle / active clips using original overlay frames. 56 default triangles / 76 total.

Controller status clips are composable layers, not mutually exclusive whole-machine snapshots. Each has an explicit `_off` clip; future integration must select/disable layers according to the original state priority (for example turn off normal when fault is shown). Original frame delays are retained at 30 fps. Rectifier combined clips hold a finished channel's last frame through the longer channel cycle.

All five viewing directions reviewed in Blender; local cover opening and active rectifier/central-controller poses also inspected. Solid meshes have closed boundaries and positive volume; cross-object coplanar checks are empty. Source-alpha samples on solid UVs use deliberately opaque RGB materials; explicit status/decal planes retain alpha. Original `tram_controllers.png` is unchanged and externally referenced with nearest sampling. No game integration or in-game review. Separate screwdriver maintenance-panel mechanics have no dedicated source sprite here and are not modeled as another opening panel.

Next: crossing signals and spoiler, then the existing wall/bench/rail assembly reuse review.

## Fourth tram batch — 2026-11-17

- `machines/required_models/tram_crossing_signal.blend/.glb`: 2 m high frame, shared inbound/outbound assembly, original four light aspects and normal/warning/remote/local-fault sequences. 12 clips; 60 fps preserves half-decisecond frame delays. Light and message planes have a deliberate 3 mm separation. Original crossing_signal atlas is externally referenced.
- `structures/required_models/tram_spoiler.blend/.glb`: fixed diagonal corner on a 2 x 2 m tile, height 2 m, separate sliding-blade exposed sections and carriage. Retracted/deployed and 1.2 s transition clips. This is a staged visual representation of the exposed blade, not a complete internal sliding cassette mechanism. Internal concealed mechanism is omitted. Weld overlay was withheld after visual review showed unsupported pixels beyond the top edge; its fitting remains pending. Existing multilevel placeholder and catalog remain unchanged.

Both models were authored through interactive Blender MCP. Front, rear, both sides and top inspected; spoiler intermediate/deployed poses and signal red/outbound poses inspected. Solid closure checks and default-pose coplanar overlap checks are empty. Original atlases unchanged, nearest sampling, no game launch or integration edits. Review renders remain outside the repository in audits/tram17.

Follow-up audit findings: earlier directional movement overlays on tram control consoles need their direction-major frame indexing checked; earlier controller status channels need their plane spacing checked against alpha occlusion. These findings are not fixed by this batch. Wall/bench/rail assembly reuse review also remains pending.

## Tram correction review — 2026-11-18

Updated through interactive Blender MCP:

- Spoiler: restored optional original welded decal, clipped to the actual triangular roof surface with a deliberate 3 mm offset. It no longer projects unsupported pixels beyond the diagonal edge. Welded/unwelded clips restored; select welded only with deployed according to source logic. Concealed cassette mechanics remain omitted as documented above.
- Window and standalone controls: corrected all 64 movement-frame UV regions to direction-major metadata indexing, using the front-facing source direction for each of the four movement states. Recomputed UVs from vertex positions to preserve orientation independently of polygon loop ordering.
- Local and central controllers: separated independent status channels by 1.5 mm increments, removing coincident alpha layers. Frames within each channel remain mutually exclusive.

Saved updated .blend/.glb pairs in the existing required_models directories. Reviewed front, rear, both sides and top of every revised asset in Blender, and movement poses on both control consoles. Solid closure checks passed (intentional glass/status/decal surfaces excepted). Original texture sheets unchanged. No integration changes or in-game checks. Review sheets and renders stay in the off-repository audits/tram17 directory.

Next remaining task: existing tram wall/bench/rail assembly reuse review.

## Existing tram assets review — 2026-11-18

Source confirms tram benches reuse the standard bench modules with COLOR_TRAM_BLUE (GAGS), not unique tram geometry. Compared the local bench.png, original chairs_wide.dmi and exported tg chairs_wide.png: identical RGBA pixels (416 x 384). Updated all five existing bench .blend/.glb files to reference the shared original tg atlas; the old local texture was not deleted because other assets may reference it. Added original rear-view base strip and central fastening artwork to the rear backrest faces, including both rear faces of the corner. Shared neutral/tintable construction retained; no game tint bindings changed.

Blender MCP review: all five module front/rear/left/right/top views inspected. A corner with two straight neighbors at 2 m translations forms a continuous L without visible gaps; the 1.49 m corner envelope is intentional, not a grid-spacing error. Review assembly saved outside the repository at audits/tram17/bench_join_review.blend. Backups also outside the repository.

Preliminary existing wall/rail review: straight window wall is 2 x 2 m, both rail variants are 2 m long; individual meshes have closed boundaries. Front/rear views inspected, but full wall-corner, split-wall and door-pocket assemblies remain pending. No wall/rail model edits made. Bench overlap scan reports contact faces inside joined seat/frame parts and between neighboring modules; exhaustive contact-face cleanup has not been completed, so no blanket zero-overlap claim is made.

## Door-pocket module — 2026-11-18

Created `structures/required_models/tram_window_door_pocket.blend/.glb` through interactive Blender MCP. Authored assembly adaptation of the existing original-atlas window wall: 2 m length, 2 m height, 0.38 m thickness, two thin window skins, continuous top/bottom frame and closed far end. Entrance at local -X; rotate 180 degrees for the opposite side instead of duplicating geometry. This pocket is an authored 3D clearance solution, not a separately named TG sprite state.

Checked closed/open 8 m-wide assembly with the existing 4 m door and pockets centered at +/-3 m. Nominal leaf body clearances: 70 mm to channel sides, 18 mm above/below, 218 mm to closed end at full travel. Source door remains unchanged. Pocket solid closure and inter-object coplanar checks pass; front/rear/both sides/top reviewed, with closed/open assembly previews. Original tram_structure atlas externally referenced unchanged, nearest sampling. Model-only work, no integration.

Off-repository assembly: audits/tram17/door_pocket_assembly.blend. Full corner/split-wall modeling and smoothing-state coverage remain pending; this entry does not mark those variants complete.

## Continuous window modules — 2026-11-18

Authored with interactive Blender MCP, saved under structures/required_models:
- tram_window_straight: continuous 2 m x 2 m window, 0.25 m frame depth; horizontal rails without redundant end posts, closed glass thickness 0.062 m. 36 triangles.
- tram_window_corner: west/south half-tile arms meeting at one shared upright, 2 m height. Rotate the same geometry for other corners. 148 triangles. Top/bottom static framing unioned; glass enters frame by 2 mm, avoiding coincident contact caps.
- tram_window_split: existing framed-window geometry reused with original split header/sill artwork and original finished upright regions; 76 triangles. This is a UV variant of the existing frame, not a new cardinal shape.

Original tram_structure atlas unchanged and externally referenced, nearest sampling. Reviewed front, rear, both sides, top and an L assembly with straight neighbors at (-2,0) and (0,-2). Solids have closed boundaries and positive volumes; glass alpha is intentional. No game/catalog changes. Review assembly outside repository: audits/tram17/window_corner_assembly.blend.

These modules cover a basic straight/corner/framed arrangement. They do not claim complete mapping of all TG smoothing masks or all split end states; junction/end-cap variants and damage/deconstruction state coverage remain to be assessed separately.

## Window end module — 2026-11-18

Created `structures/required_models/tram_window_end.blend/.glb` in interactive Blender MCP. 2 m length/height, 0.25 m depth, 68 triangles. Closed upright at -X, open continuation at +X; rotate 180 degrees for the opposite row end. Original tram-part-4 trim, symmetric front/rear finish, original unchanged atlas with nearest filtering. Separate closed glass retains intentional transparency.

Inspected front/rear/both sides/top, a three-module straight row and a terminated L with the existing corner. Grid joins have no visible geometric gaps. Solid boundaries are closed. Glass/rail contact caps are internal touching surfaces; no blanket zero-overlap claim. Exported final rear UV correction after assembly review; saved assembly snapshots predate that final UV-only correction.

Review scenes outside repository: audits/tram17/window_end_assemblies.blend. Game/catalog integration untouched. Split-specific end artwork variants, full smoothing-junction coverage and damage states remain pending.

## Split-window source correction and end module — 2026-11-18

A pixel-level reread of tram-split-12 revealed a central double-width upright (source columns 9..22), not a perimeter frame. This supersedes the earlier description of tram_window_split as merely a UV variant of the ordinary framed window. Rebuilt that asset with the central upright and two separate closed glass panels, 108 triangles. Original source trim now connects to its supporting geometry.

Added tram_window_split_end.blend/.glb (68 triangles), with the original tram-split-4 terminal upright. Opposite end uses a 180-degree rotation of the same geometry; separate cardinal meshes are not created. Both assets are 2 m high and 2 m long, frame depth 0.25 m. Source atlas unchanged, external nearest sampling. Solid materials deliberately use RGB; glass retains alpha. Closed mesh boundary checks passed; internal glass/frame contact surfaces remain and are not claimed to be absent.

Interactive Blender MCP used throughout. Reviewed all five sides of both assets and a three-tile terminated split row, including rear and top. Saved .blend/.glb pairs in structures/required_models. Old split asset backed up outside the repository, audit scene at audits/tram17/split_row_review.blend. No game launch or catalog edits. Full smoothing-junction and damage-state coverage remains pending.

## Rails and underframe review — 2026-11-18

Scope correction: tram walls/windows use textured cubes in-game. All wall modeling stops; previously authored wall assets are left untouched for the user to decide whether to remove. This rule is also recorded in AGENTS.md.

Reviewed original fluff.dm rail types and transport_module.dm tram subfloor types, directional rail artwork and all corner subfloor sprites. Inspected rail, protective cover, rail end, anchor and both underframes from front/rear/left/right/top in Blender. Rail meshes passed closure and overlap checks; a rail-anchor-end row joins at 2 m spacing. Existing rails are unchanged.

Replaced pixel-staircase underframe geometry with straight closed beams and unioned joints in the existing multilevel/tram_subfloor and tram_subfloor_corner .blend/.glb assets. Original unchanged tram_structure atlas supplies the beam trim; no generated texture. Maintained the existing vertical band (-0.13 to -0.03 m), 2 m tile clipping and Underframe mesh name. Standard section: 92 triangles; corner section: 382 triangles. Both pass closure and inter-object overlap checks. Four standard sections were reviewed as a 2 m grid. The corner was reviewed individually; full corner-to-platform assembly has not yet been validated.

All modeling through interactive Blender MCP. Backups and assembly review scenes remain outside the repository in audits/tram17; rail_and_base_review.blend contains the checked rail row and standard base grid. No game/catalog integration changes or in-game checks.

## Underframe corner connection correction — 2026-11-18

Reviewed a northwest corner together with east/south/southeast standard sections at 2 m spacing in interactive Blender. Removed the corner's disconnected side stubs and moved its load-bearing joins to the east/south tile midpoints; retained internal diagonal bracing. Extended standard beam construction beyond tile bounds before clipping so end profiles are full width, not shortened by their construction end caps.

Measured matching seam extents: transverse +/-0.127279 m, vertical -0.13..-0.03 m, for corner east versus standard west and corner south versus standard north. This supersedes the prior individual-only corner review. Reviewed front, rear, both sides and top for both revised modules and the four-tile assembly. Both meshes pass closed-boundary/normal checks; original atlas unchanged. Final counts: standard 76 triangles, corner 270 (previously 92/382).

Updated existing multilevel/tram_subfloor and tram_subfloor_corner .blend/.glb pairs. Backups and aligned_base_assembly.blend saved outside the repository under audits/tram17. No walls, integration or game launches. Remaining tram work is equipment/state review; full in-game map placement is outside current scope.

### 2026-11-18 — tram door light animations
- Replaced static lights_denied and lights_emergency with all four original frames, on front and rear. Original delays preserved: 0.30 s and 2.40 s respectively; emergency is a repeatable cycle.
- Verified exactly one frame per side throughout each clip, including half-frame boundaries at 30 fps. Inspected all source frames in Blender from front, rear, sides and top. Existing glass/lattice remains intentional transparent geometry.
- Saved structures/required_models/tram_door.blend and .glb with external unchanged tram-overlays atlas and nearest sampling. Backups and review images: ../AnomalyStation3D_offrepo/audits/tram17.
- Still pending: service-panel transitions and spark effect sequences. No game integration or tram wall changes.

### 2026-11-18 — tram door service states and sparks
- Source review: airlock.dm update_overlays selects panel artwork by DOOR motion state while panel_open is true. These are not service-cover hinge animations. Original panel_closed/open/opening/closing pixels are identical (also for protected variants); reused one mesh per side/protection variant and added opening/closing state clips without duplicated geometry.
- Added original sparks_damaged (7 frames, 2.3 s), sparks_open (6 frames, 0.6 s), sparks_broken (5 frames, 0.5 s), front and rear. Exactly one effect frame per side throughout each sequence; spark clips are mutually exclusive. Combine open spark clip with open door pose. Repeat according to powered/damage state when integrated later.
- Panel, light and spark planes use deliberate distinct depths. Coplanar-overlap check returned no pairs in default solid geometry. Reviewed closed/damaged, closed/broken/protected, fully open and mid-opening combinations from front, rear, both sides and top in Blender. Representative spark frames visually reviewed; entire sequence checked numerically.
- panel_c1/c2/c3 exist in atlas but are not referenced by current airlock update code; not added as invented gameplay states. No separate panel-hinge animation is supplied by this source.
- Saved tram_door.blend/.glb with 20 clips and unchanged external TG textures, nearest filtering. Backup: offrepo/audits/tram17/door_before_sparks; review: door_service_review.png. No game/catalog/wall changes.

## Incident display reuse review — 2026-11-18
Updated existing machines/wall_wave/incident_display.blend/.glb through interactive Blender MCP; tram subtype reuses this housing. Old local texture is pixel-identical to original incident_display.dmi. Added unchanged source PNG to shared tg/icons/obj/machines/incident_display.png and referenced it externally with nearest sampling; old texture retained for other references.

Added independent tram_symbol on/off clips and all eight original display_broken frames (14 s), plus no_power state. Existing normal/shame preserved. Replaced redundant state boxes with front artwork planes over closed housing; 64 total triangles including all frames and trim. Rear and side panel seams/fasteners and top detail use original atlas palette. No new rear mounting mechanics. Adjusted Left_Digits/Right_Digits anchors using original glyph coordinates and offsets; numeric glyphs remain dynamic, no arbitrary count baked.

Reviewed front, rear, both sides and top, normal/tram and two broken poses. Verified one screen frame visible throughout 420 frames; final inter-object coplanar check empty. Original housing closed. Decal/effect planes intentional exceptions to closure. Backups and renders outside repository: audits/tram17/incident_before_review and incident_final_*.png. No catalog/integration or wall edits. Advertisement sequence, trend and hundreds overlays remain outside this correction; this is not a claim of full generic display feature coverage.

## Tram numeric display states — 2026-11-18
Added source-atlas glyphs 0..9 for independent tens and ones slots, original blue hundreds tag and rising/falling arrows to the shared incident_display model. Source pixel offsets and exact TG colors retained (22CCFF blue, BE3455 rising red, 3CF046 falling green). GLB material factors explicitly retain these tints while referencing the unchanged original atlas; no baked recolored texture. 46 additional triangles, 110 total including hidden state alternatives, 33 clips.

Each slot has exclusive 0..9/off clips. Hundreds and trend channels have their own on/off states. Runtime still selects the count and hides display overlays for broken/no power; no catalog or gameplay changes. Default numeric layers are hidden. This supersedes the previous numeric-glyph deferral, but does not implement game logic.

Blender MCP: inspected every digit, 07/falling, 42/rising and 199/hundreds examples, with front/rear/both sides/top reviews. Verified exactly one active glyph per slot for all 20 value selections. Body unchanged; glyphs are intentional transparent planes, channels have explicit small depth spacing. Saved existing wall_wave/incident_display.blend/.glb. Backup and renders remain outside repository at audits/tram17/incident_before_numeric_states and incident_numeric_review.png. Generic delamination/advertisement variants remain outside this tram-specific pass.

## Combined-state audit — 2026-11-19
Loaded current saved assets before editing. Door now resides in structures/tram/tram_door; no recreation of the former required_models location. Added an effects-only power_off clip that hides all door light/spark frames without moving leaves or changing service/weld/seal geometry. Incident display no_power and display_broken now explicitly hide all Tram numeric, symbol, hundred and trend layers; these clips must replace active content clips rather than blend with them.

Checked from deliberately visible prior effect layers: zero remain visible after each off/broken clip. Reviewed closing+service panel, open+unpowered+service panel, counter unpowered and broken from front/rear/both sides/top in Blender. Also loaded and reviewed current local controller cover-open+power+fault+comms, unpowered rectifier and outbound crossing signal+aspect+fault status; those three assets were not changed. This is representative combination coverage, not every possible cross-product.

Saved door and counter .blend/.glb. All original exported node names preserved; external original textures and explicit numeric tint factors retained. Backup: offrepo/audits/tram17/combined_states_20261119. Reviews: combined_states_review.png and combined_equipment_review.png. No wall, catalog or gameplay edits.

Tram scope status: main authored equipment, benches, rails and base have recorded reviews. Walls/windows explicitly frozen; guideway sensor excluded. Concealed spoiler cassette remains omitted as documented. Game integration remains separate; generic incident adverts/delamination variants are not required for the tram subtype.

## Tram information plate — 2026-11-19
Added structures/tram/tram_information_plate.blend/.glb for /obj/structure/sign/tram_plate (signs.dmi, tram_plate). Source has one static direction/frame; statistics and serial number are UI data, not additional surface text. Plate visible area is 20 x 10 source pixels: 1.25 x 0.625 m at 16 px/m, body depth 0.0625 m. Two connected rear mounting rails and recessed rear perimeter treatment use original gray rim regions. Single closed unioned mesh, 52 triangles; Wall mount anchor supplied.

Original signs atlas copied unchanged into shared tg/icons/obj/signs.png; nearest sampling, externally referenced. Solid material deliberately opaque including sprite-corner alpha. Front/rear/both sides/top visually reviewed in interactive Blender MCP; closed boundaries, positive volume, no inter-object coplanar pairs. No game or catalog edits. Review: offrepo/audits/tram17/tram_plate_review.png. This resolves the previously omitted information plate; it is distinct from destination sign and incident display.
