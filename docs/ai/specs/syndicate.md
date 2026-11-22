# The syndicate campaign: traitors, the syndicate and Dynamic (plan, 2026-11-03, D316)

The owner (2026-11-03): port everything of the traitors and the syndicate, and their mode. tg's snapshot has no traitor mode of
its own: the round's mode is Dynamic (`controllers/subsystem/dynamic`), and the traitor, the blood brothers and the nuclear
operatives are its rulesets. So the mode is Dynamic, with the rulesets of these antagonists live; the rulesets of the antagonists
not yet ported stay inert and are never picked until their antagonists come.

The lead's survey (2026-11-03): the antagonist datums 0/105 tg procs, objectives 5/170, Dynamic 0/228, the uplink 0/85, the
emag's per-object reactions 33/126 (77 missing, 16 stubbed); traitors can buy 172 uplink entries, of which 124 name an inert
item; the nuclear and clown operatives add about 140 entries, nearly all inert. From the estimates the owner chose every package
below (about 309 h) and left out the MODsuits (60 h) and, for later, the syndicate cyborgs (silicons) and mechs.

## 1. Packages (the times are the lead's estimates)

| Id | What | Model | Estimate | After | Status |
|---|---|---|---|---|---|
| A1 | The antagonist core: `/datum/antagonist` and `/datum/team` (`modules/antagonists/_common`), the mind's antagonist procs (`datums/mind/antag.dm`), the objectives and the steal targets (`game/gamemodes/objective.dm`, `objective_items.dm`), the antagonist HUD icons, the round-end antagonist report (not-translated 82), the AntagInfoGeneric and AntagInfoTraitor windows' common part and the antag info button, the admin tools (Check Antagonists, the traitor panel, making a mind an antagonist) | Opus | 30 h | | merged |
| A2 | The traitor and the uplink: `datum_traitor.dm` with employers and objectives, SStraitor's code words (highlighted in chat), the uplink component (PDA ringtone, headset frequency, pen twist, implant), the uplink handler, telecrystals, discounts and sales, the purchase log and refunds, every uplink entry of `modules/uplink`, the Uplink and AntagInfoTraitor windows | Opus | 18 h | A1 | merged |
| A3 | Dynamic: SSdynamic, the tiers and threat, its config, the ruleset base and the roundstart, latejoin and midround traitor rulesets, the extended and meteor rulesets as far as their pieces exist; the lobby's Antagonists preferences tab (revises D281's "not now" for that tab); the DynamicAdmin and DynamicTester windows | Opus | 25 h | A1 | merged |
| X4 | The blood brothers: the team, conversion by flash, the shared objectives, the ruleset, AntagInfoBrother | Opus | 6 h | A1, A3 | merged |
| G1 | Stealth gear: the emag and doorjack with every object's `emag_act`, the agent ID and the ChameleonCard window, the chameleon kit and projector, thermal glasses, no-slip shoes, AI detector, EMP flashlight, mulligan, smuggler's satchel, forensics spoofer, mail counterfeiter, polarized contacts | Opus | 25 h | | G1 done (G1a-G1d merged) |
| G2 | Weapons and explosives: the energy sword and double sword, the Makarov and Donksoft cases, throwing weapons, power fist, North Star gloves, mini energy crossbow, C-4 and X-4, frag grenade and minibomb, the syndicate bomb with its payloads (Meta's training bomb), the EMP kit, smoke, pizza bomb, Earthcracker, power sink, power beacon, hypnotic flash and grenade, rad laser, radio jammer, batterer, syndicate teleporter, launchpad briefcase (all of `devices/traitordevices.dm` but the toolbox turret, G4's sentry gun) | Opus | 25 h | | merged |
| G3 | Implants and kits: the freedom, radio, stealth, storage, uplink, microbomb and EMP implants; the syndi-kits and bundles (`storage/uplink_kits`), the surplus crate, the poison kit, the cash briefcase, balloon, cards, stickers, posters, spraycan, soap, tome and toolbox; the PDA viruses (Detomatix, F.R.A.M.E., SyndEye) | Opus | 18 h | A2 | done (G3a-G3d merged) |
| G4 | The job-locked uplink entries (`uplink_items/job.dm`, 40): His Grace, meat hook, sentry gun, reverse bear trap, hot potato, laser arm, reagent dartgun, pie cannon, blast cannon, simian agents, combat bakery, mimery and the rest | Opus | 25 h | A2 || done (G4a-G4d merged) |
| X1 | The contractor: the contract kit, contracts and extraction by pod, the syndicate jail, the contractor baton and items, the SyndicateContractor tablet program, the contractor support unit | Opus | 15 h | A2 | done (X1a, X1b merged) |
| X2 | Holoparasites (`mob/living/basic/guardian`, 166 procs): the types and their abilities, summoning from ghosts, the host link; with SSpolling, the ghost polls every ghost role needs | Opus | 18 h | A1 | merged |
| X3 | The martial arts (`datums/martial`, 164 procs): the sleeping carp scroll, CQC, krav maga and the rest | Opus | 12 h | | done (X3a, X3b merged; wrestling kept out, D80) |
| N1 | The nuclear operatives: the team, leader and reinforcements, the outfits, the syndicate base on CentCom with the infiltrator shuttle, the nuke and the disk with its pinpointer and the decoy disk, the war declaration, Meta's self-destruct terminal, the station-destroyed cinematic and round end, the nukie rulesets, the lone operative event, the NuclearBomb window | Opus | 40 h | A1, A3 | done (N1a, N1b, N1c merged 2026-11-11) |
| N2 | The nukie arsenal: about 100 uplink entries (Bulldog, C-20r, L6 SAW, M-90gl, sniper, rocket launcher and ammo, grenades, medbeam, overwatch tools, the bundles and reinforcement beacons) | Opus | 40 h | N1, A2 | N2a, N2b, N2c merged (the MODsuit and silicon entries are booked out) |
| N3 | The clown operatives: their datum, rulesets and 16 uplink entries | Opus | 12 h | N1, N2 | N3a, N3b merged |

