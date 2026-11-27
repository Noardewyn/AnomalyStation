# Required 3D models

The 3D models the ported mechanics need (CLAUDE.md): a thing whose icon no entry of `godot/assets/models/models.json` names keeps
its 2D picture until its model exists. Each entry names the tg icon and states the model must cover, where the mechanic is, and what
the client draws meanwhile.

## Furniture

- **Table frames** (`table_frames.dm`, `icons/obj/structures.dmi` states `table_frame` and `wood_frame`): the bare frame a table is built on.
  The metal frame is drawn by the framed glass table's single piece without its glass (`frame_table`, a stand-in); a frame of its own is
  wanted, and a wooden one, which is still tg's sprite.
- **Flipped tables** (gaps L1, `tables_racks.dm:133`): a table flipped on its side, facing each of the four cardinal directions
  (`icons/obj/flipped_tables.dmi`, the states of every table kind: `table`, `woodtable`, `glasstable`, `reinf_table`, `poker_table`,
  `fancy_table`, and the rest the table types name as `base_icon_state`). The metal table's model (`furniture/metal_table`) turned
  on its side would serve the metal tables. Not needed: every kind is drawn by its own table's single-table model tipped onto its side (the catalog's `"tip"`, the owner, 2026-11-17).

- **The shower's falling water** (gaps H1, `structures/shower.dm:203`, tg's `water` overlay of `icons/obj/watercloset.dmi`, four
  animated frames): a stream of water from the wall shower's head (`station_props/bathroom/wall_shower.glb`, its head at 1.94 m) to the
  floor, drawn while the shower runs. Meanwhile the client draws two planes crossing under the head with tg's first water frame
  (`shower_water.glb`, made by `tools/models/make_shower_water.py`), not animated and not tinted by the shower's reagents.

