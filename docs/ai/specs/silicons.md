# The silicons campaign: cyborgs, the AI, laws, pAI and drones (plan, 2026-11-11, D326)

The owner (2026-11-11): port the robots and synthetics, PORT-CHECKLIST section 6 ("Silicons and the AI",
`modules/mob/living/silicon` 0/528) and its 22 windows. From the lead's estimates the owner chose every package below (about
172 h), the AI's free eye with static, the exosuit fabricator with a starter-only techweb, and to start only after the
syndicate campaign (D316) is done.

## 1. Survey (types-index.tsv, snapshot 1d99bd7f)

| Piece | tg | State in the port |
|---|---|---|
| Silicon base (`mob/living/silicon/*.dm`: defense, damage, death, say, movement, examine) | ~96 procs | 7 translated |
| AI mob (`silicon/ai`, 21 files, 4972 lines) | 150 procs | 1 (only `weak_syndie` is live) |
| Cyborg mob (`silicon/robot`, 13 files, 3674 lines) | 165 procs | 0, all 17 types inert |
| Cyborg models (`robot_model`) | 13 models, 36 procs | inert |
| Borg items (`items/robot/items`) | 90 types, 172 procs | 47 types live, 36 procs |
| Borg upgrades (`robot_upgrades.dm`) | 44 types | 24 live |
| Law datums (`datums/ai_laws`) | 32 types, 16 procs | inert |
| Law rack (`ai_core/law.dm`, replaces the upload console) + law modules (`AI_modules`) | 5 + 48 types, 68 + 65 procs | inert (3 boards live) |
| AI core and its construction (`ai_core/_core.dm`, `core_construction.dm`) | 28 procs | inert |
| Camera net (`SScameras` 340 lines, camera chunks, `/datum/trackable`) | | live since SI4 |
| Eyes (`/mob/eye`, AI eye, advanced camera consoles) | 142 procs | `/mob/eye`, the camera and remote eyes, the advanced camera console and the navigation computer live since syndicate N1c (the client sees a camera eye from a ghost's orbit, DV382); the AI eye and the camera chunks are SI4's |
| Holopads and holograms (`hologram.dm`, 991 lines) | 55 procs | inert |
| Brain processor (this snapshot's MMI) and the positronic brain | 8 types | 1 live |
| Robot suit (endoskeleton) and assembly (`robot_parts.dm`) | 20 procs | inert; robot limbs are live (gaps P1) |
| Recharge stations (Meta 10), robotics console, intellicard, AI restorer | 19 + 9 + 29 + 10 procs | inert |
| pAI (`modules/pai`, 15 files, 1778 lines) | 123 procs | 0 (screen icons live) |
| Maintenance drones (`basic/drone`), drone dispenser, drone shells | 66 + 9 + 3 procs | 2 |
| Exosuit fabricator, techweb, R&D machines, autolathe | 29 + 35 + 124 + 24 procs | ALL inert: nobody can print borg parts |
| Malfunctioning AI (`antagonists/malf_ai`) | 1758 lines | inert |
| Android species | 5 procs | inert |
| Job datums AI and cyborg | 101 + 45 lines | not ported (jobs cannot be taken) |

Already live and reused: the borg radio, the AI satellite's turrets and turret controls (G4d), status displays (W1; the AI picture
waits), robot limbs, the APC, the VOX sound files (`assets/tg/sound/announcer/vox_fem`, 1290 oggs), SSpolling (X2, for
positronic brains and drones), NtOS (M9, for the borg's built-in tablet and the robotics apps), the emag (G1), the observer's
free flight (M10x, for the AI eye).

The techweb's starter nodes `robotics` and `augmentation` hold the robot suit, all borg limbs, the cybernetic organs and the pAI
card, so a techweb with only its starter nodes researched is enough for a roboticist to build a cyborg.

## 2. Packages (lead's estimates)

| Id | What | Estimate | After | Status |
|---|---|---|---|---|
| SI1 | Silicon base and laws: `silicon/*.dm`, binary (machine) language, silicon radio keys, sensor HUD toggles, station alerts, the silicons' tgui reach, the law datums and all 31 lawsets, `silicon/laws.dm`, state laws (StateLawUi) | 10 h | | merged |
| SI1c | The acid the robot remains need: the acid component and every `acid_act`, the acids, `effects/decals/remains.dm` | 4 h | SI1 | merged |
| SI2 | Cyborgs: the robot mob, the 13 models, the 43 inert borg items and 20 upgrades, three module slots as hands, the cyborg HUD (modules tray, lamp, alerts, tablet), defense (flash, EMP, emag, wires, cover, cell), recharge stations, lockdown and detonation from the robotics console, the built-in tablet with NtosRobotact, NtosCyborgRemoteMonitor, BorgHypo and BorgShaker windows, the cyborg job and spawns, the name preference | 40 h | SI1 | merged |
| SI3 | Building: the brain processor (MMI) and positronic brain (ghost poll), the robot suit assembly, the exosuit fabricator with a techweb that has only its starter nodes researched (R&D itself stays inert; designs of inert items hidden, mechs' included); the intellicard, the AI restorer and NtosAiRestorer moved to SI4 (they hold an AI) | 15 h | SI2 | merged |
| SI4 | The AI: the AI mob, its core and construction, the camera net (chunks, static, `/datum/trackable`, which also fixes DV373 and the AI detector), the AI eye (section 3), clicking machines from afar and the airlock shortcuts, AiAirlock, holopads with holograms (and the crew's holocalls), VOX announcements, the AI HUD, the core/status display/portrait pickers, the voice changer, AI slippers (Meta 12), power loss and the remote power action, controlling cyborgs (RemoteRobotControl), the AI job, the AI core landmark and latejoin; the intellicard, the AI restorer and NtosAiRestorer (from SI3) | 40 h | SI1, SI2 | merged |
| SI5 | The AI laws in the round-end report (the law rack, its modules and the ion law generator moved to SI1: the silicons' law procs call the rack) | 10 h | SI4 | merged |
| SI6 | pAI: the card, the mob, software (PaiInterface), candidates (PaiSubmit, PaiCard), the holoform chassis, the pAI HUD | 12 h | SI1 | SI6a, SI6b, SI6c merged |
| SI7 | Maintenance drones: the drone mob and laws, the drone dispenser (Meta 1), the drone shell ghost role, drones as items | 8 h | SI1 | merged |
| SI8 | Malfunctioning AI: the antagonist, its modules and the doomsday device, APC hacking, shunting, the Dynamic rulesets (roundstart and midround), MalfunctionModulePicker, AntagInfoMalf | 20 h | SI4, SI5 | merged |
| SI9 | Syndicate cyborgs (assault, medical, saboteur models) and the hacked law module uplink entry; the nukies' reinforcement beacon shows them by itself once its entry's pieces are live (D316's uplink rule) | 6 h | SI2, SI5 | merged |
| SI10 | The android species | 3 h | | merged |
| SI11 | The ion storm round event (the round events base came with the syndicate campaign's N1) | 8 h | SI5 | merged |

Order: SI1, SI2, SI3, SI4, SI5, then SI6, SI7, SI9, SI8, SI11, SI10 (a package may split into stages a, b, c as the syndicate ones
did). Not in this campaign: mechs, the full techweb and experiments, exploration drones (`modules/explorer_drone` is not a
silicon), the robotic factory (not on Meta), the cyborg boot debug window.

## 3. Rules

- **The AI's view (the owner):** tg's free eye. The AI flies an incorporeal eye (the observer's flight reused) only over tiles
  the camera net sees; tiles no camera sees are drawn as static in 3D (one DEVIATIONS row for how the static is drawn).
- **Building (the owner):** the exosuit fabricator with a techweb whose starter nodes are researched; research points, R&D
  consoles and researched borg upgrades stay inert for a later research campaign.
- Cyborgs, the AI core, pAI holoforms, drones and holograms are drawn with tg's sprites as billboards, as bots and animals are
  now (D142: icon states, overlays, flicks and sounds port with each mechanic); 3D models later, by the owner's model pipeline.
- A cyborg plays in first person like a human: its three module slots take the hands' keys (tg does the same).
- **Start after the syndicate campaign (the owner).** Then as that campaign: one package at a time in its own worktree
  `.claude/worktrees/<id>` (branch `si/<id>`, `<id>` in lower case, `godot/assets/tg` and `maps/` symlinked from main), both builds 0 warnings,
  the package's filtered tests and a gameplay test on a real server, regen twice, an in-game look at the end of each package,
  then the bookkeeping commit (CHANGELOG, PORT-CHECKLIST ticks, DEVIATIONS, not-translated section, this file's result).
- Ids: a package takes the next free not-translated section and DV number at its merge.

## 4. Results

- **SI1 (2026-11-12).** The silicon base (`silicon/*.dm` but the tablet, camera and tracking procs of SI2 and SI4), the lawsets and the
  State Laws window, the law racks with their window and the law modules (the rack moved here from SI5: the silicon's law procs call it),
  tg's ion law generator, binary speech and the drones' chat, the machine language, the suicide verb, and the small helpers they call
  (not-translated section 152). The core rack in Meta's AI upload loads the round default welded in, as tg. Tests: `SiliconLawsTests` 7,
  `SiliconLawWindowsTests` 2, `LawRackGameplayTests` 1 (Meta: a freeform law slotted and screwed in through the window). In game the rack
  and its window look as tg's (the kit's Box gained tgui's backgroundColor). The robot remains wait for the acid (SI1c).
- **SI1c (2026-11-12).** tg's acid (the component, SSacid, `acid_act` at its root and every override, the four acids, the remains, the
  glowing goo and the caustic sludge; not-translated section 153). Its particles are deferred leaves as the burning component's (BYOND
  particles are not drawn: a question for the owner). Tests: `AcidTests` 6.
- **SI2 (2026-11-12).** The cyborgs: the robot mob whole but the AI shell's procs (SI4) and the syndicate models' map blips (SI9), the 13
  models, the borg items and upgrades with the parents their modules needed (the GPS with its window, marker beacons, the rag, razor,
  flyswatter, paint, picket signs, RSF, trash bag and tray, the statues with the chisel and carving block, the beauty element, the
  marionette, wall engraving), the silicons' and AI's clicks on every machine tg gives them, the airlock's AI control, the cyborg HUD, the
  recharging station, the robotics console, the cyborg job and name preference, the MMI and positronic brain, the robot suit, and the
  cyborg branches other files kept waiting (energy guns, batons, the pseudocircuit, hats, radios, the decal painter, the holosign
  overload, MobBump); `iscyborg` was a stub that always said no. Windows: AiAirlock, BorgHypo, BorgShaker, CyborgBootDebug, Gps,
  NtosGasAnalyzer, NtosRobotact, RoboticsControlConsole. A dev verb `robotize WHO` (tg's admin Make Cyborg). Left (not-translated
  section 154): the AI shell (SI4), the syndicate cyborgs' items (SI9), the instruments' song, wiremod, concrete floors. Tests:
  `CyborgTests` 9, `CyborgSoakTests` 1, `CyborgGameplayTests` 1 (Meta: Bob locks Alice the cyborg down and releases her from the robotics
  console's window; a recharging station charges her from the grid: early in a round the APCs topping up their cells take the grid's
  surplus first, as tg's SSmachines orders them). In game: the cyborg HUD (the three module slots, tablet, camera, alerts, radio, intent,
  health and zone), the laws window on login, the cyborg sprites with their names, the statues and the recharging station.
- **SI3 (2026-11-12).** Research and the exosuit fabricator: SSresearch, the techweb and its nodes and types (the station's science
  techweb with tg's 16 starter nodes researched, the autounlocking techwebs), the scientific partners, every design live (seven design
  procs and `GLOB.mod_themes`), the cybernetic organs' EMPs and the best heart's stabiliser, the exosuit fabricator whole with the
  ExosuitFabricator window (a Fabrication DesignBrowser and MaterialCostSequence on the client). DV384 hides the inert items' (the
  MODsuits') and the mechs' designs; DV385 lets the silo take an ID with no bank account until the economy (D294's rule; no one but the QM
  could print otherwise). SI3b: what waited for research uses it: the manufacturing lathe whole, the smelter's and the ORM's alloys, the
  biogenerator's own techweb (DV306 retired), the operating computer's and the medbot's techweb links, the war bonus, the research scan.
  The client's design sheet read a world-bound helper and threw (the biogenerator's window too); fixed. Left (not-translated section 155):
  points, the R&D consoles, servers, printers and experiments (a later research campaign), the science bounty (M10), the intellicard and
  the AI restorer (SI4). Tests: `ResearchTests` 7, `ExosuitFabricatorGameplayTests` 1 (Meta: Bob opens the fabricator's window, finds the
  endoskeleton and no mech or MODsuit part, and once the silo has iron prints it with Build Now). In game: the window's categories,
  search, Build Now and the queue's progress over 50 iron sheets put into the silo with the fabricator.
- **SI4 (2026-11-12).** The AI: the AI mob whole but the malf AI's antagonist (SI8) and the portrait picker (SI4d), its core and
  construction, the latejoin core and the AI job, the camera net (SScameras' chunks, static and visibility updates, `/datum/trackable`:
  DV373 retired, SyndEye and OverSeer track), the AI eye and its multicamera windows, the AI HUD, the AI's clicks and the machines'
  `attack_ai`, holopads and holocalls with the records and holodisks, VOX announcements (a play's wait: the sound head's top bit), the
  intelliCard, the integrity restorer and NtosAiRestorer, the AI slipper, the camera presets and the camera failure event, the core,
  status display and hologram preferences and pickers, robot control. Leaves that waited for the AI moved with it (the AI detector's scan
  and HUD, the crew monitor's tracking, the photocopier's AI photo, the holo pointer, the AI shell, the AI job's spawn). Two pieces of
  tg's engine it needed: the verb registry (SSverbs, the verb metadata, a mob's verbs; the client's Commands window lists a mob's
  command verbs and runs them through `invoke_verb`) and `icon_exists` (a committed icon state table generated from tg's icons, D104).
  Protocol 78: the camera static (DV386), popups and picture-in-picture views (DV387), the command verbs and their actions; the server
  measures a click's range from the client's eye, as BYOND does. Left (not-translated section 156): the HUD's language button, the
  mech domination, the tgui panel's verb entries, the malf AI (SI8), the portrait picker (SI4d). Tests: `AiTests` 7, `ProtocolV78Tests`
  5, `AiGameplayTests` 1 (Meta: Alice made the AI gets her verbs and the static masks of her eye's chunks, runs Toggle Floor Bolts,
  right-clicks a holopad from her eye and her hologram stands on it, opens a camera window in multicamera mode). In game: the Commands
  window, the static over space, the multicamera window with tg's buttons, the hologram on a pad. Meta's two air tests that fail
  (`MetaAirTests` vent, `M4bScenarioTests` breach) fail on the SI3 base too.
- **SI4d (2026-11-12).** The paintings: `modules/art/paintings.dm` whole but the patronage's payment (the bank accounts, M10): the easel,
  the six canvases and their Canvas window (a picture of the painting's pixels; the pencil, bucket and eyedropper tools the server allows,
  the colour picker, the implement's palette, zoom, finalize with tg's title and signature prompts, the plaque), the painting frames with
  the library's exhibit frames, the paint palette; SSpersistent_paintings (the archive in `data/paintings.json` and its PNGs, loaded at
  init into the exhibit frames, saved at the round's end); tg's sprite editor workspace and flood fill; the palette component (the
  palette's and the spray can's colour wheel); the AI portrait picker and the Art Galaxy program (PortraitPicker, NtosPortraitPrinter).
  A painting's picture is tg's PNG (rust-g's `dmi_create_png` through the file seam, with the server's own PNG codec); the world takes it
  into its resource cache under its MD5 and sends it to each client once (DV388, protocol 79), which reads it as an icon. Left
  (not-translated section 157): the patronage's payment, the framers' moods, NanoPaint's image files. Tests: `PaintingTests` 6,
  `PaintingGameplayTests` 1 (Meta: Alice paints with a crayon in the Canvas window, finalizes and signs it, her client gets its PNG, and
  the framed painting is archived).
- **SI6a (2026-11-12).** The pAI: `modules/pai/` whole but the admin verb (the card and its PaiCard window, the mob with its laws,
  leash, radio and messenger, the software and the PaiInterface window, the screen images, the holoform shell with tg's chassis, the
  door jack cable, which the airlock now takes, the pAI camera, the candidates and their personality saved as JSON, DV389), SSpai and the
  PaiSubmit window, the pAI HUD with its buttons and the living health doll, and tg's ghost menu (`ghost_menu.dm` whole: the HUD
  toggles, darkness, notifications, the manifest, DNR, boo, possess, Sign up as pAI; the extra view distance left out, DV390) with the
  ghost HUD's DNR and settings buttons; the dev command `paiize`. Found in passing: the capsule's footstep halt signal was sent without
  SEND_SIGNAL's check (a mob without footsteps, the holoform, raised a runtime). Left (not-translated section 158): the Newscaster and
  Music Synthesizer software (SI6b, SI6c), the admin verb. Tests: `PaiTests` 7, `PaiGameplayTests` 1 (Meta: Gina ghosts, opens the
  ghost menu from her HUD, signs up, Bob downloads her from his card's window, she buys the door jack and unfolds). In the real client:
  the pAI's HUD, its software window from the HUD, the holoform seen by another player, the ghost HUD's new buttons, the ghost menu and
  the candidacy form.
- **SI6c (2026-11-12).** The instruments: `modules/instruments/` whole (the song datum and its InstrumentEditor window, synthesized
  playback that pitches tg's samples on reserved channels and decays them, legacy playback of tg's note files, SSinstruments and the
  instrument data, the instrument items, the space piano and minimoog, the synthesizer and headphones, the harmonica, the spectral
  instruments with the spooky element), good music; BYOND's `fexists` sees tg's sound files through the host's sound index. The legacy
  MIDI instruments stay silent (DV391, an end question). Tests: `InstrumentTests` 7, `InstrumentGameplayTests` 1.
- **SI6b (2026-11-13).** The newscaster (after gaps P4a-P4d gave it the bank accounts): `machinery/newscaster/*.dm` whole (the news
  network with its channels, stories, comments, censorship by time and the wanted issue; the newscaster with its window, alerts,
  screen overlays, light, paper, the printed newspaper, welding repair, wrench and wall frame; the pAI's newscaster; the newspaper's
  pages, scribble, peek holes and the reader hidden behind it; the bounty board's station requests paid between bank accounts), the
  Newscaster and Bounty Board apps, tg's markdown (`parsemarkdown`); the announcements and the station earnings report go on the
  news. The windows PhysicalNewscaster, NtosNewscaster, Newspaper, BountyBoard, NtosBountyBoard. Not-translated section 171 (the
  photos' pictures, D104; the cross-sector servers and admin notices; the read paper's field of view; the round's newscaster log;
  the other writers of the news). Tests: `NewscasterTests` 7, `NewscasterWindowsTests` 4, `NewscasterGameplayTests` 1 (Meta: a
  crewman reads the announcements at a newscaster and posts a bounty).
- **SI7 (2026-11-13).** Maintenance drones: `basic/drone/` whole, the ghost role spawners (`mob_spawn.dm`), the drone shell dispenser,
  the silicon camera, the drone holder, holderloving and stationstuck, and the ghosts' spawners menu with its HUD button and window
  (not-translated section 160). On the way: the client's picture cache is dropped when a Snapshot restarts the private tables (a new
  body's HUD slots drew blank). Tests: `DroneTests` 6, `DroneGameplayTests` 1.
- **SI9 (2026-11-13).** Syndicate cyborgs: the syndicate cyborg procs, the models' rebuilds, the cyborg beacons, the chameleon
  projector, the cyborg pinpointer and LMG; the hacked module entry was live since SI1 (not-translated section 161). Tests:
  `SyndicateCyborgTests` 4.
- **SI11 (2026-11-13).** The ion storm round event (`ion_storm.dm` whole: `generate_ion_law` came with SI1; the event's announcement
  and its start, which scrambles every law rack and may emag the bots). Tests: `IonStormTests` 2.
- **SI8 (2026-11-13).** The malfunctioning AI: `malf_ai/` whole (the antagonist and the infected AI, the module picker, every
  module and its action, the doomsday device and its countdown, the malf cinematic, the voice changer), the Robotic Factory
  (`transformer.dm`), the AI upgrade modules (`ai_upgrades.dm`, `remote_power.dm`), the robot army and malf survival objectives, the
  roundstart and midround rulesets, the hacked APC HUD; the MalfunctionModulePicker (tg's GenericUplink, shared with the Uplink
  window), AntagInfoMalf and AiVoiceChanger windows and tgui-core's malfunction theme (approximated colours). On the way: the types
  converter's TimerIdVars table types the vars that hold a stoppable timer's text id after a number default (the malf AI's APC
  hack crashed; so did the lathe's and the butterfly's timers); strip_html, ohearers, GLOB.ai_employers and
  GLOB.all_radial_directions. Mech domination sets its flag only (the mechs are inert). Tests: `MalfAiTests` 7, `MalfGameplayTests` 1.
- **SI10 (2026-11-13).** The android species (`android.dm` whole), the cybernetic brain (`brain_cybernetic.dm`) and the robotic
  voicebox; tg's synthetic language holder written by hand (the prototype list skips generated-only holders); `get_replaceability`
  takes typepaths, so a species change swaps the organs as tg's `regenerate_organs` does (the generated slot type had made it a no-op).
  Tests: `AndroidTests` 2.
- **SI5 (2026-11-12).** The round-end report's laws (`law_report`): each AI's laws at the round's end or its deactivation with its law
  changes and its cyborgs, and the AI-less cyborgs' fates and laws. The rest of SI5's plan (the law rack, its modules and ion laws) came
  with SI1.
