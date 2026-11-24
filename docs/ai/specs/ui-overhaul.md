# UI overhaul for first person (ROADMAP stage 11)

The interface is reworked to fit first-person play. The agreed design is the wireframe document
`docs/ai/specs/ui-overhaul-wireframes.txt` (agreed on the project's Discord, as reported by the contributor doing this
work, 2026-11-23). This file turns it into work packages. Where the two disagree, the wireframes win for layout and
behaviour, and this file wins for keys (section 3).

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
- Where the legs overlap seen from the side, the far leg wins (slice T2).
- The server's mirror for shots (`ShotGeometry.Cross`) does not undo the root's `pixel_w`/`pixel_z` either, so a shot
  at a person on a stool or an operating table reads its part several pixels off (slice T2, with the legs).
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