- **Curtains** (gaps ST2, `structures/curtains.dm`, `icons/obj/watercloset.dmi` and the
  subtypes' icons): the shower curtain (`bathroom-open`, `bathroom-closed`, see-through when shut and tinted by its `color`), the bounty, cloth
  and fancy curtains (`bounty-*`, `cloth-*`, `cur_fancy-*`) drawn and open, opaque when shut; the theatre's mechanical curtains are the
  fancy ones. Meanwhile the client draws tg's sprite in 2D.

- **Bookcases** (gaps ST2, `library/bookcase.dm`, `icons/obj/service/library.dmi`): the frame (`bookempty`) and the finished shelf with
  zero to five rows of books (`book-0` to `book-5`), a wooden piece of furniture against a wall. Meanwhile the client draws tg's sprite in
  2D.

- **Display cases** (gaps ST2, `structures/displaycase.dm`, `icons/obj/structures.dmi`): the glass case on its pedestal (`glassbox`,
  with its `glassbox_closed` and `glassbox_broken` glass overlays) with the showpiece shrunk inside, the chassis (`glassbox_chassis`),
  and the vend-a-tray (`icons/obj/machines/display.dmi`: `laserbox`, `laserbox_empty`, `laserbox_open`, `laserbox_broken`, its `laserbox_overlay` hover field and the
  `laserbox_vend` flick). Meanwhile the client draws tg's sprites in 2D, the showpiece as the case's overlay.

- **Mannequin** (gaps ST2, `structures/mannequin.dm`, `icons/mob/human/mannequin.dmi`): the wooden, plastic and skeleton mannequins, male
  and female (`mannequin_<material>_<male|female>`), on their pedestal (`pedestal_<material>`), wearing clothes on a human's slots and
  turned by its dir. Meanwhile the client draws tg's sprite in 2D with the worn clothes' overlays.

- **Basketball hoop** (gaps ST2, `basketball/hoop.dm`, `icons/obj/fluff/basketball_hoop.dmi`): the
  hoop and backboard on its pole, turned by its dir, with the scoreboard (`basketball_scorecard` of `icons/obj/signs.dmi`) and its two
  digits (`days_0` to `days_9`) behind it. Meanwhile the client draws tg's sprites in 2D.

- **Showcases** (gaps ST2, `structures/showcase.dm`): the exhibits other than the old cyborg (which has its model): the mech showcases,
  the fake ID and security consoles, the cloning pod, the old pod, the TV, the wizard and the katana, each tg's machine or object
  `icon_state` of the file it names. Meanwhile the client draws tg's sprites in 2D.

## Space hazards

- **Meteors** (gaps E2b and D299, `modules/meteors/meteor_types.dm`, `icons/obj/meteor.dmi`): tumbling rocks flying through space
  at the station, one model per state: `dust` (space dust and sand, the cluster's fragments), `small` (medium), `large` (big),
  `flaming` (flaming, tunguska), `glowing` (irradiated), `sharp` (cluster), `carp` (the space carp meteor), `bluespace` (bluespace
  and EMP), `bananium`, `meateor` (meaty, xeno), `dark_matter` (the dark matt-eor, with its warp ring), `spooky` (the pumpkin) and
  `changeling` (the space changeling's meaty meteor, gaps K1). They spin as they fly (`SpinAnimation`). Meanwhile the client draws tg's sprite in 2D.
- **Immovable rod** (gaps E2b, `events/immovable_rod/immovable_rod.dm`, `icons/obj/anomaly.dmi` state `immrod`): a long metal rod
  flying straight through the station; it grows 0.5 % per sentient kill. Meanwhile the client draws tg's sprite in 2D.

## Station goals

- **Bluespace artillery parts** (gaps R1, `modules/station_goals/bsa.dm`, `icons/obj/machines/particle_accelerator.dmi`): the generator
  (`power_box`), the bore (`emitter_center`) and the fusor (`fuel_chamber`), one-tile machines that turn (simple rotation), and the
  artillery control console (`control_boxp`). Done (`machines/required_models/bsa_*`).
- **Bluespace artillery cannon** (gaps R1, `bsa.dm` `/obj/machinery/bsa/full`, `icons/obj/machines/cannon.dmi` states `cannon_west` and
  `cannon_east`, with the overlay `top_west` or `top_east` drawn above mobs): a cannon eleven tiles long and three wide facing west or
  east (tg's sprite is 352 px wide, offset -192 or -128 px), firing its beam (`icons/effects/beam.dmi` `bsa_beam`) from the bore's end;
  the muzzle splash (`icons/effects/beam_splash.dmi` `beam_splash_e`, `beam_splash_w`) and the detonation (`icons/effects/96x160.dmi`
  `bsa_impact_ex`) are effects. Done (`machines/required_models/bsa_cannon`); the beam, splash and detonation are still 2D effects.
- **DNA vault** (gaps R1, `dna_vault.dm`, `icons/obj/machines/dna_vault.dmi` state `vault`): a three by three tile machine (tg's sprite is
  offset -32 and -64 px; its fillers hold the tiles around it) lit cyan. Done (`machines/required_models/dna_vault`).
- **Meteor shield satellite** (gaps R1, `game/machinery/satellite/satellite.dm`, `icons/obj/machines/satellite.dmi` states `sat_inactive`
  and `sat_active`): a satellite floating in space that bobs while on and shoots meteors with its beam (`beam.dmi` `sat_beam`). Done
  (`machines/required_models/meteor_shield_satellite`); it does not bob yet and stands upright while off (the model's side-lying pose is not drawn).
- **DNA sampler** (gaps R1, `items/dna_probe.dm`, `icons/obj/medical/syringe.dmi` state `sampler`, in hand `sampler`): a hand-held
  sampler. Meanwhile the client draws tg's sprite in 2D.

## Transport

- **Tram spoiler** (`tram_structures.dm` `/obj/structure/tram/spoiler`, `icons/obj/tram/tram_structure.dmi` states
  `tram-spoiler-retracted`, `tram-spoiler-deployed`, the deploying and retracting flicks, `tram-spoiler-welded`; four dirs): the car's
  cut corner. Done (`structures/tram/tram_spoiler`): turned by its dir, its carriage slides out while deployed, the weld seam while welded.
- **Tram door** (`tram_doors.dm` `/obj/machinery/door/airlock/tram`, `airlocks/tram/tram.dmi` and `tram-overlays.dmi`): the car's two-tile
  airlock. Done (`structures/tram/tram_door`): it spans its own tile and its filler's by its dir, its leaves slide into the
  wall, and tg's light, panel, weld, seal and spark overlays show front and rear, animated by their own frames.

- **Tram controls** (`tram_controls.dm` `/obj/machinery/computer/tram_controls`, `computer.dmi` `tram_controls` and `tram_alt_controls`
  with their screens): the window-mounted panel and the standalone console. Done (`machines/required_models/tram_control_window` and
  `tram_control_standalone`): each hangs on its wall and shows tg's screen (the platform, the direction, NIS, broken).
- **Tram machinery** (`tram_machinery.dm`, `tram_displays.dm`, `tram_controller.dm`, `tram_power.dm`, `tram_signals.dm`): the call button, the
  destination sign and platform indicator, the local and central controllers, the power rectifier and the crossing signal. Done
  (`machines/required_models/tram_*`): each shows tg's state or overlays (screens, status lights, signal aspects). The call button's cover and
  the local controller's door swing open on their hinges while tg shows them open.

## Cargo

- **Supply pod** (`cargo/supplypod.dm` `/obj/structure/closet/supplypod`, gaps E3: the stray cargo pods; also the express console's
  pods, gaps P4): the drop pod that falls from the sky, lands, opens its door and leaves. `icons/obj/supplypods.dmi` states `pod` and
  `darkpod` (the styles of `datums/pod_style.dm`: the standard, bluespace and CentCom pods use `pod`, the syndicate and Deathsquad pods
  `darkpod`), their `_door` overlays and the decals of `decal_icon` (`default`, `bluespace`, `centcom`, `syndicate`, ...), the yellow,
  blue or red landing glow (`add_glow`), and the falling and opening frames; the landing marker is `icons/obj/supplypods_32x32.dmi`
  `LZ` and `LZ_Slider`. Meanwhile the client draws tg's sprites in 2D.

## Plants and growths

- **Space vines, kudzu** (gaps E4b, `events/space_vines/vine_structure.dm` `/obj/structure/spacevine`, `icons/mob/spacevines.dmi` states
  `Light1`-`Light3`, `Med1`-`Med3`, `Hvy1`-`Hvy3`): a creeper over a tile's floor and what stands on it, in tg's three growth stages
  (sparse shoots; a thick mat that blocks the view; a dense tangle), one of three variants a stage, tinted by its mutations' hue (the
  vine's `color`), half transparent for the transparent mutation (alpha 125) and flat on the floor for the timid one (tg puts it on the
  floor plane). Meanwhile the client draws tg's sprite: an upright picture on its tile, the timid vine flat on the floor.
- **Flower bud** (`basic/jungle/venus_human_trap.dm` `/obj/structure/alien/resin/flower_bud`, states `bud0`-`bud3`): the flowering
  kudzu's pulsating bulb in its four growth stages, with the thick vines (`icons/effects/beam.dmi` state `vine`) to its diagonals.
  Meanwhile the client draws tg's sprite upright and the vines as beams.
- **Venus human trap** (`/mob/living/basic/venus_human_trap`, state `venus_human_trap`): the man-eating plant the bud bears, a mob.
  Meanwhile the client draws tg's sprite as a standing picture.
- **Trees** (gaps ST1, `structures/flora.dm` `/obj/structure/flora/tree`): dense trees a player walks around and chops down, one model
  per look: the pine trees (`icons/obj/fluff/flora/pinetrees.dmi` `pine_1`-`pine_3`, 64x96), the decorated Christmas tree (`pine_c`) and
  the one with presents under it (`pinepresents`), the dead trees (`deadtrees.dmi` `tree_1`-`tree_6`, 64x64), the jungle trees
  (`jungletrees.dmi` `tree1`-`tree6`, 128x160) and the small jungle trees (`jungletreesmall.dmi` `tree1`-`tree6`, 96x96), the palms
  (`icons/obj/fluff/beach2.dmi` `palm1`, `palm2`) and the coconuts at a palm's foot (`beach.dmi` `coconuts`); the stump a felled tree
  leaves (`pinetrees.dmi` `tree_stump`, named after its tree), and a tree lying on its side when a shovel uproots it (tg turns the sprite
  a quarter, either way). The festivus pole (`pinetrees.dmi` `festivus_pole`) and the suplexed rod in its planter (`anchored_rod`) go
  with them. Meanwhile the client draws tg's sprites upright (a big icon centred on its tile, DV49), the uprooted one turned.
- **Bushes and grass** (gaps ST1, `flora.dm` `/obj/structure/flora/bush`, `/obj/structure/flora/grass`): knee-high bushes, reeds,
  flowers and tufts of grass, one model per look: `icons/obj/fluff/flora/ausflora.dmi` (`firstbush_1`-`4`, `reedbush_1`-`4`,
  `leafybush_1`-`3`, `palebush_1`-`4`, `stalkybush_1`-`3`, `grassybush_1`-`4`, `fernybush_1`-`3`, `sunnybush_1`-`3`, `genericbush_1`-`4`,
  `pointybush_1`-`4`, `lavendergrass_1`-`4`, `ywflowers_1`-`3`, `brflowers_1`-`3`, `ppflowers_1`-`3`, `sparsegrass_1`-`3`,
  `fullgrass_1`-`3`), the snowy ones of `snowflora.dmi` (`snowbush1`-`6`, `snowgrass1bb`-`3bb`, `snowgrass1gb`-`3gb`,
  `snowgrassall1`-`3`), the jungle ones of `jungleflora.dmi` (`busha1`-`3`, `bushb1`-`3`, `bushc1`-`3`, `grassa1`-`5`, `grassb1`-`5`), the
  large jungle bushes of `largejungleflora.dmi` (`bush1`-`3`, 64x64, drawn above mobs) and the glowing lunar plant
  (`xenoflora.dmi` `lunar_plant1`-`3`). Picked grass leaves the same tuft (it regrows); an uprooted bush lies on its side. Meanwhile
  the client draws tg's sprites upright.
- **Rocks** (gaps ST1, `flora.dm` `/obj/structure/flora/rock`, `icons/obj/fluff/flora/rocks.dmi`): the dense large rocks (`basalt1`-`4`,
  also tinted icy blue), the walkable rock piles (`lavarocks1`-`3`, the siderite `lavarocks_siderite1`-`3` and shale
  `lavarocks_shale1`-`3` ones), the jungle rocks (`jungleflora.dmi` `rock1`-`5`, `largejungleflora.dmi` `rocks1`-`3`), the siderite
  growth stalagmite (`icons/obj/mining_zones/terrain.dmi` `siderite_growth1`-`3`) and the volcanic pore with its glowing lava
  (`volcano_1`-`4` and their `_e` glow). A pickaxe mines them into sand. Meanwhile the client draws tg's sprites upright.
- **Potted plants** (gaps ST1, `items/kirby_plants/kirbyplants.dm`, `icons/obj/fluff/flora/plants.dmi`): a plant in a pot a player
  carries in both hands, one model per look: `plant-01`-`plant-24` and `applebush` (the looks a bonsai trim cycles through), the
  plastic ones `plant-26`-`plant-29`, the withered dead plant `plant-25`, Potty the Potted Plant (`potty`, with its blinking light
  `potty_light` while lit) and the neglected fern (`fern`). Held, it stands in for its carrier (tg's tactical disguise). Meanwhile the
  client draws tg's sprites as item pictures, in hand and on the floor.

## Holidays

- **Holiday items** (gaps R3, `modules/events/holiday/*.dm`, `items/gift.dm`): small hand items, one model per state: the surprise egg
  (`icons/obj/food/egg.dmi` `egg-blue`, `egg-green`, `egg-mime`, `egg-orange`, `egg-purple`, `egg-rainbow`, `egg-red`, `egg-yellow`), the
  chocolate egg, the hot cross bun, the scotch egg and the chocolate bunny; the wrapped gift (`icons/obj/storage/wrapping.dmi`
  `giftdeliverypackage1` to `5`); the christmas cracker whole and in its two halves (`icons/obj/holiday/christmas.dmi` `cracker`,
  `cracker1`, `cracker2`); the trick-or-treat bag and the skull and coffin cookies (`icons/obj/holiday/halloween_items.dmi` `treatbag`,
  `skeletoncookie`, `coffincookie`); the valentine card (`icons/obj/toys/playing_cards.dmi` `sc_Ace of Hearts_syndicate`) and the candy
  hearts (`icons/obj/holiday/holiday_misc.dmi` `candyheart` to `candyheart4`); the Easter basket; the sugar cookie's festive shapes
  (`sugarcookie_tree`, `_bear`, `_santa`, `_stocking`, `_present`, `_cane`). Meanwhile the client draws tg's sprites in 2D.
- **Holiday clothing** (gaps R3): the festive paper hat (`xmashat`), the Easter bunny head and suit (`bunnyhead`, `bunnysuit`), the bunny
  satchel (`satchel_carrot`), Santa's gift bag (`giftbag0`) and Santa's suit and hat, worn and held. Meanwhile the client draws tg's
  worn and held sprites in 2D.
- **The in-love hearts** (gaps R3, `status_effects/neutral.dm:86`, `icons/effects/effects.dmi` `love_hearts`): hearts floating over a
  valentine's date that only the lover sees (an alternate appearance). Meanwhile the client draws tg's sprite in 2D.

## Janitor

- **Janicart** (gaps H4, `vehicles/pimpin_ride.dm`, `icons/mob/rideables/vehicles.dmi` state `pussywagon`, with tg's overlays
  `cart_garbage`, `cart_bluespace_garbage` and the upgrade's `janicart_upgrade`): the janitor's ridden cart, the rider seated on it
  (tg's rider offsets per direction), its hooked trash bag and the installed upgrade. Meanwhile the client draws tg's sprite.
- **Janitorial cart's tools** (gaps H4, `structures/janitor.dm:359`, `icons/obj/service/janitor.dmi` overlays `cart_mop`, `cart_broom`,
  `cart_garbage`, `cart_bluespace_garbage`, `cart_spray`, `cart_replacer`, `cart_sign1` to `cart_sign4`): the mop and push broom in
  their clips, the trash bag, the spray bottle, the light replacer and up to four wet floor signs, at the cart model's anchor nodes.
  Meanwhile `tools/models/make_cart_tools.py` draws them as box shapes in flat colours.
- **Wet floor sign** (gaps H4, `suits/wetfloor.dm`, `icons/obj/clothing/suits/utility.dmi` state `caution`): the yellow A-frame sign
  standing on the floor. Meanwhile the client draws tg's sprite.

## Machines

- **Limb grower** (gaps MD3, `game/machinery/limbgrower.dm`, `icons/obj/machines/limbgrower.dmi` states `limbgrower_idleoff`,
  `limbgrower_idleon`, `limbgrower_panelopen`, flicks `limbgrower_fill` and `limbgrower_unfill`): medbay's tank that grows limbs in
  green synthflesh, its vat filling and draining as a limb grows. Done (`machines/required_models/limb_grower`): its display shows tg's
  live state picture and the synthflesh shows while it grows.

- **Organ harvester** (gaps MD3, `game/machinery/harvester.dm`, `icons/obj/machines/harvester.dmi` states `harvester`,
  `harvester-open`, `harvester-charging`, `harvester-active`, `harvester-o`): a body-sized pod whose lid opens, that charges and then
  works on the body inside. Meanwhile the client draws tg's sprites as a standing picture.

- **Jukebox and radiant dance machine** (gaps H3, `game/machinery/dance_machine.dm`, `icons/obj/machines/music.dmi` states `jukebox`,
  `jukebox-active`, `disco`, `disco-active`): the bar's jukebox, its lights on while it plays, and the radiant dance machine, a disco
  ball machine whose top spins while it plays. Done (`machines/required_models/jukebox`, `radiant_dance_machine`): each plays its active frames.

- **Lathes' material loading effects** (gaps RD1, `research/machinery/_production.dm:169` and `game/machinery/autolathe.dm:100`, tg's
  `flick_overlay_view` of `icons/obj/machines/research.dmi` states `material_insertion` (tinted the inserted material's colour) and
  `protolathe_progress`, and of `icons/obj/machines/lathes.dmi` state `autolathe_mat`): the sheet sliding into the protolathe, techfab or
  autolathe in its material's colour and the load lights running. The production wave's models have the nodes (`Feedstock sheet` with its
  `Feedstock tint` material, `Protolathe load 0` to `8`, `Load lights 0` to `5`), but its README leaves these clips unexported and the
  catalog has no part for them. Meanwhile the client draws tg's flicks as pictures on the machine. (The printing states `protolathe_n`,
  `autolathe_n` and `circuit_imprinter_ani` play on the models' live bodies.)

- **Nebula shielders** (gaps R2, waiting with the Radioactive Nebula station trait, `game/machinery/nebula_shielding.dm`): the
  radioactive nebula shielder (`icons/obj/machines/nebula_shielding.dmi` states `radioactive_shielding`, `_on`, `_open`, `_broken`; a
  64 px wide machine, `pixel_x -16`) and the emergency shielder of the care package (`icons/obj/machines/engine/other.dmi` state
  `portgen1_1`). Not needed until the nebula's system is ported (not-translated.md section 184).
- **Digital clock** (gaps DC1, `machinery/digital_clock.dm`, `icons/obj/digital_clock.dmi` state `digital_clock_base` with the digit
  overlays `+0` to `+9` and `+separator` at tg's offsets): a slim wall-mounted display with four blue seven-segment digits and a
  colon, lit blue, dark when broken or unpowered. Meanwhile the client draws tg's sprite and digits on the wall.

- **Master R&D server's overlays** (gaps RD2, `research/server.dm:126`, `:198`, `:258`, `icons/obj/machines/research.dmi` overlays
  `RD-server-objective-stripes` and `RD-server-hdd-panel-open`): the hazard stripes of the master server whose hard drive a traitor
  steals, and its front panel hanging open with the drive's housing exposed. Meanwhile they are flat-coloured boxes on the server's
  model (`tools/models/make_server_master_overlays.py`).

- **Teleporter** (gaps TP1, `machinery/teleporter.dm`, `icons/obj/machines/teleporter.dmi`): the hub, a walk-in ring pad
  (`tele0` off, `tele1` engaged with its swirling field, `tele-o` open panel), and the station, a control pillar beside it
  (`controller`, `controller-c` calibrating, `controller-p` unpowered, `controller-o` open panel). Meanwhile the client draws tg's
  sprites.

- **Library scanner's open panel** (gaps LB1, `library/lib_machines.dm:711`, `icons/obj/service/library.dmi` state `bigscanner2`):
  the scanner with its maintenance panel off. Its model (`machines/service_wave/library_scanner.glb`) has no hatch, so the open panel
  does not show; its scan (`bigscanner1`) and the binder's printing (`binder1`) play on the models' working surfaces.

- **Library consoles** (gaps LB1, `library/lib_machines.dm`, `icons/obj/machines/computer.dmi` state `oldcomp` with the `library`
  screen): the visitor console and the book management console. Meanwhile both are drawn by the old library computer's model
  (`station_props/station_equipment/library_console.glb`), as the admins' console is.

## Security

- **Secway** (gaps SE1, `vehicles/secway.dm`, `icons/mob/rideables/vehicles.dmi` state `secway`): the security officer's ridden
  two-wheeled scooter with a handlebar column, the rider standing on its deck (tg's rider offset: 4 px up in every direction); broken
  it smokes. Meanwhile the client draws tg's sprite.
- **Wall reagent dispensers** (gaps SE1, `reagent_dispenser.dm:334`, `icons/obj/medical/chemical_tanks.dmi` states `pepper` and
  `virus_food`): the pepper spray refiller on the brig's walls and virology's virus food dispenser, small wall-mounted tanks with a
  tap. Meanwhile the client draws tg's sprites on the wall.
- **DRAGnet teleportation field** (gaps SE1, `projectile/energy/net_snare.dm`, `icons/effects/effects.dmi` state `dragnetfield`, lit
  with range 3): a glowing bluespace net over a floor tile for three seconds. Meanwhile the client draws tg's sprite flat on the floor.
- **The enhanced interrogation chamber** (gaps SG1, `machinery/hypnochair.dm`, `icons/obj/machines/implant_chair.dmi`): a padded
  chair with a closing hood of strobing lights, one look per state: `hypnochair` (closed, empty), `hypnochair_open` (the hood up),
  `hypnochair_occupied` (closed with someone strapped in), `hypnochair_active` (the hood's lights strobing during an interrogation),
  `hypnochair_open_occupied`. Meanwhile the client draws tg's sprite in 2D.

## Kitchen

- **Barbeque grill** (gaps H2, `food_and_drinks/machinery/grill.dm`, `icons/obj/machines/kitchen.dmi` states `grill_open` (empty, no
  fuel), `grill_on` (burning fuel) and `grill` (something on it)): a charcoal grill with a lid; the grilled item shows its grill marks
  (the sizzle component's overlay). Done (`machines/required_models/barbeque_grill`): the coals burn in grill_on and the lid shuts over
  what grills; the grill marks are still the item's own picture.
- **Stove** (gaps H2, `machinery/stove.dm`, `icons/obj/machines/kitchen_stove.dmi` state `stove`, a 32x48 sprite): a freestanding
  stove top with burners and a pot on it. Not mapped on Meta; meanwhile the client draws tg's sprite in 2D.
- **Burner flames** (gaps H2, `machinery/stove_component.dm:163`): the range's and the stove's lit burners, tg's `<base>_on_flame`
  overlay (coloured `#006eff`) and the green `<base>_on_overlay` light, drawn while the burners are on. The range's model
  (`kitchen_range`) has no flame or light node; a flame part shown with those overlays is needed. Meanwhile the burners look off.
- **Coffee maker** (gaps H2, `machinery/coffeemaker.dm`, `icons/obj/machines/coffeemaker.dmi` state `coffeemaker`, mapped on Meta): the
  plain coffee maker with its pot (`pot_empty`, `pot_full`, `pot_bluespace` overlays), its cartridge (`cartridge`) and its stacked cups,
  sugar, sweetener and creamer (`cups_N`, `extras_N`); the impressa's model (`coffeemaker_impressa`) lacks the same cup and extras overlays
  and its grinder's beans (`grinder_half`, `grinder_full`). Meanwhile the client draws tg's sprite in 2D (the impressa its model without them).
- **Food cart stand** (gaps H2, `machinery/food_cart.dm`, `/obj/effect/food_cart_stand`, `icons/obj/fluff/3x3.dmi` state `stand`): the
  three by three tent the food cart unpacks into. Meanwhile the client draws tg's sprite in 2D.
- **Griddle stand** (gaps H2, `machinery/griddle.dm:229`, `kitchen.dmi` states `griddlestand_off`, `griddlestand_on` and the
  `front_bar` overlay): the commercial griddle on its stand. Not mapped on Meta; meanwhile the client draws tg's sprite in 2D.
- **Dehydrator parts** (gaps H2, `machinery/smartfridge.dm:450`, `icons/obj/machines/smartfridge.dmi` states `dehydrator-contents-1`
  and `dehydrator-panel`): the `dehydrator` model has no nodes for its contents or its open panel; meanwhile neither shows.
- **Drying rack** (gaps H2, `machinery/smartfridge.dm:557`, `/obj/machinery/smartfridge/drying/rack`, `icons/obj/machines/smartfridge.dmi`
  states `drying-rack`, `drying-rack-filled`, `drying-rack-drying`): a wooden rack with what dries on it. Done
  (`machines/required_models/drying_rack`): its shelves fill and its heater plays the drying frames.
- **Disk compartmentalizer** (gaps H2, `machinery/smartfridge.dm:813`, `/obj/machinery/smartfridge/disks`, `smartfridge.dmi` states `disktoaster`,
  `disktoaster-powered`, `disktoaster-panel`, `disktoaster-broken`): Done (`machines/required_models/disk_compartmentalizer`), its cover swings
  down with its panel open.
- **Smart fridge screen** (gaps H2, `machinery/smartfridge.dm`, the `<base>-powered` overlay of every smart fridge): the lit screen
  of a powered fridge is mapped to no part of the `smartfridge` model; meanwhile a powered fridge looks unpowered.

## Brain traumas

These are images only the patient sees (tg's client images, `override` on an invisible holder): the client draws them on their tile.

- **Phantom securitron** (gaps B1, `brain_damage/special.dm:417` `/obj/effect/client_image_holder/securitron`, `icons/mob/silicon/aibots.dmi`
  state `secbot-c`): the criminal trauma's Officer Beepsky that chases the patient; the securitron bot's model would serve. Meanwhile
  the client draws tg's sprite as a standing picture.
- **Stalking phantom** (gaps B1, `brain_damage/magic.dm:137`, `icons/mob/simple/lavaland/lavaland_monsters.dmi` state `curseblob`; the
  heretic's cosmic one `icons/mob/nonhuman-player/96x96eldritch_mobs.dmi` state `star_gazer`): a floating ghostly blob (and the
  star gazer, a large eldritch creature) that drifts toward the patient. Meanwhile the client draws tg's sprites as standing pictures.
- **Bluespace stream** (gaps B1, `brain_damage/special.dm:101`, `icons/effects/effects.dmi` state `bluestream`, with tg's wibbly filters;
  the fissure `bluestream_fade`): a shimmering blue rift the bluespace prophet steps through. Meanwhile the client draws tg's sprite.
- **Imaginary friend** (gaps B1, `brain_damage/imaginary_friend.dm:198`): a crewman in a random job's outfit (or the figment's
  `curseblob`), the human model dressed as the job would serve. Meanwhile the client draws the flat picture of a dressed dummy facing
  south (DV440).

## Antagonists

- **Changeling weapons** (gaps K1, `antagonists/changeling/powers/mutations.dm`, `icons/obj/weapons/changeling_items.dmi` and the
  inhands `icons/mob/inhands/antag/changeling_lefthand.dmi`, `_righthand.dmi`): the arm blade (`arm_blade`; the false armblade sting's
  and the synthetic arm blade use the same sprite), the tentacle (`tentacle`, with the projectile's end `tentacle_end` and the beam
  `tentacle` between the changeling and it) and the organic shield (`ling_shield`), held in place of a hand. Meanwhile the client
  draws tg's sprites in 2D.
- **Changeling armor and hive heads** (gaps K1, `mutations.dm`, the worn suit and helmet sprites of `lingarmor`, `lingarmorhelmet`,
  `hivehead` and `hivehead_legion`): the chitinous armor and helmet, the waxy hive head that releases bees and the boney legion hive
  head, worn. Meanwhile the client draws tg's worn sprites in 2D.
- **Headslug** (gaps K1, `basic/space_fauna/changeling/headslug.dm`, the basic mob icon states `headslug` and
  `headslug_dead`): the small fragile creature a changeling bursts into with Last Resort; it crawls and lays an egg in a corpse.
  Meanwhile the client draws tg's sprite in 2D.
- **Changeling egg** (gaps K1, `antagonists/changeling/headslug_eggs.dm`, the organ's `innards` sprite): the twitching egg a headslug
  plants in a corpse, an organ seen when it is taken out. Meanwhile the client draws tg's sprite in 2D.

## Xenomorphs

The xenomorphs are carbon mobs drawn from tg's sheets (gaps X5, `modules/mob/living/carbon/alien/`): the client draws each as tg's
sprite, a standing picture turned by its `dir`, until its model exists.

- **Adult castes** (`icons/mob/nonhuman-player/alien.dmi`): the drone `aliend`, the sentinel `aliens`, the hunter `alienh`, each with
  the states `_dead`, `_husked` (burnt), `_unconscious`, `_sleep` (lying), `_pounce` (the hunter's leap readied), the acid and neurotoxin
  drool overlay `alienspit`, the handcuff overlay `aliencuff`; the hunter's leap `alienh_leap` on `alienleap.dmi` (a bigger sheet, offset
  -8 px). A rigged humanoid xenomorph (digitigrade legs, a long tail, the elongated head) would serve every caste with its own skin and
  size; it needs the lying, crawling, pouncing and dying animations and the claw attack.
- **Royals** (`icons/mob/nonhuman-player/alienqueen.dmi`, 64 px, `pixel_x -16`): the praetorian `alienp` and the queen `alienq` with the
  same states, the large drool `alienspit_q`/`_p`, the large cuffs `aliencuff_q`/`_p` and the inhands drawn on that sheet
  (`<item state><caste>_l`/`_r`). Larger than a human; the queen's tail is the separate `tail_xenomorph_queen.dmi` organ.
- **Larva** (`alien.dmi`): `larva0`, `larva1`, `larva2` by its growth, each with `_dead`, `_cuff`, `_stun` and `_sleep`. A small
  crawling worm; it hides under tables (its layer drops).
- **Facehugger** (`alien.dmi`, also worn on the face from `icons/mob/clothing/mask.dmi` and held from the inhand sheets): `facehugger`,
  `_thrown`, `_inactive`, `_dead`, `_impregnated`, and Lamarr. A spider-like crawler that leaps; worn, it wraps the face (a mask model).
- **Eggs** (`alien.dmi`): `egg_growing`, `egg` (grown), `egg_hatched`, and the `egg_opening` flick. An egg the size of a crate's half,
  opening into four petals.
- **Resin** (`icons/obj/smooth_structures/alien/`): the resin wall (`resin_wall.dmi`, a smoothing wall), the resin membrane
  (`resin_membrane.dmi`, a translucent smoothing wall), the weeds (`weeds1`-`weeds3.dmi`, a floor covering that smooths with walls) and
  the glowing weed node (`weednode.dmi`, it lights blue), the nest (`nest.dmi`, a bed with the captive's `nestoverlay` over them), the
  gelatinous mound (`icons/obj/fluff/general.dmi` `gelmound`).
- **Infection images** (`alien.dmi` `infected0`-`infected5`): the embryo's stage the aliens see over a host (client images on the host);
  the host's `burst_lie` overlay while the larva bursts. Meanwhile the client draws them as pictures on the host's tile.
- **Inside a vent** (`ventcrawling.dm`): a ventcrawler's place is the pipe it is in; tg's pipe vision (the pipes' images over a dark
  screen) is not drawn (update_pipe_vision's row), so the first-person camera stands at the pipe under the floor. A view through the pipe
  network (a tunnel drawn along the pipes) is needed.

## Research

- **Experi-scanner** (gaps RD3, `experisci/handheld_scanner.dm`, `icons/obj/devices/scanner.dmi` states `experiscanner` and
  `experiscanner_closed`, the shut look of its suicide): the science department's handheld experiment scanner. Meanwhile the client
  draws tg's sprite.
- **Destructive analyzer's load and processing** (gaps RD3, `research/destructive_analyzer.dm`, `icons/obj/machines/research.dmi`
  states `d_analyzer_l`, the loaded item under its glass, and the 27-frame `d_analyzer_process` flick of a destruction): the model
  (`machines/science_wave/destructive_analyzer.glb`) has no loaded look and dips its red shield for half a second on both flicks
  (`d_analyzer_la`, `d_analyzer_process`).
- **Experimentor's load flick** (gaps RD4, `research/experimentor/experimentor.dm`, `icons/obj/machines/experimentator.dmi` state
  `h_lathe_load`, nine frames of the chamber taking an item): the model (`machines/science_wave/experimentor.glb`) has no loading
  animation; its work loop and service hatch are drawn.
- **Relics** (gaps RD4, `research/relics.dm`, `icons/obj/devices/artefacts.dmi` states `prototype1` to `prototype9` and `necrotech1`
  to `necrotech6`): the strange objects of maintenance, small handheld gadgets of odd shapes (prototype) and dark ruined trinkets
  (necrotech). Meanwhile the client draws tg's sprites.
- **Relic shields** (gaps RD4, `datums/components/shielded.dm`, `icons/effects/effects.dmi` states `at_shield1` and `at_shield2`
  over the holder, fading with the charges left): a yellow projected bubble around a person. Meanwhile the client draws tg's sprite
  over the mob.

## Circuits

- **The module duplicator's print and scan** (gaps CI1, `wiremod/core/component_printer.dm:444`, `:509`,
  `icons/obj/machines/wiremod_fab.dmi` states `module-fab-print` and `module-fab-scan`): the duplicator printing a saved circuit or
  module and scanning one put in. The model (`machines/production_wave/module_duplicator.glb`) has no print or scan animation; its
  service hatch is drawn.
- **The integrated circuit and its components** (gaps CI1, `icons/obj/devices/circuitry_n_data.dmi` states `integrated_circuit` and
  `component`, `icons/obj/science/circuits.dmi`): a handheld circuit board with a cell slot, and the small component chips. Meanwhile
  the client draws tg's sprites.
- **The compact remote and the controller** (gaps CI1, `wiremod/shell/compact_remote.dm`, `controller.dm`,
  `icons/obj/science/circuits.dmi` states `setup_small_simple` and `setup_small_calc`): a handheld one-button remote and a three-button
  controller. Meanwhile the client draws tg's sprites.
- **The circuit multitool and the USB cable** (gaps CI1, `wiremod/core/marker.dm`, `usb_cable.dm`, `icons/obj/devices/tool.dmi` state
  `multitool_circuit` with its `marked_overlay` and `multitool_circuit_flick`, `icons/obj/science/circuits.dmi` state `usb_cable`): the
  marking multitool, lit while it holds a mark, and a coiled cable. Meanwhile the client draws tg's sprites.
- **The shell assemblies** (gaps CI1, `wiremod/shell/shell_items.dm`, `icons/obj/science/circuits.dmi` states `setup_medium_box-open`,
  `setup_large-open`, `setup_medium_med-open`, `setup_stationary-open`, `setup_drone_arms-open`, `bci-open`, and the airlock and scanner
  gate assemblies' own icons): the open frames of the shells, finished with a screwdriver. Meanwhile the client draws tg's sprites.
- **The circuit shells** (gaps CI2, `wiremod/shell/`, `icons/obj/science/circuits.dmi`): the bot (`setup_medium_box`, a floor box with
  a push button), the money bot (`setup_large`, with a cash slot), the circuit drone (`setup_medium_med`, a small walking mob), the
  server (`setup_stationary`, a dense rack), the dispenser (`setup_drone_arms`, with its arms and a tray), the handheld scanner
  (`setup_small`) and keyboard (`setup_small_keyboard`), the circuit gun (`setup_gun`), the under-floor shell (`undertile`) and the
  wall mount with its frame (`wallmount`, `wallmount_assembly`). Each lights with its light component (`light_color`). Meanwhile the
  client draws tg's sprites.
- **The circuit scanner gate** (gaps CI2, `wiremod/shell/scanner_gate.dm`, `icons/obj/machines/scangate.dmi` state `scangate_black` with
  the `passive` and `scanning` overlays): tg's black gate. Meanwhile it is drawn by the security scanner gate's model
  (`station_props/station_extras/weapon_scanner_gate.glb`), whose scan field shows the two lines; a black variant is wanted.
- **The brain-computer interface and the circuit implant** (gaps CI3, `icons/obj/science/circuits.dmi` state `bci`; the implant case):
  a small cranial chip. Meanwhile the client draws tg's sprites.

## Assemblies

- **Assemblies** (gaps AS1, `modules/assembly/`, `icons/obj/devices/new_assemblies.dmi`): small handheld devices, each a model of its
  own: the timer (`timer`, with its lights `timer_light` lit one to three while counting and `timer_timing_l` in a holder), the
  proximity sensor (`prox`, `prox_scanning`, `prox_timing`), the health sensor (`health`), the infrared emitter (`infrared`, its lens
  lit when on: `infrared_on`), the voice analyzer (`voice`), the igniter (`igniter`), the signaler (`signaller`) and the mousetrap,
  open and armed (`mousetrap`, `mousetraparmed`). Meanwhile the client draws tg's sprites.
- **Assembly holder** (gaps AS1, `assembly/holder.dm`, `new_assemblies.dmi` `assembly_holder` with each assembly's `_left` half on its
  left and the same half mirrored on its right, and the halves' own overlays `_l`): two assemblies clipped together on a small frame;
  the model should take the two assemblies' models side by side. Meanwhile the client draws tg's sprite with both halves.
- **Infrared beam** (gaps AS1, `assembly/infrared.dm:115`, `icons/effects/beam.dmi` `infrared`, emissive): a thin red laser line from
  the emitter to the first thing it meets, seen only when the emitter is set visible. Meanwhile the client draws tg's beam segments.
- **Electric chair** (gaps AS1, `datums/components/electrified_buckle.dm`, `structures/electricchair.dm`, `icons/obj/chairs.dmi`
  `echair0` and the `echair_over` overlay, the shock kit's helmet and wires over the seat; tg's code uses no other state):
  a chair with a helmet on a stalk above the seat and cables to it, over any chair a shock kit is put on (the office and shuttle chairs
  and the sofas take the same overlay). Meanwhile the client draws the chair's model or sprite with tg's overlay in 2D.

## Devices and household odds

- **Tape recorder and tapes** (gaps DE1, `devices/taperecorder.dm`, `icons/obj/devices/voice.dmi` states `taperecorder_empty`,
  `taperecorder_idle`, `taperecorder_recording`, `taperecorder_playing`; `icons/obj/devices/circuitry_n_data.dmi` states `tape_white`,
  `tape_blue`, `tape_red`, `tape_yellow`, `tape_purple`, `tape_greyscale` (tinted by the dyed tape's colour), each with its `_reverse`
  side, and the `ribbonoverlay` of an unspooled tape): a handheld cassette recorder whose cassette window shows a tape or none and whose
  light shows recording (red) or playing (green), and a cassette that shows which side faces up and a loose ribbon hanging out.
  Meanwhile the client draws tg's sprites.
- **N-spect scanner** (gaps DE1, `items/inspector.dm`, `icons/obj/devices/scanner.dmi` state `inspector`): security's handheld
  contraband scanner with a cell cover. Meanwhile the client draws tg's sprite.
- **Gavel and gavel block** (gaps DE1, `items/courtroom.dm`, `icons/obj/weapons/hammer.dmi` states `gavelhammer`, `gavelblock`; the
  `carpenter_hammer` of `melee/misc.dm` beside them): the judge's wooden gavel and its sound block, and a carpenter's hammer.
  Meanwhile the client draws tg's sprites.
- **Red phone** (gaps DE1, `melee/misc.dm`, `icons/obj/devices/voice.dmi` state `red_phone`): the command's red telephone handset on
  its base. Meanwhile the client draws tg's sprite.
- **Pillows, the pillow suit and the mattress shield** (gaps DE1, `items/pillow.dm`, `icons/obj/bed.dmi` states `pillow_1` to `pillow_4`
  with and without their `_t` tag, `pillow_suit`, `mattress_shield`; the `pillow_hit` puff of `icons/effects/effects.dmi`): four
  patterned pillows whose tag can be torn off, a suit of pillows worn over the clothes, and a rolled mattress held as a shield (its
  fortified stance is a red pulsing outline on the holder, a filter the client draws). Meanwhile the client draws tg's sprites.
- **Skub** (gaps DE1, `items/skub.dm`, `icons/obj/maintenance_loot.dmi` state `skub`): the skub jar on its own (the tank holder's
  `holder_skub` is modelled already). Meanwhile the client draws tg's sprite.
- **Dice and dice bags** (gaps DE1, `items/dice.dm`, `icons/obj/toys/dice.dmi`: the bag `dicebag`, the dice `d1`, `d2`, `d4`, `d6`,
  `de6` (ebony), `spaced6`, `fudge`, `d8`, `d10`, `d00`, `d12`, `d20`, `d100`, `8bd20`, `4dd6`, each with its face overlays
  `STATE-RESULT`): every die needs its rolled face up, so the model shows the result the
  server sends (the overlay's state). Meanwhile the client draws tg's sprites.
- **Shooting targets** (gaps DE1, `items/shooting_range.dm`, `icons/obj/structures.dmi` states `target_h`, `target_s`, `target_q`,
  `target_c`; the `dent`, `scorch` and `light_scorch` marks of `icons/effects/effects.dmi` at the hit's pixel): standing cut-out
  targets of a man, a syndicate agent, a xenomorph and a clown, with bullet dents and laser scorches decalled where they were hit.
  Meanwhile the client draws tg's sprites with the marks as overlays.
- **Banners, the banner backpack and the godstaves** (gaps DE1, `items/religion.dm`, `icons/obj/banner.dmi` states `banner`,
  `banner_security`, `banner_medical`, `banner_science`, `banner_cargo`, `banner_engineering`, `banner_command`, `banner-red`,
  `banner-blue`; `backpack-banner`; `icons/obj/weapons/staff.dmi` states `godstaff-red`, `godstaff-blue`): the department banners on
  their poles, held raised, a backpack carrying a banner, and the two deities' staves. Meanwhile the client draws tg's sprites.
- **Odd melee weapons** (gaps DE1, `melee/misc.dm`, `melee/claymore.dm`: `icons/obj/weapons/sword.dmi` states `beesword`, `sord`,
  `claymore`, `claymore_old` (the weak claymore), `carrot`, `shortsword`, `gladius`; `icons/obj/weapons/whip.dmi` state `whip`;
  `icons/obj/service/kitchen.dmi` states `roastingstick` (retracted and extended) and its `roastingstick_sausage` overlay;
  `icons/obj/weapons/staff.dmi` states `bambostaff0`, `bambostaff1` (wielded); the cleric mace in its materials): a bee-striped sword, a
  wooden sord, the old claymores and the gladius, the curator's leather whip, a telescopic roasting stick with a sausage on its tip,
  a bamboo bo staff held in one or two hands, a mace of two materials. Meanwhile the client draws tg's sprites.

## Buildable structures

- **Bonfire** (gaps ST4, `structures/bonfire.dm`, `icons/obj/service/hydroponics/equipment.dmi` states `bonfire`, `bonfire_on_fire`,
  `bonfire_warm`, the `bonfire_grill` overlay and the `bonfire_rod` stake underlay): a ring of logs, burning or not, with a grill on top or
  a stake rising from it. Meanwhile the client draws tg's sprites.
- **Fireplace** (gaps ST4, `structures/fireplace.dm`, `icons/obj/fluff/fireplace.dmi` state `fireplace` and its overlays
  `fireplace_fire0` to `fireplace_fire4` and `fireplace_glow`): a stone brick fireplace set into a wall, its fire in five sizes.
  Meanwhile the client draws tg's sprites.
- **Mineral doors** (gaps ST4, `structures/mineral_doors.dm`, `icons/obj/doors/mineral_doors.dmi` states `metal`, `silver`, `gold`,
  `uranium`, `sandstone`, `plasma`, `diamond`, `wood`, `paperframe`, each with its `open` state and its `opening` and `closing` flicks):
  a slab door of each material that swings open, the plasma and diamond ones see-through. Meanwhile the client draws tg's sprites.
- **Guillotine** (gaps ST4, `structures/guillotine.dm`, `icons/obj/guillotine.dmi` states `guillotine_raised`, `guillotine_drop`,
  `guillotine`, `guillotine_raise` and the `guillotine_bloody_overlay`, `_bloodier_`, `_bloodiest_` overlays): the frame, its blade
  raised, falling and down, the stocks a strapped human's head fits into, and its blood by the kill count. Meanwhile the client draws
  tg's sprites.
- **Platforms** (gaps ST4, `structures/platform.dm`, `icons/obj/smooth_structures/platform/window_frame_*.dmi` and their
  `frame_faces/`): a raised, smoothed half-height platform of each material. Meanwhile the client draws tg's sprites.
- **Loom and steps** (gaps ST4, `structures/loom.dm`, `icons/obj/service/hydroponics/equipment.dmi` state `loom`;
  `structures/steps.dm`, `icons/obj/small_stairs.dmi` state `iron`): a wooden weaving loom, and a short set of steps facing one way.
  Meanwhile the client draws tg's sprites.

## Storage

- **Can ring** (gaps SR1, `storage/sixpack.dm`, `icons/obj/storage/storage.dmi` states `canholder0` to `canholder6`): a plastic
  six-pack ring holding as many cans as it shows. Meanwhile the client draws tg's sprites.
- **Paper sack** (gaps SR1, `boxes/food_boxes.dm`, `icons/obj/storage/paperbag.dmi` states `paperbag_None`,
  `paperbag_NanotrasenStandard`, `paperbag_SyndiSnacks`, `paperbag_Heart`, `paperbag_SmileyFace` and their `_closed` kinds): a brown
  paper lunch bag with a pen-drawn design, open or folded shut. Meanwhile the client draws tg's sprites.
- **Gum packets and wrappers** (gaps SR1, `boxes/food_boxes.dm`, `icons/obj/storage/gum.dmi` states `bubblegum_generic`,
  `bubblegum_wake_up`, `bubblegum_nicotine`, `bubblegum_happiness`, `bubblegum_bubblegum`; `icons/obj/food/food.dmi` state
  `bubblegum_wrapper` with the gum inside as its overlay): a small packet of four wrapped strips. Meanwhile the client draws tg's
  sprites.
- **Organ transport box** (gaps SR1, `storage/medkit.dm:412`, `icons/obj/storage/case.dmi` states `organbox`, `organbox-working`):
  a cooled case whose lights show it cooling while it has coolant. Meanwhile the client draws tg's sprites.
- **Test tube rack** (gaps SR1, `storage/medkit.dm:508`, `icons/obj/medical/chemical.dmi` state `rack` with its `rack1` to `rack8`
  tube overlays): a small rack holding up to eight test tubes. Meanwhile the client draws tg's sprites.
- **Sheaths** (gaps SR1, `storage/belt.dm:680`, `icons/obj/clothing/belts.dmi` states `sheath`, `grass_sheath`, `gladius_sheath`,
  `katana_sheath`, `ninja_sheath` and their `-full` kinds, worn on the belt or the suit's storage): a scabbard empty or with the hilt
  of its blade showing. Meanwhile the client draws tg's sprites.
- **Belts of this package** (gaps SR1, `storage/belt.dm`, `icons/obj/clothing/belts.dmi` states `championbelt`, `soulstonebelt` (the
  wand belt), `bandolier` (the wand bandolier), `militarywebbing` (the snack rigs), the abductor's `belt` of
  `icons/obj/antags/abductor.dmi`): worn on the waist. Meanwhile the client draws tg's sprites.
- **Bag of holding and the MEAT backpack** (gaps SR1, `storage/backpack.dm`, `icons/obj/storage/backpack.dmi` states
  `bag_of_holding`, `bag_of_holding-inert`, `meatmeatmeat`): a backpack with a bluespace glow, its unpowered frame, and a backpack of
  meat. Meanwhile the client draws tg's sprites.
- **Bags of this package** (gaps SR1, `storage/bags.dm`, `storage/garment.dm`, `storage/basket.dm`: `icons/obj/storage/storage.dmi`
  `garment_bag`, `moneybag` and `moneybagalt`; `icons/obj/weapons/bows/quivers.dmi` `quiver` (the arrow and harpoon quivers);
  `icons/obj/mining.dmi` `sheetsnatcher`; `icons/obj/storage/basket.dmi` `basket`): a garment bag, a money sack, an arrow quiver, a sheet snatcher and a wicker basket. Meanwhile the client draws tg's sprites.
- **Fishing toolbox and ammo boxes** (gaps SR1, `toolboxes/fishing.dm`, `toolboxes/weapons.dm`, `icons/obj/storage/toolbox.dmi` states
  `teal`, `ammobox`): the teal fishing tackle box and the military ammo can. Meanwhile the client draws tg's sprites (the other
  toolboxes' models are not made yet either).

## Clothing leftovers

- **Welding goggles and the welding mask** (gaps CL1, `glasses/_glasses.dm`, `icons/obj/clothing/glasses.dmi` states `welding-g`,
  `welding-gup`; `masks/gasmask.dm`, `icons/obj/clothing/masks.dmi` states `weldingmask`, `weldingmaskup`; worn from
  `icons/mob/clothing/eyes.dmi` and `mask.dmi`): goggles and a skull-faced mask with a dark visor flipped down over the eyes or up on
  the brow. Meanwhile the client draws tg's held and worn sprites in 2D and the darkened view (the "tint" fullscreen).
- **Odd hats** (gaps CL1, `head/`: `icons/obj/clothing/head/costume.dmi`, `hats.dmi`, `helmet.dmi`, `utility.dmi` and their worn
  sheets): the traffic cone (`cone`), the top hat (`tophat`), the tin foil hat (`foilhat`, scorched when warped), the birthday
  cake hat lit and unlit (`hardhat0_cakehat`, `hardhat1_cakehat`, the energy cake's `hardhat0_energycake`, `hardhat1_energycake`), the
  carved pumpkin and blumpkin lit and unlit (`hardhat0_pumpkin`, `hardhat1_pumpkin`, `hardhat0_blumpkin`, `hardhat1_blumpkin`), the
  collectable hats (the `collectable` states), the wig drawn as a hairstyle, the kitty ears in the hair's colour, the garlands, the
  head mirror, the propeller hat spinning or still (worn `propeller_hat_on`, `propeller_hat`), the whoopee cushion, the lobster hat, the
  cardborg helmet, the helmet of justice with its lights on and off, the knight's, Russian, marine and SWAT helmets, the holymelon.
  Meanwhile the client draws tg's held and worn sprites in 2D.
- **Animal masks and odd masks** (gaps CL1, `masks/animal_masks.dm`, `masks/costume.dm`, `masks/gondola.dm`, `masks/bandana.dm`,
  `icons/obj/clothing/masks.dmi` and `mask.dmi`): the pig, horse, cow, bee, raven, jackal, fox, frog, rat, bear and tribal masks, the
  gondola mask, the emotion and kitsune masks, the tiki mask's four faces, the face scarf up and down. Meanwhile tg's sprites in 2D.
- **Odd shoes and gloves** (gaps CL1, `shoes/`, `gloves/`): the ducky shoes, the griffon and bronze boots, the glow shoes, the jester
  shoes, the galoshes and absorbent galoshes, the gunboots, the jump, rocket and jet boots (`jetboots_active`), the Kindle Kicks lit in
  turn, the winter, Russian and pirate boots, the H.A.U.L. gauntlets, the race gloves, the bone bracers, the spray-on gloves and their
  can. Meanwhile tg's sprites in 2D.
- **Costume suits** (gaps CL1, `suits/costume.dm`, `suits/armor.dm`, `suits/shirt.dm`, `suits/ghostsheet.dm`): the cardborg suit,
  the bear suit, the carp, shark and shork costumes with their hoods, the ghost sheet, the well-worn shirts (shrinking in the wash), the
  balloon vest, the laser tag vests (`bluetag`, `redtag`), the reflective, SWAT, heavy, cuirass and holymelon armours, the zip-up
  hoodie with its hood down on the back (`_hood`, `_hood_t`). Meanwhile tg's held and worn sprites in 2D.

## Weaponry

- **Spears and lances** (gaps WP1, `weaponry/melee/spear.dm`, `icons/obj/weapons/spear.dmi` states `spearglass0`, `spearplasma0`, `speartitanium0`, `spearplastitanium0`, `spearbomb0`, `bone_spear0`, `bamboo_spear0`, `military_spear0`, `dragoonpole0`, `speardragon0`, `speardragonraw0`, `wireprod`, the custom tip's `spearblank0` with its tip overlay; the pillow lance `pillow_lance0`): a long pole with a tip, the explosive lance with an IED taped to it. Meanwhile the client draws tg's held and worn sprites in 2D.
- **Headpike** (gaps WP1, `structures/headpike.dm`, states `headpike`, `headpike-bone`, `headpike-bamboo`, `headpike-military`, with the head as an overlay): a spear stood upright with a head on its tip.
- **IED and the halved pipe** (gaps WP1, `grenades/ghettobomb.dm`, states `pipebomb` with the trigger's and the cable's states, `slicedapart`): a length of pipe stuffed and wired to a trigger.
- **Melee weapons** (gaps WP1: `chainsaw.dm` `chainsaw` on and off, `chainsawdual`, the doomslayer's; `sabre.dm` `sabre`, the parsnip sabre; `katana.dm`; `chainofcommand.dm`; `baseball_bat.dm` `baseball_bat`, `baseball_bat_metal`, `baseball_bat_home`, `baseball_bat_brit`; `high_frequency_blade.dm`; `supermatter_sword.dm`; the stunsword, teleprods and nunchaku of `baton.dm`): Meanwhile tg's held sprites in 2D.
- **Shields** (gaps WP1, `shields.dm`: the strobe shield `flashshield`, in hand `flashshield_flash` when it flashes, the ballistic shield `ballistic`): Meanwhile tg's sprites in 2D.
- **Canes** (gaps WP1, `items/tools/medical/cane.dm`: `cane`, `stick`, the crutches `crutch_med` and `crutch_wood`, the white cane `cane_white` folded and extended): Meanwhile tg's sprites in 2D.

## Toys

- **Toy mechs** (gaps TY1, `toy_mechs.dm`, `icons/obj/toys/toy.dmi` states `ripleytoy`, `ripleymkiitoy`, `haulertoy`, `clarketoy`,
  `odysseustoy`, `gygaxtoy`, `durandtoy`, `savannahivanovtoy`, `phazontoy`, `honktoy`, `darkgygaxtoy`, `maulertoy`, `darkhonktoy`,
  `deathripleytoy`, `reticencetoy`, `maraudertoy`, `seraphtoy`, `firefightertoy`; the base `fivestarstoy`): nineteen small plastic
  exosuits that stand on a table and fight each other. Meanwhile the client draws tg's sprites.
- **Toy swords** (gaps TY1, `icons/obj/weapons/transforming_energy.dmi` states `e_sword`, `e_sword_on_red`, `_blue`, `_green`,
  `_purple`, `_rainbow`; the double-bladed toy's `dualsaber` states): a plastic hilt whose glowing blade slides out in its colour, two
  of them joined end to end. Meanwhile tg's held sprites in 2D.
- **Action figures and talking toys** (gaps TY1, `icons/obj/toys/toy.dmi`: the 41 action figures (`cmo`, `assistant`, `captain` and the
  rest), the owl and griffin (`owlprize`, `griffinprize`), the toy AI (`AI`), the toy Codex Gigas (`demonomicon`), the xeno figure
  (`toy_xeno`, `toy_xeno_used`), the ventriloquist dummy (`puppet`), the squeaky brain (`brain-old`), the foam finger, the replica
  katana): small figures on a base, each its own. Meanwhile tg's sprites.
- **Gadget toys** (gaps TY1, `icons/obj/toys/toy.dmi` and `icons/obj/toys/hourglass.dmi`): the cap gun (`revolver`) and its cap box
  (`357OLD-0` to `357OLD-7`), the windup toolbox with its latch (`single_latch`, `single_latch_open`), the toy nuke (`nuketoyidle`,
  `nuketoy`, `nuketoycool`), the big red button (`bigred`), the broken radio, the steampunk watch (`dread_ipad`), the Codex Cicatrix toy
  opening and closing (`book`, `book_open`, `book_opening`, `book_closing`), the Intento with its four lit quadrants (`blank`, `help`,
  `disarm`, `grab`, `harm`), the singularity toy, the snap pops, the hourglass with its sand running down (`hourglass_idle`,
  `hourglass_flip`, `hourglass_1` to `hourglass_7`). Meanwhile tg's sprites.
- **Balloons** (gaps TY1, `icons/obj/toys/balloons.dmi`, `icons/obj/weapons/hand.dmi`): the water balloon empty and full
  (`balloon_red-e`, `waterballoon`), the latex glove blown up on a string and burst (`latexballoon`, `latexballoon_blow`,
  `latexballoon_bursted`). Meanwhile tg's sprites (the balloons on strings and the balloon animals came with J3 and G3b).

## Structures' leftovers and the mines

- **Landmines** (gaps ST5, `effects/mines.dm`, `icons/obj/weapons/grenade.dmi` states `landmine` and `landmine-inactive`, and the mine
  spawner `landmine_spawner`): a flat pressure-plate mine lying on the floor, its light off while it arms (the capspawn mine glows red
  once armed). Meanwhile the client draws tg's sprites flat on the floor.
- **Magic traps** (gaps ST5, `structures/traps.dm`, `icons/obj/service/hand_of_god_structures.dmi` states `trap-shock`, `trap-fire`,
  `trap-frost`, `trap-earth`, `trap-cult`, `ward`): a rune circle drawn on the floor, nearly invisible until it flares. Meanwhile tg's
  sprites, alpha as tg sets it.
- **Tripwire** (gaps ST5, `structures/tripwire.dm`, `icons/obj/tripwire.dmi` states `tripwire_post`, `tripwire_post_tied`,
  `tripwire_1` to `tripwire_3` and the `tripwire_cable` item): a sturdy stanchion and the heavy cable strung low between two of them.
  Meanwhile tg's sprites.
- **Mystery box** (gaps ST5, `structures/mystery_box.dm`, `icons/obj/storage/crates.dmi` states `wooden`, `woodenopen` and the
  treasure chest's `treasure`, `treasureopen`): a wooden crate whose lid opens for the rising prize. Meanwhile tg's sprites, the prize
  drawn as its item's sprite rising out of it.
- **Voting box** (gaps ST5, `structures/votingbox.dm`, `icons/obj/storage/box.dmi` states `votebox_maint`, `votebox_active`): a box
  with a ballot slot on top and its active light. Meanwhile tg's sprites.
- **Signboards** (gaps ST5, `signboards/_signboard.dm`, `holosign.dm`, `icons/obj/signboards.dmi` states `sign`, `sign_blank`,
  `holographic_sign`, `_greyscale`, `_emag`): the folding A-frame sign and the holographic sign, with their text written on the board
  (the client draws no text over it yet, not-translated.md 213). Meanwhile tg's sprites.
- **Maintenance oddities** (gaps ST5, `structures/maintenance.dm`, `icons/obj/structures.dmi` states `moisture_trap`, `steam_vent`,
  `steam_vent_off`; `icons/obj/antags/cult/structures.dmi` states `altar`, `altar_off` with the `altar_pants1` to `_3` overlays): the
  moisture trap, the floor steam vent and the pants altar with its pants taking shape. Meanwhile tg's sprites.
- **Fences** (gaps ST5, `structures/fence.dm`, `icons/obj/fence.dmi` states `straight`, `straight_cut2`, `straight_cut3`, `end`,
  `corner`, `post`, `door_closed`, `door_opened`): a chain link fence section, cut open in two sizes, and its door. Meanwhile tg's
  sprites.
- **Gun lockers** (gaps ST5, `structures/guncase.dm`, `icons/obj/storage/closet.dmi` states `shotguncase`, `ecase`, `wt550case`
  with their `_open` and `_door` overlays and the gun overlays `shotgun`, `ecase`, `wt550`): a tall locker with its guns standing in
  a row. Meanwhile tg's sprites.
- **Toilet bong, life candle, fans** (gaps ST5, `toiletbong.dm` `icons/obj/watercloset.dmi` `toiletbong` and its `_overlay`;
  `life_candle.dm` `icons/obj/candle.dmi` `candle1`, `candle1_lit`; `fans.dm` `icons/obj/mining_zones/survival_pod.dmi` `fans`,
  `fan_tiny`). Meanwhile tg's sprites.
- **Chairs and the handrail** (gaps ST5, `beds_chairs/chair.dm`, `icons/obj/chairs.dmi` states `brass_chair`, `plastic_chair`,
  `folded_chair`; `icons/obj/handrail.dmi` `handrail`): the brass office chair on its cogs, the folding plastic chair and the wall
  handrail. Meanwhile tg's sprites.
- **Paper frames, rolling table, material tables and false walls** (gaps ST5, `window.dm`, `icons/obj/smooth_structures/paperframes.dmi`
  with the `paper-whole` and `paper-torn` overlays; `tables_racks.dm`, `rollingtable.dmi`, `table_greyscale.dmi`; `false_walls.dm`,
  `icons/turf/walls/material_wall.dmi`): a shoji paper screen (opaque while whole), a wheeled table, a table and a false wall tinted by
  their material. Meanwhile tg's sprites.
- **Shipping containers and billboards** (gaps ST5, `containers.dm`, `icons/obj/fluff/containers.dmi`; `billboard.dm`,
  `icons/obj/fluff/billboard.dmi`): three-tile-wide containers in their companies' liveries and roadside billboards. Meanwhile tg's
  sprites.
- **Chess pieces, the eye chart, the bouncy castle, the outfit wardrobe** (gaps ST5, `chess.dm` `icons/obj/toys/chess.dmi`;
  `signs/sign_eyechart.dm` `icons/obj/signs.dmi` `eyechart`; `hunter_gear.dm` `icons/obj/toys/bouncy_castle.dmi`; `outfit_wardrobe.dm`
  `fullcabinet`, `fullcabinet_open`). Meanwhile tg's sprites.
- **Petrified statue** (gaps ST5, `petrified_statue.dm`): the petrified mob is drawn as tg draws it, the mob's own overlays copied onto
  a blank icon and greyed by a colour matrix; a human's 3D model greyed into marble would serve. Meanwhile the copied 2D overlays.

## Guns

- **Energy rifles and pistols** (gaps GU1, `guns/energy/laser.dm`, `energy_gun.dm`, `pulse.dm`, `stun.dm`, `recharge.dm`,
  `icons/obj/weapons/guns/energy.dmi` states `retro`, `laser_soulful`, `hellgun`, `caplaser`, `lasercannon`, `xray`, `mini` with the
  `flashlights.dmi` `mini-light` overlay, `decloner` (the practice phaser), `nucgun` with its reactor overlays `nucgun_fail_0` to `_3`,
  `fisher`, `pulse`, `pulse_carbine`, `pulse_pistol`, `m1911`, `advtaser`, each with tg's charge overlays; `wide_guns.dmi`
  `assault_laser`; `ballistic.dmi` `cshotgun`, `cshotgunc` (the energy shotguns)): rifles, carbines and pistols, each its own, their
  charge shown on the body. Meanwhile the client draws tg's held and worn sprites in 2D.
- **Special energy guns** (gaps GU1, `guns/energy/special.dm`, `beam_rifle.dm`, `energy.dmi` states `plasmacutter`, `adv_plasmacutter`,
  `wormhole_projector` with `wormhole_projector1` (blue) and `wormhole_projector2` (orange), `gravity_gun`, `photon`, `esniper` (the
  Event Horizon), `ioncarbine`, `meteor_gun`, `xray` (the Mind Flayer), `flora`; `wide_guns.dmi` `tesla` and `tesla_unfolded` with
  their `_charge0` to `_charge2` overlays and the 64x in-hands; `ballistic.dmi` `revolver` (the marksman revolver)): the plasma cutter
  that doubles as a welder, the wormhole projector, the gravitational manipulator, the tesla cannon with its folding stock, the photon
  cannon. Meanwhile tg's sprites in 2D.
- **Dueling pistols and their case** (gaps GU1, `guns/energy/dueling.dm`, `energy.dmi` `dueling_pistol` with the setting overlays
  `duel_red`, `duel_green`, `duel_blue`; the case's `medalbox`, `medalbox+l`, `medalboxopen`, `medalbox+b`): two ornate pistols in a
  velvet-lined case. Meanwhile tg's sprites.
- **Laser gatling gun** (gaps GU1, `guns/energy/laser_gatling.dm`, `icons/obj/weapons/guns/minigun.dmi` states `holstered`,
  `notholstered`, `minigun_spin`, in-hand `minigun`): a bulky back-mounted power pack with a cable to a multi-barrel laser.
  Meanwhile tg's sprites.
- **Crank guns** (gaps GU1, `guns/energy/crank_guns.dm`, `energy.dmi` `musket`, `musket_prime`, `repeater`, `smoothbore`,
  `smoothbore_prime`, `infernopistol`, `cryopistol` with their charge overlays, in-hand `musketcharged` and `musket_primecharged`): a
  wooden laser musket with its side crank, a crude wood-and-cardboard pistol, the paired thermal pistols with their nanite canisters.
  Meanwhile tg's sprites.
- **Bolt-action rifles and pipe guns** (gaps GU1, `guns/ballistic/rifle.dm`, `icons/obj/weapons/guns/wide_guns.dmi` states `sakhno`,
  `sakhno_sawn` (the obrez), `sakhno_tactifucked`, `zhihao`, `jezail`, `pipegun`, `pipepistol`, `regal_pipegun`, `regal_pipepistol`,
  `sks`): long wooden rifles with a working bolt, sawn off short, and the scrap-built pipe guns. Meanwhile tg's sprites in 2D.
- **Pistols and the hook shotgun** (gaps GU1, `guns/ballistic/pistol.dm`, `shotgun.dm`, `icons/obj/weapons/guns/ballistic.dmi` states
  `reagle` (the regal Desert Eagle), `flatgun` (the stickman's), `doorhickey` (the plastic doohickey), `hookshotgun`). Meanwhile tg's
  sprites.
- **Bows and arrows** (gaps GU1, `guns/ballistic/bows/`, `icons/obj/weapons/bows/bows.dmi` states `bow`, `bow_drawn`, `holybow`,
  `holybow_drawn`, `ashenbow`, `ashenbow_drawn`; `icons/obj/weapons/bows/arrows.dmi` `arrow`, `arrow_sticky`, `arrow_poison`,
  `arrow_holy`, `arrow_blazing`, `arrow_plastic`, `arrow_ashen` and their `_drawn` overlays): a bow whose string bends back when drawn,
  the nocked arrow on it. Meanwhile tg's sprites.
- **Donksoft guns and magazines** (gaps GU1, `guns/ballistic/toy.dm`, `boxes_magazines/external/toy.dm`, `icons/obj/toys/toy.dmi`
  `foamcrossbow` and the toy shotgun; the magazines' `smg9mm-full`, `smg9mm-empty`, `c20r45-toy` and `c20r45-riot-base` with their
  `c20r45-foam-<colour>-<n>` darts, `a7mm-<n>`): bright plastic foam guns, the darts shown in the magazine. Meanwhile tg's sprites.
- **Rounds** (gaps GU1, the harpoon, the healium rod `rod_healium`, the .310 casing): Meanwhile tg's sprites.
- **Firing pins** (gaps GU1, `pins.dm`, `icons/obj/devices/gunmod.dmi` `firing_pin_dna`, `firing_pin_explorer`, the gold-tinted paywall
  pin): small parts in the hand. Meanwhile tg's sprites.
- **Energy projectiles** (gaps GU1, `icons/obj/weapons/guns/projectiles.dmi` `scatterlaser` (scaled as it flies), `flare`, `u_laser`,
  `pulse1_bl`, `declone` (tinted for the duel), `coinshot`, `solarflare`, `tesla_projectile`, `plasmacutter`, `infernoshot`,
  `cryoshot`; the gravity bolts' `icons/effects/effects.dmi` `chronofield`, tinted; the meteor gun's `icons/obj/meteor.dmi` `small1`;
  the banana of `hydroponics/harvest.dmi`; the dueling chaff `effects.dmi` `shield-old`, tinted): glowing bolts and beams, the flipped
  coin. Meanwhile tg's sprites, billboarded.

## Devices

- **Boombox and music tapes** (gaps DV2, `devices/boombox/boombox.dm`, `icons/obj/devices/voice.dmi` states `boombox`, `boombox_on`,
  in-hand `boombox` and `boombox_swag` (worn on the shoulder); the tapes of `music_tapes.dm`, `icons/obj/devices/circuitry_n_data.dmi`
  `tape_white`, `tape_red`, ...): a chunky portable cassette player whose speakers light up while it plays, carried by its handle or
  hoisted on the shoulder. Meanwhile the client draws tg's sprites.
- **Hydro-vaporizer** (gaps DV2, `devices/vaporizer.dm`, `icons/obj/devices/tool.dmi` state `vaporizer`): a small belt-worn mister with
  a cell hatch. Meanwhile tg's sprite.
- **Forcefield projector and its field** (gaps DV2, `devices/forcefieldprojector.dm`, `icons/obj/devices/tool.dmi` state
  `signmaker_forcefield`; the field `icons/effects/effects.dmi` state `forcefield`): a handheld emitter and the glowing translucent
  barrier it raises on a tile. Meanwhile tg's sprites.
- **Pressure plate** (gaps DV2, `devices/pressureplates.dm`, `icons/obj/fluff/puzzle_small.dmi` state `pressureplate`, tg's
  `icons/turf/floors.dmi` `pp_overlay` over the floor tile that hides it is not drawn yet, DV528): a flat plate lying on the floor.
- **Table clock** (gaps DV2, `devices/table_clock.dm`, `icons/obj/fluff/general.dmi` states `table_clock`, `table_clock_broken`): a
  ticking desk clock and the same with its face smashed. Meanwhile tg's sprites.
- **Quantum spin inverter** (gaps DV2, `devices/swapper.dm`, `icons/obj/mining_zones/artefacts.dmi` states `swapper`, `swapper-linked`):
  a handheld alien-looking artefact that glows when linked. Meanwhile tg's sprites.
- **Small devices** (gaps DV2): the desynchronizer (`icons/obj/devices/syndie_gadget.dmi` `desynchronizer`), the hemoanalytic scanner
  (`icons/obj/devices/scanner.dmi` `bloodscanner`), the slime scanner (`scanner.dmi` `slime_scanner`), the quantum keycard (its
  greyscale `quantum_keycard_gags`, tinted by its department), the pipe painter (`icons/obj/service/bureaucracy.dmi` `labeler1`), the
  anomaly neutralizer (`icons/obj/devices/tool.dmi` `neutralyzer`), the anomaly releaser (`syndie_gadget.dmi` `anomaly_releaser`,
  `anomaly_releaser_used`) and the polycircuit aggregate (`circuit_mess`). Meanwhile tg's sprites.
- **Implant pad and mindshield implanter** (gaps DV2, `implants/implantpad.dm`, `icons/obj/devices/tool.dmi` states `implantpad-0`,
  `implantpad-1` (a case in it); `implants/implantchair.dm`, `icons/obj/machines/implant_chair.dmi` states `implantchair`,
  `implantchair_open`, `implantchair_occupied` with the `ready` light overlay): a handheld pad with a case slot, and an enclosed chair
  whose door closes on its occupant. Meanwhile tg's sprites.
- **Skillsoft station** (gaps SL1, `library/skill_learning/skill_station.dm`, the implant chair's `icons/obj/machines/implant_chair.dmi`
  states `implantchair`, `implantchair_open`, `implantchair_occupied` with the `working` overlay while it implants or extracts): the
  implant chair's enclosed seat with a working light, in the library. Meanwhile tg's sprites.
- **Skill capes** (gaps SL1, `suits/cloaks.dm` skill rewards, `icons/obj/clothing/neck.dmi` and its worn states `gamercloak`,
  `cleanercloak`, `minercloak`, `playercloak` and the fishing and athletics capes): long capes worn at the neck, one colour and badge per
  skill. Meanwhile tg's sprites.
- **Rapid wiring device** (gaps DV2, `rcd/RWD.dm`, `icons/obj/tools.dmi` states `rcl-0` and `rwd-10-layerN`, `rwd-20-layerN`,
  `rwd-30-layerN` for the three cable layers): a reel gun whose spool fills with cable of the chosen layer's colour. Meanwhile tg's
- **Machine wand and its bug** (gaps DV2, `machine_wand.dm`, `icons/obj/antags/syndicate_tools.dmi` state `weakpoint_locator`; the bug
  `icons/effects/effects.dmi` states `fly` and the `fly-surrounding` swarm over a controlled machine): a remote and a little fly that
  buzzes to the machine and settles around it. Meanwhile tg's sprites.
- **Portable wind turbine** (gaps DV2, `wind_turbine.dm`, `icons/obj/wind_turbine.dmi` states `icon`, `base_turbine` (worn) and the
  rotor frames `rotor_0` to `rotor_3`, the `baton` overlay of a hung baton; in-hand 64x64 `wind_turbine`): a backpack frame with a
  rotor whose blades turn as its wearer walks. The rotor's turn should be a rotation of the blade mesh by the frame the server sends.
- **Floppy disks and their stacks** (gaps DV2, `floppy_disk.dm`, `icons/obj/devices/floppy_disks.dmi` states `datadisk0` to
  `datadisk10` with the sticker overlays `o_empty`, `o_one` ... `o_Z`, `o_text1` to `o_text3`, `o_code`, `o_syndicate`): a 3.5-inch
  floppy whose label shows its sticker, and the pile of disks a stack draws two pixels apart. Meanwhile tg's sprites.
- **Flashlight odds** (gaps DV2, `devices/flashlight.dm`: the flashdark `flashdark`, the space furnace lantern `space_lamp` (worn,
  wielded, its `-on` states), the penlight's medical holosign `medi_holo`): a dark-casting torch, a two-handed lantern with a
  pyroclastic core, and a floating green cross over a patient. Meanwhile tg's sprites.

## Items' odds

- **Ruin puzzles** (gaps OD1, `items/puzzle_pieces.dm`): the hologrid plate (`icons/obj/fluff/puzzle_small.dmi` `lasergrid`, its
  `lasergrid_a` flash and the spent `lasergrid_full`) that its holobox burns into (`laserbox_burn`); the light mechanism
  (`puzzle_small.dmi` `light_puzzle` with nine `light_lit` lamps in a 3x3 grid, each a clickable lamp); the wall panels
  (`icons/obj/machines/wallmounts.dmi` `lockdown0`/`lockdown1` button, `keycardpad0`/`keycardpad1`, `passpad0`/`passpad1`); the shield
  gate (`icons/effects/effects.dmi` `wave2`, a wall of light) and the one-way gate (`icons/obj/structures.dmi` `oneway`, `oneway-off`);
  the dotted board (`puzzle_small.dmi` `puzzle_dots` with coloured `puzzle_dot_single` dots). Meanwhile the client draws tg's sprites.
- **Inhaler and canisters** (gaps OD1, `reagent_containers/inhaler.dm`, `icons/obj/medical/chemical.dmi` `inhaler_generic`,
  `inhaler_medical`, the canisters `canister_generic`, `canister_medical` shown under the inhaler): a small puffer with its metal
  canister. Meanwhile tg's sprites.
- **Jerrycans** (gaps OD1, `reagent_containers/jerrycan.dm`, the plastic cans with their `_highlight`, `_label_*` and `_cap_*`
  overlays and the in-hand reagent filling): a square plastic fuel can with a coloured cap and label. Meanwhile tg's sprites.
- **E-cigarette** (gaps OD1, `cigarettes.dm`, greyscale `vape`, open `vapeopen_low`, `vapeopen_med`, `vapeopen_high`, worn
  `vape_worn`): a slim pen vape, its cap open to show the coil, glowing while worn. Meanwhile tg's sprites.
- **Delivery beacons** (gaps OD1, `choice_beacon.dm`: `sb_delivery`, `self_delivery`, `icra_delivery`): handheld beacons of the
  instrument, augment and chaplain's deliveries. Meanwhile tg's sprites.
- **Small odds** (gaps OD1): the taster (`icons/obj/medical/organs/organs.dmi` `tongue`), the pile of bear armor
  (`icons/obj/tools.dmi` `bear_armor_upgrade`), the balloon mallet, the crushed cans and boritos bags (`icons/obj/service/janitor.dmi`
  trash states, `icons/obj/food/canned.dmi` empty cans), the surgical razor (`icons/obj/cosmetic.dmi` `medrazor`) and the party game
  poster (`pinningposter`) with the tail pins stuck where they landed. Meanwhile tg's sprites.

## Rapid part exchange devices and Lavaland's odds
- **Rapid part exchange devices** (gaps OD3, `research/part_replacer.dm`, `icons/obj/storage/storage.dmi` states `RPED`, `BS_RPED`,
  `borgrped` and their `_active` flicks): a boxy handheld parts organiser with a carry grip, and its bluespace kind with a glowing core;
  the flick of an exchange should be a short light pulse on the model. Meanwhile tg's sprites.
- **Ash flora** (gaps OD3, `mining/lavaland/ash_flora.dm`, `icons/obj/mining_zones/ash_flora.dmi`): Lavaland's knee-high plants, one
  model per look, each with its picked state (`...p`, the same plant cut back): the large, leafy, tall and numerous mushrooms
  (`l_mushroom1`-`4`, `s_mushroom1`-`4`, `r_mushroom1`-`4`, `t_mushroom1`-`4`), the fruiting cacti (`cactus1`-`4`), seraka
  (`seraka_mushroom1`-`2`), the fire blossom (`fireblossom1`-`2`), the glowgrowth on its dense rock (`glowgrowth1`-`3`), the ice
  moon's chilly pepper, fonarstolbe, vahrezjia, podsneyzka, inozhakust and plasma flower, and the drake beet. Meanwhile tg's sprites.
- **Colossal bones** (gaps OD3, `elephantgraveyard.dm`, `icons/obj/art/statuelarge.dmi` states `rib`, `skull`, `skull-half`): a giant
  rib arching over a tile and a monster's skull, whole and cracked in half, big enough to walk beside. Meanwhile tg's sprites.
- **Oil well** (gaps OD3, `icons/obj/watercloset.dmi` state `puddle-oil` with its `puddle-oil-splash` flick): a black, bubbling pool
  sunk in the floor; the splash should ripple its surface. Meanwhile tg's sprite.
## MODsuits
- **MOD control units and their parts** (gaps MO1, `mod/mod_control.dm`, `mod_clothes.dm`, `mod_theme.dm`;
  `icons/obj/clothing/modsuit/mod_clothing.dmi` states `<skin>-control`, `<skin>-helmet`, `<skin>-chestplate`, `<skin>-gauntlets`,
  `<skin>-boots` and their `-sealed` states for each theme's skins (standard, civilian, engineering, atmospheric, advanced, loader,
  mining, medical, rescue, research, security, safeguard, magnate, cosmohonk, the syndicate, elite, infiltrator, responsory and the
  rest); worn from `icons/mob/clothing/modsuit/mod_clothing.dmi`): a back-mounted control unit (a powered pack with the theme's
  plating) and the armoured helmet, chestplate, gauntlets and boots it deploys onto its wearer, open while unsealed and with the
  visor down and lit while sealed. Meanwhile the client draws tg's held and worn sprites in 2D.
- **MOD modules** (gaps MO1, `mod/modules/_module.dm`, `modules_general.dm`, `modules_visor.dm`;
  `icons/obj/clothing/modsuit/mod_modules.dmi`, the worn overlays `icons/mob/clothing/modsuit/mod_modules.dmi` with each module's
  `overlay_state_inactive`, `overlay_state_active` and `overlay_state_use`): the small module cartridges, and the worn pieces a
  module adds to the suit: the ion jetpack's thrusters on the back (lit while on), the storage pouches, the flashlight's lamp on the
  helmet, the visors over the helmet's eyes, the joint torsion ratchets on the knees, the recycler's hopper, the fishing glove's rod
  mount. Meanwhile tg's sprites and worn overlays in 2D.
- **MOD construction** (gaps MO1, `mod/mod_construction.dm`, `mod_core.dm`, `mod_paint.dm`;
  `icons/obj/clothing/modsuit/mod_construction.dmi`): the shell at each building step (`mod-construction_start`,
  `mod-construction_core`, `mod-construction_screwed_core`, `mod-construction_helmet` ... `mod-construction_screwed_assembly`), the
  loose helmet, chestplate, gauntlets and boots parts, the theme platings (`<skin>-plating`), the cores (`mod-core`,
  `mod-core-infinite`, `mod-core-standard`, `mod-core-ethereal`, `mod-core-plasma`, `mod-core-plasma-flower`, `mod-core-candela`) and
  the lavaland plasma flower (`plasma-flower`),
  the MOD paint kit (`paintkit`) and the skin appliers (`skinapplier`, the honkerative one). Meanwhile tg's sprites in 2D.
- **MODlink scryer and its call** (gaps MO1, `mod/mod_link.dm`, `icons/obj/clothing/neck.dmi` state `modlink`, worn on the neck;
  the call's hologram, `makeHologram(0.75)` over a copy of the caller's appearance): a neck-worn communicator, and the translucent
  blue picture of the person on the other end standing beside the wearer, turned with them. Meanwhile the client draws tg's sprite
  and the caller's 2D appearance tinted as a hologram.
- **AI mini-card** (gaps MO1, `mod/mod_ai.dm`, `icons/obj/aicards.dmi` states `minicard`, `minicard-filled`): the small card a wrecked
  suit ejects with its AI inside. Meanwhile tg's sprite.
- **MOD department and antagonist module pieces** (gaps MO2, `mod/modules/modules_engineering.dm` ... `modules_antag.dm`;
  `icons/obj/clothing/modsuit/mod_modules.dmi` and each module's worn `overlay_state_inactive`, `overlay_state_active` and
  `overlay_state_use` in `icons/mob/clothing/modsuit/mod_modules.dmi`): the worn pieces the department and antagonist modules add to
  the suit: the welding visor, the magboots' plating, the radiation shielding, the t-ray goggles, the misters' nozzles, the health
  analyzer's scanner, the injector's needle arm, the magnetic harness, the pepper shoulders, the holster, the megaphone, the projectile
  dampener's emitter, the drill and the clamp on the arm, the ore bag, the loader's hydraulic arms and magnet, the ash armour, the bike
  horn and microwave beam emitters, the rave visor, the tanner, the springlock, the anti-gravity and teleporter units, the kinesis
  emitter, the energy shield's generator, the flamethrower's nozzle, the plate compression, the demoralizer and the chameleon's pack,
  and the defibrillator gauntlets (`icons/obj/medical/defib.dmi` `defibgauntlets0`, `syndiegauntlets0`). Meanwhile tg's sprites and
  worn overlays in 2D.
- **Emergency tether** (gaps MO2, `modules_engineering.dm`, `datums/components/tether.dm`; `mod_modules.dmi` states
  `tether_projectile`, `tether_latched` and the beam `line`): the grappling hook it fires, the anchor it leaves on the floor (a squat
  reinforced spike with a ring), and the taut cable between its two ends, which should stretch, slacken and snap. Meanwhile the
  sprites and a beam of tg's `line` segments.
- **Explorer's sphere and its micromissile** (gaps MO2, `modules_supply.dm`; the wearer masked to a ball, `mod_modules.dmi`
  `ball_mask`, with the `ball` overlays, and the `mine_missile` projectile): the mining suit rolled into a metal ball that its wearer
  rolls and fires small mining missiles from. Meanwhile the masked wearer and the sprites.
- **Chrono suit's effects** (gaps MO2, `modules_timeline.dm`, `datums/proximity_monitor/fields/timestop.dm`; the eradication beam
  `chronobolt`, the eradication field `icons/effects/effects.dmi` `chronofield` around its victim, who fades out (DV540), and the time
  stop's 5 x 5 tile field `icons/effects/160x160.dmi` `time`): a crackling blue bolt, a shimmering sphere of time-bluespace around a
  person being erased, and a wide translucent dome under which everything stands still, drawn colour-inverted. Meanwhile the sprites.
- **Kinesis grip** (gaps MO2, `module_kinesis.dm`; `icons/effects/effects.dmi` `kinesis` under the grabbed object and the `kinesis`
  beam from the wearer): a gravitational glow around a held object and the beam that holds it. Meanwhile the sprites.
- **Pathfinder's flight** (gaps MO2, `module_pathfinder.dm`; the `mod_jet` overlay under the suit, the transport supply pod it rides):
  a MOD control unit with lit jets flying itself to its owner. Meanwhile the sprite overlay and the supply pod's.
- **MOD odds** (gaps MO2): the mirage grenade (`icons/obj/weapons/grenade.dmi` `mirage`), the hardlight body and prisoner bags of the
  patient transport and criminal capture modules (`/obj/structure/closet/body_bag/environmental/hardlight` and its prisoner kind), the
  energy shield's bubble (`icons/effects/effects.dmi` `shield-red`, the battlemage's `icons/effects/magic.dmi`), the projectile
  dampener's mark on a slowed bullet (`icons/effects/fields.dmi` `projectile_dampen_effect`) and the active sonar's ping on a creature
  (DV541). Meanwhile the sprites.

## Species parts

D338: until these exist, a species is the human body's billboard with its parts drawn from tg's sprites (lobby.md 10). Each part is
worn by the human body's model, follows the body's turning, and takes the colour tg gives it (the mutant colour, the hair colour or a
dye: a greyscale model tinted at run time).

- **Lizard body** (gaps SP1, `species_parts/lizard_bodyparts.dm`, `icons/mob/human/species/lizard/bodyparts.dmi`, the `lizard_*`
  states): the lizard's scaled head, chest, arms and legs (male and female chests), and the **digitigrade legs** (`digitigrade_*`
  states: the reverse-jointed lower legs and clawed feet), with the plain lizard legs they squash into under clothes without a
  digitigrade variant. Clothing on digitigrade legs needs the matching leg shape (tg's `digi_template_equipment.dmi`,
  `digi_template_underwear.dmi`: jumpsuits, oversuits, boots, sneakers, underwear). Meanwhile tg's sprites.
- **Lizard tails** (gaps SP1, `icons/mob/human/species/lizard/lizard_tails.dmi`, `m_tail_lizard_<style>_BEHIND` and `_FRONT`): the
  smooth, dark tiger, light tiger and spikes tails, each with a **wagging** animation (`m_tail_lizard_wagging_<style>_*`, tg's
  animated states), and their **tail spines** (`lizard_spines.dmi`, `m_tailspines_*`: short, long, the membranes, aquatic) riding on the
  tail and wagging with it. Meanwhile tg's sprites.
- **Moth body, wings and antennae** (gaps SP2a, `species_parts/moth_bodyparts.dm`, `icons/mob/human/species/moth/`: `bodyparts.dmi`
  `moth_*`, `moth_wings.dmi` `m_moth_wings_<style>_FRONT/BEHIND`, `moth_antennae.dmi`, `moth_markings.dmi`): the fuzzy head, chest and
  limbs with their body markings, the large **wings** (about 25 styles, including the burnt-off one, opening and closing for the flap)
  and the feathery **antennae**; the wings are the part that matters in 3D (they show the drift in zero gravity). Meanwhile tg's sprites.
- **Plasmaman body and envirosuits** (gaps SP2a, `species_parts/plasmaman_bodyparts.dm`, `icons/mob/human/species/plasmaman/bodyparts.dmi`;
  `icons/mob/clothing/under/plasmaman.dmi`, `icons/mob/clothing/head/plasmaman_head.dmi`): the skeleton's bones, one **envirosuit** and
  one **helmet** per job (with the welding visor, the torch light and the crayon smile overlay) and the envirogloves. Meanwhile tg's sprites.
- **Snouts, horns and frills** (gaps SP1, `icons/mob/human/species/lizard/lizard_misc.dmi`, `m_snout_*`, `m_horns_*`, `m_frills_*`): the
  sharp and round snouts with their light-belly kinds; the simple, short, curled, ram and angler horns; the simple, short and aquatic
  frills. Meanwhile tg's sprites.
- **Back spines** (gaps SP1, `lizard_spines.dmi`, `m_spines_*`): the short, long, membrane and aquatic spines along the back, hidden by
  a jumpsuit-hiding suit. Meanwhile tg's sprites.
- **Body markings** (gaps SP1, `icons/mob/human/species/lizard/lizard_markings.dmi`, the dark tiger, light tiger and light belly
  markings per limb): a texture layer on the lizard body's model rather than a model. Meanwhile tg's sprites.
- **Cat ears and tails** (gaps SP1, `icons/mob/human/cat_features.dmi`: `m_ears_<style>_BEHIND` and the `FRONT` holder of the outer and
  inner ear, the cat, big, coeurl, fold, lynx, round and cybernetic ears with the cybernetic kinds' coloured inner ears;
  `m_tail_cat_*` with its wagging states): ears on top of the human head, the inner ear its own coloured part, and a cat tail with its
  wag. Meanwhile tg's sprites.
- **Skeleton body** (gaps SP3a, `species_parts/misc_bodyparts.dm`, `icons/mob/human/species/misc/skeleton.dmi`, the `skeleton_*` states): the bare bone head, chest, arms and legs, plus the **degloved bone limbs** (`bone_*` states, with their `_blood` overlay) a dismembered skeleton leaves. Meanwhile tg's sprites.
- **Golem body** (gaps SP3a, `icons/mob/human/species/golems.dmi`, the `golem_*` states): the rock head, chest, arms and legs, and the **mineral veins** a golem grows from the metals it eats (`uranium_*`, `silver_*`, `plasma_*`, `iron_*`, `gold_*`, `diamond_*`, `platinum_*`, `banana_*` per body zone). Meanwhile tg's sprites.
- **Zombie claws and infection** (gaps SP3a, `icons/effects/blood.dmi` `bloodhand_left/right`, `icons/obj/medical/organs/organs.dmi` `blacktumor`): the held mutant claw (a held, undroppable hand model) and the black ooze organ. Meanwhile tg's sprites.

- **Ethereal body** (gaps SP2b, `species_parts/ethereal_bodyparts.dm`, `icons/mob/human/species/ethereal/bodyparts.dmi`, the `ethereal_*` states): the crystalline head, chest, arms and legs, glowing in the ethereal's colour (the glow's colour follows health; an emissive limb material rather than a tint). Meanwhile tg's sprites.
- **Podperson body and hair** (gaps SP2b, `icons/mob/human/bodyparts_greyscale.dmi` `pod_*`, `icons/mob/human/species/podperson_hair.dmi` `m_pod_hair_*`): the leafy limbs and the eleven hairstyles (ivy, cabbage, spinach, prayer, vine, shrub, rose, orchid, fig, hibiscus), a front and an adjacent part each. Meanwhile tg's sprites.
- **Mushroomperson body and cap** (gaps SP2b, `bodyparts_greyscale.dmi` `mush_*`, `icons/mob/human/species/mush_cap.dmi` `m_caps_*`): the stalk-like limbs and the red mushroom cap on the head, hidden by headgear. Meanwhile tg's sprites.
- **Snailperson body and shell** (gaps SP2b, `bodyparts_greyscale.dmi` `snail_*`, `icons/mob/clothing/back/backpack.dmi` `snailshell`): the slimy limbs and the armoured shell on the back. Meanwhile tg's sprites.

- **Jelly body** (gaps SP2c, `icons/mob/human/species/jelly/` limbs, `jelly_*`, `slime_*`, `luminescent_*` states): the translucent
  head, chest, arms and legs of the jellyperson, slimeperson and luminescent (the hair at 150 alpha, the skeleton and three eyes
  showing through), in the mutant colour. Meanwhile tg's sprites.
- **Cerulean body and tail** (gaps SP2c, `icons/mob/human/species/cerulean/cerulean_tails.dmi`: `default`, `skeleton`, `abyss_*`): the
  oversized fish tail that replaces the legs (a wide, tall, two-layer model behind and beside the torso, in the fish tail colour), the
  aquatic frills, and the abyss variant's translucent skeleton with its glow. Meanwhile tg's sprites.

## Medicines and toxins
- **Ice block** (gaps RG2, `xenobiology/crossbreeding/_misc.dm:127`, `icons/obj/science/slimecrossing.dmi` state `frozen`): a man-high
  block of clear, frosted ice with the frozen body dimly visible inside, standing on one tile; cryogelidia (the inverse of
  cryostylane) and the chilling dark blue extract encase a body in it, and it shatters when the body breaks out. Meanwhile tg's sprite.

## Organs and the recovered crew

- **The ethereal crystal** (gaps OG1, `heart/heart_ethereal.dm:165`, `icons/mob/effects/ethereal_crystal.dmi` state `ethereal_crystal`, tinted by
  the ethereal's colour, with a light): the man-sized crystal a dead ethereal's heart grows around the body, which shatters when the
  ethereal bursts out of it. Ethereals are not playable yet (D338), so it is not reached in a round. Meanwhile the client draws tg's
  sprite in 2D.
- **The long-term body bag** (gaps OG1, `lost_crew/body_bags.dm`, `icons/obj/medical/bodybag.dmi`, the bag's `bodybag_lost`
  states and the folded `bodybag_lost_folded`): a heavier, darker body bag lying on the floor, closed, open and folded, the recovered
  crewmember's from cargo's body freezer. Meanwhile the client draws tg's sprites in 2D, as the plain body bag's.
## Bitrunning
The den's netpod, quantum server, byteforge and order console have their models (`machines/science_wave/`, `machines/device_revision/`);
what the bitrunning machinery (gaps BR1) still draws in 2D:
- **The order console switched off** (gaps BR1, `bitrunning/objects/vendor.dm:70`, `icons/obj/machines/bitrunning.dmi` `vendor_off`, and
  its `vendor_off` flick on a refused order): the bitrunning supplies console's model (`bitrunning_supplies_order_console`) covers
  `vendor` only. Meanwhile the unpowered console keeps its powered look, and the refusal's flick is not drawn.
- **The netpod's door moving** (gaps BR1, `netpod/container.dm:21`, `:33`, the `netpod_opening` and `netpod_closing` flicks): the pod's
  model opens and shuts its door with its `open` animation (1.7 s) between `netpod_closed*` and `netpod_open_active`; the flicks
  themselves have no frames of their own. Not needed while the door's animation reads as the flick.
- **The emagged quantum server** (gaps BR1, `server/_parent.dm:127`, `bitrunning.dmi` `emag_overlay`): the sparking panel overlay of
  an emagged server on its model. Meanwhile the overlay is not drawn on the model.
- **The decrypted curiosity** (gaps BR1, `storage/lockbox.dm:324`, `icons/obj/storage/case.dmi` `bitrunning`, `bitrunning+l`,
  `bitrunning+b`): the curiosity box a domain's secondary objective prints, locked, broken and open. Meanwhile the client draws tg's
- **The virtual domains' props** (gaps BR1-2, `_maps/virtual_domains/*.dmm`): the domains reuse the station's and the ruins' things;
  their own are the binary floor and wall (`icons/turf/floors.dmi` `binary`, `bitrunning`), drawn as flat textures, and the host
  monitor (`icons/obj/devices/scanner.dmi` `host_monitor`), an item in 2D.
- **The cliffs** (gaps BR2, `turf/open/cliff.dm`, `icons/turf/cliff/cliff.dmi`, `icerock_cliff.dmi`): the glacier grind's snowy rock
  slope that slides whoever steps onto it southward, with the floor it lies on drawn under it. Needs a sloped turf model (the slope
  facing its fall direction, south) so the drop reads in 3D. Meanwhile the client draws tg's flat smoothed sprite on the floor.
- **The netguardian prime** (gaps BR2, `antagonists/netguardian.dm`, `icons/mob/nonhuman-player/netguardian.dmi` `netguardian`,
  `crash`, `scan`, `rockets`): the flying war machine a heavily threatened server spawns, with its burst SMG, the scan visor and
  rocket pods its barrage raises, and its crashed wreck. Meanwhile tg's 2D sprite and overlays.
- **The clowns** (gaps BR2, `basic/clown/clown.dm`, `icons/mob/simple/clown_mobs.dmi`): Clown Planet's clown, living lube, honkling,
  flesh clown, the clown hulks (honk hulk, chlown, honkmunculus, destroyer) and the mutant, each its own creature (the hulks larger
  than a human). Meanwhile tg's 2D sprites.
- **The xenomorph maid** (gaps BR2, `basic/alien/maid.dm`, `icons/mob/nonhuman-player/alien.dmi` `maid`): a drone in an apron; a
  variant of the adult castes' model (Xenomorphs above). Meanwhile tg's 2D sprite.
- **The password door** (gaps BR2, `doors/passworddoor.dm`, `icons/obj/doors/blastdoor.dmi` `closed`, `opening`, `open`, `closing`):
  the blast door model serves it (its open and close animations), so nothing new is needed unless a vault look is wanted.

## The miners' gear and shelters

What the Lavaland shuttles' and the mining gear's package (gaps LV1) draws in 2D; the mine cart (`miningcar`, `miningcaropen`) has its
model already.
- **Shelter furniture** (gaps LV1, `mining/equipment/survival_pod.dm`, `icons/obj/mining_zones/survival_pod.dmi` states `table`,
  `windoor`, `sleeper` (and its `sleeper-open`), `bed`, `bed_double`, `tubes`; `icons/obj/mining_zones/pod_computer.dmi`
  `pod_computer`; `icons/obj/mining_zones/donkvendor.dmi` `donkvendor`; the pod airlock `icons/obj/doors/airlocks/survival/survival.dmi`):
  the white rounded fittings of a bluespace shelter: its table, windoor, wall sleeper, single and double beds, the wall tubes, the wall
  computer with its GPS screen and the Donk storage fridge. Meanwhile tg's sprites.
- **Survival capsules** (gaps LV1, `icons/obj/mining.dmi` `capsule`, shared by all four kinds): a small capsule thrown on the ground.
  Meanwhile tg's sprite.
- **Jaunter and its tunnel** (gaps LV1, `wormhole_jaunter.dm`, `icons/obj/mining.dmi` `Jaunter`; `icons/effects/anomalies.dmi`
  `vortex`): the belt-worn wormhole device and the swirling vortex it opens. Meanwhile tg's sprites.
- **Grapple gun** (gaps LV1, `grapple_gun.dm`, `icons/obj/mining.dmi` `grapple_gun` with its hook overlay, the `grapple_hook`
  projectile and the zipline beam): a hook launcher whose hook leaves the barrel when fired. Meanwhile tg's sprites.
- **Kheiral cuffs** (gaps LV1, `kheiral_cuffs.dm`, `icons/obj/mining.dmi` `strand` with its `strand_light` glow, worn `strandcuff` with
  `strandcuff_emissive`): a wrist strand that glows while it links the wearer's suit sensors far from the station. Meanwhile tg's sprites, the glow not emissive (DV12).
- **Vent pointer** (gaps LV1, `vent_pointer.dm`, `pinpointer_vent`): the pinpointer's model in the miners' colours. Meanwhile tg's
  sprite.
- **Landing field designator and the mining shuttle beacon** (gaps LV1, `aux_base.dm`, `icons/obj/devices/remote.dmi`
  `designator_mining`; `icons/obj/mining.dmi` `miningbeacon`): the handheld designator and the waist-high beacon that marks a landing
  site for the mining shuttle. Meanwhile tg's sprites.
- **Mine rails** (gaps LV1, `mine_items.dm`, `icons/obj/track.dmi` `track`, `track_break`): the rails the mine cart rolls on, and
  the broken rail where a cart stops. Meanwhile tg's sprites flat on the floor.
## Lavaland's fauna and the kinetic crusher
The wastes' common creatures (`mob/living/basic/lavaland/`), their effects and the miner's crusher, all drawn as tg's 2D sprites
until their models exist:
- **The goliath** (`goliath/goliath.dm`, `icons/mob/simple/lavaland/lavaland_monsters.dmi` `goliath`, `goliath_preattack`,
  `goliath_dead`, the ancient goliath's `ancient_goliath*`, the saddled `goliath_saddled`): a large, slow, rock-plated quadruped, its
  pre-attack shiver before the tentacles. Its **tentacles** (`goliath/tentacle.dm`, `goliath_tentacle_spawn`, `goliath_tentacle_wiggle`,
  `goliath_tentacle_retract`): a stony tentacle rising out of the floor, wrapping, and sinking back.
- **The watchers** (`watcher/watcher.dm`, `lavaland_monsters.dmi` `watcher`, `watcher_magmawing`, `watcher_icewing` with their
  `_dead` states and the eye glow `watcher_glow`): a floating eye with membrane wings, the magmawing's burning, the icewing's frosted;
  their beam bolts (`icons/obj/weapons/guns/projectiles.dmi` `temp_4`, `ice_2`, `lava`) and the gaze's flash.
- **The legion** (`legion/legion.dm`, `lavaland_monsters.dmi` `legion`, `dwarf_legion`, `snowlegion`, `legion_large`) and its
  **skulls** (`legion/legion_brood.dm`, `legion_head`, `snowlegion_head`): a floating mass of skulls, and the flying skulls it
  launches; the infested corpse is a human body.
- **The hivelord** and its **brood** (`hivelord/hivelord.dm`, `hivelord`, `hivelord_alert`, `hivelordbrood`): a pulsing hive and
  the small flying spawn.
- **The basilisk** (`basilisk/basilisk.dm`, `basilisk`, `basilisk_alert`, `basilisk_dead`): a crystal-backed lizard, glowing red when
  overheated.
- **The brimdemon** (`brimdemon/brimdemon.dm`, `brimdemon`, `brimdemon_firing`, `brimdemon_dead`) and its **beam**
  (`icons/mob/simple/lavaland/lavaland_monsters.dmi` `brimbeam_start`, `brimbeam_mid`, `brimbeam_end`): a horned demon firing a
  straight blood-red beam.
- **The bileworm** and the **vileworm** (`bileworm/_bileworm.dm`, `icons/mob/simple/lavaland/bileworm.dmi`, `bileworm_jump.dmi`):
  a worm bursting out of the ground and diving back, its bile globs and target circle.
- **The lobstrosity** and the **juvenile** (`lobstrosity/lobstrosity.dm`, `icons/mob/simple/lavaland/lavaland_monsters.dmi`
  `arctic_lobstrosity`, `lobstrosity`, `arctic_lobstrosity_juvenile`, `lobstrosity_juvenile`): a giant crab-like lobster, the
  juvenile its small young.
- **The NODE drone** (`node_drone/node_drone.dm`, `icons/obj/mining.dmi` `mining_node_active`, `mining_node_flying`,
  `mining_node_escape`): the hovering drone that latches onto an ore vent during its wave defence, flying in and out.
- **The kinetic crusher** (`kinetic_crusher/kinetic_crusher.dm`, `icons/obj/mining.dmi` `crusher` with `_uncharged` and `_lit`, the
  skins `crusher_sword`, `crusher_harpoon`, `crusher_dagger`, `crusher_glaive`, `crusher_skull`; in-hands
  `icons/mob/inhands/weapons/hammers_*hand.dmi` `crusher0`, `crusher1`): a two-handed club-hammer with a light; its **mark**
  (`icons/effects/effects.dmi` `shield`, a bubble under the marked creature) and **blast** (`kinetic_blast`).
- **The monster organs and trophies** (`icons/obj/medical/organs/mining_organs.dmi`, `icons/obj/mining_zones/artefacts.dmi`): the
  regenerative cores, the brimdust sac, the rush gland and the crusher trophies (wings, skull, tentacle, claw, fang, spewlet) are small
  items; a generic organ or trinket model serves them.

## Lavaland's fauna, second half, the raptor ranch and the ice moon's mobs
The rest of the wastes' creatures (`mob/living/basic/lavaland/goldgrub/`, `gutlunchers/`, `raptor/`, `mook/`), the ice moon's
(`mob/living/basic/icemoon/`) and the ranch's things, drawn as tg's 2D sprites until their models exist:
- **The goldgrub** (`goldgrub/goldgrub.dm`, `icons/mob/simple/lavaland/lavaland_monsters_wide.dmi` `goldgrub`, `goldgrub_dead`,
  the alert overlay `goldgrub_alert`; the baby `lavaland_monsters.dmi` `grub_baby`): a fat, glossy burrowing larva, ridable once
  tamed; it sinks into the rock and bursts out of it. Its **egg** (`egg` green) is a food egg.
- **The gutlunchers** (`gutlunchers/gutlunchers.dm`, `lavaland_monsters.dmi` `gutlunch`, the full udder overlay `gl_full`; the milk
  one, the warrior and the grub tinted by their greyscale colours): a squat, toothy ash-land livestock with a pink udder; the grub
  its small young. The **gutlunch trough** (`icons/obj/structures.dmi` `gutlunch_trough`) is a wooden trough with the ores in it.
- **The raptors** (`raptor/_raptor.dm`, `raptor_color.dm`; `icons/mob/simple/lavaland/raptor_big.dmi` `raptor_<colour>` and
  `young_<colour>` with their `_dead` states and the collars `raptor_collar`, `young_collar`; `raptor_baby.dmi` `baby_<colour>`): a feathered,
  rideable bipedal lizard-bird in seven colours (red, purple, green, white, black, yellow, blue) and three growth stages, the chick
  small enough to hold, the purple one carried on the back with its **wings**. The **raptor egg** (`raptor_baby.dmi` `raptor_egg`), the
  **raptor trough** (`icons/obj/structures.dmi` `raptor_trough`) and the **RaptorDex** (`icons/obj/devices/scanner.dmi`
  `raptor_dex`), a handheld scanner.
- **The mooks** (`mook/mook.dm`, `icons/mob/simple/jungle/mook.dmi` `mook`, `mook_warmup`, `mook_leap`, `mook_strike`, the axe
  `mook_axe_overlay` and `axe_strike_overlay`, the bard's `bard_overlay` and `bard_strike`, the chief's `mook_chief*`, the carried
  ore `mook_ore_overlay`): small, hunched primitives in machine-made garments, crouching to leap; their **dust** puffs (`mook_dust`)
  and the village's **material stand** (`mook.dmi` `material_stand`), a stone counter with ores on it.
- **The ice demon** and its **afterimages** (`icemoon/ice_demon/ice_demon.dm`, `icons/mob/simple/icemoon/icemoon_monsters.dmi`
  `ice_demon`): a floating, horned ice spirit that shrinks before it teleports and splits into translucent copies;
  its **slippery ice** (`icons/turf/floors/ice_turf.dmi` `ice_turf-6`), a fading ice sheet, and its frost bolt (`ice_2`).
- **The ice whelp** (`icemoon/ice_whelp/ice_whelp.dm`, `icemoon_monsters.dmi` `ice_whelp`, the breath warning
  `ice_whelp_telegraph_dir`; the ash whelp `lavaland_monsters.dmi` `ash_whelp`): a young, white drake breathing a freezing line or a
  cross; its **statue** is a dragon statue of itself.
- **The polar bear** (`icemoon/polar_bear/polar_bear.dm`, `icemoon_monsters.dmi` `polarbear`): a large white bear.
- **The wolf** (`icemoon/wolf/wolf.dm`, `icemoon_monsters.dmi` `whitewolf`): a white wolf, rideable once tamed.
- **The trophies** (`icons/obj/mining_zones/artefacts.dmi` the demonic cube, the wolf ear, the bear paw, the raptor feather, the
  tail spike): small items; a generic trinket model serves them.

## The megafauna and their loot
The bosses of Lavaland and the ice moon (`simple_animal/hostile/megafauna/`, `basic/boss/`), their attacks' effects and their loot,
all drawn as tg's 2D sprites (the large ones on 64 or 96 pixel sheets):

- **The ash drake** (`megafauna/drake.dm`, `icons/mob/simple/lavaland/96x96megafauna.dmi` `dragon`, `dragon_dead`; the lesser
  drake): a winged red dragon that breathes fire, swoops off the screen and lands in a lava arena; its meteors fall on target marks.
- **Bubblegum** (`megafauna/bubblegum.dm`, `96x96megafauna.dmi` `bubblegum`): a huge horned blood demon that sinks into blood
  pools and rises from them, its clawed hands reaching out of the blood (`icons/effects/bubblegum.dmi`), and its translucent
  charging hallucinations.
- **The colossus** (`megafauna/colossus.dm`, `96x96megafauna.dmi` `eva`): a giant armoured statue-like humanoid firing volleys of
  glowing bolts; the anomalous crystals of its chest.
- **The hierophant** (`megafauna/hierophant.dm`, `icons/mob/simple/lavaland/hierophant_new.dmi` `hierophant`): a floating
  club-shaped machine; its blasts, chasers, arena walls and squares, its beacon, and the hierophant club it leaves.
- **The legion** (`megafauna/legion.dm`, `96x96megafauna.dmi` `mega_legion`, the medium ones on `64x64megafauna.dmi`, the small
  ones on `lavaland_monsters.dmi`): a floating mass of skulls splitting into smaller masses; its sentinels and the staff of
  storms.
- **The demonic frost miner** (`megafauna/demonic_frost_miner.dm`, `icons/mob/simple/icemoon/icemoon_monsters.dmi`
  `demonic_miner`): a frozen, possessed miner; its frost orbs, snowballs and ice blasts, and the prisms of its arena.
- **The wendigo** (`megafauna/wendigo.dm`, `icons/mob/simple/icemoon/64x64megafauna.dmi` `wendigo`, `wendigo_dead`): a gaunt
  antlered beast; its shockwaves and the exit portal it leaves.
- **The clockwork defender** (`megafauna/clockwork_knight.dm`, `icemoon_monsters.dmi` `clockwork_defender`): a brass knight.
- **The blood-drunk miner** (`basic/boss/blood_drunk_miner/`, `icons/mob/simple/broadMobs.dmi` `miner`, with its guidance, hunter
  and doom kinds): a mad shaft miner with a transforming cleaving saw and a kinetic accelerator.
- **The Thing** (`basic/boss/thing/`, `icons/mob/simple/icemoon/thething.dmi` `p1`, `p2`, `p3`, `dead`): a writhing flesh monster in
  three phases, its tendrils and acid; the phase depleters and aggro gates of its ruin.
- **The loot** (`mining_loot/megafauna/`): the drake's armour and cloak, the ghost sword, the dragon's blood, the lava staff, the
  H.E.C.K. suit, the bottle of mayhem, the soulscythe, the resurrection crystal, the ice trail boots, the demonic jackhammer, the
  wendigo's blood, the AI-uplink brain, the cleaving saw and the wildhunter knife, and the megafauna's crusher trophies (the
  miner's eye, tail spike, demon claws, blaster tubes, vortex and ice block talismans, wendigo horn, flesh glob): small items a
  generic item or weapon model serves; the necropolis chests are crates.
## Xenobiology and cytology
The xenobiologist's and the cytologist's odds (gaps XB1, `research/xenobiology/`), drawn as tg's 2D sprites until their models
exist; the growing vat and the microscope have theirs (`machines/medical_wave/biological_growing_vat.glb`, `microscope.glb`), the
slime management console is the standard console with tg's `slime_comp` screen:
- **The slime extracts** (`xenobiology.dm`, `icons/mob/simple/slimes.dmi` `grey-core` to `rainbow-core`, 22 colours): a small
  gelatinous core, one model tinted per colour (the adamantine, bluespace, pyrite, cerulean, sepia and rainbow ones with their own
  shine). A used extract keeps its look in tg (only its name changes).
- **The slime potions** (`icons/obj/medical/chemical.dmi` `potsilver`, `potpink`, `potred`, `potblue`, `potgreen`, `potorange`,
  `potpurple`, `potrainbow`, `potcyan`, `potbrown`, `potbluespace`): a small round flask, tinted per potion.
- **The petri dish** (`icons/obj/science/vatgrowing.dmi` `petri_dish`, with the sample's `petri_dish_overlay` tinted by the sample
  colour and `petri_dish_overlay2`): a shallow glass dish with a lid, the culture a coloured film inside.
- **The swab** and **the biopsy tool** (`vatgrowing.dmi` `swab` with its `swab_<colour>` tip, `biopsy`, `biopsy_organ`): a cotton
  bud, and a pen-like sampling tool with its sample window.
- **The vatbeast** (`mob/living/basic/cytology/vatbeast.dm`, `icons/mob/vatgrowing.dmi` `vat_beast`, `vat_beast_dead`): a big
  rideable grub-like beast with a lashing tentacle; tg's tentacle slap.
- **The console's suction tube** (`xenobio_camera.dm:668`, `icons/effects/effects.dmi` `xenotube_back` below the mobs and
  `xenotube_fore` above them): a glass tube dropping from the ceiling over the tile, a slime, monkey or potion sliding up or down
  it (`sucked_atom`'s animation) with the air's suck and shoot sounds. Drawn as tg's sprites over the tile until it has a model.
## The chapel
Gaps CH1 (D339), `modules/religion`. Meanwhile tg's sprites.
- **The altar of the gods** (`religion/religion_structures.dm`, `icons/obj/service/hand_of_god_structures.dmi` `convertaltar` and the
  sects' states `convertaltar-blue`, `-red`, `-yellow`, `-burden`, `-white`, `-maint`, `-orange`, `-festival`, `-dream`): a stone altar
  a body can lie on, its cloth and glow in the chosen sect's colours; the lit candles of its `candle_overlay`.
- **The ritual totem** (`religion_structures.dm`, `icons/obj/service/hand_of_god_structures.dmi` `ritual_totem`): the maintenance
  god's carved totem pole of scrap, heavy and anchored.
- **The ceremonial blade** (`sparring/ceremonial_gear.dm`, `icons/obj/weapons/sword.dmi` `default`): a plain ceremonial sword whose
  blade takes the colour and name of the material it is forged from.
- **The sparring contract and the holy writ** (`sparring/sparring_contract.dm`, `honorbound/honorbound_rites.dm`,
  `icons/obj/scrolls.dmi` `sparringcontract`, `honorscroll`): rolled parchment scrolls; a generic scroll model serves both.
- **The chaplain's revolver** (`burdened/psyker.dm`, `icons/obj/weapons/guns/ballistic.dmi` `lucky`): the psyker's holy
  six-shooter.
- **The festival violin** (`festival/festival_violin.dm`, `icons/obj/art/musician.dmi` `holy_violin`): a gilded violin; the violin
  model, recoloured.
- **The psyker head and brain** (`burdened/psyker.dm`, `icons/obj/medical/organs/organs.dmi` `brain-psyker`, the head's `psyker`
  limb): an eyeless, swollen head and its exposed brain.
- **The soulstone shards and the shade** (`antagonists/wizard/equipment/soulstone.dm`, `icons/obj/mining_zones/artefacts.dmi`
  `soulstone`, `purified_soulstone`, `mystic_soulstone` and their `2` states with a soul inside; `mob/living/basic/cult/shade.dm`,
  `icons/mob/nonhuman-player/cult.dmi` `shade_cult`, `shade_holy`, `shade_wizard`; the ectoplasm `icons/effects/magic.dmi`
  `ectoplasm`, `angelplasm`, `mysticplasm`): a red, blue or purple crystal shard, glowing when it holds a soul; the shade, a
  hooded, translucent spirit in the theme's colour; the ectoplasm a shapeless goo.

## The tendrils, the necropolis and the elites
The necropolis tendrils, the ice moon's nests, the necropolis chests' loot and Lavaland's elites with their tumor, all drawn as tg's
2D sprites:

- **The necropolis tendril** (`basic/lavaland/tendril/tendril.dm`, `icons/mob/simple/lavaland/tendril.dmi` `tendril`): a thick,
  pulsing stalk of red flesh rising from the ground, its heart glowing; its spike stab (`spike_stab`), the tentacle lash it throws
  in eight directions and reels back in, and the spikes erupting from the ground (`icons/effects/effects.dmi`
  `emerging_ground_spike`) along its chasers' trails and in its cross. The necropolis floor it spreads is a floor texture.
- **The nests** (`structures/spawner.dm`, `icemoon/cave_entrance.dm`, `icons/mob/simple/lavaland/nest.dmi` `hole`, `nether`): a
  monster den, a hole dug into rock (the cave entrance, the mining dens, the bone pit); the netherworld link and the ice moon's
  demonic portal, a swirling red portal that collapses flat before it closes; the syndicate warp beacon and Laughing Larry
  (`icons/obj/machines/beacon.dmi`).
- **The pulsing tumor** (`mining_mobs/elites/elite.dm`, `lavaland_elites.dmi` `tumor`, `advanced_tumor`, `tumor_popped`): a
  fleshy growth on the ash, glowing when fed a core, burst open when its elite is out; the force-field walls of its arena
  (`hierophant_wall_temp`).
- **The goliath broodmother** (`elites/goliath_broodmother.dm`, `lavaland_elites.dmi` `broodmother`): a larger, darker goliath
  with a brood sac; its babies (`goliath_baby`), small goliaths that explode when they die.
- **The herald** (`elites/herald.dm`, `lavaland_elites.dmi` `herald`): a tall, robed, floating harbinger shooting bright orbs
  (`chronobolt`), red when enraged (`herald_enraged`), a ghost when dead (`herald_ghost`); its translucent mirror image
  (`herald_mirror`).
- **The legionnaire** (`elites/legionnaire.dm`, `lavaland_elites.dmi` `legionnaire`, `legionnaire_headless`, `legionnaire_head`): a
  skeletal giant whose skull flies off on its own; the bonfire it lights (`bonfire`).
- **Pandora** (`elites/pandora.dm`, `lavaland_elites.dmi` `pandora`): a floating ornate box firing hierophant-like blasts.
- **The loot** (`mining_loot/`, `cain_and_abel/`, `vehicles/lavaboat.dm`): the berserker armour and its helmet, the memento mori,
  the concussive gauntlets, the godslayer armour, the eye of god, the spooky lantern and its wisp, Jacob's ladder (a ladder through
  every level), the book of Babel, the potion of flight, the rod of Asclepius (wooden, then with its living snake), the red and
  blue warp cubes, the immortality talisman, the paradox bags, the cursed katana and its shard, Cain and Abel (two daggers with
  their orbiting wisps and crystals), the lava boat and its oar, the ship in a bottle and the dragon boat, the herald's cloak,
  Pandora's hope, the tumor shard, the skeleton key: small items a generic item or weapon model serves, but the boats, which are
  boats.
## Xenobiology's crossbreeds
The crossed cores and what they make (gaps XB2, `research/xenobiology/crossbreeding/`), drawn as tg's 2D sprites until their models
exist:
- **The crossbred extracts** (`icons/obj/science/slimecrossing.dmi` `burning`, `charged`, `chilling`, `consuming`, `industrial`
  with `industrial_still`, `prismatic`, `recurring`, `regenerative`, `reproductive`, `selfsustaining`, `stabilized`, tinted by
  the colour, 22 colours each): the plain extract's core (Xenobiology and cytology above) reshaped per family: flaming, sparking,
  frosted, a mouth, a bubbling vat-core, a prism, a ring, a pulsing heart, a twin core, a split core, a crystal. The industrial
  one bubbles while it makes its item.
- **The slime cookies** (`icons/obj/food/slimecookies.dmi`, one state per colour): a small round cookie per colour.
- **The slime-crossed beakers and autoinjectors** (`/obj/item/slimecrossbeaker` and its blood pack, pax, omnizine and autoinjector
  kinds, `slimecrossing.dmi` `base` tinted by the colour): a soft gel sac holding the reagent, which melts away when empty.
- **The barrier cube and its walls** (`slimecrossing.dmi` `barriercube`, `slimebarrier`, `slimebarrier_thick`, `rainbowbarrier`):
  a gel cube in hand; a waist-high jelly barricade; a full jelly wall, the rainbow one shimmering.
- **The ice stasis block** (`frozen`): a block of ice around a standing mob.
- **The light prism and the prism glasses** (`lightprism`, `prismglasses`, the action buttons `prismcolor` and `lightprism`):
  a tall glowing crystal on the floor; tinted glasses with a prismatic frame.
- **The capture device** (`capturedevice`): a palm-sized ball that holds a small mob.
- **The heroine bud** (`peaceflower`, worn from `icons/mob/clothing/head/costume.dmi`): a pink flower worn on the head.
- **The slime rebreather** (`/obj/item/clothing/mask/nobreath`, `icons/obj/clothing/masks.dmi` `slime`): a gel mask over mouth and
  nose.
- **The adamantine armor and shield** (`icons/obj/clothing/suits/armor.dmi` `adamsuit`, `icons/obj/weapons/shields.dmi`
  `adamshield`): a heavy green-crystal plated suit; a large two-handed crystal tower shield.
- **The rainbow knife** (`icons/obj/weapons/stabby.dmi` `rainbowknife`): a knife whose blade cycles through colours.
- **The blood gun** (`bloodgun`, with its `bloodchill` projectile): a fleshy pistol grown on the hand, firing red globs.
- **The slime hypercharged cell** (`icons/mob/simple/slimes.dmi` `yellow-core`): the yellow extract's core with a power cell's
  contacts.

## Lavaland's ruins and the ash walkers
Lavaland's ruins load from their maps with their own turfs, walls and things, which keep tg's sprites and the turfs' textures:

- **The ash walkers' nest** (`objects_and_mobs/ash_walker_den.dm`, `icons/mob/simple/lavaland/nest.dmi` `ash_walker_nest`): a
  vile tendril rising from a nest of man-sized yellow eggs; when it breaks, the collapsing tendril (`tendril`) a player can grab
  loot from before the ground about it falls into a chasm.
- **The ash walker egg** (`ghost_roles/mining_roles.dm`, `lavaland_monsters.dmi` `large_egg`): a man-sized yellow egg with a
  humanoid shadow inside, its shell (`structures`); the egg is the ghost role's spawner.
- **The sliding puzzle's pillars** (`lavalandruin_code/puzzle.dm`, `icons/obj/mining_zones/artefacts.dmi` `puzzle_pillar`): a
  stone pillar with a picture tile set into its face (the client draws the tile cropped from the puzzle's picture); the prison
  cube (`prison_cube`), a dusty stone cube with a humanoid imprint.
- **Greed's slot machine** (`objects_and_mobs/cursed_slot_machine.dm`, `icons/obj/machines/computer.dmi` `slots`): a gilded slot
  machine with its screen (`slots_screen`, `slots_screen_working`); the bag of money (`moneybag`).
- **Gluttony's wall** (`objects_and_mobs/sin_ruins.dm`, the blob's sprite tinted yellow-green): a fleshy, pulsing wall; envy's
  knife (`envyknife`), a bone-handled kitchen knife.
- **The dead god** (`lavalandruin_code/surface.dm`, `icons/obj/fluff/lavaland/dead_ratvar.dmi`): the huge brass corpse of a
  clockwork god lying in the ash, 6 by 4 tiles.
- **The freeze cube** (`lavalandruin_code/biodome_winter.dm`, `artefacts.dmi` `freeze_cube`): a block of clear ice, spinning when
  thrown.
- **The watcher egg and hatchling** (`lavalandruin_code/watcher_grave.dm`, `egg_watcher`, `watcher_baby`): a pale egg with an eye
  showing through; the baby watcher, a small floating eye with wings that circles its owner and shoots small beams.
- **The firepit** (`awaymissions/mission_code/Cabin.dm`, `icons/obj/fluff/fireplace.dmi` `firepit`, `firepit-active`): a ring of
  stones with logs, burning or cold.
- **The all-terrain vehicle** (`vehicles/atv.dm`, `icons/obj/vehicles.dmi` `atv`, `atvcover`): a four-wheeled quad bike, its
  front cover drawn over the rider; the turret ATV carries a small mounted turret.
- **The gas miners** (`atmospherics/machinery/other/miner.dm`, `icons/obj/machines/atmospherics/miners.dmi` `miner`, `on`,
  `broken`): a heavy floor vent with a coloured glow by gas (oxygen blue, plasma purple, nitrogen green, carbon dioxide grey).
- **The dark wizard and the skeletons** (`basic/ruin_defender/`): drawn from a dressed human (the dark wizard's robes and staff;
  the skeleton, the settler with its spear, the templar's armour and sword, the ice skeleton, the plasma-burnt miner), as the
  other human-shaped mobs; their models are the human model's.
- **The ruins' walls and floors** (`turfs/closed/wall/misc_walls.dm`, `mineral_walls.dm`, `indestructible.dm`,
  `turfs/open/sand.dm`, `ice.dm`, `grass.dm`, `ashplanet.dm`): the cult's runed walls, rusted walls, wooden walls, the
  hierophant's riveted walls and the necropolis' walls; sand, the coast, ice, snow, grass and the watery rock: textures, not models.
## The restaurant
Gaps KT1 (D339), `food_and_drinks/restaurant/`. Meanwhile tg's sprites.
- **The restaurant portal** (`restaurant/_venue.dm`, `icons/obj/machines/restaurant_portal.dmi` `portal`, `portal-open`,
  `portal_door`): a floor-standing teleporter gate the robot tourists walk in and out of, its sliding door shut while the venue is
  closed, its panel open for maintenance.
- **The robot tourists** (`basic/space_fauna/robot_customer.dm`, `icons/mob/simple/tourists.dmi`): a humanoid service robot in eight
  looks with their `_features` and clothes (`amerifat`, `italian`, `french`, `japanese`, `mothbot`, `mexican`, `british`, `defect`;
  the clothes `amerifat_clothes`, `italian_pison`, `italian_godfather`, `french_fit`, `japanese_animes`, `japanese_salary`,
  `mothbot_clothes`, `mexican_poncho`, `british_gentleman`, `british_bobby`, `defect_wires`, `defect_bad_takes`), its body tinted a
  random colour; the French flag waved on the way out (`french_flag`), the moth bot's jetpack and moth wings (`mothbot_jetpack`,
  `moth_wings.dmi`); one robot model with swappable outfits serves them all.
- **The seat holosign** (`restaurant/_venue.dm`, `icons/effects/effects.dmi` `eating_zone`): a translucent hologram on a seat marking
  it for a customer; the projector (`signmaker_service`) is the holosign projector model.
- **The order's thought bubble** (`generic_venues.dm`, `icons/effects/effects.dmi` `thought_bubble`): a cloud bubble over the
  customer with its ordered food or drink inside, seen only by whoever looks at it; a billboard.
## Recreation
Gaps RC1 (D339): the holodeck, the arcades, the slot machines, the roulette and the trading card game. The arcade cabinet, the slot
machine and the computers (the holodeck console) have their models already. Meanwhile tg's sprites.
- **The roulette table** (`machinery/roulette_machine.dm`, `icons/obj/machines/roulette.dmi` `idle`, `rolling`, `open`, the
  `flick_up` and `flick_down` flicks, the number and colour overlays of its last spin): a green felt table with a sunken wheel that
  rises on its piston to spin; the winning number lit on its rim. Its prize beacon (`icons/obj/machines/floor.dmi` `floor_beacon`) is a
  small floor beacon.
- **The trading card holder** (`items/tcg/tcg_machines.dm`, `icons/obj/toys/tcgmisc.dmi` `card_holder_inactive`,
  `card_holder_active`, the red team's kind): a low floor plinth with a card slot that lights when a card is in.
- **The summon hologram** (`tcg_machines.dm`, `icons/obj/toys/tcgsummons.dmi` `hologram`, with the card's own summon sprite on it): a
  translucent hologram base in the team's colour, the power and resolve numbers floating at its foot, the green gem of a marked summon.
- **The mana and life buttons and the display panel** (`tcg_machines.dm`, `icons/obj/toys/tcgmisc.dmi` `mana_buttons`,
  `health_buttons`; `icons/obj/toys/tcgmisc_large.dmi` `display_panel` with its `gem_blue`, `gem_red` and empty gems): a wall-high
  scoreboard panel with a row of gems for mana and life, and the button posts beside it.
- **The trading cards, packs, decks and binder** (`items/tcg/tcg.dm`, `icons/runtime/tcg/default.dmi` and `xenos.dmi` (the cards'
  fronts), `icons/obj/toys/tcgmisc.dmi` `cardback`, `cardpack_*`, `deck_up`, `deck_tcg`, `deck_tcg_low`, `deck_tcg_half`,
  `deck_tcg_full`, the card binder's `binder`): a flat card with its printed front or its back, a foil
  pack, a pile of cards growing with its count, a ring binder.
- **The model Orion ship** (`machinery/computer/arcade/orion.dm`, `icons/obj/toys/toy.dmi` `ship`): a toy spaceship on a stand, the
  emagged Orion Trail's prize.
- **The holodeck's ready button** (`modules/holodeck/items.dm`, `icons/obj/machines/wallmounts.dmi` `auth_off`): a wall button for the
  thunderdome's and the dodgeball court's ready check.

## The exploration drones
Gaps EX1 (D339), `modules/explorer_drone/`. The launcher (`launcher` with its fuel overlays) and the scanner dish (`scanner_off`,
`scanner_ready`, `scanner_on`, `scanner_open`) have their models already; the consoles are the computer's model. Meanwhile tg's sprites.
- **The exploration drone** (`exodrone.dm`, `icons/obj/exploration.dmi` `drone`): a bulky, two-handed probe with folded thrusters that
  stands on its launcher's cradle between trips; it leaves the cradle at the launch and lands back on it (or on a random station tile).
- **The fuel pellets** (`exodrone.dm`, `fuel_basic`, `fuel_advanced`, `fuel_exotic`): three canisters of one shape, coloured by grade,
  that go into the launcher's fuel port.
- **The drones' loot** (`loot.dm`, `antique`, `trade_chip`, `firelance` with its in-hand states `firelance` and `firelance_charging`): a
  ruined antique, the trade chip card, and the fire lance, a two-handed heavy beam drill whose tip glows while it charges.
## The trait jobs and the deployable turrets

- **The machine gun turrets** (gaps JB1, `structures/deployable_turret.dm`, `icons/obj/weapons/turrets.dmi` `machinegun` and `hmg`, eight
  directions each): a tripod-mounted machine gun and a heavier belt-fed heavy machine gun with a seat behind it for the gunner, turning
  on its mount to face where it is aimed (eight directions; a free rotation would read better in first person), with a muzzle flash on
  each shot. Meanwhile the client draws tg's directional sprites.
- **The folded heavy machine gun** (gaps JB1, `machinery/deployable.dm`, `turrets.dmi` `folded_hmg`, and its back and inhand sprites):
  the gun folded into a carrying bundle worn on the back. Meanwhile the client draws tg's sprite.
- **The human AI's pre-packaged camera console** (gaps JB1, `station_trait/human_ai.dm:162`, `icons/obj/devices/remote.dmi`
  `botpad_controller`): a boxed handheld controller; the console it unpacks into is the computer model (`computer`) with the camera
  console's screen. Meanwhile the client draws tg's sprite.
## The fish
Gaps FI1 (D339), `fishing/fish/`, `fishing/fish_mount.dm`, `aquarium/aquarium_kit.dm`. Meanwhile tg's sprites.
- **The fish** (`fishing/fish/types/`, `icons/obj/aquarium/fish.dmi`, `wide.dmi`, `rift.dmi`; each fish's `icon_state`, its dead
  state `[icon_state]_dead`, the flop of a fish out of water as tg's floating animation): about a hundred species in a few body
  shapes, each a model of its own size and colours (the item scales with the fish's size, tg's `update_size_and_weight`): the slim
  river fish (goldfish, guppy, angelfish, plasma tetra, perch, pike, salmon, sacabambaspis), the flat reef fish (clownfish,
  cardinal, greenchromis, dwarf moonfish, monkfish, lanternfish), the long fish (swordfish, needlefish, eel-like zipzap, the
  armored pike, the chainsawfish with its running saw blade), the round fish (pufferfish, sludgefish, mossglob, the slime fish),
  the crustaceans (chasm crab and its ice kin, sand crab, skin crab, armorfish), the star fish (starfish, chrystarfish), the
  jellyfish (gunner jellyfish), the odd ones (the mastodon and the bone fish as skeletons, the lavaloop ring, the holographic fish
  with their scanlines, the babbelfish and its two halves, the dolphish, the flumpulus, the gullion of quartz, the fryish and
  fritterish shaped like fries, a dinosaur and a pterodactyl, the tadpole, the baby carp, the moonfish eggs, the donkfish, the
  jumpercable, the emulsijack, the soul fish, the ratfish). The two-handed fish (pike, swordfish, chainsawfish) have their
  wielded in-hand states (`_wielded`).
- **The fish mount** (`fish_mount.dm`, `icons/obj/wallmounts.dmi` `fish_mount`, `fish_mount_item`): a wooden trophy plaque on the
  wall with the mounted fish's model on it (tg's vis_contents), and its frame as an item.
- **The stasis fish case** (`aquarium_kit.dm`, `icons/obj/storage/case.dmi` `fishbox`, `fishbox_bluespace`): a small plastic and
  iron case with a clear lid, the bluespace one with a blue glow; it grows with the fish inside it.
- **The fish encyclopedia** (`fish_catalog.dm`, `icons/obj/service/library.dmi` `fishbook`): a book with a fish on its cover; the
  book model with its own cover texture.
- **The babbel ears** (`types/rift.dm`, `icons/obj/medical/organs/organs.dmi` `babbearfish` and its body overlay): the split
  babbelfish's two halves, worn as obsidian tubes where the ears are.
- **The lavaloop's wind-up bar** (`components/throwbonus_on_windup.dm`, `icons/effects/effects.dmi` `windup_red`, `windup_green`,
  `windup_purple`): five coloured pips stacked beside the holder while the lavaloop heats up in the throwing hand; a billboard.
## The anomaly cores, the gizmos and the pads
Gaps OR2 (D339), `research/anomaly/`, `research/gizmo/`, the pads. The anomaly refinery (`explosive_compressor` with its `-off`), the
orbital pad (`mechpad`, `mechpad-open`), the ecto sniffer (`ecto_sniffer`, `-p`, `_open`) and the tucker tubes gizmo
(`gizmo_active_0`, `_on`) have their models already; the launchpad and orbital pad consoles are the computer's model. Meanwhile tg's
sprites.
- **The quantum pad** (`machinery/quantum_pad.dm`, `icons/obj/machines/telepad.dmi` `qpad-idle`, `qpad-idle-open`, the `qpad-beam`
  flick): a round floor pad with a glowing ring, its panel open for maintenance, a column of light when it sends and receives.
- **The gizmos** (`research/gizmo/gizmo_machines.dm`, `icons/obj/science/gizmos.dmi` `gizmo_0` to `gizmo_4`, `beyblade` with
  `beyblade_spinning`, `gizmo_active_1` to `gizmo_active_4` with their `_on` states): five odd machines of unknown purpose, a top-like
  gizmo that spins and wanders, and four more toggle gizmos that light up when they hum to life.
- **The gizmo items** (`gizmo_items.dm`, `gizmos.dmi` `gizmo_item_1` and the moo gizmo's): a hand-held strange device with wires.
- **The raw anomaly cores** (`raw_anomaly.dm`, `icons/obj/devices/new_assemblies.dmi` `rawcore_bluespace`, `_vortex`, `_grav`, `_pyro`,
  `_flux`, `_hallucination`, `_bioscrambler`, `_dimensional`, `_weather`, `_inert`): a rough crystal in each anomaly's colour.
- **The anomaly cores** (`anomaly_core.dm`, `new_assemblies.dmi` `anomaly_core`, `pyro_core`, `grav_core`, `flux_core`, `vortex_core`,
  `bioscrambler_core`, `hallucination_core`, `dimensional_core`, `weather_core`): the refined core, a cut gem in a metal cage.
- **The reactive armour and its shell** (`suits/reactive_armour.dm`, `icons/obj/clothing/suits/armor.dmi` `reactive`, `reactiveoff`
  and the shell's `reactiveoff`): a ribbed chest plate with sensor nodes, lit when on.
- **The polymorphic field inverter** (`belts/polymorph_belt.dm`, `icons/obj/clothing/belts.dmi` `polybelt`, `polybelt_inactive`): a
  belt with a glowing DNA buckle.
- **The perceptomatrix helm** (`head/perceptomatrix.dm`, `perceptomatrix_helmet`, `_inactive`): a closed helmet with a visor of
  lenses, dark without its core.

## Fishing
Gaps FI2 (D339), `fishing/fishing_rod.dm`, `fishing_equipment.dm`, `bait.dm`, `fishing_minigame.dm`, `fishing_portal_machine.dm`
(all states in `icons/obj/fishing.dmi` unless named). Meanwhile tg's sprites.
- **The fishing rods** (`fishing_rod`, `fishing_rod_bone`, `fishing_rod_science`, `fishing_rod_telescopic` and
  `_telescopic_collapsed`, `fishing_rod_master` and `_master_collapsed`, `fishing_rod_material` with its material colour; the reels
  `reel_overlay`, `reel_bone`, `reel_science`, `reel_telescopic`, `reel_master`, `reel_material` and the line's coloured reels
  `reel_white`, `reel_green`, `reel_red`, `reel_blue`, `reel_sinew`, `reel_auto`, `reel_bluespace`; the slotted parts' overlays
  `line_overlay`, `hook_*_overlay`, `worm_overlay`, `dough_overlay`, `battery_overlay`): a rod with its grip, reel and guides, the
  telescopic and master rods collapsible to a short tube, the tech rod's display; the reel shows the line slotted.
- **The fishing line** (`fishing_line`, tg's held beam from the rod to the bobber): a thin cord from the rod's tip to the bobber, in
  the line's colour.
- **The bobber** (`float`, the lit lure's `lure_light`): a red and white float that bobs on the spot and dips at a bite.
- **The hooks** (`hook`, `shiny`, `gold_shiny`, `weighted`, `rescue`, `hook_bone`, `gyro`, `jaws`, `treasure`, the anomaly hook `anom`
  with its core overlays): small hooks, the jaws a trap, the anomaly hook with its glowing core.
- **The lures** (`daisy_chain`, `minnow`, `plug`, `dropping`, `spoon`, `artificial_fly`, `led`, `lucky_coin`, `algae`, `grub`,
  `buzzbait`, `spinnerbait`): a painted lure of each kind, the LED one lit.
- **The bait can** (`bait_can`, `bait_can_open`, `bait_can_empty`) and its baits (`worm`, `doughball`, `doughball_blue`).
- **The fish-porter 3000** (`portal`, `portal_open`, `portal_on`, each dimension's portal state `portal_aquarium`, `portal_beach`,
  `portal_chasm`, `portal_ocean`, `portal_hyperspace`, `portal_randomizer`, `portal_syndicate` and the linked spots' `portal_lava`,
  `portal_river`, `portal_plasma`, `portal_tray`, `portal_rift`, `portal_fry`, `portal_mansus`): a machine with a round basin whose
  portal swirls in the dimension's colours while on, and its open panel.
- **The fishing bag** (`fishing_bag`, `fishing_bag_carpskin`), **the fish gene gun** (`fish_gun` with its inject and extract states)
  and **the fish trait injector** (`fish_trait_injector`).
- The minigame's HUD (`icons/hud/fishing_hud.dmi`) is a screen overlay drawn from tg's sprites; no model.

## Aquariums
Gaps FI3 (D339), `fishing/aquarium/`, `gloves/special.dm`, `boxes/fishing_boxes.dm` (the tanks' states in `icons/obj/aquarium/tanks.dmi`,
the kits' in `supplies.dmi`). Meanwhile tg's sprites, with the fish drawn into the tank's picture.
- **The aquarium** (`aquarium_base`, with `aquarium_glass_water`, `_glass_air`, `_glass_water_broken`, `_glass_air_broken`,
  `_glass_cracks`, `aquarium_borders`, `aquarium_panel`): a waist-high glass tank on a metal stand with a lid, the water inside, a
  control panel that opens on the side; the fish and props swim and stand inside, behind the glass (the model needs an inner volume
  the fish visuals are drawn in, and a cracked-glass state).
- **The bioelectricity generator** (`bioelec_base` and its glass, borders and panel states): the aquarium with copper coils and
  electrodes around the tank.
- **The fish tank** (`fish_tank`, `fish_tank_water`, `fish_tank_air`, `fish_tank_borders`, `fish_tank_panel`, `_panel_open`;
  the bluespace tank `fish_tank_bluespace` and its states): a carriable rectangular plastic tank with a handle, the bluespace one
  smaller with a blue glow.
- **The aquarium props** (`rocks`, `seaweeds_back`, `seaweeds_front`, `sand`, `treasure`, and their `_fish_tank` states drawn in a
  tank): small plastic rocks, seaweed, a sand board and a tiny treasure chest, as items and inside the tanks.
- **The aquarium kits** (`construction_kit`, `bioelec_kit`, `bluespace_kit`, `fish_feed`): a flat-pack kit box in three colours and
  a small can of fish feed.
- **The fish analyzer** (`icons/obj/devices/scanner.dmi` `fish_analyzer`, its `fish_analyzer_case` tinted by its random colour): a
  fish-shaped handheld scanner with a screen.
- **The athletic fishing gloves** (`icons/obj/clothing/gloves.dmi` `fishing_gloves`): sporty fingerless gloves with grip pads.
- **The fishing boxes** (the box `illustration` `fish`, the lures' tackle box `plasticbox`): a cardboard box with a fish on it and a
  small plastic tackle box.
- **Abductor, dullahan, ghost, shadow and vampire bodies** (gaps SP3b, `species_parts/misc_bodyparts.dm`, `icons/mob/human/species/abductor/`, `dullahan`, `ghost`, `shadow`): the grey three-fingered limbs; the detached **dullahan head** held in the hands (its own orbiting-free model with the eyes inside); the legless **ghost** chest with its see-through limbs; the see-through **shadow** limbs. Meanwhile tg's sprites.
- **Nightmare light eater** (gaps SP3b, `icons/obj/weapons/Nightmare_items.dmi` `light_eater`, `icons/mob/inhands/antag/nightmare_*`, the `crit_glow` outline): the armblade the heart of darkness grows, held in the hand with the critical-strike glow; and the **shadow walk** holder (`purple_laser`). Meanwhile tg's sprites.
## MetaStation's last machines and photography

- **Laptops** (gaps MT2, `modular_computers/computers/item/laptop.dm`, `icons/obj/devices/modular_laptop.dmi`): a laptop open
  (`laptop`, with its screen's program overlay, and `laptop-off` unpowered) and closed (`laptop-closed`), small enough to sit on a
  desk; MetaStation places two civilian ones. Meanwhile the client draws tg's sprite in 2D.
- **Photo booths** (gaps MT3, `machinery/photobooth.dm`, `icons/obj/machines/photobooth.dmi`): a booth one tile big with drapes and a
  camera, open (`booth_open`) and shut on its occupant (`booth_closed`), with its countdown light (the security booth's height chart is
  the same model); MetaStation has two. Meanwhile the client draws tg's sprite in 2D.
- **Picture frames** (gaps MT3, `photography/photos/frame.dm`, `icons/obj/signs.dmi` `frame-empty`, `frame-overlay`, the bar portrait's
  `frame-monkey`, `frame-fruit`, `frame-rat`, `frame-ratvar`): a flat wall frame that shows the framed photo's picture (its small icon)
  or a portrait; MetaStation has five. Meanwhile the client draws tg's sprite in 2D on the wall.

