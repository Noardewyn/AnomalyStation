# M10x: the lobby finished, observers and the character's looks (plan, 2026-10-27, D281)

The owner's request after M10 (2026-10-27): finish the lobby (the character setup and the lobby's other buttons), the big chat
window in the lobby with tg's channels and timing, and observers with the orbit menu, teleport, follow and a player's eyes. tg's
reference for each slice is read at its start; this file grows a results section per slice as multiplayer.md did.

## 1. What tg does (read 2026-10-27)

- **The lobby's buttons** (`modules/lobby_menu/lobby_menu.dm:323-400`): Ready, Join, Observe (`make_me_an_observer`), Setup Character
  (the preferences on the character tab), Settings (the preferences on the game tab), Changelog (`client.changelog()`), Crew Manifest,
  Polls (the database), Start Now (the localhost admin). Ours: all drawn (slice F); Observe disabled (DV182), Settings opens no page,
  Changelog deferred, Polls not translated.
- **Chat in the lobby**: tg's chat panel (tgui-panel) is on screen in the lobby as in the round. tgui-say's channels are Say, Radio,
  Me, OOC, Admin (admins only), Pray (`tgui-say/ChannelIterator.ts`). A new player's `say` is `/atom/movable/say` with no hearers (the
  lobby mob is in nullspace), so only OOC (`client/verbs/ooc.dm`) reaches anyone. OOC is allowed unless `GLOB.ooc_allowed` is off:
  the ticker turns it off at the round's setup when `OOC_DURING_ROUND` is not set (`ticker.dm:247`) and on at the round's end
  (`ticker.dm:177`); tg's shipped `game_options.txt` sets the flag, so OOC is always on there. Admins bypass the mute. Dead chat
  (`deadchat_broadcast`, `__HELPERS/mobs.dm:261-300`) skips new players unless the round is finished (`GAME_STATE_FINISHED` shows it
  to everyone).
- **Observing** (`new_player.dm:72-112`): BYOND's alert "Are you sure you wish to observe?", a new `/mob/dead/observer` with
  `started_as_observer`, moved to `/obj/effect/landmark/observer_start` (one on MetaStation), the key moved, `set_ghost_appearance`,
  the real name from the preferences, the lobby music stopped, "<name> has observed." in dead chat (`DEADCHAT_DEATHRATTLE`), the new
  player deleted.

## 2. Slices (in order; each tested on a real server, committed, then the next)

