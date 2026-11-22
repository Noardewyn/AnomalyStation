# C3 playtest checklist (M7, electrics)

The owner's playtest of ROADMAP M7 on MetaStation. The README's "Run" section has the server and client commands and the "Dev commands" table; run the server with `--client-console` so the dev commands work from the in-game console (backtick). Coordinates are `X Y Z` on Meta's station level (z 1); F4 shows your own. Join as an engineer (`--name`), or take tools with `give me TYPE`. Each line names tg's behaviour to expect; the automated check that already covers it is in brackets. The visual lines (marked "look") have no automated check of their pixels: they are the S7 visual check.

## 1. The grid

- [ ] The round starts on tg's charged SMESes: the Engineering SMES window shows charge, input and output; turning its output off drops the station's supply. [SmesPacmanGameplayTests]
- [ ] A PACMAN on a cable with plasma sheets, wrenched down and started in its window, adds power; look: its running sprite and sound. [SmesPacmanGameplayTests]
- [ ] Cut an exposed cable with wirecutters: the coil piece drops and the neighbours' icons end at the cut; lay it back with the piece. Bare-handed on a live cable you may be shocked: burn, stun, the skeleton flash and sparks; insulated gloves stop it; a grille over a live cable bites. [CableCutGameplayTests, ShockGameplayTests]
- [ ] A room's blackout: unlock its APC with your ID, open the cover, take the cell out, cut the APC's feeder cable: the lights go dark, the doors stop opening, the vents stop; lay the cable back and everything returns within a few seconds (SSmachines cycles of 2 s). [M7ScenarioTests]
- [ ] An APC's window: its channels (equipment, lighting, environment) set by hand switch the room's machines, lights and air; the cell charges from the grid; with the grid cut and the cell draining, tg's thresholds turn lighting and equipment off first. `areapower X Y light off` does the same from the console. [ApcGameplayTests]
- [ ] The power monitor console on the Bridge: every APC on its network with charge, load and channel states, the supply and demand history chart; switching an APC's channel shows on the next update. [PowerMonitorGameplayTests]
- [ ] `emp X Y 1 3 6` near machines and a player with a flashlight and a radio: machines flicker or break as tg rolls them, cells drain. [EmpGameplayTests]

## 2. Lights

- [ ] Wall light switches (130 on Meta) turn their room's fixtures off and on with tg's click; look: the switch's sprite and the room going dark in 3D. [LightingGameplayTests]
- [ ] Break a bulb (a toolbox), take the broken bulb out by hand, put a new one from a light replacer or a box; a broken powered tube sparks now and then. [LightingGameplayTests, LightReplacerGameplayTests]
- [ ] A blackout in a room: tg's emergency lights come on in low-power mode, then fade; look: the dim red light. [EmergencyLightGameplayTests]
- [ ] Build a light fixture: the wall frame from iron, wire it, put a bulb in, it lights from the room's power. [LightConstructTests]
- [ ] Flares (they burn out into trash), glowsticks (crack to light, colours), candles and torches lit with a welder or lighter; look: their light colour and size. [LightingGameplayTests, FlareTests]
- [ ] Reading in the dark: a paper or book in an unlit room says it is too dark; a gas analyzer refuses there until the lights return or you hold a flashlight. [DarknessGameplayTests]
- [ ] Options, "Shadowed lights": high gives 10 lamps real shadows, medium 4; the number changes at once. Look: 10 close lamps all cast shadows.
- [ ] Look: the hull and the floors at the station's edge are lit by the stars (starlight).

## 3. Engineering tools

- [ ] Multitool (the arrow to a buffered machine), t-ray scanner (hidden cables and pipes under the floor), gas analyzer, inducer (charges an APC's cell), rapid cable layer, light replacer, engineering holosign, utility belt, electrical toolbox. [the engineering tools' gameplay tests]
- [ ] Engineering scanner goggles cycle off, meson and T-ray with their action button; the atmos technician's version adds pipe connections and the thermal air view. Look: the meson outline and tint, the T-ray images, the thermal colours at 300 K and 500 K (its gradient is flagged for a BYOND comparison). [EngineGogglesGameplayTests]
- [ ] The RPD (the CE's and the atmos technicians' lockers, and 11 on Meta): its window's tabs, previews, layers and paint; lay a run of pipes with a pump between two pipes; wrench, destroy and reprogram modes; the pressure gate, temperature gate (its window, the multitool's inversion) and temperature pump. The Disposals and Transit Tube tabs show but say they are not translated yet. [RpdGameplayTests]
- [ ] A computer taken apart with a screwdriver leaves its frame (stage sprites 4 to 0) and is built again with a crowbar, wirecutters, the board, cable and glass; a new frame from five iron sheets. [ComputerFrameGameplayTests]

## 4. Solars

- [ ] The Port Quarter array: Meta leaves it unwired, so lay the missing cable first; the solar control's window scans for hardware, tracks the sun and the panels turn; the array's SMES charges. [SolarGameplayTests]

## 5. The supermatter engine

- [ ] The engine: nitrogen into the cooling loop, the two freezers on (ThermoMachine windows), the loop's pumps on (AtmosPump windows), the emitters on by hand: the beams energise the crystal, it zaps the tesla coils and the network's supply rises; the waste heats the gas. Look: the crystal's sprite, glow and rays, the zaps' beams, the emitter shots. [SupermatterGameplayTests]
- [ ] `smcrystal X Y status`, `smcrystal X Y power 5000`: a hotter engine; radiation (`irradiate`) shows on a Geiger counter and the radiation alerts. [RadiationGameplayTests]
- [ ] Delaminations: `smdelam X Y` (explosive), `smdelam X Y singularity` (the singularity eats the engine), `smdelam X Y tesla` (the energy ball and its mini-balls zap), `smdelam X Y cascade` (the crystal mass and the rift). Anomalies scatter over the station (vortex, bioscrambler, dimensional: its themed floors, plasma ones set bare feet alight). Look: each effect's sprites and sounds. [DelaminationGameplayTests, DelamAnomalyTests]
- [ ] A body burnt to a husk (a plasma fire, or the delamination): grey-brown skin, no hair, red husk patches. [HuskGameplayTests]

## 6. Known gaps (expected, not bugs)

- 3D models are missing for the supermatter crystal, the freezer and the heater, the meters and the heat exchanger: they show as tg's sprites (the owner, 2026-10-21: the models come after 0.7.0).
- Containment fields and the singularity/tesla engine are out of M7's scope (inert); turbines, the TEG and the RTG come after the core version.
- The RPD's disposals and transit tube tabs, the RPED in a frame, the supermatter's hallucination pulse (M8), wire hacking.

## 7. Report

Note anything that differs from tg with the room and the steps, and any runtime the server console prints beyond the boot's.
