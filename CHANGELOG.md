# Changelog

What changed in each version of Anomaly Station, newest first, written for the people who play the builds. The version
scheme and the rules for this file are in [docs/versioning.md](docs/versioning.md). A build names its version on the
client's top-right corner, in its window title and in the first line of the server's log: `0.5.0 (37558c67)` is version
0.5.0 built from commit `37558c67`; `dirty` after the commit means it was built from uncommitted changes.

## [Unreleased]

### Added

- Joining shows a loading screen while the station is built and loaded; your character enters the round only once the game is ready.
- Light floor tiles glow and light the room around them, laid on the floor or as a ceiling; lava and circuit floors light their surroundings too.
- Another player's OOC message plays a soft ping; its volume (or off) is in Settings, Audio.

### Changed

- Scanner readouts (health, gas analyzer and the like) show whole in a framed box in the chat, as in SS13, instead of a separate window.
- Station announcements and admin messages show in coloured boxes in the chat; OOC lines are larger and bold.

### Fixed

- Fewer stutters while moving into parts of the station seen for the first time; the game no longer prints Ogg Vorbis warnings at the start.
- Map editor: flying up inside a room two levels high keeps the lamps' light and shadows instead of turning the room flat.
- Lasers fly through windows, glass doors, grilles and glass floors and ceilings aimed at in first person, as in tg.
- Map editor: a floor placed as a ceiling from inside a room whose walls are two levels high goes on top of the walls.

## [1.13.0-pre-alpha] - 2026-11-25

### Protocol

- Network protocol 95: clients and servers older than 1.13.0-pre-alpha cannot connect to this version.

### Added

- A luminescent jellyperson's integrated slime extract now does what its colour does in tg, for both its minor and major activation.
- The mech removal tool works: two-handed (5 force in one hand, 19 wielded), and wielded it pries the pilot out of a mech.
- Held melee things attack with animations made in Blender: a one-handed chop straight down, a two-handed chop, and a stab for pointed things such as screwdrivers and spears.
- Dev menu: pick a held thing's attack animation, as a default for all things and for wielded ones, and as an override for each item.
- Dev menu: held things can be flipped left-right or up-down in one click, all at once, wielded or one by one, and given their own wielded pose.
- Moths and plasmamen are playable: moth wings, antennae and markings in Character Setup; plasmamen spawn in envirosuits and burn in oxygen without one.
- Ethereals and ceruleans are playable too: an ethereal glows in its colour and runs on charge; a cerulean has a fish tail and frills.
- Podpeople, mushroompeople, snailpeople, jellypeople, slimepeople, luminescents, stargazers, skeletons, zombies and golems are ported, each with its own mechanics.
- Abductors, dullahans, vampires, spirits, ghosts and nightmares are ported too; every tg species is now playable (most through mirrors, toxins or admins).

### Changed

- Magazines, speedloaders and ammo boxes are drawn half as large in first-person hands.
- A melee hit, with a weapon, a fist or a mech, always lands on the body part you aim at; a lost limb can't be hit at all.
- In first person your own attacks no longer show the flying, spinning weapon sprite; others and the third-person view still see it.
- A body part flashes red, for everyone, on any damage it takes (a blow, a fall, a burn), not only on a shot's hit.

### Fixed

- A melee swing's whoosh no longer plays on clicks faster than the weapon's attack cooldown; you hear it only when a blow really goes out.
- Coin flips, a vampire's drinking, an ethereal's overcharge and a torn-off fish tail now play their sounds.
- Tab in the lobby no longer hides the mouse pointer.
- The crosshair now opens for the spread of firing two light guns at once in combat mode.
- Picking an item up with an empty hand in combat mode no longer plays a punch swing.
- Character Setup's species page opens again, so the lizard and the felinid can be picked (it showed an empty window).

## [1.12.0-pre-alpha] - 2026-11-23

### Added

- New 3D models: syndicate beacon, turret and sleeper, corgi pinata, Interdyne container, nuke statue, flatpacker, flatpack cart, minimoog, organ harvester.

### Changed

- Client builds are leaner: no console exe, BUILD-MODE.txt or editor launcher; the editor starts from editor/, and short READMEs cover launch problems.

## [1.11.1-pre-alpha] - 2026-11-22

### Protocol

- Network protocol 94: clients and servers older than 1.11.1-pre-alpha cannot connect to this version.

### Changed

- Shotguns kick the view twice as hard.
- The game is named Anomaly Station everywhere: builds, the program, the macOS app, the server's install folder and service.
- Map files are `.as3dmap` and the code is named Anomaly; maps saved as `.ss3dmap` still open in the editor.

## [1.11.0-pre-alpha] - 2026-11-21

### Added

- A melee swing whooshes: a knife or a sword slashes, a heavy or long thing swings hard, a fist whiffs in combat mode.
- The Emergency NanoMed hangs on the wall as a 3D cabinet instead of a flat picture.

### Fixed

- The deathmatch arena's medbay-to-atrium corridor and its maintenance tunnel keep their air: a maintenance roof tile no longer vents them to space.

## [1.10.0-pre-alpha] - 2026-11-21

### Protocol

- Network protocol 93: clients and servers older than 1.10.0-pre-alpha cannot connect to this version.

### Added

- Dev menu, Player tab: an auto-respawn mode; a dead player sees a countdown and comes back in a new body at a spawn point (10 s, settable).
- Dev menu, Player tab: possess any mob (an NPC, an animal, a cyborg) and play it from its own eye height, then return to your body.
- A deathmatch arena map (`--map deathmatch_arena`): eight rooms, weapon pads that refill in 5 s, two mechs, respawns, and a kills and deaths scoreboard on K.

### Changed

- Every gun now kicks: its climb pulls the aim up (hold it down with the mouse), heavy guns jolt the view, and the kick depends on the gun.
- Muzzle flashes are twice as big, the own gun's and other shooters'.
- Rockets and 40mm grenades fly as big pictures of themselves with a fire trail behind them.
- Rockets and 40mm grenades fly half as fast as before.
- A mech's guns aim at the crosshair, kick and flash at the muzzle of the arm that fired, and its shots leave that arm; the crosshair shows their spread.

### Fixed

- A rocket or grenade fired at the ceiling explodes under it, where you see it, and the ceiling or the floor above takes the blast.
- A crack in a ceiling tile no longer wipes the shot dents from it.
- The smart SMG's bullets hit the target they home on instead of flying through it half the time.
- The smart SMG's lock reticle shows on the target its bullets home on, in front of it, whatever you fired at.

## [1.9.0-pre-alpha] - 2026-11-21

### Protocol

- Network protocol 92: clients and servers older than 1.9.0-pre-alpha cannot connect to this version.

### Added

- With a gun in hand the crosshair shows how wide its shots scatter, opens as you fire and marks your hits.

### Changed

- In the third person's free look a shot, a hit or a throw turns the character to the crosshair for a second (a setting); keys strafe meanwhile.
- Guns spread half as wide as in tg by default; a server setting (spread_scale) sets the share, 1 for tg's own spread.
- Grenade launchers fire their shells straight along the crosshair again instead of lobbing them on an arc.

### Fixed

- On a busy server R throws the held item again instead of it flashing back into the hand or hitting what is in reach.
- The Abielle smart SMG's bullets home onto the man aimed at again instead of spraying past him, and homing shots are seen in flight.
- Homing shots fly a smooth arc on screen with an unbroken tracer instead of jumping two metres at a time.
- The smart SMG's bullets fired at the floor or a wall never lock onto the shooter.
- The rocket launcher's backblast is drawn behind the shooter instead of on and in front of them.
- The tesla cannon's lightning is drawn whole instead of as brown boxes.

## [1.8.0-pre-alpha] - 2026-11-20

### Protocol

- Network protocol 91: clients and servers older than 1.8.0-pre-alpha cannot connect to this version.

### Added

- Shots leave holes in floors and ceilings as in walls, and what breaks walls (the pulse rifle) breaks floors and ceilings too.
- Dev menu, Player: an "Invisible (like a ghost)" switch hides the player from everyone but ghosts and draws them see-through.
- The security barrier is drawn as a storey-tall block like the wooden barricade, not a flat billboard.
- Dev menu, Tools: a world-things tuner sets a 2D thing's type to stand or lie flat, its size and its move, saved for everyone.
- The jukebox, radiant dance machine, barbeque grill, drying rack and disk compartmentalizer are 3D models.
- A metal table frame is a 3D frame on four legs instead of a flat picture.

### Changed

- The wall and ceiling height is the server's setting (WALL_HEIGHT, 2 to 4 m), the same for every player; the client option is gone.
- Projectiles fly 30% faster with shorter tracers; a shot lights the floor ahead, and the own muzzle flash is twice as big, sized with the gun.
- Projectiles drawn from tg's sprites are smaller, face the eye and leave the gun's muzzle.
- The version tag reads "Anomaly Station, Development Build"; held weapons and tools get retuned first-person poses.
- The Cards Against Spess decks are removed (non-commercial license).
- Sounds and music with no license (three lobby tracks, the air raid alarm) are removed; builds now credit each sound's authors.

### Fixed

- Shots fly up into the ceiling and hit it; one into the floor of the level above no longer vanishes, and a near ceiling's shot is seen.
- A shot no longer starts slow and speeds up, nor jerks when fired on the run; a hitscan's tracer starts at the gun, not far ahead.
- A grenade launcher's shell leaves the muzzle instead of flying a metre above it.
- Airflow streams reach the top of a wall breach, and the air of the level above is drawn pouring down through open space.
- Wide floor pictures (jungle and pine trees, the colossus, other big creatures) stay centred on their tile from every side.
- Space ants, ashes, gibs, rubble, garbage, chemical piles and wrapping shreds lie flat on the floor and can be aimed at.
- Blood, gibs and other floor dirt that slid off their tile stay flat on the floor instead of standing up.
- Items too big for an inventory slot (the dragonator spears) are drawn shrunk inside it instead of spilling over the next slots.

## [1.7.1-pre-alpha] - 2026-11-20

### Protocol

- Network protocol 90: clients and servers older than 1.7.1-pre-alpha cannot connect to this version.

### Added

