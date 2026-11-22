# The client light grid

Status: v1, M3 P7 (the client light grid) filled sections 3 to 6, 2026-10-08. **M3 P7 owns this spec** (sections 3 to
6); sections 1, 2 and 7 are contracts (the replicated state is M3 P5's encoding; the shader interface is
shared with M3 P6; changes through the lead). Decisions: D24 (dynamic light computed on the client from replicated source
state), D114 (tg's own light math on a client grid, at most 4 shadowed lights, a dev flashlight), D116 (space light, a
deviation), D113 (lights are boxes on the wall). Design basis: the M3 scout's `scope.md` 2h.

## 1. What the server replicates (protocol version 3, `src/Protocol/MessagesV3.cs`)

The server keeps tg's corner lumcount for gameplay (M7 lumcount, D170: SSlighting, the light sources and the lighting
corners of `modules/lighting`, so reading, writing and the scanners need light as in tg; the lighting objects stay out, DV85),
and it does not draw light (D24): it sends the state of tg's light sources and of what blocks light, and the client still
computes the light it draws from it, with its own grid (below). The two agree because both run tg's falloff on the same
sources; the server's number is never sent.

- **A light source per entity**: `NetField.Light`, a `LightState` (`EntityState.Light`, `EntityChange.Light`): tg's
  `light_range` (tiles), `light_power`, `light_color` as RGBA (`PARSE_LIGHT_COLOR`: null is white), `light_on`,
  `light_angle` (degrees, 360 is omnidirectional), `light_dir` (a BYOND direction, the cone's centre), `light_height`
  (`LIGHTING_HEIGHT` 1, or the type's own), the visual offset in tiles (tg's `calculate_light_offset(src)`,
  `lighting_atom.dm:218`: the pixel offsets and `get_light_offset()`, so a wall fixture lights from the wall), and
  `light_system` (`COMPLEX_LIGHT` 1: tg's corner light, this grid; `OVERLAY_LIGHT` 2-4: tg's movable overlay lights, drawn by the
  grid with the same math at the server's centre, DV61).
  It is present while the atom has a light (`light_system` not `NO_LIGHT_SUPPORT`, `light_power` and `light_range` not 0)
  and is an entity (on a turf), and absent otherwise. A carried light is its holder's (M4a's flashlight, DV61): an entity
  sends its own light or the brightest it carries, an overlay light tg shows on it (`overlay_lighting`'s `current_holder`, a
  flashlight in a hand or on a belt, its directional cast as the offset: two tiles ahead of the holder's `dir`) or a
  `COMPLEX_LIGHT` in its contents (tg's top atom: a desk lamp in a hand); `On`
  says whether tg has a light source for it now (`/atom/proc/update_light`, `lighting_atom.dm`: `light_on` as well), so a
  switched-off lamp keeps its record and only its `On` changes. A change of any of these vars or of the
  offset is a change of the field (the hint is tg's `update_light()`, which the port turns into `LightChanged`,
  presentation-seam.md 5).
- **Per cell** (`MapGrid` version 3 and its cell-change records):
  - **opacity** (`CellFlags.Opaque`): tg's `IS_OPAQUE_TURF` (`turf.directional_opacity == ALL_CARDINALS`): the turf is
    opaque or an opaque movable stands on it (walls, closed airlocks, opaque structures), computed by
    `CellState.IsOpaque` (M3 P5);
  - **the base light** (`MapGrid.BaseLights`, `BaseLightRuns`): the cell's area's `base_lighting_color` and
    `base_lighting_alpha` (`areas.dm`, `update_base_lighting`), an index into a small table, 0 for none (alpha 0);
  - **starlight** (`CellFlags.Starlight`): a `/turf/open/space` cell whose starlight is on (tg's `update_starlight` and
    `enable_starlight`, `space.dm:104-120`: next to a non-space turf), a light source at the cell with
    `MapGrid.Starlight`'s values (tg's `GLOB.starlight_range`, `starlight_power`, `starlight_color`, `space.dm:1-20`) and
    `LIGHTING_HEIGHT_SPACE` (-0.5); a change of the global values is `Delta.StarlightChanged`.
- Other light-emitting turfs (`light_floor.dm`, lava, chasms) are not replicated in M3 (none on Meta): an open issue for the
  milestone that maps them.

## 2. tg's math, the reference (P7 ports it engine-free in `godot/scripts/Shared/Light/`)

- **Where light lives:** at tile **corners** (`lighting_corner.dm`), each lit by every source whose range reaches it; a
  tile is drawn with a bilinear blend of its 4 corners (tg's lighting object, `lighting_object.dm`).
- **Which corners a source reaches** (`impacted_corners`, `lighting_source.dm:465`): the corners of every turf in
  `view(ceil(light_range + visual_offset), source_turf)` that is not opaque, with the source turf's luminosity raised to
  that range: BYOND's view over opaque cells (P7 ports BYOND's view algorithm, or tg's own shadowcasting if equal on the
  tests; it says which).
- **How much** (`falloff_at_coord`, `lighting_source.dm:271-296`, through the sheet of `generate_sheet` `:243` and
  `LUM_FALLOFF` / `APPLY_CORNER` `:191-213`): for a corner at offset (x, y) from the source (corners at half-tile offsets,
  shifted by the visual offset), `multiplier = 1 - CLAMP01(sqrt(x*x + y*y + height) / max(1, range))`; with
  `0 < light_angle < 360` it is cut to a cone around `dir2angle(light_dir)`: the angle delta beyond `light_angle / 2` fades
  the light to 0 over 30 degrees. The corner gets `multiplier * light_power` times the colour's channels.
- **Summing** (`lighting_corner.dm` `update_lumcount`, `update_objects`): per corner the channels of all sources add up; the
  displayed value is tg's (the largest channel normalisation and `LIGHTING_SOFT_THRESHOLD` 0 of `__DEFINES/lighting.dm`),
  `LIGHTING_ROUND_VALUE` 1/64 rounding where tg rounds.
- **Constants** (`__DEFINES/lighting.dm`): `LIGHTING_HEIGHT` 1, `LIGHTING_HEIGHT_FLOOR` 0, `LIGHTING_HEIGHT_SPACE` -0.5,
  `MINIMUM_USEFUL_LIGHT_RANGE` 1.4, `LIGHTING_ROUND_VALUE` 1/64, `LIGHTING_TILE_IS_DARK` 0.2.
- **Base light** adds the area's colour at its alpha to every cell of the area (tg's base-lighting overlay), on top of the
  corners.

## 3. The math as built (P7)

Code: `godot/scripts/Shared/Light/` (engine-free; `TgLightMath.cs`, `ByondView.cs`, `LightGrid.cs`, `ILightCells.cs`,
`ReplicaLightCells.cs`). Single precision like DM. Every number is tg's (snapshot 1d99bd7f).

**The falloff.** `TgLightMath.FalloffAtCoord` is `falloff_at_coord` (`lighting_source.dm:271-296`) line for line:
`multiplier = 1 - CLAMP01(sqrt(x**2 + y**2 + z**2 + height) / max(1, range))` (the height sits under the root, not squared),
and for `0 < light_angle < 360` the cone: `coord_angle = delta_to_angle(x, y)` (`__HELPERS/maths.dm:10`), `center_angle =
dir2angle(light_dir)` (`__HELPERS/type2type.dm:80`, BYOND's direction flags N 1, S 2, E 4, W 8), `angle_delta = abs(center
- coord)` folded to 0..180, result `max(multiplier * (1 - max(angle_delta - light_angle / 2, 0) / 30), 0)`. The sheet is
`generate_sheet` (`:243-258`), cached per `(max(1, range), visual_offset, offset_x, offset_y, light_dir, light_angle,
light_height)` like tg's `GLOB.lighting_sheets` (`:228`, per grid here): entry `[k][l]` is the falloff at
`x = -bound + offset_x - 0.5 + k`, `y = -bound + offset_y - 0.5 + l`, with `bound = ceil(range) + visual_offset` (`:247`) and
`visual_offset = max(ceil(abs(offset_x)), ceil(abs(offset_y)))` (`:450`). A corner reads the sheet at its offset from the
source tile (`LUM_FALLOFF`, `:191`, `_range_offset` `:175`), so the replicated `offset` is added to the corner's offset
exactly as tg adds it (a source with offset +1 pools its light one tile to the west). Corner (i, j) is tg's corner at
`(i + 0.5, j + 0.5)` (`lighting_corner.dm` `New`, `x + 0.5`): the north-east corner of tile (i, j), the south-west corner of
tile (i + 1, j + 1), the texel (i, j) of section 7.

**The corners a source reaches** (`impacted_corners`, `:465-513`): the corners of every turf in `view(working_range,
source_turf)` with `working_range = ceil(light_range + visual_offset)` (`:471`) that is not `IS_OPAQUE_TURF` (`:477-479`;
the replicated `CellFlags.Opaque`). `view()` is `ByondView`, a port of BYOND's visibility as Lummox Jr. describes it
(BYOND forum post 2130277) and OpenDream implements it (`OpenDreamShared/Dream/ViewAlgorithm.cs`, MIT, pinned 1362abc5):
the window is the square `(2r+1)^2` around the source; step 3 floods outward in square rings (`Vis2`: a cell is reached when a
neighbour of the previous ring was reached, an opaque cell is reached but ends the flood), step 4 floods outward in
diamond rings (`Vis`: reached by a neighbour of the previous diamond ring and by step 3), step 5 makes the source's own
cell visible. Step 9 (the wall and corner tiles that bound the view) only changes opaque cells and `impacted_corners` skips
opaque turfs, and steps 6-8 (luminosity) hold for every turf (`lighting_object.dm` `Initialize` gives each turf luminosity 1,
the source turf gets the range, `:470-472`), so they are not ported. This is BYOND's rule as far as it is documented, not
BYOND's code; it is a port of an approximation, and the tests pin it on walls and doorways.

**The sum** (`APPLY_CORNER`, `:199-213`; `lighting_corner.dm:90` `update_lumcount`): per corner
`falloff * light_power * channel` (the channels are `light_color`'s bytes over 255, `PARSE_LIGHT_COLOR`) over every source.
The grid keeps the sums in fixed point (units of 2^-16) so removing a source's old effect is exact. The displayed value
is `update_objects` (`lighting_corner.dm:108-130`): when the largest channel is above 1 all three are divided by it
(`TgLightMath.Normalize`), then each is rounded to `LIGHTING_ROUND_VALUE` 1/64 (DM's `round(x, 1/64)`, halves up);
`LIGHTING_SOFT_THRESHOLD` is 0, so there is no soft floor. Negative sums (a darkening light) are clamped to 0 at the
texture.

**The base light** (`lighting_area.dm:41-76`: a `BLEND_ADD` overlay of `base_lighting_color` at `alpha = base_lighting_alpha`)
adds `colour * alpha / 255` to the cell's light. tg draws it per turf; the texture holds corners, so a corner takes the mean of
the four tiles around it (a flat area gives exactly its own value, an area's edge blends like the corners do). The base light
is **not applied to space-turf cells** (`/turf/open/space*` by `TurfTypeAt`, `ILightCells.IsSpaceTurf`; DV22): tg's
`/area/space` (`areas/misc.dm:7-9`, alpha 255) would make the void fullbright, and `/area/space/nearstation` has none.
The texel's alpha channel carries the base light's mean alpha (0 to 1).

**Starlight** (`space.dm:1-20` for the globals, `:104-120` for the cells): each cell with `CellFlags.Starlight` is a source
at its tile with `MapGrid.Starlight`'s values (tg: range 2, power 1, `COLOR_STARLIGHT` `#8589fa`, height
`LIGHTING_HEIGHT_SPACE` -0.5; the `ReplicaWorld` carries them, `StarlightChanged` swaps them on all starlit cells). At a
corner half a tile from a starlit cell the root is `sqrt(0.25 + 0.25 - 0.5) = 0`, so the multiplier is 1: the hull face next
to a starlit cell gets a full `power * colour` per source, and the sum is normalised like any other. Space cells draw the starfield
(P6) and are not lit as turfs: the hull and what stands on it are lit by the corners the starlit cells reach, and nothing else.