| Slice | What | Estimate |
|---|---|---|
| A | The lobby's chat: the big chat window and the compact bar usable over the lobby; OOC with tg's `ooc_allowed` timing and `OOC_DURING_ROUND`; the IC channels greyed in the lobby (tg's reach no one); dead chat shown to the lobby after the round's end | 4-6 h |
| B | Observe and dead chat: the lobby's Observe with tg's confirmation, the observer at Meta's landmark with the ghost's looks, the lobby music stopped; dead chat (`say_dead`, `deadchat_broadcast`, the death and arrival notices, "has observed") with its tab; DV182 retired | 8-11 h |
| C | Orbit, teleport, jump, follow and a player's eyes: SSpoints_of_interest and tg's orbit menu, the orbiter (the camera circles or follows its target), Teleport to an area, Jump to a mob, the ghost HUD's buttons, Observe a mob (its first-person view and its HUD) | 14-20 h |
| H | Changelog: tg's Changelog window filled from `CHANGELOG.md` | 3-4 h |
| G | Settings, the Esc menu, the window rule, bug reports and admin help (D282, section 7; rescoped from tg's Game Preferences page) | 35-53 h |
| D | The character's looks: SSaccessories, hair and facial hair with colours, eye colour, underwear, undershirt and socks with colours, backpack, jumpsuit style; tg's Character page; drawn on the body in game (section 8) | 15-24 h |
| E | The preview: the character in its job's outfit in the menu, in 2D, turned left and right with on-screen arrows (D284, section 8) | 5-7 h |
| I | The closing: the demo check, the owner's checklist, the re-tag, docs, release 1.1.0-pre-alpha | 3-4 h |

Total 86-126 h (G grown by D282). Not now (the owner): the Quirks, Loadout, Antagonists and Personality tabs; the polls.

## 3. Slice A: the lobby's chat (2026-10-27, done; no protocol change, DV198)

- **The client**: the chat lines, the say window and the chat window are drawn over the lobby screen while it is open (`TguiHost.Adopt`:
  between the lobby and the windows, and given back to the HUD's layer after it) and offer only OOC and Pray there (DV198).
- **OOC**: was already translated (`ooc.dm`) with tg's `OOC_DURING_ROUND` set by the host (`TgConfigText`); the admin's line
  (`ooc.dm:104-116`: `adminooc`, `adminobserverooc`, the fake key) is translated now, with tg's `ooc_color` preference
  (`preferences/ooc.dm`) and `is_admin` (`__HELPERS/admin.dm`). The host is an admin, so its OOC lines had been dropped.
- **Pray**: the prayer's admin side (`pray.dm:19-46`): the prayer types without the antagonists', `COMSIG_GLOB_SEND_PRAYER`, the boxed
  message to the admins with `CHAT_PRAYER` (`_globalvars/religion.dm`'s colours, crosses and boxes); `key_name_admin` stands for
  `ADMIN_FULLMONTY`'s links as in `message_centcom`; the requests manager and the chat icon are not translated.
- **A fix found on the way**: the server made every client with the address 127.0.0.1 (`ClientSeam.Connect`'s default), so with the
  lobby (`ENABLE_LOCALHOST_RANK`) every remote player held the host's `!localhost!` admin rank. The session's address is passed now; an
  in-process loopback client keeps 127.0.0.1.
- **Dead chat** for the lobby after the round's end belongs to dead chat itself (slice B).
- Test: `LobbyChatGameplayTests` (the host and a remote player in the pregame: OOC both ways, the admin's line, Say from the lobby
  reaching no one, Pray reaching the admin, OOC after the round start); `SayModelTests.InTheLobbyTheWindowOffersOocAndPray`.

## 4. Slice B: observing and dead chat (2026-10-27, done; no protocol change, DV182 retired, DV199)

- **Observe** (`new_player.dm:72-112`): the lobby's Observe is on (`canObserve` as tg's: always in the pregame, past the startup in the
  init); tg's question is tgui's alert (DV199); the observer starts at Meta's `/obj/effect/landmark/observer_start` (the arrivals'
  "Space Station 13" sign), named by the preferences, the lobby music stopped, "has observed" in dead chat. `set_ghost_appearance`
  without the hair preferences (slice D).
- **Dead chat**: `say_dead` (`mob_say.dm:112-162`) and `deadchat_broadcast` (`__HELPERS/mobs.dm:255-310`) with its ghost runechat;
  `TURF_LINK` and `FOLLOW_OR_TURF_LINK` (the links work with slice C's follow and teleport). Its callers: the death notice
  (`death.dm:241`), the arrival (`game.dm:244`), the reconnect and disconnect notices (`client_procs.dm:515`, `:621`, with tg's
  `broadcast_login_logout` preference), the communications console's five lines; the cross-station message (`:508`) waits for its proc.
- **The client**: the chat window's Dead tab (tg's `.deadsay`, `.ghostalert`); tg's alert window wraps its message (it ran out of the
  window with a scroll bar).
- Test: `ObserverGameplayTests` (Alice observes from the lobby; her dead chat is read by neither the living Bob nor the lobby's Carol; Bob's
  death notice; Bob, dead, reads dead chat; after the round's end the lobby reads it); `ChatLogTests` (the Dead page).
- Seen on the way, not changed: MetaStation's boot with the shipped seed 1 raises one more not-translated runtime than seeds 10 and 11
  (`_organ.dm:102`, an organ's `setup_bodypart_overlay`, the visual organs, M10+).

## 5. Slice C: orbit, teleport, jump, follow and a player's eyes (2026-10-27, done; protocol 51, DV201, DV202)

- **Points of interest** (`controllers/subsystem/points_of_interest.dm`, whole): SSpoints_of_interest with tg's sorted mob and other lists
  (`BINARY_INSERT_PROC_COMPARE` by `compare_to`: the mob type priority, then the name), `get_mob_pois`, `get_other_pois`, `is_valid_poi`,
  `get_poi_atom_by_ref`, the nuke disk and Nar'Sie helpers, the POI datums (`validate`: not in nullspace, no stealthmin, no new player)
  and `/datum/element/point_of_interest`. Its callers that the port has: the ghost (`observer.dm:150`), every living mob
  (`living.dm:19`), the anomalies, the singularity and the supermatter; the others (the nuke and its disk, mechs, eye mobs, the
  immovable rod, his grace, the eightball, cult runes, the blob core, mob spawners, CTF, the deadchat control component, the horrific
  necktie, the infiltrator shuttle) are in files the port has not translated and call it when they come. `get_all_orbiters`
  (`atom_orbit.dm`) counts the orbiters.
- **The orbit menu** (`observer/orbit.dm`, whole; `tgui/states/observer.dm`): `GLOB.orbit_menu` with tg's sections (alive, antagonists,
  critical, deadchat controlled, dead, ghosts, misc, npcs), the living data (health, the ID's job and sechud icon, the mind's job and
  trim icon), the misc data (the supermatter's integrity, the nuke's timer, the disk's holder, the singularity's energy) and the "orbit"
  and "refresh" acts. The client's `Orbit` interface translates tgui's `Orbit/*.tsx` (the search with Enter orbiting the least orbited
  match, the colour schemes, auto-observe, real names, the settings blade with the orbited one's info; the sechud icon cropped to its
  pixels as tg's CSS crops it). The kit gained a clickable notice, a coloured collapsible header and a cropped picture.
- **Orbiting** was translated with the energy ball (D169, `orbiter.dm`): the ghost's `ManualFollow` (the orbit size from the icon, the
  ghost-orbit shape), its `orbit` and `stop_orbit` overrides and `on_deorbit` are added. The ghost circles its target with tg's
  `SpinAnimation` (protocol 32's chains) and follows the target's turf; the ghost's camera keeps D160's view around the ghost. Moving
  ends the orbit (`orbiter_move_react`), and with it an auto-observe (`stop_orbit`'s `reset_perspective`).
- **The verbs** (bodies and verb procs as `reenter_corpse`'s): Teleport (`dead_tele`: the sorted areas without `HIDDEN_AREA` in
  `tgui_input_list`, a random turf of the area), Orbit (`follow`), Jump to Mob (`jumptomob`) and Observe (`observe`). Ways in, as tg's:
  the ghost HUD's **Orbit** and **Teleport** buttons (`hud/ghost.dm:8`, `:11`, `screen_objects/ghost.dm:19-24`, `:45-50`, tg's
  `screen_ghost.dmi` states); the chat links (the observer's `Topic`: `follow`, `x/y/z`, `reenter`, `view`, and `observer_view`); the
  ghost's **double click** (`_onclick/observer.dm`'s `DblClickOn`: follow a movable, jump to a turf). **Jump to Mob and the Observe
  verb have no way in**: tg reaches them only from the verb panel and the command bar, which the port does not have (rules 7.10); their
  bodies are translated for the verb layer. Observing is reached through the orbit menu's auto-observe, as in tg.
- **Observing through a player's eyes** (`do_observe`, `cleanup_observe`, `on_observing_z_changed`): `client.eye` is the mob, the ghost
  joins its `observers` and is shown its HUD (`show_hud(version, viewmob)`; the ghost HUD's own `show_hud` steps aside while observing).
  Protocol 51 sends the eye in the private block; the client's camera is then the mob's first-person view (DV201) and the HUD drawn is
  the mob's as the server sends it (the replicator reads the HUD groups from the observed mob). Leaving is tg's: moving (the orbit ends),
  `reset_perspective`, the ghost's logout, the observed mob's deletion.
- **The double click** reaches the server as protocol 51's `DblClick` action after the second click (DV202); BYOND's `/atom/DblClick` and
  the mob's `DblClickOn` were translated in M4a.
- **Fixed on the way**: tg's chat links without quotes (`FOLLOW_LINK`, `TURF_LINK`, `FOLLOW_OR_TURF_LINK`, which dead chat uses) were
  neither kept by the host nor offered by the client; both take an unquoted href now (protocol.md section 56). Section titles take tgui's
  named colours.
- **Left out**: a ghost's single click (`ClickOn` with tg's `attack_ghost`: examine, a machine's window to observers, the scans; its
  live-partial stub reports once), so the first click of a double click reports it too; `jump_to_interact`'s `attack_ghost` (the
  minigames' "play" links); the ghost HUD's spawners, DNR, settings and minigames buttons and the HUD boxes; the deadchat control
  component; admin stealth (`fakekey`) beyond the checks translated.
- Tests: `ObserverOrbitGameplayTests` (MetaStation with the lobby: Alice observes, Bob and Carol play; the Orbit button's menu lists Bob;
  Alice orbits Bob, follows Bob's walk and stops when moving; a double click jumps to a turf and follows Bob; Teleport's list moves
  Alice into the area picked; auto-observe makes Bob Alice's eye with Bob's HUD and moving gives Alice's own back; Carol's death
  notice's (F) link follows the body), `Client/Tgui/OrbitWindowTests`, `Client/Play/ObserverViewTests`, `Protocol/ProtocolV51Tests`.

## 6. Slice H: the changelog (2026-10-27, done; no protocol change, DV200)

- `code/datums/changelog/changelog.dm` (the whole file), the client's `changelog` verb (`interface/interface.dm:95`), `GLOB.changelog_tgui`,
  `SetupLogs`' `changelog_hash` and client/New's unread notice with `aggressive_changelog` (`client_procs.dm:576`), and the lobby's button.
  The source is `CHANGELOG.md`, which the host mounts into the world's files (`ServerHost.MountChangelog`, found up from the working
  directory as the model catalog is); its versions stand for tg's months (DV200).
- The client: tg's `Changelog.tsx` (`Shared/Tgui/Interfaces/Changelog.cs`): the version dropdown with its arrows above and below, the
  version's changes with tg's icons, the header and the licence footer (DV200). The kit's tables keep a column of a pixel width (an HTML
  table's), which the icons need.
- Found by the quick run: the join's unread notice shifted a lossy test link's packets, and `SpaceDriftControlTests`' catwalk landing
  snapped once. The cause was older: the first jetpack push from rest in open space was not predicted (DV177 left its drift to the
  server; protocol 50's speed from rest covers a step off a floor only), so its correction was the drift of every frame in flight (1.04
  tiles there). The owner chose to predict it: protocol 51's push speed (protocol.md section 56).
- Tests: `ChangelogGameplayTests` (the unread notice, the button, the versions, get_month, the remembered hash), `ChangelogWindowTests`
  (CHANGELOG.md's parsing, the window and its act).

## 7. Slice G: Settings, the Esc menu, the window rule, bug reports, admin help (plan, 2026-10-27, D282)

tg: `modules/escape_menu/escape_menu.dm` and `tgui/packages/tgui-escape-menu` (opened by the skin's Menu button, `interface/skin.dmf:175`;
Esc closes only the focused tgui window there), `modules/admin/verbs/adminhelp.dm` and `adminpm.dm` (tickets, admin PMs,
`send2adminchat_webhook`). The server game preferences translated first for this slice are set aside in a stash ("M10x slice G: tg game
preferences on the server"): the owner keeps the client's own options only.

**The window and input rule (D282).** In game Esc only opens and closes the menu (in the lobby it opens nothing, D283: the gear button opens Settings); the one exception is a focused text
input (the say bar, the chat input, a modal's field), where Esc drops the input. Tab (cursor mode) frees and captures the mouse; a window
opened by the player's own action (a machine, the PDA, a container, the chat window) frees it and captures it again when the last such
window closes, unless the player pressed Tab meanwhile. One owner of `Input.MouseMode`. With the cursor free the crosshair hides and a
hint "Cursor mode (Tab)" shows. Every in-game window has a close button, a drag bar and a resize grip, and keeps its place and size across
restarts (the options file's `window.` and `window_size.` keys). Modals close with their Cancel or close button; Enter in a modal's field
still submits.

| Step | What | Estimate |
|---|---|---|
| G1 (done, 32d9136f) | The audit against the rule: every window (the tgui windows, the big chat window, the say bar (moved only), container windows, the scan readout, the analyzer panel, Settings, the dev console and dev menu), the one mouse owner, clicks reaching every window with the cursor free; a UI test opening each kind and clicking its close button, dragging and resizing it | 6-10 h |
| G2 (done, 00faec12) | The Settings window: tabs Controls, View, Graphics, Accessibility and Keybindings with the client's existing options; F10 and the lobby's Settings open it; replaces the options panel and the "not ported yet" notice | 3-4 h |
| G3 (done) | The Esc menu on the client in our own style with tg's font and sounds: the header, Resume, Character, Settings, Players (names and ping, no Ignore: the server prefs are set aside), Admin Help, Bug Report, Leave Body (Ghost; Suicide hidden, tg's suicide is not ported; greyed in the lobby), Quit (one confirmation; no Disconnect); Changelog the one resource | 3-4 h |
| G4 (done) | The Esc menu's server side: `escape_menu.dm` (the header's data, the players list, the actions), tg's `ghost` verb with its confirmation | 3-5 h |
| G5 (done) | Bug Report: a form (title, what happened, steps), the frame captured when the menu opened (attach checkbox, thumbnail) or a file from disk, the client version, OS, GPU, preset, round, map, area and tile filled in; a protocol message (WebP, about 2 MB at most), the server's limits (text caps, one report per 2 minutes, an image that does not decode dropped); saved to `data/bug_reports/<date>/<round>-<n>/` and posted to the config's Discord webhook with the screenshot | 6-9 h |
| G6 (done) | Admin Help: tg's tickets (open, reply, resolve, reject, IC issue, close), the player's ahelp box and ticket view (the menu's button blinks on an admin's reply), the admin's ticket list and ticket windows (tg's `browse()` pages drawn as client windows), admin PMs both ways with tg's sounds, the relay to the webhook when no admin is on | 12-18 h |
| G7 (done, with G6: dd6c277f, Release 1.0.6-pre-alpha 68382799) | A gameplay test on a real server, the docs, DV entries, CHANGELOG, the release for the protocol change | 2-3 h |

**G1 and G2 as built (2026-10-27).** `MouseOwner` (engine-free, tested) owns cursor mode and `GameScene.SyncMouse` is the one place
`Input.MouseMode` is set (the overlays: the Esc menu, Settings, the dev console, the dev menu). A tgui window or an open storage turns
cursor mode on (`WindowsOpen`); the chat window only while typed in (the owner, 2026-10-26); the scan readout does not (it comes from
scanning, mid-play). `WindowChrome` is the title bar, close button, grip and saved place of the readout, the dev console and Settings; the
storage and inventory windows drag by their title strip and scale by their corner (`window_scale.NAME`); ImGui keeps `imgui.ini`.
The modals' Esc cancel and `TguiWindows.Escape` are gone (DV203). The Esc menu so far is Resume, Settings and Quit (`Hud/EscMenu.cs`).

**G3 and G4 plan.** tg's escape menu is a raw `tgui_window("escape_menu")` with `init` and `state` messages and `action` replies,
the lobby menu's shape: the server side follows `modules/lobby_menu` (a reserved window index, `Limits.EscapeMenuUiWindow` =
`MaxUiWindows - 1`, interface `"EscapeMenu"`; `Replicator.Tgui` takes its messages as the lobby's, `ServerHost.ApplyUi` turns the
client's acts into `action` messages). An old client would draw that window as an unknown tgui window, so this bumps the protocol to 52
(fetch and compare origin first); G5 adds to 52 before the release. Translate `escape_menu.dm` (not the suicide icon: tg's suicide is not
ported; the header's time dilation: `SStime_track` is not translated, its perf log; `reset_held_keys_verb`) and `subsystem.dm`, call
`initialize_escape_menu` from client/New (client_procs.cs:293), and tg's `ghost` verb (`observer.dm:307-326`). The client's menu takes
its data from that window (TguiHost skips drawing it, as the lobby), plays `sound/misc/escape_menu/esc_open|middle|close.ogg` (one-shots
at 0.6, the middle a loop at 0.8, tgui-escape-menu `audio.ts`), uses Pixellari and Grand9K (`MaptextRenderer.Shared.Fonts`), sends
`opened` and `closed`, and has the pages Home, Players (no Ignore), Leave Body (Ghost) and Quit; Admin Help comes with G6, Bug Report
with G5.


**G3 and G4 as built (2026-10-28, protocol 52, DV205).** `modules/escape_menu/escape_menu.cs` and `subsystem.cs` translate tg's
files (no suicide icon, no time dilation, no `reset_held_keys_verb`; the verb-change signals are not translated, so `canAdminHelp` is
FALSE until G6), `client/New` detaches `initialize_escape_menu`, and tg's `ghost` verb (`/mob/living` and `/mob/eye`) is in the same
file. The window is `Limits.EscapeMenuUiWindow` (199) with the interface `"EscapeMenu"`; `Replicator.TakePageMessage` and
`ServerHost.ApplyPageAct` serve it and the lobby alike. The client's `EscMenu` reads that window's data (TguiWindows skips it), plays
tg's three sounds and sends `opened` and `closed`; Settings is the client's (DV204). The menu follows the windows scale while open (the
owner, 2026-10-28: it was small and ignored the setting, because a container resets its child's scale), its base sizes about a quarter
larger. One more firing subsystem shifts every seeded run's random draws (master.dm's first-fire offset per subsystem), so the tests'
ranges that held one stream's outcome were widened to tg's random spread. `EscapeMenuGameplayTests` covers the init, the
2 s process, Ghost through tg's alert and the stop. A protocol change is released before the next commit (versioning.md), so G5's
bug report message takes protocol 53.

**G5 as built (2026-10-28, protocol 53, DV206, D283).** The Esc menu's Bug Report page (`EscMenu`: a title, what happened, the
steps, the frame read back when the menu opened or an image from disk, lossy WebP at 80% halved until it fits 2 MB; the form keeps its
draft and is not rebuilt by the menu's 2 s updates) sends `PlaySession.BugReport`, which goes in 4 KB parts on the State channel: the
real server takes no client message over 8 fragments, which the loopback tests do not model (found on the real network, protocol.md
section 58). The host checks the 2 minutes per key at the first part, fills in the key, round, map, mob, area and tile, saves
`report.txt` and `screenshot.webp` under `BUG_REPORTS` (config/server.txt: `data/bug_reports`) and posts both to
`BUG_REPORT_WEBHOOK_URL`. Not the client's round, map, area and tile as planned: the server's own are the ones to trust. The owner
(2026-10-28, D283): no Esc menu in the lobby; Change Log and Bug Report are buttons in its bottom right corner (Bug Report opens the
form alone, which closes when sent), the gear opens Settings. The menu keeps within the screen (its scale is capped to fit) and hugs
its page. The owner (2026-10-28): Settings, the bug report and the changelog
share the menu's look, `Hud/MenuStyle.cs` (a Godot theme with Pixellari and Grand9K falling back to the default font for the icons, and
a tgui palette for the changelog's window).

**G6 as built (2026-10-28, protocol 55, DV216-DV219).** tg's `adminhelp.dm` and `adminpm.dm` (`modules/admin/verbs/`): the ticket
manager, the ticket (open, the 2-minute verb cooldown, reply, reject, IC issue, close, resolve, reopen, re-title, the admin's and the
player's panels), the Adminhelp tgui window (`Interfaces/Adminhelp.cs`), View Latest Ticket and the tgui-less admin help, the admin PMs
both ways with tg's `adminhelp.ogg`, keywords_lookup without its admin panel links. The verbs tg removes and gives back are a
`client.verbs` list with tg's `add_verb` and `remove_verb` (`__HELPERS/verbs.cs`), so the Esc menu's `canAdminHelp` follows them (tg's
`on_verb_change`). The Esc menu's Admin Help page: Create Admin Ticket (disabled while the verb waits) and View Latest Ticket; Admin
Help and View Latest Ticket blink red while an admin's reply waits (tg's blink-red, DV218); tg's See Admin Notices, Pray and See Notes
are not there (their acts are not translated). tg's `browse()` pages are client windows (DV216, protocol.md 60: `ClientSeam.Browse`,
the replicator's reserved indices, `Interfaces/Browser.cs` drawing the page as the chat draws its lines) and `/datum/browser` is
translated for the player's panel. The links are tg's: `client/Topic`'s `priv_msg`, `player_ticket_panel`, `tguiless_adminhelp` and
the holder's `ahelp` and `ahelp_tickets` (`modules/admin/topic.cs`, with `HrefToken` and `CheckAdminHref`). The admins' tools are
console forms (D275): `tickets [active|closed|resolved]` opens tg's ticket list (R_ADMIN), `pm KEY [MESSAGE]` is tg's Admin PM. With no
admin on, the ticket goes to `ADMINHELP_WEBHOOK_URL` (tg's `REGULAR_ADMINHELP_WEBHOOK_URL`, `--adminhelp-webhook`) as tg's Discord
embed (`modules/discord/discord_embed.cs`) through `/datum/http_request` and the host's `HttpSeam` (DV219: the author's key). BYOND's
`input()` prompts are tgui's (DV217). Not translated: the stat panel's ticket lines, the blackbox, TGS and the cross-server comms,
window_flash, the stealth keys, the admin panels' links (the full monty, the player panel, notes), the ticket history and the urgent
webhook (no config key yet). `AdminHelpGameplayTests` runs the whole round trip and the webhook.

## 8. Slices D and E: the character's looks and the preview (plan, 2026-10-28, D281, D284)

**What tg does.** `SSaccessories` (`controllers/subsystem/sprite_accessories.dm`: PreInit `setup_lists` :56, `init_hair_gradients`
:105, `init_hair_masks` :116, `init_sprite_accessory_subtypes` :123; at PreInit, before `GLOB.preference_entries`) over
`datums/sprite_accessories/` (`hair.dm` 244 types with `getCachedIcon` :42 and the gradients :1096, `facial_hair.dm` 39, `clothing.dm`
135 with `make_appearance` :27) and the random pickers (`__HELPERS/mobs.dm:71-118`). The preferences: `species_features/basic.dm`
(eye colour; hair and facial hair styles, colours and gradients, `generate_icon_with_head_accessory`), `clothing.dm` (backpack, jumpsuit,
socks, undershirt, underwear, `generate_underwear_icon`) and `underwear_color.dm` (only the underwear has a colour); the colour buttons
are `set_color_preference` (`preferences.dm:257`) through `tgui_color_picker`. `apply_prefs_to` (:527) ends in `update_body`; the body
draws them in `human_update_icons.dm` (`update_body` :663, `get_underwear_overlays` :675, `update_hair` :724), the head's
`get_hair_overlays` and `get_gradient_overlay` (`head_hair_and_lips.dm:47-239`) and the eyes' `generate_body_overlay` (`_eyes.dm:305`).
The preview is `char_preview` (`preferences.dm:309`, :353-380; `preferences_setup.dm:95`: the dummy with the prefs in its highest
job's outfit), refreshed from four hooks, turned by `rotate` (:233). The page is `MainPage.tsx`: the 48 px choice grids of the
`features` and `clothing` categories with their colour buttons, two `PreferenceList`s, and `CharacterPreview` (a ByondUi map).

**What the port has.** The preference framework and `apply_prefs_to`; the outfit reads `backpack` and `jumpsuit_style`; the head's
setters (`head_hair_and_lips.cs:307-419`); the random colours; the 578 generated accessory types; the converted `human_face`,
`hair_gradients`, `hair_masks` and `underwear` icons; the icon recipes on both sides; the human dummy and `dress_up_as_job`; tgui's
Dropdown, ImageButton, ColorBox and ByondUi. Missing: SSaccessories, the accessory procs and pickers, the 13 preferences, the colour
picker, and the drawing points deferred so far (`head_hair_and_lips.cs:126,141,201,239`, `_eyes.cs:278`, `human_update_icons.cs:596-602`,
the ghost's hair `observer.cs:235-246`, `create_mob.cs:52`, `dummy.cs:162`). A mob is a billboard composed from its replicated
overlays, so hair and underwear draw in game without client work (the mask's `Shift` becomes a `Blend` at an offset).

| Step | What | Estimate |
|---|---|---|
| D1 | SSaccessories, `_sprite_accessory.dm`, `make_appearance`, the random pickers; unit tests of the lists | 3-4 h |
| D2 | The body: `getCachedIcon`, the hair, facial hair, gradient, underwear and eye overlays, the ghost's hair; a gameplay test of the replicated overlays | 4-5 h |
| D3 | The 13 preferences and `set_color_preference`; `apply_prefs_to` reaching the head, the eyes and the outfit; the savefile | 3-4 h |
| D4 | The Character page: tg's choice grids with their icons (built on the client from its icon copies, lazily), the colour buttons, the two preference lists; a gameplay test that a lobby choice is on the spawned body | 4-5 h |
| D5 | The colour picker: Godot's own in the menu's look (the owner, 2026-10-28; a DV entry; tg's ColorPickerModal later) | 1 h |
| E1 | The preview on the server: `char_preview`, `create_character_preview_view`, `render_new_preview_appearance`, the refresh hooks, `rotate` both ways (D284) | 2-3 h |
| E2 | The preview on the client: a map view with no turfs draws its screen objects as one 2D picture (nearest filtering), with the left and right arrows (D284) | 3-4 h |

**D3 as built (2026-10-28).** `preferences/species_features/basic.cs` (9 preferences), `clothing.cs` (5) and `underwear_color.cs`: 15
preferences, not 13 (the hair and facial hair gradients each have a choice and a colour). Not translated: `icon_for`,
`generate_icon_with_head_accessory` and `generate_underwear_icon` (the grids' pictures are the client's, D4). `set_color_preference`
waits in tg's `tgui_color_picker` (`tgui_input/color.cs`, the `ColorPickerModal` window; the client's window is D5). The savefile and
`apply_prefs_to` needed no change: they already walk every preference. Every player's character now gets tg's defaults (a random
natural hairstyle and beard by gender, random eye and hair colours, the heart boxers, a sports bra for a female). `LobbyGameplayTests`
sets the braid, its colour, briefs, a grey satchel and the eyes through the picker, and finds them on the spawned engineer.
The savefile reached the disk only later: the world's file seam is in memory, so the host mounts `PLAYER_SAVES` (config/server.txt:
`data/player_saves`, `--player-saves DIR`) at tg's `data/player_saves/` (`DmMountedFiles`); `PlayerSavesGameplayTests` loads a
character saved by the previous server.

**D4 as built (2026-10-28).** The server: each choiced preference with `should_generate_icons` (the seven looks and `ui_style`) sends
its choices' pictures in the constant data, the /icon recipe of tg's `icon_for` (`generate_icon_with_head_accessory`,
`generate_underwear_icon`, `get_display_icon_for` for the bags and jumpsuits, `universal_icon.cs`), with /icon `Crop` and `Scale`
added to the recipe (protocol.md 34.3, no version change); about 95 KB for 396 choices. `skin_tone` sends tg's display names and
colours (`GLOB.skin_tone_names`). The client: `PreferencesMenu.cs` draws MainPage's features column, ChoicedSelection and the two
PreferenceLists (DV44). `PreferencePictureTests` composes the pictures with the client's icon library; `LobbyGameplayTests` clicks the
hairstyle's button, the braid's picture and the eye colour's button. Not yet looked at in the real client (with E2).

**D5 as built (2026-10-28, DV209).** `InputModals.cs` ColorPickerModal: tg's window with Godot's ColorPicker (the kit's
`UiKind.ColorPicker`) and tg's InputButtons, in the Esc menu's look (`TguiHost`, as the changelog). The picked colour is kept as quiet
local state so the picker is not rebuilt while dragged. `LobbyGameplayTests` picks the eyes' colour in it and presses Submit.

**E as built (2026-10-28, DV210).** The server: `char_preview` (Initialize, Destroy, `update_body`, `create_body`) and
`create_character_preview_view` in `preferences.cs`, `get_highest_priority_job` and `render_new_preview_appearance` in
`preferences_setup.cs`, the dummy's `delete_equipment` (the wardrobe deletes), `has_equipped` and `wipe_state`; the four refresh hooks
(`update_preference`, `switch_to_slot`, the jobs' `set_job_preference`, `randomize_character`), tg's `rotate` and the loadout's
`rotate_dummy` both ways. With them the randomization middleware (`middleware/random.cs`: the `randomization` data, the `random`
constant data, `randomize_character`, `set_random_preference`). The client: `UiNode.MapPicture` makes a ByondUi map a `TguiMapPicture`
(the screen object whose screen_loc names the map, composed at its appearance's dir, nearest filtering); the HUD already skips a
screen_loc with a map id. `PreferencesMenu.cs`: CharacterControls' Rotate, the preview with its two arrows, RandomizationButton as the
kit's icon-only Dropdown (a MenuButton), getRandomization on the features and the lists, random_body's Randomize and Cancel,
random_species. `LobbyGameplayTests` turns the preview both ways and by Rotate, composes it with the client's compositor, sets the
hairstyle's dice to Do not randomize, randomizes the character, and sees the doctor's uniform on the preview after the Occupations
page's High. No protocol change: the screen object and its appearance's dir already replicate.

No protocol change is expected. Risks: every random human (NPCs, dummies, tests) gains hair and underwear, so seeded and visual tests
move (the Meta suite at the end); hair with a `y_offset` can be taller than 32 px (the billboard must take it); whether the lobby
player's screen objects replicate and the HUD skips a `screen_loc` with a map id is checked in E2.

## 9. Slice I: the closing (2026-10-28, done; no protocol change)

**The re-tag.** One leaf in the code still waited for M10x: `set_ghost_appearance`'s hair and facial hair preferences
(`observer.dm:837`), translated now that the looks are (`ObserverGameplayTests` checks the ghost's hairstyle and beard against the
player's preferences); not-translated.md's stale `update_icon` hair row (translated in D2) is removed. No site is tagged M10x.

**The full suites** (Core and Server, Meta included) ran with six Server failures, all from the random stream the new looks shift
(every human now rolls its hair, underwear and eye colour), none from M10x's own code:
- two Meta delamination tests reached two leaves of later milestones: a dimensional anomaly's glass theme gives its airlocks and tables
  tg's `can_shatter` element (`basemats.dm:73`), and the M4b scenario's decompressed room froze its water vapour (`freeze_turf`,
  `_open.dm:520`). Both are translated: `datums/elements/can_shatter.cs` (with BYOND's circle generator as OpenDream draws it),
  `datums/elements/frozen.cs`, the freon effect (`datums/status_effects/gas.cs`) and `freeze_turf`; `WetFloorTests` covers them.
- an airlock pump eaten during its `start_cycle` stoplag ends in a null `get_turf(src)`, a runtime in tg too: added to the delamination
  tests' list of tg's own runtimes.
- the inducer test's bound on the grid's own charge was exactly one APC cycle (50 kJ): now a tenth of the inducer's rate.
- the 3/7/14 blast test counted cells that change far south of the bridge in the same seconds: it counts the blast's square only.
- the singularity's random walk took it 14 tiles off the crystal in 15 s: the pull test counts the items within its pull of where it
  ended (1 of 1 in this seed, which is thin).
The six were rerun filtered and pass.

**The demo check** is the gameplay tests the checklist names, each on a real server; no separate scenario test was written.
**The owner's playtest list** is `docs/ai/C7-checklist.md`. Release: 1.1.0-pre-alpha.

## 10. Gaps SP1: the roundstart species, part 1 (2026-11-19, D338; no protocol version change)

**Selectable.** The server's config writes tg's `ROUNDSTART_RACES` for the ported species, the human, the felinid and the lizard (DV543);
`get_selectable_species` and the species preference read it. The species preference's constant data (tg's `species.dm`
`compile_constant_data`: name, description, lore, perks, diet, the enabled features) and the species middleware's spritesheet (each
species' dummy in the consistent assistant's clothes, flattened, scaled to 64 px and cropped as tg's) go with the window's static data
(DV44, `species_icons`). The client draws tg's `SpeciesPage.tsx` (the Species button beside Rotate; the gender buttons hide for a species
without sexes) and the features' names of `species_features.tsx` (`PreferencesMenu.cs`).

**Drawing (D338).** A mob is a billboard composed by the client from its replicated appearance (D104); there is no human 3D model yet,
so "the human 3D body" is the human's billboard and a species is drawn exactly as tg composes it: its limbs are tg's limb sprites of the
species (`limb_id`: the lizard's greyscale `icons/mob/human/species/lizard/bodyparts.dmi`, tinted by his mutant colour; the felinid's are
the human's), and its parts are tg's bodypart overlays (`datum_bodypart_overlay_mutant` and the body markings) on the limb that holds them,
each an overlay of tg's sprite state (`m_<feature>_<style>_<layer>`) on tg's layer (BEHIND, ADJ, FRONT) of the body, in tg's colour
(the mutant colour, the hair colour or a dye). The client draws an overlay at the billboard's direction, so a part faces as the mob faces,
behind the body from the front and over it from the back as the layers say; the wagging tail is tg's animated `wagging_` state. No
client code is species-specific: the server's appearance carries it all (the replication of the overlays is tested by
`SpeciesSP1GameplayTests`). The lizard's feature pictures are tg's side shots (`generate_lizard_side_shot`), whose `icon(file, state,
EAST)` became the recipe's direction pick (protocol.md, the /icon recipe). The 3D models a species would need instead are in
`docs/ai/required-models.md` ("Species parts").