- Armory test map (`--map armory`): every working weapon, ammo, armor, suits and mechs laid out by kind, plus a shooting range.
- Bitrunning: the quantum console deploys virtual domains, netpods send you in as an avatar, caches bring reward crates and points.
- Integrated circuits: print circuits and components, wire them in the circuit editor, run them from remotes and controllers.
- PanD.E.M.I.C. vaccines cure and immunize; the slime, gluttony and gondola toxins, nanomachines, xenomicrobes, spores and Agent-S infect.
- Diseases and virology: classic and advanced diseases spread, show symptoms, mutate and are cured; the PanD.E.M.I.C. makes cultures and vaccines.
- The remaining organs (augmented eyes, special hearts, tongues, livers, stomachs and more) and the "Recovered NT Employee" corpse crate.
- Moods and sanity: a HUD face shows your mood; good and bad events shift it, and low sanity slows you and drives you insane.
- Drugs, pyrotechnics and other chemicals: highs, overdoses, view effects, thermite, fires, mutation toxins, eigenstasium and more.
- Medicines, toxins and addictions: strange reagent, sedatives, impure inverse reagents, and three-stage withdrawal.
- Drinks: drunkenness by strength and tolerance, cocktail effects, and tg's effects for coffee, juices, oils and spices.
- Lizards and felinids are playable, with species parts, languages and traits chosen on a new Character Setup page.
- MODsuit modules for engineering, medical, security, cargo, service, science and the Syndicate; station suits come with theirs.
- MODsuits: deploy and seal parts, run on a core, take modules, have their window; MODlink calls, paint kits and a PDA app.
- Skills: mining, climbing, the gym, cleaning and more level up with tg's effects and skill capes; skillchips and the Skillsoft station.

### Changed

- Chat lines over the view are half the size, with their own scale setting in Settings, Interface.

### Fixed

- Multi-z: stepping from the upper floor onto the top of the stairs no longer drops you under the floor for a moment.
- Anomaly Editor's top view shows and selects items on the floor.
- Dev panel held-things tuning: the left hand mirrors the Shift, tuning is saved per thing for everyone, with Reset and Clear.
- A body on a dragged bed, stretcher or chair moves with it smoothly.
- A lying body faces the camera; a body on a bed lies on the mattress.
- Cutting off a leg takes the blood on its foot with it.

## [1.7.0-pre-alpha] - 2026-11-19

### Protocol

- Network protocol 89: clients and servers older than 1.7.0-pre-alpha cannot connect to this version.

### Added

- The rapid part exchange device (and its bluespace version), lavaland ash flora, the elephant graveyard and the rotfrag grenade.
- More odd items: the V8 engine and House Edge, street drugs, flatpacks, piggy banks, the Hippocrates bust, the gun maintenance kit and more.
- Ruin puzzles (hologrid, light mechanism, keycard and PIN gates), the inhaler, e-cigarettes, gear beacons and other odds.
- More devices: the boombox, forcefield projector, pressure plates, quantum spin inverters, machine wand, wind turbine, implant pad and more.
- Crew guns: plasma cutters, wormhole projector, gravity gun, tesla cannon, dueling pistols, laser gatling, bows, pipe guns, foam guns, firing pins.
- Hides and sheets: tanning leather, full material recipe lists and many sheet crafting interactions.
- Anomaly Editor: a shuttle can be shown docked on the map, with a warning when it does not fit.
- The launcher shows a server's map and its mode.
- Anomaly Editor shows a loading screen with the station behind it.
- New 3D models: tram doors, controls, signs, rectifiers and spoilers, the limb grower and the stacking machine console.
- Traps and odds: landmines, magic traps, trip cables, the mystery box, voting box, fences, gun lockers, the eye chart and statues.
- Toys: toy swords, cap guns, water balloons, snap pops, talking figures, the toy nuke, toy mechs, the hourglass and more.
- Melee leftovers: spears, IEDs, chainsaws, the captain's sabre, baseball bats, the HF blade, supermatter sword and more.
- Clothing leftovers: welding visors, laser tag, animal masks, jumpsuit options, special shoes and hats, item-specific suicides.
- Boxes, bags, belts and backpacks come filled as on tg; paper sacks, the organ box, sheaths and the bag-of-holding rift work.
- Bonfires, fireplaces and the gym; mineral doors, the guillotine, the loom, the spirit board and small steps.
- The tape recorder, N-spect scanner, pillows, dice, shooting targets, department banners and more household odds.
- Assemblies: timers, sensors, infrared emitters, voice analyzers and mousetraps rig bombs; the shock kit makes an electric chair.
- Abandoned crates with a code to crack, water coolers, wall clocks and calendars; the rest of the closets and crates are filled.
- Anomaly Editor: File, Recent maps.
- The Outpost (`--map outpost`): a small four-level station built in the map editor, with its own emergency shuttle.
- Map editor: per-level base turf and the map's mine setting; a station with no mine starts sooner.
- Map editor: a new station can be made inside a maps folder and played at once with its shuttles.
- Map editor: Play can start you as a new arrival on the arrivals shuttle.
- A floor tile can be laid on the ceiling from below and pried off with a crowbar.
- Sparklers, large firecrackers and the antigravity grenade.

### Changed

- Tram walls are joined blocks again, with signs and controls hanging on them; tram doors have both glass panes.
- Exosuits rock with each step like walking mobs.
- Anomaly Editor is a separate program in the `editor` folder; the game no longer carries the editor or the server.
- The hidden interface (F6) hides name tags too, and world consoles still open their windows.
- Items in the world tilt to face the eye from above and are picked where they are drawn.
- Walking rocks and bobs about half as much.
- Explosions shake the view 40 % less.
- Moving backward and sideways is slower (49 % and 63 % of your speed).
- 40mm grenade launchers lob their grenades on an arc.
- The map editor palette and the spawn panel list types as a tree.
- Map editor: building on a block's top no longer changes the edited level.
- Map editor: with the area palette, erase and Delete remove only the area.
- Map editor: a new multi-level map starts with open space above the first level.
- Map editor: the camera stays put when the edited level changes.
- The Outpost's upper levels have open space as base turf and no mining level loads.
- The map editor is renamed Anomaly Editor.
- Anomaly Editor marks shuttle problems in red and good fits in green.
- The game shows a loading screen while connecting.
- Anomaly Editor starts in a small loading window.
- The engine's splash screen is no longer shown.
- Anomaly Editor's launchers carry its name (`AnomalyEditor.bat`, `anomaly-editor.sh`, `Anomaly Editor.app`).
- The game's loading screen has no Cancel button.

### Fixed

- Walls, doors and tables no longer flicker through windows.
- Anomaly Editor starts from a downloaded build again.
- Dragged things follow smoothly a tile behind.
- Pushing crates, lockers and canisters is smooth in every direction.
- A launched grenade leaves the gun's muzzle.
- Things pushed by air against a grille or wall stay put.
- Things blown out of a hull breach fly on into space.
- Clicking an item while stunned or cuffed no longer flashes it into your hand.
- First person: you see your reflection in windows and screens.
- Without gravity you can only push off walls and solid things, as in tg.
- Pipes reach the vents and scrubbers they feed.
- Third person: your character no longer vanishes during a delusion.
- A flipped table has the right height.
- Map editor: an emptied tile gets the level's base turf.
- Map editor: Play has working shuttles on maps of any size, also with no CentCom.
- Map editor: scripted runs continue across opening another map.
- Map editor: tools work from any distance and side.
- Map editor and game: ceilings and floors laid from below work on multi-level maps.
- A wall seen from below shows plating.
- A glass floor is one see-through pane from both sides.
- Map editor: checks flag bad ceilings.
- Map editor: after Play and Stop, wall panels and floors keep their look.
- A wall panel follows its wall when the wall is removed or built.
- The client no longer hangs at start on a sound bus race.
- Map editor: the wheel over a slider scrolls the panel only.
- A deployed server's changelog window shows the changelog.
- Shots pass through a ladder's hatch.
- Shots on stairs land where you see them.
- Shots at moving people hit where your screen showed them (lag compensation up to 0.6 s).

## [1.6.0-pre-alpha] - 2026-11-17

### Protocol

- Network protocol 87: clients and servers older than 1.6.0-pre-alpha cannot connect to this version.

- Map editor: rebindable hotkeys, multi-level tools with a level lock, layer-by-layer erase, ceiling editing, facing arrows and autosave recovery.
- The maps live in the repository and are edited with the map editor; map patch files and their server options are gone.
- Map editor: undo and redo cover the map's settings.
- Map editor: Turn (R), multi-rectangle selection and pasting into another map.
- Closets, crates and machines turned on the map keep their facing in the round.
- Map editor: preview fixes.
- Map editor (`--editor=MAP`): a full map editor running the game's own round start, with tools, palette, var editing, docking panels, Play mode, multi-level maps, map settings (sky, sun, air, weather), shuttles, ceilings, checks and package export; see docs/mapping-guide.md.

## [1.5.0-pre-alpha] - 2026-11-17

### Protocol

- Network protocol 86: clients and servers older than 1.5.0-pre-alpha cannot connect to this version.

- Tables are flipped from the context menu instead of a right click.
- Fixed: walking around a flipped table no longer jerks you back.
- The macOS client starts on Apple silicon.
- New 3D models: the DNA vault, meteor shield satellite and bluespace artillery parts; flipped tables are 3D.
- Fixed: the sun no longer lights indoor windows and door glass.
- Fixed: stairs with no level above lie flat.
- Fixed: no flickering stripes where a window meets a door, firelock or table.
- Furniture: curtains, bookcases, display and trophy cases, vend-a-trays, mannequins, tank holders, basketball hoops, plaques.
- Plants: chop trees, mine rocks, pick grass, replant with a shovel; potted plants can be carried and trimmed; the Christmas tree gives gifts.

## [1.4.0-pre-alpha] - 2026-11-16

### Protocol