## 4. Incremental updates and cost (P7)

- **Rule.** A source is recomputed when its record changes (`LightGrid.SetSource`: range, power, colour, `On`, angle, dir,
  height, offset or tile; an off, empty, zero-power or overlay-system source is removed and its record is kept by the
  replica) and when an opaque cell inside its view box (`bound` tiles each way) changes (`CellOpacityChanged`), exactly the
  moments tg updates a source (`update_light`, `COMSIG_TURF_NO_LONGER_BLOCK_LIGHT` -> `force_update`; opaque turfs entering
  view call `vis_update`). A recompute takes the source's old per-corner effect off and puts the new one on (tg's `OLD` in
  `APPLY_CORNER`), so only the corners the source touched are written. A base-light or space-ness change marks only the four
  corners around the cell.
- **Equal to a full recompute.** Sums are fixed point, so the incremental result is bit-identical to a grid built from the
  final state; `IncrementalUpdatesEqualAFullRecomputeExactly` runs 600 random source and opacity changes (colours, cones,
  offsets, space-height lights, switching on and off) and compares every corner with a fresh grid and with `RecomputeAll()`.
- **Dirty rectangle.** `Flush` rewrites only the texels of the bounding box of what changed since the last flush
  (`Texels`, RGBA16F), and `LightLayer` uploads the image when a flush reported a change.
