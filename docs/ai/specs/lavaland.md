# Lavaland: the plan

Status: decided (D304, the owner, 2026-11-02): all eight stages, starting now. It revises D108's "no lavaland" and retires DV236
(the mining and labour shuttles land in empty space) where the level is converted. Progress is kept in the "Progress" section below.

## What tg has, against the port (snapshot 1d99bd7f)

| Part | tg | Port today (`docs/ai/specs/generated/types-index.tsv`) |
|---|---|---|
| `Lavaland.dmm`, the fixed part of the level | 432 KB, 532 types, about 5400 instances | 4622 instances of live types, 706 inert: stone tiles, lava, catwalks, tables, ore boxes, conveyors, cameras |
| The caves, generated each round | `datums/mapgen`, 1117 lines | The generators are mostly inert (68 inert, 25 live) |
| Turfs | lava, chasm, basalt, mineral walls | Lava 12 and chasm 10 all inert; mineral 38 live, 28 inert; asteroid floors 14 live, 24 inert |
| Mining (`modules/mining`) | 14603 lines, 947 procs | 4 of 947 procs; the ore machines, the accelerator (4 types) and its 22 modkits are inert |
| Fauna (`basic/lavaland`, `/mob/living/basic/mining`) | 7166 lines, 49 types | All inert |
| Megafauna | 2781 lines, 17 types | All inert or base types |
| Weather (`datums/weather`) | 1463 lines, 18 types | All inert |
| Ruins | 39 templates, 1125 lines of their own code | Not loaded |
| Ghost roles (`mob_spawn/ghost_role`, ash walkers among them) | 97 types | All inert |

## The stages

Estimates use the rate of `docs/ai/specs/gaps.md` (about 0.1 h a proc), with procs guessed from lines where tg's count is not to hand
(about 15 lines a proc, as `modules/mining` has). They are rough and leave out the fixes each stage brings.

| Stage | What it does | Estimate |
|---|---|---|
| **L1 The level** | `Lavaland.dmm` converted and loaded with tg's traits and real docks (retires DV236); the cave generator; lava (burns, and glows in 3D) and chasms (the `chasm` component: falling); basalt and volcanic mineral walls; outdoor air and light | 25-35 h |
| **L2 Mining and ore** | Pickaxes, drills, shovels, mining scanner, ores, ore boxes and bags, the kinetic accelerator (absorbs J6, part of D298), the redemption and stacking machines, the equipment vendor | 40-50 h |
| **L3 The base and the labour camp** | The mining base's machines as mapped, the gulag teleporter, the item reclaimer, labour points | 15-20 h |
| **L4 Ash storms** | `datums/weather`: tg's storms with their warnings and damage, with a look in 3D (radiation storms come free as an event) | 10-15 h |
| **L5 Fauna** | Goliaths, watchers, legions and the rest on the AI core C1 already ported, with their loot | 40-55 h |
| **L6 Tendrils and loot** | Tendrils and their spawning, necropolis chests, crusher, trophies, accelerator modkits | 30-40 h |
| **L7 Megafauna** | Bubblegum, the colossus, the drake, the legion, the hierophant, blood-drunk miner and the elites, with arenas | 30-45 h |
| **L8 Ruins and ghost roles** | The 39 lava ruins through tg's seeding, ash walkers and other ghost roles | 40-60 h |

L1 to L4 is a real miner's shift (about 90-120 h): fly down, dig, bring ore back, flee a storm. It is empty of danger until L5, so
L1-L5 (about 130-175 h) is the first version that plays like tg's. The whole is about 230-320 h.

## Risks

