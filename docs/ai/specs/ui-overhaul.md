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
2. **Live HUD** (wireframes section 2, without the hotbar): `UiTokens`, native hands, health doll with target zone,
   combat toggle, alert chips at the top left, ping and FPS at the top right, context prompts with key chips beside the
   crosshair, the held-item card. Client only: it re-places and re-draws the screen entries the server already sends.
3. **Belt hotbar**: keys 1 to 6 for the worn belt's contents, 7 to 9 for the pockets and the ID slot; a press takes the
   item into the active hand and a second press puts it back. Needs a typed message and server checks.
4. **Tab overlay** (wireframes section 3): three columns, chat at the left, doll and gear in the middle, own storage
   over the external container or strip target at the right, the nearby-items strip, Shift+left click transfer, strip
   timers inside the slots, world interact on E.
5. **Chat** (wireframes sections 2 and 7): the palette and badge shapes, keyword pills with a tick sound, direction
   pills for speakers out of view, department tabs.
6. **Anatomical focus** (wireframes section 4): hold Alt, hover a body part, release to lock the target zone.
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
  a `ContextHintsMessage` from the player's bindings, at most three), `LiveHud` (which widget a screen entry is,
  by hand index or icon state and never by position; the selected target zone from `zone_sel`'s overlay; the layout
  of every box from `HudMetrics`). Nodes in `Hud/`: a new `LiveHudView` drawn under tg's pictures inside
  `HudLayer`'s root, and changes to `HudLayer`, `ChatLines` and `StatsCorner`.
- Layout: a gap for the nine-slot hotbar (9 x 48 + 8 x 4) at the bottom centre with the hands either side (active
  72, other 56, a 2 px border on the active one) and tg's bottom slots moved into the gap until package 3; the doll
  panel at the middle right, left of tg's right column, the combat pill under it; the held-item card at the bottom
  right above tg's two-row menu; alert chips in a row at the top, right of the action buttons; one "Ping | FPS" chip
  at the top right.
- The doll: one standard ramp when no colourblind palette is chosen (level 0 green `#8FD694`, 1 to 2 yellow
  `#FFE156`, 3 to 4 orange `#FFA62B`, 5 red `#FF4D4D`, missing dark grey, disabled red); the hatching keeps all
  levels apart (D70). The target zone is outlined in white (amber would vanish on an orange limb); groin, eyes and
  mouth are named in the header and outlined on the chest or head, since tg's doll has six zones.
- Every key shown comes from the bindings. `I18n.T` takes key chips and server labels as arguments only, never as
  the template (`[]` is a slot in it).
- The fake server gains a `zone_sel`, a combat toggle, one alert and hand indices, for tests and screenshots; a new
  `ANOMALY_SHOT=livehud` mode is added to `godot/tools/interaction-shots.sh`. "Before" shots are taken first.

### Slices, each built, verified and reviewed on its own

| Slice | What | Tests (classes under `tests/Anomaly.Server.Tests`, with `&Category!=Meta`) |
|---|---|---|
| A | `UiTokens`, `KeyChips`, prompts beside the crosshair, the ping and FPS chip | `Client.Play.KeyChipsTests`, `Client.Play.UiTokensTests`, `Client.Play.PlayOptionsTests` |
| B | Doll colours and panel, target zone, combat pill, the fake server's `zone_sel` and combat toggle | `Client.Play.HudTests`, `Client.Play.LiveHudTests`, `Client.Interaction.FakeInventoryTests` |
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
| `TARGET: HEAD` on the doll | 2 | |
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
| Each body part of the looked-at mob outlined: cyan hovered, amber locked, red dashed severed or damaged | 6 | the mobs are 2D sprites today; there are no part meshes |
| A label per part with what hitting it does | 6 | where that text comes from |
| Header with the current lock; releasing Alt on a part locks it; the doll's target follows; attacks, shots and surgery use it | 6 | |

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