- **Followed events** (`LightLayer`): `ReplicaWorld.LightChanged` (one source per entity, keyed by its id), `CellsChanged`
  (the replica's flags against the last seen: an opacity change recomputes the sources that see it, a starlight flag change
  adds or removes the cell's source, base light marks corners), `StarlightChanged` (every starlit cell's source gets the
  new values), `Despawned`, and a 5 Hz check that moves the source of a lit entity that changed tile. Levels are built the first time the camera is on
  them and kept updated afterwards.
- **Cost** (Release, Apple M1 Pro, the test `BootCostOfAThousandThreeHundredSources`: 255 x 255, a lattice of walls
  and doorways, 1300 tubes of range 6.5 plus 1700 starlight cells of range 2): 1300 sources in 26 ms (about 20 us per source,
  the box is 15 x 15 tiles; the view is most of it), 1700 starlight cells in 5 ms, the first full flush of the 256 x 256 texture
  4.5 ms; a changed door recomputes only the sources that see it (tens of microseconds each).

## 5. The shadow budget and the dev flashlight (P7)

- **Budget** (D114, revised by D168): `LightLayer.SetPreset(n)` allows `n` shadowed `OmniLight3D`, 0 to 16: 10 on high, 4 on
  medium, 0 on low. The options' "Shadowed lights" (`PlayOptions.ShadowedLights`, file key `shadowed_lights`, `preset` by default or
  0 to 16, `--option=shadowed_lights=N`) replaces the preset's number (`RenderPreset.LightsFor`); a change applies at once, with the
  other options, through `Quality.Apply` (the layer frees the slots above a smaller count, and new ones are made as lamps are chosen).
  The layer keeps a pool of that size. Every 0.2 s `ShadowedLightSelector.Select` takes the lit sources of the camera's level
  (live ones: `On`, positive power, `COMPLEX_LIGHT`) within `range + 2` tiles of the camera, sorts them by distance (ties
  by id) and keeps the first `n` with a line of sight to the camera (a half-tile-step walk over the opaque cells; a lamp on
  a wall tile still counts as seen). A chosen source without a slot takes a free one, and a slot whose source is no longer
  chosen fades out over 0.35 s before it is released (so a light never pops: a new one fades in over the same time).
