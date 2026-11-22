# C7 playtest checklist (M10x: the lobby finished, observers, the character's looks)

The owner's playtest after M10x (D281-D284, D286, `docs/ai/specs/lobby.md`). Play on MetaStation with two clients, one of them an admin (the
host's client in the lobby, or a key in `config/admins.txt`); the shipped `config/server.txt` turns the lobby and the open jobs on.
Each line names tg's behaviour to expect; the automated check that covers it is in brackets. Lines marked "look" have no automated
check of their pixels.

## 1. The lobby's chat (slice A)

- [ ] In the lobby the big chat window (Shift+T) and the compact bar work over the lobby screen; OOC reaches the other lobby players and
  the players in the round; Say, Radio and Me are greyed (tg's reach no one from the lobby). [LobbyChatGameplayTests]
- [ ] After the round's end the lobby sees dead chat. [LobbyChatGameplayTests]

## 2. Observing and dead chat (slices B and C)

- [ ] The lobby's Observe asks tg's question, then you are a ghost at Meta's observer landmark with your character's looks and hair;
  the lobby music stops; "has observed." in dead chat. [ObserverGameplayTests]
- [ ] Dead chat (its tab): ghosts and the dead talk; deaths and arrivals are announced there. [ObserverGameplayTests, GhostScenarioTests]
- [ ] The ghost's Orbit menu: orbit a player, a machine or the supermatter (the camera circles it); Teleport to an area; Jump to a mob;
  follow; Observe a player (their first-person view and their HUD). [ObserverOrbitGameplayTests, OrbitWindowTests]

## 3. The Changelog (slice H)

- [ ] The lobby's Change Log button opens tg's Changelog window with this file's versions; the lobby button blinks while there is an
  unread version. [ChangelogGameplayTests, ChangelogWindowTests]

## 4. Settings, the window rule, the Esc menu (slice G)

- [ ] In game Esc only opens and closes the menu; in a text field Esc only leaves the field. Tab frees and captures the mouse; a window
  you open (a machine, the PDA, a bag) frees it and gives it back when closed. Every window closes, drags, resizes and keeps its place
  after a restart. (look)
- [ ] F10 and the lobby's gear open Settings: Controls, View, Graphics, Accessibility, Keybindings. (look)
- [ ] The Esc menu: Resume, Character (in the round: look only, the edits are refused), Settings, Players, Admin Help, Bug Report,
  Leave Body (Ghost, with tg's question), Quit; tg's sounds; the buttons look like buttons. [EscapeMenuGameplayTests]
- [ ] Bug Report: fill the form with the screenshot attached; it is saved under `data/bug_reports` and posted to the webhook when one
  is set. [BugReportGameplayTests]

## 5. Admin help (slice G6)

- [ ] A player's Esc menu, Admin Help, Create Admin Ticket: tg's box; Submit. The admin hears tg's sound and sees the ticket in chat
  with its links; the player cannot open another ticket for 2 minutes. [AdminHelpGameplayTests]
- [ ] The admin clicks the player's name in the ticket and answers: the player gets tg's red box with the sound; the player's Admin Help
  blinks red; View Latest Ticket shows the ticket's log. The player answers through the admin's name. [AdminHelpGameplayTests]
- [ ] The admin types `tickets` in the console: tg's ticket list; its link opens the ticket's panel (Refresh, Re-Title, the log);
  REJT, IC, CLOSE and RSLVE work with tg's messages. `pm KEY` messages a player. [AdminHelpGameplayTests]
- [ ] With `ADMINHELP_WEBHOOK_URL` set and no admin online, a ticket reaches the Discord channel. (Discord: not checked by a test; the
  test posts to a local server.) [AdminHelpGameplayTests]

## 6. The character's looks and the preview (slices D and E)

- [ ] Setup Character: the hairstyle, beard, underwear, undershirt, socks, backpack and jumpsuit buttons open tg's pictures; the colour
  buttons open the colour picker; a long list keeps its scroll when you pick. [LobbyGameplayTests]
- [ ] The preview shows the character in the highest priority job's outfit; the arrows and Rotate turn it at once; the dice randomize.
  [LobbyGameplayTests]
- [ ] In the round the character has the chosen hair, eyes, underwear and backpack; people blink; a hat hides the hair; every backpack
  offered works (the duffel bag zips). [HairGameplayTests, DuffelBagGameplayTests]
- [ ] Restart the server: the characters and their slots are still there. [PlayerSavesGameplayTests]
- [ ] Look: the Character page, the pictures and the preview against tg's.
