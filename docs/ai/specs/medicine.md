# M8 Medicine: design and plan

Written 2026-10-22 against main (protocol 33; M7x on its branches takes protocol 34). tg = `../tgstation/code` (snapshot 1d99bd7f).
Paths without a prefix are in the repo, `Tg/` = `src/Core/Tg/`. Run autonomously under D178: every choice is a "my choice" row (section 6),
numbered D181-D195 in DECISIONS.md (DVxxx are numbered as the slices write them). Format follows `docs/ai/specs/weapons.md`.

## 0. What already exists (verified)

Damage, stat, crit, death (M2):
- Damage procs, `updatehealth`, `update_stat` (`Tg/modules/mob/living/carbon/carbon.cs:522`, HARD_CRIT at :534), `death` (`living/death.cs:237`,
  `carbon/death.cs:19`, `human/death.cs:56`), `revive`/`heal_and_revive` (`living.cs:1097`, `:1169`; carbon override `carbon.cs:568`, `:590`),
  `fully_heal`. Crit fullscreens (`carbon.hud.cs:67-75`), the health doll and health indicator as tg screen objects (D107; private appearances,
  `src/Protocol/MessagesV2.cs:182-242`: no health number leaves the server). Dev verbs: damage/heal/fullheal/kill/revive/bleed/blood/wound/
  dismember/analyze/effect/outfit/mobinfo (`src/Server/DevCommandLine.cs:233-275`).
- tg tests already ported: `mob_damage` (PARTIAL, `tests/Anomaly.Core.Tests/Tg/UnitTests/mob_damage.cs:13`), `stuns`, `oxyloss_suffocation`,
  `full_heal`, `status_effect_validity` (`Tg/M2Medical/T1/PortedTgTests.cs:1`), `blood_volume_procs`, `medical_wounds`, `dismemberment`,
  `limbsanity`, `organ_bodypart_shuffle` (`tests/Anomaly.Core.Tests/Tg/UnitTests/`). Integration: `HealthDollTests`, `DeathPostureE2ETests`,
  `WoundGameplayTests`, `GauzeGameplayTests`, `BloodGameplayTests`, `HuskGameplayTests`, `M2ScenarioTests`, `M6ScenarioTests`.
Breathing, heart, CPR:
- Human breathing through the lungs is whole (`Tg/modules/surgery/organs/internal/lungs/_lungs.cs:939` `check_breath`, 1360 lines; the gas
  reagents are NotTranslated rows :459-:930 "reagents, M8"); `human/life.cs:115` `breathe`, `:125` `check_breath`. The non-human carbon
  `check_breath`/`handle_suffocation` are NotTranslated (`carbon/life.cs:224`, `:231`; monkeys only, stays out).
- Heart: `_heart.cs` (258) `Stop`/`Restart`/`on_life` (:119, :135, :180); `hear_beat_noise` NotTranslated (:164). Cardiac arrest whole:
  `can_heartattack`/`undergoing_cardiac_arrest`/`set_heartattack` (`carbon/life.cs:533-565`), its damage `human/life.cs:397 handle_heart`.
- CPR is translated and reachable: `human.cs:206 do_cpr` (2 balloon alerts Deferred, M9), called from the help hand `_species.cs:878`. No test.
- Stamina and stamcrit exist (`datums/status_effects/debuffs/stamcrit.cs`); tg's stamina screen object is a row (`_onclick/hud/human.dm:33`).
Wounds and bleeding (M6): `datums/wounds/` whole (4.4k lines, 2 NotTranslated: `bones.cs` head traumas, `slash.cs:355` las_cauterize which
  M7x takes); treatment hooks translated: `check_grab_treatments`, `treat`, `try_handling`, `on_xadone`, `on_synthflesh` (`slash.cs:228-339`,
  `pierce.cs:167-194`), gauze seeping. Embedding (M6). `second_wind`'s determination reagent is Deferred (not-translated.md:940).
Medical items:
- `Tg/game/objects/items/stacks/medical.cs` (763): the shared heal loop (tg medical.dm:61-372), wrap and gauze (:395-665). The other stacks
  (bruise pack, suture, ointment, mesh, aloe, bone gel, poultice, bandage) are var blocks only (header :12-16).
