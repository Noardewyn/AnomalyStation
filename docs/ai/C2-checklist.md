# C2 playtest checklist (the first demo, D87)

ROADMAP section 8, checkpoint C2: the owner plays MetaStation and covers every D87 area. The README's "Run" section has the server and client commands and the "Controls" and "Dev commands" tables. Run the server with `--client-console` so `teleport` and `spawn` work from the in-game console (backtick). Coordinates are `X Y Z` on Meta's station level (z 1); F4 shows your own. Each line names the tg behaviour to expect, and the automated check that already covers it is in brackets.

Bots: `dotnet tools/Anomaly.Tools.Bots/bin/Release/net10.0/Anomaly.Tools.Bots.dll --bots 10 --minutes 10 --behaviour mixed` (build with `dotnet build AnomalyStation.sln -c Release` first). A dummy to hit: `spawn /mob/living/carbon/human X Y`.

## 1. Basic server

- [ ] The server prints "joins open"; two clients join (a second `--name`), see each other walk, and the bots walk too.
- [ ] Moving, running (C) and turning are smooth at `--sim-latency 100 --sim-loss 2` (prediction). [M2ScenarioTests]
- [ ] Leave and rejoin: you get your body back; no runtime is printed in the server console beyond the boot's.

## 2. Character, locomotion, basic medical

- [ ] Footsteps while walking; lie down and get up (U) takes tg's delay; a shove (right click in combat mode, F) moves a player a tile. [M2ScenarioTests]
- [ ] `damage me brute 30 chest`, `damage me burn 30 l_arm`, `damage me tox 40`, `damage me oxy 40`: the health doll shows each zone; another player sees the injury overlays; F9 shows the health analyzer panel.
- [ ] `damage me tox 150`: hard crit, then death at tg's pace; the other player sees the body lying. `revive me` brings you back.
- [ ] `stun me 3`, `knockdown me 3`, `effect me add TYPE` (`effect types` lists them).

## 3. Map, items, light

- [ ] Walk the station: walls, floors, ceilings (F2 hides them), doors, lights and darkness; maintenance is dark until lit.
- [ ] Doors: a public airlock opens to you; a security door (57,138) does not open to the assistant's card. [M4aScenarioTests]
- [ ] Some doors with tg's "abandoned" helper roll per round: welded, bolted, a wall or a false wall (the Vacant Office door at 60,126 with SEED 1 is welded; that is tg's behaviour).
- [ ] Pick up, drop, throw (R and a click), open a toolbox with the other hand, take a tool from its window. [M4aScenarioTests]

## 4. Atmosphere and space

- [ ] Fitness Room (139,184): `breach 136 184`: the room drains, the space wind pulls you and loose items, the firelock (138,182) closes with its siren. `patch 136 184` and `valve` on a canister refill it. [M4bScenarioTests]
- [ ] Fire: `air add plasma 50 139 184` in the Fitness Room, then a lit welder thrown in; hotspots, the fire puff and the crackle; the Recreation Area extinguisher puts it out. [M4bScenarioTests]
- [ ] EVA: with the Captain's spare (`give me /obj/item/card/id/advanced/gold/captains_spare`), suit up in E.V.A. Storage, cycle the Auxiliary Airlock, fly with the jetpack, anchor with the magboots, come back. No oxygen loss on the way. [M4cScenarioTests]
- [ ] Walking into space unprotected: cold, low-pressure damage and suffocation show on the doll.

## 5. Construction and destruction

- [ ] Aux Base Construction room (x 42-51, y 147-160): iron, glass and rods on the table (51,149) and (51,148). [M5ScenarioTests]
- [ ] Slice a wall with the welder (10 s), rebuild it with two iron; build girders, a wall, a grille with a window, an airlock from an assembly; take each apart again with tg's tools.
- [ ] The bridge fire axe cabinet (115,134): break the glass with a toolbox, wield the axe (Z), break a reinforced window to space in one hit; seal it with glass and a screwdriver.
- [ ] C-4 on the zone's west wall (42,155): set the timer, walk out; the blast shakes the view, breaches the wall, and the zone vents. [M5xScenarioTests]
- [ ] `explode X Y 1 2 3`, `singularity X Y` in an empty area: tg's damage pattern; the singularity eats and grows. [M5xScenarioTests]

## 6. Dismemberment and wounds

- [ ] On a dummy or a second player: the fire axe in combat mode at an arm (aim zone) mangles and then severs it: the dismember sound, the arm and what it held on the floor, blood around; the victim's doll shows the arm missing; that hand cannot pick up. [M6ScenarioTests]
- [ ] `dismember WHO l_leg`: the victim falls and crawls slower than a walk.
- [ ] `wound me chest slash severe`, `bleed me l_arm 3`: bleeding, blood trails and footprints; `blood me 300` shows the low blood state.
- [ ] Gauze on a bleeding limb stops the bleeding and shows on the body; `gib WHO` leaves gibs and the limbs.
- [ ] The contact sheet (approved 2026-10-20): the doll and the body look the same in play.

## Report back

For each failed line: where you were (F4), what you did, what you saw, and a screenshot if it helps. "Different from tg" and "looks wrong in 3D" are both worth a line.
