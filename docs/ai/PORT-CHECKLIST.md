# What is not ported yet: the tg port checklist

Status: recounted 2026-11-21 (1.9.0-pre-alpha). A list of everything in tg's MetaStation round that
the port does not have yet, to tick off as it is ported. Things that are not on MetaStation, cut or obsolete are listed apart
in section 12; they will most likely not be ported.

Sources:
- tg's snapshot `1d99bd7f` (2026-10-04) in `../tgstation/`;
- the translation tracker `docs/ai/specs/generated/types-index.tsv` (recounted 2026-11-21): 46,051 tg types, of them 38,275
  live and 7,766 inert; 65,645 tg procs, of them 45,586 translated (69%) and 432 stubbed;
- MetaStation's map (`_maps/map_files/MetaStation/MetaStation.dmm`): how many of each type it places;
- the client's tgui windows (`godot/scripts/Shared/Tgui/TguiWindows.cs`): 308 of tg's 444 interfaces;
- `docs/ROADMAP.md`, `docs/ai/DECISIONS.md`, `docs/ai/specs/not-translated.md` for what is planned, deferred or cut by the owner.

How to read a row:
- `[ ]` not ported, `[x]` ported. Tick a row when its mechanic runs on the server, is drawn and heard in the client (D142) and
  has its window; add the version or the commit after it. Do not delete rows: tick them.
- `Meta N`: how many MetaStation places. `win:` tg's tgui windows it needs. `x/y live`: the family's live types in the tracker.
  A type is live when every tg proc on it, and on its ancestors below the nearest live one, is declared (types-converter.md
  4.2). Live is not the same as done: leaves can still sit in `not-translated.md` and `live-partial.txt`.