- Health analyzer: `obj_item_healthanalyzer` is an empty class (`devices/scanners/health_analyzer.cs:22`); `healthscan` whole except the
  ethereal line (:328) and two rows (organ table tg :286, diseases tg :442, not-translated.md:223-224); `chemscan`, `woundscan`, `diseasescan`
  NotTranslated (:359, :366, :372). Reached only by the dev verb `analyze` (`Tg/Infrastructure/DevCommands.cs:246-259`).
- Medkits: `obj_item_storage_medkit*` are generated var blocks, no `PopulateContents` (Meta's 9 regular, 3 each of fire/brute/toxin/o2 spawn
  empty). Boxes: `storage/boxes/medical_boxes.cs` (bandages, beakers, bodybags, pillbottles, syringes). Lockers `closets/secure/medical.cs`:
  chemical, CMO, medical2, psychology only; `medical1` (x2 on Meta) and `paramedic` are empty (tg medical.dm:7, :147).
- Defibrillator: nothing (`carbon.cs:688-711` `can_defib*` NotTranslated "M8 defibrillation"); twohanded component exists (`datums/components/
  twohanded.cs`, M5); power cells exist (M7).
Reagents:
- Holder PARTIAL (`modules/reagents/chemistry/holder/holder.cs:12-20`): New/Destroy, add/remove (mob branches NotTranslated :647 purity, :718
  on_mob_add), trans_to, expose, del_reagent, clear, update_total (:364 end of metabolization NotTranslated). `metabolize` is a Deferred stub
  (`holder/mob_life.cs:30-33`), `end_metabolization`/stasis NotTranslated (:44, :51). Reagent base: New, expose_*, transfer hooks
  (`chemistry/reagents.cs`, 155 of tg 362). Reagents: water, fuel, foam and a few (`reagents/other_reagents.cs`, `pyrotechnic_reagents.cs`);
  `medicine_reagents.cs` is a SKELETON (salbutamol type only). Liver `handle_chemical` NotTranslated (`_liver.cs:103`, failure :163), stomach
  digestion NotTranslated (`_stomach.cs:126`, :155, :259-:322), `get_fullness`/`has_reagent` of living (`living/life.cs:153`, :169).
- Containers: `obj_item_reagent_containers*` are generated var blocks only (no procs anywhere). SSreagents class exists (`controllers/subsystem/
  processing/reagents.cs`).