- Network protocol 85: clients and servers older than 1.4.0-pre-alpha cannot connect to this version.
- Servers can join the launcher: they show in its list and can use the launcher's login.
- Admins: a forced midround ruleset asks which player becomes the antagonist.
- No more freeze when changing level on a multi-level station.
- A glass floor seen from below is see-through.
- Dev tools: noclip moves up and down; the delete tool works on floors from below.
- Digital wall clocks show the round time.
- The experimentor and strange objects from maintenance work.
- The communications console buys emergency shuttles, requests nuclear codes and the spare ID code.
- The morgue and crematorium work.
- Medbay wall machines: the DeForest first aid station, vitals displays and the medical kiosk.
- The labor camp: prisoner IDs, points goals, the teleporter, reclaimer and prisoner management console.
- Scanner gates and the hypnochair.
- Requests consoles: department messages, announcements, emergency calls with temporary access.
- The limb grower, organ harvester and lazarus injector.
- The bridge teleporter, hand teleporter and bluespace locator.
- Wall telescreens, entertainment TVs and the curator's broadcast camera.
- Security gear: the DRAGnet, the secway, pepper spray refillers and barrier grenades.
- Research: R&D servers generate points, the Techweb console researches nodes, and the master server's drive can be stolen.
- Fabricators: protolathes, circuit imprinters, techfabs and the autolathe print with materials from the ore silo.
- Experiments: experi-scanners, the destructive scanner and analyzer, and autopsy experiments.
- Fixed: no "ObjectDisposedException ... ShuttleFx" disconnect at round restart.
- Fixed: walls and floors are sharp on Intel graphics under Linux.
- The multi-z debug map has five space levels below and above.
- Falling through a hole takes time and hurts on landing, by tg's damage.
- Walking people rock and bob with their steps.
- Burning gas looks like a glowing cloud filling the tile, with tg's hot-fire extras.
- Fixed: the server no longer runs out of memory in the second round.
- Unported guns and containers say "not ported yet" instead of erroring.
- MultiZ Debug loads CentCom and Lavaland whole, so shuttles dock.
- Fixed: lavaland fauna no longer spawns on lava.
- Fixed: several server errors (null rod whip, struggling out of a locker, camera view, ghost teleport).
- The AI upload's custom law board and photocopier forms ship with dedicated servers; laws are set in `config/silicon_laws.txt`.
- The kitchen works: cutting, dough, oven, stove, griddle, grill, microwave, fryer, processor, gibber, smart fridges and custom food.
- The janitor: mop bucket, janitorial cart, janicart with upgrades, the advanced mop and wet floor signs.
- Records consoles: medical and security records, crimes, fines, wanted status and posters.
- A changeling's arm blade slashes computers.
- The bar: drink glasses, bottles, champagne, pruno, molotovs, the bar sign, the jukebox and the dance machine.
- Washing your face also washes your glasses and mask.
- Changelings: Dynamic rolls them; chemicals, hivemind, the Cellular Emporium with over thirty abilities, absorbing and transforming.
- Washing and household: sinks, toilets, urinals, showers, washing machines, mirrors (and their bad luck), dressers and the recycler.
- Xenomorphs: infestations, facehuggers, larvae and all castes up to the queen, with their hivemind and abilities.
- Commendations: at round end players can anonymously thank someone, who gets a heart.
- The station clock uses the real date (`--no-real-date` to turn off).
- The revolution: head revolutionaries convert with flashes; win or lose by the heads of staff.
- Holidays: festive names, decorations, hats and holiday events (Easter, Halloween, Valentine's, Christmas); off by default on servers.
- Station traits: tg's good, neutral and bad round traits, with lobby sign-ups and admin controls.
- Brain traumas: mild, severe and special traumas with tg's effects and cures.
- Station goals: the bluespace artillery, DNA vault and meteor shield.
- The alert level opens marked doors and defib mounts on red and shows on fire alarms.
- Space vines (kudzu) grow, mutate and tangle people.
- More random events: carp migration, mouse migration, the wise cow, heart attacks, fake virus, mass hallucination, sentient pets.
- Cargo and shuttle events: stray cargo pods, the shuttle loan, shuttle insurance and catastrophe.
- Corpse spawners leave real bodies.
- Meteors, space dust, sandstorms, the immovable rod and the dark matt-eor.
- More events: radiation leaks, wormholes, the aurora caelus, portal storms, tram malfunctions, earthquakes.
- Syndicate, Nanotrasen, pirate, Russian and abductor troopers as enemies.

## [1.3.0-pre-alpha] - 2026-11-15

### Protocol

- Network protocol 84: clients and servers older than 1.3.0-pre-alpha cannot connect to this version.
- Falling through open space hurts on landing, not at the start.
- Holopad holograms move smoothly with their eye.
- Third person: a dim room under hidden decks no longer lights up from the sun.
- Cyborgs can equip their tools from the module panel.
- The janitor cyborg's cleaner box works.
- Weather: radiation storms, the weather anomaly and ash, rain, sand and snow storms, with haze and particles.

## [1.2.0-pre-alpha] - 2026-11-14
- Random events are on, every 2.5 to 7 minutes.
- Admins can switch the next round's map with `changemap`.
- A descending elevator no longer shows warning signs inside.
- Late joiners can be cyborgs (up to 3).
- You can reach things on the floor's edge above from below a hole or the stairs.
- The AI's eye flies freely; in cursor mode the AI clicks things under the pointer.
- The AI has a Toggle Meson Vision button.
- HUD buttons have tooltips in cursor mode.
- Items off the middle of their tile no longer sink into floors and tables.
- Fixed: machines and furniture are no longer black and white on weaker GPUs.
- F12 toggles fullscreen with any window open.
- Settings: Minimap text size.
- Flying up or down a level is quicker and no longer jerks the view.
- Your name no longer hangs inside your head after revival in third person.
- Trams: consoles open their windows, no sinking through the floor, no stutter on arrival.
- Trams: 3D spoilers and 3D holographic barriers; cyborgs press call buttons from afar.
- Fewer mispredictions on a bad connection or busy server.
- Vending machine cards no longer overlap price and name.
- Mid-round action buttons appear at once.
- The aux base construction console works.
- A ladder with no other deck lies flat.
- Fewer wrong reflections in window glass.

## [1.1.29-pre-alpha] - 2026-11-14

### Protocol

- Network protocol 83: clients and servers older than 1.1.29-pre-alpha cannot connect to this version.
- Space and Ctrl move flyers, ghosts and the AI eye up and down; pulling moved to H.
- The round-end report lists the medals awarded.
- More random events: electrical storms, grid check, comms blackout, gravity outage, supermatter surge, grey tide, rogue vendors and more.
- Anomalies and vent clogs as random events; ghosts get tg's alerts.
- Languages work as in tg, with the language menu.
- Leaning against walls, flipping tables and placing or slamming people on them.
- Newscasters, newspapers and the bounty board.
- The rest of cargo: express console, materials market, produce and mining consoles, department orders, the black market.

## [1.1.28-pre-alpha] - 2026-11-13

### Protocol

- Network protocol 82: clients and servers older than 1.1.28-pre-alpha cannot connect to this version.
- Q puts the held item down where the crosshair points.
- Thrown people fly the same arcs as thrown things.

## [1.1.27-pre-alpha] - 2026-11-13

### Protocol

- Network protocol 81: clients and servers older than 1.1.27-pre-alpha cannot connect to this version.
- Thrown things fly by physics: real arcs to the crosshair, landing where they touch down, falling through open space.
- R throws the held item at the crosshair at once.
- The dev spawn tool hangs wall furniture on the clicked wall face.
- Third person: elevator lanterns on hidden decks no longer float.
- The dev spawn tool builds ceilings on any deck.
- The elevator call button's lights show on its 3D model.
- multiz_debug: elevator floors are named right and the lantern counts from 1.
- The elevator hall lantern is a 3D box.
- multiz_debug: the upper decks vent into space sideways.
- A player blown out of a breach flies on into space.
- Third person: decks above yours are not drawn.
- A held gun's number shows shots left and capacity (2/6).
- Shots at stairs no longer jump a level; the revolver's count goes down as you fire.
- Ricochets bounce off floors and ceilings, and their tracers bend.
- Ladder hatches are wider (1.2 x 0.9 m).
- Guns shoot where you aim, up, down and point-blank; tracers reach the hit.
- Dropped items show in mirrors.
- Mirrors reflect the room and your own body.
- Glass floors are drawn as glass.
- PDA and console job apps work as in tg (ID writer, records, power monitor, NTNRC chat, arcade, viruses and more).
- The Virtual Pet app and Mafia (from the ghost Minigames button and a PDA app).
- Genetics: the DNA scanner and console, all of tg's mutations, genetic damage and meltdowns.
- Character Setup opens again.
- The AI can be picked on the Occupations page and as a late joiner.
- The DNA infuser grows animal organs with set bonuses.
- The station economy: bank accounts, paychecks, budgets, cash, holochips and NT Pay; vending and fines charge again.
- Cargo with the economy: priced orders, private buys, goody packs, exports, mail and shopping apps.
- Civilian bounties, station bounties and security patrol bounties.

## [1.1.26-pre-alpha] - 2026-11-13

### Protocol

- Network protocol 80: clients and servers older than 1.1.26-pre-alpha cannot connect to this version.

- Glass no longer takes shadows and reflects what stands beside it.
- Glass is smoothly blended instead of dithered.
- Polished and wet floors faintly reflect the room (Graphics: "Floor reflections").
- Full-auto guns fire through open space to another deck.
- In cursor mode a plain click on the world does nothing.
- Shot trails fade twice as fast.
- Tramstation's tram runs: call it, ride it smoothly, it stops at platforms and hits people on the rails (dev: `tram`).
- Ambient occlusion no longer darkens glass, glows and the floor around thin things.
- Multi-deck pipe adapters and cable hubs show which way their line runs.
- Projectiles fly evenly, leave along your aim on the run, and are sized by calibre.
- A gun shows its shots left on its icon.
- More full-auto guns: Saber SMG, Thompson, L6 SAW, S-120 laser, disabler SMG; the SAW's dust cover.
- Recoil snaps up and settles back, adding up over a burst.
- A hit flashes the struck body part red and jolts the body.
- Every projectile draws a glowing trail; hits show a flash, sparks and a lasting mark.
- Walking over open space above a window keeps you on it.
- Ladders: left click climbs up, right click down.
- Ladders are solid and climbing is animated.
- A ladder's hole is a framed hatch beside it, not the whole tile.
- Text on things and the HUD is placed as in tg.
- Models no longer speckle; ambient occlusion grain is smoothed.
- Ambient occlusion shades corners and under furniture.
- The gravity generator model has no holes.
- Multi-deck disposal trunks have new models.
- Elevator indicators show their floor number.
- The multi-z cable hub and pipe adapter have 3D models.
- Stepped full-tile windows are framed on the inner corner.
- Multi-deck pipe adapters are 3D risers that join through the floor.
- The RTG has a 3D model.
- Dev menu: Tab returns control to the game while the menu stays open.
- The free camera works on the level it flies in.
- Flying: PageUp and PageDown change level (dev: `up`, `down`).
- Walking on a moving elevator no longer drops you under it.
- A downward ladder has an open hole and takes clicks from every side.
- Full-tile windows and walls are framed at every corner.
- Multi-deck things are 3D models: ladders, disposal risers, elevator parts, tram rails, subfloor and walls.
- Walls keep their frame on the outer corner of an L or T.
- Dev menu: Ctrl + click picks a type for Spawn.
- Walls are framed where they meet what they do not join.
- Dev menu: placement fixes on the top level and replacing walls with windows.
- Multi-z: hover, clicks and dev tools aim at the camera's level in third person.
- Dev menu: a window stuck on another window joins it into one pane.
- Dev menu: Turfs tab lists windows; each tab has favourites.
- Dev menu: spawning on a wall's top places on the level above; new Stick option.
- Multi-z: wall tops are flush, no seams from above.
- Multi-z: a wall over open space has a solid underside.
- Multi-z: the server takes clicks on other levels of your stack.
- Multi-z: climbing stairs against a railing is no longer corrected; no slipping past railings.
- Multi-z: stacked walls and windows join into one surface.
- Malfunctioning AIs: the zeroth law, the Malfunction Modules shop (lockdown, overrides, robotic factory, Doomsday Device) and APC hacking.
- Fixed: AI camera static could leave chunks stale.
- Fixed: crashes of lavaland butterflies and the manufacturing lathe.
- Maintenance drones as a ghost role, with tools, laws and drone chat; the ghost Spawners Menu.
- Androids (admin or mirror only): robotic limbs, a cybernetic brain, no breathing or hunger.
- Fixed: a species change swaps the organs.
- Fixed: HUD slots no longer stay blank after changing bodies.
- Ion storms scramble AI laws.
- Syndicate cyborgs from the nuclear operatives' beacons.

- Musical instruments: tg's instrument editor, songs, synthesized and recorded instruments (MIDI-based ones are silent for now).

- Personal AIs (pAIs): ghost sign-up, the card's window, software, holographic chassis, door jack; a new ghost menu.

## [1.1.25-pre-alpha] - 2026-11-12

### Protocol

- Network protocol 79: clients and servers older than 1.1.25-pre-alpha cannot connect to this version.

- Painting: canvases with a painting window and tools, frames, the library's archived exhibits and the AI's portrait.

## [1.1.24-pre-alpha] - 2026-11-12

### Protocol

- Network protocol 78: clients and servers older than 1.1.24-pre-alpha cannot connect to this version.

- The AI: camera eye and static, remote control of machines, tracking, multicamera, holopads, VOX, cyborg control, intelliCards.
- The exosuit fabricator prints cyborg parts, cells and cybernetic organs from the ore silo; starter research.
- Cyborgs: models with modules, a HUD, cells and chargers, maintenance and lockdown, emags; their items work; statues and engraving.
- Acid melts items, machines, floors, walls and clothes, and burns skin.
- The captain-caster broadcasts the captain to the status displays.
- Brig cell timers and flashers.
- AI laws and the module rack: law modules, 26 core lawsets, ion and hacked laws, State Laws, binary chat; the suicide verb.
- The bananium nuke clowns the station; phobias work.
- Clown operatives and their uplink items.
- Nuclear operative reinforcements: the operative and overwatch beacons, ED-209, assault pod targeting, codespeak manuals.
- Nuclear operative kits: C-4 and X-4 bags, grenade boxes, the outlaw's bundle, pinatas, gum and lollipops.
- Nuclear operative guns: C-20r, smartgun, M-90gl, L6 SAW, Bulldog, sniper rifle, rocket launcher; syndicate firing pins.
- The infiltrator's navigation computer picks a landing spot.
- The war declaration.
- Random events start: the lone operative and scrubber overflows; Dynamic can bring midround operatives.
- Nuclear operatives: the syndicate base, the infiltrator, tg's kit and the round's result.
- Pressure tanks merge, leak and burst.
- NTOS radar apps: Fission360, Lifeline and the Custodial Locator.
- Foam darts.
- F6 (`ui hide`) hides the whole interface.
- The nuke: the self-destruct terminal, the disk, the countdown, the station-destroyed cinematic and core theft.
- The contractor's support unit and fulton kit.
- The contractor: tablet, kidnapping contracts, extraction pods and payouts.
- Martial arts: Close Quarters Cooking, boxing, Kaza Ruk, Plasma Fist, Spider's Bite, Mushroom Punch and more.
- The Sleeping Carp; silence works.
- Chameleon thermals, thermal and x-ray vision, the chameleon projector.
- The EMP flashlight, polarized contacts, smuggler's satchel, AI detector and fake identity kit.
- The chameleon kits, agent card and no-slip shoes.
- The emag and the doorjack.
- Turrets: the traitor's sentry toolbox, AI satellite turrets and turret controls.
- Syndicate job items: His Grace, the monkey agent; supply pods fall and land.
- Syndicate job guns: meat hook, reagent gun, pie cannon, blast cannon, rebar crossbow; tank transfer valves.
- Syndicate job items: the haunted eightball, mail counterfeit device, clown items, DNA syringe gun, ancient toolbox, combat bakery kit; crew mail.
- Card decks, the Detomatix, F.R.A.M.E. and SyndEye disks; computer disks.
- Seditious posters (and all posters), the pony box, cardboard cutouts, the sleepytime kit, plushes and bedsheets.
- The RPD lays disposal pipes; monkeys stuff enemies into disposals.
- Admin logs: package wrapping is logged.
- Mail: package wrapping, destination tagging and sorting pipes.
- The disposal bin's lever and lamps.
- Elevator floor indicators show the direction again.
- The elevator call button is clickable; the platform glides.
- Multi-z: lower walls no longer show through the floor above.
- Disposals work, across decks too.
## [1.1.23-pre-alpha] - 2026-11-09

### Protocol

- Network protocol 77: clients and servers older than 1.1.23-pre-alpha cannot connect to this version.

- Multi-z: 3D stairs and climbable ladders.
- Multi-z: elevators with call buttons, panels, shaft doors and crushing.
- Multi-z: level changes glide instead of jumping.
## [1.1.22-pre-alpha] - 2026-11-09

### Protocol

- Network protocol 76: clients and servers older than 1.1.22-pre-alpha cannot connect to this version.

- Multi-z (`multiz_debug`): open space you fall through, with tg's fall damage; catwalks hold.
- Multi-z: other levels are drawn and lit through open space.
- Conveyor switches stand on the floor.
- New players start from tuned default settings.
- Faxes have the same size whatever is loaded.
- A bare grille shows its bars on top.
- The emitter has no see-through patches.
- Bedsheets and the codex gigas are 3D.
- New and revised models for many machines and furniture.
## [1.1.21-pre-alpha] - 2026-11-08

### Protocol

- Network protocol 75: clients and servers older than 1.1.21-pre-alpha cannot connect to this version.

- Cryo cells no longer stay flat; tank holders are 3D and show their contents.
- The dev menu opens only for admins when dev commands are off.
- Admins keep their rights in any job or antagonist role.
- A Linux x64 client (needs Vulkan).

## [1.1.20-pre-alpha] - 2026-11-08

- Desk buttons and the newsfeed TV sit on their desks.
- Soil plots stand on the floor; puddles lie flat.
- Hydroponics trays are waist-high; the wooden barricade is a block.
- The Ripley wreckage and quartermaster's statue are sprites again; steam vents lie flat.
- Votes: a Vote button with tg's vote window; restart and custom votes.
- The emergency shuttle is called automatically when four fifths of the crew are dead.
- The anti-breach shield projector is solid again.
- Revised models for mining and science machines, with their animations.
- An empty fax keeps all its faces.
- Settings tabs are regrouped as in most games.
- In an exosuit, first person shows the arms' equipment.
- A mech you left is drawn again.
- A client reads its server from `server.txt` beside the exe.

## [1.1.19-pre-alpha] - 2026-11-07

### Protocol

- Network protocol 74: clients and servers older than 1.1.19-pre-alpha cannot connect to this version.

- Status displays work; world maptext is drawn in 3D.
- A singularity eats the ceiling too.
- A supermatter at 0 % delaminates again.
- Revised models for chargers, rechargers and atmos machines.

## [1.1.18-pre-alpha] - 2026-11-07

### Protocol

- Network protocol 73: clients and servers older than 1.1.18-pre-alpha cannot connect to this version.

- Airtight plastic flaps block like a wall; crawl under them.
- Exosuits: Climb in on the radial menu.
- Character Setup's preview shows again.
- Exosuits: the cockpit, mech bay power and exosuit control windows.

## [1.1.17-pre-alpha] - 2026-11-07

### Protocol

- Network protocol 72: clients and servers older than 1.1.17-pre-alpha cannot connect to this version.

- Exosuits: all of tg's mechs with their tools and weapons; smooth walking, power use, doors, cockpit view.
- tgui labels in rows show (NT CIMS, NT Software Hub, NtOS clock).
- The NT Software Hub lists programs vertically; sideways scroll is kept.
- Settings: separate Chat scale.
- Poly speaks lower and slower; parrots no longer squeak.
- NtOS program windows can be dragged and resized.
- NtOS header buttons are icons again.
- Space wind and gravity pulls are smooth glides; you can lean against weak wind.
- Unported PDA programs keep the NtOS header.
- Dev menu: a Tools tab with per-item held offset.
- Dev menu: map models can be scaled and moved live.
- Poly talks slower.
- Delamination counters count shifts and survive restarts.
- Walking into rock no longer digs; cracks show on every face.
- T-ray shows and lets you remove pipes and meters in walls.
- MetaStation: Assistants no longer spawn trapped on cargo's conveyor.
- Sofa and bench armrests are on the right ends.

## [1.1.16-pre-alpha] - 2026-11-06

### Protocol

- Network protocol 71: clients and servers older than 1.1.16-pre-alpha cannot connect to this version.

- The RPD ghost shows only where a click would build.
- A saved turn on a random spawner holds for any rolled thing.
- Revised engineering and medical machine models with their animations.

## [1.1.15-pre-alpha] - 2026-11-06

### Protocol

- Network protocol 70: clients and servers older than 1.1.15-pre-alpha cannot connect to this version.

- The RPD: one button per pipe, Shift + wheel turns, wheel changes layer, and a ghost preview.
- Chat translator (offline, off by default): translated chat, overheads and TTS.
- Admins: the Secrets panel (`secrets`).
- The dead-chat singularity and the small supermatter crystal.
- Clicks reach models behind a shut windoor on their tile.
- New power gear: floodlights, portable SMES, prototype emitter, dark matter singularity, reality tear.
- Mining is held: hold the button to keep digging.
- Dev menu: player markers through walls.
- Page windows close again.
- Tab leaves cursor mode after a click on a page window.
- Dragging from or onto a ceiling no longer errors.
- Conveyor belts visibly run, with proper corners.
- Conveyor belts no longer outrun you.
- Window tooltips stay up and wrap.
- Scrolled lists keep their position on update.
- Syndicate odds: balloon, stickers, cash briefcase, spraycan rune, soap, tome, lipstick, catnades; cash, soap and lipstick work anywhere.
- Syndicate implants and kits.
- Blood brothers.
- Cybernetic implants: arm toolsets, nutriment pumps, reviver, thrusters and more.
- Chemistry: dispensers, ChemMaster, reaction chamber and other machines, and the reactions.

## [1.1.14-pre-alpha] - 2026-11-04

### Protocol

- Network protocol 69: clients and servers older than 1.1.14-pre-alpha cannot connect to this version.
- Conveyor belts and switches work as in tg, carrying you smoothly.
- Revised models for chemistry, drink, cryo, DNA, BCI and fabrication machines; the gravity generator shows its states and charge field.
- The minimap can be dragged to look around and moved by its edge.
- Holoparasites: eleven guardian types played by ghosts; ghosts get tg's role poll alerts.
- Dynamic runs the round: threat tiers, roundstart, latejoin and midround traitors, the lobby's Antagonists tab, admin panels.
- Traitors: objectives, code words, the hidden uplink with tg's Uplink window, and shuttle hijacking.
- The Earthcracker's bang is heard.
- Traitor weapons and explosives: energy swords, power fist, grenades, the syndicate bomb, power sink, syndicate teleporter and more.
- Antagonists: admin-made custom antagonists with objectives; Check Antagonists and the round-end report.
- The minimap is twice as large by default and scales with the screen.
- Wall things no longer get dragged by a tiny mouse move during a click.
- Lobby buttons and the chat window scale with the screen; the chat is twice as large.
- Chem smoke and foam: foam, resin, the smoke machine, chemical grenades, clusterbusters and the foam and smoke reactions.
- Botany: trays, soil, watering, weeds and pests, harvest, mutation and grafting, seed extractor, biogenerator, beekeeping.
- Surgery as in tg on the operating table, with the operating computer and surgery disks.
- Autopsies print the scanner's report.
- Paperwork: writing, stamps, folders, filing cabinets, notice boards, the photocopier and faxes.
## [1.1.13-pre-alpha] - 2026-11-03

### Protocol

- Network protocol 68: clients and servers older than 1.1.13-pre-alpha cannot connect to this version.

- Dev menu: the minimap can show the creatures of your level.
## [1.1.12-pre-alpha] - 2026-11-03

### Protocol

- Network protocol 67: clients and servers older than 1.1.12-pre-alpha cannot connect to this version.

- A minimap of your level with department colours and labels; drag, zoom and hover for room names.
- Poly speaks with her voice again; parrots repeat in the speaker's voice.
- Wet floors are visible.
- A bare grille has no top again.
## [1.1.11-pre-alpha] - 2026-11-03

### Protocol

- Network protocol 66: clients and servers older than 1.1.11-pre-alpha cannot connect to this version.

- Running accelerates and decelerates; backward and sideways movement is slower.
- Clicking an intercom no longer flashes it in your hand.
- A camera beside a window hangs on it.
- The third-person camera no longer clips into walls.
- Settings: a speech to text language.

## [1.1.10-pre-alpha] - 2026-11-03

### Protocol

- Network protocol 65: clients and servers older than 1.1.10-pre-alpha cannot connect to this version.

- The third-person camera no longer clips into walls.
- Whispers sound breathy and quiet; shouts sound strained and louder.
- Speech to text writes only in the picked languages.
- Character Setup: voice pitch arrows and a radio voice preview.
- The lobby music respects the volume settings from the start.
- Radio voices sound like a walkie-talkie.
- Settings, Speech: Test voice and Test over the radio.
- Voices are offered by the character's gender; pitch goes from -5 to 5.
- Speech to text while typing: hold Alt or the chat's Mic button.
- More things speak with a voice: vending machines, computers, parrots.
- Voices fade in thin air and are silent in space; deaf characters hear none.
- Text to speech starts faster.
- Settings, Speech can delete downloaded speech models.

## [1.1.9-pre-alpha] - 2026-11-03

### Protocol

- Network protocol 64: clients and servers older than 1.1.9-pre-alpha cannot connect to this version.

- Text to speech voices the radio, with channel and volume settings.
- Voices sound like their source: machines, gas masks, megaphones, silicons.
## [1.1.8-pre-alpha] - 2026-11-03

### Protocol

- Network protocol 63: clients and servers older than 1.1.8-pre-alpha cannot connect to this version.

- Text to speech (off by default): characters speak in their chosen voice from where they stand.
- Speech to text (off by default): hold N and speak into the chat input.
- The screen between rounds only says the server is restarting.
- Hyperspace no longer shows other ships through the tunnel.
- Shuttle engine heaters have their floor and ceiling again.
- The login screen and title say Anomaly Station.
- The dev console's suggestions list every match.
- Boulders: Lavaland's ore vent, the boulder retrieval matrix, the smelter and refinery.
- Mining machines: stacking machine, furnace, unloader and labour camp points.
## [1.1.7-pre-alpha] - 2026-11-02

### Protocol

- Network protocol 62: clients and servers older than 1.1.7-pre-alpha cannot connect to this version.

- The Windows console launcher has the game's icon.
- At round end you stay connected in a waiting lobby with OOC chat.
- Dev: `esp` lets the living see ghosts.
- Dev: `reboot now` restarts at once.
- Dev: `shuttle dock` docks the emergency shuttle at once.
- The dev menu's Player tab works for ghosts.
- Flames taper off at the top.
- Dev fly ignores the air's push.
- Dev godmode blocks all status effects; a new Invulnerable switch (`invuln`).
## [1.1.6-pre-alpha] - 2026-11-02

### Protocol

- Network protocol 61: clients and servers older than 1.1.6-pre-alpha cannot connect to this version.

- Noclip no longer climbs onto tables and crates.
- The dev menu's "tp to me" works; no ImGui ID error.
- The ore redemption machine, ore silo and ore boxes work.

## [1.1.5-pre-alpha] - 2026-11-02

### Protocol

- Network protocol 60: clients and servers older than 1.1.5-pre-alpha cannot connect to this version.

- Lavaland has its own dark ash sky.
- Lavaland's boss arenas have stone tiles; water has a shoreline.
- Rock walls have no see-through strips.
- Lavaland's rock holds ore veins and gibtonite.
- Hair gradients turn with the head.
- Lavaland: generated caves and lava rivers, lava and chasms, reached by the mining and labour shuttles.
- The lobby music plays track after track.
- Smoke clouds spread, block sight and make you cough.
- Explosions are pixel fire and smoke.
- C-4 sticks to any face of any object.
- Explosions burst over their radius; screen shake is halved.
- Fire is jagged pixel flames and costs less frame time.
- Top-right HUD text is translucent, with an opacity slider.
- Window and chat buttons follow window opacity; lower title bars.
- HUD hand icons are held up.
- No more "too many messages" disconnects.
- A docked mining or labour shuttle no longer shows hyperspace.
- Shuttle landings and departures: hyperspace ripples, blue light streaks, real engines with jets and hum.
- Shuttle engine blocks lose their stray floor and ceiling.
- Flying shuttles' engines show jets; departures streak away.
- The lobby's title1 track plays.
- The lobby has an Exit button.
- The lobby's start button is a plain plate.
- The game has its own icon.
- CentCom's full map is loaded; shuttles dock there.
- Emergency shuttle flights have turbulence, carp and meteors.
- Cargo: supply and request consoles and the cargo shuttle (free orders for now).
- Mining, labour and ferry shuttle consoles work.
- Escape pods launch on red alert; the safe holds the emergency kit.
### Added
- The arrivals shuttle brings late joiners in from hyperspace.
- The emergency shuttle console: early launch with three IDs, emag entry.
- Shuttles fly through a blue hyperspace tunnel; falling off the hull drifts you away.
- Security and detective gear: forensic scanner, evidence bags, keycard device, tackling gloves, riot shields, flashbangs and more.
- Medical, science and engineering gear: surgical tools, body bags, crew monitor, jaws of life, firepack and more.
- Mining and cargo gear: kinetic accelerator and modkits, shovels, satchels, scanners, gibtonite and more.

### Fixed
- The emergency shuttle keeps its air when it flies.
- A flying shuttle keeps its windows, doors, fittings and decals.
- Air streams through ceilings, floors and walls are shorter; new airflow defaults.
- Every creature turns as you walk around it.
- Monkeys are their proper size again.
- Bot control panels respond to clicks.

## [1.1.4-pre-alpha] - 2026-10-31

### Protocol

- Network protocol 59: clients and servers older than 1.1.4-pre-alpha cannot connect to this version.

### Added
- Every job comes with its full kit, including the clown's and the mime's.
- Ruin creatures behave as in tg (admin spawn for now).
- Meta's monkeys and Pun Pun are alive; monkey cubes, monkeyize and humanize work.
- Vermin roam maintenance; farm animals, carp, bears and snakes; butchering and riding.
- Bots: cleanbots, medibots, MULEs, secbots and others, buildable, each with its window.
- Meta's pets are alive, tameable, take commands and remember between rounds.
- Smoking, crayons and spray cans, laser pointers, whetstones, the captain's charter, cameras and the heroic beacon.
- Hostile creatures have tg's AI.
- Job clothing toggles, hoods, ties, boot knives, medals and prisoner shoes work.
- Area ambience, the ship's hum and phantom noises in dark tunnels.
- Rooms echo by their size.
- Settings: an Audio tab.
- The character menu's Job Clothes checkbox works in the round.
- Picked colours preview at once.
- I opens the inventory.
- Settings > Audio: six volume sliders.
- The AI's foam dispenser is a 3D floor plate.
- Combat mode, winter coat and severed limb sounds.
- Settings: a Look tab with post-processing options and new defaults.
- Dev: the mount tool moves wall mounts.
- Dev menu: player list with teleports and a personal speed slider.
- Settings > Graphics: fullscreen resolution.
- A Linux server build for a VPS (`tools/deploy-server.sh`, docs/hosting.md), admins from `config/admins.txt`.
- Admins with the debug right see the gas overlay.

### Changed
- Radiation on screen is a flickering film grain.
- The radial menu scales with the window.
- The chat sits at the bottom left; the changelog window is titled Anomaly Station.
- The character preview has the Job Clothes checkbox.
- Meson scanners reach 11 tiles.
- Footsteps follow distance walked, with an even rhythm.

### Fixed
- Pointing puts the arrow exactly on the target and keeps it a constant size.
- The security HUD job icon sits at the top right.
- The T-ray scanner shows cables and pipes behind walls.
- Saved turns hold for randomly spawned things.
- Many loud sounds no longer crackle.
- A knocked-down person falls with a thud and drops what they hold.
- Area hum and ambience start on join and loop cleanly.
- The tunnels' hum loops without dips; footsteps no longer mute it.
- Unported consoles show a lit screen and go dark without power.
- No more multi-second freezes when entering unloaded parts of the station.

## [1.1.3-pre-alpha] - 2026-10-30

### Protocol

- Network protocol 58: clients and servers older than 1.1.3-pre-alpha cannot connect to this version.

### Changed
- The dev spawn tool builds ceilings; the Ceilings tab is gone.
- Looking up through a hole with rods or a tile aims at the hole.
- Lattices and catwalks in the ceiling wear down and drop their rods.
- Fewer stalls after teleports: the server streams nearby things first.
- First-person melee is a proper swing.
- The Makarov is drawn smaller; the dev menu sizes held things.

### Fixed
- Machines and furniture no longer stay flat while their model loads.
- Fire and boarding axes are held blade away.

## [1.1.2-pre-alpha] - 2026-10-30

### Protocol

- Network protocol 57: clients and servers older than 1.1.2-pre-alpha cannot connect to this version.

### Added
- The PACMAN generator has a revised 3D model.
- Computers and consoles stand taller.
- I toggles the inventory; number keys 1 to 9 use the item buttons.

### Changed
- Ceilings are built like floors, from lattice to finished floor.
- The dev spawn tool places floors, lattices and catwalks on ceilings.
- The bug report attaches only the menu's screenshot.
- The radial menu and emote wheel are larger, with icons and an opacity slider.

### Fixed

- No more crash after switching camera modes near lamps.
- No dark stripes on walls lit at a slant.
- Explosions no longer tear the ceiling far beyond the floor; shots no longer wear ceilings.

## [1.1.1-pre-alpha] - 2026-10-29

### Protocol

- Network protocol 56: clients and servers older than 1.1.1-pre-alpha cannot connect to this version.

### Added

- Screen effects for burning, heat, cold, toxins, radiation, shocks and hits.
- Settings: windows and chat opacity.
- Machine windows open centred on screen.
- Leaving the chat and windows is quick (Tab, Esc, a click outside); Shift+Esc closes all.
- The chat input stays active after sending.

- Gas filters and mixers have their windows.
- Portable pumps, scrubbers and tank regulators have their windows.
- NtOS department consoles; NT CIMS supermatter monitor; ghosts can view machine windows; delamination counters.
- Atmospherics control consoles and air sensors.
- The gravity generator's control window.
- Reinforced engine floors behave as in tg.
- Field generators and containment fields.
- Incinerator airlocks, ignition switches and access buttons.
- Station and atmospheric alert consoles, Canary, and the power flow control console.
- Space heaters and cell chargers.
- The incinerator's gas turbine, thermoelectric generators and RTGs.
- Shield generators: anti-breach, shield walls, atmos holofields and the modular shield.
- The crystallizer and portable electrolyzer.
- The RCD family: RCD, RLD, RTD and the decoration device.
- The hypertorus fusion reactor.
- Station blueprints, the airlock painter and the decal painter.
- The big manipulator and manufacturing machines; the portable gravity unit.
- Examine texts for machines as in tg.
- Examine texts for structures, floors, items and weapons.
- Plumbing: ducts, tanks and the plumbing constructor's machines.
- Examine texts for clothing, limbs and more.
- IV drip, combination safe and loot panel windows.
- Special floors and walls: tiles, grass, light floors, reinforced plating, rock.
- Memories and the Memory Panel.
- MetaStation round-start setup as in tg.
- Eating and drinking, hunger, fatness and vomit.
- Vending machines: browse, take (free for now), restock, hack, and get crushed.

### Changed

- About 50 % higher frame rate with lamp shadows on: shadows redraw only when something nearby moves.
- Fewer stalls while walking; joining Meta is about 2 seconds faster.
- Joining MetaStation is about 7 seconds faster.
- The P.A.C.M.A.N. generator has a new 3D model.
- Hits throw debris by material; impact sparks take the shot's colour.
- Held guns are twice as big again.
- Unconscious or dead, you can look round freely; your body stays as it fell.
- The T-ray scanner blinks again and follows you at once.
- Engineering goggles' views follow you at once.
- Meson goggles show solid green walls through third-person darkness.
- The gravity generator no longer draws a flat field ring.
- Real dynamic lights with shadows replace per-tile light; sun, stars and the planet's glow light what is open to space.
- Your flashlight shines from your hand and sways as you move.
- Shadow priority: fire and shots first, then carried lights, then lamps, visible ones first.
- The default light is darker so shadows show.
- High and max keep object shadows farther; your body casts a shadow in first person.
- Others' flashlights shine from their hand, with lens glare.
- Lit flashlights and lamps glow in hand and are held lens forward.
- Graphics presets (low to max) plus individual settings.

### Fixed

- Pushing things no longer jerks the view: the push is predicted.
- The gravity generator no longer shows its flat picture beside the model.
- Vents and scrubbers show their animated lights and proper outline.
- Thin windows are framed as one pane.
- No strip of sunlight under dark ceilings.
- Light no longer passes through shut airlocks and blast doors.
- Small lamps no longer shine through walls and shut doors.
- Slipping on a wet floor names the floor again.
- Slurred speech no longer fails on missing text files.
- Telecomms monitors show their values again.
- Unported examine texts fall back to name and description.

## [1.1.0-pre-alpha] - 2026-10-28

### Protocol

- Network protocol 55: clients and servers older than 1.1.0-pre-alpha cannot connect to this version.

### Added

- Admin Help as in tg: tickets, replies, `tickets` and `pm KEY`; with no admin online it goes to a Discord webhook (`ADMINHELP_WEBHOOK_URL`).
- Glass-material things shatter when thrown.
- Cold water vapour freezes floors into permafrost, freezes items and ice-cubes cold people.

### Changed

- Shuttle engines glow blue inside the nozzle.
- New 3D models: portable scrubber, intercom, bounty pad, disposal outlet, order consoles, arcade cabinet, ore box, mass driver; some remodels.
- The delivery chute is 3D; the gravity generator is 2 m tall with lit corner modules and keeps its model when broken.
- The grand piano and slot machine are 3D; consoles and frames are 1.5 m tall.
- Vents and scrubbers are thin 3D floor slabs again, one per state.
- Default light is a little dimmer.
- In the round, the Esc menu's Character is view-only.
- The Esc menu in the round has no Change Log.
- Buttons are framed like buttons.
- The colour picker has no screen sampler.

### Fixed

- Bullet tracers are visible.
- Characters and preferences survive server restarts (`data/player_saves`).
- The character page keeps its scroll position.
- The character preview turns at once.
- Duffel bags zip and unzip as in tg.
- Observers from the lobby are ghosts with their character's hair.

## [1.0.6-pre-alpha] - 2026-10-28

### Protocol

- Network protocol 54: clients and servers older than 1.0.6-pre-alpha cannot connect to this version.

### Added

- Settings, Developer: "Port status over things" shows how much of each object is ported (checkout only).
- People have tg's hair, beards, gradients, underwear and eyes; hats hide hair.
- People blink.
- Characters have tg's look preferences, saved and applied at spawn.
- The Character page has picture grids for looks and a colour picker.
- The Character page previews the character in their top job's outfit.
- tg's randomization buttons on the Character page.
- First person shows the held things in both hands as 3D, with swings, recoil, reloads and effects.
- Bullets leave a short fading trace.

### Changed

- Settings and the Change Log use the Esc menu's look.
- The Change Log has no license paragraph.
- Switching camera mode briefly shows its name.
- The role greeting stays in the chat.
- The tip of the round stays in the chat.
- Windows keep their share of the screen on resize.
- The big chat window opens on entering the round and in the lobby, each remembering its place.
- The Esc menu has no background loop and dims more.
- The F4 stats show a prediction line.
- Projectiles fly three times as fast as in tg (a dev slider sets it).
- First-person held things: bigger guns, mirrored tools, reload and rack motions, shot-coloured muzzle flash.
- A bullet hole shows only on the side hit.
- The HUD's hand boxes are swapped to match the view.

## [1.0.5-pre-alpha] - 2026-10-28

### Protocol

- Network protocol 53: clients and servers older than 1.0.5-pre-alpha cannot connect to this version.

### Added

- Bug Report with a screenshot, saved by the server and optionally posted to Discord.

### Changed

- In the lobby, Esc no longer opens the menu; Settings, Change Log and Bug Report are buttons.
- Each server start uses a new random seed (`--seed` or SEED fixes it).

## [1.0.4-pre-alpha] - 2026-10-28

### Protocol

- Network protocol 52: clients and servers older than 1.0.4-pre-alpha cannot connect to this version.

### Added

- Esc opens tg's escape menu in the lobby and in game.
- A hint shows when you are in cursor mode.
- The Settings window (F10) with tabs.

### Changed

- Esc only opens and closes the menu.
- Only Tab frees and captures the mouse; opening a window turns cursor mode on.
- The scan readout, dev console, storage and inventory windows move, resize and remember their place.

### Fixed

- Firelock and external airlock frames have no holes; open leaves stay inside.
- Half-pixel model faces no longer flicker.

## [1.0.3-pre-alpha] - 2026-10-27

### Protocol

- Network protocol 51: clients and servers older than 1.0.3-pre-alpha cannot connect to this version.

### Added

- Chat in the lobby (OOC and prayers).
- Prayers reach the admins.
- Observe from the lobby.
- Dead chat.
- Ghosts get Orbit and Teleport buttons with tg's orbit menu.
- Observe through a player's eyes with auto-observe.
- Ghost Teleport to areas.
- Ghost double click follows or jumps; dead chat's (F) and (T) links work.
- The lobby's Changelog button opens this changelog; unread updates are announced.

### Fixed

- Damaged eyes blur and narrow the sight; glasses correct the blur.
- Climbing onto and off tables is smooth and predicted.
- Remote players no longer get the host's admin rights.
- Admins' OOC lines reach others.
- Long alert messages wrap.
- Unquoted chat links are clickable.
- Fixed-width table columns keep their width.
- The first jetpack push no longer jumps the view.

## [1.0.2-pre-alpha] - 2026-10-27

### Protocol

- Network protocol 50: clients and servers older than 1.0.2-pre-alpha cannot connect to this version.

### Fixed

- Drifting through space between floors no longer jerks the view.

## [1.0.1-pre-alpha] - 2026-10-27

### Protocol

- Network protocol 49: clients and servers older than 1.0.1-pre-alpha cannot connect to this version.

### Changed

- Lit surfaces are capped at 1.5 by default.

### Fixed

- Switching walk and run while moving no longer jerks the view.

## [1.0.0-pre-alpha] - 2026-10-27

### Added

- Options: crosshair size.
- About 150 more machines and furniture are 3D models, with animated screens, doors and moving parts.
- More 3D models, including bookcases and smart fridges.
- Stasis beds and the suit storage unit are 3D, with their states.
- A showroom map of all MetaStation art (`--map showroom`).
- Server: `--no-lobby`.
- Admin console: `reboot [SECONDS]`.
- Transit tubes with pods and stations, as 3D models; the RPD lays them.
- Character setup: the Occupations page with job priorities.
- Character setup: up to three characters, assignable to jobs.
- A supermatter cascade strands the emergency shuttle.
- Players logging in after round end see the reboot countdown.

### Changed

- The station is named Anomaly Station.
- The shipped config opens every job but silicons (`OPEN_JOBS`); others join as assistants.

### Fixed

- Riding a transit pod or pulled locker glides smoothly.
- Titanium airlocks no longer flicker or show gaps.
- Health analyzer timestamps and time of death use the station clock.
- Shuttle doors and buttons keep to their own shuttle.
- No boot runtime from self-removing emergency closets; `stack_trace` prints its stack.
- Airflow streams appear evenly; vent streams are shorter.
- The sun matches the server at join; small sky, window and solar fixes; the miner's locker is 3D.
- Distant doors are drawn.
- Entering new parts of the station no longer freezes the game.
- Animated things no longer cause hitches.
- The server keeps pace with 100 players on MetaStation and frees the old round on restart.

## [0.9.7] - 2026-10-26

### Protocol

- Network protocol 48: clients and servers older than 0.9.7 cannot connect to this version.

### Added

- The ice moon orbits the planet and passes over the station.
- Looking at the sun shows a lens flare.
- Solar panels turn smoothly.
- Solar panels start at random angles.
- Hard hat headlamps and the welding hard hat visor toggle.
- Welding without eye protection hurts the eyes.
- Dead bodies get an optional night-vision button.

### Fixed

- The welding hard hat starts with its visor up.
- Dead bodies no longer see meson outlines.
- Windows and chat move with the screen on resize.
- Floor and wall fixtures fall apart instead of floating when their support is gone.
- The typing indicator shows only while typing; the chat window stays open.
- Dev `revive` as a ghost revives or rebuilds your body.
- The pointing arrow stands over the pointed thing.
- `teleport` works from the in-game console.
- Lights switch at once.
- Jetpacking between floor and space no longer jerks the view.
- Riding a pulled bed glides smoothly.
- Wind shakes the view only once; walking resists it.
- Incapacitated, you can look only about 50 degrees aside; others see your body still.
- Air rushing through a ceiling hole no longer throws you up a level.

## [0.9.6] - 2026-10-26

### Protocol

- Network protocol 47: clients and servers older than 0.9.6 cannot connect to this version.

### Added

- A sky: a lava planet, moons, rocks, an asteroid field and a moving sun.
- Solar panels tilt toward the sun.

### Fixed

- A MetaStation server starts about 25 seconds sooner.
- Items on one tile no longer flicker or swap; mobs draw in front of them.

## [0.9.5] - 2026-10-26

### Protocol

- Network protocol 46: clients and servers older than 0.9.5 cannot connect to this version.

### Added

- Admin tools as in tg: ranks from `config/admins.txt`, bans, notes and watches.
- A server database (`data/ss3d.sqlite`): names are reserved and rounds are numbered.

### Fixed

- Models built while an icon was loading are rebuilt.
- Diagonal corner walls are prisms with wall texture.
- Hovering a turf names its type.
- Diagonal shuttle walls no longer draw black.
- Third person and free camera: rising above walls no longer blacks out the map.
- Third person: your own flashlight beam starts at the body.
- Shuttle windows draw as glass blocks; tiny fans lie flat.
- Building out of reach places one step toward the crosshair.

## [0.9.4] - 2026-10-26

### Protocol

- Network protocol 45: clients and servers older than 0.9.4 cannot connect to this version.

### Added

- The round ends at CentCom with tg's report, then a fresh round starts and clients rejoin.
- Heads of staff are playable, with captaincy and the spare ID safe.
- MetaStation's round-start shuttles stand at their docks; others wait at CentCom.
- Late joiners arrive on the arrivals shuttle.
- The emergency shuttle: called from the communications console, docks, flies and ends the round.
- Dev `shuttle call [norecall]|recall`.
- The view follows you onto another level.
- Dev `startnow`.
- Bananium floors honk and slip.
- Server option `OPEN_JOBS` (`--open-jobs`).

### Fixed

- An ejected cryo patient is no longer drawn inside.
- Buckling seats you on the chair; lying on a bed puts the view on top.
- Late joiners no longer spawn in space.
- OOC stays on at round start.
- The lobby no longer draws over its windows.
- Late joiners arrive at their job's start.
- Reconnecting mid-round puts you straight back in your character.
- A skull crack on someone not in hard crit now deals its damage.

## [0.9.3] - 2026-10-24

### Protocol

- Network protocol 44: clients and servers older than 0.9.3 cannot connect to this version.

### Changed

- Flashlight beams have no haze and softer edges.
- Glowing model parts are much dimmer; lamps keep their glow.
- Rooms are a little dimmer and unlit places darker.
- Joining without the lobby uses tg's late join as an assistant.

### Added

- The round loop: tg's lobby, character setup, Ready, the countdown, spawning as a station engineer and the job menu.
- Buildable ceilings: plating, frames, floor finishes, breaches with decompression, damage, and a ceiling editor.

### Fixed

- Walking in from outside an airlock ends weightlessness.
- Lockers and cabinets show their own door from round start and get their missing tg contents.
- Wall lamps light their own wall.
- Light behind a closed door no longer flickers on the walls beside it.

## [0.9.2] - 2026-10-24

### Protocol

- Network protocol 43: clients and servers older than 0.9.2 cannot connect to this version.

### Added

- Security camera consoles show a live camera view; about 3 % of cameras start off.

### Changed

- The server sends each player only what they can see within 128 m, about half the bandwidth (`--no-interest` to disable).
- Your hands and ID-opened doors respond at once (predicted), rolling back if the server disagrees.

## [0.9.1] - 2026-10-24

### Protocol

- Network protocol 42: clients and servers older than 0.9.1 cannot connect to this version.

### Added

- New chemical tank models; the SMES has a 3D body with animated panel and indicators.
- A rebuilt 3D urinal.
- 3D wall fixtures, signs, cabinets and atmos machinery with their states and animated displays.

### Fixed

- Other players move smoothly on a lossy connection.
- Jetpack drift stops on platforms; steering is smoother and predicted.
- The SMES indicators use the original colours and blinking.
- Walking off a platform keeps your movement angle.
- Reverted the platform-edge drift change.
- Dead bodies have no meson outlines; ghosts get a night-vision button.
- Player names sit above the head and follow it when lying.
- Pointing hits the exact target; the APC indicator is brighter.
- Sinks, showers, floor lamps and holopads have correct sizes and placement.


## [0.9.0] - 2026-10-23

### Added

- Examining a player shows tg's full description, a closer second look, and HUD records with links.
- Identity: masked people without ID are "Unknown"; knocked-out players see no names.
- Bystanders see shoves, drops and items put in bags.
- A renamed player's ID and PDA take the new name.

## [0.8.7] - 2026-10-23

### Protocol

- Network protocol 41: clients and servers older than 0.8.7 cannot connect to this version.

### Added

- Grabs: aggressive, neck and strangle grabs; resisting and throwing.
- Giving items with G.
- Hand emotes: *slap, *kiss, *hand, *circle, *noogie, *shoesteal.
- Hugs, head pats and shakes with tg's lines.

## [0.8.6] - 2026-10-23

### Protocol

- Network protocol 40: clients and servers older than 0.8.6 cannot connect to this version.

### Added

- The big chat window (Shift+T) with tabs, tg's colours and channel buttons.
- Radio and telecomms: department channels through the telecomms room, radio and intercom windows, encryption keys.
- Station announcements and the Automated Announcement System.
- The remote signaling device.
- The supermatter reports on the engineering channel.
- Telecomms monitoring and server monitoring consoles.
- The station has a name; announcements that use it work.
- Screentips on radios and intercoms.
- The PDA with NtOS.
- The PDA messenger through telecomms, with a Messages chat tab.
- The crew manifest and basic PDA apps.
- The messaging server, message monitor console and NTNet relay.
- The chat's Radio list shows your headset's channels.

### Fixed

- Empty override images no longer desync the client.
- Third-person camera and visibility mask fixes; gas sensors lie flat.
- Cursor mode works in both views; melee effects and hit shakes.
- Suffocation gasps; wire controls on open panels.
- Wall fixtures keep their support; small atmos and drift fixes.
- Dev flight no longer climbs furniture; suit storage units hold EVA suits for now.
- Motor oil leaves oily footprints.
- tgui windows show icons and their access eye.

### Changed

- A too-soon emote says "emote on cooldown!".

## [0.8.5] - 2026-10-23

### Protocol

- Network protocol 39: clients and servers older than 0.8.5 cannot connect to this version.

### Added

- Emotes: the emote wheel (J), tg's emote panel, typed and custom emotes, with sounds and visuals.
- Chat lines use tg's colours.
- People scream, gasp and cough where tg makes them.

## [0.8.4] - 2026-10-23

### Protocol

- Network protocol 38: clients and servers older than 0.8.4 cannot connect to this version.

### Added

- Self-contained release builds for Windows and macOS (`tools/build-release.sh`).
- Speech as tg's: the say window, range, shouting, whispering, runechat and typing bubbles.
- OOC chat and praying.
- Machines speak again.

## [0.8.3] - 2026-10-22

### Protocol

- Network protocol 37: clients and servers older than 0.8.3 cannot connect to this version.

### Added

- Pointing with Shift + middle click.
- Rest and sleep buttons; resist (B).
- Sleep and Succumb in the radial menu.
- Climbing onto tables and other furniture, and vaulting railings.
- The strip menu.
- Shoelaces.
- Adjusting a jumpsuit from the strip window.

### Fixed

- Changed turfs no longer pass signals to climbable tables (a delamination runtime).

## [0.8.2] - 2026-10-22

### Protocol

- Network protocol 36: clients and servers older than 0.8.2 cannot connect to this version.

### Added

- Drag gestures in cursor mode to pick up, equip, store, use and drop items.
- The radial menu offers Buckle, Unbuckle, Pull and Stop pulling.
- The stasis bed is drawn as tg's.
- The stasis bed works as tg's.
- Buckling to chairs, beds and tables; roller beds; rolling medical beds.
- Breaking out of lockers, crates, cryo cells and suit storage units; everyone sees visible messages.

### Fixed

- Freezers no longer suffocate whoever is inside; they cool as in tg.

## [0.8.1] - 2026-10-22

### Added

- The Cryo window.
- The cryo cell works as tg's, with cryoxadone and pyroxadone.
- The cryo cell is drawn as tg's, with its animations and sounds.

## [0.8.0] - 2026-10-22

### Protocol

- Network protocol 35: clients and servers older than 0.8.0 cannot connect to this version.

### Added

- Hallucinations from tg's pool, caused by the supermatter and anomalies (dev: `hallucinate`).
- Reagents are metabolized, overdose and are tasted.
- The liver and liver failure.
- Dev: `reagent` and `reagents`.
- Medical stacks: bruise packs, sutures, ointment, mesh, bone gel, tourniquets.
- The health analyzer modes, penlight and stethoscope.
- First aid on yourself through the health doll; a readout panel (F9).
- The defibrillator and its wall mounts.
- Shocks cause jitter and hand convulsions.
- Dev: `heart` and `breath`.
- tg's medicines and their overdoses; synthflesh and pepper spray.
- Drowsiness, dizziness and heart attacks.
- Determination after a fresh wound.
- Disgust.
- Data HUDs: medical and security glasses.
- Implants and full job outfits with mindshields and skillchips.
- Skillchips.
- The stamina display.
- Reagent containers: beakers, droppers, syringes, with fill levels.
- Pills, patches, medipens and spray bottles.
- Medkits, pill bottles, medical belts, blood packs, IV drips, syringe guns.

### Fixed

- A container thrown into a wall no longer errors.
- Items leaving your hand by other means are removed from the hand slot.
- Hallucinated people can be clicked where drawn.
- The medical HUD floats over the head.
- The oxygen deprivation kit is filled.
- Dev `outfit` removes worn things first.

## [0.7.2] - 2026-10-22

### Added

- Ballistic guns: the .38 revolver, Russian revolver, Makarov and contraband pistols.
- .38 rounds: lead, rubber with bounces, ricochets and shrapnel.
- Holsters, collecting casings, and rechargers.
- Shotguns and shells, with spreading pellets.
- Full auto (WT-550) and the battle rifle with its scope.
- Gunpoint hold-ups; electrodes tase.

### Changed

- Shots are drawn in 3D: glowing bolts, sparks, tracers, beams, muzzle flashes and impacts.
- Your gun kicks and flashes in view.

### Fixed

- Shot effects no longer show as flat pictures.
- Muzzle flashes reach clients.

## [0.7.1] - 2026-10-22

### Protocol

- Network protocol 34: clients and servers older than 0.7.1 cannot connect to this version.

### Added

- Energy guns: disablers, energy guns, lasers, ion rifle, temperature gun; cells, firing pins and rechargers.
- Batons, flashes and restraints: stun baton, telescopic baton, stunprod, flashes, handcuffs, zipties, bear traps, security belts.
- Shots hit the body part they really cross and can fly past a body.
- Recoil kicks the view; spread is vertical too.

- The supermatter crystal has a 3D model with tg's glow, rays and state effects.

### Changed

- Projectiles and effects are drawn at the height they really fly.

- Airlocks no longer light a patch of floor (D174).

### Fixed

- Wall lamps keep their size and place in every state.
- A closed opaque door is lit by the side it faces, not through itself.

## [0.7.0] - 2026-10-21

### Protocol

- Network protocol 33: clients and servers older than 0.7.0 cannot connect to this version.

### Added

- The rapid pipe dispenser (RPD) with tg's window; pressure gates, temperature gates and pumps, multi-deck adapters.
- Darkness matters: no reading or scanning in the dark.
- Power monitoring consoles; computers show their screens, light up and can be dismantled.
- Engineering scanner goggles: meson, T-ray, pipe connections and thermal modes.
- Computer frames are built and taken apart in stages.

### Changed

- T-ray and goggle views show constantly instead of blinking (DV91).
- Status alerts sit in the top right at any HUD scale.
- Device and crafting windows can be resized and remember their size.
- 3D-model things keep their facing when moved and can be rotated by hand (Alt-click).
- Portable canisters have a new 3D model with gas colours, pressure lights and a tank slot.

### Fixed

- Unported consoles leave their frame and board when dismantled.
- Pipe connection arrows and thermal tiles lie flat on every tile.
- Husks look like tg's husks.
- Things on a table no longer sink under it and fall when it is gone.
- Melting ice under objects no longer errors.
- Contact floors like plasma react to what steps on them.
- Starlight lights the hull and edge floors.
- Device windows show all row labels again.
- A cascade delamination no longer stalls the client.
- The singularity leaves unported monkeys alone instead of erroring.
- Vomiting no longer errors.
- `smdelam` no longer double-delaminates.
- The dimensional anomaly no longer gets stuck on torn tiles.
- An exploding atmos tank no longer errors.

## [0.6.4] - 2026-10-21

### Protocol

- Network protocol 32: clients and servers older than 0.6.4 cannot connect to this version.

### Changed

- More shadowed lights per preset (high 10, medium 4, low none).

### Added

- Options: "Shadowed lights" count (0 to 16).
- Supermatter delamination: explosion, singularity, energy ball or resonance cascade, with anomalies (dev: `smdelam`).
- The vortex, bioscrambler and dimensional anomalies.
- Light switches.
- Light fixture frames are built and taken apart.
- Flares, candles and torches.
- Glowsticks.
- The energy ball roams, zaps and grows mini-balls.

### Fixed

- Things starting to drift wait tg's first drift delay; no more errors.

## [0.6.3] - 2026-10-20

### Protocol

- Network protocol 31: clients and servers older than 0.6.3 cannot connect to this version.

### Added

- The supermatter engine works as in tg: emitters, gases, power, damage and the countdown (dev: `smcrystal`).
- Anomalies near a stressed supermatter: flux, gravitational, pyroclastic and hallucination.
- Pump windows.
- Reflectors.
- Emitter beams and the projectile system.
- Emitters are wrenched, welded, powered and locked.
- Tesla zaps, tesla coils and grounding rods (dev: `zap`).
- The inducer.
- The light replacer; light tubes and bulbs are real items.
- The rapid pipe cleaner layer and pipe cleaner art.
- Holographic sign projectors and the atmos holofan.
- Meson goggles and blindness tint.
- The gas analyzer and its window with the atmos handbook.
- Light tubes and bulbs can be fitted by hand.

## [0.6.2] - 2026-10-20

### Protocol

- Network protocol 30: clients and servers older than 0.6.2 cannot connect to this version.

### Added

- EMPs hit cells, suits, jetpacks, airlocks and alarms as in tg (dev: `emp`).
- APCs run room power with channels, cells, the APC window and maintenance.
- Power cables: cut, lay and join networks; multitool readings.
- SMES units and P.A.C.M.A.N. generators (dev: `recharge`).
- Solar arrays, trackers and the solar control console.
- Radiation, its sickness, protection and the geiger counter (dev: `irradiate`).
- The engine room's freezers, heaters, heat exchangers and gas meters.
- Live power shocks; insulated gloves protect.
- Electrical tools: multitool APC finder, T-ray scanner, tool belts and toolboxes.

### Fixed

- An airlock losing power no longer errors.

## [0.6.1] - 2026-10-20

### Protocol

- Network protocol 29: clients and servers older than 0.6.1 cannot connect to this version.

### Added

- An airlock with its safety wire cut crushes whoever stands in it.
- Lights switch to a dim red emergency light when power is cut.
- Dev: `areapower` cuts or restores a room's power channel.
- Head blows can cause brain damage and concussions.
- Holding a bleeding limb slows the bleeding.
- Low blood and staggering punches blur the view.

### Fixed

- Filters added or removed show at once.

## [0.6.0] - 2026-10-20

### Changed

- The player moves at half speed: run 6.7 m/s, walk 2.5 m/s.
- Walking footsteps are heard too.
- A ghost's camera turns freely.
- More table and wall items are 3D models.

### Fixed

- 3D models are aimed at by their own shape.
- Dev rotate turns any 3D model.
- Wall mounts are aimed at by their model.
- A door button no longer draws a hanging green block.
- Torn coloured clothes show their torn picture.

## [0.5.2] - 2026-10-19

### Protocol

- Network protocol 28: clients and servers older than 0.5.2 cannot connect to this version.

### Changed

- Ghosts fly freely in third person by default.
- Zero-gravity and jetpack movement glides continuously with tg's inertia.
- The singularity and other tile-moved things glide smoothly.
- The singularity lights its surroundings in its colours and glows.

### Fixed

- Items on racks and shelves can be aimed at where drawn.
- An open windoor no longer catches clicks meant for what is behind it.
- The suit storage unit shows its state.

## [0.5.1] - 2026-10-18

### Protocol

- Network protocol 27: clients and servers older than 0.5.1 cannot connect to this version.

### Changed

- Progress bars are larger and labelled.
- An item's bar is drawn under its button.

## [0.5.0] - 2026-10-18

The first numbered build, collecting everything from the start of the port (2026-10-05).

### Server and connection

- A headless .NET server runs tg's MetaStation; players join from the Godot client.
- Server console and dev commands (`help` lists them).
- Load-test bots and replays.
- Build versions in the client and server log; this changelog (D158). Protocol 26.

### Character and movement

- First-person view with mouse look and a third-person view; client-side prediction.
- tg's movement speeds, crawling, shoving, the health doll and damage.
- Hands, inventory, storage, ID cards, the radial menu.
- HUD text follows the HUD scale.

### The station

- All of MetaStation: walls, floors, doors, airlocks, lights, firelocks.
- Machines and furniture as 3D models; items and people as billboards.
- Flashlights with beams, tinted by visible gases.

### Atmosphere and space

- tg's LINDA air, space wind and zero-gravity drift.
- Pipenets, vents, scrubbers, pumps, filters, air and fire alarms, canisters.
- Gas visuals, fire and plasma.

### Construction and destruction

- tg's construction and crafting in tgui windows.
- Basic tools and the fire axe.
- Explosions and the singularity.

### Death and the body

- Ghosts.
- Blood, wounds, embedding, dismemberment, gibbing, gauze.
- BYOND filters reach the client.

### Known gaps

- Surgery, most organs, the full medical bay.
- Power, social systems, the round loop.
