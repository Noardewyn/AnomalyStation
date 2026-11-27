# UI overhaul for first person (ROADMAP stage 11)

The interface is reworked to fit first-person play. The design has two source documents, both copied here from the
contributor who owns the design (the project's UI designer):

- `docs/ai/specs/ui-overhaul-reference.txt` (added 2026-11-26): the design specification. Its Appendix A is the
  designer's own raw notes; its sections 1 to 7 are the specification written up from them.
- `docs/ai/specs/ui-overhaul-wireframes.txt` (2026-11-23, agreed on the project's Discord as the contributor reports):
  example layouts for that specification.

The wireframes are rough examples, not a layout to copy (the designer, 2026-11-26: "for the wireframes, use your
judgement in design as these are very rudamentary, feel free to change the layout if you think you have something
better"). What each element is and does comes from the designer's notes and words; where it sits and how it looks
is ours to design well, shown to the designer in screenshots.

This file turns them into work packages. Read both source documents in full before starting a package. Order of
authority where they disagree: what the designer has said since (recorded in this file with its date) wins over
everything; then the designer's raw notes (the reference's Appendix A); then the reference's sections and the
wireframes for layout and behaviour; this file wins for keys (section 3). Section 0 says where the work stands;
section 7 maps the wireframes to packages and section 9 the reference.

## 0. Where the work stands (2026-11-26)

Branch `ui-overhaul`, local only, nothing pushed. Commits, oldest first:

| Commit | What |
|---|---|
| `cf06907` | This spec and the wireframes |
| `8d6d518` | Package 1: keys |
| `052116d` | Package 2, slice A: prompts beside the crosshair, ping and FPS |
| `e7d96bf` | Package 2, slice B: the health doll panel and the combat pill |
| `a67c16a` | Package T, slices T0 and T1: a person is hit on the body alone |
| `5d1449c` | Package T, slice T2: the leg in front, shots, effects pass clicks |
| `5a5c4a8` | Spec only |
| `f1a2ee1` | Package T, slice T4a: eyes, mouth and groin by look; tg's gun in the mouth (protocol 98 since the merge; built as "95") |
| `77f593f` | Package T, slice T4b: the prompt follows the part looked at (protocol 98 since the merge) |
| `3398a80` | Package T, slice T4c: a patient lies face up, "Turn over", the input-script step `radial=LABEL` (protocol 98 since the merge; signed off by the contributor 2026-11-26) |
| `d48b027` | Package T, slice T5a: looking down shows the own body (client only; section 8, "T5a as built") |
| `a98d5ee` | Package T, slices T5b and T5c: the own body is a target, the doll's eyes, mouth and grasp (client only; section 8, "T5b and T5c as built") |
| `6a3e649` | Package 2, slice C: the hands either side of the hotbar gap, the bottom slots in the gap, the held-item card (client only; section 6, "Slice C as built") |
| `e3af6b3` | Package 2, slice D: the vitals column at the right edge, alerts as chips, the hint strip over the hands, the swap key cap, the red cuffed hand (client only; section 6, "Slice D as built") |
| `b258604` | The prompt beside the crosshair only for a thing within reach (server and client, no wire change, DV601; section 9, one known fault accepted by the contributor) |
| `fc882c5` | Package 2, slice E: the vitals without a box, tg's combat toggle in the corner, the key-duplicating buttons hidden, keys for crafting, language and navigate, "Offer item" in the wheel (server and client, no wire change, DV139; section 6, "Slice E as built") |
| `52bcb78` | Package T, slice T5's numbers: the look-down pivot 0.25 m and the limit 135 degrees (client only; section 8, "T5's numbers changed") |
| `5a42a6d` | Merge of `origin/main` (release 1.14.0, upstream's protocol 95 to 97): this branch's wire changes are protocol 98, its new deviations DV599 (click through), DV600 (Turn over) and DV601 (the prompt only in reach); signed off by the contributor 2026-11-26 |
| `6449390` | Chip pictures fitted to their art (client only; section 11) |
| `b475196` | A hit on an alien aimed at a body part it lacks lands on its chest (server; DV602; found by the merge's stress test) |
| `f6a5677` | Code comments of this branch: what and why, not who asked or when (upstream's new rule) |
| `7e4f032` | Package 3: the belt hotbar, ten cells on the number keys, action buttons on Shift+digits (server and client, protocol 98, DV603; section 10; signed off by the contributor 2026-11-27) |
| `11e5c15` | The chat window above the bar, see-through at rest, input shown only while typing (client only; section 13) |

The branch follows `origin/main` (https://github.com/Noardewyn/AnomalyStation): fetch at the start of a session and
before any wire or server work, and merge upstream in before building on it; upstream's protocol and DV numbers win
and ours are renumbered after them. Upstream's `CLAUDE.md` now forbids code comments that say who asked for something
or when; builders write what the code does and why, with plain `DEVIATION(DVnnn)` and decision references.

Protocol version 98 (ours; built as "95" before the merge) is not released: slices T4a, T4b and T4c and the hotbar
ride one release by the owner (`tools/release.sh`); until then the build warns `ANOMVER01` and
`BuildInfoTests.TheVersionNamesTheProtocolItSpeaks` fails by design.

Next, in this order:
1. Package 4, the Tab inventory (section 12; the strip view's search mechanic is agreed by the lead developer): the
   remaining design points are confirmed with the designer before a builder starts; it changes the wire (the
   contents of every worn storage at once, a second container, the search), so the contributor signs off.
2. The actions menu (section 15), after the Tab inventory.
3. The devlog screenshots (`../devlog-screenshots/2026-11-26/`, outside the repository): a last retake with the
   finished HUD (`02`, `03`, `06` and `07` still show the old look; the chips, the bar and the chat have changed).
4. Packages 5 to 8 and what section 9 adds. The Tab overlay's character sprite is the main way to act on oneself;
   looking down is a secondary way and gets no more design work. Tg's zone selector stays under Tab. Anatomical
   focus (package 6) is held on the middle mouse button (the contributor, 2026-11-26), so the middle click's second
   binding for swapping hands goes then.

Known limits of what is committed, to remember: a PDA worn on the belt slot has no cell on the bar (section 14,
withdrawn); with both arms lost the bar is not drawn and the pockets cannot be clicked; dragging onto or out of a
pocket cell does nothing (tg's slots work by clicks; package 4 brings drags); the emotes menu stays open after a
send hides the chat's rows; the client crashes on a malformed `--keys` argument (seen in a stress test).

Seen in play and not yet looked into: the prompt not always matching where the crosshair is drawn, and "Headpat"
offered to a handcuffed player (section 9); a patient buckled to the operating table drawn as an upright sideways
picture over the table; the radial's title crossed through by the ring line; Shift+F4 pressed twice within two
seconds not bringing the HUD back.


To tell the owner (the contributor does this; `DECISIONS.md` is not edited by us): the protocol changes of version
98; that the lying-body rule of the play test of 2026-10-13 is narrowed for bodies buckled to an object (DV573) and
"Turn over" is new (DV600); tg's `handle_suicide` is translated; the earlier decisions revised for the interface
(section 1).

## 1. What was agreed, and what it changes

- The whole interface is reworked for first person: the live HUD, the Tab overlay, limb targeting, machine and
  handheld interfaces, grabs and hallucination hooks (wireframes, sections 2 to 6), with the palette of section 7.
- Interface components need not use tg's sprites. Items, clothing and other game content keep their tg pictures.
- Keys follow Space Station 14's defaults, exactly where SS14 has the same function (section 3).

This revises earlier decisions for the interface only. They are not edited here; the owner records the revision in
`DECISIONS.md`:

| Decision | What it said | What changes |
|---|---|---|
| D107, D126 | The HUD is tg's screen objects drawn with tg's sprites at their `screen_loc` | The server still computes and replicates tg's screen objects, and clicks still go to them. The client places and draws them itself. |
| D131 | Storage and inventory are native windows over tg's screen entries | Same data; they dock into the Tab overlay's columns instead of floating. |
| D148 | Each tgui window is a literal translation | Existing windows stay. A schema-driven builder is added beside them for machines that do not need a custom window. |
| D247, D250, D259 | The chat bar and chat window are tg's, ported as they are | New layout, palette, badges and keyword pills over the same chat data. |
| D282, D290 | Windows float, drag, resize and open centred | The Tab overlay is three fixed columns. Machine windows still float. |

Rules that stay: the server is the authority and runs tg's code (D40, D86); colour is never the only carrier of medical
information (D70: the doll keeps its hatching); every key can be rebound (D53); player-visible text goes through
`I18n.T`.

## 2. How the work is done

- Logic goes in `godot/scripts/Shared/` (no Godot types), so `tests/Anomaly.Server.Tests` can test it. Godot nodes in
  `godot/scripts/Hud/` only draw and forward input.
- Colours, sizes and the zone shares come from one place, `Shared/Play/UiTokens.cs` (added by package 2), not from
  literals in the widgets.
- A package that needs data the client does not have adds a typed message and a server check for it (D40), and a
  gameplay test that drives a real server.
- Each package: tests for its `Shared` logic, a line under `## [Unreleased]` in `CHANGELOG.md`, updated
  `docs/running.md` when controls change, and before/after screenshots from `godot/tools/interaction-shots.sh` or
  `--fake-server --screenshot-after`.
- A change to gameplay logic taken from tg (not to how it is shown) gets a row in `DEVIATIONS.md`.
- Unit tests do not close a package. Before it is committed, each package is playtested in the running client
  against a real server (`--realtime`; the fake server only for what needs no server logic): every changed behaviour
  is driven through the real input path (scripted keys, `docs/ai/specs/apps.md`, or by hand) and checked on
  screenshots and in the server's state. The steps, what was seen and the screenshot paths are reported; what could
  not be driven is named.

## 3. Keys

Rule: an action that SS14 also has gets SS14's default key, taken from `Resources/keybinds.yml` of
space-wizards/space-station-14. An action SS14 does not have keeps its key unless SS14 uses that key for something this
game also has. A key the wireframes name for a new feature (Tab, 1 to 9, Alt) is used when SS14 does not claim it for
an existing function. Saved options files keep the keys their player had.

| Action | Was | SS14 function | Default now |
|---|---|---|---|
| Move | W A S D, arrows | MoveUp, MoveLeft, MoveDown, MoveRight | unchanged |
| Walk instead of run | C (toggle) | Walk: hold Shift | Shift, as a toggle: a tap switches walking and running (SS14 holds it; see section 5) |
| Lie down, get up | U | ToggleKnockdown: C | C |
| Combat mode | F | none (an action button) | F (the wireframes' C is SS14's lie down) |
| Left, right click | mouse | Use, UseSecondary | unchanged |
| Drop | Q | Drop: Q | unchanged |
| Throw | R | ThrowItemInHand: Ctrl+Q | Ctrl+Q |
| Swap hands | X, middle click | SwapHands: X | X (middle click stays as a second binding) |
| Use held item | Z | ActivateItemInHand: Z | unchanged |
| Examine | V, Shift+left click | ExamineEntity: Shift+left click | Shift+left click only |
| Pull | Ctrl+left click | TryPullObject: Ctrl+left click | Ctrl+left click only |
| Stop pulling | Delete (H was the pull key) | ReleasePulledObject: H | H and Delete, as in tg. A pull is not held in a hand and Drop does not end it (decided 2026-11-23) |
| Say | T | FocusChatInputWindow: T | unchanged |
| Radio | Y | FocusRadioWindow: ; | ; |
| Me (emote text) | M | FocusEmote: Shift+' | Shift+' |
| OOC | O | FocusOOCWindow: [ | [ |
| Emote wheel | J | OpenEmotesMenu: Y | Y |
| Inventory | I | OpenInventoryMenu: I | unchanged |
| Close all windows | Shift+Esc | WindowCloseAll: Shift+Esc | unchanged |
| Action buttons 1 to 9 | 1 to 9 | Hotbar1 to Hotbar9 | unchanged until package 3 |
| Console | ` | ShowDebugConsole: ~ | unchanged (the same key) |
| Debug HUD | F4 | ShowDebugMonitors: F3 | F3 |
| Hide interface | F6 | HideUI: Shift+F4 | Shift+F4 |
| Fullscreen | F12 | ToggleFullscreen: F11 | F11 |
| Names | F3 | none | F4 |
| Pet commands (held) | Shift | none; Shift is Walk | J |
| Cursor mode / Tab overlay | Tab | none in play | unchanged |
| Quick equip | E | (E is ActivateItemInWorld) | unchanged until package 4 adds world interact |
| Up a level, down a level | Space, Ctrl | none; Ctrl is the modifier of pull and throw | unchanged keys (DV422). Ctrl alone acts when it is let go with nothing else pressed meanwhile, so Ctrl+Q and Ctrl+left click do not also go down a level |
| Resist, pray, give, push to talk, flashlight, scoreboard, the other function keys | B, P, G, N, L, K, F1 F2 F5 F9 F10, \ | none | unchanged |

Later packages: 1 to 9 become the belt hotbar and the action buttons move to Shift+1 to 9 (package 3); E becomes world
interact, with quick equip on Shift+E (belt) and Shift+B (backpack), and V opens the backpack (package 4); Alt held is
anatomical focus (package 6).

## 4. Packages, in order

1. **Keys** (section 3). Client only.
2. **Live HUD** (wireframes section 2, without the hotbar): `UiTokens`, native hands, the health doll (injuries only,
   see below), combat toggle, alert chips at the top left, ping and FPS at the top right, context prompts with key chips beside the
   crosshair, the held-item card. Client only: it re-places and re-draws the screen entries the server already sends.
3. **Belt hotbar**: keys 1 to 6 for the worn belt's contents, 7 to 9 for the pockets and the ID slot; a press takes the
   item into the active hand and a second press puts it back. Needs a typed message and server checks.
4. **Tab overlay** (wireframes section 3): three columns, chat at the left, doll and gear in the middle, own storage
   over the external container or strip target at the right, the nearby-items strip, Shift+left click transfer, strip
   timers inside the slots, world interact on E.
5. **Chat** (wireframes sections 2 and 7): the palette and badge shapes, keyword pills with a tick sound, direction
   pills for speakers out of view, department tabs.
6. **Anatomical focus** (wireframes section 4): while Alt is held, the mob under the crosshair shows where its head,
   arms, legs and body are on its sprite, so the player knows where to aim, above all on non-humanoids. It locks
   nothing (see "Targeting" below).

**Targeting** (the contributor who owns the design, 2026-11-24, correcting the wireframes' `TARGET: HEAD` and "release
to lock"): the zone a player targets is where they look on the other mob's sprite, as the game already works. The
health doll is not used for targeting: it only shows where the player is injured. Anatomical focus only shows where
the parts are; it does not select or lock a zone. tg's zone selector at the bottom right is hidden in the
first-person view for the same reason. It still shows while the cursor is free (Tab), because looking can only pick
six zones today (head, chest, arms, legs: the click carries one of `AimZone`'s values, set as tg's `zone_selected`
by the server before tg's click runs, DV30): eyes, mouth and groin, which tg's surgeries need, can only be chosen on
the selector, and a ctrl-click on the doll grasps the zone it holds (DV72). Once looking can pick those three (finer
regions on the sprite, which needs a wider `AimZone` in the protocol), the selector goes entirely.

Aiming by look is to be reworked to be more accurate (the contributor, 2026-11-24, with the lead developer's
agreement as the contributor reports it from the project's Discord: a rework is fine as long as nothing downstream
breaks). A part that cannot be seen from where the player stands need not be targetable. It is its own package,
built after slice B of the live HUD; its design section is added when the investigation is in.

Open points on aiming by look, seen in the playtest of 2026-11-24 against a real server and not yet looked into
(none is caused by the HUD work; they matter more now that the selector is out of sight):

- From the side and the front a head or a leg aimed at is the part hit (tg still rolls its own misses). From behind,
  the crosshair on an arm gave "chest" four times out of four, and on the screen-left leg gave "right leg" twice out
  of four: which part wins where two overlap, and left and right in the back view, need checking in
  `BodyZoneAim` and its server mirror for shots.
- The prompt beside the crosshair comes from the server's answer to the hover, which uses the zone selected before
  the look: looking at a head it read "Hug" while the click patted the head. The hover should carry the zone looked
  at (a server change).
- The prompts refresh only when the target changes, so they can be stale after the held item or combat mode
  changes.
7. **Handheld screens and schema-driven machine panels** (wireframes section 5).
8. **Grabs and hallucination hooks** (wireframes section 6).

The "Targeting" paragraphs above are as written on 2026-11-24 before the rework; section 8 (package T) is what was
built and decided since: looking now picks nine zones, the prompt follows the look, and the zone selector stays under
Tab.

Added from the reference document (2026-11-26; section 9 has every element):

- Package 4's middle column is the character's own sprite, not an abstract doll: an item used on it is used on
  oneself at the body part clicked on the sprite ("grab syringe on cursor and click on chest", the designer's raw
  notes), with the worn things laid out around it where they sit on the body. It uses the same zone map as aiming by
  look (`BodyZoneMap`), so the head, the eyes and the mouth can be reached there too.
- 9. **Damage screen effects**: a directional blood vignette for brute, a heat-wave border for burn, a frost creep
  for oxygen loss, muffled sound below 20% health. Not in the wireframes.
- 10. **Aiming down sights**: with an aimable firearm in hand the right button held aims down the sights, and racking
  the slide or chambering moves to another key; with anything else in hand the right button stays tg's secondary use
  or shove. Not in the wireframes; the designer's note is a question ("ads with guns?"), so it is confirmed with the
  designer before it is built.

## 5. Package 1: keys

Change `KeyBindings.Defaults()` (`godot/scripts/Shared/Play/PlayOptions.cs`) to the "Default now" column, and whatever
the new defaults need to work:

- Key names with a modifier: `Shift_KEY` exists. `Ctrl_KEY` and `Alt_KEY` are added the same way where the table needs
  them (Ctrl+Q), including mouse buttons if pull and examine are to become rebindable bindings. A modified press that
  has no binding of its own still falls back to the plain key, as Shift does today.
- Walk stays tg's toggle, on SS14's key: a tap of Shift switches between walking and running, through the toggle the
  server already has. Nothing changes on the server or in the protocol. Shift alone is a press-only action on a lone
  modifier (see "A press does one thing" below), so Shift+left click examines without switching.
  Decided 2026-11-23, after two other designs were built and dropped: a held key done with toggles on the client
  (the server drops toggles over its rate limit, which left the mob walking with no key held) and a held key done
  with a new server message (reliable, but it changes the wire protocol, which needs the owner's agreement and a
  release). Hold to walk can come back with the first package that changes the protocol anyway (package 3).
- Throw on Ctrl+Q does what R does today (it throws at once, D328).
- The radio, OOC and me keys select their channel as well as focusing the input, whether the chat window is open or
  closed (playtest 2026-11-23: with the window open they only focused it and it stayed on Say).
- The keys the input script cannot press yet (`;`, `[`, `'`, F11) are added to it, so a playtest needs no
  operating-system key events.
- A press does one thing. An action bound to Ctrl or Shift alone that acts on the press only (down a level on Ctrl,
  walk/run on Shift) is sent when that key is let go, and only if no other key or mouse button was pressed while it
  was held. Held actions (pet commands, push to talk) still start on the press.
  It is not sent while a text field has the keyboard (the chat input, a say window, a machine window's field): a
  lone Shift or Ctrl tapped while typing does nothing in the game (playtest 2026-11-23: it switched walk/run, and a
  ghost went down a level). A mouse press that another part of the interface takes first (the dev menu) cancels it
  as well.
  Exactly what "alone" means (probes and playtest 2026-11-23): the key is alone when no other modifier key (Ctrl,
  Shift, Alt, Meta) is down as it goes down, so Alt+Shift and Ctrl+Shift never switch walk/run; a second key of the
  same name going down while it waits (the other Shift key) cancels it like any other key; only a key or mouse
  button that goes down during the hold cancels it, so a mouse button or a movement key already held (W, a drag,
  holding fire) does not.
  The options panel follows the same rule when it captures a key: a modifier binds itself on its release only if
  nothing else, a refused mouse press included, was pressed while it was held. Any mouse button and a wheel notch
  count as pressed there, as they do in the game.
  In the free camera the rule sends nothing: Shift is the camera's fly-faster key and the mob stands still.
  A wait whose key is no longer down (its release went to another part of the interface, or the window lost focus)
  is dropped, in the game and in the panel, so the next tap of that key is not lost.
  Known limits, accepted (2026-11-23): the rule knows a key by its name, so with one Shift key held a tap of the other
  Shift key can count as alone, and the same for the two Ctrl keys; a key press the dev menu takes for itself (Esc
  closing one of its lists) does not cancel a wait; nor does a dev-menu click that starts and ends within one frame,
  or a wheel notch that a scrolling list takes (in the options panel, Shift held, the list scrolled, Shift let go
  binds Shift).
  Also left as they are at the end of this package: a Ctrl or Shift pair bound to a held or movement action sends
  its press but not its release (Shift pairs did the same before); Fullscreen bound to Ctrl or Shift alone acts on
  the press.
- No two default actions share a key. A test asserts it for the whole table, and asserts each row of section 3.
- `godot/default-options.cfg`, the controls table of `docs/running.md`, any on-screen key hint or help text that names
  a changed key, and the tests that name the old keys are updated.
- A saved options file with the old keys loads unchanged. An action the file has no line for (the file is older than
  the action, or older than its binding) gets its new default, unless the file gives that key to another action. So a
  version 4 file, written when pull was Ctrl+left click and H and Delete stopped pulling, loads with exactly those.
- The options panel does not accept a binding that does nothing or half works (playtests 2026-11-23):
  - a plain mouse button is taken only for the three click actions (primary, secondary, middle click);
  - Examine and Pull take the left button with Shift, Ctrl or both, and nothing else from the mouse, so they can
    never take a button away from a click action;
  - any other action ignores a mouse press while it waits for a key;
  - whatever modifier is part of a Pull or Examine binding is not passed on as a click modifier: Pull bound to
    Shift+left click sends tg's Ctrl-click and pulls. Only a modifier held on top of the binding is passed on (Ctrl+left
    click with Shift also held is tg's Ctrl-Shift-click);
  - a Ctrl pair the game itself takes before the bindings is refused: the window scale keys, Ctrl with `=`, `-` or
    `0` and their keypad forms, with or without Shift.
  The titles of the actions in the panel do not name a key.

Not in this package: new actions (world interact, open backpack, belt hotbar), and any HUD change.

Tests: `tests/Anomaly.Server.Tests`, filter `FullyQualifiedName~PlayOptions` plus whichever classes cover the input
handling touched, with `&Category!=Meta`.

Playtest (section 2), in the running client against a real server, with screenshots: a tap of Shift switches tg's
walk/run button to walk and a second tap back to run, Shift+left click examines and does not switch it, Ctrl+left
click pulls, H and Delete each stop the pull, Ctrl+Q throws the held item at the crosshair without also
dropping it and Q alone drops it,
Ctrl tapped alone as a ghost goes down a level and Ctrl+Q as a ghost does not, C lies down, F toggles combat mode,
Y opens the emote wheel, `;` `[` and Shift+' open their chat inputs on the radio, OOC and me channels with the chat
window open and with it closed, F3 F4 Shift+F4 and F11 do what the table says,
J shows pet commands, a key rebound in the options panel (a Ctrl pair) works, and an options file with the old keys
still plays with the old keys.

## 6. Package 2: live HUD

Client only. Planned 2026-11-23 from a read of the HUD code; starts after package 1 is committed (both edit
`GameScene.cs`, `GameScene.Interaction.cs` and `GameScene.Dev.cs`, and the key chips use package 1's binding names).

### What the server does not send, so it is not in this package

- The names of held items (the captions of the hand boxes, the card's title, "(Crowbar)" in a prompt) and item traits.
- Alert names as text and the numbers of the mockup's "GRAV: 1.0G" and "293K" chips.

Package 3 adds a typed message for the belt hotbar anyway; it also carries the names of the items in the player's own
hands and slots, and the card and hand boxes show them from then on. Until then: the hand boxes show the item's
picture, the card shows the picture, the hand and the key chips, and an alert chip shows tg's alert icon with its name
as the tooltip.

Also left to their packages: "[E] Open / Strip" (package 4), "[ALT] V.A.T.S." (package 6). "[F1] Show Traits" is
dropped (F1 is the camera mode and there is no traits data). The wireframes' "[C] COMBAT" shows the bound key (F).

### How it is built

- A click still goes to tg's screen object: `HudPicker` hit-tests the rectangles `HudLayer.Collect` reports, so a
  re-placed entry is clicked where it is drawn. A native widget (hand box, combat pill) reports its rectangle under
  the entry's id. `zone_sel`, swap, drop, throw, resist, pull and the move intent button stay at tg's places.
- Godot-free logic in `Shared/Play/`: `UiTokens` (sizes in reference pixels at 1080 lines, colours from wireframes
  section 7, the Tab overlay's shares 0.28 and 0.64), `KeyChips` (a binding name as a chip label; the prompt lines of
  a `ContextHintsMessage` from the player's bindings: at most three, plus the stop-pulling line when the server sends
  one, so it is never the line that is cut; a click line shows the mouse button bound to that click, since the game
  acts on a click only from the mouse; the block sits past whichever sight is drawn, the gun sight included, and in
  cursor mode beside the pointer; the text has a dark outline so it reads over bright scenery), `LiveHud` (which widget a screen entry is,
  by hand index or icon state and never by position; the layout of every box from `HudMetrics`). Nodes in `Hud/`: a new `LiveHudView` drawn under tg's pictures inside
  `HudLayer`'s root, and changes to `HudLayer`, `ChatLines` and `StatsCorner`.
- Layout: a gap for the nine-slot hotbar (9 x 48 + 8 x 4) at the bottom centre with the hands either side (active
  72, other 56, a 2 px border on the active one) and tg's bottom slots moved into the gap until package 3; the doll
  panel at the middle right, left of tg's right column, the combat pill under it; the held-item card at the bottom
  right above tg's two-row menu; alert chips in a row at the top, right of the action buttons; one "Ping | FPS" chip
  at the top right.
- The doll: one standard ramp when no colourblind palette is chosen (level 0 green `#8FD694`, 1 to 2 yellow
  `#FFE156`, 3 to 4 orange `#FFA62B`, 5 red `#FF4D4D`, missing dark grey; a disabled part keeps tg's own picture, dark
  grey with red marks); the hatching keeps all
  levels apart (D70) without hiding the colour: at every level most of the part shows its colour. A wound's
  outline stays tg's pulsing red. The doll shows injuries only: no target header and no target outline (see
  "Targeting" in section 4). tg's zone selector (`zone_sel`) is hidden in the first-person view (not drawn, not
  clickable) and shown at tg's place while the cursor is free.
- Every key shown comes from the bindings. `I18n.T` takes key chips and server labels as arguments only, never as
  the template (`[]` is a slot in it).
- The fake server gains a `zone_sel`, a combat toggle, one alert and hand indices, for tests and screenshots; a new
  `ANOMALY_SHOT=livehud` mode is added to `godot/tools/interaction-shots.sh`. "Before" shots are taken first.

### Slices, each built, verified and reviewed on its own

| Slice | What | Tests (classes under `tests/Anomaly.Server.Tests`, with `&Category!=Meta`) |
|---|---|---|
| A | `UiTokens`, `KeyChips`, prompts beside the crosshair, the ping and FPS chip | `Client.Play.KeyChipsTests`, `Client.Play.UiTokensTests`, `Client.Play.PlayOptionsTests` |
| B | Doll colours and panel, combat pill, the fake server's combat toggle | `Client.Play.HudTests`, `Client.Play.LiveHudTests`, `Client.Interaction.FakeInventoryTests` |
| C | Native hands, the bottom row in the hotbar gap, the held-item card | `Client.Play.LiveHudTests`, `Client.Play.HeldPoseTests`, `Client.Interaction.FakeInventoryTests` |
| D | Alert chips at the top, tg's alert column (DV82) removed | `Client.Play.HudTests`, `Client.Play.LiveHudTests` |

### Slice C as built (2026-11-26): the hands, the bottom slots in the hotbar gap, the held-item card

Client only. On the human HUD (tg's inventory toggle on screen; every other HUD and hands beyond the second keep
tg's places):
- The two hands are native boxes either side of a gap for the nine-slot hotbar (9 x 48 + 8 x 4) at the bottom
  centre. A box is 72 px with a 2 px amber border only when that hand carries tg's active overlay, else 56 px. Each
  shows the held item's picture and, over it, what tg draws on a hand slot: the handcuff and blocked overlays and a
  held stack's count. A hand box is tg's hand slot for input: `HudLayer.Collect` reports the box and the picture
  square with the held items under the entry's id.
- tg's six bottom slots (ID, belt, back, suit storage, both pockets; a non-zero slot id, ordered by it) are fitted
  into the gap until package 3; a stack's count on an item there is drawn at the cell's scale.
- "[X] Swap hand" is a chip over the right hand; where that place would touch tg's drop or swap button (4:3, 5:4
  and 16:10 screens at about 125 to 190% scale) it is raised above them, and it is left out only at 1280x1024 at
  290% and above.
- The held-item card sits under the combat pill: the active hand's item picture with its count, which hand, and key
  chips for use, drop and throw from the bindings; hidden with an empty active hand. No name and no traits until
  package 3 sends them.
- With the chat window open at the bottom left the row moves right of it only while it still fits (the smaller box
  at 32 px or more, clear of tg's drop and swap buttons); otherwise it is where it is with the chat closed, and a
  window over it covers it as a window covers tg's HUD (D282).
- The fake server gained hand indices, tg's slot ids, tg's static and toggled slot groups, the inventory toggle and
  the count of settled actions.

Playtested against the real server (evidence: the session's scratchpad, `hands` and `playtest-hands`): empty hands,
an item in each, and X, tg's swap button and a click on the other box each moving the border and the card; a click
on the held flashlight turning it on; drags hand to pocket, hand to hand and pocket to hand; the back slot opening
the backpack and the belt giving up the PDA; the inventory window complete with nothing drawn twice; "30" on cable
and "15" on iron in the box and on the card, a bruise pack going from 6 to 5, a split leaving 9 and 1; counts on
the belt and in a pocket inside their cells at 100% and 250%; tg's blocked mark with an arm removed and the cuff
marks when handcuffed; Z, Q and Ctrl+Q doing what the card says and a rebound key changing its chip; no overlap at
1280x720 and 1920x1080 from 100% to 250%; a cyborg and a ghost keeping tg's layout; the HUD hidden and shown, a
death and a revival. Probes: 134,640 layouts over 40 screens, every scale from 50 to 300% and every chat position
with no overlap and nothing off screen.

Not driven: middle click to swap; a drag from the floor onto a hand box; the ID slot; an animal, an alien and the
AI in the client; reconnecting mid-round; four hands (unit test only).

Known limits:
- At 250% scale the chat window at its default place covers the left hand's box; closing or moving the chat
  uncovers it. Package 5 replaces that window.
- The handcuff marks are faint on the dark box (tg's handcuffed alert still shows).
- On 4:3 and 5:4 screens at 230% scale and above the boxes go under 32 px for want of room.
- The tooltip of a slot hidden under a window still shows (older).
- The doll panel sits a little higher than in slice B, to keep the card's room.

### Designs confirmed by the designer (2026-11-26); these replace what the slices table and the layout say

From 2026-11-26 the manager designs each UI element and the designer confirms it before it is built; a builder gets
the settled design and stops on any design choice it does not settle.

- Alert chips (slice D): a vertical stack directly above the health doll, right-aligned with the doll panel, the
  newest alert nearest the doll; square chips of 40 px with the doll panel's fill and rim and tg's alert picture
  inside; no text until package 3, when each chip grows a short label to its left. Not the row at the top left of
  Mockup 1: the stack keeps everything about the player's own state in one place and does not move with tg's
  action buttons. tg's alert column is not drawn on the human HUD.
  Where there is no room above the doll (the designer's second answer, the same day): the stack is above the doll
  when five full-size chips fit between the panel and the ping chip, and otherwise in one column immediately left
  of the doll panel, level with its top and running down. The place depends on the screen and the UI scale alone,
  never on how many alerts show. tg's order from top to bottom in both places. Only in the left place do the chips
  shrink (to 24 px at least) and then wrap into further columns to the left. The F3 debug lines are not an
  obstacle.
- The vitals column (the designer, 2026-11-26: "integrate the EKG like thing that shows the health with the health
  doll and move the doll and icons ect all to hugh the right side so they use up less of my field of view"; the
  tall narrow arrangement chosen from two mockups). One column 128 px wide against the right screen edge, anchored
  above tg's bottom-right menu: the vitals panel, the pill, the held-item card. The panel is one box of four stacked
  cells: the health doll at its whole-pixel scale S, then tg's health indicator (the EKG with its percentage), tg's
  mood face with the hunger bar, and tg's stamina indicator, each tg's own picture at scale S-1 and still tg's
  screen object for input; the panel's size never changes. tg's internals and suit-cell indicators become chips
  with the alerts. The pill keeps its full word, scaled to fit. When the column does not fit the height, S steps
  down first, then the pill shrinks; where even that does not fit (very large UI scales) a reduced column is used:
  the doll cell and the pill just left of tg's right column, tg's status icons at tg's own places. The alert stack
  follows the panel: above it against the edge, or left of the column.
- Refined from the first real-server screenshots (the designer, the same day): the panel has three cells (the
  doll, the health indicator, the mood face), not four. tg draws its stamina picture only while stamina is down,
  so stamina is a chip with the alerts, like internals and the suit cell (alerts, then stamina, internals, suit
  cell). The mood face is centred, and shares its cell with the hunger bar only while tg shows one. The hint strip
  sits 6 above the visible top of tg's drop and swap buttons, not their whole icon squares.
- Settled in review (2026-11-26): an indicator counts as shown only while tg draws something visible (its icon
  state exists and its alpha is above zero; tg hides the hunger bar with alpha 0). Any other entry tg puts in its
  right status column on the human HUD (an alien plasma display, say) is a chip too with the full column, after the
  suit cell. The hint strip has the pill's height (32), fill and rounded ends, and no rim, since it is not a button.
  While the hunger bar shows, the mood face and the bar are drawn together at the largest whole scale at which the
  pair fits the panel; at some UI scales (125% and 150% at 1080 lines) that is one less than the face alone, so the
  face is a size smaller while the player is hungry. Accepted: the column stays 128 wide.
  Where more chips show than fit one column, tg's alerts keep the first column and later chips spill to the left.
- The held-item tip ("we could also move the cards position on the screen", then chosen from mockups): the card
  is gone from the column. A place right of the right hand was tried first and dropped the same day: tg's
  bottom-right buttons leave room for a card there only at 100% scale on 16:9 screens. What is built is a
  one-line hint strip centred over the hands' row, just above tg's drop and swap buttons: the active hand's item
  picture, then use, drop and throw with their key caps, in the pill's look and height and a compact text size. No
  count and no hand tag (the hand box shows both). Never wider than the hands' row; it scales down to fit and is
  left out where it cannot. It takes no clicks. When package 3 sends item names, the name leads the line.
- Swap hint (changes slice C): the labelled "[X] Swap hand" chip is gone; a small key cap with the swap key sits
  inside the top outer corner of the inactive hand's box and moves with a swap.
- A cuffed or missing hand (changes slice C): its box has a 2 px red border and a lighter fill, with tg's cuff or
  blocked mark at full strength on top.

### Slice D as built (2026-11-26): the vitals column, the alert chips, the hint strip

Client only, as designed above. Three review rounds; the last was clean (validator PASS on 4032 tests, the stress
tester's three reproduced faults rechecked as fixed, no findings).

- Settled by the stress test: a chip's picture is always drawn inside its chip. tg throws a new alert 32 pixels to
  the right and slides it back over a second (`alert.dm:65-66`); as a chip that slide is not played
  (`LiveHud.ChipLook` drops the look's move, keeping turn, scale, colour and alpha). On other HUDs, and for the
  status indicators at tg's places with the reduced column, tg's animation plays as before.
- The hint strip and its picture stand at whole pixels (the anchor is floored). On portrait screens at very large
  UI scales the reduced column narrows and the chips shrink below 24 px so that nothing of ours leaves the screen;
  no landscape screen is affected.
- Playtested on a real server at 1920x1080 and 1280x720: an empty-hand click on the doll checks yourself, an item
  on it is used on yourself; a click on the mood face prints tg's mood readout; the pill and F toggle combat; a
  Shift-click on a chip examines the alert and its tooltip shows; alerts kept tg's order and never changed place;
  the hunger bar appeared beside the mood face without the panel changing size; limb damage, crit, death and
  revival; the strip with an item and without; the key cap following a rebind; a missing arm's red box (handcuffs
  by the builder with two clients); 100% to 300% UI scale; a cyborg and a ghost keep tg's layout.
- Not playtested: a chip click that does more than examine (the buckled alert); the doll's eye and mouth hover in
  the new place; a colourblind palette; an animal or alien HUD; the alien plasma display and the suit-cell chip (no
  dev command gives the organ or the suit; unit-tested only); changing the UI scale during play.
- Known limits: the mood face is a size smaller while the hunger bar shows at 125% and 150% on 1080 lines; the F3
  debug text may be overlapped; an open chat window covers the strip where it covers the hands; a cyborg still
  gets the combat pill (from slice B).
- Seen and older than this slice: Shift+F4 pressed twice within two seconds did not bring the HUD back.

### Slice E, asked for by the designer on seeing slice D (2026-11-26); built after slice D is committed

"id like the box around the ekg, mood and doll removed. and those 3 icons moed more to the right ot be inline iwth
the status icons more. the combat mode indicator should be moved but i think i prefered it when it had the original
sprite. maybe put it back bottom right as some of the sprites there will be removed. like remove pull, throw, resist
and rest and the crafting ect as these should all now be bound to keys correct?" Then, asked about the buttons with
no key: remove them and give them keys; "use ss14 keybinds for them"; "use G, and move offer item to right click menu
only when looking at someone you can talk too"; the run/walk sprite and tg's drop and swap buttons go too.

Settled design (the manager's, from the designer's words above; the points marked "to confirm" are shown to the
designer in screenshots before the commit). Human HUD only; every other HUD keeps tg's layout, and loses the combat
pill it had since slice B.

Vitals:
- No box: no panel fill, rim or separator lines, and no cells. Three pictures stacked against the right screen
  edge: the health doll at its whole-pixel scale S, under it tg's health indicator with its percentage at S-1,
  under it tg's mood face at S-1, 8 between pictures. All three, and the chips when they stand above the doll,
  are centred on one vertical axis, placed so that the widest square (the doll's) touches the right screen edge;
  tg's pictures carry their own transparent margin, so the art stands clear of the edge. (First built with the
  squares' right edges on one line 8 from the edge: the art then stood on three different centre lines.)
- The hunger bar is a status chip (the designer, 2026-11-26, chosen from two mockups: beside the mood face it
  stuck out left of the doll and ran into the chips where they stand left). Chip order after tg's alerts: hunger,
  stamina, internals, suit cell, other info-group entries, sleep; each only while tg draws something visible for
  it. The mood face always stands alone and never moves or changes size (this ends slice D's limit of a smaller
  face while hungry). With the reduced arrangement the hunger bar is at tg's own place.
- The hunger chip does not fade (the designer, 2026-11-26: "id rather not have the hunger chip fade in it should
  appear right when the status starts"): tg fades its bar in and out over about a second; as a chip the picture is
  at full opacity and the chip is there from the moment tg starts showing the bar and gone from the moment tg
  starts hiding it.
- The stamina chip shows only while stamina is down: tg's entry starts with no art (`stamina0`) but holds
  `stamina_full`, a blue bolt, after the first stamina update, so by the "visible" rule alone the chip stayed for
  the whole round (seen in the stress test's playtest). As a chip it is left out in the `stamina_full` state.
- The stack is anchored at the bottom: the mood face's bottom is 8 above the top of the combat sprite in the
  corner. S steps down as in slice D, only while the three pictures alone do not fit between the room under the
  ping chip and that floor; the doll is never made smaller to make room for chips. The reduced arrangement of
  slice D stays for extreme UI scales, without box and pill.
- Chips as in slice D: above the doll when five fit under the ping chip, else in columns left of the doll (6 from
  the widest of the three pictures). Each picture is still tg's screen object for clicks and tooltips.

Bottom right:
- The combat pill is removed everywhere. tg's own combat toggle (`combattoggle/flashy`, tg's sprite and its own
  flashing while combat is on) is drawn at the corner tile, tg's zone-selector place (`ui_zonesel`), in first
  person and under Tab; under Tab a click on it toggles, as tg's. The corner and the column on one axis were
  confirmed by the designer from real-server screenshots on 2026-11-26 ("yes that looks good").
- Under Tab only, in one row left of it at tg's bottom-row places: the zone selector at `ui_movi`'s place, tg's
  memories button at `ui_acti`'s place, tg's area creator at `ui_human_floor_changer`'s place. In first person
  none of the three is drawn. The hands' row yields to them as it yields to tg's bottom-right entries today.
- Not drawn and not clickable on the human HUD: tg's pull, resist, rest, throw, run/walk, drop, swap (both
  halves), inventory toggle, floor changer, crafting, language and navigate buttons. Every one has a key.
- tg's sleep button has no key: it is a chip after the status chips, only while tg draws something visible for it.
- The hint strip sits 6 above the top of the hands' row (the taller, active box).

Keys (`PlayOptions`):
- Crafting on G and the language menu on L, as in Space Station 14 (`OpenCraftingMenu`, `OpenLanguageMenu` in the
  forks' `Resources/keybinds.yml`); navigate on M (Space Station 14 has no such function). Each key sends the click
  of tg's own screen button, which the server still sends though it is not drawn; nothing changes on the wire.
- "Give item" is no longer a key or an options row (the wire's keybinding code stays). The dev flashlight moves
  from L to U.

Offer item (server; a wheel entry beyond tg's, like Pull and Strip, DV139):
- The right-click wheel on a living mob beside the player has "Offer item", after Strip, exactly when tg's
  `/mob/living/proc/give(offered)` would go through to the offer: the player is not incapacitated, is not already
  offering, holds in the active hand an item `give` accepts; the mob is not the player, is not incapacitated and
  can hold items (`TRAIT_CAN_HOLD_ITEMS`). Picking it calls tg's `give` with that mob, which checks everything
  again and makes tg's offer alert on the other side.

Known costs, accepted by the designer or to tell them: nothing on screen shows walking or running; nothing shows
that something is being pulled (the wheel says "Stop pulling", H stops).

### Slice E as built (2026-11-26)

As designed above; server and client, no wire change (the wheel's entry travels as a label, DV139). One fix round
after a clean first review (the stamina chip at `stamina_full`, the hunger chip's fade, three stale comments).

- Found in that round: an entry that is a chip by role but has no chip at the moment (stamina at full, a hunger
  bar fading out) was drawn at tg's own place, which for stamina is just above the doll. Such an entry now draws
  nothing.
- tg's navigate and area-creator buttons are not ported (`human.cs`, `Dm.Deferred`): M has nothing to click on a
  real server and no area creator shows under Tab until they are.
- tg's own "Offer item" screentip (a carbon's Ctrl+Shift-click hint, shown with any item in hand) is left out of
  the wheel so that the entry shows only by the rule; the Ctrl+Shift-click itself still offers.
- Saved keys stay as they were (package 1's rule): a saved file that has L on the flashlight or M on another action
  leaves the language menu or navigate unbound until the player rebinds or resets the keys. A saved `give_item`
  line is read and ignored.
- The hands' row still keeps clear of the inventory toggle's old place at the bottom left.
- Playtested on a real server at 1920x1080 and 1280x720 (the session's scratchpad, `slice-e` and
  `playtest-slice-e`): only the combat toggle in the corner, also after pulling, resting, throw mode and walking;
  F and a click under Tab toggle combat; the zone selector under Tab picks a part; clicks at the hidden buttons'
  old places reach nothing; the doll's check, the mood readout and the tooltips at the new places; three alerts,
  hunger, stamina and sleep chips in order; G crafting, L language menu, U flashlight, none while typing; Offer
  item between two clients (tg's offer alert on the other side, the item changes hands once, walking away
  cancels, not with an empty hand, Ctrl+Shift-click still offers); a cyborg and a ghost with tg's own layout;
  100% to 300% UI scale with nothing overlapping.
- Not playtested: an item used on a limb of the doll and the eye and mouth hover at the new place; handcuffs with
  this slice (a missing arm was); memories opened under Tab; the offer refused for a sleeping or critical person
  and from two tiles (server tests only); another z-level.
- Known limits: G does not close the crafting window (tg's button does not either; Esc does); at 250% on 720 lines
  and 300% on 1080 an open chat window covers part of the hands and the strip.

### To confirm with the owner

- The prompts move beside the crosshair; the owner moved the screentip to the top right on 2026-10-16, "out of the
  view's middle".
- The alert chips sit right of the action buttons instead of at the far top left, where the buttons are.
- D138's text and the DV82 row describe the old places; the DV82 row is rewritten in slice D.

### Known limits

- Other HUDs (cyborg, alien, basic mob, mech pilot, ghost, AI) keep tg's places, except a combat toggle with the same
  icon states; hands beyond the second stay at tg's place.
- `HudLayer`: the child order must keep `LiveHudView` below the pictures; the top-left row's measure must leave the
  alerts out (else the chips move every frame); the doll's zones and its clicks go through the same placing code.

## 7. Wireframe checklist

Every element of `ui-overhaul-wireframes.txt`, the package that delivers it, and what has to be settled when that
package starts. A package is not done while one of its rows is missing.

### Mockup 1: live HUD (Tab closed)

| Element | Package | To settle |
|---|---|---|
| The middle of the view stays clear | 2 | |
| Alert chips at the top left (`[ALERT: INTERNALS OFF] [GRAV: 1.0G] [293K]`) | 2 (icons), 3 (text and numbers, with the server message) | tg's action buttons are at the top left |
| `[PING | FPS]` at the top right | 2 | |
| Health doll at the middle right: Head, Chest, Groin, both arms, both legs; green, yellow, orange, red, dark grey for missing or prosthetic; never a percentage | 2 | tg's doll has no groin part |
| `TARGET: HEAD` on the doll | dropped | the doll shows injuries only; targeting is where the player looks (section 4, "Targeting") |
| `[C] COMBAT: PEACEFUL` toggle under the doll | 2 | the key is the binding (F) |
| `[ALT] V.A.T.S.` hint under the doll | 6 | |
| Crosshair prompts: `[LMB] Pry Airlock (Crowbar)`, `[RMB] Shove / Secondary`, `[E] Open / Strip` | 2 (LMB, RMB from the hints sent today), 3 (the held item's name), 4 (E) | |
| Held-item card at the bottom right: name, `[F1] Show Traits`, traits | 2 (picture and keys), 3 (name) | F1 is the camera mode; no traits data exists |
| Hands either side of the hotbar, with the item's name; the active one 72 px with a 2 px amber or cyan border; `[X] Swap Hand` | 2 (boxes, picture), 3 (name) | |
| Hotbar 1 to 9: 1 to 6 the worn belt's contents, 7 left pocket, 8 right pocket, 9 ID or PDA; short item labels; a press takes the item into the active hand, a second press returns it | 3 | the action buttons move to Shift+1 to 9 |
| Compact chat at the bottom left: department tags, `Local |` lines, the hint `[Say / ;Radio] Press T to talk...` | 5 | |
| A pill at the left or right screen edge for a local speaker out of view: name, distance, direction, the words | 5 | |
| Keyword pills in chat (own name, job titles, codewords) with a quiet tick | 5 | |

### Mockup 2: Tab overlay

| Element | Package | To settle |
|---|---|---|
| Tab frees the cursor and splits the view at 28% and 64%; nothing overlaps; the chat is never covered | 4 | |
| Left: the full chat and radio log, tabs `[ALL][LOCAL][ENG][COMMON]`, time stamps, the input with a channel picker | 4 (place), 5 (tabs and look) | |
| Middle: head, eyes, ears, mask, suit, back, uniform, gloves, belt, shoes around the doll, each with its item's name | 4 | names need the package 3 message |
| Middle: quick pockets (ID, both pockets), the belt's contents 1 to 6, both hands | 4 | |
| Right, top: own storage with its name, used and total capacity, `[Sort]`, close, a grid with stack counts | 4 | tg has no sort; capacity is tg's storage limits |
| Right, middle: the external container or the stripped player, distance and `Max 1.8m`, `[Close]`; closes by itself beyond 1.8 m | 4 | the server decides the range |
| Strip slots show the timer inside the slot (`[####--] 2.6s`), `Drag->Me`, `Click 2s`, `[Hidden]` pockets | 4 | replaces the strip window (tgui `StripMenu`) |
| Right, bottom: nearby floor and table items within 1.2 m | 4 | a server query with a range check |
| Drag and drop between all item slots; Shift+left click moves an item between the active hand, the open backpack and the external panel | 4 | Shift+left click is examine in the world; in the overlay it is transfer |

### Mockup 3: anatomical focus

| Element | Package | To settle |
|---|---|---|
| Hold Alt (or the middle button), without pausing | 6 | Alt is push to talk while typing; the middle button swaps hands |
| Each body part of the looked-at mob outlined on its sprite: the part under the crosshair in cyan, a severed or damaged one red dashed | 6 | the mobs are 2D sprites; the regions come from the same sprite-pixel rules the game aims by (`BodyZoneAim`), so the overlay shows exactly what a click would hit; non-humanoids need their regions defined |
| A label per part (head, arms, legs, body) | 6 | what else the label says ("Armor: High", "Slows Move Speed") has no data source today |
| Header with the current lock; releasing Alt on a part locks it; the doll's target follows | dropped | it only shows where to aim; nothing is locked (section 4, "Targeting"). The amber "locked" outline goes with it |

### Mockup 4: items, radio and machines

| Element | Package | To settle |
|---|---|---|
| Health analyzer: the readout on the held item's screen, no popup (`HOLD 'I' TO INSPECT`, `[LMB] Scan Target`, `[RMB] Toggle Chem/Wound Readout`) | 7 | I is the inventory key |
| A radio lying in the world shows what it receives as floating text within 4 m | 7 | |
| A universal panel built from a JSON-like state: `gauge`, `bar_list`, `segmented_toggle`, `button`, a title, theme and status level; floats, leaves the edges of the view clear, closes beyond 1.5 m, `[ESC]` | 7 | which machines move to it; the 298 translated windows stay |

### Mockup 5: grabs and hallucinations

| Element | Package | To settle |
|---|---|---|
| While grabbed: a banner at the top with the grab state, hatched screen edges, an orange vignette, slight sway with a level horizon | 8 | |
| A panel: grabbed by whom and how far, the state, `[SPACE / WASD] BREAK FREE` with a stamina bar | 8 | resist is B; Space is up a level |
| Hallucinations shown to one client for a short time: fake severed parts on the doll (`screwy_health_doll`), a fake item in the hand and viewmodel (`inhand_fake_item`), `fake_alert`, `hud_screw` | 8 | the server still starts them (tg's code) |

### Section 7: palette

| Element | Package |
|---|---|
| Common `#8FD694` rounded square; Security `#FF4D4D` angled octagon; Engineering `#FFA62B` striped header pill; Medical `#38D9F5` cross-notched pill; Science `#C77DFF` diamond-edged pill; Command `#FFE156` bold double-border pill; Local white on `#1E242B`, solid dark pill with a white rim | 2 (tokens), 5 (badges) |
| Shape as well as colour tells the channels apart | 5 |

## 8. Package T: aiming by look, reworked

Asked for by the contributor on 2026-11-24 with the lead developer's agreement (section 4, "Targeting"): the zone a
player targets is the body part they look at on the other mob's picture; a part that cannot be seen from where the
player stands need not be targetable; nothing downstream may break. Built before the rest of the live HUD.

### How it works today (read 2026-11-24)

The crosshair's ray hits a pixel of the mob's whole picture (`PickShapes`, any pixel with alpha of 40 or more, clothes
and held items included). `BodyZoneAim.ZoneAt` looks for the limb picture that is opaque at that pixel in the frame
drawn (`EntityLayer.DrawnDir`); the zone rides the click or throw (`AimZone`, six values, bits 4 to 6 of the modifiers
byte); the server's `AimAt` (`ServerHost.World.cs`) sets tg's `zone_selected` before tg's click runs, for a carbon
target only; tg then rolls its own miss (80% the zone chosen). An aimed shot recomputes its zone on the server from
the committed mask table (`ShotGeometry`, `Generated/BodyMasks.bin`, DV95). A hover carries no zone.

### What is wrong

- A. The lookup takes the first limb found in list order, not the one drawn on top. Where the legs overlap in the
  side frames the wrong leg wins (W frame, pixel 17,5: tg draws the left leg, the lookup says right). The server's
  mirror for shots is wrong the same way (it ignores the leg masks of tg's `handle_masking`), so the test that
  compares the two passes while both are wrong.
- B. A pixel of the picture that no limb covers (hair, a hat, a backpack, a held item, a coat's edge) gives no zone,
  the server then skips `AimAt`, and tg uses whatever zone was selected before, with nothing shown. This is the likely
  cause of an arm aimed at from behind hitting the chest, and of a leg reading as the other leg, in the playtest;
  the probe tests of slice T0 settle it.
- C. The hover's hints (the prompts beside the crosshair) are built from the zone of the last click, not the look.
- D. A limb is recognised by the end of its icon state alone (`screwdriver_head` would count as a head); a picture
  with an eight-direction overlay picks diagonal frames the server's mirror never uses; DV30's note that a lying body
  gives no zone is out of date.
- Looking can pick six zones only; eyes, mouth and groin need tg's zone selector.
- Xenomorphs have tg body parts but are drawn as one picture, so they give no zone at all.

### Design

One zone map per picture and direction, made by the same compositing that draws the picture, so what is seen is what
is hit: body-part pictures paint their zone in draw order, every other picture paints nothing (clothes and held items
never block). The hitbox of a limb-built mob is its naked body (the contributor, 2026-11-24): clothes, hair, hats,
backpacks and held items are not part of what a click or a look hits, so a ray through a pixel no body part covers
goes on to whatever is behind, and every hit lands on a body part and carries a zone. A mob drawn as one picture
keeps its whole picture as its hitbox. The map is cached and is also what the anatomical focus overlay draws. The
server keeps its own table for shots (already body parts only) and the two are compared by test.

The finer regions (the contributor, 2026-11-24): only regions that are real zones in tg's rules are mapped and
drawn. Those are the head, the chest, the two arms, the two legs, and the eyes, the mouth and the groin, each where
it sits on tg's targeting doll, whose layout is the human sprite seen from the front. Hands and feet are not
regions: in tg a hand is part of its arm and a foot part of its leg (nothing reads tg's precise hand and foot zones
as a selected zone), so they belong to the arm and the leg and nothing separate is shown for them.

### Slices

| Slice | What | Touches |
|---|---|---|
| T0 | Probe tests with tg's real icons: the zone at set pixels of a naked and a dressed human in each frame, the pixels with no zone, and the whole chain from a ray behind the mob to the zone | tests only |
| T1 | A body part is recognised by its icon file, not the end of its state; the hitbox of a limb-built mob is its body parts alone; the limb-only lookup stays as it is for the comparison with the server | client only |
| T2 | The zone map from the compositor's draw order (fixes the legs); the server's mirror reads the leg masks and its table is regenerated | client and server, no protocol change |
| T3 | The map cached per picture and direction and offered to the overlay | client only |
| T4 | Eyes, mouth and groin by look (three more `AimZone` values, the spare modifier bit as the high bit); the hover carries the zone so the prompts name the right action | protocol change: a release by the owner |
| T5 | Self-targeting without the selector: the ctrl-click grasp and finer zones on the doll; then tg's zone selector goes entirely | client, after T4 |
| T6 | Authored zone pictures for xenomorphs, then the anatomical focus overlay (package 6) | art, tools, client and server |

### To settle before T4

- Settled: the finer regions follow tg's targeting doll (see "Design"), so the eyes are the doll's eye pixels and
  looking there with a screwdriver or a pen in hand is tg's eye stab, as choosing the eyes on the selector is today.
- Settled (the contributor, 2026-11-24): the zone follows the look between clicks, so the prompt names what a click
  will do: the hover carries the part looked at and the server sets tg's `zone_selected` from it. It does not change
  while the player is in a timed action (any of tg's do-afters is running for that mob): a surgery step, a gun held
  in the mouth and the like keep the zone they started with, wherever the player looks meanwhile; the look applies
  again once the action ends. The server decides this, not the client.
- What mobs without tg zones (simple and basic mobs, silicons) show in the overlay.
- Xenomorphs: tg gives an adult six body parts (head, chest, two arms, two legs) and a larva two (head, chest),
  with damage by part, but draws each caste as one picture (`alien[caste]`), so their regions have to be drawn by
  hand per caste and direction.

### Downstream, guarded by tests

tg's readers of `zone_selected` (attacks, help actions and their hints, surgery, throws, the projectile's first
zone, the self-grasp); `MeleeAimTests`, `AimedShotGameplayTests` (the client and server grids must stay equal),
`FirstAidGameplayTests`, `HeadTraumaGameplayTests`, `DragModelTests`, the codec and protocol tests for T4; record and
replay (the zone is a plain byte in the log); the doll's self-click (DV102) and the grasp (DV72). DV30 is updated
with T1 and T2, and a new deviation row is added for the hover in T4.

### T0 and T1 as built (2026-11-24)

The probes show the frame, the pixel and the limb lookup are right for an arm aimed at from directly behind; the
playtest's "chest" came from pixels with no zone (hair, the backpack, a sleeve edge), which the body-only hitbox
removes. Playtested against the real server: from behind, the arm aimed at took 6 of 9 and 10 of 10 hits (the rest
tg's own miss roll); hair, a backpack and a held item are passed through to what is behind; clothing over a limb
still counts; a lying body, animals, a borg and a xenomorph behave as before; twelve dressed people in view cost no
measurable frame rate.

Found in review and fixed before the commit: a body drawn outside the icon square (gigantism) and a body raised or
shifted by furniture (a stool, a bar stool, an operating table, a bed's tile, a handrail) had a hitbox that did not
cover the drawn body; for the raised body the zone read was also several pixels off, which is older than this
package. The hitbox and the zone now take the body's placement from the compositor.

Known limits and open points:
- Where the legs overlap seen from the mob's left side, the far leg won; slice T2 makes the leg drawn in front win,
  on the client and in the server's mirror (the view from the right side was already correct).
- The server's mirror for shots (`ShotGeometry.Cross`) did not apply the root's `pixel_w`/`pixel_z`. Measured in
  slice T2: a vertical shift (a stool, an operating table, a bed) never put a shot off, because the picture stands on
  its lowest drawn row on both sides; a sideways shift (a handrail) and a giant's body past the icon square did, and
  T2 fixes those. An earlier note here said shots at a seated person read several pixels off; that was a reviewer's
  reading, not a measurement, and it was wrong.
- `mouse_opacity` is not replicated, so the client cannot tell from data which entities BYOND would click through.
- Shots only (the server's mirror; melee and throws read the client's picture and are right), measured by the stress
  test of slice T2 and older than this package:
  - a monkey: tg moves its torso, arms and head down with height filters, which the client draws and the server's
    table does not know, so about half of a monkey's body pixels read as another part for a shot (dwarf and tall
    humans probably likewise; not measured);
  - a lying moth or lizard: wings and tails reach below the body; the client stands the picture on its lowest drawn
    row and the server on the lowest body row, so the server's picture sits 3 rows (moth) or 7 rows (lizard) off; the
    same cause as the dressed lying body whose clothes reach below it;
  - a golem's hands (7 to 19 pixels) and a husk seen from the east (8 pixels) differ between the two sides.
- Not undone by the mapping: a root filter that moves pixels (displacement, wave), a limb nested in a non-limb
  overlay with its own offset, and a flick or inset drawn in place of the mob's own appearance.
- Body pictures that are not limbs (a lizard's snout, horns, frills, tail and spines, a moth's wings and antennae, a
  pod person's "hair") are not in the hitbox, up to about half of such a sprite. Settled by the contributor
  2026-11-24: they do not count; only the limbs are hit.
- A strap or a headset pixel beside the body is passed through like any clothing, which can look like a miss.
- Not tried in the client: the hold-right-click radial, the hover outline option, a second mob behind the hair.

Seen in the playtest, older than this package:
- tg's short-lived effects (the hit flash) take clicks; in tg they are click-through
  (`mouse_opacity = MOUSE_OPACITY_TRANSPARENT`). A quick second attack on the chest is often lost to it. To be fixed
  with slice T2 (the contributor, 2026-11-24).
- The prompt's lines for one target differ between runs (combat mode with an item shows "Hug / Shove" or only
  "Pull"; a corgi shows only "Pull" although a click pets it).
- A bare-hand punch names no body part in chat.
- The server logs a `NullReferenceException` in `datum._SendSignal` (`signals.cs:175`) after mobs are spawned on wall
  tiles, and setting the cerulean or mushroom species throws.

### T2 as built (2026-11-24)

Committed as `5d1449c`. Playtested against the real server: from the left side the near leg took 8 of 8 hits and the
far leg's sliver 8 of 8; 70 pistol shots at a standing person from the front and both sides each hit the part under
the crosshair, as did shots at a lying person, a bar-stool sitter and a giant's head; 20 of 20 quick attacks and 6
of 6 quick shots stayed on the person (before, 2 or 3 in 6 went to a short-lived effect).

### T4 design: the finer zones and the look-following prompt

A protocol change (the click, the throw and the hover gain what is described here), so it needs the owner's release
(`tools/release.sh`); until then the build warns `ANOMVER01` and `BuildInfoTests.TheVersionNamesTheProtocolItSpeaks`
fails by design. Built in two parts, T4a then T4b, for one release.

T4a, the finer zones by look:
- `AimZone` gains Eyes, Mouth and Groin (7, 8, 9; the spare bit of the modifiers byte is the zone's high bit, so
  every byte sent today keeps its meaning). The server's `AimAt` maps them to tg's `BODY_ZONE_PRECISE_EYES`,
  `_MOUTH` and `_GROIN`, which tg's own code already handles everywhere a selected zone is read. The server refuses
  any other value, as today.
- The regions are tg's own, from the zone selector's `get_zone_at`, whose doll is the human sprite seen from the
  front, placed relative to the head and the chest as they are drawn so that a short, a tall or a non-human body
  keeps them on its face and hips:
  - front view: with the head's top row `t` and middle column `c`, the eyes are rows `t-4` to `t-2`, columns `c-1`
    to `c+1`, plus columns `c-2` and `c+2` on row `t-3`; the mouth is rows `t-6` and `t-5`, columns `c-1` to `c+1`
    (on the human sprite: tg's rows 25 to 27 and 23 to 24, columns 15 to 17);
  - side views: the same rows, the three head columns nearest the face's front edge on each row;
  - from behind: no eyes and no mouth (they cannot be seen);
  - the groin, in every view: the chest's lowest four rows (tg's rows 10 to 13);
  - a region applies only on a pixel whose part on top is the head (eyes, mouth) or the chest (groin).
- Only real zones are regions: hands and feet stay part of the arm and the leg.
- A shot still reads six zones on the server (DV95): a shot aimed at the eyes hits the head.
- The zone map is computed once per picture and direction and cached (slice T3 folds in here).

T4b, the look-following prompt:
- The hover carries the zone looked at and is sent again when the zone changes on the same mob. The server sets
  tg's `zone_selected` from it without logging (`set_selected_zone(..., should_log = FALSE)`), then runs tg's hover,
  so the prompt names what a click will do (Headpat on the head, Hug on the chest).
- Not while the mob is in a timed action: while any of tg's do-afters runs for that mob the hover's zone is ignored,
  so surgery, a gun held in the mouth and the like keep the zone they started with. The server decides.
- A click still sets the zone itself, as today, so nothing depends on the hover having arrived.
- Bounded: a hover with a zone is rate-limited like any hover; the server validates the zone and the target as for
  a click.

Found in the review of T4a and settled by the contributor (2026-11-24): with the mouth targeted, a gun fired from the
adjacent tile reaches tg's `handle_suicide` (`gun.dm:662`: the gun pointed at the head, a 12 second do-after, then a
shot of five times the damage, or "decided to spare"), which the port had left untranslated (`not-translated.md`
section 47), so the click did nothing. "The gun in mouth should only work at close range like melee range, otherwise
it should just target the closest part of the body for damage": that is tg's own rule (the branch needs the target
adjacent; from further away the shot is an ordinary one and the server reads the mouth as the head), so
`handle_suicide` is translated as tg has it, with T4a.

Known limit of that rule, noted at the contributor's request (2026-11-24): because looking picks the zone, a gun
fired from the adjacent tile with the crosshair on the mouth's six pixels starts the 12 second action instead of
firing at once. In tg the player chooses the mouth on the selector beforehand, so it cannot happen by a slip of
aim; here it can in a point-blank fight. Left as tg's behaviour; if it proves a nuisance in play, the action can be
tied to a deliberate input instead of the look.

### T4a as built (2026-11-24)

Protocol 98 (built as 95; renumbered in the merge with origin/main). Playtested against the real server: from the front the eyes, the mouth and the groin select tg's
precise zones on exactly the pixels of tg's doll (read from the zone selector's picture and from a penlight's two
answers); from each side they sit on the three columns at the front of the face; from behind there are none; a
bar-stool sitter's and a giant's follow the drawn face; a screwdriver on the eyes is tg's eye stab; a help click on
a lizard's groin pulls its tail; a shot at the eyes from two tiles hits the head. The gun at the mouth from the next
tile: the "points ... at ...'s head" line, twelve seconds with no shot, "pulls the trigger!", then 150 brute to the
head against 30 for an ordinary point-blank shot; a laser likewise (125 burn); an unloaded pistol clicks; from two
tiles the same aim is an ordinary shot at the head.

Seen in the playtest, tg's own behaviour:
- When the target or the shooter moves away during the twelve seconds nothing is said (tg prints the "spare" line
  only while the target is still adjacent).
- A click on the target during the wait clubs it with the gun (tg's click falls through to the melee attack while
  the gun is busy).

Not playtested: the eyes and the mouth of a lying person. Three knocked-down people were all drawn back up, where
there is no face to aim at; the automated tests cover a lying body's regions. Whether a knocked-down person should
be drawn face up more often is a separate question about the lying picture, older than this package.

Lying, playtested afterwards (2026-11-24): on a limp body lying face up, seen from the side it faces, the penlight
answered "eyes" on exactly 11 pixels (the three by three patch and the two ends of the eye line) and "mouth" on
exactly 6, and nothing on 16 spots of the head around them; seen back up, 33 clicks on the head gave neither, and
the lower torso selects the groin.

What decides it is older than this package: a lying body that is conscious (knocked down, resting) is always drawn
back up (`BillboardMath.ChooseLyingDir`: only a limp body, `stat != 0`, shows its front, and only to a viewer on the
side it faces). So the eyes and the mouth of a conscious lying person, including a conscious patient on an operating
table, cannot be picked by looking; tg's zone selector in cursor mode still can. To settle with slice T5, before the
selector goes.

Seen in that playtest, not looked into: the face of the lying face-up body was drawn as a bright white patch (the
clicks on it still found the eyes and the mouth), and red cubes and streaks appeared around the body while clicking.

### T4c: a patient's face (asked for by the contributor, 2026-11-24)

"The eyes and mouth of a conscious patient cannot be picked by looking, including a conscious patient on an
operating table - this needs to be fixed." The cause is the rule for which side of a lying body is drawn
(`BillboardMath.ChooseLyingDir`, from the owner's play test of 2026-10-13): a conscious lying body is always drawn
back up (knocked down or crawling, it lies face down), and a limp one front or back by the side the viewer stands
on. Planned, after T4b, client only: a body lying buckled to furniture that lays people down (a bed, an operating
table, a roller bed, a stasis bed; the posture byte already carries the buckled bit, DV573) is drawn face up from
every side, conscious or not, so a surgeon can aim at the eyes and the mouth from any side of the table. A conscious
person lying on the floor keeps the owner's face-down picture. This narrows a rule the owner set, so the owner is
told with the change; `DECISIONS.md` is not edited by us.

T4c, extended the same day at the contributor's suggestion ("maybe there should be an option to flip over someone
laying down, via the context interaction wheel"): besides the furniture rule, the hold-right-click radial on a lying
person offers "Turn over". It is not in tg (a deviation row with the change). As planned:
- the server keeps which side is up for a lying mob once someone has turned it; the posture byte carries it in a
  spare bit (bits 6 and 7 are free), so every client draws the same side from every position; it is forgotten when
  the mob stands up;
- without a turn the defaults stay: buckled to lying furniture face up; otherwise the owner's rule (conscious back
  up, limp by the viewer's side);
- the radial entry is one more `RadialExtra` through the existing `ContextPick` (protocol version 86's mechanism);
  the server checks the actor is adjacent, able to act and has a free hand, and that the target is lying; one line of
  visible text ("X turns Y over."); no do-after;
- server and protocol, in the same unreleased protocol version as T4a and T4b.

Corrected by the contributor (2026-11-24): turning someone over is a timed action, "short like 1-2 seconds, how
long it takes to turn someone around". So: a do-after of 1.5 seconds on the target with tg's progress bar and tg's
usual interruptions (the actor or the target moves away, the actor is stunned or loses the free hand, the target
stands up); "X starts turning Y over." when it begins and "X turns Y over." when it ends; nothing changes if it is
interrupted. This replaces "no do-after" above.

### T5: targeting oneself by looking down (asked for by the contributor, 2026-11-24)

"For self targeting I'd like the player to be able to look down at themselves. The perspective should shift
forwards slightly so they can see their body like how when you look down at your chest your eyes are forwards so
you can see all your body." This replaces the earlier plan for T5 (finer zones on the health doll): the player's own
body is seen below when the view pitches down, the eye moving forward a little, and the part of it under the
crosshair is what a click targets, with the same zone map as for others. Under investigation (how the camera and the
own mob's picture work today); the design is written here before anything is built. Open from the start: one's own
head, eyes and mouth cannot be seen by looking down, so they need another way (the health doll, or something else).

T5, settled so far (the contributor, 2026-11-24): one's own head, eyes and mouth are targeted through tg's zone
selector in cursor mode ("the zone selector under tab I think is best"), not through regions on the health doll.
To work out in the design: today a click on the doll's head with an item sends Head and would overwrite an eyes or
mouth just picked on the selector (DV102). And the opposite of today's safety must hold: today the own body is never
under the crosshair, so a click in the world can only reach others; once the own body can be looked at, it must not
get in the way of an item, a body or a person at one's feet.

T5, settled (the contributor, 2026-11-24): "Your body should count as a target when you're looking at it, same as
targeting other people (with the exception of what we have said for head etc)." So no special rule for the own
body: its hit shape is its body parts alone, the nearest thing under the crosshair wins, and the part under the
crosshair is the part targeted, in every mode, combat mode included (a weapon swung at one's own body hits oneself,
as a click on one's own sprite does in tg). The design places the own body's picture so it hides as little of the
floor at the player's feet as it honestly can, and states what it does hide.

T5, settled (the contributor, 2026-11-24): the own body is drawn exactly as every other player's sprite is, the
standing vertical billboard; nothing about how it is drawn changes. Only the view shifts: "forwards and down so that
you're looking back at your body. Exactly like a human looking down as the eyes are on the neck so looking down also
shifts the eyes forwards". So the eye turns about a neck pivot as the view pitches down. No laid-down or tilted
picture of the own body.

Read from the code for T5 (2026-11-24, not run): the eye sits straight above the centre of the player's collision
capsule at 1.3 m with no forward offset (`CameraRig.cs`); the pitch already reaches 89 degrees down; nothing on the
camera's side keeps the eye out of a wall, only the capsule's 0.4 m radius does (near plane 0.05 m); in first person
the own billboard is drawn as a shadow only (D288) and the crosshair's pick always excludes the own mob; the server
already accepts a click whose target is the sender's own mob and applies its zone, so aiming at oneself needs no
server or protocol change; in tg an item with force clicked on oneself hits oneself in any mode unless an
interaction claims the click first. Also read: a click on the health doll's head with an item sends Head, which
overwrites an eyes or mouth picked on the selector just before, so a penlight on one's own eyes or mouth probably
cannot work today.

### T4b as built (2026-11-24)

Still protocol 98 (built as 95) (a second wire change inside the unreleased version, as version 89 did): a hover on an entity may
carry the zone as one byte after the target; a hover without one keeps its old bytes. Beyond the design above:
- the server remembers the last look it skipped during a do-after and applies it, with fresh hints, in the tick the
  mob's last do-after ends; any later hover, unhover, click or throw clears what was remembered;
- in cursor mode a hover carries no zone, so a zone picked on tg's selector is not overwritten by the pointer
  passing over a person;
- the client sends zone changes no faster than the server's hover limiter refills (10 a second, bucket 5).

Playtested against the real server: with empty hands the prompt reads Headpat on the head, eyes and mouth and Hug
elsewhere as the crosshair moves with no click, and Pull tail on a lizard's groin; looking around during the gun's
twelve seconds neither changes the prompt nor spares the target, and afterwards the selector shows the part looked
at; a click during the wait still spares; in cursor mode the selector kept the left arm while the pointer crossed a
person. While the crosshair is moving the prompt trails it by a moment; after half a second's rest it was always
right.

Known, tg's own or for later:
- with most items in hand tg's hint for a person does not depend on the zone;
- tg does not list a do-after without a target in `do_afters`, so such an action does not freeze the zone;
- the ctrl-click grasp on the health doll (DV72) takes the zone last looked at on someone else (measured: a look at
  another's right leg, then the grasp, grasps one's own right leg); slice T5 changes how oneself is targeted;
- not playtested: a surgery step with looks during it (the automated test covers it: the step stays on the chest).

T5, added by the contributor (2026-11-24): "Add eyes and mouth to the health doll." So one's own eyes and mouth have
two ways: tg's zone selector under Tab, as it is, and the health doll, whose head gains the eyes and the mouth as
regions: an item used on the doll's eyes or mouth is used on one's own eyes or mouth, and elsewhere on the head on
the head. This also removes the fault read from the code, where a click on the doll's head overwrote an eyes or
mouth picked on the selector.

### T5 design as proposed (2026-11-24; read from the code, nothing run; awaiting the contributor's answers)

Client only: no server and no protocol change (the server already accepts a click and a hover on the sender's own
mob with a zone).

The eye about the neck: the eye turns about a point `d` below it on its own vertical line; at pitch `p` below level
it moves forward `d sin p` and down `d (1 - cos p)`. Proposed `d` = 0.10 m (the jaw and upper neck of the sprite,
drawn at 0.05 m a pixel with the eyes at about 1.275 m). With the default 75 degree field of view the own body
enters the view at about 57 degrees; at 75 degrees the legs, groin, belly and hands are in view; at the limit of 89
degrees everything below the neck but the chest's top row, the legs a band about 40 pixels tall under the crosshair
on a 1080p screen and the torso about 415 pixels tall below them, as in life. The head never enters the view. The
pitch limit, the near plane and the field of view need no change. The eye moves at most 0.10 m and the collision
capsule keeps its centre 0.4 m from anything solid, so the eye stays out of walls without a cast.

The own sprite: the same standing billboard, shown to its own eye once the pitch passes about 40 degrees (shadow
only before, as today, D288), never its rows above the neck (at level pitch the eye is inside the sprite's plane at
head height). Not shown while lying (the eye sits inside the lying picture), dead, a ghost, in an exosuit, a scope,
a locker or third person; the health doll covers those. The first-person held items are lowered out of view as the
body comes in.

Targeting: while shown, the own mob is no longer excluded from the pick; its hit shape is its body parts below the
neck; the nearest surface wins; the click and the hover carry the zone, in every mode. The sprite is a vertical
plane through the middle of the body across the facing, so it only ever covers floor behind that line: whatever the
player faces stays clickable; a thing under or behind the player is covered by the legs and torso, and the player
steps back or turns. tg's rule holds: an item with force used on one's own body hits oneself in any mode.

The health doll: its head is tg's 32 by 32 picture with the head on rows 23 to 29, so tg's `get_zone_at` eye and
mouth pixels land on it; `DollZoneAt` returns Eyes or Mouth there and Head elsewhere on the head. At a 1080p HUD
scale of 1 the doll is drawn at 3 screen pixels a pixel: the eyes are a 9 by 9 patch, the mouth 9 by 6. While the
pointer is over them, tg's own `eyes` and `mouth` hover pictures are drawn. The ctrl-click grasp carries the limb
clicked.

To settle: whether the own sprite is mirrored left to right for its own eye (unmirrored, the player's left arm and
the item in the left hand appear on the right of the screen, as on the doll but against the first-person held
items); `d` 0.10 m or larger (0.20 m gives legs about 98 pixels tall and loses the top six rows of the chest).

### T5 design, corrected by the contributor (2026-11-24); this replaces the two points "to settle" above

"The eyes should be angled down more than 90 so that you're looking slightly back at yourself, pivot about a point
15cm from the neck and see how that works. Also there should be no need for mirroring unless you've flipped the view
somehow. The view should remain in the same orientation as it starts. So when you're looking down your body should
be on the bottom half of the screen and the floor ahead."

- The pivot is 0.15 m below the eye. The downward pitch limit is raised past 90 degrees so the view looks slightly
  back at the body; to start, 105 degrees, tuned in the playtest.
- The view keeps its orientation all the way: the pitch is one continuous turn about the camera's own right axis,
  with no flip or roll at 90 degrees, so the screen's right stays the player's right and the floor ahead stays in
  the upper part of the view.
- No mirroring is a consequence, not a choice: the own body is the same card other players see, standing where the
  player stands and facing the way the player faces. Looked back at from the player's own eye, its left arm is on
  the player's left. (The earlier "mirror" question came from the billboard code laying a picture out along the
  camera's right; for its own eye the sprite must stand facing the mob's facing instead of turning to the camera.)
- Worked from the geometry (not yet seen in the game): at 100 degrees the eye is 0.15 m ahead of the body and about
  1.12 m up; the feet sit just above the middle of the screen, the legs across it, and the torso fills the lower
  half to about row 20; the floor ahead fills the upper half. At 105 to 110 degrees the body rises to about two
  thirds of the screen. The head stays out of view at every pitch.

T4c as built, one widening of the wording above (found in review, 2026-11-24): the face-up rule applies to a body
lying while buckled to any object that is not a mob, which is what the posture byte can tell (its lying and buckled
bits), not only to furniture that lays people down. The difference is a person knocked down or paralysed while
buckled in a chair or on a vehicle: drawn face up from every side where before a conscious one was drawn back up.
Kept: it serves the same purpose (the face of someone slumped in a seat can be aimed at) and needs no further wire
change; narrowing it to laying furniture would need the server to send the side itself.

T5, added from the designer's raw notes (the reference document, Appendix A, 2026-11-26): "inventory ui that shows
sprite in the middle, lets you use items on sprite like using on self this targets the body part you target on the
sprite. so you can like grab syringe on cursor and click on chest for example." So oneself is targeted four ways,
and each is to work on its own: looking down at one's own body (below the neck); the character's sprite in the
middle of the Tab overlay (every zone, the head, eyes and mouth included; package 4); the health doll, whose head
gains the eyes and the mouth; and tg's zone selector under Tab.

### T5a as built (2026-11-26): the look-down camera and the own body in view

Client only; no server or protocol change. The own body is not a target yet (slice T5b).

- The numbers live in `godot/scripts/Shared/Play/LookDown.cs`: `PivotMetres` 0.15, `MaxDownDegrees` 105,
  `MaxUpDegrees` 89. The pivot shrinks in proportion for an eye lower than the standard 1.3 m (a small or lying
  body), so no eye turns under the floor, and never grows. The 105 degrees apply in first person only; the free,
  orbit and editor cameras keep 89 and clamp on a switch.
- The own body shows once no ray of the view can miss the card on its way in: from 52.5 degrees down at the default
  75 degree field of view (65 at 50, about 38 at the widest). It rises from the bottom edge; shadow only before, as
  D288. Not shown lying, limp, dead, a ghost, observing, in an exosuit, a scope, a locker, or another camera.
- The card stands across the view yaw with its front toward the eye ahead of it (`face_back` in the picture
  shaders), so the left arm is on the screen's left with no mirroring.
- Everything that read "ahead" from the camera's forward takes it from the yaw now (the own body's frame, the muzzle
  flash, the compass, the flashlight's sway); pictures that face the eye lie flat past straight down instead of
  sinking into the floor; a pick hits a standing picture where the shader draws it at every pitch.
- The held items drop out of view over the 15 degrees after the body starts to show.

Playtested against the real server (evidence: the session's scratchpad, `t5a` and `playtest-t5a`): a toolbox in the
left hand is on the screen's left at 100 degrees and on the viewer's right seen from the front, and the right hand
likewise; from 0 to 105 degrees the body rises from the bottom edge with no pop and no cut, feet at the middle at
100 and the body to about 58% of the screen at 105, never the head; walking and strafing at 100 degrees move as at
level; items at the feet lie flat from 90 to 105 and a click at 95 picks one up; the held item is up at level, gone
at 100 and back after looking up; no body lying, dead, or in the free camera, which stops at 89; a mouse's eye
stays above the floor at 105; the flashlight's spot moves smoothly through 90; name tags, the radial and a pistol
shot at the floor behave past 90.

Not driven: the mouse path to the 105 limit (a scripted pitch sets the camera directly); a ghost, a locker and an
exosuit; a mirror and a wet floor while the body is drawn (one window at 65 degrees showed no second body); chat
bubbles and thrown items past 90.

Known limits:
- At 15 cm the torso is drawn very large and the legs are a short band under the crosshair. The two numbers are the
  contributor's to tune after seeing it.
- With the eye-height option raised well above 1.3 m the head can come into view at 105 degrees.
- A person behind the player is never in view (at 2 m their arms are 84 degrees back from straight down; the view
  reaches 52.5), so nothing behind can be clicked by looking down.
- A picture that faces the eye (a loose item) lying flat past 89 degrees can miss a pick along a one-pixel border
  (the older `FacingLift`).
- A start argument or scripted pitch is not clamped until the first mouse turn, as before.
- For T5b: `Billboard.TryPick` and the hover outline (`HoverHighlight.BoxOf`) do not follow `FacesBack` yet; nothing
  reaches them with the own body while it is excluded from the pick.

To tell the owner: D288 (the own body is a shadow only in first person) now has the body itself in view when the
player looks down past about 52 degrees.

### T5's numbers changed (2026-11-26): pivot 0.25 m, limit 135 degrees

Settled by the contributor from a comparison sheet (section 0). `LookDown.PivotMetres` is 0.25 (was 0.15) and
`MaxDownDegrees` 135 (was 105); the numbers in "T5a as built" below are the old ones. What follows from them:
- The crosshair climbs the own body's middle line: the feet (the left leg) at 105 degrees down, the groin at about
  119, the chest at 135 (pinned by `LookDownTests.TheCrosshairRisesUpTheOwnBodyFromTheFeetToTheChest`; in the
  playtest wrench clicks at those angles hit the left leg, the groin and the chest). The arms and the head stay
  out of reach by look, as accepted; the inventory is the main way to act on oneself.
- With the longer neck the feet come into view a little later (about 63 degrees down at the default field of
  view; the body counts as shown from 52.5), and a body with an eye between about 0.33 and 0.55 m now sees itself
  when looking down; before, only an eye above 0.55 m did.
- Playtested on a real server at 90, 105, 120 and 135 degrees with a scripted pitch, empty-handed and with an
  item in hand (the session's scratchpad, `lookdown-135`): no view through the own head or body, no near-plane
  clipping, the held item fully lowered. The stress test then drove the real mouse: the pitch stops at 135 with
  no flip or jitter and comes back smoothly; W, A, S, D and a yaw turn at 135 go the same ways as when level; a
  wrench, a pistol used on oneself and a throw at 120 and 135 behave; a possessed monkey and corgi show their own
  body with nothing cut; the dev flashlight's patch does not turn round past straight down.
- Not playtested: standing against a wall or in a doorway at 105 to 135, the smallest and largest field of view,
  a cyborg, lying, buckled, dead, third person and free look entered at 135, a held flashlight item.
- Raised by one reviewer and not reproduced (so not acted on): for a body whose eye is between about 0.33 and
  0.46 m, the near plane would cut a strip of the own sprite from about 128 degrees down, because `BodyFrom` checks
  only the lower end of the pitch range. The monkey and the corgi tried in the client had a human's eye height
  (1.3 m), so no such body was found; if one turns up, `BodyFrom` should also refuse a neck that does not clear the
  near plane at the limit.

### T5b and T5c as built (2026-11-26): the own body as a target, the doll's eyes, mouth and grasp

Client only; the server already took a click, a hover and a throw at the sender's own mob with a zone.

T5b:
- The own mob is left out of every first-person pick except while its body is drawn for its own eye
  (`LookDown.PickExclude`): the crosshair, the pointer in cursor mode, the throw, the drag source, pointing. No
  special rule: body parts alone, the nearest surface wins, the zone is the part under the crosshair, in every mode.
- What the crosshair reaches (geometry, not a choice): the eye sits ahead of the body's vertical axis along the yaw
  and looks back along the same line, so the crosshair's ray always passes through that axis and meets the card on
  its middle column only. From about 97 degrees it is on the own feet and legs (the left leg: the middle falls on
  column 17), from about 103 on the groin (tg's attack text says "chest" for it), at the 105 limit 0.57 m up. The
  chest proper would need a limit of about 135 degrees. The arms and the right leg are never under the crosshair:
  they are reached by the pointer in cursor mode (hover and the Ctrl-click grasp; a plain click on the world does
  nothing in cursor mode, an older rule), by the health doll and by tg's zone selector. For the contributor to
  decide with the pivot and the limit.
- What the body covers: only floor behind the body's own line. At 95 degrees the crosshair is about 5 cm ahead of
  it; an item at the feet is picked up there, and from about 97 degrees the crosshair is on the own feet.

T5c:
- `BodyZoneAim.DollFaceAt` refines the doll's head into tg's eye and mouth pixels (`get_zone_at`) for an item used
  on the doll and for the hover picture (tg's `eyes` and `mouth` states of `screen_gen.dmi`, half transparent).
- The Ctrl-click on the doll carries the limb clicked, the head on the face pixels too: tg's self-grasp looks the
  zone up as a body part and has none for the eyes or the mouth (found in review).

Playtested against the real server (evidence: the session's scratchpad, `t5bc` and `playtest-t5bc`): a wrench swung
with the crosshair on the own body hits "yourself in the left leg" at 98 and 100 degrees and the groin at 103 and
105, in and out of combat mode, and an empty hand checks oneself for injuries; from 60 to 95 degrees clicks go to
the person ahead or the floor, never to oneself; lying or at level pitch nothing lands on oneself; in cursor mode
the Ctrl-click grasps the bleeding limb under the pointer with left and right correct, and a pointer on an empty
sleeve reads the floor behind; dragging the own body, or the doll, onto a table climbs it and onto a chair sits; a
throw at the own body drops the item on the own tile; on the doll the eye and mouth pictures show under the pointer
and go away off them, a penlight gives tg's eye and mouth messages, a bruise pack on the rest of the head treats the
head, a Ctrl-click on the eyes, the mouth and the plain head grasps the bleeding head and on each arm and leg that
limb while the selector holds another part; the selector is not changed by the doll; the eye picture sits on the
face in the three colourblind palettes and at 150% and 250% scale; examine and the radial work on the own body.

Not driven or not confirmed: the mouth's hover picture at 150% and 250% scale (the click there lands on the mouth
pixel; the screenshots are too small to show the picture); the free camera; a gun held to one's own mouth; two
players at once.

Known, tg's own or older:
- The prompt on the own body never names the part (tg's hints do not depend on it for most items).
- A gun with the crosshair on the own leg clubs the leg and does not fire (tg fires at oneself only at the mouth).
- A toolbox used on oneself out of combat mode opens tg's tool radial, as on anyone.
- The self-grasp's timed action has no target, so it does not freeze the zone (section 8, "T4b as built").
- An item lying at the feet is only under the crosshair in a narrow band around 95 degrees.

## 9. Reference document checklist

Every element of `ui-overhaul-reference.txt` that section 7 does not already carry, the package that delivers it,
and what has to be settled. Where the reference and the wireframes say the same thing, section 7 has the row. "A"
is the designer's raw notes (Appendix A); numbers are the reference's sections.

### What the designer has overruled since

| Reference | What holds instead |
|---|---|
| 4: releasing Alt on a part "locks that limb on your HUD targeting doll" | Nothing is locked; the doll shows injuries only; the part targeted is the part looked at (section 4, "Targeting"; section 8) |
| 4: the doll goes green, yellow, red, flashing black | Green, yellow, orange, red with hatching, dark grey for a missing limb (built, slice B; D70) |
| 3: combat mode on C or F | F, the binding (section 3) |

### Layout and inventory (reference 1, 2, A)

| Element | Package | To settle |
|---|---|---|
| The middle of the Tab overlay is the character's sprite with the worn things around it where they sit on the body; an item on the cursor used on the sprite is used on oneself at the part clicked (A) | 4, with package T's zone map | which frame of the sprite is shown and whether it can be turned to show the back |
| Everything worn is shown beside the doll, as in Barotrauma and NEO Scavenger (A) | 4 | |
| Items in worn storage (backpack, pockets) are shown (A) | 4 | |
| An item picked from the hotbar or the inventory goes to the active hand (A) | 3, 4 | |
| Drag and drop works for everything while the inventory is open; drag travel under 300 px between hands, pockets and gear (2) | 4 | |
| The compact chat has six lines and its background fades out after 8 seconds idle (2) | 5 | |
| The centre of the view is never blurred; machine and loot panels close on taking damage or moving more than 1.5 m (7) | 4, 7 | closing on damage is new; the server decides the range |

### Hotbar, hands and firearms (reference 3, A)

| Element | Package | To settle |
|---|---|---|
| Only items in the pockets, the belt, suit storage or the ID and PDA slots can be bound to 1 to 9 (3) | 3 | suit storage is not in section 4's layout of the nine keys (belt 1 to 6, pockets 7 and 8, ID 9) |
| X swaps the active hand; the active hand has a high-contrast highlight (3) | 1 (done), 2 slice C | |
| With an aimable firearm in hand the right button held aims down the sights, and racking or chambering moves to R or Alt+right button; otherwise the right button is tg's secondary use or shove (3; A: "ads with guns?") | 10 | a question in the designer's notes: confirm before building; what the port's gun sight and scope already do (DV96) |

### Health, focus and first-person visibility (reference 4, A)

| Element | Package | To settle |
|---|---|---|
| Anatomical focus reaches 15 m and floats a tag by each part; it is mainly for non-humanoids, for combat and surgery (4, A) | 6 | xenomorphs and other mobs drawn as one picture need their regions drawn by hand (section 8) |
| Screen effects when hurt: a directional blood vignette for brute, a heat-wave border for burn, a frost creep for oxygen loss, muffled sound below 20% health (4, A) | 9 | what tg's own damage overlays already show in the port |
| Grip brackets at the screen's edges coloured by tg's grab tier: yellow passive, orange aggressive with sway, red chokehold with tunnel vision (4) | 8 | |
| A fireman carry tilts the carried player's camera 25 degrees with an over-the-shoulder view (4) | 8 | the horizon stays level (reference 7) |
| Hallucinations also fake chat: phantom speech pills from off screen (`fake_chat`) (4) | 8 | |
| A knockdown or stun never spins the camera: lowered eye height and a vignette instead; the horizon stays upright (7) | 8 | the camera's lying height exists (0.4 m) |

### Prompts and chat (reference 5, A)

| Element | Package | To settle |
|---|---|---|
| The prompt sits below and right of the crosshair and follows the active hand's item, the target and combat mode (5) | 2 slice A (done), T4b (done) | it refreshes on a look; a change of held item or combat mode alone does not refresh it yet |
| A tip for the item held in the hand; holding F1 expands a card of all its verbs (5, A) | 2 slice C | F1 is the camera mode; no list of an item's verbs is sent today |
| Local speech in 3D above heads inside a 100 degree field of view; size and opacity fall off from 0 to 10 m; whispers in italics within 2 m only (5, A) | 5 | what tg's runechat in the port already does |
| Channel tags by department with colours; own name, job title, department and codewords highlighted with pill badges and a soft tick (5, A: "see starlight ss14 system") | 5 | |
| Local speech has a white outline in the log and a one-click Local only tab (7) | 5 | |
| A held radio shows its text scrolling on the item's own screen as well as in the chat log; a radio in the world floats its frequency and text above it within 4 m (5, A) | 7 | |

### Item and machine interfaces (reference 6, A)

| Element | Package | To settle |
|---|---|---|
| Tier 1, on the held item's own screen, about a quarter: health analyzer, gas analyzer, T-ray scanner, Geiger counter; hold I to bring it close without freeing the cursor | 7 | I is the inventory key |
| Tier 2, one schema-driven renderer, about two thirds: APCs, air alarms, vending machines, autolathes, cryo and sleeper pods | 7 | the 298 translated tgui windows stay until a machine moves over |
| Tier 3, hand-built, about a tenth: PDA and crew manifest, ChemMaster, surgery console, telecomms, the nuclear bomb | 7 | |

### Asked for by the designer after the reference (2026-11-26)

| Element | Package | To settle |
|---|---|---|
| "The on screen tooltips should only show up if youre in range to interact with the object": the prompt beside the crosshair is shown only when the player can reach the thing looked at | 2 (a small slice of its own, after package T's slice T4c) | today the prompt shows at any distance (the hover has no range check). Who decides reach: the server knows tg's own rule (`CanReach`: adjacent, the held item's reach, over a table, not through a window) and can send no hints for a thing out of reach, with no change to the wire; what is shown for actions that work at a distance (a gun, a throw, examine); whether the thing's name still shows out of reach |

Settled by the designer (2026-11-26), the three points of the row above:
- In range is tg's own reach rule, the one a click obeys: the next tile, further with a long-reach item, across a
  table, not through a window or a wall. The server decides it and sends no hints for a thing out of reach.
- Out of reach nothing is shown for actions that work from afar either (a gun, a throw, examine), whatever is held.
- Out of reach the thing's name does not show: the crosshair is bare.

### The in-range prompt: built, committed with one known fault (2026-11-26)

Three fix rounds were used up with one fault open, and the change was parked in a stash on top of `a98d5ee`. The
contributor accepted that fault as a known limit on 2026-11-26 ("just accept it and make a note"). The stash was
then applied onto the head after package 2's slice D (no conflicts, every hunk as reviewed), verified and committed
as it is, with the fault recorded below and in DV601.

As built (server and client, no wire change; DV601 and protocol.md 14.2 have the full text):
- The server answers a hover of a thing its sender cannot reach (tg's `IsReachableBy` with the held item's reach)
  with a zero-length name and no hints; the client then shows a bare crosshair. Gated: every living mob acting from
  where it stands, whatever its state. Not gated: a ghost, the AI, a cyborg with no module out, the hunter while it
  leaps on click, a mob inside a mech or a locker; screen targets never. A ceiling cell goes bare outside the
  ceiling's range for every living mob.
- The client sends an unchanged hover again every half second (not while the pet-commands key is held), so the
  prompt follows walking into and out of reach and a change of held item or combat mode. A held key whose release
  was lost is released when the key is no longer down.
- On a real server nothing shows for a thing until the server has answered during the current look. Only the
  prompt goes bare: the radial keeps its title ("Ceiling" for a ceiling cell) and the dev tools their labels.
- Working out a hover's answer no longer writes to the player's chat (a mute in the server's chat sink); before, a
  cuffed player got "You are incapacitated at the moment!" on every look at a person beside them.

The open fault (reproduced by the stress test, `src/Server/ServerHost.World.cs` `NoteReach` and the router): reach is
decided for the body playing when the hover is applied, and the answer is built one tick later for the body playing
then. A player who changes body in that one tick (a ghost re-entering its corpse while looking at a far pen, or the
reverse) gets one answer by the old body's rule: a far thing named, or a ghost's name blank, for at most half a
second until the next repeat. The fix would be small (keep the mob with the flag and compare it in the router).

Playtested against the real server (evidence: the session's scratchpad, `reach` to `reach5` and `playtest-reach`): a
pen, a person and a vending machine are bare from four tiles and named from the next; walking up brings the prompt
within about a second and stepping back clears it; a pistol holder sees nothing at five tiles; the lines follow a
change of held item, a hand swap and combat mode without looking away; the pointer under Tab likewise, with the hand
slot's and the HUD's own tooltips unchanged; the radial is titled near and far, with only the fixed entries far, and
"Ceiling" on a far ceiling cell; with 300 and 400 ms of added latency no far name ever shows; a ghost sees names at
any distance; a cuffed player and a pAI in holoform get no chat line from looking; a mech pilot keeps the old
prompt; one's own body shows the prompt; sixty seconds on one person leave the chat and the server's log clean.
Probes: 200 hovers in a tick and 50 a second for ten seconds are held by the limiter (one in five applied) with the
answer always the last applied hover's; two minutes of repeats on five kinds of thing pile nothing up; the hover
agrees with a real click for windows on each edge, tables, grilles, diagonals and airlocks.

Not driven: throw mode confirmed on; a person seen through a window (the crosshair stops at the pane); a locker,
a cyborg and the AI in the client; a round restart with a hover pending; the lost key release beyond one console run.

Known limits, accepted: an answer in flight from an earlier look at the same thing counts for the current one; on a
slow connection every name lags by the round trip; while J is held the prompt does not follow reach; a change on
the thing itself (a door opening) refreshes the prompt only on a new look; the repeat is about 100 bytes a second
per player in a recording.

Seen while the devlog screenshots were staged (2026-11-26, not yet looked into): the prompt did not always match
where the crosshair is drawn ("Headpat" with the crosshair on the upper chest, "Hug" on the chin), and a handcuffed
player still got "Headpat" as the prompt on a person.

Verify on the parked tree: `C:/Users/Lucas/.claude/state/qa/logs/verify-AnomalyStation-20261126-064511-583150.log`
(213 server tests); on the head with the client tests added, 4098 server tests. Filter on the parked tree: `(FullyQualifiedName~HoverReach|FullyQualifiedName~HoverZone|FullyQualifiedName~KeyChips|FullyQualifiedName~PlaySessionTests|FullyQualifiedName~RadialTests|FullyQualifiedName~Ceiling|FullyQualifiedName~Screentip|FullyQualifiedName~ContextHint|FullyQualifiedName~ProtocolV14|FullyQualifiedName~FakeInventory|FullyQualifiedName~PetsGameplay)&Category!=Meta&FullyQualifiedName!~BuildInfo&FullyQualifiedName!~Bench`.

### T4c playtested (2026-11-26)

Against the real server, driven by the input script (evidence: the session's scratchpad, `playtest-aim-t4c` and
`playtest-aim-t4c2`): a conscious patient on an operating table and on a bed is drawn face up from all four sides and
a penlight finds the eyes and the mouth on the drawn face; a conscious person on the floor is back up with neither.
"Turn over" from the radial: the progress bar, "You start turning X over." and "You turn X over.", the body face up
from every side with eyes and mouth by penlight, and back again on a second turn; not offered on a standing person,
a corgi, an item or from two tiles; walking away, the target standing up and both hands full each stop it; standing
up forgets the side; a patient on furniture turns back up and face up again; a bystander's client gets the same
lines and draws the same side; a pistol shot at the same spot hit the right arm face up and the left arm after a
turn.

For test runs the input script has a new step, `SECONDS:radial=LABEL` (`docs/ai/specs/apps.md`): it holds the right
button, lets the radial open, aims at the entry with that label and releases, and logs what was picked or what was
offered. Use it for every later playtest of a radial entry.

Known, for later:
- The person being turned reads the third-person line ("X starts turning Y over."), not a "you" form.
- An actor with no arms, or with no free hand, is refused silently.
- Older than this package, seen in both playtests and confirmed on commit `77f593f`: a patient on the emergency
  medical bed (`/obj/structure/bed/medical/emergency`) is not drawn at all when seen from the north, and from the
  other sides lies with the head on the other end than on a bed or a table. Needs its own fix.
- A slow test, `BuckleGameplayTests.A_friend_is_buckled_to_a_chair_and_a_roller_bed_...`, fails at its line 178 at
  commit `77f593f` already (the builder's report; not looked into).

## 10. Package 3: the belt hotbar

### Design confirmed by the designer (2026-11-26)

The designer's notes: "quick swap hotbar config items in storage to specific slots 1-9. or like zomboid where you can
equip items to belt to be able to access quick. left and right of hotbar show hand slots, when you select item from
hotbar or inventory it goes to active hand." The reference's section 3: only what is in the pockets, the belt, suit
storage or the ID slot can be on a key; a press takes the item into the active hand and the same number puts it back.
Chosen by the designer from mockups on 2026-11-26: ten cells (tg's toolbelt and medical belt hold seven items, so the
wireframe's six belt keys would leave a full belt's seventh tool without a key), quick swap, and the worn slots under
Tab only.

The bar (human HUD only; other HUDs keep tg's layout and have no bar):
- Ten cells in one row between the hands, in three groups: 1 to 7 the worn belt's contents, 8 and 9 the left and the
  right pocket, 0 the ID slot. Cells are 48 square with 4 between them and 12 between the groups; the hands' row
  grows by the difference and keeps its rules (centred at the bottom, right of the chat window while it fits, the
  hint strip 6 above the active hand's box).
- A cell has the chips' fill and the inactive hand box's faint rim (as built in slice 3B; first written as "the chips' rim"), its key's cap in the top left corner (from the bindings, the swap cap's
  look), the item's picture in the middle and a stack's count as tg draws it. No names in the cells.
- A cell's states: empty (rim and a dim key cap only); holding an item; out (its item is in one of the player's
  hands, taken from this cell: the picture stays at about a third of its opacity, so the player sees where it goes
  back); refused (the rim blinks red for about a third of a second after a press that did nothing). With no belt
  worn the seven belt cells are empty cells at half opacity; the row never changes width.
- The pocket and ID cells are tg's own slot entries re-placed into the cells, so under Tab a click or a drag on
  them is tg's (the ID taken out of the PDA and so on). A belt cell is ours: under Tab a click on it is the key's
  press.
- tg's belt, back and suit-storage slots, which stood in the gap since slice C, are drawn only under Tab, in a
  short row just above the right end of the bar, clickable as now; where tg's bottom-right buttons reach over that place (a 5:4 screen at the largest UI scales) the row stands over the bar's left end, and it is left out where neither fits. Suit storage has no key.
- The hint strip now leads with the held item's name ("Wrench", then use, drop and throw with their keys), from the
  names the server sends for the items in the player's own hands. The hand boxes stay pictures only. Under Tab the
  hint strip is not drawn (the pointer has tooltips there), which leaves its place to the worn slots' row.
- A cell's item picture is drawn the way a hand box draws its item, centred in the cell.

Keys: 1 to 9 and 0 are the ten cells (number row and keypad). tg's action buttons move to Shift+1 to Shift+9, and the
numbers drawn on them follow the bindings. Saved keys stay as they were (package 1's rule).

What a press of cell N does. The server decides and does everything with tg's own procs (the belt's storage, the
slot's equip rules, put in hands), so tg's checks, sounds and delays apply; cuffed, stunned or dead, nothing happens:
1. The item of cell N is out, in either hand: it goes back into cell N.
2. The active hand is empty and cell N holds an item: the item comes into the active hand.
3. The active hand holds an item that is out from another cell M, and cell N holds an item: the held item goes
   back to M and N's item comes into the active hand (quick swap). If it cannot go back, nothing happens.
4. The active hand holds any item and cell N is empty (no item in it and none out from it): the item is stored in
   cell N if tg lets it in (the belt's storage rules; a pocket's or the ID slot's equip rules).
5. Anything else (an item not from the bar in hand and cell N holding an item; an empty hand and an empty cell): the
   press is refused and the cell blinks.

Which belt item is in which cell: when a belt is put on, its contents take cells 1 to 7 in tg's storage order. An
item keeps its cell while it is in the belt or, taken from the bar, in the player's hands. An item put into the belt
any other way takes the lowest free cell. An item that leaves both the belt and the hands (dropped, thrown, handed
over, used up) frees its cell, and so does an out item put somewhere else. Items beyond seven (a bandolier) have no
cell and are reached under Tab. Taking the belt off empties the seven cells. A pocket's or the ID slot's item taken
from the bar is out from its cell in the same way until it is put back or leaves the hands.

The wire: the server sends the player the ten cells' state and the names of the items in their own hands; the client
sends "press cell N", a number from 1 to 10 and nothing else. The server trusts only that number, checks the sender's
own mob on every press and counts presses in the action rate limit.

### Slices

| Slice | What |
|---|---|
| 3A | The wire and the server's rules; the client keeps the cells' state and sends presses from keys 1 to 0; the action buttons on Shift+1 to 9; gameplay tests on a real server. Nothing new is drawn yet. |
| 3B | The bar: cells, states, key caps, the blink, the worn slots' row under Tab, the item's name in the hint strip; layout tests; playtest and screenshots. |

Both are reviewed on their own; the package is one commit after the designer's sign-off (a wire change).

## 11. Chip pictures: fitted to their art (asked for by the designer, 2026-11-26)

"i just noticed the hunger sprite isnt centred or the righ size within its box, can you check this and the other
stutus effect sprites when showing on the hud". Checked in the real-server shots of slice E: a chip draws tg's whole
32-pixel square, and tg's art sits where tg wants it inside that square. The hunger bar with its burger measures 23
by 22 pixels, off the square's centre; the stamina bolt 16 by 29; tg's alert pictures about 30 high.

Settled design, one rule for every chip (alerts and status chips alike):
- A chip shows the entry's visible art: the smallest rectangle holding every opaque pixel of what the entry draws
  (its icon state with its overlays), not tg's whole square.
- That art is scaled uniformly, by whatever factor, so that its larger side fills the chip less 2 on each side, and
  centred in the chip both ways, at whole pixels. So every chip's picture reaches the same extent whatever tg's art
  measures, and nothing is ever drawn outside its chip. (First built with whole-number factors only, to keep the
  pixel art sharp: at the default 125% UI scale and at 150% that made most pictures smaller than before, since the
  HUD already resamples there, and changed nothing at 100%.)
- The rectangle is worked out from everything the entry shows over its animation (a blinking alert does not jump),
  with a pulsing outline filter counted at its full reach, and again only when the entry's icon state or overlays
  change.
- The chip's fill, rim, size, place and order do not change; a chip's maptext stays over the chip as now; the click
  and tooltip area stays the whole chip.
- Only chips: the doll, the health indicator, the mood face, the combat toggle and every entry at tg's own place
  keep tg's square.

Built in its own worktree and branch beside package 3, and merged before slice 3B.

## 12. Package 4: the Tab inventory (design, confirmed so far)

Confirmed by the designer from mockups on 2026-11-26. Not built; it starts after package 3. What exists today is in
the survey of the same day (cursor mode on Tab, tg's toggleable slot panel in a native `InventoryPanel`, one
`ContainerWindow` for tg's single active storage, the strip menu as a tgui window, drags as tg's MouseDrop, no paper
doll of the own sprite, "Things here" in the radial, quick-equip on E only).

- Tab (cursor mode) shows three columns over the view, with the hands and the hotbar staying at the bottom and the
  vitals at the right edge: the full chat at the left; in the middle the player's own character sprite with the worn
  slots laid out around it where they sit on the body; at the right the storage panels.
- The middle column is see-through: only the slot cells and the sprite are drawn, no panel behind them, so the room
  and anyone walking up stay in sight. The overlay closes by itself when the player takes damage (the reference's
  guardrail).
- An item used on the sprite is used on oneself at the body part clicked (the nine-zone map of aiming by look); an
  empty-hand click checks oneself.
- Tab opens the backpack: its contents show at the top right at once, with tg's own opening sound each time, as
  when it is opened by hand ("open with tab and tg sound").
- Everything carried is on screen at once and an item can be dragged from any of it to any other ("the tab
  inventory shoud show belt and pockets and suit storage ect too so you can move items between all the storage"),
  each thing shown once: the belt (1 to 7), the pockets (8, 9) and the ID slot (0) are the bar at the bottom, which
  becomes a drag target; suit storage is a slot beside the character; the right column holds the backpack (with its
  count of used and free room), then any other storage worn (a bandolier's items beyond seven, a box in a pocket),
  then the other container or the person being stripped, then the items lying nearby.
- To settle with the designer before building: the right column when several panels do not fit; what the strip
  target's panel shows and where its timers go; Shift+click transfer; the keys (E world interact, quick-equip to
  belt and bag, a key for the backpack); what closes the other container's panel (distance).
- Server side, to design with the builder: tg shows one storage at a time (`active_storage`), so the contents of
  every worn storage at once, and a second container beside the backpack, need the server to send them (the hotbar
  message of package 3 already carries the belt's); every move still goes through tg's storage and equip procs.

Added by the designer the same day: "the strip should show both people's inventories side by side with a search
mechanic like tarkov where it takes time to reveal each item in storagae, less time for belds and pockets. need to
select the storage to search it or have (search) prompt over the hidden items in each. shift click is normally the
inspect key on ss14. id like a keybind for click transfer, maybe cntrl click. but clicka nd drag should also work. e
is fine, can alwyas be rebound in the settings so make the interact binding seperate to the quick equip."

- Stripping: the other person's inventory is shown beside the player's own, laid out the same way (their sprite
  with the worn slots around it, their storages under it). What is inside their storages is hidden until searched:
  a hidden storage shows a "Search" prompt; choosing it reveals its items one at a time, each taking time, less for
  a belt and pockets than for a bag. The times, what the searched person is told and what interrupts a search are
  to be settled. THIS IS A MECHANIC tg DOES NOT HAVE (in tg a worn bag is taken off before it can be looked into,
  and a pocket's item is unknown until it is stripped), so it needs the owner's agreement before it is built; the
  contributor raises it.
- Shift+click stays examine. A separate, rebindable key action moves an item at once between the hand and the
  open panels, by default Ctrl+click; dragging works as well. In the inventory this takes Ctrl+click from tg's own
  Ctrl-click actions on a carried item (a PDA's pen, for one), which then need another way in.
- E is world interact, its own binding, separate from quick equip (which keeps a binding of its own).

Settled further by the designer (2026-11-26, later): "we can also use alt click for the alternative item
interactions like in ss14. please review the ss14 bindings in detail. for right column, scolling works. for walking
away, your plan also works. items revealed should stay revealed if you leave and open storage again unless someone
else moves them. maybe have their sprite and storages visible but also all our storages visible on the left, but
not hte players sprite. can also use scrolling for our storages if there are too many."

- The right column scrolls when its panels do not fit. The other container's or the strip target's panel closes by
  tg's reach rule (when the player can no longer reach it).
- The strip view: the chat stays at the left; the middle column shows all of the player's own storages (the
  backpack and any other worn storage, scrolling when there are too many) and not the player's own sprite; the
  right column shows the target's sprite with the worn slots round it and the target's storages with their "Search"
  prompts. The bar and the hands stay at the bottom.
- An item revealed by a search stays revealed for that searcher when the view is closed and opened again, until
  someone else moves it.
- Alt+click is tg's alt-click (the alternative interaction), in the world and on an item in the inventory, as
  Space Station 14's `AltActivateItemInWorld`. Ctrl+click in the inventory is the quick transfer; tg's own
  Ctrl-click actions on a carried item go to the right-click wheel there.
- Space Station 14's bindings were read in full (`Resources/keybinds.yml` of the fork checkout, 2026-11-26). Those
  this package takes, to confirm with the designer: E `ActivateItemInWorld` (world interact, its own action);
  Alt+left click and Alt+E `AltActivateItemInWorld`; Alt+Z `AltActivateItemInHand`; Shift+B, Shift+E, Shift+G,
  Shift+H, Shift+F `SmartEquipBackpack`, `Belt`, `Pocket1`, `Pocket2`, `SuitStorage` (tg's quick-equip keybindings
  for the bag, belt, pockets and suit storage); V `OpenBackpack` and Shift+V `OpenBelt`; Shift+middle click
  `Point`; comma `FocusWhisperChatWindow`. Space Station 14 has no quick-transfer key and no key for tg's plain
  quick equip, which needs a new default once E is world interact. Kept as ours where Space Station 14 differs:
  Tab (theirs cycles chat channels), F combat mode (theirs flips an object), a tap of Shift for walk (theirs is
  held), 1 to 0 for the belt bar with the action buttons on Shift+digits (theirs: two action bars). Alt held for
  anatomical focus (package 6, from the wireframes) would now clash with Alt+click and needs another key.

Keys confirmed by the designer (2026-11-26: "your key suggestions are fine, for anotomical focus, maybe `"): the
Space Station 14 bindings listed above are taken as listed; tg's plain quick equip moves to R; the quick transfer is
Ctrl+click in the inventory. Anatomical focus (package 6): the backtick was withdrawn (it is the dev console, as in
Space Station 14: "dont use `"); chosen from four candidates: the middle mouse button, held ("do middle mouse"). The
middle click's second binding for swapping hands goes then; X stays.

## 13. The chat window in first person (asked for by the designer, 2026-11-27)

"we imght have to move the chat too to make the hotbar work, and maybe change it ui so its less intusive", then,
offered a compact text feed instead of the window: "keep the window but higher so the bar can fit under it, and also
make it more transparent. maybe the box with the text input and buttons only appears when you go to type".

Settled design (client only; built as its own slice right after the bar, shown to the designer with it):
- The chat window stays a window, with its title bar, its tabs and its log. Its default place in the round is at
  the left, with its bottom edge 8 above whatever of the bottom row stands under it (the hands' row; the hint strip
  where the window reaches over it), so the bar is never under it. The hands' row is always centred at the bottom
  and no longer moves aside for the chat (slice 3B drops that rule). A place or size the player has given the window
  is kept as theirs.
- More see-through: while the player is not typing the window's backing is drawn at about a third of its opacity
  (title bar and tabs with it) and the log's text keeps full strength with a dark outline so it reads over the room.
  While the player types, or the pointer is over the window under Tab, it is at its normal opacity.
- The row of chat buttons (Say, Whisper, Shout, Radio, Me, Emotes, OOC, Message) and the input row are hidden until
  the player goes to type: a chat key (T, the radio key, Me, OOC and the others) or a click in the window under Tab
  shows them with the input focused; they hide again when the input is sent, cancelled or loses focus. The log takes
  the room they leave.
- The lobby's chat is not changed.
- To tell the owner: the chat window's default place, opacity and hidden input change upstream's defaults (the chat
  panel opens on entering the round with its input shown).

## 14. The belt slot's own item on the bar (found by the designer, 2026-11-27)

"ive noticed the PDA isnt in the hotbar?" Checked in tg: a job's PDA is worn ON the belt slot by default
(`pda_slot = ITEM_SLOT_BELT`, `_job.dm`; 30 job outfits name it there; jobs with a tool belt carry the PDA in a
pocket, where cell 8 or 9 shows it). The bar as designed in section 10 shows only what is INSIDE a belt that is a
storage, so with a PDA on the belt slot the seven belt cells were empty and faded and the PDA had no cell. A gap in
the design, not a fault of the build.

Settled design, added to section 10's rules:
- When the belt slot holds an item that is not a storage (a PDA, a single tool), that item is cell 1: it shows in
  cell 1 at full strength, cells 2 to 7 are empty at half opacity. A press of 1 takes it into the active hand
  (rule 2), a second press puts it back on the belt slot (rule 1), a bar item in hand swaps with it (rule 3); it is
  "out" from cell 1 while held, as a pocket's item is from its cell.
- With nothing on the belt slot, a press of 1 with an item in the active hand puts that item on the belt slot if
  tg's equip rules allow it there (rule 4): a PDA goes on; a tool belt goes on too, and its contents then take the
  cells as section 10 says. Cells 2 to 7 refuse while there is no belt storage.
- When what is on the belt slot changes kind (a belt put on while the PDA is in hand), the cells are given out
  afresh, as when a belt is changed.

WITHDRAWN by the designer the same day ("i tihnk leave the bar as it is"): section 14's cell for the belt slot's own
item is not built; the bar stays as section 10 has it, and a PDA worn on the belt slot has no cell.

## 15. The actions menu (asked for by the designer, 2026-11-27)

"we need an action wheel like the k menu in ss14 which lets you assign actions to numbers, but for us we would need
it to be assignable to the shift+number buttons. id also like the actions useable from the action menu itself."
Space Station 14's `OpenAbilitiesMenu` is on K.

Confirmed by the designer from mockups:
- K opens the actions menu, as in Space Station 14. The deathmatch scoreboard, which was on K (held), moves to
  another key (Caps Lock held is free) and stays rebindable.
- In first person the top left shows only a row of nine slots, each with its Shift+number key cap and the action
  assigned to it; tg's own list of every action button is no longer drawn there. A new action takes the first free
  slot by itself, so nothing has to be set up.
- The menu is a window with the cursor free: the nine slots at its top and, under them, every action the player has
  (tg's action buttons: its picture and its name). A click on an action uses it; a drag onto a slot assigns it; a
  right click on a slot clears it.
To design before building (its own package, after package 3 and the chat window): where the slot assignments are
kept (per character or per player, across rounds or not); what a slot shows when its action is gone (an item put
away, a spell lost); tg's own states on a button (a cooldown, an active toggle, a count) in a slot and in the menu;
tg's action palette and its drag-to-reorder, which this replaces on the human HUD; other HUDs (a cyborg's module
buttons, an alien's abilities, a ghost's): the same menu or tg's layout.

Settled by the designer the same day: "dont need to remember slots, after the action is gone the slot should be
hidden, like ss14 so it only shows when youre dragging items so you can put them into the slots. toggles should
heighlight the square, cooldowns should have a wheel that slowly reduces as the cooldown ticks, like ss14 and counts
can have a number overlayed or in teh botton corner. cyborn alien and ghost should get same menu, as long as it still
works".
- Slot assignments are not remembered: they last while the player has the actions and start again with the
  automatic first-free-slot filling.
- An empty slot is not drawn. A slot whose action is gone becomes empty and disappears; the empty slots show only
  while an action is being dragged, as places to drop it, as in Space Station 14.
- A toggle that is on highlights its square. A cooldown is a wheel over the square that shrinks as the cooldown
  runs down. A count is a number in the square's bottom corner. In a slot and in the menu alike.
- A cyborg, an alien and a ghost get the same slots and the same menu, provided their actions still work through
  it; where a HUD's actions cannot be driven this way it keeps tg's layout and that is reported.

Order (the designer, 2026-11-27: "do tab inventory before actions menu unless it needs to be that way round"):
package 3 and the chat window, then the Tab inventory (section 12), then the actions menu (section 15). Nothing in
the actions menu has to come first; it can reuse the Tab inventory's dragging between panels.

The search mechanic of section 12 (looking into a bag someone still wears, item by item, over time) is agreed by the
lead developer, as the contributor reports on 2026-11-27 ("it is agreed"). It is built with the Tab inventory and
recorded as a deviation from tg when it is; `DECISIONS.md` is the owner's to update.

Section 13 as built (2026-11-27), additions and what to tell the owner:
- The plain chat lines (drawn when the window is closed) are also lifted clear of the bottom row: where their block
  would reach over the hands' row or the hint strip its bottom is 8 above the top of what stands under it (at 200%
  UI scale the lines ran into the hint strip's left end).
- The window and the lines keep clear of the largest place the hint strip can take (the row's width, full height),
  not the strip as drawn at the moment, so that neither moves when the held item changes.
- Owner decisions this departs from in the round (the lobby is unchanged), for the contributor to raise: D292 (a
  sent message kept the input focused for the next one: now the input lets go and the rows hide); D290 (window
  backgrounds see-through by the Settings slider with text and controls solid: the chat window's backing is now
  also multiplied by 0.35 at rest, and its title bar's text, its X and its tab labels fade with it); D259 and D247
  (the action row and the input row always shown: now only while typing); and the layout of 2026-10-30 that put
  the chat window at the bottom left under the inventory (now above the hands' row).

### Package 4 in slices

| Slice | What |
|---|---|
| 4A | Client only: under Tab the chat window as the left column, the character's sprite with the worn slots round it in the middle, an item used on the sprite used on oneself at the part clicked, the overlay closing on damage. |
| 4B | Server and client: Tab opens the backpack with tg's sound; the contents of every worn storage shown at once in the right column, with names and room; dragging between everything; Ctrl+click transfer. |
| 4C | The other container's panel beside the backpack (closing by tg's reach rule) and the nearby items' strip. |
| 4D | The strip view: the target's sprite, slots and storages at the right, the player's own storages in the middle, the search that reveals a storage's items one at a time (agreed by the lead developer). |
| 4E | The keys of section 12 taken from Space Station 14: E world interact, R quick equip, the smart-equip keys, V and Shift+V, Alt+click and Alt+Z, Shift+middle click to point, comma to whisper. |

### Slice 4A: settled design (the worn slots' arrangement chosen by the designer from two mockups, 2026-11-27)

Human HUD only, in cursor mode (Tab; the inventory key I does the same as Tab). Other HUDs keep what they have.
- The chat window, when it is open, is docked as the left column while Tab is on: from the screen's left edge to
  28% of its width (`UiTokens`' Tab share), from just under the action buttons at the top left down to 8 above the
  bottom row, at its normal opacity. When Tab goes off it returns to where it was (its default place or the
  player's own). A chat window the player has closed stays closed.
- The middle column (28% to 64% of the width) holds the character panel, see-through: no backing, only the cells
  and the sprite. The player's own sprite, facing the viewer, as the game draws the body (clothes and all), at a
  whole-number scale (6 at the reference size, stepping down where the panel does not fit the height). Round it,
  the worn slots as cells of the bar's size and look, by body position: above the sprite a row of ears, head, eyes,
  under it the mask, under it the neck; left of the sprite, top to bottom, suit, uniform, gloves; right of it, top
  to bottom, back, suit storage, belt; under it, shoes. 4 between cells, 12 between the sprite and the side
  columns. The pockets and the ID slot are not repeated: they are cells 8, 9 and 0 of the bar.
- A cell is tg's own slot entry re-placed (as the bar's pocket and ID cells are), so a click, a drag and a tooltip on
  it are tg's: a click with an item in hand puts it on, an empty-hand click takes the thing off. An empty slot
  shows tg's own slot picture.
- The sprite takes clicks as the health doll does: with an item in the active hand a click on a body part uses the
  item on oneself at that part (the nine zones of aiming by look, from the sprite's own pixels); an empty-hand
  click checks oneself; Ctrl+click grasps the limb clicked. The pointer's prompt names the part under it.
- The worn slots are available under Tab without the player doing anything first (tg's expanded inventory is kept
  open for the human HUD; in first person its entries are not drawn and not clickable, as now). The old native
  inventory panel with its outline doll is gone, and so is the short row of belt, back and suit storage over the
  bar (slice 3B): those three are in the character panel.
- The overlay closes by itself when the player takes damage: cursor mode turns off, unless the player is typing in
  a text field or a window that needs the pointer is open.
- Nothing of this is drawn in first person.