HUDs: `obj_item_clothing_glasses_hud` PARTIAL (`modules/clothing/glasses/hud.cs`, 26 lines, only `equipped`); `/datum/atom_hud` SKELETON
  (`datums/hud.cs`, 117). The replicator already sends a client's own `client.images` with the atom they hang on (`src/Server/Replication/
  Replicator.cs:1605-1640`, D40: only to that session); the client draws them only as progress bars (`godot/scripts/Hud/ChatLines.cs`).
Implants and skillchips (D180): `datums/outfit.cs:363-373` NotTranslated for an outfit's implants and skillchips, so in strict worlds
  (`ServerHost.cs:288` NotTranslatedThrows) tg's security officer, warden, HoS, detective, captain (mindshield) and engineer, CE, doctor, CMO,
  janitor, cook, miner, roboticist, RD, psychologist, clown, bartender, coroner (skillchips) outfits throw. tg: implant.dm 226, implant_mindshield
  52, implanter 55, implantcase 76; skillchip.dm 386, carbon/skillchip.dm 80, brain/skillchip.dm 174, job chips 95. Meta: 1 implanter,
  box/chemimp, box/trackimp.
Hallucinations (D166): the crystal's pulse is Deferred (`modules/power/supermatter/supermatter_extra_effects.cs:121-123`), also the
  hallucination anomaly's pulses and `effect_demoralize` (not-translated.md:1327, :1342, :1387). tg: modules/hallucination 3798 (27 types),
  `__HELPERS/hallucinations.dm` 303, status effect 176.
Rows tagged M8: not-translated.md 136 lines, live-partial.txt 31. Groups: data HUDs (~35 rows: med_hud_set_*, update_ID_card, readjust_atom_huds,
  login reload_huds), reagents/metabolism (~25), blood transfer (`blood.dm:370-824`, 5), digestion (~10), debuffs (dizzy, drowsy, jitter, drunk:
  ~8), genetics/mutations (~8), diseases (~8), quirks (8, status_procs.cs:642-715), cleaning/decals with reagents (~6), plumbing (4), crafting
  with reagents (5), skillchips (2), implants (1), penlight (2), luminescent fluid (7), the 13 disposals rows (live-partial.txt:148-170: a mis-tag).
Client and first person: other mobs are clicked on their drawn body part (DV30 aim zone; `GauzeGameplayTests` clicks Bob's left arm). The own
  body is never under the pointer; a drop or a ctrl-click on the health doll means the own mob (`godot/scripts/Shared/Play/DragModel.cs:128`,
  DV72 at :137). Chat keeps 6 lines of 240 chars (`Shared/Play/FeedbackModels.cs:19-25`): a health scan does not fit. Assets: tg sounds
  (`godot/assets/tg/sound/machines/defib/*`, `items/hypospray.ogg`) and icons (`icons/obj/medical/*`) are converted.
Meta medical spawns (`_maps/.../MetaStation.dmm`): medkits 9 regular + 3 fire/brute/toxin/o2 + 2 plain, defib/loaded 4, defib mounts 3,
  health analyzers 7, HUD glasses 7, stethoscopes 4, syringes 10 + 6 boxes, droppers 18, beakers 24, bottles epinephrine 8 / multiver 8 /
  morphine 6 / toxin 3, blood packs 13 + 2 blood freezers, IV drips 10, sutures 4, meshes 4, gauze 3, ointment 2, bruise pack/bone gel/bandage 1,
  pill bottles (epinephrine, mannitol, ...), medipen/pumpup 2, syringe/epinephrine 2, pepper spray 2, cleaner spray 7; vending/medical 1 (no
  vending machine is ported). Outfits: paramedic (EMT belt: gauze/twelve, tourniquet, bonesetter, syringe, 2 bottles; box/bandages; penlight),
  doctor (medkit/surgery, penlight, entrails_reader chip), CMO (chip, advanced analyzer).

## 1. Scope boundary (decided, D-row 1)

IN, tg sizes:
- Reagent holder rest + metabolism (holder.dm 820 rest ~400, mob_life.dm 207, reagents.dm rest ~200, properties ~60), liver metabolism and
  failure (_liver.dm ~250 of 404), stomach digestion (~100), living/carbon reagent hooks (~80); addictions recorded but not kept (D-row 9).
- Core medicines (D-row 2), ~1.5k: medicine_reagents.dm epinephrine, salbutamol, oxandrolone, sal_acid, pen_acid, potass_iodide, morphine,
  atropine, coagulant, salglu_solution, spaceacillin, mannitol, leporazine, omnizine (bottles/medipen survival paths reach it), inacusiate, oculine;
  cat2: libital, aiuri, probital, lenturi, hercuri, convermol, tirimol, seiver, multiver, syriniver, synthflesh, penthrite (+ c2 base);
  toxin.dm: toxin, formaldehyde; other: iron, determination, blood; drug: pumpup; food: capsaicin, condensedcapsaicin (pepper spray).
  Status effects they need: drowsiness 42, dizziness 78, heart_attack 191, the drugginess/jitter already there.
  Why these: exactly what Meta's medkits, medipens, pill bottles, syringes, bottles, lockers and outfits contain, plus epinephrine/salbutamol
  named by ROADMAP; nothing a chem dispenser would be needed for.
- Delivery (~3.5k): reagent_containers.dm 430, syringes 456 (base and the station subtypes), applicator 49, pill 491 (base + station pills),
  patch 262, hypospray 411 (base + medipens), spray 462 (base, hercuri, pepper, cleaner as an inert reagent), cups/_cup.dm (~400: pour,
  draw, drink, splash) and bottle/beaker procs, dropper 93, pillbottles.dm, medkit.dm (PopulateContents, storage limits), the two empty lockers,
  EMT belt, blood_pack 91 + iv_drip 461 + blood transfer rows (D-row 12), the syringe gun and its dart (~180, Meta spawns 2; M7x left it to M8).
- Healing items (~0.9k): the rest of medical.dm (bruise pack, suture/medicated, ointment, mesh/advanced, aloe, bone gel, poultice, bandage),
  tourniquet 146, bonesetter/hemostat use as wound tools (bones.dm/slash.dm treat already there).
- Analyzer (~0.5k): the item procs (scan, mode, chem/wound scans, simple, advanced, simple/disease reaching only the non-disease part), the
  organ table row; penlight eye/mouth examine (flashlight.dm:125-210); stethoscope (_neck.dm:250-375). Exact values only to its user (D40).
- Defibrillator, death, revival (~1.2k): defib.dm 746, defibrillator_mount.dm 273, `can_defib*`, heart beat noise, revival through the ghost.
- Data HUDs (~1.0k): atom_hud (hud.dm 482), data_huds.dm medical and security subsets, HUD glasses toggles, every med_hud row; the stamina
  screen object. Security HUD's job and mindshield icons (wanted status waits for records, M9). D-row 5.
- Implants and skillchips (~1.1k): unblocks every station outfit in strict worlds (D180). D-row 6.
- Hallucinations (~4.3k): D166 put them here; last slice. D-row 7.
OUT (ROADMAP plus mine, D-row 8): surgery and operating tables, chem dispenser/master/heater and reactions (the lookup stays empty), cryo, sleepers,
  stasis beds, medical vendors (no vending machine is ported: a vending milestone), diseases and the disease analyzer lines, genetics/mutations,
  quirks, addictions (points not kept), impure/inverse reagents (purity stays 1 without chemistry), food/hunger HUD and nutrition, body bags,
  medibots, crew monitor/suit sensors console, organ transplants, the non-human carbon breathing. Total IN: ~15.6k tg lines + ~1.4k ours
  (client, dev verbs, tests helpers). ROADMAP's estimate was ~15k.

## 2. Slices (dependency order)

Agent fit (2 at a time: one Opus + one Sonnet, no helpers). A, D, E take judgement in core paths or the client seam -> Opus. B, C, F, G are
mechanical against tg -> Sonnet. Order: A (Opus) -> B (Sonnet) beside D (Opus) -> C (Sonnet) beside E (Opus) -> F (Sonnet) -> G (Sonnet) ->
closing (main session). Every slice: tg visuals and sounds with the mechanic (D142), CHANGELOG line, not-translated/live-partial rows closed
or retagged, filtered tests; the full Core and Server suites only at the close.

### Slice A. Reagent holder and metabolism - Opus, tg ~1.3k
- tg: `holder/holder.dm` rest (add_reagent mob branch :113-:179 `process_mob_reagent_purity` (purity 1: no split), `on_mob_add`; `remove_reagent`,
  `remove_all`, `trans_to` with methods, `copy_to`, `get_master_reagent`, `multiply`, `convert_reagent`, `add_reagent_list`, `update_total` :602
  end of metabolization, `isolate_reagent`, `has_reagent`, `get_reagent_log_string`), `holder/mob_life.dm` 207 (`metabolize`, `metabolize_reagent`,
  `end_metabolization`, overdose start/process, `handle_stasis_chems`), `holder/properties.dm` rest, `reagents.dm` (`on_mob_life`,
  `on_mob_metabolize`, `on_mob_end_metabolize`, `on_mob_add`, `on_mob_delete`, `overdose_*`, `metabolization_rate`, `expose_mob` with
  INGEST/INJECT/PATCH/TOUCH/VAPOR and touch protection, `get_taste_description`), `_liver.dm:54-200` (handle_chemical, comedy metabolism,
  examine, failure stages), `_stomach.dm:73-111` and `:201-336` digestion (not hunger), `living/life.dm:102,:118`, `carbon/life.dm:32`
  addictions -> Deferred row, `living_defense.dm:782` taste, `cleanable.dm:94` init_reagents, `blood_types.dm:27`.
- Ours: `modules/reagents/chemistry/holder/*.cs`, `chemistry/reagents.cs`, `_liver.cs`, `_stomach.cs`, `living/life.cs`; dev verb `reagent WHO
  TYPE N` (add to the blood, tg's admin "Add reagent") and `reagents WHO` (list) in `DevCommandLine.cs`/`ServerHost.Dev.cs` (D-row 15).
- tg tests: `metabolizing.dm` (over the core set, not all 959), `stacked_metab.dm`, `reagent_transfer.dm`, `reagent_mod_procs.dm`,
  `reagent_names.dm` (core set). `liver.dm` tests skeleton and plasmaman livers only: not adapted (species out), noted.
- Core: water drunk metabolizes at tg's rate; an overdose starts and ends; a liverless body takes tg's liverless path; the stomach moves an
  ingested reagent into the blood at tg's rate; determinism (two runs, same hash).
- Visuals/sounds: none (A has no item); the analyzer's chemscan arrives in D.

### Slice B. Core medicines - Sonnet, tg ~1.7k (after A)
- tg: the reagents listed in section 1 (their `on_mob_life`, `on_mob_metabolize`, `overdose_process`, `expose_mob`, wound hooks `on_xadone`/
  `on_synthflesh`/coagulant clotting), `datums/status_effects/debuffs/{drowsiness,dizziness,heart_attack}.dm`, the M8 rows of dizzy/drowsy
  (`_species.dm:957/:965`, `blood.dm:227/:238`, `mild.dm:107`), `_wounds.dm:481` determination, the lungs' gas reagents that are reagents of the set
  (none: pluoxium, bz, freon, halon, healium, hypernoblium, nitrium, zauker stay rows for later gases, retag M10+).
- Ours: `modules/reagents/chemistry/reagents/{medicine_reagents,cat2_medicine_reagents,toxin_reagents,other_reagents,food_reagents,
  drug_reagents}.cs`, the three status effects.
- tg tests: `combat_stamina.dm` if M7x did not port it (pumpup/epinephrine stamina), `strange_reagent.dm` not adapted (reagent out).
- Core: per medicine one test of tg's numbers (libital 2 brute/s heals + liver damage; epinephrine below 0 health stops oxyloss and losebreath;
  salbutamol clears losebreath; coagulant lowers a slash's blood flow; penthrite keeps a stopped heart patient alive; morphine drowsiness,
  overdose), the heart attack status' stages.
- Visuals: drowsiness/dizziness screen effects through the existing fullscreen/filter path (dizziness is tg's client view wobble: the
  camera sway, as M6's concussion row asked; DVxxx if not exact).

### Slice C. Containers and delivery - Sonnet, tg ~3.5k (after B; beside E)
- tg: `reagent_containers.dm` 430 (attack, splash, transfer amount verb, `is_*` flags), `syringes.dm` (draw/inject modes, blood draw ->
  `transfer_blood_to`, the station subtypes `epinephrine`, `syriniver`, `convermol`, ...), `applicator.dm`, `pill.dm` (base + Meta/medkit pills),
  `patch.dm`, `hypospray.dm` (base + medipens), `spray.dm` (base + hercuri, pepper, cleaner), `cups/_cup.dm` + `bottle.dm` procs, `dropper.dm`,
  `storage/pillbottles.dm` (PopulateContents), `storage/medkit.dm` (Initialize storage rules + every PopulateContents), `belt.dm:192-229`
  (medical, paramedic), lockers `medical1`/`paramedic` (`generate_items_inside`), `blood_pack.dm`, `game/machinery/iv_drip.dm` (with its beam to
  the patient, DV77 quads), `blood.dm:370,:430,:433,:619,:824` (transfer, data, compatibility), `copy_data` :659 (viruses: Deferred), the
  syringe gun (`guns/syringe_gun.dm` station gun + `projectile/bullet/dart.dm`, on M7x's gun core).
- Visuals: `update_overlays` fill levels from `reagent_fillings.dmi` and `mix_color_from_reagents`, syringe draw/inject states, used medipen
  `medipen0`, pill and patch icons, IV drip states and beam; sounds `hypospray.ogg`, syringe, `drink.ogg`, spray, patch.
- tg tests: `pills.dm`, `reagent_container_defaults.dm`, `reagent_mob_expose.dm`, `syringe_gun.dm`.
- Integration `MedicineDeliveryGameplayTests` (MetaStation medbay): Alice opens a regular medkit (gauze, 2 sutures, 2 meshes, medipen, simple
  analyzer); injects Bob with the medipen (Bob's epinephrine rises, the pen shows empty, both hear the hypospray); draws epinephrine from a Meta
  bottle with a syringe (fill overlay on the held syringe in her ViewModel) and injects; feeds a pill; hangs a blood pack on an IV drip.

### Slice D. Healing items, analyzer, first aid in first person - Opus, tg ~1.2k + ours ~0.6k (beside B)
- tg: `medical.dm` the remaining stack types (`try_heal_checks`, `heal` of bruise pack :374, suture :689, ointment :724, mesh :750 incl.
  `attack_self` :807 opening, aloe :834, bone gel :861, poultice :910, bandage :938), `tourniquet.dm` 146, `health_analyzer.dm` item procs
  (:54 attack_self mode, :65 interact_with_atom with `flick("-scan")` and `healthanalyzer.ogg`, :115 secondary wound scan, :540 chemscan, :642
  woundscan, :677-760 simple analyzer with ping/buzz and `_pinprick`, `advanced`), the organ table row (:286), `flashlight.dm:125-210`
  eye/mouth examine, `clothing/neck/_neck.dm` stethoscope (pulse, heart and lungs via `hear_beat_noise`/`hear_breath_noise`, `_heart.cs:164`,
  `_lungs.cs:1271`).
- Client (ours): (1) self-application: with an item in the active hand, a click on a health doll limb (cursor mode) is a click on the own mob
  aimed at that limb's zone (D-row 3, extends DV72); the empty hand keeps tg's doll self-check. (2) Readout panel: a chat message whose HTML is
  tg's `boxed_message` (examine_block) opens whole in a scrollable panel by the chat for its recipient, closed by its close button or the next one (D-row 4; D282: it is moved and resized as every window);
  the gas analyzer's readout gains it too. Both engine-free in `godot/scripts/Shared/Play` with tests in `tests/Anomaly.Server.Tests/Client`.
- Core: each stack heals tg's amounts per limb with tg's self-delay; a suture closes a slash's blood flow; mesh/ointment treat burn wound
  sanitization/flesh healing; bone gel + surgical tape set a bone; tourniquet stops a limb's bleeding; CPR (tg `do_cpr`: oxyloss -7 per pump,
  the panic speed) restores a crit patient's breath; the stethoscope hears no beat on a stopped heart.
- tg tests: `breath.dm` and `lungs.dm` base human cases (plasmaman/ashwalker out), `medical_wounds` rerun.
- Integration `FirstAidGameplayTests`: Alice scans Bob: her chat holds the boxed scan with Bob's exact numbers; Bob's and Carol's sessions
  receive no message containing them (D40, compare to the `HealthscanTests` text); Alice sutures Bob's bleeding arm aimed by DV30; Alice
  (alone) meshes her own burned chest through the doll; the client hears the suture/mesh sounds and sees the bandage overlay.

### Slice E. Defibrillator, cardiac arrest, death and revival - Opus, tg ~1.2k (after D; beside C)
- tg: `items/tools/medical/defib.dm` 746 (cell from M7, safety, emag, EMP, paddles with twohanded wield, charge/cooldown, `do_help` revival and
  cardiac restart :589-695, `do_harm` heart attack :546, `do_disarm`, paddle range snap back, `on_defib_success`), `defibrillator_mount.dm` 273
  (wall mount, paddles from the wall, `/charging` subtype from area power), `carbon.dm:719-772` `can_defib*`, revival through the ghost
  (`grab_ghost`, DEFIB_TIME_LIMIT, brain damage on revival), `drag_pickup` element for the defib (not-translated.md:720).
- Visuals/sounds: defib overlays (charge level, paddles out, safety light, emagged), wielded paddle sprites in the ViewModel, the body's jitter
  on a shock (jitter exists), defib charge/zap/success/failed/ready/safety sounds, `egloves.ogg`, body fall.
- Dev verbs: `heart WHO stop|start` (`set_heartattack`), `breath WHO N` (losebreath) (D-row 15).
- tg tests: `unequip_defib.dm`. Core: no cell -> no shock; a dead body with a ghost within the time limit revives at tg's health with brain
  damage; past the limit or without a heart fails with tg's message; a living patient in cardiac arrest gets the heart restarted; harm mode
  with safety off stops a heart.
- Integration `DefibGameplayTests`: Bob is killed (dev), ghosts, Alice takes the loaded defib from medbay, wields the paddles, revives him;
  Bob's client returns to the body; the mount on the wall shows the paddles gone.

### Slice F. Data HUDs, implants, skillchips - Sonnet, tg ~2.4k + client ~0.3k (after A)
- tg: `datums/hud.dm` 482 (atom_hud add/remove/show_to/hide_from), `game/data_huds.dm` (medical: health bar, status; security: ID job icon,
  implant icons; the basic health HUD), every M8 data HUD row (living.dm:11,:31,:51,:832; carbon.dm:28,:443,:624; death.dm:252; init_signals;
  appendix; brain; logout; login reload_huds; outfit/inventory/job update_ID_card; living_update_icons readjust; mob.dm:1632/:1642 trait
  show/hide), `clothing/glasses/hud.dm` rest (toggle, EMP), the stamina screen object (`_onclick/hud/human.dm:33`); implants: `implant.dm`,
  `implant_mindshield.dm`, `implanter.dm`, `implantcase.dm`, `boxes/implant_boxes.dm`, `implant_misc.dm` chem and tracking (tracking inert: no
  tracker console), `outfit.dm:285`; skillchips: `skillchip.dm`, `carbon/skillchip.dm`, `brain/skillchip.dm`, `brain_item.dm:336`,
  `outfit.dm:291`, the job chips (their traits; the drunken brawler's martial art and entrails reader's text stay rows).
- Client: private images attached to a mob are drawn as small billboards at their pixel offset above the mob (HUD bars, status and job
  icons), not as progress bars; `image.override` waits for G.
- tg tests: `huds.dm`, `outfit_sanity.dm` (every station job outfit in a strict world: closes D180's gap).
- Integration `MedHudGameplayTests`: Alice puts on medical HUD glasses: her replica gets Bob's health bar image, Carol's (no glasses) gets
  none (D40); the bar drops when Bob is hurt; an officer from `outfit /datum/outfit/job/security` in a strict world has a mindshield and
  shows its icon to a security HUD.

### Slice G. Hallucinations (D166) - Sonnet, tg ~4.3k + client ~0.4k (after F)
- tg: `datums/status_effects/debuffs/hallucination.dm`, `__HELPERS/hallucinations.dm` (pulses, `get_random_valid_hallucination_subtype`),
  `modules/hallucination/*` all 27 types, `screwy_hud.dm`; closes the supermatter (`supermatter_extra_effects.cs:121`), anomaly and delamination
  rows. Types whose pieces exist use them (fake sound -> one client's sound, fake chat/message -> its chat, fake alert/health doll -> its screen,
  images -> its `client.images`); `delusions` needs image override on other mobs: the client draws an override image in place of the mob's
  own picture for that viewer only.
- Protocol 35 only if the image override flag is not already in the image appearance (check first); otherwise none.
- tg tests: `hallucination_icons.dm`. Integration `HallucinationGameplayTests`: a supermatter at high power pulses Alice (watching it), her
  client gets a fake sound or image, Bob's gets nothing.

### Closing (main session)
- `M8ScenarioTests` (the done-when, MetaStation medbay, seed fixed, run twice: same hash): Bob is given by dev commands a severe slash on the
  left arm (bleeding), a moderate burn wound + 60 burn on the chest, losebreath 20 + 60 oxyloss (suffocation), and a stopped heart (`heart bob
  stop`) -> hard crit. Alice (paramedic outfit, alone) uses only Meta items: analyzer scan; defib paddles restart the heart; CPR; epinephrine
  medipen; gauze then suture on the arm; mesh on the chest; salbutamol medipen / convermol syringe. Assert: stat CONSCIOUS (out of crit), every
  wound's blood_flow 0, losebreath 0 and oxyloss not rising for 1200 ticks (60 s); Alice's chat had exact values, Bob's and Carol's none.
- Separate `GunshotTreatmentGameplayTests` (D177/M7x, section 3). M2-M6 medical tests rerun; full Core and Server suites; a smoke render of
  the analyzer panel, the HUD bar, a filled syringe and the defib; CHANGELOG; release 0.8.0 (protocol bump if G needed it).

## 3. Interactions

- D177 (shots hit the real part) and M7x: a bullet or laser damages and wounds the limb it crossed, so treatment is per limb: the analyzer's
  limb table and woundscan name it; the medic aims the item at that limb (DV30 on others, the doll on oneself, D-row 3); `auto_change_zone` of
  the heal loop moves to the next hurt limb as tg. Bullets: pierce wounds (suture, gauze), embedded slugs/pellets (M6 embedding; hemostat or
  hand removal), bone fractures (bone gel, surgical tape, bonesetter). Lasers: burn wounds (ointment, mesh, aiuri/hercuri; infection ->
  spaceacillin / sanitization). A laser cauterizes a slash (`las_cauterize`, M7x). Disablers: stamina (pumpup/epinephrine). Pepper spray and
  the syringe gun join here (D-row 10). `GunshotTreatmentGameplayTests`: Carol shoots Bob's arm with the .38 and his chest with a laser; Alice
  finds both with the analyzer and treats them until no bleeding.
- First-person client: on others, the click lands on the drawn body part (DV30 zone), progress bars are labelled (D159), tg's heal messages
  go to chat; on oneself, the doll limb click (D-row 3) or `attack_self` (Z) for medipens/pills/patches and the mesh as tg. Held items show
  their state in the ViewModel (D126: fill level, used pen, defib paddles). The analyzer's output is the readout panel (D-row 4); the HUD
  glasses' bars float over heads (F). The patient sees his own state only through tg's doll, indicator, fullscreens and alerts (no numbers).

## 4. Risks

- R1 Size: ~15.6k tg + ~1.4k ours, the largest milestone so far; G (4.3k) is self-contained and last, so a schedule slip shows there first;
  it is not cut silently (D29): report the remaining slices at the playtest if it slips.
- R2 Strict worlds: new paths reach NotTranslated leaves (moods M9, quirks, addictions, diseases). Every one on a slice's path becomes
  `Dm.Deferred` with a not-translated row or is translated; `outfit_sanity` in a strict world is the gate.
- R3 Determinism and cost: metabolism adds a per-reagent loop to every carbon's Life; measure Meta at rest with 20 mobs holding reagents
  (target: under 0.2 ms per SSmobs fire) and hash two runs.
- R4 Non-core reagents in mapped containers (coffee, sodas, condiments, cryoxadone beakers) metabolize with the base reagent's behaviour only
  (no effect): one not-translated row lists them (D-row 8); the cryoxadone beakers stay inert without cryo.
- R5 D40: the scan panel must stay a private chat message; a test compares every other session's feed. HUD images go only to holders of the
  HUD trait (client.images is per session).
- R6 Revival needs the player's ghost (D151 minimal ghosts); bots in tests must ghost and return; a disconnected player cannot be revived
  (tg `can_defib_client` text).
- R7 Self-application via the doll needs cursor mode (Tab); if the playtest finds it slow, a "use on self" key is the fallback (later).
- R8 Hallucination images that replace other mobs' pictures (delusions) need client work beyond F's billboards; if the image override is
  not in the protocol, protocol 35.
- R9 The 13 disposals rows tagged M8 in live-partial.txt are a mis-tag (D-row 11); retag, do not translate.

## 5. Size and order summary

| Slice | Agent | tg lines | ours | after | beside |
|---|---|---|---|---|---|
| A holder + metabolism | Opus | 1.3k | 0.1k | - | - |
| B core medicines | Sonnet | 1.7k | - | A | D |
| C containers + delivery | Sonnet | 3.5k | 0.1k | B | E |
| D healing items + analyzer + 1st person | Opus | 1.2k | 0.6k | - (chemscan after A) | B |
| E defib + revival | Opus | 1.2k | 0.1k | D | C |
| F data HUDs + implants + skillchips | Sonnet | 2.4k | 0.3k | A | - |
| G hallucinations | Sonnet | 4.3k | 0.4k | F | - |
| Closing | main | - | 0.2k | all | - |

## 6. Decided

The choices are DECISIONS.md rows D181-D195 (my choices under D178).