- **Level order.** Lavaland would load after CentCom (DV239's rule: the station stays on z 1), so the reserved level moves from z 3 to
  z 4. Tests and dev commands that name z 3 need changing, as they did for CentCom.
- **The random stream.** Cave generation draws a lot from the world's RNG. CentCom alone broke 16 seed-sensitive tests; expect more, and
  a test run for each stage. The generator could draw from its own stream to limit this (a deviation to record).
- **Boot time and memory.** A third full 255 x 255 level, with generated caves, adds to every server start and every Meta test (the Server
  suite already takes about 24 minutes). Measure in L1. The client must not draw the level for those on the station; the renderer's
  level filter does this for the reserved level.
- **3D look of a tile game.** Lava and chasms have no model; open caves with low ceilings need the sky and the planet handled, since
  the sky now shows the lava planet below the station. Decide the look in L1 with a screenshot for the owner.
- **Overlap with D298.** J6 (mining gear), P4 (the economy, for mining points) and the bots' and fauna's AI touch the same files, so
  these should not run at the same time as L2 or L5.

## Answers (D304)

1. **Where in the order?** Now, before D298's open packages.
2. **How far?** All of L1-L8.
3. **Mining points without the economy?** Yes, as in tg.

## Progress

- **L1a, the level and its caves (2026-11-02).** `Lavaland.dmm` is converted with Meta and loads after CentCom with tg's
  ZTRAITS_LAVALAND; the mining, common mining and labour docks are the real ones. The areas' cave generators run: tg's
  `cave_generator` and biomes are translated, and rust-g's `cave_system_generator_generate` and `dbp_generate` (rust-g 7.0.0, tg's
  pinned version, and the dbpnoise crate it calls) are ported in `Builtin/rust_g.mapgen.builtin.cs`, each on a stream of its own
  (the cave from one draw of the world's, the noise from its seed string), as rust-g has its own generators. tg's lava rivers (`setup_rivers`, `spawn_rivers`, `turf.Spread`) run between the generation and the population. The flora, geysers, ore vents and fauna the population places are
  still inert (L2, L5); the mining mob respawner comes with L4 and L5.
- **L1b, lava, chasms and the open sky (2026-11-02).** tg's lava turf (`lava.dm`, but the plasma lava Lavaland lacks) burns
  and ignites what stands in it, lights its edge tiles, and takes a heatproof lattice and a lit cigarette; the mining and lava
  catwalks and the permanent fire overlay are translated. The chasm turf and component (`chasm.dm` twice) drop what steps in
  to the level below or, on Lavaland, into the depths (death for the living); a catwalk stops it. The fishing hooks of both
  wait for fishing; the immerse element and lava's emissive overlays are BYOND presentation (DV12). Tg's outdoor areas get no
  default ceiling, and a planet's turfs (planetary air, or any turf on a mining-trait level) do not vent through an open
  roof: with the first change alone every Lavaland tile vented to space and SSair took its whole share every tick.
- **L1c (2026-11-02):** the smooth basalt floors (the shale and siderite biomes) are live.
- **L1d (2026-11-02):** the ore in the rock. The random rocks (`/turf/closed/mineral/random/*`, all but the jungle's) and
  `SSore_generation` are live: the volcanic, siderite and shale rock of the caves gets its ore by its depth from the open caves
  (`calculate_rock_edges`, `randomize_ore`), the veins spread (`spread_vein`: cluster, scatter, plain, branch) and gibtonite
  deposits appear. The ore vents (`/obj/structure/ore_vent`, their sizes, minerals, wave defence and boulders) are L2.
- **L1e (2026-11-02):** the boss arenas' stone tiles (their random sprites, and the lava, chasms and slowdown they keep off their
  turf), the necropolis walls and Lavaland's water (smoothed with the basalt, siderite and shale around it, with their outlines)
  are live. The water's own mechanics (wetting, putting out fires, swimming, scooping, fishing) wait for the water package, as
  every other water type does.
- **L1f (2026-11-02, D305):** no ceilings on Lavaland's surface (tg's outdoor areas, the owner's choice); its own dark ash sky with
  no sun or light of space on tg's ZTRAIT_NOPARALLAX levels, named in the join map (protocol 60, DV256).
- **L2 is split** (2026-11-02) by what each part stands on:
  - **L2a** (done 2026-11-02): the material container, remote materials, the ore box, the base mineral machine, the ore
    redemption machine and the ore silo (its log, bans, ID requirement and radio announcements), with their windows (`OreBox`,
    `OreRedemptionMachine`, `OreSilo` and the shared material bar), all live. The redemption machine's alloys (the Alloys tab and
    Smelt) are deferred leaves until research is ported (the smelter's techweb and SSresearch's designs, as the manufactorio
    lathe's techweb); the silo's machine pictures are their icon and state (DV257). The autolathe, recycler and protolathe stand
    on the same container and come later with their own packages.
  - **L2b** (done 2026-11-02): the stacking machine and its console, the processing unit (the furnace) and its console, the
    unloading machine, and the labour camp's collection unit, point claim console (the parole, the release announcement and
    the one-prisoner launch check on the labour shuttle) and points checker, with their windows (`StackingConsole`,
    `ProcessingConsole`, `LaborClaimConsole`), all live. The furnace's alloys wait for research, as the redemption machine's
    do: choosing one stops it, as tg's does on an unresearched alloy. tg's stacker makes its store with `new type(src, 0)`,
    which the stack refuses with a trace; the port keeps it.
  - **L2c** (done 2026-11-03): the ore vents (the base vent, the station's tapped starter vent, the random, boss and debug vents,
    SSore_generation's vent sizes and minerals and its boulders each fire, the ore budget lists, the mining epicenter), the
    boulders and their artifact, labour camp and shabby kinds (cracked by pickaxes into ore, thrown or used on lava as a
    sinking platform), the labour camp's work pit, the boulder retrieval matrix, the refinery and the smelter (with their
    chemical boosts and plumbing) and the boulder beacon, all live. Waiting: the wave defence after a vent's second scan (the
    NODE drone and the spawner's fauna, L5; the boss vents' megafauna, L6), the vent's GPS signal (L2d), the skills, the bank
    card's messages, particles and emissive glows (DV12); the artifact boulder's relic and the refinery's industrial waste stay
    inert (research, chemistry). Random vents come with the ruins that place them (L8); a vent's top is not trimmed for a mob
    behind it (DV261).
  - **L2d**: marker beacons, the remaining mining items, coins and the mining order console with its points.
- **L3, the labour camp part** (gaps LC1, 2026-11-16): the gulag teleporter and its console, the item reclaimer, the camp's beacon,
  the prisoner management console and the prison's plate press and produceporter, live with their windows; the labour shuttle
  stands at its dock (shuttle travel). Left in L3: the mining base's machines as mapped.
- **Still open in L1:** the biome-replacing rock (ruins, L8). The rest of `Lavaland.dmm`'s inert things belong to other packages
  (the base's machines, L3; paperwork, conveyors, cameras and hydroponics are inert on Meta too).

## Questions for the owner

1. **Where in the order?** Before or after D298's open packages (J4-J6, S3, S4, P1-P4). Recommended: finish J4 and J5 (cheap, unrelated), then
   L1-L4, with J6 inside L2.
2. **How far?** Recommended: commit to L1-L5 now, and decide L6-L8 after playing it.
3. **Mining points without the economy?** The vendor spends mining points, not bank money, as in tg, so it should work before P4. Recommended: yes.