- `(owner: ...)`: the owner already decided when or whether; `(D123)`: the decision.
- The ticks are kept by hand, in the package that ports a row. The numbers are recounted by
  `tools/checklist/refresh-port-checklist.py` after every package's regen and at every merge train's end: the header, section
  0, every "`path` n/N" (a type family that starts with `/`: its live types; a directory of tg's `code/`: its procs declared)
  and section 13's windows drawn. By hand: the `type` rows of `types-index.tsv` whose path starts with the family, column 5
  (`live`/`base` against `inert`); a directory: the `proc` rows by their `file:line`, column 6 (`no` = not declared).

---

## 0. Overview by tg area

Share of tg's procs declared in each area of `code/` (translated or stubbed), recounted 2026-11-21 by
`tools/checklist/refresh-port-checklist.py`. All of `code/`: 46,018/65,645 (70%). An area at 100% can still have leaves in
`not-translated.md`; the rows below say what of it runs.

| State | Areas (procs declared / tg procs) |
|---|---|
| Done (100%) | art 98/98, brain traumas 216/216, detective work 36/36, dna 104/104, dna infuser 174/174, economy 45/45, experisci 127/127, forensics 37/37, grenades 141/141, hallucination 236/236, holiday 110/110, lost crew 38/38, mafia 124/124, materials 197/197, meteors 78/78, mutations 235/235, newscaster 71/71, plumbing 148/148, porta turrets 105/105, power 994/994, rcd 182/182, robot items 198/198, security levels 20/20, silicons 528/528, skills 13/13, station goals 94/94, telecomms 123/123, transit tubes 74/74, water structures 60/60, weather 66/66, wounds 182/182 |
| Mostly ported (70-99%) | paperwork 405/407, transport 366/368, station traits 136/138, language 64/65, pai 121/123, manufactorio 95/97, memory 183/188, diseases 299/308, hydroponics 513/529, items 1,239/1,280, cards 59/61, recycling (disposal pipes) 224/232, movespeed and actionspeed 27/28, devices 698/724, structures 1,329/1,383, reagents 1,958/2,041, uplink 55/58, `mob/living` base 595/631, surgery dir (bodyparts, organs) 1,523/1,616, tgui 134/144, AI modules 66/71, MODsuits 817/882, storage 199/217, doors 341/372, weaponry 387/426, atmospherics 1,051/1,178, AI controllers 752/853, wires 170/195, cargo 346/398, tools 224/259, martial arts 140/164, stacks 338/398, bitrunning 233/277, lobby menu 26/31, mapping 324/393, machinery 1,410/1,715, implants 131/160, clothing 960/1,173, projectiles 943/1,153, instruments 79/97, food and drinks 788/969, turfs 595/761, modular computers 584/751, effects 679/876, carbon 893/1,182, status effects 556/755, `__HELPERS` 809/1,108, vehicles and mechs 639/876, jobs 183/251, `_onclick` 675/935, photography 75/106, library 161/228, events 398/564, computers 369/525 |
| Partly (2-69%) | subsystems 1,091/1,578, shuttles 274/401, elements 804/1,200, vending 95/148, components 2,250/3,579, basketball 29/51, actions 136/246, basic mobs 1,500/2,756, client 367/737, wiremod 473/1,063, mining 412/947, keybindings 28/71, research 322/993, mob spawners 34/116, spells 123/426, holodeck 16/65, antagonists 875/3,715, species (`carbon/human/species_types`) 55/273, admin 177/1,643, simple animals 47/448, awaymissions 13/136, admin verbs 85/975, mapfluff 23/268, quirks 10/287 |
| Not started (0-1%) | explorer drones 3/207, achievements 1/72, fishing 10/787, minimap 1/127, buildmode 0/102, CTF 0/101, deathmatch 0/113, religion 0/283, TCG 0/67, tutorials 0/48 |

---

## 1. Gaps in what is already ported

Live mechanics that still miss a window, a leaf or a piece the owner put off.

- [x] Atmos filter and mixer windows (the trinary filters and mixers are live, 77/77; win: AtmosFilter, AtmosMixer) (engineering E1, 11266e76)
- [x] Portable pump, portable scrubber and pipe scrubber windows (live; win: PortablePump, PortableScrubber, PipeScrubber) (engineering E3)
- [x] Gas tank window: the release pressure (tanks live 34/34; win: Tank; tg's window has no valve: the valve is the alt-click and the action button, both live) (engineering E3)
- [x] Gravity generator window (the generator is live; win: GravityGenerator) (engineering E4)
- [x] Supermatter monitor (the crystal is live; win: Supermatter, NtosSupermatter) and the delamination counter display (incident_display, Meta 5) (engineering E2; the counters start at 0 each round, DV220)
- [x] IV drip window (gaps G1) (the drip is live; win: IVDrip)
- [x] Combination safe (gaps G1) (`/obj/structure/safe` is live; win: Safe)
- [x] Loot panel (gaps G1): tg's list of a tile's contents (`/datum/lootpanel` is live; win: LootPanel)
- [x] Memory panel (gaps G4: the 54 memories and the HUD button; win: MemoryPanel; DV191 retired by gaps S3: the captain's code folder). Left: the tattoo kit and the engravings (with the art), the memories of callers not yet live
- [x] Floors with their own procs (gaps G2; the engine floor family, 35 types, Meta 496 tiles: engineering E4): wood (Meta 480), carpet (223), grass (68), large, circuit, the reinforced plating (`plating/reinforced`) and others (Meta's 24 floor types, 1,045 tiles); volcanic mineral turfs (9), plastitanium walls (5). Left inert, none on Meta: the tram's high-traction floor, the rusted and meat platings, the random, ore, gibtonite and biome rocks, the explosive plastitanium wall, snow, ice and lavaland floors
- [x] Meta's untranslated mapping helpers and spawners (gaps G3; the dead body placer's cadavers came with gaps OG1; left: the holidays wait for `check_holidays`) (ROADMAP 3, M4 follow-up): request console helpers (Meta 64), mail sorting, `dead_body_placer`, Ian's birthday and new year, `turn_off_lights_with_lightswitch`, xmas trees, costumes, maintenance MOD, `round_default_module`
- [ ] The deferred leaves of ported mechanics: `not-translated.md` (222 sections; the sites tagged M10+) and `live-partial.txt` (429 lines); each names its milestone
- [ ] Owner-deferred pieces of ported milestones: screentips (M9 G, owner; the object examines and `examine_more` are done, D288: X1, X2, X3); chat icons, icon2html (owner: later); the fireman carry and piggyback (M9 F, owner); leaning on walls, the table slam and the table flip (done, gaps L1); the hand-item emotes' leftovers
- [x] M10x's slices (D281, D282, `docs/ai/specs/lobby.md`; done, released in 1.1.0-pre-alpha): G the Esc menu, bug reports and tg's admin help, D the character's looks, E the preview, I the closing; not now (owner): tg's server game preferences (stashed, D282), the Quirks, Loadout and Personality tabs, the polls (the Antagonists tab: syndicate A3, D316)
- [x] M9x station life (D228, D249): eating, drinking and hunger with the nutrition HUD (done, gaps S1), vending machines (done, gaps S2: free until the economy, DV225), paper, pens, clipboards and the hand labeler (done, gaps S3), the PDA's job apps (done, gaps S4; the cargo apps, the budget orders, shipping and bounty board: gaps P4)
- [ ] The core version's left-outs (ROADMAP 4): languages (alpha; done in gaps L2), proximity voice (D254; TTS and speech to text came with D307), full rollback beyond stage 1 (D38)

---

## 2. The round: Dynamic, events, goals

Already ported: the ticker, the lobby and the round start, late join, the emergency shuttle, the round end and a clean restart
(M10 F, G, H). The round is extended play: no antagonist, no director, no objectives (ROADMAP 4).

- [ ] **Dynamic** (`controllers/subsystem/dynamic`, `/datum/dynamic_ruleset` 25/50): the threat budget, ruleset picking, midround and latejoin injection, the admin panel and the tester (win: DynamicAdmin, DynamicTester) (syndicate A3: SSdynamic with tg's tiers (this snapshot has tiers, not a threat budget), the traitor rulesets roundstart, latejoin, midround and mass, extended, the panel and the tester; the other rulesets wait for their antagonists, DV346)
  - [ ] Roundstart rulesets: traitor (done, syndicate A3; extended too), blood brother (done, syndicate X4), changeling, heretic, malf AI (done, silicons SI8), wizard, blood cult, revolution, nuclear operatives (done, syndicate N1b) (and clown ops: done, N3a), spies, blood worm, meteor, nations, extended
  - [ ] Midround rulesets from ghosts: wizard, nukies (done, syndicate N1c), blob, xenomorph (done, gaps X5), blood worms, nightmare, space dragon, abductors, space ninja, revenant, space changeling, paradox clone, voidwalker, fugitives, morph, slaughter demon; pirates, spiders
  - [ ] Midround rulesets from the living: traitor (done, syndicate A3, with mass traitors), malf AI (done, silicons SI8), blob, obsessed
  - [ ] Latejoin rulesets: traitor (done, syndicate A3), changeling, revolution
- [ ] **Random events** (`modules/events` 398/564; `/datum/round_event_control` 106/114; SSevents, the event controls and the lone operative and scrubber overflows live since syndicate N1c, the rest set aside, DV380)
  - [x] Anomalies (`/datum/round_event_control/anomaly` 13/13; the anomaly objects themselves are live, `/obj/effect/anomaly` 21/21, D166; gaps E1a: every anomaly event but the weather one, with the bluespace and ectoplasmic anomalies; gaps E2a: the weather one)
  - [x] Meteors: meteor waves (5 kinds), stray meteor, dark matt-eor, space dust, immovable rod (gaps E2b; the meteor types since D299; the changeling's meteor waits for the changeling)
  - [x] Creatures: carp migration, mice migration, sentience (2), rabbit release, wisdom cow, spacevines (kudzu) (gaps E4: carp and mice migration, sentience (2), the wisdom cow; gaps E4b: the space vines; gaps R3: the rabbit release)
  - [x] Machinery (gaps E1a and E1b; the scrubber overflows: syndicate N1c): vent clogs (4), scrubber overflows (4), electrical storm, grid check, communications blackout, processor overload, camera failure, brand intelligence (vending), gravity generator blackout, supermatter surge (2), grey tide (doors)
  - [x] People: heart attack, disease outbreak (2), brain trauma, mass hallucination, fake virus (gaps E4; the disease outbreaks came with the diseases, gaps VI1; the brain trauma event's trauma came with the brain traumas, gaps B1)
  - [x] Space and weather: radiation storm (gaps E2a), radiation leak, ion storm (AI laws, silicons SI11), wormholes, aurora caelus, earthquake, sandstorms (2, gaps E2b), portal storms (2; gaps E2c with the troopers; the Nar'Sie storm's constructs wait for the cult), the tram malfunction (gaps E2c)
  - [x] Cargo and command: stray cargo (2), shuttle loan, shuttle catastrophe, shuttle insurance, market crash, bureaucratic error, false alarm, night shift (gaps E1b: market crash, bureaucratic error, false alarm, night shift; gaps E3: the cargo and shuttle ones)
  - [ ] Antagonist events: lone operative (done, syndicate N1c), wizard events (33: summon guns and magic and the rest), bitrunning glitch, dynamic tweak
  - [x] Holiday events: Easter, Santa, Valentines, spooky (see Holidays) (gaps R3)
- [x] **Station traits** (`datums/station_traits` 136/138, 81 traits; win: StationTraitsPanel; gaps R2: all of them with the lobby's sign-up buttons, the round-start report's lines and the admins' panel, and the cargo gorilla's, bridge assistant's and Pun Pun's jobs; left (not-translated.md 184): seven traits wait out of the random pool for their systems (the shuttle firesale, the radioactive nebula, the closet anomaly, the scryers, pet day, the veteran advisor's and the human AI's jobs), Ian's extra lives, the hangover's drunkenness, the nebulas' parallax; the icemoon's bright day and forested are planetary), for example: galactic grant, lucky winner, filled and empty maintenance, premium internals, strong and distant supply lines, bountiful bounties, quick and slow shuttle, luxury and cramped escape pods, pet day, birthday, glowsticks, bot and machine languages, colored assistants, Ian's adventure, the trait jobs (cargo gorilla, Pun Pun), scryers, skub, spiked drinks, blackout, late arrivals, hangover, structural weakness, carp and spider infestation, mail blocked, vending shortage, unnatural atmosphere, storms, nebula
- [x] **Station goals** (`modules/station_goals` 94/94; gaps R1: all three with their windows; the carp sampler is gaps OD2's, its self-mutation waits for the space dragon, `not-translated.md` section 218): bluespace artillery, DNA vault, meteor shield satellites (win: BluespaceArtillery, DnaVault, SatelliteControl)
- [x] **Security levels** (`modules/security_levels` 20/20): green, blue, red, delta and their effects (emergency maintenance access, shuttle times, the status displays and lights); keycard authentication devices (Meta 8; win: KeycardAuth) (the levels, the keycard devices, the shuttle times and the status displays with the syndicate and silicon packages; gaps R4: the red alert's door access, the fire alarms' picture and light, the defibrillator mounts' clamps, the APC's night lights, the computers' alert relevancy and the supermatter's delta)
- [x] **The station's self-destruct** (the nuclear self-destruct terminal, Meta 1; win: NuclearBomb) and the nuke disk with its pinpointer (Meta 1) (syndicate N1a)
- [ ] **Round end leftovers** (not-translated.md 82): the antagonists' report and objectives (done, syndicate A1), the end credits (gaps R5: the roll and the verb; tgstation ships no contributors.dmi, and the server cannot read one, D104), the cinematics (`datums/cinematics`: the nuke's done, syndicate N1a; the malf's, silicons SI8; Nar'Sie left), achievements and medals (the owner, D332: not now), commendations (done, gaps R5; the OOC heart waits for the chat icons), the market report (done, gaps P4d; the restaurant venues wait for SSrestaurant), the AI laws in the report (done, silicons SI5)
- [x] **Votes** (`datums/votes` 3/4: restart, map, custom; win: VotePanel) and polls (owner: not now) (the owner, 2026-11-08: SSvote, the restart and custom votes, the VotePanel; the map vote stays out, as tg's does without a map rotation, not-translated.md 142)
- [ ] **Achievements and scores** (`/datum/award` 0/134; win: Achievements, AchievementsAdminPanel) (the owner, D332: not now; tg keeps them in its database)
- [x] **Holidays** (`modules/holiday` 110/110 procs, 78 holidays): date-driven names, decorations and events (Meta's xmas tree spawners, Ian's birthday helpers) (gaps R3: the calendar, every holiday, the readers and the holiday events; monkey day's Pun Pun and the station traits' decal colours wait for gaps R2; on with the real date, D332, gaps R6)
- [ ] **Persistence** (SSpersistence; the paintings: silicons SI4d): paintings, photo albums, trophy cases, engraved and chiselled messages, the persistent station data (the owner, D332: not now, as D15)

---

## 3. Antagonists (`modules/antagonists` 875/3,715; `/datum/antagonist` 38/142)

Each row means the antagonist with its datum, team, objectives, powers, items, HUD icon and info window.

- [x] Traitor: uplink (370 uplink items, `/datum/uplink_item` 367/370; win: Uplink), telecrystals, objectives, codewords, final objectives (win: AntagInfoTraitor) (syndicate A2: 364 of 370 entries live, those with an unported item hidden, DV343; the uplink implant, G3a)
- [x] Changeling: absorbing, DNA, the 40 abilities, the cellular emporium, fallen changeling (win: AntagInfoChangeling, CellularEmporium, LingMMITalk) (gaps K1: the roundstart, latejoin and space changeling rulesets live, the hivemind, the headslug and its egg; left: the flattened picture of the flesh ID, the legion brood's creator, the spider infestation's bloody spider eggs, the blood worm's expulsion, the cure of diseases, the arm blade against computers (the aliens' attack), not-translated.md 187)
- [ ] Heretic: paths, influences, rituals, the 116 knowledges, ascension, lunatics, summons, the arena (win: AntagInfoHeretic)
- [x] Blood brothers (win: AntagInfoBrother) (syndicate X4: the conversion by flash, the team's objectives and name; the preference picture waits for the ethereal and moth species)
- [x] Malfunctioning AI: modules and the doomsday device (silicons SI8; win: AntagInfoMalf, MalfunctionModulePicker, AiVoiceChanger; mech domination waits for the mechs)
- [ ] Wizard: spellbook (88 entries; win: Spellbook), 226 spells (`modules/spells` 123/426), apprentice (win: ApprenticeContract), the wizard's shuttle and den, imps, wizard events (win: AntagInfoWizard, AntagInfoDemon)
- [ ] Blood cult: runes, the cult's magic, constructs and shades, soulstones, Nar'Sie and the summoning, the cult's structures
- [x] Revolution: head revolutionaries, conversion by flash, the enemy of the state, the revolution's end (win: AntagInfoRevolution) (gaps V1; the drinks that read revolutionaries since gaps RG1: the cuba libre heals them, grenadine weakens them, the garibaldi's resilience; the death moodlet comes with the moods)
- [x] Nuclear operatives: the team, the nuke and the disk, the war declaration, the syndicate base and shuttle, reinforcements, clown operatives, the lone operative (the nuke and the disk: syndicate N1a; the team, the leader, the outfits, the base, the infiltrator, the reinforcements and the roundstart ruleset: N1b; the war declaration, the infiltrator's navigation computer, the lone operative and the midround nukies: N1c; the arsenal's guns: N2a; the bags, kits and pinatas: N2b; the reinforcements and the assault pod: N2c; the clown operatives, their gear and rulesets: N3a; the bananium nuke's clowning of the station with the fear and phobia system: N3b; done but the MODsuits and the silicons, booked)
- [ ] Spies: bounties and the spy uplink (win: AntagInfoSpy, SpyUplink)
- [ ] Blood worm (new in this snapshot; win: AntagInfoBloodWorm)
- [ ] Nations: departments as nations, separatists (win: AntagInfoSeparatist)
- [ ] Obsessed (and the former obsessed), paradox clone, brainwashed and hypnotized victims (win: AntagInfoBrainwashed) (the obsessed and the hypnotized: done with the brain traumas, gaps B1)
- [x] Xenomorphs: the castes, larvae, facehuggers and eggs, resin, the hive, the queen (carbon aliens 9/9 with the abstract base; `/obj/structure/alien` 17/17; gaps X5: with the nest, the alien HUDs, the hivemind, the infestation ruleset, the aliens' attacks on doors, vents and rocks, and ventcrawling; left: the royals' see-through (DV460), the pipe vision inside the vents, the see-death mood with the moods, the 3D models in required-models.md)
- [ ] Blob: the overmind, the 17 strains, blob structures (0/11), blobbernauts and spores (win: AntagInfoBlob)
- [ ] Space dragon with carp rifts
- [ ] Nightmare (light eater; win: AntagInfoNightmare), revenant, morph (win: AntagInfoMorph)
- [ ] Space ninja (win: AntagInfoNinja)
- [ ] Pirates: the pirate crews, their shuttle and the cargo pad (win: CargoHoldTerminal)
- [ ] Giant spiders (48 types) and their webs and eggs (`/obj/structure/spider` 6/16)
- [ ] Abductors: the ship, the consoles, the glands and the experiments (win: AbductorConsole, GlandDispenser, ProbingConsole)
- [ ] Fugitives and their hunters
- [ ] Slaughter and laughter demons
- [ ] Voidwalker and sunwalker (win: AntagInfoVoidwalker, AntagInfoSunwalker)
- [ ] Minor antagonist datums: sentient creature (win: AntagInfoSentient; done), syndicate monkey (done, syndicate G4c), venus human trap (done, gaps E4b), pyroclastic anomaly, survivalist, highlander, valentines and the heartbreaker (done), magic servant, recovered crew (done, gaps OG1), the bitrunning glitch, ash walkers
- [ ] ERT and deathsquad (admin-called teams; `/datum/antagonist/ert` 0/40, `/datum/job/ert` live)
- [x] Objectives (`/datum/objective` 58/98) and the antagonist HUD icons for all (syndicate A1; left: the silicons' and the wizard's objectives, five steal targets with their packages, not-translated 130)
- [x] Syndicate gear as a set: energy swords (done, syndicate G2), the emag and the doorjack (done, G1a), the agent ID and chameleon kit (win: ChameleonCard; done, G1b), radio jammer (done, G2), stealth and implant items (the suspicious autosurgeons and the antagonists' arm kits done, gaps P6), the syndicate bombs (training bomb on Meta 1; done, G2), the black market (win: BlackMarketUplink, LTSRBT; done, gaps P4d: the market uplinks, the LTSRBT and the market's items, `/datum/market_item` 79/79), the contractor (X1a: the contracts, the extraction and the holding facility, the tablet; win: SyndicateContractor done; X1b: the support unit and the fulton kit done)

---

## 4. Jobs, roles and the character

Already ported: the lobby, the occupations page and every job whose datum is live (D278, OPEN_JOBS), the heads of staff and
captaincy (D274), outfits 222/513, ID trims 152/159, the moods (gaps MS1), screen alerts 168/201, emotes 188/227.

- [x] Jobs that can now be taken with their kits (gaps J2, D298): security officer with the department assignment, warden, bartender, cook, chaplain with the religion choice, clown, mime
- [ ] Jobs whose datum is not ported (cannot be taken): veteran security advisor and human AI (the bridge assistant and the trait jobs Pun Pun and cargo gorilla: done, gaps R2; the cyborg since silicons SI2, the AI since SI4)
- [ ] The departments behind the open jobs (section 5): a botanist, chemist or roboticist can join, but most of the job's work is not ported
- [ ] Job playtime and its requirements (win: TrackedPlaytime); the character-to-job slots (D267)
- [ ] Roundstart species (tg's config): felinid, lizard, moth, plasmaman, ethereal and cerulean; the human, the felinid and the lizard (with the ash walker and silverscale species) are live and selectable (`/datum/species` 10/31) (gaps SP1, D338: the species page, their features, organs, digitigrade legs, Draconic and Nekomimetic, cold blood, the wagging tail, purrbation, the feral bite; drawn from tg's sprites on the human body; left: not-translated.md section 223). Left: the moth, plasmaman, ethereal and cerulean (gaps SP2), each with its organs, bodyparts, language, traits and sprites (the plasmaman's envirosuit, the ethereal's charge, the moth's wings)
- [ ] Other species (made by magic, mutation or events; android done, silicons SI10): fly, jelly and slimepeople, pod people, golem, abductor, android, skeleton, zombie, shadow, snail, mush, dullahan, spirit
- [x] Monkeys (gaps C5; left: `not-translated.md` section 111) (Meta 8 and Pun Pun): the monkey species, its AI, monkey cubes (Meta 4 boxes), monkeyize and humanize
- [ ] Quirks (101; `datums/quirks` 10/287; owner: the Quirks tab not now)
- [ ] Loadout and personality (`modules/loadout`, `datums/personality`; owner: not now)
- [x] Hair, facial hair, their colours and gradients, eye colour, underwear, undershirt, socks, backpack and jumpsuit style on the human body and the Character page (M10x slice D, 1.0.6-pre-alpha)
- [ ] The rest of the sprite accessories (other species' features) and bodypart overlays (the lizard's snouts, horns, frills, spines, tails and body markings and the cat ears and tails came with gaps SP1; left: the moth's, the pod's, the mushroom's, the cerulean's, gaps SP2 and SP3)
- [x] Languages (26/26 live; the holder and menu types 42/42): draconic, moffic, calcic, voltaic, sylvan, nekomimetic, terrum, machine, codespeak and the others with their names, the language holder and menu (win: LanguageMenu) and the HUD buttons; the language's chat icon waits for the chat icons (owner: later; not-translated 173); the species that speak them wait for the roundstart species row (gaps L2)
- [x] Skills (`datums/skills` 6/6, gaps SL1): SSskills, the mind's experience and levels with tg's messages, the mining, athletics, cleaning, gaming and fishing modifiers and experience, the legendary skill capes by supply pod, the exercised status, the fitness examine; the skill station (Meta 1; win: SkillStation), the generic skillchips (skillchips 32/32), the admin skill panel (win: SkillPanel) and the PDA's skill tracker (win: NtosSkillTracker). Left (`not-translated.md` section 221): the wizard's spellbooks, the veteran cloak's preference, the skill readers in files not ported (swimming, the arcade cabinets, fishing, bitrunning, the fireman carry)
- [x] Nutrition and hunger (M9x, gaps S1): eating, feeding, drinking, the nutrition HUD, starvation and fat, vomit; the drinks' and food reagents' own effects and drunkenness (gaps RG1). Left: the drinking glasses, mugs and bar bottles (the broken bottle's `SwapColor`), golem food, the allergies (quirks)
- [x] Addictions (`/datum/addiction` 8/8 live; gaps RG2): the mind's addiction points from a drug's metabolism, the addiction past its gain threshold, the three withdrawal stages of the opioids, stimulants, alcohol, hallucinogens, maintenance drugs, medicines and nicotine with their effects (the stimulants' slowness and wooziness, the maintenance drugs' night vision and beard, the medicines' fake alerts and health doll), the addiction lost under its loss threshold, a full heal's cure and naloxone's resilience. Left (not-translated 225): the withdrawal moodlets (the moods), the hallucinogen withdrawal's plane filters in the client
- [x] Moods and sanity (`/datum/mood_event` 404/404 live; gaps MS1, D339): the mood datum of every human, the mood events with their timeouts (hugs, meals, drinks, drugs, deaths seen, hunger, area beauty, art, games, pets and the rest), SSmood's sanity drain and recovery with the slowdown, the action speed, the hallucinations and the insanity, the mood face on the HUD with its sanity colour and `print_mood` on its click, the hunger rate and sleep quality by sanity. Left (`not-translated.md` section 227): the quirks' and personalities' moods (owner: not now), the antagonists' moods with their antagonists, the moods of systems not ported (the chapel, recreation, fishing, the diseases, Lavaland, the gizmo)
- [x] Brain traumas (83/87 live; gaps B1): mild, severe and special traumas, phobias, the magical traumas, hypnosis, split personality, the imaginary friend, the obsession with the obsessed antagonist, the voice of god; the brain's random roll and the cures. Left: the religions' burdened and honorbound traumas and the voidwalker's (with their systems), the friend's picture faces south (DV440), the trauma calls of the systems not ported (quirks, heretics, wizards, reagents, not-translated 185)
- [x] Genetics: DNA, genes and the 92 mutations (all live), genetic powers, injectors (done, syndicate G4a: the DNA injectors and the DNA syringe gun), the DNA console and scanner (Meta 3 and 3; win: DnaConsole), the chromosomes, genetic damage (gaps P3)
- [x] The DNA infuser (Meta 1; `game/machinery/dna_infuser` 174/174; win: InfuserBook): the infuser, its entries and book, the organ sets of the carp, fish, fly, fox, goliath, gondola, rat, roach and stoat with their set bonuses, and the other species' organs it grows (the moth's wings and antennae, the lizard's horns, frills, snout and tongue, the cat's ears and tail, the penguin's ears, the podperson's organs; gaps P3b)
- [x] Diseases and virology (`/datum/disease` 47/52 live; gaps VI1): advance diseases and symptoms, cures, the PanD.E.M.I.C. (Meta 1; win: Pandemic), SSdisease, the outbreak events, the infective, rot and germ components. Left (not-translated.md section 228): the asthma attack (the quirks), the morph and jellypeople forms, the wizard's spells of wizarditis
- [x] Martial arts (syndicate X3): CQC and the chef's cooking, the sleeping carp (X3a: the base, the combo display, the scroll, the carp gloves and the bo staff), boxing, evil and hunter boxing, plasma fist, mushpunch, psychotic brawling, jungle arts, kaza ruk, spider's bite (X3b); wrestling is kept out (D80: Goon-derived); their remaining sources and leaves are not-translated.md section 147
- [x] Organs not ported (`/obj/item/organ` 273/324 live): cybernetic implants (39/43 live: the eye HUDs and the surgical processor, gaps P1; the rest, gaps P6; left: the cursed katana's and the vorpal scythe's shards, the BCI), the species' tails, snouts, spines and fangs (gaps SP1), the eye augments, hearts, tongues, stomachs, livers, lungs, vocal cords, the appendix and its inflammation and the golem, plasmaman, skeleton, ethereal, slime and snail organs that work without their species (gaps OG1, win: HighLuminosityEyesMenu, TacVisorEyesMenu); left (not-translated.md 229): the zombie infection, the species' own (cerulean, dullahan, mushpeople, vampire, lustrous), the abductor glands and other antagonists' organs, the chaplain's, the monster cores and Lavaland's (D342)
- [x] Implants (`/obj/item/implant` 36/45 live): tracking, chemical, exile; storage, explosive, freedom, uplink, radio, EMP, stealth, deathrattle and the nuclear operative's (syndicate G3a); the beacon, the bluespace grounding implant and the exile's anti-teleport kind, the implant pad and the mindshield implanter with the genetic purifier (done, gaps DV2; win: ImplantPad, ImplantChair); the spell implant (gaps OG1); left: the tactical map, the abductor's, the battle royale's (not-translated.md 216), the camouflage, MOD and circuit ones
- [ ] Ghost roles and spawners (`mob_spawn.dm`, the drone shells and the spawners menu done, silicons SI7; the corpse spawners done, gaps E2c and E3; the other ghost role spawners left; `modules/mob_spawn` 34/116, `/obj/effect/mob_spawn` 116/203; win: SpawnersMenu done), ghost polls and alerts (done, syndicate X2: SSpolling and the poll alert), DNR (done, silicons SI6), ghosts' scans and data HUDs, the minigames menu (done, gaps S4d, with Mafia; win: MinigamesMenu done), possession and deadchat control

---

## 5. Departments

### 5.1 Engineering and atmospherics

Already ported (M4, M5, M5x, M7): all 20 gases and their reactions, the pipe network with pumps, valves, vents, scrubbers,
filters, mixers, heat exchangers, thermomachines and canisters, the air alarm, power (cables, APCs, SMES, solars, PACMANs,
cells), the supermatter and its delaminations, the energy ball, the singularity, the gravity generator, lighting, radiation,
EMP and shocks, the engineering tools, the RPD, construction and destruction, explosions and C4.

- [x] Singularity containment: field generators and their fields (`/obj/machinery/field` 6/6 live, Meta 4; engineering E7). Left: the singularity beacon (syndicate gear, section 3), the prototype emitter, the energy cannon and the wabbajack statue (shuttle props); tg's snapshot has no singularity generator
- [x] The turbine: core rotor, inlet compressor, turbine outlet (Meta 1 each) and its computer (Meta 1; win: TurbineComputer) (engineering E11)
- [x] The thermoelectric generator (win: ThermoElectricGenerator) and the RTG (`power/rtg` 6/6) (engineering E11)
- [x] The hypertorus fusion reactor (engineering E12b) (`hypertorus` 5/5; its core box Meta 1; win: Hypertorus)
- [x] The crystallizer (engineering E12a) (Meta 1; win: Crystallizer) and the electrolyzer machine (Meta 1; win: Electrolyzer; its reaction is live)
- [x] Atmospherics control consoles for the tanks, the incinerator and ordnance (Meta 10; `atmos_control` 29/29; win: AtmosControlConsole) and their air sensors (Meta 11) (engineering E5)
- [x] Atmos alert consoles (Meta 3; win: AtmosAlertConsole) and station alert consoles (Meta 6; win: StationAlertConsole, NtosStationAlertConsole) (engineering E6)
- [x] The incinerators' airlock controllers, airlock sensors, igniters and sparkers (Meta 2 each; `embedded_controller` 32/32; win: AirlockController, AirlockButtonController) and the access buttons (Meta 6) (engineering E8)
- [x] The APC control console (Meta 1; win: ApcControl) (engineering E6)
- [x] Space heaters (Meta 9, and 2 improvised chem heaters; win: SpaceHeater) (engineering E9)
- [x] Shield generators (engineering E10): the shield generator (Meta 2), shield wall generators (Meta 6, 2 of them in xenobiology), the atmospheric shield generators (Meta 6), the modular shield generator (win: ModularShieldGen, ModularShieldConsole)
- [x] Cell chargers (Meta 16); the rechargers are live (engineering E9)
- [x] The RCD family (`/obj/item/construction` 24/24, `items/rcd` 182/182 procs; the RCD, RLD, RTD and RDD in engineering E13, the plumbing constructor in E15, the RWD and the exosuit's RCD in gaps DV2): the RCD, RLD, the rapid tiling and decoration devices, the plumbing constructor (Meta 2), the rapid wiring device (win: RapidConstructionDevice, RapidTilingDevice, RapidDecorationDevice, RapidPlumbingDevice)
- [x] The station blueprints with area editing (engineering E14) (win: Blueprints)
- [x] The airlock painter (Meta 3) and the decal and tile painters (engineering E14; tg's tile painter is the decal painter's Tiles and Trimlines categories) (win: DecalPainter)
- [x] Plumbing (engineering E15; the reactions wait for chemistry) (`modules/plumbing` 148/148, machines 0/24; ducts partly ported): the chemical factory (win: ChemAcclimator, ChemAutomaticBuffer, ChemDisposer, ChemFilter, ChemMixingChamber, ChemPress, ChemReactionChamber, ChemSplitter, ChemSynthesizer; synthesizer Meta 1), the plumbed reagent tanks (Meta 11)
- [x] The big manipulator (engineering E16; the lathe's designs since silicons SI3) (`game/machinery/big_manipulator` 128/128; win: BigManipulator) and the manufacturing machines (`modules/manufactorio` 95/97; win: ManufacturingSorter): not mapped, buildable
- [x] The portable gravity unit (engineering E16; no weather until SSweather) (win: Portagrav) and the barometric (mining weather) monitors (Meta 2; win: AnomalousWeatherTower)

### 5.2 Medical

Already ported (M6, M8, M8x, M8y): wounds, bleeding, dismemberment, gibbing, blood and forensics in the world; the reagent
holder and metabolism with 261 of 954 reagents; gauze, sutures, meshes and medkits, syringes, hyposprays, pills and patches
(applicators 88/88), the health analyzer, the defibrillator and its wall mount, CPR, revival, data HUDs, implants in part,
hallucinations, cryo cells, stasis beds, IV drips, buckling.

- [x] Surgery, reworked in this snapshot as operations (`/datum/surgery_operation` 120/120, gaps P1, `modules/surgery/operations`): amputation, adding and replacing limbs, organ manipulation and repair, bone repair, debridement, healing, revival, autopsy and dissection, brainwashing, lobotomy, pacification, plastic surgery, lipoplasty, cavity implants, implant removal, dental, bioware, virus, filter, pump, puncture and wing repair, zombie, asthma
- [x] Surgical tools (gaps P1; the antagonists' supermatter theft scalpel and hemostat stay inert: scalpel, retractor, hemostat, cautery, circular saw, drill, drapes; Meta 1-3 each), surgery disks, the surgery tray, the operating computer (Meta 5; win: OperatingComputer)
- [ ] Chemical reactions (`/datum/chemical_reaction` 660/717 live): the reaction engine is live (gaps F1: the holder's reactions, the equilibria, the reaction datum, SSreagents) with every live reaction indexed (the owner, 2026-11-03); every chemistry reaction is live (gaps P5: the explosive, flash, EMP and mob-spawning mixes, medicines, drugs, catalysts, the randomized recipes); the kitchen's food and soup reactions are live (gaps H2); the slime extracts' (xenobiology) wait for their package; purity, impurities and pH (the pH booklet, Meta 1)
- [ ] The reagents not ported (`/datum/reagent` 933/954 live): romerol (the zombie species) and concrete wait for their systems (gaps RG3, not-translated 226; the vaccine and the disease and transformation toxins came with VI1's diseases in gaps train 12); the drinks and the food reagents are done (gaps RG1: every alcohol, soft drink and food reagent with the drunkenness, the drinker traits and the liver); the medicines, the toxins and the impure and inverse reagents are live (gaps RG2: strange reagent's revival, helbital's game with death, the sleeping and breath-stopping toxins, the impure medicines; left, not-translated 225: the fish procs, gibbium's GBS, the spider serum's antagonist, rotatium's plane turn in the client, the moods); the drugs, pyrotechnics, other reagents, the gases' reagents and eigenstasium with the eigenlinked lockers are live (gaps RG3)
- [x] Chemistry machines (gaps P5): the chem dispenser (Meta 3, and the booze and soda dispensers 1+1; win: ChemDispenser), the ChemMaster and CondiMaster (Meta 4+3; win: ChemMaster), the chem heater (Meta 4; win: ChemHeater), the chem separator (Meta 1; win: ChemSeparator), the mass spectrometer (Meta 1; win: MassSpec), the reagent grinder (Meta 9), the smoke machine (done, gaps F1; win: SmokeMachine), the portable chem mixer (win: PortableChemMixer), the medipen refiller
- [x] The crew monitoring console (Meta 5; win: CrewConsole) with the jumpsuits' suit sensors (gaps J5)
- [x] Medical records (gaps SC2; Meta 5 consoles and 7 laptops; win: MedicalRecords, NtosRecords)
- [x] The DeForest first-aid wall stations (`wall_healer`, Meta 16), vitals displays (Meta 6), the medical kiosk (Meta 1; win: MedicalKiosk) (gaps MD1; the vitals display's circuit port waits for wiremod)
- [x] The limb grower (win: Limbgrower; done, gaps MD3), the organ harvester (done, gaps MD3), prosthetics (the robot, surplus, advanced and android limbs are live, gaps P1), the organ smartfridges (Meta 4)
- [x] The blood filter (win: BloodFilter; done, gaps J5), the lazarus injector (Meta 1; done, gaps MD3)
- [x] The morgue and the crematorium (Meta 13) (gaps MD2)
- [x] The recovered crew (`modules/lost_crew` 61/61; gaps OG1: the long-term body bag's injured body, its mind lockbox, the ghost role on revival and the reward, the morgue's cadavers); body bags and the autopsy scanner's scan are done (gaps J5)
- [x] Medbots (gaps C3, section 7)
- [ ] Genetics, virology, organs, implants and brain traumas (section 4)

### 5.3 Science

- [x] The techweb (`modules/research` 322/993): 128 nodes, 1,270 designs (silicons SI3: SSresearch, the techwebs, the nodes and every design, the station's starter nodes researched), research points, the R&D consoles (Meta 3; win: Techweb, NtosTechweb), the R&D servers and their control console (Meta 2+1; win: ServerControl), tech disks (Meta 4) and design disks (gaps RD2: the consoles, the servers with the master's hard drive, the control console, the disks and the Science Hub app; the B.E.P.I.S. that prints the experimental disks is not ported)
- [ ] Experiments (gaps RD3: every experiment and the experiment handler are live, those waiting on ordnance, exploration drones, fishing, cytology, the arcade and mech construction in not-translated.md 195; the handler's window lists techwebs once the R&D servers are live, gaps RD2; the experimentor and its relics are live, gaps RD4; research papers remain; `/datum/experiment` 85/85, `modules/experisci` 127/127; win: ExperimentConfigure, done): the experi-scanners (Meta 3; done, gaps RD3), the destructive scanner (Meta 1; done, gaps RD3), the destructive analyzer (Meta 1; win: DestructiveAnalyzer; done, gaps RD3), the experimentor (Meta 1; win: Experimentor; done, gaps RD4), research papers (win: NtosScipaper)
- [ ] Fabricators: protolathes, circuit imprinters and the departmental techfabs (Meta 9; `/obj/machinery/rnd` 27/28; win: Fabricator; done, gaps RD1), the autolathe (Meta 1; win: Autolathe; done, gaps RD1), the exosuit fabricator (Meta 3; win: ExosuitFabricator; done, silicons SI3), the ore silo and remote materials (Meta 1; win: OreSilo), the flatpacker and flatpacks (Meta 2; win: Flatpacker)
- [ ] Ordnance: tank transfer valves (Meta 5; win: TransferValve; done, syndicate G4b), the tank compressor (Meta 1; win: TankCompressor), the burn and freezer chambers (section 5.1), the doppler array (Meta 1; win: DopplerArray), the mass drivers to the testing range and their controllers (Meta 4; win: MassDriverControl), bomb cores, ordnance disks (Meta 4)
- [ ] Xenobiology (gaps C4: the slime itself is live; extracts, potions and the console remain): slimes (Meta 3; `basic/slime` 4/5) and their feeding, growth and splitting, slime extracts (0/23) and their reactions, the slime management console with its camera (Meta 2), the slime processor (Meta 1), the monkey recycler (Meta 1), the pens' shield wall generators (Meta 2), cell lines and the growing vat (Meta 1), the biopsy tool and the microscope (Meta 1+1; win: Microscope)
- [ ] Robotics: mechs (`/obj/vehicle/sealed/mecha` 28/28: Ripley, Clarke, Odysseus, Gygax, Durand, H.O.N.K., Phazon, Savannah-Ivanov, Reticence, Marauder, Vim; Meta 2 and a wreckage) with their 133 parts and equipment (win: Mecha), the mech bay recharge ports and consoles (Meta 6+5; win: MechBayPowerConsole), the mech pad and its console (Meta 1+1; win: MechpadConsole), the exosuit control consoles (Meta 2; win: ExosuitControlConsole), building cyborgs (section 6), robotic limbs (Meta 4)
- [ ] Circuits (wiremod; `modules/wiremod` 473/1,063, 191 components): integrated circuits (Meta 4), shells, USB ports, the component printer and the module duplicator (Meta 2+2; win: ComponentPrinter), the BCI implanter (Meta 2), the circuit multitool (Meta 6) (win: IntegratedCircuit, CircuitModule, CircuitAccessChecker, CircuitSignalHandler, ProcCallMenu) (gaps CI1: the core with the integrated circuit, ports, datatypes, variables, the circuit multitool, the USB cable, the component printer, the module duplicator and the save and load, the shell component, the math, list, string, table, variable, utility and module components, the compact remote (Meta 4) and the controller (Meta 3), the IntegratedCircuit editor (DV571), ComponentPrinter, CircuitModule and CircuitAdminPanel; left: the acting and sensing components, the presets and the other shells with the BCI, gaps CI2; the USB ports, gaps CI3)
- [ ] Anomaly research: raw anomaly cores (Meta 3), the anomaly refinery (Meta 1; win: AnomalyRefinery), refined cores and their artefacts (the anomalies are live, D166)
- [ ] Exploration drones (`modules/explorer_drone` 3/207): the launcher and drones (Meta 2+2), the exoscanner and its console (Meta 1+1; win: ExodroneConsole, ExoscannerConsole, AdventureBrowser)
- [ ] The gizmo machines (new in this snapshot; the "tucker tubes" gizmo, Meta 1)
- [ ] The ectoscopic sniffer for positronic brains (Meta 1)

### 5.4 Service

- [ ] **Food and eating** (`modules/food_and_drinks` 788/969): 1,285 food types (`/obj/item/food` 1,152/1,285 live; gaps S1, and gaps H2: the food subtypes' cooking procs, the donuts, fish fillets, cans and pizzas), eating (done, gaps S1), the grown produce (`/obj/item/food/grown` 153/154, gaps P2), food quality and moodlets (the moodlets done, gaps MS1), the crafting recipes (`/datum/crafting_recipe` 1,124/1,288 live) that make food
- [x] **Kitchen machines** (gaps H2): microwaves (4/4 types live; Meta 2), the oven and range (Meta 3), the griddle (Meta 2), grills and stoves, the deep fryer (Meta 1), the food processor (Meta 2), the gibber (Meta 1) and the meat spike (Meta 2 and a frame), the ice cream vat (Meta 1), the food cart (Meta 1), smartfridges and drying racks (Meta 14; win: SmartVend), the cook's order console (Meta 1; win: ProduceConsole), kitchen tools (knives, rolling pins, plates, trays)
- [ ] The restaurant: robot customers, the restaurant portals (Meta 2) and their holosigns (Meta 2)
- [x] **Bar**: the drinks' effects, drunkenness and the cocktails' job, healing and special effects (done, gaps RG1); drinks and glassware (done, gaps H3: the glass styles, the drinking and shot glasses, the bottles, cans, the mauna mug and the organ jar; Meta 41), the bar sign (done, gaps H3; Meta 1), the jukebox and the radiant dance machine (done, gaps H3; win: Jukebox; not mapped), shakers (Meta 5; live), condiments (`/obj/item/reagent_containers/condiment` 46/46; Meta 19), the coffee maker (Meta 1; done, gaps H2), the fermenting barrel (Meta 1; `/obj/structure/fermenting_barrel` 3/3), Pun Pun (section 7, done)
- [x] **Botany** (`modules/hydroponics` 513/529, gaps P2; the botanical encyclopedia waits for S4): hydroponics trays and soil (Meta 39), 158 seed types with plant genes and mutations, growing, weeds, pests and harvesting, the seed extractor (Meta 4; win: SeedExtractor), the biogenerator (Meta 4; win: Biogenerator), plant analyzers (Meta 7; win: PlantAnalyzer), cultivators (9), hatchets (4), shovels and spades (4), secateurs, plant bags, watering cans (9), fertilizers and the nutrient vendor, beehives and bees, the botanical encyclopedia (win: NtosBotanicalEncyclopedia)
- [x] **Janitor** (gaps H4; the other ridden vehicles' riding waits for the vehicles): the mop and bucket (Meta 6+3), the janitorial cart (Meta 1) and the janicart with its key and upgrades (Meta 1), the advanced mop, the wet floor signs, soap (1), rags (8), the push broom (2), plungers (2), cleaning decals, trash bags; the light replacer and holosigns are live; soap (done, syndicate G3b)
- [ ] **Chapel** (`modules/religion` 0/283): the religion choice, bibles (Meta 3) and blessing, holy water, the null rod and its 33 forms (Meta 1), the 10 sects and the altar of the gods (Meta 1), the soulstone (Meta 1), sparring (win: SparringContract), the chapel's mass driver (Meta 1)
- [ ] **Library and curator** (`modules/library` 161/228): books (Meta 58), bookcases (Meta 15; done with the library subsystem, gaps ST2), the library console and the book management consoles (Meta 1+2; win: LibraryConsole, LibraryVisitor, LibraryAdmin), the library scanner (Meta 1; win: LibraryScanner), the bookbinder (Meta 1), the tgui books (the DSM, the IDC, the infuser book; Meta 8; win: DSMBook, IDCBook, InfuserBook), display and trophy cases (Meta 9; win: Trophycase, Vendatray; done, gaps ST2)
- [ ] **Theatre**: the clown's and the mime's kit and powers (mime spells, the invisible wall), costumes (the costume spawners), the theatre's curtains (Meta 8; done with the curtain button, gaps ST2)
- [x] **Washing and household** (gaps H1; the toilet's fish and the puddle's tadpoles wait for the fishing): sinks (Meta 32), showers (Meta 31), toilets (14) and urinals (5), puddles (3), washing machines (Meta 4), bedsheets (13) and their bins (2) (done, syndicate G3c), dressers (6), mirrors (13) and razors (3) for hair and beards
- [x] **Vending machines** (gaps S2; `modules/vending` 95/148; 78 of 85 types live; all of Meta's 92 machines; win: Vending): products, restocking (refills Meta 3), wires, tilting and crushing, the prices and payment (gaps P4a). Left: NtosRestock (the PDA app, S4; NtosCouponMaster with gaps P4b), the custom, runic, labor camp and museum vendors
- [ ] **Recreation**: the holodeck (its computer Meta 1; `modules/holodeck` 16/65; win: Holodeck), the arcade (Orion Trail Meta 1, the battle arcade; win: OrionGame, BattleArcade), slot machines and the roulette (win: SlotMachine, Roulette), playing cards (done, syndicate G3d), dice (Meta 2), the trading card game (`items/tcg` 0/67), toys (`/obj/item/toy` 193/197; Meta 40) and plushies (done, syndicate G3c), crayons and spray cans (graffiti; win: Crayon), the gym's punching bags, weight machines and training machine (Meta 3, 5, 3; win: TrainingMachine; done, gaps ST4, with the training toolbox; the athletics experience and the exercised status came with gaps SL1)
- [x] **Music** (silicons SI6c; the legacy MIDI instruments' notes silent, DV391; the synthesizer's circuit shell waits for wiremod): instruments (26 types; Meta 4) and the piano and other musician structures, songs and the instrument editor (win: InstrumentEditor)
- [ ] **Art** (silicons SI4d: canvases, easels, paintings and frames, the portrait printer; sculpting came with silicons SI2; left: the PDA painter): canvases and easels (Meta 15+7; win: Canvas), paintings and frames (Meta 13; persistence), sculpting (carving blocks, the chisel), the portrait printer (win: NtosPortraitPrinter), the PDA painter (Meta 6; win: PaintingMachine)
- [ ] **Photography** (gaps J3: cameras and photos work without the picture itself; `modules/photography` 75/106 before it): cameras (Meta 4) and photos, picture frames (Meta 5), photo albums, the photo booths and the green screen camera (Meta 2+1)
- [ ] **Fishing** (`modules/fishing` 10/787): rods and baits (win: FishingRod), fish and their traits, fishing spots, the fishing portal generator (Meta 1), aquariums and fish tanks (Meta 1+1; win: Aquarium), the fish mount (Meta 1), the fishing toolboxes (Meta 2), the fish analyzer and catalog (win: FishAnalyzer, FishCatalog, FishingCalculator)

### 5.5 Cargo, economy and mining

- [x] **Supply** (`modules/cargo`): the supply console and request consoles (done, gaps P4b; Meta 2+3; win: Cargo, NtosCargo), 405 supply packs with their prices, private and goody orders and coupons (done, gaps P4b; win: NtosCouponMaster), the supply shuttle's trips, exports and selling (done, gaps P4b; the fish wait for fishing), cargo express with its beacon (done, gaps P4d; win: CargoExpress), the galactic materials market (done, gaps P4d; win: MatMarket), the order consoles and the departmental orders (done, gaps P4d: cook, mining and the six department consoles; Meta 2+7; win: ProduceConsole, NtosDeptOrder; the bitrunning vendor waits for bitrunning), the black market with its uplink and LTSRBT (done, gaps P4d; win: BlackMarketUplink, LTSRBT); the supply pods and their landing (done, syndicate G4c: `supplypod.dm` and `podspawn`, which the surplus crate, the monkey agent, the job choice beacons, the objective equipment and the shuttle's contraband return use) The shuttle loan came with gaps E3. Left: the items' pictures in the black market's windows (D104)
- [x] **Bounties** (gaps P4c): 202 cargo bounties, the civilian bounty pad and console (Meta 1+1; win: CivCargoHoldTerminal, NtosCivCargoHoldTerminal). Left: the fish bounties' items (fishing; the virus bounties' samples came with the diseases, gaps VI1); the bounty board (win: NtosBountyBoard, BountyBoard) is the newscaster's request board (done, silicons SI6b)
- [x] **Deliveries**: package and gift wrapping (Meta 20+1), the destination tagger (Meta 1; win: DestinationTagger), mail and mail bags (done, syndicate G4a: the letters, envelopes, traitor mail, mail crates and the counterfeit device; the economy's mail crate with gaps P4b) with the mail sorting helpers, shipping (the barcodes' app done, gaps P4d; win: NtosShipping) (done with the disposals, D325: the parcels, the destination and sales taggers and the wrapping, not-translated.md 146; the gift wrap, `items/gift.dm`)
- [x] **Disposals and the recycler** (the units, chutes and outlets with D325; the recycler gaps H1): disposal units (Meta 54), delivery chutes (11) and outlets (14) (win: DisposalUnit), the recycler (Meta 1)
- [x] **Conveyors** (D317): conveyor belts and switches (Meta 42+6), the belt stack and the switch assembly, the switch's wires; a player's ride predicted (DV347). Left: the switch's circuit component (circuits)
- [x] **Economy** (`modules/economy` 45/45, `subsystem/economy` 14/14; gaps P4a): bank accounts and NT Pay (win: NtosPay), paychecks and the departmental budgets with their cards, cash (space cash done, syndicate G3b), holochips, coins and poker chips, the payment component, the accounting console and the bank machine (Meta 1+1; win: AccountingConsole, BankMachine), holopay (win: HoloPay), the ticket machine (Meta 1; gaps S3), the stock market and its events (gaps P4b). Left: the materials market console (P4d), the newscaster's earnings report (SI6b)
- [ ] **Mining on the station** (`modules/mining` 412/947): the ore redemption machine (Meta 1; win: OreRedemptionMachine), the stacking machine and its console (Meta 1+1; win: StackingConsole), the mining equipment vendor, ores (`/obj/item/stack/ore` 19/19) and the other materials' sheets, pickaxes, drills, shovels, the kinetic accelerator and crusher, the mining scanner, ore boxes and containers (win: OreBox, OreContainer), survival capsules, the boulder retrieval matrix and the boulder refineries (Meta 1+2), marker beacons (Meta 57), the GPS (Meta 4; win: Gps) The miners' own tools are live (gaps J6, D298), as are the machines (`/obj/machinery/mineral` 12/12, the boulder machines `/obj/machinery/bouldertech` 3/3, OD3's Lavaland odds). Left: the survival capsules (`/obj/item/survivalcapsule` 0/6) and the kinetic crusher (`/obj/item/kinetic_crusher` 0/2).
- [ ] **The mining shuttle** (its consoles Meta 3) and Lavaland (section 10)
- [x] **The labor camp**: the gulag teleporter and its console (Meta 1+1; win: GulagTeleporterConsole), the item reclaimer (Meta 1; win: GulagItemReclaimer), points and the claim console (win: LaborClaimConsole), the prison's plate press and produceporter (gaps LC1; the claim console and stacker: Lavaland L2b)
- [ ] The labor shuttle's flight (Meta 1): the shuttle stands at its dock as the other shuttles do; it flies with shuttle travel
- [ ] **The auxiliary base**: the construction console with its camera (Meta 1; done 2026-11-14: its drone kept in the aux base, the internal RCD, fans, turrets, Log out), the base's controller (win: AuxBaseConsole) and the base's landing (its docking port is live)
- [ ] **Bitrunning** (`modules/bitrunning` 233/277): the netpods (done, gaps BR1; Meta 3; win: NetpodOutfits), the quantum server and its console (done, gaps BR1; Meta 1+1; win: QuantumConsole), the byteforge (done, gaps BR1; Meta 1), domains and avatars (the avatar, the safehouses and the test, gondola asteroid and grasslands hunt domains done, gaps BR1; win: AvatarHelp; the other domains wait for gaps BR2, the six megafauna domains for the owner's Lavaland mobs, D342), the bitrunning order console (done, gaps BR1), the glitch (section 3, gaps BR2); left: `not-translated.md` section 230

### 5.6 Security

Already ported (M7x and later): the station's guns and ammunition, batons, flashes, handcuffs, security outfits and lockers,
the security HUD, the crime datum, security cameras and the camera console.

- [x] Brig cell timers (`status_display/door_timer`; win: BrigTimer) and the brig's flashers (Meta 25) (done, syndicate brig timers, 2026-11-12)
- [x] The security records console (gaps SC2; Meta 12; win: SecurityRecords, NtosRecords) and the warrant console with fines (Meta 2; win: WarrantConsole), the printed rapsheet and posters (the hung poster's mugshot waits for a larger resource cap, DV498; the circuit components for the integrated circuits)
- [x] The prisoner management console (Meta 2; win: PrisonerManagement) and the labor camp (section 5.5) (gaps LC1)
- [x] Wall telescreens and TVs (`computer/security` 181/181 live; Meta 56 consoles and telescreens): their types and the entertainment screens, with the broadcast camera and the showtime button (gaps TL1)
- [x] The detective: the forensic scanner, evidence bags, the detective board, the spoofer and the outfit (gaps J4, D298); swabs wait for P3
- [x] Security gear left out of M7x: pepper spray, the dragnet, the secway (Meta 1); shields and the sechailer's phrases are live (gaps J4, D298) (done, gaps SE1: the DRAGnet with its beacon, snare and teleportation field, the secway with its key, banana jam, smoke and welding, the wall pepper spray refillers and the virus food dispenser, the armory's barrier grenade with its security barriers; the pepper spray itself was live since M8)
- [ ] Turrets: the AI satellite's turrets (Meta 11) and turret controls (Meta 3; win: PortableTurret, TurretControl) (done, syndicate G4d: the portable turrets, their covers, frames and control panels, the toolbox sentry gun), deployable turrets (`/obj/machinery/deployable_turret` 0/4)
- [x] Scanner gates (Meta 2; win: ScannerGate), the hypnochair (win: HypnoChair) (gaps SG1)
- [x] Secbots (gaps C3, section 7)

### 5.7 Command and the bridge

Already ported: the communications console's announcements and shuttle call, the captain's spare ID safe, ID cards and
access, the crew manifest, the heads' outfits and captaincy, the automated announcement system.

- [x] The ID console and job management (gaps S4a; PDA and laptop apps; win: NtosCard, NtosJobManager)
- [x] Status displays (Meta 105; gaps W1: their text is world maptext; the AI's picture picker waits for the AI, the NtOS app NtosStatus for NtOS programs)
- [x] The greenscreen camera (the captain-caster, `machinery/greenscreen_camera`: its feed on the status displays, its microphone to their speakers) (Meta 1) (done, syndicate greenscreen, 2026-11-12)
- [x] Requests consoles (Meta 40; win: RequestsConsole) with their mapping helpers (gaps G3, SC1)
- [x] Digital clocks (Meta 11) (done, gaps DC1: the wall clock with the round time's digits on its own subsystem, its blue light, the emag's scramble and the multitool's reset, the welder's repair, the wrench taking it down as its frame)
- [x] Newscasters and newspapers (silicons SI6b; Meta 94+2; win: PhysicalNewscaster, Newspaper, NtosNewscaster): the news network, channels, stories, comments, censorship and the wanted issue, the printed newspaper, the announcements and the earnings report on the news, the bounty board (win: BountyBoard, NtosBountyBoard). Left: the photos' pictures (D104), the round's newscaster log
- [x] Holopads and holocalls (silicons SI4; Meta 89; win: Holopad)
- [x] Faxes (gaps S3; faxing CentCom stays with the admin tools; Meta 16; win: Fax, NtosFaxBond) and photocopiers (Meta 13; win: Photocopier)
- [ ] The teleporter: hub, station and console (Meta 2 each; win: Teleporter), the hand teleporter (Meta 2), quantum pads and launchpads (win: LaunchpadConsole, LaunchpadRemote; the briefcase launchpad and its remote: syndicate G2; the hub, station, console and hand teleporter: done, gaps TP1)
- [ ] The gateway's station side (Meta 1 and its console; win: Gateway); away missions are in section 12
- [x] The communications console's leftovers: buying shuttles, messages to CentCom and the Syndicate, the status display messages (gaps CM1: the shuttle catalogue with tg's purchasable emergency shuttles converted, DV188, the nuke codes' request, the spare ID safe code's drop pod, the console hacks, the syndicate console; left: the cross-sector messages to other servers, the battlecruiser card, the ninja's drain, the Scrapheap Challenge and Arena shuttles, not-translated.md 200)
- [x] The captain's chain of command (Meta 1; `/obj/item/melee/chainofcommand` 3/3) and the station charter (`/obj/item/station_charter` 3/3)

---

## 6. Silicons and the AI (`modules/mob/living/silicon` 528/528)

- [x] The AI (silicons SI4; the voice changer: SI8): the AI and cyborg job datums, the core and its construction (`game/machinery/ai_core` 98/98), the eye and camera movement, the AI HUD, clicking machines from afar and the airlock window (win: AiAirlock), holograms over holopads, tracking people, deploying into shells, the core and status display pictures (win: AiCoreDisplayPicker, AiStatusDisplayPicker), the voice changer (win: AiVoiceChanger), the AI slippers (Meta 12; `/obj/machinery/ai_slipper` 1/1)
- [x] AI laws (silicons SI1; the ion storm event SI11 and the malf AI's upgrade modules SI8 done): the law datums (`datums/ai_laws` 31/31), the law rack that replaces the upload console in this snapshot (Meta 1; win: LawRack), law modules (0/49; Meta 3; win: StateLawUi), ion laws
- [x] Carding and restoring (silicons SI4): the intellicard (Meta 3; win: Intellicard), the AI restorer (win: AiRestorer, NtosAiRestorer)
- [x] Cyborgs (`silicon/robot` 17/17): the 13 models, 90 borg items, upgrades, the cyborg HUD, recharge stations (Meta 10), building a cyborg (the robot suit, Meta 1), locking and detonating from the robotics console (Meta 1; win: RoboticsControlConsole, RemoteRobotControl, NtosCyborgRemoteMonitor), the borg's own windows (win: BorgHypo, BorgShaker, NtosRobotact) (silicons SI2; the AI shells with the AI, SI4, the syndicate models with SI9)
- [x] Brains for shells: the brain processor (this snapshot's MMI, Meta 5) and positronic brains (silicons SI2)
- [x] pAI (`modules/pai` 121/123, silicons SI6; the Newscaster and Music Synthesizer software with SI6b and SI6c): pAI cards (Meta 5), the software and the holoform (win: PaiCard, PaiInterface, PaiSubmit)
- [x] Maintenance drones and the drone dispenser (Meta 1; `basic/drone` 7/8 live, silicons SI7)

---

## 7. Creatures and bots

Already ported: the human and the ghost; 12 of 563 basic mob types (mimics, constructs, guardians, heretic summons and the
soulscythe, with their antagonists still to come). The holoparasites (guardians, 11 types) are live since syndicate X2.

- [x] The basic mobs' AI (gaps C1; `datums/ai` 399 types live, 107 inert with their hosts; all 343 behaviour trees embedded): planning, behaviours, targeting, pathfinding (JPS, SSpathfinder), the basic mob. Left: each creature's own trees and leaves (C2-C5), `not-translated.md` section 104
- [x] Meta's pets (gaps C2; Tom and Pete wait for C4; left: `not-translated.md` section 108): Ian and the corgi puppies, Runtime, Poly (and its speech memory), Renault the fox, McGriff the pug, Sgt. Araneus, Citrus the sloth, Wags-His-Tail the lizard, Tom the mouse, Pete the goat, the vomit goose, the baby penguin; petting, pet commands and taming
- [x] Pun Pun and the monkeys (gaps C5, section 4)
- [x] Bots (gaps C3, with the navigation beacons and the bot assemblies; left: `not-translated.md` section 109, NtosBotControl with S4; before it `basic/bot` 31/31, Meta 11): cleanbots (Meta 3), the medbot (1), MULEs (3; win: Mule), the repairbot (1; win: RepairBot, RepairbotResources), secbots (Beepsky's armsky and officer, Pingsky; 3), floorbots, firebots, honkbots, hygienebots, vibebots; the navigation beacons (live; win: NavBeacon), bot assemblies (Meta 1), the bot controls (win: SimpleBot, BotAnnouncement, NtosRoboControl)
- [x] Vermin: mice (chewing cables), cockroaches, rats (gaps C4; left: `not-translated.md` section 110)
- [x] Space fauna: carp (13 types) and other space mobs (gaps C4: carp, bears and snakes live; the ruin and gateway fauna, the gorilla and the deer with gaps C6; those of an antagonist or event wait for it)
- [x] Farm animals: chickens and eggs, cows and milk, goats, pigs, sheep, rabbits; butchering (`butchering` component inert) and riding (`riding` component inert) (gaps C4; the gorilla and the deer with C6)
- [ ] Lavaland's fauna (`basic/mining` 0/48), its megafauna and elites, raptors and ranching (24 types; win: RaptorDex, NtosRaptorDex): not in the gaps campaign, the owner ports them in a session of their own (D342)
- [ ] Hostile creatures of events and antagonists (spiders, xenos (the player xenomorphs done, gaps X5; the AI-controlled basic aliens are 9/18 live), blob minions, hivebots, gorillas, bears, clowns, skeletons, zombies, statues, demons): with their events and antagonists

---

## 8. Items

Already ported families (for reference): tools, gas tanks, radios and encryption keys, flashlights (31/38), extinguishers,
medical stacks, defibrillators, syringes, hyposprays, pills and patches, health analyzers, restraints, most ammunition,
circuit boards, stock parts, ID cards, fire axes, electronics, wall frames, RCLs, inducers, skillchips, medkits, sheets and
tiles (stacks 589/621 since gaps SK1: the hides tanned, every sheet's recipes, the wall fillings; the golem food waits for the golems and
the runed metal for the cult, `not-translated.md` section 214).

- [x] Clothing leftovers (gaps CL1; gaps J1 made the 58 inert clothing types the jobs start in live, with the accessories, medals and hoods; before it 817/1794 live; Meta 161 inert pieces: hazard vests, traffic cones, gas masks, surgical masks, welding masks and goggles, science goggles, earmuffs...): their own procs (mask sounds, visor toggles, ear protection), accessories, suit sensors (section 5.2): the procs of `modules/clothing` left (glasses, masks, hats, gloves, shoes, suits, jumpsuits, badges, neckwear), the laser tag game, the strong pull, the series and onwear mood components, the theatre's fake wizard costumes, the clothing's suicides. Left (not-translated.md 209): chameleon clothing (traitor gear, D333), the plasmamen's and ethereals' clothing (species), the reactive armour, the polymorph belt and the perceptomatrix (anomaly cores), the horrific necktie and the mind-monkey helmet (ghost polls), the collar bomb, the admin outfits, the fishing gloves, the pearl necklace, the wheelys
- [ ] Cups and containers (310/314 live since gaps OD1's jerrycans, OD2's blastoff ampoule and BR1's bitrunning bottles, 135 before the food and bar packages; Meta 92 inert then; left: the cult's and the heretic's flasks and the Lavaland potions): drinking glasses, bowls, mugs, cans, the shaker, salt and pepper
- [x] MODsuits (`modules/mod` 817/882; `/obj/item/mod` 234/245 live; Meta: 12 MOD units in the suit storage units, 7 modules and the maintenance MOD spawner): the control unit, themes, 100+ modules, the MOD installer and paint (win: MODsuit, MODpaint, NtosMODsuit) (gaps MO1, the core: the control unit with its themes, skins and parts, deploying and sealing, the cores, the module base with the general and visor modules, the action buttons and pins, the AI and pAI assistant, the MODlink with the scryer and its calls, the paint kit and skin appliers, the shell's construction, the wires, the hat stabilizer, the MODsuit Control app (win: MODsuit, MODpaint, NtosMODsuit); gaps MO2, the modules: the engineering, medical, security, supply, service, science, maintenance and timeline modules, the storage of holding, kinesis and pathfinder modules, the anomaly core slot and the antagonists' modules, the uplink's MODsuits; left: the space ninja's modules (M10+) and the soul shard cores (the antagonists, D333), the circuit module (wiremod), and leaves of unported systems (particles, achievements, the anomaly cores, body textures), not-translated.md 219 and 222)
- [x] Paperwork (`modules/paperwork`, gaps S3; M9x): paper (368 types; Meta 20 sheets, 47 paper bins, 6 carbon bins) with writing and stamping (win: PaperSheet), pens (Meta 67), folders (89; win: Folder), clipboards (8; win: Clipboard), stamps (15), the hand labeler (15), the paper cutter (4), filing cabinets (17; win: FilingCabinet), notice boards (16; win: NoticeBoard), carbon paper, paper planes
- [x] Smoking (gaps J3): cigarettes and cigars, packs, lighters and zippos, matches, pipes, cigarette butts; the e-cigarettes (9/9 live, gaps OD1: the cap, the voltage, the emag's smoke and burst). Left: the smoke particles, rolling rollies with dried plants (P2), `not-translated.md` section 107
- [x] Grenades and explosives (76/79 live; C4 is done; the smoke grenade by D303; gaps F1: chem grenades and casings (Meta 4), smart metal foam grenades (10), cleaner grenades (3), antiweed (1), teargas, clusterbusters and gas crystals): flashbangs, EMP grenades, the syndicate's charges, landmines and traps (done, gaps ST5: the landmines and the mine spawner, the magic traps and the tripwire; the fugitive hunters' bounty trap waits, not-translated.md 213), the grenade launcher (Meta 1); the IED and the halved pipe (done, gaps WP1) (syndicate G2: the minibomb, HE, frag, gluon, EMP and hypnotic grenades, the pizza bomb, the Earthcracker, the syndicate bombs and beacons); the rotfrag and rotsting grenades (admin goofs; done, gaps OD3). Left: the ninja's charge and the heretic's rust sower (antagonists, D333), the MODsuit's mirage grenade (its module, gaps MO2)
- [x] Guns left out of M7x (240/333 live; the projectiles tree 943/1077): the kinetic accelerator, plasma cutters, syringe, dart and chem guns, launchers, bows and crossbows, toy and foam guns, magic guns and staves, syndicate guns, attachments and gun crafting (done, syndicate G4b: the reagent gun, the meat hook with the magic gun base, the pie and pneumatic cannons, the blast cannon, the bolt rifle base and the rebar crossbows; done, gaps GU1: the special energy guns (the ion carbine, the plasma cutters, the wormhole projector, the gravity gun, the tesla cannon, the marksman revolver, the photon cannon, the Event Horizon), the dueling pistols and their case, the laser gatling, the crank guns, the lasers, energy, pulse and stun guns left, the SC/FISHER, the kinetic accelerator's light mount, the guns' fantasy bonuses, the firing pins, the bolt-action rifles, pipe guns and SKS, the pistols and shotguns left, the bows and arrows, the Donksoft guns and magazines, their casings and projectiles, these guns' light and bayonet mounts, the gun kits' recipes). Left (`not-translated.md` section 215): the magic guns, staves and wands (53, the wizard, D333), the 22 CTF guns, the antagonists' guns (the cycler, the harpoon gun, the enchanted rifle, the Ansem/SC pistol, the Hand of Midas, the heretics', the cult's and the abductors'), the away missions' four, the wiremod gun, the bloodchill and the psyker chaplain's revolver
- [x] Melee leftovers (32/105 live; since gaps WP1 the types of `weaponry/melee/`, `weaponry/shields.dm` and `tools/medical/cane.dm` are 166/176 live): energy swords (done, syndicate G2: the energy axe, swords, sabers, cutlass, blades, the double-bladed sword, the power fist; the implants' hardlight blade, gaps P6), shields (17/17; done, gaps J4 and WP1: the strobe shield, the ballistic shield's plates), the chain of command (Meta 1; done, gaps WP1, with its right-button haste), knives (27/27 live; Meta 5; their suicides, gaps WP1), spears (16/16; done, gaps WP1: the explosive lance, the Grey Tide with its illusions, the bone, bamboo, military and dragonator spears, the wireprod, the headpike, the pillow lance), claymores and katanas (gaps DE1: the claymore's base with the carrot, the shortsword and the gladius, the weak and ceremonial claymores; the katanas, gaps WP1; left: the cutlass and the highlander's; and `melee/misc.dm` whole: the bee sword, the curator's whip, the roasting stick, the cleric mace, the sord, the carpenter hammer, the bamboo staff, the moonlight greatsword), chainsaws, the sabres, baseball bats with kneecapping, the high-frequency blade, the supermatter sword, the stunsword, teleprods and nunchaku (done, gaps WP1), canes (1; done, gaps WP1: the cane, the crutches, the white cane), the broom (1; live). Left: the soulscythe (Lavaland, `not-translated.md` section 211), the stunsword's reskins (section 114), the cutlass and the highlander's claymore (section 206)
- [x] Storage leftovers (gaps SR1; `/obj/item/storage` 726/752 live, every kind mapped on Meta live): boxes (262/274), bags (43/45), belts (87/91), backpacks (96/103), toolboxes (45/46), lockboxes (16/17), can rings (5/5), secure briefcases, candle and crayon boxes, cafeteria trays, monkey cube boxes and dice bags (live before; dice bags: gaps DE1), fishing toolboxes (done, gaps SR1: every `PopulateContents` of `items/storage/`, the paper sack's designs and hat, the gum packets and wrappers, the can ring's count, the organ transport box's coolant freezing what it holds, the test tube racks, the garment bags, the sheaths' draw and counterattack, the wand belts and bandolier, the bag of holding's rift, the MEAT backpack, the quivers and money bags, and the storage kinds of `datums/storage/subtypes/`). Left, `not-translated.md` section 208: the healing explorer's webbing (Lavaland's monster organs), the rumble royale kit, the fishing boxes and the super fishing toolbox (fishing), the living duffel bag (the curse of hunger), the decrypted curiosity (bitrunning); the dueling case came with gaps GU1, and the 28 `/obj/item/storage` types defined outside `items/storage/` (RPEDs, fish cases, fishing bags, the snail's shell and others, with their own packages)
- [x] Assemblies (the 31 types of `modules/assembly` all live; Meta 47): timers (11), proximity sensors (6), the mousetrap (1), health sensors, infrared emitters, voice analyzers (win: ProximitySensor, HealthSensor, InfraredEmitter), the electropack (Meta 2; win: Electropack), the compact remote (Meta 4) (done, gaps AS1: the assembly holder with its two halves drawn, the timer (win: Timer), health sensor, infrared emitter and its beam, armed mousetraps and the mousetrap box, the voice analyzer; holders on gibtonite, fuel tanks, cups and tank bombs, the shock kit's electric chair; the compact remote is wiremod and the IED is inert: not-translated.md section 205)
- [x] Disks (99/100 live; Meta 21): tech and design disks, ordnance disks, NtOS program disks (inserting, ejecting and the file manager's copying: done, syndicate G3d, with the syndicate and virus disks), DNA data disks (done, gaps P3), the nuke disk; the floppy disk's write-protect tab, its stickers (written or picked from a radial), the disk stacks and the flat parcel, the theme and black market disks (done, gaps DV2). The bitrunning disks came with gaps BR1 (their spells and trophies wait, `not-translated.md` section 230). Left: the syndicate AI upgrade disk (`not-translated.md` section 216)
- [x] Rapid part exchange devices (gaps OD3; `research/part_replacer.dm` and its storage whole, 13 types made live; not mapped on Meta: the protolathe prints them, cargo sells the crate, an engineering cyborg's upgrade adds one): the RPED swapping an open machine's parts for better ones and listing a shut one's, the bluespace RPED at range through its beam and its guard against reagents and rigged cells, the tiered, cargo and cyborg RPEDs; a machine frame filled with its board and parts, a computer frame wired and glassed, a vending machine restocked; the storage's thirty-stack quota and the lowest-tier dump
- [x] Items' odds (gaps OD1; 102 types made live; Meta 19 Space Law books, the party poster, a can and a boritos bag): the ruins' puzzles (`puzzle_pieces.dm` whole: the hologrid plates, the light mechanism, the buttons, keycard pads, password and PIN panels, the shield and one-way gates, the pod door relay and the PIN's clues), the inhaler and its canisters with albuterol, the jerrycans, the instrument, augment and chaplain's armaments beacons, the taster, the bear armor, the door seal's suicide, the boritos bags and the crushed and food cans, the balloon mallet, the surgical razor's tray picture, the stingball's rebounds, the party poster's tail pin, the ID's honorific picker and the manuals' suicides. Left: the wizard's teleportation scroll (`not-translated.md` section 217)
- [x] Devices (`items/devices` 698/724 procs; gaps DE1 and DV2): binoculars (4), tape recorders and tapes (9+1; done, gaps DE1: recording, playback, the transcript, the tape's sides, unspooling and respooling), telephones (9; the red phone, done, gaps DE1), the laser pointer (1), the inspector (2; done, gaps DE1: the N-spect contraband scan), the universal price scanner, the radio jammer (done, syndicate G2), the chameleon projector (done, syndicate G1d), pinpointers (9/10 live; left: the vent pointer, which waits for mining's ore vents, `not-translated.md` section 206), the bluespace locator (win: BluespaceLocator; done, gaps TP1), the radioactive microlaser (win: RadioactiveMicrolaser; done, syndicate G2), the eight ball (win: EightBallVote; done, syndicate G4a), the reverse bear trap (done, syndicate G4a); the boombox and its music tapes, the hydro-vaporizer, the forcefield projector, pressure plates, the table clock, the quantum spin inverter, the desynchronizer, the hemoanalytic scanner, the quantum keycard, the pipe painter, the anomaly neutralizer and releaser, the polycircuit, the slime scanner, the flashlights' and scanners' leftovers (the flashdark, the space bubble lantern, the penlight's holosign, the disease analyzer), the machine wand and the portable wind turbine (done, gaps DV2). Left: the battle royale (an admin event), the syndicate intelliCard (`not-translated.md` section 216); the debug items (the human spawner, the omnitool, the spawntool) are done, gaps OD2
- [ ] Household and odds (gaps DE1 for the most): pillows (6; done, gaps DE1, with the pillow suit and the mattress shield; the pillow lance, gaps WP1), lipstick (7; done, syndicate G3b), the desk bell (12; live), skub (1; done, gaps DE1), the rubber ducky and the bike horn (2; live), banners (15/15 and the banner backpacks, the godstaves, the courtroom's gavel, the dice and dice bags but the Die of Fate, the shooting targets and the melee file's odd weapons: done, gaps DE1; Meta 1), the V8 engine and the House Edge, the street drugs, the flatpacks and the flatpack cart, the piggy banks, the ethereal disco ball, the busts and the Maneki-Neko, the virgin mary, the rolling table dock, the bot pad and its remote, the carp DNA sampler, the lead pipe and the lead-acid battery, the hair dye spray, the gun maintenance kits and the pitchfork (done, gaps OD2; Meta: the two flatpacks, the battery, the vault piggy bank), vending refills (56/57, gaps S2), vehicle keys (7/7 live; Meta 2), granters (manuals that teach skills and spells, `/obj/item/book/granter` 35/47 live: the skill, crafting, sign language, origami, chuunibyou and smoke manuals, done, gaps SL1; the wizard's 12 spellbooks wait for the wizard). Left: the wizard's spellbooks (section 221) and the Die of Fate (`not-translated.md` section 206), the skateboards (they wait for the vehicles), the piggy banks' persistence (section 218)
- [x] Toys (gaps TY1): `toys.dm` whole (the water balloon, the cap gun, the toy swords and the double-bladed toy, the windup toolbox, the snap pops, the talking figures with their chatter voices, the toy nuke and AI, the action figures, the dummy, the steampunk watch, the Codex Cicatrix toy, the Intento, the banhammer and the rest; the floor-placeable toys are set down where the floor is clicked), the toy mechs and their battles (`toy_mechs.dm`), the latex glove balloon, the hourglass. Left: the trading card game, the wizard's frog statue (`not-translated.md` section 212); the Donksoft guns came with gaps GU1

---

## 9. Structures and furniture

Already ported: tables, chairs, beds, windows, grilles, girders, lattices, railings, racks, most closets and crates, signs,
posters and plaques, fire axe and extinguisher cabinets, frames, plastic flaps, door assemblies, barricades, transit tubes,
holosigns.

- [x] Closets and crates leftovers (closets 130/293, crates 33/113 live; Meta 8 inert closets): the cardboard box's sneaking (done, syndicate G3a), the secure crate and the syndicrate with its key (done, syndicate G4c), the laundry and trash carts, coffins, critter crates, the abandoned crate's lock puzzle (win: AbandonedCrate) (done, gaps ST3: every closet's and crate's contents left, the trash and laundry carts, the trash bin, the cardboard, critter and stasis crates, the glittery crates, the infinite closet, the abandoned crate's deca-code lock with its loot and window; the see-inside view from a shut closet is not-translated.md 207)
- [x] Reagent dispensers (8/37 live; Meta 69): water coolers (3), plumbed tanks (11; done, engineering E15), serving dishes (2) and the other tanks (done, gaps ST3: the water cooler's cups, jug and tipping, the serving dish, the irish keg; the wall dispensers with gaps SE1)
- [x] Water structures (gaps H1, `structures/water_structures` whole): sinks, toilets, urinals and puddles (section 5.4)
- [x] Decor: potted plants (Meta 63; done, gaps ST1) and flora (215/215; Meta 58; `flora.dm` done, gaps ST1; Lavaland's ash flora, done, gaps OD3: picked by hand, regrowing, with their light), statues (`/obj/structure/statue` 44/44; Meta 2) and sculpting, showcases (Meta 20; done, gaps ST2), plaques (2; done with the engraving, gaps ST2), the decorative fluff (1) (statues and sculpting: silicons SI2; the petrified statue: gaps ST5; left with their ruins and the abductors: three fluff pieces, the iced abductor, the nuke balloon and the forgotten ship's captain's sleeper, and the Hilbert hotel's broken TV showcase)
- [x] Furniture (gaps ST2): mirrors and dressers (done, gaps H1), curtains, bookcases, easels, display cases, notice boards (section 5) (the easels and the notice boards were live already)
- [x] Odd structures on Meta (gaps ST2): the broken flooring decals (5), the tank holders (3), the mannequin (1), the basketball hoop (1), marker beacons (57) (the marker beacons: silicons SI2)
- [ ] Buildable or orderable, not mapped: bonfires and firepits, the fireplace, the guillotine, the loom, beeboxes, aquariums, mineral doors, platforms, the spirit board, the cat house, the bouncy castle, the pinata, the mystery box, shipping containers, floodlights (done, gaps ST4: the bonfires with their grill and stake, the fireplace, the guillotine, the loom with the loomable element, the mineral doors, the platforms, the spirit board, the small steps; done, gaps ST5: the mystery box, the bouncy castle, the shipping containers and billboards, the voting box, the signboards, the fences, the gun lockers, the toilet bong, the chess pieces, the paper frames, the rolling table, the brass and plastic chairs and the handrail; the cat house and the pinata were live before; the aquariums wait with fishing)

---

## 10. Beyond the station: MetaStation's other levels and shuttles

In tg a MetaStation round also loads Lavaland (its mining level: Meta's map config names no other), space levels with
ruins, the CentCom level and the transit and reserved levels. The port runs the station level only (D90: one level).

- [ ] Lavaland: the mining level and its base, lava and chasms (`chasm` component inert), ash storms, the necropolis and tendrils with their loot, the fauna, megafauna and elites (the mobs and tendrils: the owner's own session, D342), ash walkers, the lavaland ruins (the ash flora and the elephant graveyard's parts: done, gaps OD3: the bone statues, the wasteland's earth and very strong rock, the oil well, the filled graves)
- [ ] Space levels with ruins: their templates are data (live 168/169), their content is not; derelicts and the whiteship (Meta's config names a Meta whiteship)
- [ ] The CentCom level: where the shuttles arrive, the admin rooms, the Thunderdome, the syndicate and wizard bases
- [ ] Shuttles beyond the emergency shuttle: the cargo, mining and labor shuttles' trips (their consoles are inert: `computer/shuttle` 30/33; win: ShuttleConsole), the arrivals shuttle, the ferry, the whiteship, custom shuttles (the custom docking port is inert; win: ShuttleBlueprints), buying shuttles, the emergency shuttle console's authorizations (win: EmergencyShuttleConsole)
- [x] Weather (`datums/weather`, gaps E2a): SSweather, every weather type (ash, radiation, rain, sand, snow, void storms, the floor is lava), the storms' sounds, drawn by the client (protocol 84)

---

## 11. Interface, admin tools and minigames

### 11.1 Interface and HUD

- [ ] The tgui windows the client does not draw: 396 of tg's 444; the full index is section 13
- [ ] The HUDs of other mobs (`/datum/hud`: pAI (done, silicons SI6), alien and larva (done, gaps X5), blob overmind, revenant, guardian and dextrous (done, syndicate X2), soulscythe); the human and ghost HUDs are live (the robot's: silicons SI2, the AI's: SI4)
- [ ] The remaining screen alerts (`/atom/movable/screen/alert` 183/201 live)
- [ ] Action buttons: item actions (`/datum/action/item_action` 107/125 live); spells (226), innate actions (`/datum/action/innate` 64/162) and vehicle actions (`/datum/action/vehicle` 30/43) come with their owners
- [ ] tg's stat panel (the Status tab: round time, the shuttle's ETA, the security level; the MC and admin tabs): the client has only the command verbs' tabs (silicons SI4's Commands window)
- [x] tg's escape menu: an Esc menu with tg's items in the client's own style (D282; M10x G3-G6, 1.0.4-pre-alpha to 1.0.6-pre-alpha)
- [ ] Hotkeys help (win: HotkeysHelp); tg's keybindings (`/datum/keybinding` 31/86 live; the client's Settings window has a Keybindings tab, D282)
- [ ] tgui input leftovers (win: CheckboxInput, KeyComboModal)
- [ ] The minimap (`modules/minimap` 1/127, new in this snapshot; D315: `load_z` translated for every player's minimap, DV266; the tacmap action, the HUD display, blips, labels and drawing, the minimap table not yet)
- [ ] The new players' tutorials (`modules/tutorials` 0/48)
- [ ] The playtime tracker, the achievements, the votes and the memories (sections 2 and 4; the language menu came with gaps L2)

### 11.2 Admin and server tools

Already ported (M10 I, D275): kick, local bans and notes in SQLite, the dev commands.

- [ ] Admin verbs (`modules/admin` 177/1,643): the player panel, view variables, the spawn panel (win: SpawnPanel, SpawnSearch), secrets (win: Secrets), smites, forcing events (win: ForceEvent), the dynamic panel (Check Antagonists, the traitor panel, the team panel and the combo HUD: syndicate A1), the outfit manager and editor and select equipment (win: OutfitManager, OutfitEditor, SelectEquipment), command reports (win: CommandReport), the admin PDA and fax (win: AdminPDA, AdminFax), the law and borg panels (win: Lawpanel, BorgPanel), the beaker panel (win: BeakerPanel), the skill panel (done, gaps SL1: the console's skill-panel; win: SkillPanel), Assume and Give Direct Control (done, D346: the dev possess, unpossess and givecontrol, the Player tab's Possess section and tool), the sentience balloon, ghost pool protection, the trophy and painting panels, the CentCom pod launcher (win: CentcomPodLauncher), the shuttle manipulator (win: ShuttleManipulator), the admin verb panel (win: AdminVerbPanel), the policy panel (win: Policypanel)
- [x] Admin help tickets and admin PMs (win: Adminhelp; M10x G6, 1.0.6-pre-alpha)
- [ ] The player ticket history (win: PlayerTicketHistory, the database), prayers and the requests to CentCom and the Syndicate (win: RequestManager), new players' interviews (win: Interview, InterviewManager)
- [ ] Debug windows: appearance and plane debuggers, filterrific, the particle editor, the color matrix editor, the light controller and spawner, the MC overview and dependency debug, the timer and atmos control panels, the AI behaviour tree viewer, the log viewer, the event logger, the sprite auditor, the color blind tester, the matrix math tester
- [ ] Build mode (`modules/buildmode` 0/102)
- [ ] The logs, the blackbox and the round's data file (not-translated.md 82, 83)

### 11.3 Minigames

- [ ] Deathmatch (`modules/deathmatch` 0/113; win: DeathmatchLobby, DeathmatchPanel)
- [ ] Capture the flag (`modules/capture_the_flag` 0/101; win: CTFPanel)
- [x] Mafia (gaps S4d; `modules/mafia`, the ghosts' minigames menu; win: MafiaPanel, NtosMafiaPanel)
- [ ] Basketball (`modules/basketball` 29/51; win: BasketballPanel) (the hoop and the ball: gaps ST2; the minigame: not-translated.md 204)
- [ ] The PDA's games and toys: the arcade, the virtual pet, space betting, the emojipedia (the arcade, the pet and the emojipedia done, gaps S4c-S4d; space betting comes with P4; win: NtosArcade, NtosVirtualPet, NtosSpaceBetting, NtosEmojipedia)

---

## 12. Not on MetaStation, cut or obsolete (probably not ported)

Kept apart at the owner's request: tg has these, but a MetaStation round does not use them, tg is phasing them out, or tg has
removed them. Short notes only, no checkboxes; move a row into the main list if it is wanted after all.

### 12.1 Other maps only

- The tram and transport (`modules/transport` 366/368): TramStation's tram, its controls, crossing signals and plaques, the tram malfunction event; elevators and lift indicators (win: TramControl, TramController, ElevatorPanel, CrossingSignal, TramPlaque).
- Multi-z: stairs (0/8), ladders (0/5), open space and falling between levels. MetaStation is one level; IceBoxStation and TramStation are not.
- IceBoxStation's Icemoon: the icy mining level that replaces Lavaland there, its structures (`structures/icemoon` 0/6), snow storms, the ice wilderness and its fauna.

### 12.2 In tg's code but not mapped on MetaStation

A MetaStation round can still meet them (built from boards, ordered or found in ruins), but the map places none:
- Sleepers (0/8, win: Sleeper): tg's MetaStation maps stasis beds instead (D216).
- New machines of this snapshot that Meta does not map: the mail sorter, the produce teleporter, the dish drive, the lipid extractor, the robotic factory that turns people into cyborgs, the reconnaissance platform (minimap table), the nebula shielding machine (its station trait has weight 0 and is never rolled at random).
- The experimental cloner (`game/machinery/experimental_cloner` 0/64; win: ExperimentalCloner): a ruin's prototype of the outlawed cloning pods.

### 12.3 Away missions, ruins, admin and debug content

- Away missions behind the gateway (`modules/awaymissions` 13/136, `_maps/RandomZLevels`): the away maps and their content. Meta's gateway itself is in the main list (5.7).
- Ruin-only content (`modules/mapfluff` 23/268): the ruins' own machines, mobs, puzzles and loot (the vault controller, win: VaultController; the lore terminals, win: Terminal; the wish granter; the abstract puzzle gizmos; the anomalous crystals).
- Admin-only and one-off: the highlander, the greentext, the admin's CentCom rooms; the pirates' data siphon comes with the pirates (section 3).
- Debug tools: the debug chemical synthesizer and recipe debugger (win: ChemDebugSynthesizer, ChemRecipeDebug), the debug component printer, the cyborg boot debug (win: CyborgBootDebug), `modules/debugging`.

### 12.4 Replaced by the port's own engine (not ported by design)

- Lighting (`modules/lighting`): the client's light grid (D24); the server's light level per tile is ported (D170).
- Map loading (the `.dmm` reader and loader of `modules/mapping`): the map converter (D46).
- BYOND's client plumbing: `modules/asset_cache`, `modules/tgui_panel` and `modules/tgchat` (the client's own chat windows, M9), the DMF skin (`interface/`), the stat browser, BYOND's right-click menu and verb panels (the radial, D49), `/datum/browser` popups, `modules/tooltip`.
- Server plumbing: `modules/tgs`, `modules/discord`, `modules/autowiki`, `modules/error_handler`, `modules/unit_tests` (tg's tests are ported as adapted subsets), Lua scripting (DREAMLUAU is off in the port's compile).

### 12.5 Obsolete in tg's code

- `/mob/living/simple_animal` (`modules/mob/living/simple_animal` 47/448): tg's legacy mob base. New and migrated mobs are basic mobs; what is left (the megafauna, oozes, a few hostile mobs) ports with its content, or as basic mobs if tg migrates it first.
- `code/game/gamemodes` (0/98 types): the remains of the old game modes, replaced by Dynamic; only objectives and helpers live there now.
- Cloning's leftovers on Meta: the cloning pod exhibits (Meta 2) and the old pod showcases, decorations of a removed mechanic.
- tgui windows with no DM user (sub-parts of other windows or dead): CodexGigas (the removed devil's), EngravedMessage, Fabrication, RestockTracker, SeedTable. (BountyBoard is the newscaster's bounty tab and NtosBountyBoard's content, done with silicons SI6b.)

### 12.6 Removed from tg (not in the snapshot)

Well-known SS13 mechanics that are not in tg any more; nothing to port. Checked against the tracker: none of their types exists.
- The old game modes (replaced by Dynamic), hardsuits (MODsuits), the old PDA item (NtOS PDAs), cloning pods and the cloning console, nanites, the old integrated circuits (wiremod), telescience, the antimatter engine, radiation collectors, the plant DNA manipulator, the AI upload console (module racks in this snapshot), the step-by-step surgeries (operations in this snapshot).
- Antagonists: gangs, the devil, the clockwork cult, swarmers, the sentient disease, the hivemind, monkey mode. The shadowling and vampires were never tg's.

---

## 13. Index of the tgui windows not drawn yet

396 of tg's 444 windows (the client drew 48 when this index was made); 262 of the 396 are ticked now. One row per
window, grouped by where its mechanic sits above; tick it when the client draws it (a registration in TguiWindows.cs), in the
package that draws it. Windows of section 12 are listed for completeness.

### Live mechanics missing their window (section 1): 12 (12 drawn)

- [x] AtmosFilter (E1)
- [x] AtmosMixer (E1)
- [x] GravityGenerator (E4)
- [x] IVDrip (G1)
- [x] LootPanel (G1)
- [x] MemoryPanel (gaps G4)
- [x] PipeScrubber (E3)
- [x] PortablePump (E3)
- [x] PortableScrubber (E3)
- [x] Safe (G1)
- [x] Supermatter (E2)
- [x] Tank (E3)

### The round and antagonists (sections 2, 3): 47 (24 drawn)

- [ ] AbductorConsole
- [ ] Achievements
- [x] AiVoiceChanger (silicons SI8)
- [ ] AntagInfoBlob
- [ ] AntagInfoBloodWorm
- [ ] AntagInfoBrainwashed
- [x] AntagInfoBrother (syndicate X4)
- [x] AntagInfoChangeling (gaps K1)
- [ ] AntagInfoDemon
- [x] AntagInfoGeneric (syndicate A1)
- [ ] AntagInfoGlitch
- [ ] AntagInfoHeretic
- [x] AntagInfoMalf (silicons SI8)
- [ ] AntagInfoMorph
- [ ] AntagInfoNightmare
- [ ] AntagInfoNinja
- [x] AntagInfoRevolution (gaps V1)
- [x] AntagInfoSentient (syndicate G3c)
- [ ] AntagInfoSeparatist
- [ ] AntagInfoShade
- [ ] AntagInfoSpy
- [ ] AntagInfoSunwalker
- [x] AntagInfoTraitor (syndicate A2)
- [ ] AntagInfoVoidwalker
- [ ] AntagInfoWizard
- [ ] ApprenticeContract
- [x] BlackMarketUplink
- [x] BluespaceArtillery (gaps R1)
- [ ] CargoHoldTerminal
- [x] CellularEmporium (gaps K1)
- [x] ChameleonCard (syndicate package G1b)
- [x] DnaVault (gaps R1)
- [x] DynamicAdmin (syndicate A3)
- [x] DynamicTester (syndicate A3)
- [ ] GlandDispenser
- [x] KeycardAuth (gaps package J4)
- [x] LTSRBT
- [x] LingMMITalk (gaps K1)
- [x] MalfunctionModulePicker (silicons SI8)
- [x] NuclearBomb (syndicate N1a)
- [ ] ProbingConsole
- [x] SatelliteControl (gaps R1)
- [ ] Spellbook
- [ ] SpyUplink
- [x] StationTraitsPanel (gaps R2)
- [x] Uplink (syndicate A2)
- [x] VotePanel (the owner, 2026-11-08: votes)

### Jobs, character and ghosts (section 4): 10 (7 drawn)

- [ ] EcholocationFocus
- [x] GhostMenu (silicons SI6; the extra view distance left out, DV390)
- [x] HighLuminosityEyesMenu (gaps OG1)
- [x] LanguageMenu (gaps L2)
- [x] MinigamesMenu
- [x] SkillStation (gaps SL1)
- [ ] SlimeBodySwapper
- [x] SpawnersMenu (silicons SI7)
- [x] TacVisorEyesMenu (gaps OG1)
- [ ] TrackedPlaytime

### Engineering and atmospherics (5.1): 34 (33 drawn)

- [x] AirlockButtonController (E8)
- [x] AirlockController (E8)
- [x] AnomalousWeatherTower (E16)
- [x] ApcControl (E6)
- [x] AtmosAlertConsole (E6)
- [x] AtmosControlConsole (E5)
- [x] BigManipulator (E16)
- [x] Blueprints (E14)
- [x] ChemAcclimator (E15)
- [x] ChemAutomaticBuffer (E15)
- [x] ChemDisposer (E15)
- [x] ChemFilter (E15)
- [x] ChemMixingChamber (E15)
- [x] ChemPress (E15)
- [x] ChemReactionChamber (E15)
- [x] ChemSplitter (E15)
- [x] ChemSynthesizer (E15)
- [x] Crystallizer (E12a)
- [x] DecalPainter (E14)
- [x] Electrolyzer (E12a)
- [x] Hypertorus (E12b)
- [x] ManufacturingSorter (E16)
- [x] ModularShieldConsole (E10)
- [x] ModularShieldGen (E10)
- [ ] PipeDispenser
- [x] Portagrav (E16)
- [x] RapidConstructionDevice (E13)
- [x] RapidDecorationDevice (E13)
- [x] RapidPlumbingDevice (E15)
- [x] RapidTilingDevice (E13)
- [x] SpaceHeater (E9)
- [x] StationAlertConsole (E6)
- [x] ThermoElectricGenerator (E11)
- [x] TurbineComputer (E11)

### Medical (5.2, 4): 19 (19 drawn)

- [x] BloodFilter (gaps J5)
- [x] ChemDispenser
- [x] ChemHeater
- [x] ChemMaster
- [x] ChemSeparator
- [x] CrewConsole (gaps J5)
- [x] DnaConsole
- [x] ImplantChair (gaps DV2)
- [x] ImplantPad (gaps DV2)
- [x] InfuserBook
- [x] Limbgrower (gaps MD3)
- [x] MassSpec
- [x] MedicalKiosk (gaps MD1)
- [x] MedicalRecords (gaps SC2)
- [x] OperatingComputer (gaps P1)
- [x] Pandemic (gaps VI1)
- [x] PortableChemMixer
- [x] Reagents
- [x] SmokeMachine (gaps F1)

### Science (5.3): 30 (16 drawn)

- [ ] AdventureBrowser
- [ ] AnomalyRefinery
- [x] Autolathe (gaps RD1)
- [ ] CircuitAccessChecker
- [x] CircuitModule (gaps CI1)
- [ ] CircuitSignalHandler
- [x] ComponentPrinter (gaps CI1)
- [x] DestructiveAnalyzer (gaps RD3)
- [ ] DopplerArray
- [ ] ExodroneConsole
- [ ] ExoscannerConsole
- [x] ExosuitControlConsole (D321)
- [x] ExosuitFabricator (silicons SI3)
- [x] ExperimentConfigure (gaps RD3)
- [x] Experimentor (gaps RD4)
- [ ] Fabrication
- [x] Fabricator (gaps RD1)
- [ ] Flatpacker
- [x] IntegratedCircuit (gaps CI1)
- [ ] MassDriverControl
- [x] MechBayPowerConsole (D321)
- [x] Mecha (D321)
- [ ] MechpadConsole
- [ ] Microscope
- [x] OreSilo (Lavaland L2a)
- [ ] ProcCallMenu
- [x] ServerControl (gaps RD2)
- [ ] TankCompressor
- [x] Techweb (gaps RD2)
- [x] TransferValve (syndicate G4b)

### Service (5.4): 33 (14 drawn)

- [ ] Aquarium
- [ ] BattleArcade
- [x] Biogenerator (gaps P2)
- [x] Canvas (silicons SI4d)
- [x] Crayon
- [ ] DSMBook
- [ ] FishAnalyzer
- [ ] FishCatalog
- [ ] FishingCalculator
- [ ] FishingRod
- [ ] Holodeck
- [ ] IDCBook
- [x] InstrumentEditor (silicons SI6c)
- [x] Jukebox (gaps H3)
- [ ] LibraryAdmin
- [ ] LibraryConsole
- [ ] LibraryScanner
- [ ] LibraryVisitor
- [x] MarkdownViewer (J2)
- [ ] OrionGame
- [ ] PaintingMachine
- [x] PlantAnalyzer (gaps P2)
- [ ] ReligiousTool
- [ ] Roulette
- [x] SeedExtractor (gaps P2)
- [x] SeedTable (gaps P2)
- [ ] SlotMachine
- [x] SmartVend
- [ ] SparringContract
- [x] TrainingMachine (gaps ST4)
- [x] Trophycase (gaps ST2)
- [x] Vendatray (gaps ST2)
- [x] Vending

### Cargo, economy and mining (5.5): 25 (23 drawn)

- [x] AccountingConsole
- [ ] AuxBaseConsole
- [x] AvatarHelp (gaps BR1)
- [x] BankMachine
- [x] BountyBoard
- [x] Cargo
- [x] CargoExpress
- [x] CivCargoHoldTerminal
- [x] DestinationTagger (D325: the disposal mail)
- [x] DisposalUnit (D325: the disposal system)
- [x] Gps (silicons SI2)
- [x] GulagItemReclaimer (gaps LC1)
- [x] GulagTeleporterConsole (gaps LC1)
- [x] HoloPay
- [x] LaborClaimConsole (Lavaland L2b)
- [x] MatMarket
- [ ] MineBot
- [x] NetpodOutfits (gaps BR1)
- [x] OreBox (Lavaland L2a)
- [x] OreContainer (gaps ST5)
- [x] OreRedemptionMachine (Lavaland L2a)
- [x] ProcessingConsole (Lavaland L2b)
- [x] ProduceConsole
- [x] QuantumConsole (gaps BR1)
- [x] StackingConsole (Lavaland L2b)

### Security (5.6): 11 (11 drawn)

- [x] BrigTimer
- [x] DetectiveBoard (gaps package J4)
- [x] ForensicScanner (gaps package J4)
- [x] ForensicsSpoofer (gaps package J4)
- [x] HypnoChair (gaps SG1)
- [x] PortableTurret (syndicate package G4d)
- [x] PrisonerManagement (gaps LC1)
- [x] ScannerGate (gaps SG1)
- [x] SecurityRecords (gaps SC2)
- [x] TurretControl (syndicate package G4d)
- [x] WarrantConsole (gaps SC2)

### Command (5.7): 10 (8 drawn)

- [ ] Gateway
- [x] Holopad (silicons SI4)
- [ ] LaunchpadConsole
- [x] LaunchpadRemote (syndicate G2)
- [x] Newscaster
- [x] Newspaper
- [x] Photocopier (gaps S3)
- [x] PhysicalNewscaster
- [x] RequestsConsole (gaps SC1)
- [x] Teleporter (gaps TP1)

### Silicons and bots (6, 7): 22 (21 drawn)

- [x] AiAirlock (silicons SI2)
- [x] AiCoreDisplayPicker (silicons SI4)
- [x] AiRestorer (silicons SI4)
- [x] AiStatusDisplayPicker (silicons SI4)
- [x] BorgHypo (silicons SI2)
- [x] BorgShaker (silicons SI2)
- [x] BotAnnouncement (gaps package C3)
- [x] Intellicard (silicons SI4)
- [x] LawRack (silicons SI1)
- [x] Mule (gaps package C3)
- [x] NavBeacon (gaps package C3)
- [x] PaiCard (silicons SI6)
- [x] PaiInterface (silicons SI6)
- [x] PaiSubmit (silicons SI6)
- [ ] PetBuilder
- [x] PortraitPicker (silicons SI4d)
- [x] RemoteRobotControl (silicons SI4)
- [x] RepairBot (gaps package C3)
- [x] RepairbotResources (gaps package C3)
- [x] RoboticsControlConsole (silicons SI2)
- [x] SimpleBot (gaps package C3)
- [x] StateLawUi (silicons SI1)

### Items (8): 17 (17 drawn)

- [x] AbandonedCrate (gaps ST3)
- [x] BluespaceLocator (gaps TP1)
- [x] Clipboard (gaps S3)
- [x] EightBallVote (syndicate G4a)
- [x] Electropack (gaps J5)
- [x] Fax (gaps S3)
- [x] FilingCabinet (gaps S3)
- [x] Folder (gaps S3)
- [x] HealthSensor (gaps AS1)
- [x] InfraredEmitter (gaps AS1)
- [x] MODpaint (gaps MO1)
- [x] MODsuit (gaps MO1)
- [x] NoticeBoard (gaps S3)
- [x] PaperSheet (gaps S3)
- [x] ProximitySensor (gaps package C3)
- [x] RadioactiveMicrolaser (syndicate G2)
- [x] Thermometer

### NtOS (PDA and laptop) apps: 48 (42 drawn)

- [x] NtosAiRestorer (silicons SI4)
- [x] NtosArcade
- [x] NtosBotanicalEncyclopedia
- [x] NtosBountyBoard
- [ ] NtosCamera
- [x] NtosCard
- [x] NtosCargo
- [x] NtosCivCargoHoldTerminal
- [x] NtosCouponMaster
- [x] NtosCursor
- [x] NtosCyborgRemoteMonitor
- [x] NtosDeptOrder
- [x] NtosEmojipedia
- [x] NtosFaxBond (gaps S3)
- [x] NtosGasAnalyzer (silicons SI2)
- [x] NtosJobManager
- [x] NtosMODsuit (gaps MO1)
- [x] NtosMafiaPanel
- [ ] NtosNanopaint
- [x] NtosNetChat
- [x] NtosNetDos
- [x] NtosNetMonitor
- [x] NtosNewscaster
- [x] NtosPay
- [x] NtosPhysScanner
- [x] NtosPortraitPrinter (silicons SI4d)
- [x] NtosPowerMonitor
- [x] NtosRadar (syndicate N1b)
- [x] NtosRadarSyndicate (syndicate N1b)
- [x] NtosRecords
- [ ] NtosRestock
- [x] NtosRevelation
- [x] NtosRoboControl
- [x] NtosRobotact (silicons SI2)
- [ ] NtosScipaper
- [x] NtosSecurEye (syndicate G3d; person tracking: silicons SI4, DV373 retired)
- [x] NtosShipping
- [x] NtosSignaler
- [x] NtosSkillTracker (gaps SL1)
- [ ] NtosSpaceBetting
- [x] NtosSpectreMeter
- [x] NtosStationAlertConsole (E6)
- [x] NtosStatus
- [x] NtosSupermatter (E2)
- [x] NtosTechweb (gaps RD2)
- [x] NtosVirtualPet
- [ ] RestockTracker
- [x] SyndicateContractor (syndicate X1a)

### Beyond the station: shuttles and Lavaland (10): 5 (2 drawn)

- [x] EmergencyShuttleConsole
- [ ] NtosRaptorDex
- [ ] RaptorDex
- [ ] ShuttleBlueprints
- [x] ShuttleConsole

### Interface (11.1): 4 (1 drawn)

- [ ] CheckboxInput
- [x] ColorPickerModal (M10x D5, 1.0.6-pre-alpha)
- [ ] HotkeysHelp
- [ ] KeyComboModal

### Admin and debug (11.2): 49 (5 drawn)

- [ ] AchievementsAdminPanel
- [ ] AdminBookViewer
- [ ] AdminFax
- [ ] AdminPDA
- [ ] AdminVerbPanel
- [x] Adminhelp (M10x G6, 1.0.6-pre-alpha)
- [ ] AppearanceDebug
- [ ] AtmosControlPanel
- [ ] BeakerPanel
- [ ] BehaviorTreeViewer
- [ ] BorgPanel
- [ ] CentcomPodLauncher
- [x] CircuitAdminPanel (gaps CI1)
- [ ] ColorBlindTester
- [ ] ColorMatrixEditor
- [ ] CommandReport
- [ ] ControllerOverview
- [ ] EventLogger
- [ ] Filterrific
- [ ] ForceEvent
- [ ] GhostPoolProtection
- [ ] GreyscaleModifyMenu
- [ ] Interview
- [ ] InterviewManager
- [ ] Lawpanel
- [ ] LightController
- [ ] LightSpawn
- [ ] LogViewer
- [ ] LuaEditor
- [ ] MCDependencyDebug
- [ ] MatrixMathTester
- [ ] OutfitEditor
- [ ] OutfitManager
- [ ] PaintingAdminPanel
- [ ] ParticleEdit
- [ ] PlaneMasterDebug
- [ ] PlayerTicketHistory
- [ ] Policypanel
- [ ] RequestManager
- [x] Secrets (the power and light buttons live, DV351)
- [ ] SelectEquipment
- [ ] SentienceFunBalloon
- [ ] ShuttleManipulator
- [x] SkillPanel (gaps SL1)
- [ ] SpawnPanel
- [ ] SpawnSearch
- [ ] SpriteAuditor
- [x] Timer (gaps AS1; the timer assembly's window, not an admin one)
- [ ] TrophyAdminPanel

### Minigames (11.3): 5 (1 drawn)

- [ ] BasketballPanel
- [ ] CTFPanel
- [ ] DeathmatchLobby
- [ ] DeathmatchPanel
- [x] MafiaPanel

### Not for MetaStation (section 12): 15 (6 drawn)

- [x] ChemDebugSynthesizer
- [ ] ChemRecipeDebug
- [ ] CodexGigas
- [ ] CrossingSignal
- [x] CyborgBootDebug (silicons SI2)
- [x] ElevatorPanel (D325: multi-z elevators)
- [ ] EngravedMessage
- [ ] ExperimentalCloner
- [ ] Puzzgrid
- [ ] Sleeper
- [ ] Terminal
- [x] TramControl (the tram port)
- [x] TramController (the tram port)
- [x] TramPlaque (the tram port)
- [ ] VaultController
