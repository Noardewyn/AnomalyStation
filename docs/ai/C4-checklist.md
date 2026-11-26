# C4 playtest checklist (M7x weapons, M8 medicine, M8x cryo, M8y stasis and buckling, M8z basic interactions)

The owner's playtest after M8 (D178). The README's "Run" section has the server and client commands; run the server with `--client-console` so the dev commands work from the in-game console (backtick). `give me TYPE` (or `spawn TYPE`) hands out gear; `outfit me TYPE` dresses you as a job. Each line names tg's behaviour to expect; the automated check that covers it is in brackets. Lines marked "look" have no automated check of their pixels.

## 1. Aiming and hits (D177)

- [ ] A shot leaves your eye along the crosshair: aim at a target's head, then an arm, then a leg; the damage lands on that part (the health doll or an analyzer shows it). [AimedShotGameplayTests, M7xScenarioTests]
- [ ] Aim just beside a body, between the legs or over a shoulder: the shot flies on and dents the wall behind. A lying body is hit only where the shot crosses it. [AimedShotGameplayTests]
- [ ] Aim at the floor or the ceiling: the shot stops there with its impact. [ShotGeometryTests]
- [ ] A pistol's or a shotgun's spread scatters up and down as well as sideways; recoil kicks the view up and back. Look: the kick's size and return.

## 2. Energy guns

- [ ] The disabler (Security, suit storage) drops a target to stamina crit in tg's number of hits; the laser burns and sets a carbon alight as tg; the e-gun and the HoS's gun switch modes with their sound and overlay. [EnergyGunGameplayTests, EnergyGunTests]
- [ ] Look: the 3D bolts (laser red, disabler blue, ion, electrode, temperature), their light on walls in the dark, the muzzle flash, the impact burst, your own gun's kick in first person (D176).
- [ ] A recharger charges a gun notch by notch; look: its charging and full states and light. An empty gun dry-fires. [EnergyGunGameplayTests]
- [ ] A gun without a firing pin refuses with tg's message; the clown pins honk. [EnergyGunTests]
- [ ] The taser's electrode tases (the tased effect and alert). [ShotgunTests]

## 3. Ballistics

- [ ] The detective's revolver from his holster: fire, dump the cylinder, reload from a speedloader; casings drop with their sound; the Russian revolver spins. [BallisticGameplayTests]
- [ ] The Makarov's magazine out and in; the ammo count on the gun's sprite. [BallisticTests]
- [ ] The riot shotgun: pump (rack sound), fire, reload shell by shell; buckshot pellets each hit or miss on their own; the double-barrel opens. [ShotgunGameplayTests]
- [ ] The WT-550 fires full auto while the button is held and stops on release. [FullAutoGameplayTests]
- [ ] The battle rifle's scope zooms the first-person view; the rifle degrades and a recharger repairs it. [ScopeGameplayTests, ShotgunTests]
- [ ] A gun held at someone (right click) holds them up. [ShotgunTests]

## 4. Batons, flash, cuffs