- **The positional shadow atlas** (`ShadowAtlasPlan.For(n)`, set by `Quality.Apply`): Godot's Forward+ atlas is one square split
  into four quadrants, each cut into 1, 4, 16, 64 or 256 slots (`positional_shadow_atlas_quad_N`); a spot's shadow takes one slot
  and an omni light's two of one quadrant (its two halves, cube or dual paraboloid), a light big on screen takes a big slot and a
  small one a small slot or else a bigger one, and a light with no free slot has no shadow. So the atlas holds `2n + 1` (the +1
  is the camera's own flashlight spot, which casts a shadow outside the budget): the quadrants start at 4 slots and the last
  ones are cut 4 times finer in turn until they hold it, so the nearest lamps keep the biggest slots: low (4, 2048) 4, 4, 4, 4
  (16 of 512 px), medium (8, 4096) 4, 4, 4, 16 (12 of 1024 and 16 of 512), high (16, 4096) 4, 4, 16, 16, max (32, 8192) 16, 16,
  16, 64 (48 of 1024 and 64 of 512). Until D288's play test the plan counted one slot a lamp, so on medium and max the lamps
  filled the atlas and the flashlight had no shadow (the debug draw of the atlas showed it). The atlas is 16-bit depth (Godot's
  default), so 4096 is 32 MB and 8192 128 MB.
  **Measured** (D168, M1 Pro, Metal, Forward+, 1920x1080 windowed, vsync off, fake feed with 20 bots, high preset, `--bench=20 --bench-at=80,146` (medbay, 10 lamps in view), two runs each on a loaded machine, load average 11 to 13, so the runs differ by up to 25 per cent): 0 shadowed lights 146 and 135 FPS (7.1 ms a frame on average), 4 lights 119 and 137 (7.8 ms), 10 lights 104 and 136 (8.3 ms), a budget of 16 (10 lamps in line of sight, so also 10 shadows) 95 and 111 (9.8 ms). The 1% low (p99) is 88 and 73 FPS at 0, 82 and 49 at 4, 76 and 44 at 10. So ten shadows cost about 0.5 ms a frame over four and 1.2 ms over none here, inside the noise of the runs; the draw calls (about 870) and primitives (890k) are the same in every run.
  Flashlight spots: of the budget, at most `MaxBeamShadows` (2, unchanged at 10: the spots are few and each shadow is a full
  frustum over 24 tiles) went to other flashlights' spots; D288 dropped the cap: the spots take the budget before every lamp
  but the effects (section 8).
- **The Godot light** is a stand-in for the shadows, not a second light: energy `power * 0.5`, range `(light_range + 1)`
  tiles in metres, the replicated colour, no specular, at `WorldScale.LampHeight` above the level's floor (D119: 1.85 m under the 2 m ceiling, near the top of the wall like the fixtures; the light grid itself has no height, it is a 2D tile lattice sampled by world X and Z) and at the tile minus tg's offset.
  The grid term is the light the material receives without shadows (`EMISSION += ALBEDO * anomaly_grid_light`), the Godot lights add
  the shadowed, directional term on top.
- **The dev flashlight**: `LightLayer.ToggleFlashlight()` toggles a `SpotLight3D` child of the camera (range 20 m, angle 32
  degrees, energy 4, shadows on). Client only; P6 binds the key and the console command. The flashlight item lights the same
  spot (M4a, DV61): while the player's own entity carries a directional overlay light that is on, the spot is on in that light's
  colour, `(range + 3)` tiles long, energy 3 per unit of power; overlay lights are never shadow candidates. A flashlight's
  colour is drawn mixed 65 per cent of the way to white (`FlashlightBeam.Presented`, DV62), in the grid and in the spots.
- **The ambient on MetaStation** (`GameScene.MetaLightAmbient`): 0.008 (the owner, 2026-10-17: a place without light, a
  maintenance tunnel, is almost black, as tg's unlit turf is; it was 0.06). `ANOMALY_LIGHT_AMBIENT` overrides it.
- **The other flashlights** (DV62, `FlashlightBeam`): every other lit directional overlay light on the camera's level within
  24 tiles is a `SpotLight3D` of the same colour, range and energy. A carried one is at its holder's drawn pose (the session's
  `IPoseSource`), 1.15 m up (0.35 m lying) and 0.15 tiles ahead (the owner, 2026-10-29; it was 0.3, apart from the body),
  along the holder's replicated yaw (a capsule mob), tilted
  0.12 rad down; one on the floor is 0.12 m up along the way the server cast it (the negated offset: tg's
  `directional_offset` follows the parent's dir). They move every frame. Each has a glare at its start
  (`flashlight_glare.gdshader`, the spot's child, the owner 2026-10-29: one could not tell where another player shone): a quad
  turned to the eye, added to the view, a faint lit lens 0.25 m across from the side that grows to 1.6 m and brightens (the bloom
  spreads it) the nearer the eye is to the beam's axis, from just outside the 32-degree cone, so a flashlight aimed at one dazzles a
  little; it is drawn 0.25 m nearer the eye than the beam's start, so the holder's picture does not cut it, and walls hide it. Of the shadow budget, the nearest of them take up
  to 2 (never more than the budget), and the lamps' selection gets the rest; the others light without shadows.
- **Current flashlight presentation** (owner, 2026-10-24): all gameplay presets set `RenderPreset.Fog` to 0 to remove the haze and volumetric rendering cost. Both camera and other holders' spots use `SpotAngleAttenuation = 2` for soft edges on surfaces and zero volumetric fog energy. `GasFog` skips texture construction and uploads while volumetric fog is disabled; gas optics still tint and shorten surface lighting. The explicit dev light-room fog option remains available for diagnostics. The following fog description is historical; flashlight volumetric energy and the `ANOMALY_BEAM_FOG` override have been removed.
- **The beam fog** (DV62, `BeamFog`): the world environment's volumetric fog, set by the quality preset (`RenderPreset.Fog`:
  off on low, a 64-froxel grid on medium, 96 on high; `ANOMALY_FOG=0` turns it off, `ANOMALY_FOG_DENSITY` and `ANOMALY_BEAM_FOG` tune
  it): density 0.015, white albedo, anisotropy 0.3, 32 m long, no ambient, GI or sky in it. Only the Godot lights light it:
  every flashlight spot with `light_volumetric_fog_energy` 4, the shadowed lamps with 0.2 (the owner, 2026-10-17: thinner than first tuned, so the haze in plain air does not hide the view; a gas's own fog is unchanged and stands out more). The grid is the materials'
  emission and lights no fog. The froxel grid is coarse, so a beam's edge is soft. The dev light room (`LightRoomDemo`,
  `--fog 0|1|2`) shows a flashlight in its dark room from the side, head on and from behind (`beam_*.png`). On MetaStation
  (`--bench=20`, fake feed, RTX 4080 at 1920x1080) medium is 417 FPS with the fog and 418 without, high 396 and 397.
- **The gases in the beams** (DV62, `GasOptics`, `GasFog`): a visible gas (the replica's `GasAt`, version 5) has a strength
  per metre equal to the gas haze's (`GasLayer`, DV32: tg's overlay alpha reached over 4 tiles) and an optic of our own table
  by its overlay state: the share it scatters in its sprite's mean colour and the share it absorbs of the other colours
  (water vapour 1.0 and 0.1, freon 0.9 and 0.2, N2O 0.8 and 0.3, plasma 0.5 and 1.0, miasma 0.2 and 2.0, the rest between;
  every overlay tg has has a row, and the gameplay test checks the server's list against it). `GasFog` keeps one fog
  volume over the camera's level whose shader reads an RGBA half-float texture of the level's cells: a gas cell adds its
  fog (density times `FogGain` 2, albedo its scattered colour), a space turf subtracts the air's density (a beam out of a
  window into space shows nothing); the texture is uploaded at most 5 times a second. Every flashlight spot, the own one
  along the camera's heading, is tinted each frame by `GasOptics.Transmittance` over the cells along it (Beer-Lambert every
  half tile, the near part weighted more): its hue is what gets through, its brightness falls by at most half, its range to
  40 per cent in a gas that stops everything. Not seen: invisible gases (O2, N2, CO2), pressure, a vacuum inside the station
  (no per-cell pressure on the wire). The dev light room shows a pocket of steam, plasma and miasma over the beam
  (`beam_<gas>_side.png`, `beam_<gas>_head_on.png`).
- **The shader.** `anomaly_grid_light(world_pos)` (`light_grid.gdshaderinc`) fetches the four texels of the point's tile with
  `texelFetch` (corner (i, j) at world `X = origin.x + i * tile`, `Z = origin.z - j * tile`) and blends them bilinearly like
  tg's lighting object blends a turf's four corners, times `anomaly_light_strength` (default 0.75) plus `anomaly_light_ambient`
  (default 0.035, 0.035, 0.045). Fetching the corners by hand keeps the result independent of the filtering a global
  texture gets.
- **The dev scene** `godot/scripts/Light/LightRoomDemo.tscn` (a corridor of tubes, a dark room with one lamp, a hull under
  starlight, all through the real grid and shader, no server): `godot --path godot res://scripts/Light/LightRoomDemo.tscn --
  --shots DIR` saves three screenshots and quits; without `--shots` it is a free-fly view (WASD, mouse, F flashlight).

## 6. Tests (P7)

`tests/Anomaly.Server.Tests/Client/Light/`:

- `TgLightMathTests`: the falloff at corners worked by hand from `falloff_at_coord` (0.693814 at (0.5, 0.5), range 4;
  0.688195 at (1.5, 0.5), range 6; the clamp; range below 1; the starlight height giving exactly 1), the cone (inside half the
  angle uncut, the 30-degree fade 0.267467 at (2, 1), dark beyond, the angle delta folded past 180), `dir2angle`,
  `delta_to_angle`, the corner normalisation and the 1/64 rounding.
- `LightGridTests`: a lamp's corners by tg's falloff and rounding; power and colour per channel with the largest-channel
  normalisation; a wall blocking and a door opening and closing (`CellOpacityChanged`); a light on a wall cell; starlight
  next to a hull; the base light (per cell, edge blends, none on space turfs, adds to the corner light); off, empty and
  overlay sources; the visual offset; incremental equals full; the dirty rectangle and the half-float texels; the boot cost
  (printed, with a loose guard); the shadow selection (nearest, line of sight, budget 4 / 2 / 0).
- Not tested here: `ReplicaLightCells` and `LightLayer`'s event wiring need P5's replica (its version-3 members throw until it
  lands); the layer catches that and stays off.

## 7. The grid texture and the shader include (the interface P6 and P7 share)

- **The texture** (P7 writes it): one `RGBA16F` `Texture2D` of `(maxx + 1) x (maxy + 1)` texels per z level: texel
  `(i, j)` is the corner at the south-west of tile `(i + 1, j + 1)` (tile x covers corners x - 1 and x along that axis), RGB
  the light, A the base light's share. Set as **global shader uniforms** (`RenderingServer.global_shader_parameter_set`,
  declared in `godot/project.godot`'s `[shader_globals]` by P7) with these names (`godot/scripts/Shared/Light/LightGridUniforms.cs`):

  | Uniform | Type | Meaning |
  |---|---|---|
  | `anomaly_light_grid` | `sampler2D` | the corner texture of the level the camera is on |
  | `anomaly_light_grid_size` | `vec2` | its size in texels (`maxx + 1`, `maxy + 1`) |
  | `anomaly_light_grid_origin` | `vec3` | the world position of corner (0, 0), the south-west corner of tile (1, 1), metres (`TileSpace`: X 0, Z 0, Y the level's floor) |
  | `anomaly_light_tile_metres` | `float` | metres per tile (2, D74) |
  | `anomaly_light_ambient` | `vec3` | the dim ambient every lit material gets (P7 tunes it) |
  | `anomaly_light_strength` | `float` | the scale of the grid term (P7 tunes it; 0 turns the grid off for debugging) |

- **The include** (P7 wrote the body; the file is `godot/shaders/light_grid.gdshaderinc`). `res://shaders/light_grid.gdshaderinc`
  (`LightGridUniforms.IncludePath`) declares the globals above and one function every world material calls, P6's turf, wall, prop,
  door and piece shaders and, since M3 I1c, the mob and item billboards and the stand-in shapes
  (`picture_billboard.gdshader`, `placeholder_lit.gdshader`):

  ```glsl
  // The light the grid gives a surface point: the four corner texels around the point's tile position, fetched one by one and blended
  // bilinearly in the shader (tg's lighting object does the same over its four corner values), times anomaly_light_strength, plus
  // anomaly_light_ambient. world_pos: the fragment's world position (metres); only x and z are read.
  vec3 anomaly_grid_light(vec3 world_pos);
  ```

  A material adds `ALBEDO * anomaly_grid_light(world_pos)` to `EMISSION` (and keeps its own `ALBEDO` for the real shadowed
  Godot lights on top). A standing picture reads the grid where it stands (`MODEL_MATRIX[3]`, 0.3 m up), a wall face or a floor its own
  fragment. The body returned `vec3(0.0)` until P7 (the contracts step's include, so that P6 could compile against it); it does not any
  more. M3 I1c: the mobs' and items' billboards were the one world material without the term (a `StandardMaterial3D`), so a mob in a lit room
  was lit by the few shadowed `Light3D` nodes alone and looked dark and shaded while the props around it looked right (the owner's walk,
  2026-10-09); they take the term now, and so do the stand-in boxes and capsules. The real-server shots of the lamps, a dark maintenance room,
  space outside a window and the flashlight are in the I1c package's results (`i1c-shots/light-*.png`).
- **The layer node** P6's scene creates (`godot/scripts/Light/LightLayer.cs`, P7's): `LightLayer.Attach(IWorldFeed feed,
  Camera3D camera)` once the feed is playing, then it follows the replica on its own (it reads `ReplicaWorld`'s v3 cell and
  light state and events) and owns its Godot lights; `LightLayer.SetPreset(RenderPreset)` from P6's presets (D288: the light
  distance, the lamps with shadows, their shadow mode, the sun's shadow); `LightLayer.ToggleFlashlight()` from the dev key.

## 8. The Godot lights (D288, DV221)

Since D288 the grid is not what the client draws: `LightLayer.Dynamic` (on by default; the View tab's "Godot lights" switch and
`ANOMALY_LIGHT_GRID=1` turn the old grid back on for a comparison until the owner accepts the new light) draws the sources as
Godot lights. The grid keeps up with its sources (sections 3 and 4) and is the lamps' bounce: `anomaly_light_strength` is its
strength times `FillShare` (0.05, the owner's 2026-10-29, was 0.3; the View tab's slider, `ANOMALY_FILL`), so a surface a lamp does not face, or round a corner from
it, still gets a share of tg's light, which stops at the walls (a place without light stays dark); the vision floor is divided by
the share so mesons and night vision see as far as before. At a share of 0 the texture is uploaded dark. The server is unchanged.

- **A lamp per source.** Every live source of the camera's level that is not a flashlight's beam (those stay spots, section 5)
  is an `OmniLight3D`: tg's colour, `OmniRange` = tg's range in metres, energy = `light_power` x 3 (the owner's 2026-10-29, was 3.6) x tg's falloff at the source
  (`falloff_at_coord(0, 0)`, `1 - sqrt(height) / range`: a small light is dim where it stands, as in tg), `OmniAttenuation` 0.25
  (Godot's `(1 - (d/r)^4)^2 * d^-0.25` follows tg's linear falloff within a few per cent over the range), no specular. It stands
  at its tile less tg's visual offset, clamped to 0.375 tile (a wall lamp's half tile would put it on the wall's face), at 0.7 of
  the wall's height (under the fixture, so its ceiling is lit; a source with `light_height` below 0.5 at 0.25 m); a light a mob
  carries is at its drawn hand. A source's change, a switch off and a despawn reach its lamp at once (tg's switches and
  `flicker()` are instant); a source switched on appears with the selection, brought forward to the next frame.
- **Which are drawn** (`SelectLamps`, every 0.2 s): the sources within the preset's light distance + 2 tiles, by priority
  (`ShadowedLightSelector.Priority`, the owner, 2026-10-29) and nearest first within it, every light whose range (plus 2 m)
  reaches into the camera's frustum before every one that does not, whatever its kind (the owner, 2026-10-29; a turn of the view
  by 20 degrees brings the next selection forward), so the shadows go where they are seen (a lamp with a shadow keeps it until
  another is a tile nearer): first the effects (`/obj/effect` and `/obj/projectile`: tg's fire, sparks and flashes, the light
  of an explosion, which has none of its own), then the lights on bodies (the other holders' flashlight spots, all of them
  within the budget, then what a mob carries), then the lamps and the machines. The first `budget` get a shadow, and of those
  the preset's lamps with object shadows (4, 4, 8, 16) nearest the camera, those reaching the view first, hold the things and the
  characters, whatever their priority (an effect far off took them before); a light of range under 2.5 tiles is drawn only within
  10 tiles (a screen's or an APC's light lights next to nothing farther) and its shadow holds the walls and the doors alone. A
  light beyond the budget, small or big, is drawn only while the camera has a line of sight to its tile
  (`ShadowedLightSelector.HasLineOfSight`), since without a shadow its light crosses the walls and the shut doors (the owner's
  play test, 2026-10-29: small lights took no shadow and drew everywhere, and maintenance's dim lamps, 2.4 tiles, lit the tunnel
  behind a shut door; on the bench path that is also faster, low 225 FPS against 193, max the same). Lamps fade in and out over
  0.35 s (their shadows by `shadow_opacity`), and Godot's distance fade takes them out over the last 6 m of the distance.
- **Shadows of things.** The 3D models and pieces (`StaticProps`' MultiMeshes, render layer `LightLayer.ThingLayer`) cast
  shadows; the flat pictures (props, items) do not. A shadow pass draws whole chunks of them (a chunk is one MultiMesh), so they
  are the dearest shadows: only the shadows of the preset's lamps with object shadows hold them (`RenderPreset.ObjectShadowLamps`,
  by `ShadowCasterMask`; the other lamps' shadows hold the walls alone), and the sun's only with the preset's object shadows at
  "lamps and sun". An airlock or a blast door that is not glass (`PieceParts.BlocksLight`: tg's door is opaque shut, its glass
  ones, firelocks and windoors are not) is drawn in the walls' layer instead (`PropInstance.Wall`, its own MultiMesh per chunk), so
  every shadowed lamp's shadow holds it: on the things' layer a lamp beyond the nearest lit the room behind a shut door. And a shut
  door's cell blocks every lamp and flashlight whatever its model (`LightLayer.PlaceDoorBlocks`, the owner's play test, 2026-10-29:
  light came through the seams and onto the sides of a door's window): every cell with `DoorCellLight`'s code (tg's opaque turf with a
  door) gets a box drawn only into the shadows (`door_block.gdshader`, which the sun's shadow leaves out), whose half beyond the
  door's plane from each light is what that light's shadow holds, its face toward the light by the most the light's shadow bias
  allows, so that everything past the plane is the bias or more behind it. The lamps keep Godot's 10 cm bias (`LampShadowBias`)
  and see the box with its face 10 cm toward them (render layer `LightLayer.BlockLayer`): a 2 cm bias, tried so the far side of a
  16 cm leaf and the sides of its 5 cm insert went dark, striped every wall a lamp lit at a slant (shadow acne; the owner's play
  test, 2026-10-29). Their normal bias is three shadow texels (`LampShadowNormalBias`; Godot's one left faint stripes close up).
  The flashlights (Godot's 3 cm spot bias) see the same instances with the face 1 cm toward them (`LightLayer.BeamBlockLayer`):
  10 cm left the door's window insert black in the beam. A mob's
  picture casts one with the preset's character shadows; the own body's picture in first person is not drawn but still casts its
  shadow (`Billboard.ShadowOnly`, the owner, 2026-10-29; on its own render layer, `LightLayer.OwnBodyLayer`, which the own
  flashlight's shadow leaves out: held before the body and shone down, it drew the body's shadow ahead of it), turned across the view as the camera's right (`EntityLayer.MobShadows`; off on low, since a moving mob
  makes the lamps near it redraw their shadows every frame): the billboards turn to the main camera in the shadow passes too
  (`MAIN_CAM_INV_VIEW_MATRIX`), so the shadow is the picture the eye sees and the picture never shades itself. Glass, a model's
  glass insert and a window's panes, casts none (`prop_atlas` drops those pixels in a shadow pass, told by the pass's eye not
  being the main camera's), so the sun comes through the windows; only its see-through pixels: a shut airlock's insert under tg's
  opaque fill overlay casts one (the owner, 2026-10-29: the lamps behind a shut door lit patches through it). A picture turned to the eye (the billboards, `ANOMALY_PICTURE_LIGHT`;
  `prop_atlas`' billboards, items and resting billboards) is lit `0.5 + 0.5 * N.L`: a light behind it lights it at half.
- **The own flashlight** (D288, after the owner's play test): its spot is the camera's child at the hand (0.35 m right, 0.4 m
  down, 0.15 m ahead, mirrored when the lit flashlight, tg's `-on` icon state, is in the left hand; the dev flashlight too), aimed
  at the crosshair 8 m ahead: from the eye its shadows fall behind what it lights and show nowhere. It does not light the
  first-person held things (`HeldLayer`, its `LightCullMask`). It is always drawn with its shadow, outside the budget and
  before every other light (the owner, 2026-10-29), in third person too, where it is a spot at the drawn body's hand. While the Godot lights draw the sources no flashlight is in the
  grid (`GridLight`), so the bounce does not fill its shadows, and the other holders' spots reach as far as the lamps. Its beam
  sways (`FlashlightSway`, the owner after S.T.A.L.K.E.R.'s torch): a slow drift of 1.5 degrees standing, a swing toward a turn
  of the view (0.04 s of the turn rate, at most 10 degrees; the owner's values, 2026-10-29) on an under-damped spring that settles back with a little overshoot,
  and a sway in step with the head bob, 1.2 degrees at a walk and more running (`CameraLayer.Stride`); the View tab has sliders
  for the four (a negative turn sway makes the beam lag instead). A light that is on in a hand (tg's `-on` look: a flashlight, a
  desk lamp) glows in first person (`held_item_lit`'s `lit`: its picture lights itself, its bright pixels most), and a
  flashlight is held lens forward (`held_items.json`: pitched 100 degrees; tg's south picture points the lens down).
- **The sun** is a `DirectionalLight3D` along the sky's sun (`Starfield.SunDirection`, SSsun's azimuth), colour
  (1, 0.96, 0.88), energy 0.7 (the owner's, 2026-10-29; was 1.4), shadowed by the preset (`SunShadowPlan`). The tiles' MultiMeshes cast double-sided shadows, so the
  ceiling (a face turned down) shades the rooms, and a hidden ceiling still casts its shadow (`turf_kit`'s vertex keeps it in the
  orthographic shadow pass). Every lit world material has `world_light.gdshaderinc`'s `light()` (plain Lambert diffuse with
  Godot's shadow): the sun reaches a point only within 0.75 of the shadow's distance (`anomaly_sun_reach`), where its shadow
  decides (windows let it in), and beyond that only where the sky is open, so the inside of the station is never sunlit past its
  shadow. Under a covered cell the band just below the ceiling (from 0.1 to 0.25 m, wider farther off, where the coarser splits
  leak more) gets no sun: the shadow leaks at the joint of a wall and the ceiling by its bias and showed as a strip of light in
  the dark, and the sun through a window reaches that high only at the window. The sun is drawn only while a cell open to the sky lies within its shadow's reach of the camera (checked twice a
  second over the mask below; it fades over 0.5 s), so deep inside the station its shadow pass is not drawn at all.
- **The light of space** (`SkyLight`): `anomaly_exterior` is one byte per tile of the camera's level, 255 where the cell is open to
  the sky (`ILightCells.IsOpenToSky`: not opaque and no metal ceiling, `ReplicaLightCells` reads the ceilings), updated per cell
  from the replica's cell changes. A material adds `ALBEDO * anomaly_sky_light(world_pos, world_normal)`: the cell half a tile along
  its normal (a hull's outer face reads the space before it), blended between tile centres, times an ambient cube by the normal:
  tg's `COLOR_STARLIGHT` from above (0.9) and the sides (0.7), the magma planet's red (1, 0.3, 0.08) from below (1.1) and a little
  from the sides (0.2), scaled by 0.2 (the owner's, 2026-10-29; the View tab's slider). It replaces tg's starlight cells (section 1) and equals six unshadowed
  directional lights at no per-light cost.
- **The presets** (`RenderPreset`, `GraphicsSetting`; the options' Graphics tab offers the preset and every setting, and a
  changed setting makes the custom set, `quality=custom` with `gfx.ID=value` lines):

  | Setting | low | medium | high | max |
  |---|---|---|---|---|
  | Light distance (tiles) | 12 | 18 | 24 | 32 |
  | Lamps with shadows | 4 | 8 | 16 | 32 |
  | Shadow atlas (px) | 2048 | 4096 | 4096 | 8192 |
  | Shadow softness | hard | low | medium | high |
  | Lamp shadows | dual paraboloid | cube | cube | cube |
  | Object shadows | off | nearest lamps | nearest lamps | lamps and sun |
  | Lamps with object shadows | 4 | 4 | 8 | 16 |
  | Character shadows | off | on | on | on |
  | Sun shadow (atlas, splits, metres) | 2048, 2, 40 | 4096, 2, 60 | 4096, 4, 100 | 8192, 4, 150 |
  | Ambient occlusion | off | off | medium | high |
  | Indirect light (SSIL) | off | off | off | on |
  | Floor reflections (screen-space, on wet and polished floors: `TurfStyle.Polished`) | off | off | on | on |
  | Bloom | on | on | on | on |
  | MSAA | off | 2x | 4x | 4x |
  | Render scale | 75% (FSR) | 100% | 100% | 100% |
  | View distance (m) | 60 | 100 | 200 | 300 |

- **Tuning.** `ANOMALY_LAMP`, `ANOMALY_SUN` and `ANOMALY_SKY` override the lamps' energy per power, the sun's energy and the sky's gain,
  `ANOMALY_FILL` the bounce's share (the grid's light under the lamps, 0.05), and the View tab has sliders for them with the count
  of lamps drawn and shadowed. On MetaStation's fake feed (medbay at 80,146, arrivals at 44,131, the APC room at 93,132) the
  lamps were tuned against the grid: a room lit by one lamp matches it, a corridor with lamps nine tiles apart is a little darker
  than the grid with the bounce (113 against 134 in its brightness; the grid saturates within a few tiles of a lamp), and
  medbay, with its lamps close together, brighter (Godot's lights add up). After the play test the owner set the defaults darker
  (2026-10-29: lamps 3 per power, sun 0.7, sky 0.2, bounce 0.05; the light strength 2.2, cap 1.3, model glow 0.5, lamp glow 4 and
  ambient 0.001 as they were).
- **Measured** (section 5's bench, `--bench=15`, fake feed, RTX 4080 SUPER, Vulkan
  Forward+, 1920x1080, after a warm-up preset): low 232 FPS (32 lamps drawn, 4 with shadows), medium 167 (37, 8), high 122 (45,
  16), max 78 (78, 32); the grid with D114's lights on the same path: medium 274, high 239, max 242. Without the shadows of
  things the presets were 235, 195, 150 and 131; with things in every lamp's shadow and the sun's, 169, 121, 71 and 56; the sun's
  shadow holding the things costs about 3 ms at high (4 splits over 100 m draw most of the chunks), so only max has it. The
  rest of the difference to the grid is the lamps' shadows; the sun's shadow without things costs little and is off deep
  inside; dual paraboloid shadows instead of cubes gain 2 per cent at medium and 18 at high (so only low has them). After the
  owner's play test of 2026-10-29 (shut doors in every shadow, the own body's shadow, the lights reaching the view first, 8 and 16
  lamps' shadows holding the things on high and max), on the same path with the user's server idle beside it: low 215, medium
  122, high 93, max 52; without the lights reaching the view first medium 136 and max 64 (the bench flies at 82 m/s and turns
  often, so the lamps entering the view keep taking fresh shadows; walking they change far less), and max with 4 lamps holding
  the things 70.