Not in this campaign (the owner): the MODsuits (the syndicate, infiltrator and elite suits and their modules); the syndicate
cyborgs and the hacked AI law module (silicons); the Dark Gygax, Mauler, Reticence and the concealed weapon bay (mechs); the
clown car (vehicles). Also waiting for other plans: the codespeak manual (languages, L2), CRAB-17 (the economy, P4).
Added to this campaign by the owner (2026-11-04), from gaps W1 (world maptext): the brig cell timers (`machinery/doors/brigdoors.dm`
`door_timer`, with the flashers and the BrigTimer window) and the greenscreen camera with its displays (`status_display.dm`'s
greenscreen, whose branches W1 translated); the lead places them after N3 (not-translated section 151). The brig timers: done
2026-11-12 (BrigTimerTests 4, BrigTimerWindowTests 2; the Core and Server quick suites; DM's `%` truncates both operands, so the
countdown uses `Dm.mod`. In game (Meta, the brig): as the warden, clicking Cell 1's timer opened the BrigTimer window; Short and
Start set 02:00 with Stop lit, and the cell's display showed the red CELL / 02:00). The greenscreen camera: done 2026-11-12
(GreenscreenTests 4, GreenscreenGameplayTests 1 with the client's compositor, a replicator test; the engine fixes are in
not-translated section 151; the client's model decodes are counted only once complete; the mark on the recorded tile lies on it, DV383. In game (Meta, the captain's quarters): with a
human before the poster, clicking the captain-caster showed "feed activated" and the displays beside the poster showed the human's head
and shoulders; clicking again, "feed deactivated" and a blank screen. The machine ran at load 30-46, so the client ran with
ANOMALY_ICONS_SYNC=1 (the camera's model waited over half a minute for its textures otherwise), and each look ran on a fresh server: tg's
auto-evac calls the shuttle once every joined player has left, and the evac displays then keep the shuttle's timer).

## 2. Rules

- A package follows `docs/ai/specs/engineering.md` sections 2 (the brief) and 3 (merging); worktrees `.claude/worktrees/syn-<id>`,
  branches `syn/<id>`, at most 3 of the lead's agents at once on disjoint files.
- **The uplink lists every tg entry.** An entry whose item, or a system the item needs, is not live is hidden from the uplink
  (one DEVIATIONS row, A2), and shows up by itself when a package makes it live. A gear package therefore makes items live and
  never edits the uplink's lists.
- Reserved ids, so the gaps campaign's packages (which number on from section 123 and DV318) never clash: not-translated
  sections 130-143 and deviations DV340-381, by package: A1 130 (DV340-342), A2 131 (343-345), A3 132 (346-348), X4 133
  (349-351), G1 134 (352-354), G2 135 (355-357), G3 136 (358-360), G4 137 (361-363), X1 138 (364-366), X2 139 (367-369), X3 140
  (370-372), N1 141 (373-375), N2 142 (376-378), N3 143 (379-381). Since 2026-11-05 the owner's other work has used DV347-371
  and sections 140-146, so a package from G3c on takes the next free number at its merge (G3c: DV372, G3d: DV373, section 136 extended; G4: section 137, as reserved).
- Order: A1 first with an independent gear package (G2); then A2 and A3; then the rest as their dependencies merge. G1 waits for
  the gaps campaign's P5 (the chemistry machines' `emag_act`).

## 3. Results

- **A1 (merged 2026-11-04, `63ed968b` + regen `abe2d10d`).** The antagonist datum, the custom antagonist and its info button, teams, the antag HUDs (SSantag_hud) and the combo HUD, every objective whose pieces exist (left inert: the silicons' and summon magic), the steal targets of the 27 live items (SStraitor makes them; five wait for N1, G3, research and silicons), the round-end antagonist report, AntagInfoGeneric with tg's objective printout, the admin tools as browse() pages (Check Antagonists, the traitor panel, the team panel) and the console's check-antagonists, traitor-panel, check-teams and combo-hud. DV340. Tests: AntagonistCoreTests 4, AntagInfoWindowTests 2, AntagonistGameplayTests 1; the non-Meta suites green (Core 7164, Server 3385); MetaChecks' 4 failures are Lavaland's on main. Five var-only antagonist datums turned live with it (morph, fallen changeling, former obsessed, prank vendetta, recovered crew) and the panel offers them, as tg.

- **G2 (merged 2026-11-04, `e8590ba1`, regen `34a18821`, sweeps `6746a444`, rebase onto A1 `94a3b2a2`).** All of `energy.dm` with the jousting component, the double-bladed sword, the power fist, the North Star gloves (`punchcooldown`), the Makarov and Donksoft cases, the mini energy crossbow; the minibomb, HE, frag (the pellet cloud's grenade part), gluon, EMP and hypnotic grenades; the pizza box and bomb (the whole pizza with it), the Earthcracker and its weak points, `syndicatebomb.dm` and `syndicatebeacon.dm` whole, the power sink, the hypnotic flash with the trance, hypnosis and pacifism effects, the syndicate teleporter, the launchpads and the briefcase launchpad (LaunchpadRemote), all of `traitordevices.dm` but G4's toolbox turret (RadioactiveMicrolaser). No deviation. Tests: SyndicateWeaponsTests 9, SyndicateWeaponsGameplayTests 1; the quick run on A1+G2 green (Core 7173, Server 3385). The rebase restored 13 rows G2 had removed (fixed) and A1's subsystems moved the crossbow bolt's hit past its 1-second knockdown in the test (fixed by following the hit). Open: the Earthcracker's bang is tg's only mp3, which the sound tool does not copy; the bomb's countdown maptext waits for W1; the hypnotized antagonist datum has no package.

- **A2 (merged 2026-11-04, `81d87da5` + regen, rebased onto G2 with fix-up `006a1138`).** The traitor (`datum_traitor.dm` whole: the uplink and its sales, tg's objective count, the job objective and the ending one, employers and flavor, code words highlighted by `codeword_hearing`, AntagInfoTraitor, the round-end report with telecrystals and purchases), `uplink_handler.dm`, SStraitor's code phrases, the uplink component whole (PDA ringtone, radio `:z` code, pen twist; locking, the failsafe, telecrystals, refunds, the Uplink window), `modules/uplink` whole with 364 of 370 entries live (DV343 hides the rest), telecrystals, `uplink_reimburse`, the uplink location preference; the emergency console's hijack stages; `get_crewmember_minds` (a deferred stub before: objectives found no targets) and the head of personnel's `captain_replacement` objective. Tests: TraitorUplinkTests 4, TraitorWindowsTests 2, TraitorGameplayTests 1 (a real server: panel, PDA code, Uplink, C-4 in hand, the report); the quick run's three failures (map read speed, the socket test, the regeneration test) pass alone: the machine ran three agents. Left: the surplus crate (P4's supply pod), the uplink implant (G3), the contract reroll (X1).

- **A3 (merged 2026-11-04, `941b3742` + regen `2fc62b73` + strings `43ae2904`, rebased onto A2 with fix-up `f182a7ae`).** SSdynamic (`dynamic.dm` whole with `build_dynamic_toml`), the ruleset and tier files, the roundstart, latejoin, midround, from-ghosts and from-living bases, the traitor rulesets (roundstart, latejoin, midround, mass) and extended; tg's `config/dynamic.toml` read through rust-g's `toml_file_to_json`, tg's shipped `DYNAMIC_CONFIG_ENABLED`, `PROTECT_ROLES_FROM_ANTAGONIST` and `ALLOW_LATEJOIN_ANTAGONISTS`; the ticker's roundstart selection, PostSetup's rulesets, the latejoin call, `try_reject_player`, the round result and the round-end lines; the round-start CentCom report with the threat advisory; the lobby's Antagonists tab (`middleware/antags.dm`, `be_special` saved, tg's 37 antagonists with their preview pictures, editable in the round as tg, noted on DV215); DynamicAdmin and DynamicTester, the console's dynamic-panel and dynamic-tester. DV346. Tests: DynamicTests 8, DynamicWindowsTests 4, DynamicGameplayTests (Meta: two players opt in, a Low Chaos round makes them traitors with the greeting and stinger, the third stays crew), a preference picture test; rechecked with A2's live traitor; the quick run was left to the owner (2026-11-04). The from-ghosts rulesets have no volunteers until X2's SSpolling; nobody is role-banned (DV194). Found on main: `LobbyGameplayTests`' occupations test fails since D312 added a second chevron-right to the Character page.

- **X2 (merged 2026-11-04, `f7b3ab25` + regen `ca4e92e4` + test worlds `fd377b3a`, rebased onto A3 with the lead's fix-up `9b1283b6`).** SSpolling (`polling.dm` but the stat line), `candidate_poll.dm`, `poll_ignore.dm`, the poll alert (click to sign up, alt-click never for this round, ctrl-click jump, the chat links, the role, time and count maptext), the ghost role preferences and the prompt sound; A1's `replace_banned_player` and the ghost control requests now poll; the lead wired the antagonist player-age check (`get_days_to_play_antag`, A3) into `is_eligible`. The holoparasites: `mob/living/basic/guardian/` whole but `can_suicide` (11 types, 4 themes, the verbs, the host link, the tech, wizard, carp and miner creators) with the components they need (life link, leash, damage chain, explosive trap, full-auto ranged, charge). No deviation. Tests: HoloparasiteTests 5, HoloparasiteGameplayTests 1 (an observer clicks the alert and manifests beside the host); the whole suites were left to the owner. Bots that gain a personality and regal rats now hold tg's 10-second ghost poll.
- **X4 (merged 2026-11-04, `f4b26bb8` + regen `ec7338da`; the lead's own work in this session, no agent).** `brother.dm` whole but the preview picture: the blood brother, the conversion by flash with tg's refusals (dead, silicon, in critical, unconscious, a vacant mind, the team's own target, another team's brother, the unconvertable), the brothers' team with its shared objectives (convert, kills, steals, escape or hijack), its name and its conversion count, the convert-a-brother objective, the admins' Adjust Remaining Conversions; `can_flash_from_behind.dm`, carbon `equip_conspicuous_item`, the roundstart `blood_brother` ruleset (live: Dynamic picks it, DV346 narrowed) and the AntagInfoBrother window. Deferred (section 133): the preview picture (the ethereal and moth species are not live) and the mobs' attack log lines. No deviation (DV349-351 unused). Tests: BloodBrotherTests 6, DynamicTests' live and pick lists updated; the whole suites are left to the owner.
- **G3a (merged 2026-11-04, `df8f4a0e` + regen `affc592e`; the lead's own work in this session, no agent).** G3 is split in stages, each merged on its own: G3a the implants and the kits, G3b and G3c the small syndicate items with the systems they need, G3d the PDA disks. G3a: the freedom, EMP, internal radio (with `radio()`'s implant half), storage, uplink, explosive (micro, macro, fake macro, deniability) and deathrattle implants, the stealth implant's agent box (`cardboardbox.dm` whole and the Deploy Box action), `uplink_kits.dm` whole (every kit, the two random syndi-kits, the induction kits and the nuclear operative implant), the syndicate box storages and the space suit list. The uplink implant left `UplinkWaits`; 26 kit entries whose kits hold later packages' items wait there, naming the package (G1, G3b, G4, X3, N1-N3, the MODsuits; the romerol kit waits for the zombies). No deviation. not-translated.md section 136. Tests: ImplantKitTests 9 (each implant, the kits, a spawn-and-delete sweep of every implant, implanter, case and kit), ImplantGameplayTests 1 (a real server: the implant's button opens the Uplink with 10 TC, the freedom implant bought, self-implanted, the cuffs slipped and gone from the client), TraitorUplinkTests updated.
- **G3b (merged 2026-11-04, `6e71228c` + regen `a9750818`; in this session, no agent).** The syndicate balloon, the syndicate stickers, `spawnergrenade.dm` whole, `syndie_spraycan.dm` whole with the demoraliser (live, but the moods are not ported, so it demoralises nobody yet), the soaps of `clown_items.dm`, the syndicate tome (all its procs) with `effect_remover.dm` (its screentip a live-partial row), the syndicate toolbox and the nuke screwdriver, `cash.dm` whole (space cash was inert: the briefcase's five bills merge into one 5000-credit stack, as tg), the secure, cash and hitchhiker briefcases, the lipsticks of `cosmetics.dm` (the razors are the barber's). The stickers and catnade entries left `UplinkWaits`; the posters, pony, pajamas and cutouts now wait for G3c (the cutouts moved there: they need tg's dynamic human appearances). Tests: SyndicateOddsAndEndsTests 6. Space cash, soap and lipstick are placed on MetaStation and now initialise there: the Meta suites are the ones to watch.
- **G3c (merged 2026-11-10, `7a504485` + regen `026c49f5`; in this session, no agent).** `posters/poster.dm` whole (the rolled poster hangs on a wall, takes a shard trap, comes down with wirecutters and rips by hand) with `poster_demotivational.dm`; the slime potion base and the intelligence potions with `sentient_creature.dm`, its AntagInfoSentient window and `simple_access.dm` (the pony box's potion raises only tg's dangerous pony, `SENTIENCE_PONY`); `cardboard_cutouts.dm` whole with `tactical.dm`, the `basic/everyone` alternate appearance and the image's `copy_overlays` (DV372: the cutouts whose outfit or species is inert wait); `bedsheet_bin.dm` whole, the random bedsheet spawner and `bed_tucking.dm`; `plushes.dm` whole with `keep_me_secure.dm`, `edit_complainer.dm` and `spasm_animation`; `dehy_carp.dm` and water's swelling of it; the mug; the blood-red sneaksuit. The four G3c entries left `UplinkWaits`. The cards (`modules/cards`, 990 lines) moved to G3d with the PDA disks. Also fixed: the client did not build on main (`DestinationTagger.cs` lacked `using System.Linq`). Tests: SyndicateSleepytimeTests 8; Meta boots with one runtime that is not G3c's (a mob on a lavaland basalt turf in `become_hearing_sensitive`).
- **G3d (merged 2026-11-10, `624757f2` + regen `ad580c6f`; in this session, no agent).** `modules/cards` whole (the deck deals face down and up, two cards make a hand, a hand or a card goes back into the deck, a wielded deck shuffles, a thrown deck forces 52 card pickup, a pen or crayon marks a card; the blank, CAS, KOTAHI, tarot with the haunted deck, Wiz-Off and card shoe decks; the CAS and Wiz-Off strings files mounted) with the holographic deck; `virus_disk.dm` whole (the clown's, the mime's, the Detomatix and the F.R.A.M.E. disks) with the messenger's virus send, the rigged message and its explode link, the PDA's `explode` and the clown disk's upload (`sound_player.dm`), the clown PDA's slip charge and the hidden uplink's illiterate unlock; the computer disks, which the stage needed for any disk to be used (inserting and ejecting, a disk's files in the file manager and the notepad, a unique program leaving its disk); `secureye.dm` with the syndicate data disk and the NtosSecurEye window (the camera console's content), without the person tracking (DV373: `/datum/trackable` and the camera chunks are the AI's). The four G3d entries (syndiecards, Detomatix, F.R.A.M.E., SyndEye) sell. not-translated.md section 136. Tests: SyndicateCardsAndDisksTests 8; DetomatixGameplayTests 1 (a real server on MetaStation: Alice's Detomatix disk, the messenger's "Attach Virus", tg's three forge prompts, the rigged message through the telecomms hub to Bob as from the forged sender, and Bob's rigged reply link blowing up his PDA). Meta boots with the one runtime that is not ours (a lavaland basalt mob in `become_hearing_sensitive`).
- **G4a (merged 2026-11-10, `5d1edd87` + regen `970b3d19`; in this session, no agent).** G4 is split in stages: G4a the small job items, G4b the guns (the meat hook,
  the reagent dartgun, the pie cannon, the blast cannon, the syndicate rebar crossbow), G4c His Grace, the simian agents and the sentry
  gun with the portable turrets. G4a: the magic eightballs (the haunted one's ghost vote, EightBallVote), the mail whole but the economy's
  crate (letters, envelopes, the traitor's armed mail, the mail crates, the GLA-2 counterfeit device, the item_scaling element), the DNA
  injectors and the DNA syringe gun, the telecrystal toolbox, the hot potato, the reverse bear trap, the reverse revolver in its box of
  hugs, the briefcase gun, the combat bakery (the baguettes, the throwing croissant, the crafting recipe books), the arcade prize pool
  and the toy spawner. 11 entries sell: the haunted eightball, the counterfeit kit, the clumsiness injector, the ancient toolbox, the
  hot potato, combat baking, the reverse bear trap, the modified syringe gun, the reverse revolver and both briefcase guns. The
  bureaucratic error waits for the round events; the syndicate MMI, the springlock module and the clown car are out of this campaign.
  Fixed on the way: a random spawner inside a container spawned its loot into nullspace. Found, not fixed (section 137): the flesh
  wounds stay inert for want of their reagent reactions, and a head shot's eye puncture throws. No deviation. Tests:
  SyndicateJobItemsTests 13, CounterfeitMailGameplayTests 1.
- **G4b (merged 2026-11-10, `a6b5911e` + regen `8d18defb`; in this session, no agent).** The meat hook whole with the magic gun base (`magic.dm`; the wands and
  staves stay the wizard's); the reagent gun with its dart synthesiser; `pneumatic_cannon.dm` whole (the pie cannon and the pneumatic
  cannons) with the cream pie that splats, `components/splat.dm` and `components/face_decal.dm` (the pie on a face, cleaned by a wash);
  the blast cannon whole with its blast wave and, since it fires nothing without one, `transfer_valve.dm` whole with its TransferValve
  window (the ordnance lab's TTV bombs work: an assembly on its fuse opens it, so the assembly's `holder` is hand-declared as an item);
  the bolt rifle base and the rebar crossbows of `rifle.dm`, their rods with `elements/caseless.dm` and `elements/projectile_drop.dm`,
  the rebar quivers and their storage. All five entries sell (the crossbow left `UplinkWaits`). Fixed on the way: washing a human threw
  (the bodyparts' `on_owner_clean`), the ballistic `drop_bolt` was synchronous though the crossbow's waits (slots.tsv had it coloured).
  Leaves (section 137): the cuffable element, the wheelchair bomb, the cosmic fields, the admin messages and the pie cannon's hold to
  fire (no autoclick yet). No deviation. Tests: SyndicateGunsTests 8, MeatHookGameplayTests 1. Also fixed in the tests: the three
  create-and-destroy sweeps did not read `GLOB.adjectives`, which G4a's junk mail picks from (they failed since G4a), and the integrity
  test's untranslated-damage example was the cardboard cutout, live since G3c (now the secure crate). Still failing on main and not
  G4b's: tg's create_and_destroy on the beesease bottle (the diseases), and the Meta load counts with Lavaland's fourth level.
- **G4c (merged 2026-11-10, `311cd680` + regen `a17e8096`; in this session, no agent).** G4 gets a fourth stage: G4c His Grace, the supply
  pod and the simian agents; G4d the sentry gun with the portable turrets. His Grace whole with tg's stun absorption (`stun_absorption.dm`
  whole), His effect and His Wrath; the supply pod (`supplypod.dm` whole but the gondola pod mob and a goody order, `podspawn`): it is
  cargo's (gaps P4) but the monkey agent rides it, and it retires the workarounds of the surplus crate (both entries sell), the job choice
  beacons, the objective equipment's fallback and the cargo shuttle's contraband return; the loadout beacons with the monkey agent
  (`crewlike_monkify` and its two mutations, the syndicate monkey antagonist), the monkey gun case with its guns, switchblade and Uzi
  magazine, the secure crate base and the syndicrate with its key. Engine: the shim's `/icon` gets `SwapColor` (server and client; the
  pod's door masks), and a movable given an area on the map lands on its first turf (BYOND, OpenDream: the pods wait in CentCom's holding
  area; one engine test that assumed otherwise is updated, the off-map room stays). Decided slot rows: four closet procs and
  `spawn_antag` (translation-rules Appendix A). Kept as tg has it: the monkey gets no objective and its allergy dies with the beacon
  (section 137). No new deviation (DV343's text updated). Tests: SyndicateG4cTests 6, SupplyPodMaskTests 2 (server tests, the real pod
  icon); the create-and-destroy lists skip the landing zones as tg's do; the integrity example is now the pinata; the medical wounds unit
  test keeps its victim's limbs (the boot's mutation shuffle moved the seed onto a critical tier's dismemberment).
- **G4d (merged 2026-11-10, `951ace96` + regen `b07978d1`; in this session, no agent). G4 is done.** The `porta_turret/` files whole: the
  portable turret with its cover, frame (built step by step into a turret with the energy gun put in it) and control panel, the turret
  subsystem, the turret tracker and the hostile machine element; the toolbox sentry turret (bashed out of the turret toolbox with its
  combat wrench, repaired and folded back with it), both turret toolboxes (the nuclear operatives' is live for N2), the combat wrench and the
  hybrid turret gun; the turret branches of the tased status effect. The client draws the PortableTurret and TurretControl windows; Meta's
  AI turrets and three control panels are live. Generator: the turret's `base` and the sentry's `toolbox` are objects (`ObjectHoldingVars`).
  Left out (section 137): the silicons' and the hulk's clicks (live-partial rows), the blob branch, the syndicate turret's navigation icon
  and minimap blip; the AI's manual control is translated but unreached. Kept as tg has it: a turret shoots anyone without a crew record.
  No new deviation (DV12 for the panel's glow). Tests: SyndicateG4dTests 4, TurretWindowsTests 3 (server tests); MobLifecycle leaves out the
  random syndi-kit (the shifted stream picks its bees kit, whose disease is not ported). In the game: Meta's AI-upload turrets pop out of
  their covers and tase an intruder (the electrode tether drawn), the sentry fires bullets, the frame and toolbox render. Found on the way
  (not this stage's): six Meta checks and two M10 shuttle tests fail on main as well (three were known; the other five, which G4c's filter
  did not run, meet the Lavaland level: Meta has four levels, a Lavaland turf in a movables loop, the random dock gone).

- **G1a (merged 2026-11-10, `8cfbc857` + regen `4493484a`; in this session, no agent).** G1 is split in stages, each merged on its own:
  G1a the emag with every live object's `emag_act`, G1b the agent ID and the chameleon gear, G1c the rest of the stealth tools.
  G1a: `emags.dm` whole but the battlecruiser card (the emag and its look-alikes, two emags stuck together, the fake emag a real one
  rigs to blow, the doorjack's recharging charges), `/atom/proc/emag_act` as the root (every translated `emag_act` overrides it now;
  three that skipped tg's parent call make it), and the 15 live-partial `emag_act` stubs: the button, the communications console, the
  airlock and windoor with their `finish_emag_act`, the firelock, the fire alarm, both shield generators, the message monitor, the
  intercom, the closet, the secure safe, the air alarm, the space suit, the modular computer console and the modular computer (the
  PDA's syndieOS theme). The emag and doorjack entries show in the uplink (DV343 hides them no more). No deviation. Left out (section
  134): the battlecruiser card with the Starfury (N1 or later), the bomber and emag log lines (M10), the AI's `emag_act` (the
  silicons) and the 64 `emag_act` procs of inert machines. Tests: SyndicateEmagTests 4; the neighbouring Core suites (doors,
  closets, bots, vendors, turrets, uplinks, the create-and-destroy sweeps; 1116) and the Server traitor, uplink and door tests (147)
  pass. In game (Meta): a doorjack swiped on the port hallway's airlock opens, bolts and darkens it with "It now has 2 charges
  remaining."; an emag on a chief engineer's locker breaks its lock with sparks.

- **G1b (merged 2026-11-10, `bc3d45dd` + regen `e872e79f`; in this session, no agent).** `clothing/chameleon/` but the drones' camogear: the chameleon change action
  and its subtypes (clothing, PDA, stamp, the agent card's look and trim, the chameleon laser's and the scanner's), the outfit action
  (a job's outfit or a saved one over every chameleon item), the chameleon clothing and the broken kit, the chameleon laser with its
  casing and harmless projectile, the chameleon scanner; the agent card of `cards_ids.dm` (access theft through its ChameleonCard
  window, forging, the camera tracking it hides); `datum/outfit`'s `get_chameleon_disguise_info`. The client draws ChameleonCard with
  the AccessList component. The chameleon kit, the broken kit, the agent card and the no-slips sell (the kits left `UplinkWaits`). No
  deviation. Left out (section 134): the drones' camogear, the forger's bank account (SSeconomy), the access and forging logs, the mask's
  TTS voice spoofing (translated, unreached while TTS is off). Tests: SyndicateChameleonTests 5, ChameleonCardWindowTests 1; the
  neighbouring Core suites (770 with the create-and-destroy sweeps) and the Server Tgui (224) and traitor tests pass. In game (Meta):
  the agent card in hand shows its look, outfit and trim buttons, and swiped over the captain's spare ID opens ChameleonCard with the
  spare's accesses under the Common wildcard.

- **G1c (merged 2026-11-10; in this session, no agent).** The EMP flashlight, the polarized contacts, the mulligan serum with the fake
  identity kit and the arrivals signaller (`mulligan_kit.dm` whole), the smuggler's satchel with its storage, the AI detector (its
  static scan finds no camera chunks, DV373; no AI exists). `randomize_human_normie` now gives tg's random name (a test world without
  tg's strings keeps the name, rules 10.6) and `copy_clothing_prefs` is translated. Fixed on the way (this campaign's G4c): Appendix C
  names `icon.SwapColor`, and the atoms test counts the live mutations. The thermal glasses and the chameleon projector were stage G1d.
  Tests: SyndicateStealthToolsTests 6; the Core quick suite. In
  game (Meta): the EMP flashlight blinked at a chameleon jumpsuit scrambles its look for thirty seconds ("now has 3 charges").

- **G1d (merged 2026-11-10; in this session, no agent).** The thermal glasses whole (`_glasses.dm:586-645`: the EMP overload, translated
  in `thermal_overload` for the HUD thermals too, the thermoncle's examine, the eyepatch's flip), so the chameleon thermals and the x-ray
  goggles are live; the chameleon projector (`chameleonproj.dm` whole). An atom given an appearance snapshot now takes its name and
  description, as BYOND's appearance does (the dummy is named as what it copies). The client draws the mobs behind the walls for a living
  SEE_MOBS viewer (DV374, `see_mobs.gdshader`); the projector scans anything with an icon state (DV375); the dummy is sent as the type it
  copied, so the client draws it with that thing's model and scale (DV376, `IShownAsType`). Tests: SyndicateThermalsAndProjectorTests 4
  (one drives the dummy with a player's held move key through the capsule bridge), the client's vision test; the Core and Server quick
  suites. In game (Meta): with the chameleon thermals on, a human and a medbot behind a wall show through it; the projector scanned a
  toolbox, hid its holder as an item-sized toolbox on the floor, and the disguise crept two tiles north in two seconds.

- **X3a (merged 2026-11-10; in this session, no agent).** X3 is split: X3a the base and the Sleeping Carp, X3b the other styles. The
  martial art base whole (`_martial.dm`: the unarmed strike, grab and grab resisting hooks, the streak and the combo display, teaching,
  unlearning and switching styles), its button, the mind-bound and item-given components, the martial books and the sleeping carp
  scroll (it left `UplinkWaits`), the Sleeping Carp whole with the bo staff (the staff's walking aid) and the carp gloves, the silence,
  the species' martial attacking limb. The types tool keeps the components' `style` an object (the name is a typepath elsewhere). No
  deviation; not-translated section 147. Tests: SyndicateMartialArtsTests 4; the Core and Server quick suites. In game (Meta): the
  scroll's reading teaches the art, the first carp shove shows the combo display, the second (the Keelhaul) kicks the floored target and
  clears it, and the art's button (the move list) is drawn on the action bar with its key 2 (an earlier run, from a
  pre-merge build, showed none; the merged build draws it, and MartialArtsButtonGameplayTests composes and clicks it through the real server).

- **X3b (merged 2026-11-10; in this session, no agent).** The other styles: CQC whole with the chef's Close Quarters Cooking (the cook's
  chip now teaches it; it works only in the kitchen) and the CQC manual; boxing, evil and hunter boxing with the boxing gloves (their
  traitor mail uppercut), the punching mitts, the crit visual and the rebuke; Kaza Ruk with its three stance buttons, follow-up strikes,
  gloves and implant; the Plasma Fist with its scroll and the repulse spell (whole, with the wizard's and the xeno's); the Spider's Bite
  and its scroll; the Jungle Arts, the Mushroom Punch with the odd mushroom, the Psychotic Brawling with the violent psychosis; the
  martial gi of the carp kit (X3 left the syndi-kit's `UplinkWaits` text). Wrestling is kept out: `wrestling.dm` is Goon-derived and
  D80 names it. Kaza Ruk's two `activate_style` and `deactivate_style` definitions are joined in DM's order (the later one's `..()` runs
  the earlier). The types tool keeps the chef chip's and the implant's `style` an object. The athletics skill (an untrained mind), the
  sources of the Jungle Arts and the Mushroom Punch, the plasmaman Apotheosis, the touch spell return and the gloves' reskins are leaves
  in section 147. No deviation. Tests: SyndicateOtherMartialArtsTests 8; the Core and Server quick suites (the regeneration test re-run
  after the gi). In game (Meta): the Kaza Ruk gloves put on gave the three stance buttons and the punches their elbow strike follow-ups;
  the plasma fist scroll read, Punch Punch Shove shouted TORNADO SWEEP! and threw both humans in front into the wall.

- **X1a (merged 2026-11-11; in this session, no agent).** X1 is split: X1a the contracts, X1b the support unit and the fulton kit. The
  contractor hub and the syndicate contract (the extraction pod, the stripped victim's six minutes in the holding facility with tg's
  ordeal, the pod home), the contract reroll, the Syndicate Contractor Uplink app with its client window (SyndicateContractor and tgui's
  FakeTerminal, typed out on a new render-again clock of the client's tgui), the lazy templates with the ninja den (converted and
  mounted like the shuttles), the holding facility's landmark, `gather_belongings`, `give_important_for_life` and
  `find_obstruction_free_location`. A fix on the way: the mounted `lazy_templates/` folder made preloadTemplates read a folder, which
  the map reader refused, so SSmapping's init stopped before the station areas were listed; the reader now gives an empty text tg's
  null bounds. The extraction pod's `pre_return` waits for the lazy load. The types tool keeps the contract's `id` an object. Dev
  commands `antag` and `lazyload` for the playtests. The ransom's bank and cargo money, the black market's early return and the
  souvenir achievement are leaves in section 138. No deviation. Tests: SyndicateContractorTests 3, ContractorGameplayTests 1 (a real
  server: the pod lands at the dropoff, the target goes in, is held in the den on the reservation level, and the return pod brings them
  back); the Core and Server quick suites. In game (Meta): the tablet's app registered a traitor (the typed terminal, the briefing)
  and listed six contracts with their targets, payouts, messages and dropoffs; a player moved into the loaded holding facility saw the
  den drawn.

- **X1b (merged 2026-11-11; in this session, no agent). X1 done.** The contractor support unit (`contract_teammate.dm` whole: the
  antagonist and its outfit; the beacon that calls a ghost down in a pod was G4c's) and the fulton kit: its box and `mining/fulton.dm`
  but the networked kinds (the mining's "Candela" networks). The hub's three extra entries (the pinpointer, the fulton kit, the
  reinforcement) now sell. DV377: a holder that lifts a mob is drawn as that mob (as the chameleon dummy, DV376); a lifted thing drawn
  as a 3D model shows no balloon or rise (a client leaf in section 138). Tests: SyndicateContractorSupportTests 3,
  FultonGameplayTests 1 (a real server: the clients get the holder with the person's picture under the balloon, animated, and the
  person back next to the beacon); the Core and Server quick suites. In game (Meta, the holodeck): the beacon's naming window and the
  pack's list of beacons, "attaching...", "extracting!", the balloon inflating over the target and lifting them away, and the descent
  next to the beacon; the support unit's outfit and antagonist (its AntagInfo window, the lit cigarette, the toolbox).

- **N1a (merged 2026-11-11; in this session, no agent).** N1 is split: N1a the nuke, N1b the operatives (their team, outfits, base and
  shuttle, the war declaration, the rulesets and the lone operative). The nuke whole with its NuclearBomb window (the client's; its
  keypad is LockedSafe's too, and tgui's fluid buttons are now blocks in a stack item, which the keypad's columns need), Meta's
  self-destruct terminal with SSmapping's nuke threats (the vault's circuit floors red while it counts down), the syndicate nuke, Meta's
  beer nuke (its flood is a round event: a leaf), the nuclear disk with the station-loving component, the cinematics (the client
  hides its whole interface while tg's splash plane shows, with the player's own switch: the console's `ui` and F6, the owner's call; and it parses BYOND's percent screen_locs, DV378; the cinematic stays on the screen of a player gibbed as the explosion starts, who
  sees it to the end as a ghost, DV379), the nuke core and its container, the steal
  targets of the disk and the core, the nuked round end. `loc_name` is translated on the way (its NotTranslated threw inside the
  station-loving component's handlers), and so are the foam darts (G2's Donksoft guns loaded inert darts, whose untouched materials
  broke the magazine's material count; a surplus crate with a Donksoft case showed it). Dev command `nukecode`. Tests: SyndicateNukeTests 5, NukeGameplayTests 1 (a real server: the
  disk and the keypad through the client's window, the countdown, the cinematic's screen on the client, the nuked station and the round
  over); the Core and Server quick suites. In game (Meta): the disk in, the code and 90 seconds typed on the window, DEVICE ARMED at
  delta with the vault's floors red, then tg's intro countdown and the station exploding over the screen, the player shredded to atoms
  and the round-end readout with the station destroyed.
- **N1b (merged 2026-11-11; in this session, no agent).** The operatives: their datums (operative, leader, reinforcement, the lone
  operative's team), the team with its result and report, the outfits, the job and its spawn points, the roundstart ruleset (the
  elevator, the base's load, the infiltrator's call; live, DV346's list shrinks by one), the syndicate base, its elevator, the
  infiltrator and the base's assault pod converted from tg's maps, the infiltrator with its terminals and board, the syndicate
  closets, `syndicate_name`, the alias and species preferences, `equip_species_outfit` (it was a skeleton), the kit's boots, nuclear PDA,
  Ansem lore and survival box, the operative's lobby picture. What the base and the kit needed is ported whole rather than left inert
  (the owner, 2026-11-11: no workarounds, port the missing mechanics): the pressure tank with the gas leaker, volatile gas storage,
  crackable and cracked (SSair processes the leaker as tg passes it, a datum), the NTOS radar programs (Fission360, Lifeline, Custodial
  Locator) with the client's NtosRadar windows (the client's Overlay places a top-left child at PosX, PosY) and tg's radar pictures,
  the base's keycard doors with SSqueuelinks, the hijacked shuttle's dock at the base, the nuclear pinpointer's armed-nuke line.
  A dock made in a template's load yields a tick before loading its round-start shuttle (tg's call sleeps at the reservation's
  CHECK_TICK, the tick spent by the load; the port's never is, DV3: the assault pod's dock recursed). The engine's contents
  reconcile drops an appended copy of an atom already inside (tg's `area.contents += turf` of a turf the area holds: the base's
  space tiles over its reserved level's space) instead of rebuilding the area's list of a whole level each time: the base's load took
  four minutes, now about fifteen seconds in a Debug test (still one tick under DV3: the server stalls while it loads). Left for the owner: the operatives' tactical map implant (tg's minimap
  HUD against D315's minimap). Tests: SyndicateOperativeTests 3, PressureTankTests 2, Fission360Tests 2, NtosRadarTests 2, DynamicTests
  updated, NukieGameplayTests 1 and NukieRoundTests 1 (real servers: a leader made on Meta wakes on the base, the infiltrator docks
  there and flies to Meta's northwest dock from its console's window; a lobby round with the nukies the only ruleset makes the two
  players who prefer the operative the squad in the elevator, brings them down into the base and calls the infiltrator).
- **N1c (merged 2026-11-11; in this session, no agent).** The leader's war declaration (the telecrystals shared among the uplinks, the
  challenge timer, the held shuttle; the admin's force-war and reinforcement topics; the research bonus waits for the research), the
  round's events (SSevents, the event controls and events; an event of a type not ported is set aside as one of another map, DV380;
  random events stay off in our config, tg ships them on: for the owner), the lone operative event with the disk's weight on it (the
  ghost poll by the antagonist preference, tg's mock client and `randomize_human_appearance` for the body, DV381), the scrubber
  overflows and the beer nuke's beer flood, the midround nukies ruleset; the infiltrator's navigation computer whole with the advanced
  camera console, the camera eyes (`/mob/eye`, the camera, the remote: the base the silicons' AI eye builds on), the nav icon element on
  the airlocks, shutters, turrets and shuttle consoles, SSshuttle's dock overlap and the hidden shuttles' turfs (the infiltrator and the
  pods are hidden; their entries follow the turfs a move replaces, TURF-IDENTITY), `/mob/up` and `/mob/down`. The client looks from a
  camera eye as from a ghost's orbit, its keys flying it (tg's relaymove), the landing zone flat on the tiles (DV382; the view size is
  left alone, DV349). On the way: BYOND's `/image` is a `/datum`, so `qdel` of one is a datum's (an operative's antag HUD qdels its
  images when the body is gibbed; the port's non-datum path threw and cut the gib short). Left: the camera chunks a plain camera console's eye asks for (silicons SI4), the console's circuits and silicon
  clicks, the ghosts' alerts. Tests: SyndicateN1cTests 5 (the war declaration, the event pool, the beer flood, the lone operative from a
  signed-up ghost), NavigationComputerGameplayTests 1 (a real server on Meta: the eye, its images and keys, a clear spot placed, the
  view given back, the infiltrator flown to the custom location), NukieGameplayTests +1 (two ghosts who sign up for the forced midround
  nukies wake on the base as the team, tg's cap of two at this population), client rule tests 2.
- **N2a (merged 2026-11-11; in this session, no agent).** The guns, as section 149 lists: the operatives' automatic guns, the Bulldog,
  the sniper rifle, the launchers with the backblast element, the pneumatic grenade launcher, the implant firing pins, their magazines,
  casings and projectiles, the gun cases, the sniper briefcase, the grenadier belt and the energy shield. The uplink's Donk and surplus
  sniper magazines and the grenadier kit leave the waits list. The M-90's underbarrel var is the launcher's base type (GAP(types): the
  unrestricted carbine holds a plain launcher). Tests: SyndicateN2aTests 8 (the pin's implant check, the L6's cover, the Bulldog's drums, the
  M-90's underbarrel, the rocket's blast, the disruptor's sleep, the cases' kits).
- **N2b (merged 2026-11-11; in this session, no agent).** The kits, as section 149 lists: the duffel bags, the large crate and the crate
  of hats with the unusual effect, the outlaw's hat and spurred boots, the pinatas with their component and what they drop (gum and
  lollipops with the chewable element, the tator tot with the dunkable element, the Donk pocket box). The buzzkill and manhack grenades,
  the induction kit, the outlaw's bundle and the pinata leave the waits list; the core gear, the double-bladed sword case, the medical
  bundle and the firestarter wait for the MODsuits (their MOD parts). Tests: SyndicateN2bTests 10 (the bags' kits, the crate and the
  crowbar, the unusual glow, the hat's flight, the snake in the boots, the pinata's setup, loot and break, a toolbox swung at it in
  combat mode, the gum, the dunk); the Core and Server quick suites. In game (Meta, the bar): the kit used in hand set up the syndicate
  corgi pinata; swung at in combat mode with a toolbox (outside it, tg's toolbox offers its tools) it played its swing, dropped a grenade
  and a C-4 charge and turned to its damaged sprite. The baseball bat is still inert (its pre_attack, a later package).
- **N2c (merged 2026-11-11; in this session, no agent).** The reinforcements, as section 149 lists: the operative and overwatch
  beacons, the overwatch agent's datum with the bodycam component, codespeak and the language manuals, the ED-209 beacon, the assault
  pod and its targeting device, the agent's jacket and turtleneck. The cyborg teleporters (the silicons) and the clown beacon (N3) stay
  inert. Tests: SyndicateN2cTests 7 (the beacon's operative check, a ghost who signs up dropped as a teammate, the overwatch agent at
  the outpost with codespeak and the team's bodycams, the ED-209's pod, the assault pod's landing zone, the codespeak manuals); the
  Core and Server quick suites. In game (Meta, the base): an operative's ED-209 beacon dropped the blood-red supply pod with the
  "ED-209(+1) Syndicate Robot"; the assault pod's targeting device opened its "Area to land" list of the station's areas. The ghost
  beacons (reinforcement, overwatch) need a second player who signs up: the tests cover them.
- **N3a (merged 2026-11-11; in this session, no agent; worktree syn-n3).** The clown operatives, as section 150 lists: their datums
  and outfits, weapons, shoes, costume bag, rulesets, space lube with the slippery item component, the clown infiltrator map; every
  clown uplink entry is live. N3b is the bananium nuke's clowning of the station (`make_into_clown`), which needs tg's fear system
  (`components/fearful/`, about 860 lines) for the clown phobia. Tests: SyndicateN3aTests 10 (live types, the clown operative and his
  kit, the leader's title, the sword, the shield, the bombanana, the tear-stache, the combat shoes' peels, super lube, the costume bag);
  DynamicTests' live list gains the clown ruleset; create_and_destroy leaves out bundle_b (its random bees kit's disease bottle, as
  MobLifecycleTests); the Core and Server quick suites. In game (Meta, the base): a clown operative ("You feel lightheaded.", the
  clown mask) sprayed super lube on the tile ahead (the wet floor sheet) and lit the bananium sword ("bananium sword enabled", its
  yellow light).
- **N3b (merged 2026-11-12; in this session, no agent; worktree syn-n3).** The bananium nuke and the fear system, as section 150 lists:
  `make_into_clown`, the fearful component with its effect handlers and simple sources, the phobia source and the phobia traumas, tg's
  phobia lists (read at world start: a bare test world without tg's strings gets no phobia words instead of tg's crash, which
  broke 1369 Core and 8 Server tests on the first run); the dev command `trauma [WHO] TYPE`. Tests: SyndicateN3bTests 7 (live types, the nuke's cinematics and icons, the
  clowning, a clown in sight, the word honk, panic and a hug, losing the phobia); the Core and Server quick suites. In game (Meta, the
  bar): `trauma me` gave the clown phobia ("You start finding clowns very unnerving..."), the spawned bananium fission explosive showed
  its yellow sprite, and a clown mask dropped in view frightened ("Seeing clown wig and mask shakes you up!", "terrifies you!", the
  startle's "You lose your balance!"). With N3 the nuclear operatives are done but the MODsuits and the silicons (booked).
