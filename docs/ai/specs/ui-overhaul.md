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

## 3. Keys

Rule: an action that SS14 also has gets SS14's default key, taken from `Resources/keybinds.yml` of
space-wizards/space-station-14. An action SS14 does not have keeps its key unless SS14 uses that key for something this
game also has. A key the wireframes name for a new feature (Tab, 1 to 9, Alt) is used when SS14 does not claim it for
an existing function. Saved options files keep the keys their player had.

| Action | Was | SS14 function | Default now |
|---|---|---|---|
| Move | W A S D, arrows | MoveUp, MoveLeft, MoveDown, MoveRight | unchanged |
| Walk instead of run | C (toggle) | Walk: hold Shift | hold Shift |
| Lie down, get up | U | ToggleKnockdown: C | C |
| Combat mode | F | none (an action button) | F (the wireframes' C is SS14's lie down) |
| Left, right click | mouse | Use, UseSecondary | unchanged |
| Drop | Q | Drop: Q | unchanged |
| Throw | R | ThrowItemInHand: Ctrl+Q | Ctrl+Q |
| Swap hands | X, middle click | SwapHands: X | X (middle click stays as a second binding) |
| Use held item | Z | ActivateItemInHand: Z | unchanged |
| Examine | V, Shift+left click | ExamineEntity: Shift+left click | Shift+left click only |
| Pull | Ctrl+left click | TryPullObject: Ctrl+left click | Ctrl+left click only |
| Stop pulling | H or Delete | ReleasePulledObject: H | H |
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
| Resist, pray, give, push to talk, flashlight, scoreboard, levels, the other function keys | B, P, G, N, L, K, Space and Ctrl, F1 F2 F5 F9 F10, \ | none | unchanged |

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
- Walk is held, as in SS14: running is the default, holding the key walks, releasing it runs again. The server keeps
  tg's move intent; the client sends tg's toggle on press and on release only when the intent differs from what the key
  state asks for.
- Throw on Ctrl+Q does what R does today.
- No two default actions share a key. A test asserts it for the whole table, and asserts each row of section 3.
- `godot/default-options.cfg`, the controls table of `docs/running.md`, any on-screen key hint or help text that names
  a changed key, and the tests that name the old keys are updated.
- A saved options file with the old keys loads unchanged.

Not in this package: new actions (world interact, open backpack, belt hotbar), and any HUD change.

Tests: `tests/Anomaly.Server.Tests`, filter `FullyQualifiedName~PlayOptions` plus whichever classes cover the input
handling touched, with `&Category!=Meta`.