- [ ] The stun baton (on/off, the cell): a hit, then the knockdown two seconds later; the telescopic baton extends. [RestraintGameplayTests, baton unit tests]
- [ ] The flash blinds (look: the white screen on the victim's client); a burnt-out flash. [RestraintGameplayTests]
- [ ] Cuff a downed man: his hands hold nothing; he resists out with the resist button; wirecutters cut cable cuffs; a bear trap catches a walker's legs. [RestraintGameplayTests, RestraintTests]

## 5. Medicine (M8)

- [ ] The paramedic's whole job: `outfit me /datum/outfit/job/paramedic`; on a friend give `wound NAME l_arm slash severe`, `damage NAME burn 60 chest`, `breath NAME 20`, `damage NAME oxy 60`, `heart NAME stop`. He falls into hard crit; with medbay's things alone (the storage counter's windoors open to your ID) bring him back: out of crit, nothing bleeding, breathing for a minute. [M8ScenarioTests]
- [ ] The health analyzer: a click scans (the scan frame flickers, the beep); only you get the readout whole in a blue box in the chat with tg's exact numbers, the limb table with each wound and the organ table; Z switches the mode to chemicals and wounds; the simple analyzer gives the wound advice. Your friend's chat shows nothing. [FirstAidGameplayTests, M8ScenarioTests, FirstAidTests]
- [ ] Wounds by part: aim a suture, gauze, mesh or ointment at a limb (DV30) and it treats that limb, with tg's delays (longer on yourself), sounds and messages; gauze shows its bandage on the limb; a mesh pack is sealed until you use it in hand (Z); on yourself, click the limb on the health doll in cursor mode (Tab). [FirstAidGameplayTests, FirstAidTests]
- [ ] Gunshots: a .38 at an arm leaves a pierce wound that bleeds, a laser on the chest burns it (and cauterizes a cut); the analyzer names each; suture and gauze stop the bleeding, mesh heals the burn. [GunshotTreatmentGameplayTests]
- [ ] CPR with an empty hand on someone in crit lying down: tg's 3 seconds, 7 oxygen damage off a pump, faster when he is near death. [M8ScenarioTests, FirstAidTests]
- [ ] The defibrillator: wear it on the back and click it (or hook it into a wall mount and click the mount) for the paddles, Z wields them, a click on the chest: tg's 3 + 2 seconds, the charge, zap and success sounds; it restarts a stopped heart, revives a body dead less than tg's limit whose player is near (the ghost is pulled back), and refuses with tg's reasons (no heart, a suit in the way, too much damage). Look: the paddles in first person, the unit's lights on the mount. [DefibGameplayTests, DefibTests]
- [ ] Medicines work at tg's rates: the medipen injects at once (the pen turns used), the syringe draws (right click) and injects in 3 seconds with its fill shown in your hand, a pill is fed in 3 seconds, a patch sticks, the IV drip feeds a blood pack at 5 u/s with its line to the patient; overdoses and the liver as tg. Look: the syringe's fill colour, the drip's line. [MedicineDeliveryGameplayTests, MedicineGameplayTests, DeliveryTests, MedicineTests, MetabolismTests]
- [ ] The medical HUD glasses (`give me /obj/item/clothing/glasses/hud/health`, E to put them on): a health bar and a status icon float over every person's head, for the wearer only; the security HUD shows jobs and the mindshield. Look: the bar's colour by health. [MedHudGameplayTests, HudTests]
- [ ] Hallucinations: `hallucinate me 60` (the random ones: sounds, fake fire, xenos, bubblegum, the screwy health doll...), `hallucinate me /datum/hallucination/delusion/preset/syndies 30`: the others look like syndicate operatives to you alone and can still be clicked; nobody else sees any of it. [HallucinationGameplayTests, HallucinationTests]

## 6. Cryo (M8x)

- [ ] Meta's cryo room (80,110): wrench the anaesthetic canister onto its port (81,107) first (it is not wrenched at round start, as tg's map), turn the freezer to its minimum in its window, put a cryoxadone beaker from the counter into a cell, alt-click the cell open; stun a hurt friend and drag him in: the cell closes and turns on, its hum plays, and he heals as tg once his body is below 273 K (faster knocked out); the cell warns and opens him out when he is healed. Ctrl-click switches the cell; the Cryo window shows his state, temperature and damages and the beaker. [M8xScenarioTests, CryoGameplayTests]
- [ ] Look: the cell's open, off and on pictures and the opening and closing animations; the patient seen inside behind the glass, bobbing while it runs (drawn at about half scale to fit under the ceiling, DV122); inside, your own view is dark but for a small window.

## 7. Stasis beds, buckling, drag gestures, closets (M8y)

- [ ] Buckling: in cursor mode (Tab) drag a friend onto a chair, stool, bed or roller bed (tg's 2 s): he is buckled, sits facing the chair's way or lies on the bed; a click with an empty hand unbuckles him; a roller bed rolls with him when pulled; cuffed, he resists out in a minute. [BuckleGameplayTests, BucklingTests]
- [ ] The radial menu (hold the right button): Buckle yourself onto a chair and Unbuckle; Pull a crate and Stop pulling; each shown only when it can be done. [DragGestureGameplayTests]
- [ ] Drag gestures in cursor mode: an item from the floor into a hand, onto yourself, onto a doll or HUD slot, into your backpack or a box; from your hand onto a table, a closet, the floor beside you. A refusal tells you why (too big, full, wrong slot, hands full, out of reach). [DragGestureGameplayTests, DragGestureTests]
- [ ] The Treatment Center's stasis beds (79,105 and 85,105): drag a dying friend (`wound NAME chest slash severe`, `damage NAME oxy 60`) onto a bed: the mattress glows in over 5 s, a ripple runs over him, he gets the Stasis alert and stops getting worse; alt-click switches the bed (5 s apart) and he bleeds again; the room's breaker stops it; a body dead on it keeps its defib window. Look: the mattress fade and the ripple. [StasisGameplayTests, M8yScenarioTests]
- [ ] Closets: shut in a locker, walk to open it; welded or locked, resist and break out in tg's 2 minutes while the locker shakes for the others; in the kitchen's freezer locker you keep breathing and grow cold. [ClosetBreakoutGameplayTests, ClosetAirGameplayTests]
- [ ] Bystanders now see actions' messages ("X shoves Y", "X buckles Y to the chair").

## 8. Basic interactions (M8z)

- [ ] The strip menu: in cursor mode (Tab) drag a downed friend onto yourself (or the radial's Strip on him): his strip window shows his slots (pockets hidden); take his shoes and ID with tg's delays (he reads "You feel someone fumble with your belongings." when he cannot see, the others read "X tries to remove Y's shoes"), put them back from your hand, set his suit sensors, untie or knot his laces. Look: the window and the items' pictures. [StripGameplayTests, M8zScenarioTests]
- [ ] Climbing: the radial's Climb on a table, rack, railing, crate, canister or tank (or drag yourself onto it): tg's 2 s, then you stand on it (the view rises) and a 2 s stun; walk from table to table and step down; vault a railing; a click knocks a climber off. [ClimbGameplayTests]
- [ ] Pointing: shift and the middle mouse button on something: the others read "X points at Y." and see tg's red arrow over it for 2.5 s; on a thing in your own hands, a thought bubble over your head. Look: the arrow. [PointVerbHudGameplayTests]
- [ ] The HUD: the rest button (U too) lies you down and up; lying, the sleep button shows above Throw and asks before you sleep. The resist key B frees you from a pull, cuffs, a buckle or a closet. [PointVerbHudGameplayTests]
- [ ] The radial over the floor: Sleep (tg's question; "You are already sleeping!"), Succumb ("You are unable to succumb to death!" when healthy). In crit the Succumb alert asks for your last words (tg's text window) and ends your life. [PointVerbHudGameplayTests, M8zScenarioTests]

## 9. Known gaps (expected, not bugs)

- Magic, syndicate-only and exotic guns, launchers, grenades and flashbangs, gun attachments (seclights, bayonets), the DRAGnets (inert, D179).
- The cuffed-item leash (cuffing a toolbox or a chair) waits (not-translated section 46).
- Animals and bots are hit by tg's 2D rule, not by their drawn picture (D180).
- Medicine (not-translated sections 50-56): reagents without their own translated procs metabolize with no effect (coffee, sodas, drugs); chemistry machines, reactions, hydroponics, diseases and the disease analyzer, surgery waits (D188); the sleeper is not ported (D216: Meta maps none); addictions, moods, emotes (the screams, gasps and yawns of the defib, the heart attack, the medicines) and the medical records and their HUD links are later milestones (M9, M10).
- No printing of the analyzer's last scan (ctrl-shift-click) yet; a held item's flick (the analyzer's scan flash) is not drawn in the hand or the first-person view.
- Data HUDs: no diagnostic HUD (silicons, mechs, airlocks); only the station jobs' implants and skillchips (no explosive, storage, freedom or uplink implants, no prisoner console); the generic skillchips and the chef's martial art wait.
- Basic interactions: the strong grabs (a second ctrl-click on a pulled mob still says "not ported"), the fireman carry and piggyback, the table slam and flip are M9's (D234); the last words are not whispered (say is M9's); the point arrow does not slide (DV159).
- Hallucinations: the fake station announcements play without the announcer, the chat hallucination finds no crew names (no manifest yet), your mother does not follow you, the eyes in the dark do not glow (no emissive plane).

## 10. Report

Note anything that differs from tg with the room and the steps, and any runtime the server console prints beyond the boot's.
