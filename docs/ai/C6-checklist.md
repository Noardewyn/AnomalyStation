# C6 playtest checklist (M10, the core version)

The owner's playtest after M10 (D266-D279), the core version's checkpoint (ROADMAP section 8, "C3, end of M10"). Play a whole round on MetaStation with two or three clients and some bots (the README's "Run" section; the shipped `config/server.txt` turns the lobby and the open jobs on). Each line names tg's behaviour to expect; the automated check that covers it is in brackets. Lines marked "look" have no automated check of their pixels.

Bots for company: `dotnet tools/Anomaly.Tools.Bots/bin/Release/net10.0/Anomaly.Tools.Bots.dll --bots 12 --minutes 90 --behaviour wander` (they ready up in the lobby and late-join after the start).

## 1. The lobby and the character (slices F, D278, D279)

- [ ] Joining before the round: tg's lobby screen (the title picture, the menu with its sounds and flicks, the shutter, the countdown, the player count), the lobby music. Ready toggles; the host's client has Start Now. [LobbyGameplayTests]
- [ ] Setup Character: tg's random character; change the name (the dice re-roll it), the gender and the age. Make a second and third character in the slots at the top, switch between them, delete one. [LobbyGameplayTests]
- [ ] Occupations: set jobs to High, Medium or Low, choose what happens when none is free; Show Characters, then drag a character onto a job. At the round start you get a job by those choices, playing the character you put on it. [LobbyGameplayTests]
- [ ] Look: the windows' layout, colours and fonts against tg's (the preview of the character, hair and the other pages are not ported, below).

## 2. The round start and the late join (slices F, G2)

- [ ] When the countdown ends (or Start Now): every ready player appears at their job's start in tg's outfit with the name and age set, the crew manifest lists them, the lobby closes; the round-start sound plays. [LobbyGameplayTests, M10ScenarioTests]
- [ ] After the start, Join opens tg's job menu (the departments, the slots, the closed jobs greyed with their reason, Random Job); you arrive buckled on a chair of the docked arrivals shuttle with the arrival announcement. [LobbyGameplayTests, M10ScenarioTests]
- [ ] Late-join as the captain: tg's announcement, the outfit, the spare ID safe's code in chat (DV191); open the safe on the bridge with its keypad and take the spare ID. A lone Head of Personnel becomes the acting captain. [HeadsGameplayTests]
- [ ] Leave during the round and come back: straight into your body (the others saw you SSD); leave from the lobby and come back: a new lobby. [LobbyGameplayTests, ReconnectTests]

## 3. Shuttles and the round's end (slices G, H)

- [ ] Look: the cargo, mining, labour and arrivals shuttles stand at Meta's docks, turned as tg turns them; the escape pods are in their bays.
- [ ] The communications console on the bridge (with a head's ID, or `shuttle call` from the dev console): call the shuttle with a reason, tg's announcement and timer; recall it. [ShuttleGameplayTests]
- [ ] The emergency shuttle docks at the evacuation bay with tg's announcement; board it; it leaves into transit (hyperspace around you) and the round ends at CentCom. [ShuttleGameplayTests, RoundEndGameplayTests]
- [ ] The round's end: tg's report in chat (the round's length, the station's integrity, survived or marooned), the reboot countdown on the screen; the client reconnects by itself into the next round's lobby. `reboot 10` from the console ends a round at once. [RoundEndGameplayTests, M10ScenarioTests]

## 4. Admin tools (slice I)

- [ ] The localhost client is an admin; others are admins by `config/admins.txt`. From the server console or an admin's console: `ban KEY 30 reason`, `bans`, `unban ID`; the banned player is disconnected with tg's text and refused at the next connect. `note KEY text`, `watch KEY text`: the note shows to the player at the next connect, the watchlist line to the admins. [AdminGameplayTests]
- [ ] A name is a key: a second client with the same name and no token is refused. [AdminGameplayTests]

## 5. The network (slices B-E)

- [ ] Walk with `--sim-latency 100 --sim-loss 2`: your own walking has no visible correction; opening a door and picking things up react at once (predicted), and the others' motion stays smooth. [PredictionGameplayTests, RealServerPredictionTests, LossyNetworkTests]
- [ ] Look: things and people appear as you walk into a new part of the station without a stall; doors far down a corridor are drawn. [InterestGameplayTests]
- [ ] The security camera console (the bridge, security): pick a camera, its picture shows that place live; walk away from the console and the picture closes. [CameraConsoleGameplayTests]

## 6. A long round with bots (slice J, the done-when)

- [ ] A round of an hour or more with a dozen bots and two players: no crash, the server's tick stays smooth, the shuttle ends it and everyone rides the restart into the next lobby. [M10ScenarioTests, `tools/soak/run.sh` results in multiplayer.md section 15]

## 7. Known gaps (expected, not bugs)

- The character preview, hair, clothes and the other preference pages (loadout, antagonists, quirks, the game preferences) are not ported (not-translated.md 79); the lobby's Observe is off (DV182).
- The arrivals shuttle stays docked (DV189); CentCom is a stand-in dock, not a map (DV190); the shuttle consoles and the cargo console are after M10.
- The silicons (AI, cyborgs) stay closed; antagonists, the economy, security levels, suicides and the admin panels are after the core version (not-translated.md 84).
- The round-end report is in chat, not a browser window (DV192).

## 8. Report

Note anything that differs from tg with the room and the steps, and any runtime the server console prints beyond the boot's.
