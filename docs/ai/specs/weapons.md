# M7x Weapons: design and plan

Written 2026-10-22 against main (protocol 33). tg = `../tgstation/code` (snapshot 1d99bd7f). Paths without a prefix are in the repo.
Next free ids at planning: D179, DV94, protocol 34. The plan's open questions were decided under D178 (D179, section 6).

## 0. What already exists (verified)

- Projectile base, whole (D165): `src/Core/Tg/modules/projectiles/projectile.cs` (1550 lines): `fire` (:697), `aim_projectile` (:1297),
  `calculate_projectile_angle_and_pixel_offsets` (:1481, angle from icon-x/icon-y), `process_movement` (:914), `Bump` (:299), `impact` (:320,
  tg's distance zone roll `ran_zone(def_zone, ...)` at :349 = tg projectile.dm:503), `process_hit_loop` (:371), `select_target` (:455),
  `can_hit_target` (:491), `scan_moved_turf` (:574), `on_entered` (:604), `CanPassThrough` (:615, passes anything in `impacted`), `on_hit` with
  sounds and impact visuals, ricochet, reflect. Hit chain to `bullet_act` of atom/obj/living/carbon/human with armour, dismemberment
  (not-translated.md section 34). Only `beam/emitter` and `energy/nuclear_particle` have their own files.
- Type stubs (var blocks) for every tg type are generated: `Generated/Types/obj.item.gun.g.cs` (332 types), `obj.item.ammo_box.g.cs` (182),
  `obj.item.ammo_casing.g.cs` (276), `obj.projectile.g.cs` (393), `obj.item.melee.g.cs` (105), `obj.effect.projectile.g.cs`; flash, restraints,
  firing pins in `obj.item.g.cs`, recharger in `obj.machinery.g.cs`, outfits in `datum.outfit.g.cs` (e.g. `datum_outfit_job_security` :2594 with
  `suit_store = disabler`, `l_pocket = handcuffs`, `r_pocket = flash/handheld`). `/obj/item/gun` itself is an empty skeleton
  (`datums/components/fullauto.cs:18`). So a subtype whose parents' procs are translated works for free (contraband mateba/deagle included).
- Outfits equip (`datums/outfit.cs:233`), but jobs are a stand-in: every player is an assistant (`Infrastructure/CrewStandIn.cs`); no dev verb
  equips an outfit (`src/Server/DevCommandLine.cs:764-798`).
- Lockers: `closets/secure/security.cs` has the `PopulateContents` of Meta's lockers, not the `populate_contents_immediate` that hold the guns
  (tg security.dm:17 captain e_gun, :46 HoP e_gun, :72 HoS e_gun/hos, :100 warden compact shotgun). Boxes: `storage/boxes/security_boxes.cs`
  (firingpins, flashes, handcuffs, ...; not the shell boxes, tg security_boxes.dm:164-210). Belts: no `belt/security/full` (tg belt.dm:266), no
  holsters (tg storage/holsters.dm, 108).
- Carbon cuff procs are `Dm.NotTranslated` (`modules/mob/living/carbon/carbon.cs:155-182, :469, :897` = tg carbon.dm:188-250, :630, :1119).
  `flash_act`, `get_eye_protection` exist; `is_eyes_covered` is NotTranslated (`living_defense.cs:178`).
- Protocol: `ActionMessage` (`src/Protocol/MessagesV2.cs:403`) carries IconX/IconY and DV30's AimZone; the server builds tg's params in
  `ServerHost.World.cs:ApplyClick` (~:472) via `ClientSeam.ClickParams` (`Infrastructure/ClientSeam.Click.cs:63`). MotionInput carries only yaw
  (`Codec.V2.cs:59`). The server knows each capsule mob's continuous position and yaw (`LocomotionSeam.TryGetState`, `LocomotionSeam.cs:140`).
  Camera shake: `CameraShakeMessage` (`MessagesV21.cs`), fed by tg's `shake_camera` (`mob_helpers.cs:186`).
- Client: the pick ray is engine-free (`godot/scripts/Shared/Play/WorldPicker.cs`, ray.OriginEast/North/Up, Dir*), `PlaySession.Click`
  (`Shared/Play/PlaySession.cs:251`). Billboard math engine-free: `Shared/Icons/BillboardMath.cs` (`ChooseDir`, `ChooseLyingDir`, `RayStanding`,
  `RayLying`, `SpriteRect.AsItem`), body zones `Shared/Play/BodyZoneAim.cs` (state names -> zone, opacity via the compositor). Mob scale 0.8,
  tile 2 m, eye 1.3 m standing / 0.4 m lying (client settings, `WorldScale.cs`, `CameraLayer.cs:24`). Projectile and effect drawing: DV77/DV78
  quads (`Render/BeamSegment.cs`, `EntityLayer.UpdateBeam` :689). Hit particles `Render/HitBurst.cs`. Held item in first person:
  `Hud/ViewModel.cs` (D126: the hand slot's 2D picture, so the gun's overlays show for free).
- Tests: `tests/Anomaly.Core.Tests/Tg/UnitTests/projectiles.cs` (PARTIAL: `gun_go_bang` waits), `M7/ProjectileTests`,
  `Integration/ProjectileGameplayTests`, `Integration/MeleeAimTests` (prints the zone map of tg's human picture, a model for the server test).
- not-translated rows that this milestone closes or retargets: section 34 (on_ricochet auto aim, the other beams), `drag_drop.dm:129/:143`
  (MouseDown/Up never called: full auto, row tagged M7), `embedding.dm:161` (setup_shrapnel), `bones.dm:162` (recoil with a fractured arm),
  `slash.dm:261` (las_cauterize), `toolbox` cuffable_item and `wirecutters` cuffsnapping (M9 restraints -> M7x), `emitter.dm:402` (energy gun
  into the emitter: optional, keep for later).

## 1. The station set (exact types; parents in brackets)

Sources: MetaStation.dmm, tg security.dm lockers, spawners `effects/spawners/random/armory.dm` (Meta: disablers, e_gun, laser_gun, shotgun,
dragnet) and `contraband.dm:57` (armory loot), outfits `jobs/job_types/{security_officer,head_of_security,warden,detective,captain}.dm`,
belts `belt.dm:266`, `holsters.dm:62`.

- Guns [`/obj/item/gun`]: energy [`gun/energy`]: `disabler`, `e_gun`, `e_gun/hos`, `laser`, `laser/carbine`, `laser/pistol`, `laser/practice`,
  `laser/carbine/practice`, `ionrifle`, `temperature`, `temperature/security`. Ballistic [`gun/ballistic`]: `revolver` -> `revolver/c38` ->
  `c38/detective`, `revolver/russian`; `shotgun` -> `shotgun/riot`, `shotgun/doublebarrel`, `shotgun/automatic` -> `automatic/combat` ->
  `combat/compact`; `gun/ballistic/automatic` -> `wt550`, `battle_rifle`, `pistol` -> `pistol/contraband` (contraband armory loot 80/181, and
  tg's `gun_go_bang` test needs `pistol`). Free via parents: the loot's `revolver/mateba`, `pistol/deagle/contraband`. Out (ROADMAP): grenade
  launcher, syringe gun, `e_gun/dragnet` (spawns, but its snare projectile stays NotTranslated: see risks), the rocket casing.
- Energy casings [`ammo_casing/energy`]: `disabler`, `disabler/hos`, `laser`, `laser/hos`, `laser/practice`, `lasergun`, `lasergun/carbine`,
  `lasergun/pistol`, `lasergun/carbine/practice`, `ion`, `ion/hos`, `temp`, `temp/hot`, `electrode` (ROADMAP; ammunition/energy/stun.dm).
- Ballistic casings [`ammo_casing`]: `c38`, `c357`, `c9mm`, `c10mm`, `c46x30mm`, `shotgun` (slug) -> `shotgun/beanbag`, `/rubbershot`, `/buckshot`.
- Magazines [`ammo_box` -> `ammo_box/magazine` -> `/internal`]: `internal/cylinder` -> `rev38`, `rus357`; `internal/shot` -> `riot` (rubbershot),
  `dual` (beanbag), `com` -> `com/compact`; external `wt550m9`, `m9mm`, `m10mm`, `m38` (the battle rifle's); `ammo_box/speedloader` -> `c38`,
  `c357`. Boxes: `storage/box/rubbershot`, `lethalshot`, `beanbag`, `slugs`.
- Projectiles [`/obj/projectile`]: `beam` -> `beam/laser` (+`laser/hos` if the casing names one), `laser/rapid`, `laser/rapid/practice`,
  `beam/practice`, `beam/disabler` (+`/hos`); `ion`; `temp`, `temp/hot`; `energy` -> `energy/electrode`; `bullet` -> `bullet/c38`, `a357`,
  `c9mm`, `c10mm`, `c46x30mm`, `shotgun_slug`, `shotgun_beanbag`, `pellet/shotgun_buckshot`, `pellet/shotgun_rubbershot` (the exact names come
  from each casing's `projectile_type`).
- Pins: `firing_pin`; `firing_pin/clown`, `clown/ultra`, `clown/ultra/selfdestruct` (the contraband pistol's 10%, pistol.dm:38).
- Batons (weaponry/melee/baton.dm, 1046): `melee/baton` (the detective's), `baton/telescopic` -> `telescopic/gold` (captain), `baton/security`
  -> `security/loaded`, `loaded/hos`, `security/boomerang` (+loaded), `security/cattleprod` (tg's baton test). Flash (assembly/flash.dm, 422):
  `assembly/flash`, `flash/handheld`. Restraints (items/handcuffs.dm, 726): `restraints`, `handcuffs`, `handcuffs/cable` (+`white`, the colours),
  `cable/zipties` (+`used`), `legcuffs`, `legcuffs/beartrap`.
- Holders: `belt/security/full`, `belt/holster/detective/full` (2 speedloaders c38 + the revolver), `box/firingpins`, `box/handcuffs`,
  `box/flashes`, `box/zipties`; `/obj/machinery/recharger` (game/machinery/recharger.dm, 227; 19 on Meta).

## 2. D177 design: a shot hits where it lands

### 2.1 The aim on the wire (protocol 34, slice A)
- `ActionMessage` gains, for `Click` and the new `MouseDown`/`MouseDrag` kinds, an optional aim (a flag bit): `AimEast`, `AimNorth` (int, Motion
  units 1/256 tile, absolute), `AimUp` (u16, mm above the level's floor), `AimYaw` (u16, 65536 per turn clockwise from north, as MotionInput.Yaw),
  `AimPitch` (i16, 65536 per turn, +up, clamped to +-16384). The client fills it from the camera ray it already casts (`WorldPicker` ray, metres
  -> units) in `PlaySession.MakeTargeted`; the codec bounds-checks (Limits).
- New `ActionKind.MouseDown`, `MouseDrag` (aim update, at most one per input frame while a button is held), `MouseUp`: the host calls tg's
  `/client/MouseDown`, `MouseDrag`, `MouseUp` (translated, `_onclick/drag_drop.cs`, never called today) so tg's `automatic_fire` works.
- Server (`ApplyClick` and the new handlers): the origin is trusted only near the authority: horizontal distance to `LocomotionSeam.TryGetState`
  position <= 0.75 tile, height within [0, MaxEyeHeight 1.9 m] (lying: [0, 0.8 m]); otherwise the server's own position and the standing/lying
  eye height replace it. The aim goes into tg's params text as one port key, `anomaly-aim=<east>,<north>,<up>,<yaw>,<pitch>` (tile pixels and
  degrees), so it rides tg's own chain untouched: `ranged_interact_with_atom` -> `try_fire_gun` -> `fire_gun(params)` -> `process_fire` ->
  `fire_casing` -> `throw_proj` -> `aim_projectile(params2list(params))`; the full-auto component stores the MouseDown/MouseDrag params the same way.

### 2.2 Angle, height, slope (slice A, `projectile.cs`)
- Units: tile pixels, 32 per 2 m tile, heights too (eye 1.3 m = 20.8 px). New hashed port vars on `obj_projectile`: `dm_aimed` (bool),
  `dm_origin_x/y` (absolute tile pixels of the 2D origin), `dm_origin_h`, `dm_slope` (height px per horizontal px).
- `aim_projectile` (:1297): when the modifiers hold `anomaly-aim`, the 2D start is the aim origin's tile (if it is not the source's tile or an
  adjacent open one, the source's tile) and `pixel_x/pixel_y` its offset in the tile (capsule mobs stand off-centre, tg uses the mob's pixel
  offsets); `calculate_projectile_angle_and_pixel_offsets` (:1481) returns the aim yaw as tg's angle (same convention: clockwise from north) instead
  of the icon-x geometry; `set_angle(angle + deviation)` stays, see 2.3. `dm_origin_h` = aim up, `dm_slope` = tan(pitch').
- Height at a 2D point p: `h(p) = dm_origin_h + dm_slope * |p - origin|`. `set_angle` (:788) on ricochet/reflect re-bases the origin to the
  current point and height (keeps the slope; a mirror or wall bounce is horizontal).
- Floor and ceiling (DV, owner question 1): `process_movement` (:914) checks each step; when h leaves [0, the wall height: 2 m, one tile, D119's default;
  `WorldScale.DefaultWallHeight`] the shot impacts the turf it is over (`impact(loc)`, tg's on_hit on a turf: dent, impact effect, sound) at the
  crossing point.
- Unaimed shots (no `anomaly-aim`: the emitter, turrets, mobs' AI, `fire_projectile`, a gun fired by a mob without a client): keep tg whole (2D
  hit, tg's def_zone and `ran_zone`), drawn level at the middle of a mob picture (12.8 px = 0.8 m). No deviation for them.

### 2.3 Spread and recoil in 3D (slice A helper, used from slice B on)
- tg rolls stay as they are and in their order: `process_fire`'s `sprd` (gun.dm:586, `rand(0,1)` sign times half the spread),
  `process_burst` (:495-498), `fire_casing`'s variance (`_firing.dm:7-10`), `pellet_cloud`'s per-pellet spread. Each ends as aim_projectile's
  `deviation` d (degrees). For an aimed shot with d != 0 the server draws one more value, phi = `rand(0, 359)` from the world RNG (deterministic,
  after tg's draws), and turns the aim by |d| about it: yaw += |d| cos phi, pitch += |d| sin phi. So each gun's tg cone becomes a 3D cone of the
  same half-angle (DV: one extra RNG draw per shot changes later rolls versus a 2D run; documented).
- Recoil is tg's camera kick (`simulate_recoil` gun.dm:536 -> `recoil_camera` mob_helpers.dm:212, the client's pixel_x/y pushed opposite the shot
  by `strength` tiles, back over `backtime`). Translate `recoil_camera` into the existing CameraShake path with a version 34 `Kind = Recoil`
  flag; the client turns a recoil path into a camera rotation instead of an eye shift: the backward component pitches the view up, the sideways
  component (tg's `recoil_deviation` +-20 deg) yaws it, 3 deg per tile of tg offset (owner question 2), and returns over tg's backtime. The kicked
  crosshair then aims the next shot, which is how tg's recoil spoils follow-up shots. The player's `min_recoil_multiplier` preference reads its
  default (DV41 pattern).

### 2.4 The crossing test on a mob (slice A, the heart of D177)
- Hook: one place, `can_hit_target` (:491), living branch, after tg's own checks (firer, faction, density, dead-unless-original) and only for
  `dm_aimed` projectiles. It replaces tg's three 2D shortcuts for aimed shots: `direct_target` -> TRUE, `hit_prone_targets` auto-aim at
  `MAX_RANGE_HIT_PRONE_TARGETS`, and `body_position == LYING_DOWN` -> FALSE. Since every path (Bump :299, `scan_moved_turf`, `select_target`,
  `on_entered`, `fire`'s point-blank `direct_target`) goes through it, the rule holds everywhere.
- Geometry (engine-free, shared with the client): link `godot/scripts/Shared/Icons/BillboardMath.cs` (+ `Affine.cs`, `Raster.cs`) into
  `Anomaly.Core.Tg.csproj` the way `tests/Anomaly.Server.Tests.csproj:30` links Shared, so server and client place pictures with one function
  (`SpriteRect.AsItem`, the scale 0.8, feet on the floor). Mob centre C = its continuous position (capsule) or tile centre + pixel offsets.
  - Standing: the picture is a billboard through C facing back along the shot's horizontal heading u (the shooter sees it so; also right for
    ricochets). `RayStanding` with the 3D ray (origin, u, slope) gives the picture pixel (icon-x, icon-y); the frame is
    `BillboardMath.ChooseDir(mobYaw, C, viewer = C - u)`.
  - Lying: `RayLying` against the flat picture at the floor (as the client draws it); the lying transform is undone as `BodyZoneAim.ZoneAt`
    does; frame `ChooseLyingDir` (limp from stat).
- Which part: the server walks the carbon's `BODYPARTS_LAYER` overlay images (tg's `get_limb_icon` states `<limb_id>_<zone>[_<gender>]`,
  hands count as their arms: `BodyZoneAim.ZoneOfState`, topmost first, with the image's pixel offsets) and tests opacity against a **limb mask
  table**: 32x32 bits per (icon file, state, dir), generated by a new mode of `tools/Anomaly.Tools.Icons` from the converted body-part icons and
  committed as `src/Core/Tg/Generated/BodyMasks.g.cs` (or an embedded binary). The server still reads no .dmi at run time (D104, owner question 3).
  Unknown state: the human mask of the same zone. Non-carbon living (Ian, mice, bots, cyborgs): the table holds the opaque bounding box per
  state and dir; a crossing inside it hits with tg's def_zone (chest).
- Hit: `def_zone` = the crossed zone; `impact` (:349) skips `ran_zone` for aimed shots (tg's zone miss roll by distance dropped, D177; also
  `bullets/rifle.dm:160`'s own roll if a rifle bullet is ever in). `get_zone_with_miss_chance` does not exist in this snapshot; tg's only zone
  rolls on this path are those two `ran_zone` calls. tg's `check_hit_limb_zone_name` (projectile.cs:165) then picks the limb (a missing limb
  gives tg's chest fallback).
- Miss: `impacted[WEAKREF(target)] = TRUE` and FALSE: tg's own never-double-hit list; `CanPassThrough` (:615) lets the projectile through the
  mob it was blocked by (a failed `step_towards` retries from the border on the next loop of `process_movement`) and it flies on to what is
  behind. `select_target`'s `pick(considering)` among several mobs becomes the nearest crossed one along the heading (DV).
- Non-mob targets keep tg's 2D collision (walls, windows, grilles, airlocks, tables via PASSTABLE, machines, structures, items lying around):
  no height test, except the floor/ceiling rule of 2.2.

### 2.5 Height on the client (slice A, protocol 34)
- New entity field `NetField.Shot`: `OriginX`, `OriginY` (int, absolute tile pixels), `OriginH` (i16, 1/16 px), `Slope` (i16, 1/4096), `Angle`
  (u16). Sent for `/obj/projectile`, `/obj/effect/projectile/*` (tracer, muzzle, impact) and `/obj/effect/temp_visual/impact_effect*` and the
  firing effect spawned by a shot (the effect copies its projectile's values; an impact effect gets the impact height and slope 0). The
  replicator writes it from the hashed vars; unaimed shots send h = 12.8 px, slope 0.
- The client draws a moving projectile at its node's drift progress projected onto the true line (origin + u * ((p - origin) . u)) at height
  `h(p)`; tracers' two ends and effects use the same formula. This revises DV78 (the bolt was on tile centres at 16 px).

### 2.6 Deviation entries (DEVIATIONS.md)
- DV94 The shot's 3D aim: the client's eye origin (validated) and direction ride the click params (`anomaly-aim`); tg's angle from icon-x/screen-loc
  is replaced by the aim yaw; the start pixel offsets come from the continuous position; the vertical slope is port-only.
- DV95 The 3D crossing: for aimed shots `can_hit_target` tests the mob's drawn body parts; tg's direct-target hit, prone auto-aim, lying skip,
  `pick` among mobs and `impact`'s `ran_zone` are replaced; a miss marks `impacted` and flies on.
- DV96 Spread as a 3D cone (one extra RNG draw) and recoil as a camera rotation (CameraShake `Kind = Recoil`).
- DV97 A shot that reaches the floor or the ceiling hits the turf there (if the owner agrees).
- DV98 The battle rifle's scope (`datums/components/scope.dm`, 273) zooms the first-person field of view by tg's `range_modifier` instead of
  shifting a 2D view (slice D).
- DV78 updated (2.5). Unaimed shots are not deviations.

## 3. Slices (dependency order)

Agent fit: A, B, C need judgement in tg's core paths -> Opus. D, E, F are mechanical against a spec -> Sonnet. E has no dependency and can run
beside A/B (one Opus + one Sonnet at a time). F can start after A and run beside C/D.

### Slice A. Aim and crossing (D177 core, protocol 34) - Opus, ~1.6k lines ours
- tg: `projectile.dm` (1430) touched procs only: `aim_projectile`, `calculate_projectile_angle_and_pixel_offsets`, `set_angle`,
  `process_movement`, `can_hit_target`, `impact`; `mob_helpers.dm:212` `recoil_camera` (translate, ~25).
- Ours: `src/Protocol` (MessagesV34.cs, Codec.V34.cs: aim fields, Mouse kinds, `NetField.Shot`, CameraShake kind; `ProtocolVersion.Current = 34`,
  docs/ai/specs/protocol.md section), `ServerHost.World.cs` ApplyClick + MouseDown/Drag/Up, `ClientSeam.Click.cs` params key,
  `Server/Replication/Replicator.cs` Shot field, `projectile.cs` (2.2-2.4), new `Infrastructure/ShotGeometry.cs` (the crossing),
  `tools/Anomaly.Tools.Icons` mask mode + `Generated/BodyMasks.g.cs`, `Anomaly.Core.Tg.csproj` links, client `PlaySession`/`WorldPicker`/`GameScene.
  Interaction` (aim on clicks, Mouse kinds while held), `EntityLayer.UpdateBeam` height, `CameraLayer` recoil rotation.
- Dev verb `outfit <type>` (`DevCommandLine.cs`, `ServerHost.Dev.cs`: `equipOutfit`, tg's admin "Select equipment") for tests and playtests.
- Tests: Core `M7x/ShotGeometryTests` (front/side/back frames: head at the head row, chest, between the legs passes, over a shoulder passes,
  lying body hit only when the ray crosses it, an off-centre capsule), `M7x/AimTests` (params -> angle and slope, deterministic scatter cone,
  floor impact); codec round-trip and limits tests (`tests/Anomaly.Server.Tests/Protocol`); Integration `AimedShotGameplayTests`: two real
  clients, Alice's click with an aim on Bob fires a test projectile through a dev hook (`fire_projectile` with aim params) - head aim damages the
  head, an aim beside Bob hits the wall behind (a dent), the replica's Shot field gives the client the right height.
- Sounds/icons: none new (the effects are D165's).

### Slice B. Gun core, energy guns, pins, recharger - Opus, tg ~2.4k
- tg: `projectiles/gun.dm` (734; all but suicide `handle_suicide` :662, mail :717-734, fantasy :136-149, attachments `add_seclight_point`/
  `add_bayonet_point`/`clear_suppressor` stay NotTranslated per ROADMAP), `ammunition/_ammunition.dm` (175), `_firing.dm` (102),
  `guns/energy.dm` (375), `guns/energy/stun.dm` (73, disabler), `energy_gun.dm` (:1-110: e_gun, hos), `laser.dm` (:1-130: laser, pistol,
  practice, carbine, carbine/practice), `special.dm` (:1-38 ionrifle, :338-360 temperature), `ammunition/energy/{_energy 13, laser 200 (the
  station casings), stun 46, special 97 (ion, temp)}`, `projectile/beams.dm` (:1-60 beam, laser, rapid; :135-200 practice, disabler),
  `projectile/energy/{_energy 8, stun.dm :1-66 electrode}`, `special/ion.dm` (16), `special/temperature.dm` (95), `pins.dm` (:1-116 base and
  test_range, :146-190 clown), `game/machinery/recharger.dm` (227), the lockers' `populate_contents_immediate` (security.dm:17, :46, :72, :100;
  armory1-3 and tac :273-338 for free), `bones.dm:162` and `slash.dm:261` rows.
- Ours: `modules/projectiles/gun.cs` (moves the skeleton out of fullauto.cs), `ammunition/*.cs`, `guns/energy*.cs`, `projectile/beams.cs` (extend),
  `projectile/energy/stun.cs`, `special/ion.cs`, `special/temperature.cs`, `pins.cs`, `game/machinery/recharger.cs`, `security.cs`.
- tg tests: `projectiles.dm` `projectile_movetypes` (all projectile types, generated), pin test cases from pins behaviour (no tg test).
  Core: disabler 3 hits -> stamina crit (M2 stamina), laser burn vs the sec vest's laser armour (M5 `run_armor_check`), no pin -> `fail_message`,
  e_gun mode switch, recharger charges 1 notch per tg cycle, cell empty -> dry fire. Integration `EnergyGunGameplayTests`: an officer equipped by
  `outfit /datum/outfit/job/security` draws the disabler from suit storage and disables Bob; the recharger shows charging.
- Visuals (D142): energy gun `update_overlays` (charge bars `[icon_state]_charge[n]`, mode overlays `[icon_state]_[select_name]`, energy.dm
  update_overlays), `inhand` charge states, recharger `recharger-charging/full/power/open` and its light, pin icons; all through the hand slot
  and ViewModel. Sounds: each casing's `fire_sound` (sound/items/weapons/{laser,taser2,ionrifle,pulse...}), `dry_fire_sound`, mode switch
  `toggle_sound`, recharger ping. Check the converted sound set has them.

### Slice C. Ballistics: base, revolvers, pistols, magazines, bullets - Opus, tg ~2.5k
- tg: `guns/ballistic.dm` (933; suppressors and sawing-off in, bayonet/seclight out), `ballistic/revolver.dm` (:1-230: revolver, c38,
  detective, russian spin and the self-shot roulette; mateba free), `ballistic/pistol.dm` (:1-60 pistol, contraband), `ballistic/automatic.dm`
  (:1-35 base only; wt550 and battle rifle go to D), `boxes_magazines/_box_magazine.dm` (326), `ammo_boxes.dm` (79), `speedloaders.dm`
  (:1-60), `external/pistol.dm` (:1-80), `internal/{_internal 11, _cylinder 68, revolver 31}`, `ammunition/ballistic/{revolver 121, pistol 87}`
  (station casings), `projectile/bullets.dm` (28), `bullets/{revolver 192, pistol 76}` (station bullets), casings' `bounce_away`
  (_ammunition.dm:168), embedding's `setup_shrapnel` row (`embedding.dm:161`, `projectile.dm:1410-1430`), `on_ricochet` auto aim
  (projectile.dm:445-459), `storage/holsters.dm` (108), `belt.dm:266` security/full PopulateContents (here or E).
- tg test: `projectiles.dm` `gun_go_bang` (finish `Tg/UnitTests/projectiles.cs`). Core: c38 rubber vs .38 lethal, a bullet's wound through the
  vest (M6), the speedloader fills the cylinder, the Russian revolver's spin, magazine eject/insert on the pistol, casings eject.
  Integration `BallisticGameplayTests`: the detective's revolver from his holster, reload from a speedloader, a shot that breaks an arm.
- Visuals: ballistic `update_icon_state`/`update_overlays` (`[icon_state]_mag`, `_bolt`, empty states, `mag_display_ammo` counts), magazine
  and speedloader ammo states (`[base]-[ammo_count]`), spent casing sprites on the floor. Sounds: `fire_sound`, `rack_sound`, `load_sound`,
  `eject_sound`, `bolt_drop_sound`, casing drop sounds, dry fire.

### Slice D. Shotguns, pellets, full auto, the battle rifle - Sonnet, tg ~1.7k
- tg: `ballistic/shotgun.dm` (:1-130 shotgun, riot, automatic, combat, compact; :338-400 doublebarrel), `internal/shotgun.dm` (69),
  `ammunition/ballistic/shotgun.dm` (station shells of 245), `bullets/shotgun.dm` (station of 207), `datums/components/pellet_cloud.dm` (418),
  `datums/components/fullauto.dm` (330, `automatic_fire`), `ballistic/automatic.dm` (:66-91 wt550, :342-470 battle_rifle), `external/smg.dm`
  (:1-20 wt550m9), `external/rifle.dm` (:18-35 m38), `ammunition/ballistic/smg.dm` (:1-10), `datums/components/scope.dm` (273, DV98),
  shell boxes `security_boxes.dm:164-210`.
- Tests: Core: riot shotgun pump (`rack`) then fire, reload shell by shell to 6, buckshot pellets each test the crossing (some hit, some
  miss), double-barrel breaks open, WT-550 full auto fires every 0.3 s while held and stops on MouseUp, magazine swap, battle rifle degradation
  and recharger repair. Integration `ShotgunGameplayTests` (pump and reload through real clicks), `FullAutoGameplayTests` (MouseDown/Up).
- Visuals/sounds: shotgun `update_icon_state` (pump), shell sprites, `pump` sounds (`sound/items/weapons/gun/shotgun/rack.ogg`, insert),
  WT-550 `wt550-[n]` magazine overlay, battle rifle degradation overlays, scope zoom.

### Slice E. Baton, flash, cuffs - Sonnet, tg ~2.3k (independent; can start first)
- tg: `weaponry/melee/baton.dm` (:1-456 baton and telescopic incl. gold, :457-772 security incl. loaded/hos, :809-910 cattleprod and boomerang),
  `assembly/flash.dm` (:1-308; cyborg/memorizer/armimplant/hypnotic out), `items/handcuffs.dm` (:1-178 restraints and handcuffs, :201-390 cable
  and zipties, :409-571 legcuffs and beartrap), carbon cuffs (`carbon.dm:188-250` get_attached_restraint, cuff_resist, uncuff, clear_cuffs;
  `:630` update_handcuffed; `:1119` set_handcuffed), `living_defense.dm:109` is_eyes_covered, `toolboxes/_toolbox.dm:49` cuffable_item and
  `wirecutters.dm:59` cuffsnapping rows, `belt.dm:266`, boxes PopulateContents.
- tg tests: `baton.dm` (230, all cases: sec, prod, det; left/right, on/off, combat or not), `combat_flash.dm` (41), `combat_cuffs.dm` (18).
  Core: beartrap snaps a walker's legs, cuff resist timer, the boomerang thrown returns. Integration `RestraintGameplayTests`: baton knockdown,
  flash blinds (the client's flash overlay), cuff a downed man, he cannot use his hands, resist out.
- Visuals: baton `[icon_state]_active`/`_nocell`, telescopic extended states, flash `flash`/`flashburnt` and `flick("flash2")`, the victim's
  `flash_act` overlay (existing), handcuffs/legcuffs worn overlays (`update_worn_handcuffs` exists, legcuffs), beartrap armed/closed states.
  Sounds: `sound/items/weapons/egloves.ogg`, baton toggle `sparks`, `sound/items/weapons/flash.ogg`, `handcuffs.ogg`, `cablecuff.ogg`,
  beartrap snap.

### Slice F. D176 client visuals and the closing scenario - Sonnet (client) + main session (scenario), ~1.2k lines client
- After A (Shot field) and B (energy kinds); bullets after C. See section 4. Then `M7xScenarioTests` (MetaStation, a fixed seed): officer from
  his outfit disables a target, laser burns and bullet wounds through armour, head shot vs past-the-body miss, riot shotgun pump and reload,
  magazine swap, no-pin gun refuses, baton, flash, cuffs; run twice -> same state hash. CHANGELOG, ROADMAP status, protocol.md, release 0.8.0
  (or 0.7.x) when the owner asks. Full Core and Server suites here only.

## 4. D176 client visuals (slice F; client only, `godot/scripts/Render`)

- One generic rule, no per-type table: a projectile's colour is its `light_color` (it has a light, NetField.Light) else its `color` else the
  mean opaque colour of its own icon state (`Shared/Icons/SpriteColors.cs`); its kind comes from the type path prefix.
  - `beam/*` laser bolt: an elongated additive capsule (core white, rim in the colour), 1.2 m long along the heading (0.5 m at first; the owner, 2026-11-11), with an `OmniLight3D`
    of tg's `light_range` 1 / `light_power` 1.4 (laser `COLOR_SOFT_RED` #FA8282, disabler `LIGHT_COLOR_BLUE` #6496FA, carbine practice
    `COLOR_DARK_RED`). `beam/practice`, `laser/rapid` the same mesh, shorter.
  - `energy/electrode`: a jittering spark ball, `COLOR_YELLOW` #FFFF00. `ion`: a pulsing sphere with a crackle shader (sprite colour).
    `temp`: frost-blue sphere with trailing particles; `temp/hot`: orange (sprite "lava").
  - `bullet/*`: a thin bright tracer streak (warm white, 0.6 m, unlit, no light); `pellet/*` thinner.
  - Hitscan tracers (`/obj/effect/projectile/tracer/*`, the emitter): a lit cylinder between the two 3D ends (2.5) instead of DV77's quad.
- Muzzle flash: tg's firing effect (`temp_visual/dir_setting/firing_effect`, `_blue`, `_red`, miscellaneous.dm:60-93, 3 ds) triggers a 3-frame
  star quad + a short light (yellow-white, blue, red by state). For the own mob the flash and a recoil kick play on the `ViewModel` gun at its
  muzzle point (the gun picture's rightmost opaque pixel row centre); for others at the shooter's hand height along the shot.
  `/obj/effect/projectile/muzzle/*` (hitscan) the same.
- Impacts: tg's impact effects (`temp_visual/impact_effect/*` from `on_hit`, `/obj/effect/projectile/impact/*`) -> `HitBurst` in the colour
  plus a 0.15 s light at the Shot height; on a mob the existing blood burst. Wall dents: check the existing client draws tg's dent overlay on
  the wall face; if not, a small dark decal at the impact height on the 3D wall face.
- Held gun: `ViewModel` already shows the hand slot's picture with tg's overlays (ammo count, mode, empty states) once B/C translate
  `update_overlays`; add the fire kick (a short back/up offset on the own firing effect) and the muzzle flash anchor. No 3D gun model (D104,
  D126); the owner may add models later (D176).
- Trails and marks (the owner, 2026-11-11): every projectile draws a trail from the gun to its head that ends where it hit and fades after it
- Flight and calibre (the owner, 2026-11-12): a projectile is drawn moving at its mean speed along its line (`ShotLook.Fly`), not standing and jumping with the tiles the server names; the own muzzle is put beside the start of the line (the eye of the click) as it stands beside the eye now (`EntityLayer.GunPoint`); a bullet and its trail are scaled by `ShotLook.Calibre` (0.7 pistol and SMG, 1.4 rifle, 1.7 slug, 2.2 heavy).
  (`ShotTrace`, `ShotLook.Trail`); an impact is a star, sparks, a light and a dark mark on the surface for some seconds (`ShotMark`, DV399).
- Recoil: `CameraLayer` applies the CameraShake `Recoil` kind as rotation (2.3).
- Tests: engine-free parts only (colour choice, height formula, the projected drift) in `tests/Anomaly.Server.Tests/Client`; a smoke screenshot
  (`godot/tools/smoke.sh`) of a laser, a disabler, a bullet and an impact. No Windows build (owner).

## 5. Risks

- R1 Limb mask fidelity: the server table must match what the client draws (species, gender, digitigrade, prosthetics, clothes do not change
  zones). Mitigation: the Integration test compares the server's zone grid with `BodyZoneAim` on the replica (MeleeAimTests pattern).
- R2 Continuous position vs tile `loc`: a capsule mob stands off its tile centre and tg hits on tile entry; the plane test uses the true
  position, so a shot can pass "through" the mob's tile and miss correctly, but a mob straddling two tiles is only tested from its `loc` tile.
  Accept; note in DV95.
- R3 Determinism: the extra scatter draw and the mask table must be hashed/fixed; the limb table is committed, never read at run time.
- R4 Latency: the client's origin is ahead of the server's by the RTT; the 0.75 tile tolerance covers a sprint (~0.3 tile at 100 ms).
- R5 Full auto needs held-button input (MouseDown/Drag/Up) for the first time; throttle MouseDrag to the input frame rate (Limits).
- R6 Map spawns that stay inert by ROADMAP: two DRAGnets (armory spawner), the grenade launcher, two syringe guns; firing a DRAGnet reaches
  `Dm.NotTranslated` (net snare). Keep them inert (no pin flag?) or translate the dragnet's two projectiles (~120 lines): owner question 4.
- R7 Size: tg subset ~9k lines (ROADMAP said ~8k) plus ~1.6k of our own for D177 and ~1.2k client.

## 6. Decided (D179, my choices under D178)

1. A shot whose 3D line reaches the floor or the ceiling stops there and hits that turf (DV97).
2. Recoil in first person: 3 degrees of camera kick per tile of tg's recoil offset, back over tg's backtime (DV96; since 2026-11-11 out in 0.05 s,
   back smoothly, kicks adding up to 12 degrees).
3. The body parts' pixels reach the server as a committed table generated from tg's body-part icons by `tools/Anomaly.Tools.Icons`; the server
   still reads no icon at run time (D104 kept).
4. The DRAGnets stay inert (ROADMAP leaves the dragnet out); the Makarov pistol (`pistol`, `pistol/contraband`) joins the set (tg's
   `gun_go_bang` test, Meta's contraband armory loot); the WT-550 stays in (ROADMAP lists it).
