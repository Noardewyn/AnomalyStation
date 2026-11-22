# Interaction: from the camera ray to tg's `ClickOn`

Status: v1, 2026-10-09 (the M4a contracts step, M4a.0). The contract M4a's packages build against (`docs/ai/specs/m4a-ownership.md`):
the click's target kinds and tg's `params` (sections 2-3, P6 on the server, P7 on the client), the client's pick (4, P7),
the native windows over tg's screen entries (5, P7), the hover, screentips and the radial (6, P1 and P6 and P7), the inert
guard (7, P1 with P2-P4 at their sites), the FakeServer's version 4 (8, P7) and the feedback until M9 (9). Decisions: D38,
D40, D44, D49, D53, D70, D103, D104, D107, D120, D121, D124-D133. Design basis: the M4a scout's report (`m4/scope-a.md`
2a-2j) and the lead's plan (`m4/plan-a.md`). Wire: `docs/ai/specs/protocol.md` section 14; types: `src/Protocol/MessagesV4.cs`.
A package that finds this spec wrong for the real code takes the most faithful option, reports it, and the lead amends
the spec.

## 1. Principle

- **The server runs tg's whole chain** (D86): BYOND's `client/Click` (tg's override with the click limiter), the atom's
  `Click`, `ClickOn` with its modifier branches, `UnarmedAttack`, `melee_attack_chain`, `base_item_interaction`,
  `tool_act`, `attack_self`, `throw_item`, the storage and closet procs, translated literally. Nothing about what a click
  does is decided on the client.
- **The client sends ids, never verdicts** (D40): what it clicked (an entity, a turf by its cell, a screen object), the
  button and the held modifiers, and tg's pixel within the icon. The server checks that the target exists and is visible
  (section 2); reach is tg's own (`Adjacent`, `IsReachableBy`, on tiles).
- **No new verbs.** The left button is tg's left click and the right tap tg's right click (D124); the radial only offers
  what tg's screentips say a click would do (D125), plus a few fixed client entries.
- **No hands prediction** (D38, D103: M10). The item reaches the hand when the server says so (about 150-200 ms on
  localhost); the client gives immediate local feedback (a highlight pulse, a crosshair tick, the view-model's raise) and
  never moves anything itself.

## 2. Click targets

A click (`ActionKind.Click`) or a hover (`ActionKind.Hover`) names one of three kinds of target (`TargetKind`):

| Kind | What the player hit | What the server resolves | D40 checks before tg runs |
|---|---|---|---|
| `Entity` (id) | a billboard (a mob, a moved item), a static prop of a chunk batch, a D113 piece (airlock, window, grille, table, firelock) | the object with that world id | it exists, it is an entity the replicator sends (`ReplicatedFields.IsEntity`: a movable on a turf, `invisibility <= 25`), it is not deleted, its turf is on the mob's level and within tg's view range (`get_dist(mob, target) <= 7`) |
| `Turf` (x, y, z) | a floor, a wall face, a ceiling cell | tg's `locate(x, y, z)` | inside the map, z is the mob's level, within view range |
| `Screen` (id) | a HUD entry or a cell of a native window in cursor mode, or the item drawn in a hand or slot entry (version 12) | the screen object with that id | it is in the session's `client.screen` now (tg's slot and hand objects and an open storage's cells and items all are), or it is a clickable `vis_contents` child of an object there (the item in a hand or slot) |

- A click on an item shown in the user's own storage window is a `Screen` click on the item's id: tg puts the item itself in
  `client.screen` (`storage.dm:1072`, `show_contents`), and tg's screen `Click` takes it from there.
- A click on a hand or slot entry follows BYOND's `vis_contents` rule (protocol version 12, protocol.md section 21): tg draws the held
  or worn item through the slot's `vis_contents` (`inventory_slot.dm:55`), and BYOND sends a click on one of the item's opaque pixels to
  the item itself unless it has `VIS_INHERIT_ID`. So a click on the item's pixel is a `Screen` click on the **item's** id (the entry lists
  it among its `VisChildren`; the server accepts it as a listed child of an object in `client.screen`, with the holder's `screen_loc`),
  and a click on the slot's own pixels is a `Screen` click on the slot, whose `Click` swaps hands or, on the active hand, forwards to the
  item (`screen_objects.dm:200-218, :291-310`). With the other hand active and empty, a click on a held toolbox is tg's `attack_hand` on
  it and opens its storage; with an item in the active hand it is `attackby`. A vis child of another session's screen object is refused.
- A version-3 click (kind `None` with a target id) is an `Entity` click (`ActionMessage.EffectiveTargetKind`), so the M3
  clients and bots keep working until they send kinds.
- A forged or stale target is dropped silently and counted (`Session.ClicksRejected`): a click that races a despawn is
  normal. A message that breaks the format is a strike (protocol.md 8).
- **Where BYOND's location and control come from.** For an entity, the target's turf and the map control (tg's
  `SKIN_MAPWINDOW_MAP`); for a turf, the turf itself; for a screen object, BYOND passes the screen object's `screen_loc` as
  the location text of a HUD click: P6 writes that rule from the BYOND reference into `ClientSeam.Click` (the screen case
  is new in M4a).

## 3. tg's `params`

The server builds BYOND's params text for `Click` and `ClickOn` (`ClientSeam.ClickParams`, P6), tg reads it with
`params2list` (`code/__DEFINES/_click.dm`):

| Key | When | Value |
|---|---|---|
| `left`, `right`, `middle` | the button | `1` |
| `shift`, `ctrl`, `alt` | each held modifier | `1` |
| `button` | always | `left`, `right` or `middle` |
| `icon-x`, `icon-y` (`ICON_X`, `ICON_Y`) | when the client sent them (`ActionMessage.IconX`, `IconY` > 0) | tg pixels 1-32 within the clicked icon |
| `screen-loc` (`SCREEN_LOC`) | a `Screen` target | the screen object's own `screen_loc`, filled by the server (never the client's text) |

- **`icon-x`/`icon-y`** are what BYOND gives: the pixel within the clicked atom's icon, x from the left and y from the
  bottom. For a billboard or a prop it is the picture pixel under the ray. For a floor cell or a table top it is the
  tile-relative point, east is x and north is y, times 32 and rounded up (1-32). tg reads them for table placement
  (`tables_racks.dm:393-396`), the zone selector and about 27 other files.
- BYOND's key order is P6's to copy from the BYOND reference; tg never depends on the order.
- `vis-x`, `vis-y` and `link` are not sent. `drag` (tg's `DRAG`: the button that drags) is in the params of a drag's MouseDrag and
  MouseDrop only (protocol version 17, section 11).

## 4. The client's pick (P7)

### 4.1 The world pick (`IWorldPicker.Pick`)

One engine-free function over the replica and three geometry adapters picks the nearest hit along the camera ray from the
eye (D120: 1.2 m above the floor), stopped by the first wall. The highlight, the crosshair, the radial and the click all use
it, so they agree.

| Surface (`PickSurface`) | Shape | Target |
|---|---|---|
| `Billboard` | the billboard's quad and its picture's opaque pixel (as `EntityLayer.TryPick`) | the entity |
| `StaticProp` | the prop's quad from the chunk batch's placement and the atlas raster's alpha; items at D121's half scale, lying on their support | the entity |
| `Piece` | the D113 piece's box: an airlock's panels, a window's slab, a grille, a table (its top is a box at `WorldGeometry.TableTop`), a firelock | the entity |
| `Floor` | the floor plane of the level (z = 0 of the level) | the turf |
| `Wall` | a wall face of `MapRenderer`'s wall grid (the Amanatides-Woo walk of `MapRenderer.RayDistance`) | the wall's turf |
| `Ceiling` | the ceiling plane over a cell | the turf under it |

- **Order.** The nearest hit wins. A table piece in front of its floor cell wins over the floor (dropping onto a table is a
  click on the table, tg's `table_place`). A piece or prop behind the first wall face is never hit.
- **Piles.** The nearest opaque pixel wins; the radial's "Things here" (section 6.3) lists the cell's other items.
- **Distance.** 8 m (4 tiles) for ordinary clicks; in throw mode the held item's `throw_range` (7 tiles, 14 m), and with no
  hit the target is the floor cell where the ray meets the floor, or the cell at that range along the view. An ordinary
  click with no hit within its 4 tiles takes the same pick over tg's view radius (7 tiles, 14 m, `WorldPicker.PickInView`;
  the space play test, 2026-10-15): BYOND's screen always has a turf under the mouse, so looking out into space (no
  ceiling, the floor far below the 4-tile ray) or down a long corridor a click still lands on a turf (a space turf over the
  starfield), and tg's ranged items get their target (the extinguisher sprays toward it and pushes a weightless user the
  other way). The 3D distance only chooses what is under the crosshair; whether it is in reach is tg's, on the server.
- **Particle effects** (`/obj/effect/particle_effect`: trails, water puffs, smoke) are never picked: tg makes them
  mouse-transparent (`effect_system.dm:13`, `effects_water.dm:7`).
- **`entitiesOnly`** leaves the cells out as hits (they still stop the ray): the hover of an empty hand.
- Tests (`tests/Anomaly.Server.Tests/Client/Interaction/`, P7): every surface, behind walls, the table over the floor, a pile,
  the throw-mode range, `icon-x`/`icon-y` for a billboard, a prop, a floor and a table top.

**As built (M4a P7, `docs/ai/specs/apps.md` 2.12).** `WorldPicker` is engine-free over `IPickWorld` (the map's walls and ceilings, `CollectStatic` per cell, `CollectMoving`); the Godot adapters are `WorldPickAdapter`, `MapRenderer.CollectStatic` and `EntityLayer.CollectPickShapes`. Differences from the table: a table is a box from the floor to its top (a ray at its side must not reach the floor under it); an open door's pick boxes are its two ends; floor decals (pipes, wires) are not picked; a wall decal is a thin slab on its face, not its picture's alpha. `WorldPicker.LastShape` is the shape the highlight draws around; `PickThrow` is the throw-mode pick.

### 4.2 `icon-x` and `icon-y`

`PickHit.IconX`/`IconY`: section 3's rule, computed from the hit point. A billboard's pixel is its picture's (left is 1,
bottom is 1); a prop lying flat (D122) uses its floor-aligned picture; a wall face gives the face's tile-relative point
(the horizontal coordinate along the face, the height as y).

### 4.3 The reach mirror (crosshair colour)

The client mirrors tg's tile reach for the crosshair only: green when the target's turf is adjacent to the own mob's turf
(tg's `Adjacent` without the border details), amber otherwise. It never stops a click: the server decides.

### 4.4 The HUD pick and cursor mode (`IHudPicker`)

- **Cursor mode** (Tab by default, D53/D70 remappable): the mouse is freed over the HUD and the windows, the camera stops
  turning, and a click on an entry is a `Screen` click on its id (`IHudPicker.PickScreen`: the topmost entry whose drawn
  picture has an opaque pixel at the point; a native window's cell before the HUD under it; within an entry, its listed
  `vis_contents` children first, topmost first, each on its own picture: a hit is a `Screen` click on the child, protocol.md
  section 21.4). Tab again recaptures the mouse (D282: Esc only opens and closes the menu). A click (not a drag) on the world in cursor mode is nothing; the Pull
  key (H; Ctrl until DV422, pulling 2026-10-15) ctrl-clicks the thing or turf under the pointer (the pointer's pick) as it does the one at the
  crosshair.
- Opening a storage shows its window without freeing the mouse; a click on the world still works in captured mode.
- tg's zone selector reads `icon-x`/`icon-y` of its click: the HUD pick gives them within the entry's 32 x 32 icon.

### 4.5 Mouse and keys (D124, D53, D70)

| Input | Sends | tg |
|---|---|---|
| left button | `Click`, `button=left` | tg's left click |
| right button, a tap (released within 0.25 s) | `Click`, `button=right` | tg's right click: weld shut (`try_to_weld_secondary`), the shove, open a bag (`COMSIG_ATOM_ATTACK_HAND_SECONDARY`) |
| right button held (0.25 s or more) | nothing until a choice | opens the radial (section 6.3) |
| middle button | `Click`, `button=middle` | tg's `MiddleClickOn`: `swap_hand()` |
| Shift, Ctrl, Alt held | the click's modifiers | `ShiftClickOn` (examine), `CtrlClickOn` (pulling, translated 2026-10-15: the living `base_click_ctrl`'s `grab`, which skips a non-mob, then the `pulled` verb and `start_pulling`; a ctrl-click on what one pulls does nothing, on a mob one pulls it is tg's aggressive grab, M9), `AltClickOn` (`click_alt`: open a storage, a toolbox's latches) |
| Q, E, X, Z, R, Space | `Keybinding` (down; Space also up) | tg's `drop_item`, `quick_equip`, `swap_hands`, `activate_inhand`, `toggle_throw_mode`, `hold_throw_mode` |
| H, Delete | `Keybinding` 9 (down, protocol version 20) | tg's `stop_pulling` |
| Tab | (client) | cursor mode |
| left button held and moved more than 4 pixels in cursor mode, released over a thing | `Drop` (version 17, section 11) | BYOND's MouseDrag and MouseDrop: tg's drag and drop |

- M2's `Help`/`Shove` bindings become `PrimaryClick`/`SecondaryClick` with an options migration (the right tap is still the
  shove on a mob with an empty hand); free-fly's Q and E apply only in that camera mode; F1-F10, U, C and F stay as they are.
- The keybinding codes are the frozen table `KeybindingCode` = `ClientSeam.KeybindingType` (protocol.md 14.1).

## 5. Native windows over tg's screen entries (D131, P7)

- **The server stays 100 % tg.** tg's `show_contents` puts a storage's `storage_interface` cells, its close button and the
  items themselves into `client.screen`, each with the `screen_loc` tg's `orient_storage` gives it; tg's inventory toggle
  shows or hides the slot objects of `HUD_GROUP_TOGGLEABLE_INVENTORY`. The private block already carries all of them.
- **The group byte** (`ScreenEntry.Group`, tg's `hud_used.screen_groups`) tells the client which entries a window takes:
  `HudGroup.Storage` entries go into the container window, `HudGroup.ToggleableInventory` entries into the inventory panel
  around the doll; every other entry is drawn at its `screen_loc` by the HUD layer as in M2. tg's hand slots stay at the
  bottom edge.
- **`IContainerView`** lays out one group's entries as a grid in tg's order and cells, from their `screen_loc`s
  (`ScreenLoc.Parse`), and gives every cell its role (`Slot`, `Item`, `Close`, `Other`) and its D70 label (the item's or the
  slot's name). It is a pure function of the entries and the replica's tables, tested from real server entries (the
  `HealthDollTests` pattern).
- **Clicks** always go to tg's screen ids: a click on an item takes it into the hand (tg's item `Click` in storage); a click on
  a cell with an item in hand inserts it (`screen_objects.dm:653-669`); the close cell is tg's `/atom/movable/screen/close`.
- **D70:** every cell keeps a text label; the active hand is marked by a shape (tg's `hand_active` overlay), never by colour
  alone.
- Lockers and crates need no window: tg's `open` dumps their contents on the turf, where they become entities.

**As built (P7).** `ContainerLayout` reads tg's real strings: the `storage_cell` entry (a range `"x:px,y:py to x2:px2,y2:py2"`, `spanning_screen_loc`) is the whole grid and one `Slot` cell (a click inserts the held item), `storage_close` is `Close`, the corners, row joins and `storage_up` are `Other`, every other entry of the group is an `Item` at the cell its `"x:px,y:py"` stands on (tile numbers are 1-based and a pixel offset counts: `"5:-16"` and `"4:16"` are the same pixel). The group of the inventory is laid out by position on the 32-pixel grid (an anchor's base is its edge or the middle, so `WEST`, `CENTER` and `EAST` entries keep their order). A label comes from the icon state because the screen entry carries no name (a word table for tg's slots, `Humanise` otherwise). The screen click on an item shown in the user's own storage is on the item's own id (tg puts the item itself in `client.screen`).

## 6. Hover, screentips and the radial (D125)

### 6.1 The flow

1. **Client (P7):** when the pick's target changes, it sends `Hover(target)` (rate-limited, `ActionLimits.Hover`), and
   nothing when nothing is under the crosshair (it clears its own hints).
2. **Host (P6):** at most one hover per session per tick (the last) runs `ClientSeam.Hover(client, atom)` in the command
   phase, with `usr` the client's mob: BYOND's `MouseEntered(location, control, params)`, tg's `/atom/MouseEntered`
   (`_atom.dm:862`), which stores the atom in `SSmouse_entered.hovers[client]`.
3. **tg (P1):** `SSmouse_entered`'s fire (`SS_TICKER`, every tick) runs `on_mouse_enter(client)` for each client's last
   hovered atom: tg's `COMSIG_ATOM_MOUSE_ENTERED`, then the screentip: the name (`used_name`), and, when the atom has
   `HAS_CONTEXTUAL_SCREENTIPS_1` or the held item `ITEM_HAS_CONTEXTUAL_SCREENTIPS`, the context list from
   `COMSIG_ATOM_REQUESTING_CONTEXT_FROM_ITEM` (tg's `add_context` overrides, through `register_context`) and
   `COMSIG_ITEM_REQUESTING_CONTEXT_FOR_TARGET` (`add_item_context`, the `contextual_screentip_*` elements).
4. **The seam:** where tg composes the maptext of `active_hud.screentip_text` (`_atom.dm:900-960`), P1's translation calls
   `ScreentipSeam.Publish(client, src, used_name, context)` instead (complete, C; a DV row: the structured message replaces
   tg's maptext composition). tg's checks before it stay: the screentip preference (`SCREENTIP_PREFERENCE_DISABLED` or
   `NO_SCREENTIPS_1`: `Publish` with a null name), `CONTEXT_ONLY` with no context (nothing shown: a null name).
5. **Host (P6):** its `IScreentipSink` turns the call into a `ContextHintsMessage` for that session (keys to
   `(button, modifiers)` by `ScreentipSeam.TryKey`, tg's order of lines, labels as tg's text).
   P1's translation keeps tg's checks and composition of the lines (so `SCREENTIP_PREFERENCE_CONTEXT_ONLY` sees tg's
   `extra_context`) and publishes `used_name` with the context list only when `CONTEXTUAL_SCREENTIP_SET` came back; it never
   writes `screentip_text` (its maptext stays empty, and `set_hover_maptext` is a deferred leaf). Without preferences (M9)
   the HUD's `screentips_enabled` is null, which tg treats as enabled. `build_context`'s cursor icons
   (`GLOB.screentip_context_icons`) are deferred leaves: the client draws the buttons.
6. **Client (P7):** shows the name and the LMB and RMB labels at the crosshair (tg's screentip line), and keeps the whole
   list for the radial.

### 6.2 What it is not

- No invented labels: a target tg gives no context for shows only its name. Wires and hacking (tg's `wirecutter_act` on an
  open panel reaches the wires UI, which M4 leaves out) produce no label, so the radial has no entry for them.
- The hints are advisory: the radial's choice sends the click it names, and the server runs tg's chain for that click.

### 6.3 The radial (P7)

Holding the right button opens a radial menu around the crosshair with:
- tg's context entries for the target with the held item (each one sends its click: button and modifiers);
- fixed entries: **Examine** (a Shift-left click), **Throw at** (throw mode on, then a left click on the target),
  **Swap hand** (the swap-hands keybinding), **Things here** (a client-side list of the cell's items from the replica, for
  picking one of a pile as the click's target), **Cancel**.
Releasing on an entry chooses it; releasing in the centre cancels.

M8y slice D (D230, DV139, protocol version 36): after tg's entries the server adds **Buckle**, **Unbuckle**, **Pull** and **Stop pulling**
(`RadialExtras`) on a thing next to the player, each only when tg's action is possible (a label tg gives is not repeated). A hint carries its
action (`ContextHintAction`): Buckle is a Drop of the own mob onto the thing (tg's drag of oneself, `user_buckle_mob`), Unbuckle the empty
hand's left click (`attack_hand`), Pull the ctrl-click, Stop pulling the stop-pulling keybinding. Opening the radial re-sends the hover, and
fresh hints that differ replace the open radial's entries.

M8z slice B (D238, DV151, protocol version 37): **Strip** is added the same way on a human next to the player (`RadialExtra.Strip`, when
`should_strip` and `can_perform_action` allow tg's drag of him onto oneself). Its hint's action is `ContextHintAction.DropOntoSelf`: choosing
it sends a Drop of him onto the own mob (the health doll in first person), which opens his strip window (apps.md, `StripMenu`).

Exosuits (D321, DV359): **Climb in** is added the same way on a sealed vehicle next to the player (an exosuit, the clown car) that has a free
seat he is not in (`RadialExtra.Enter`). Its hint's action is `ContextHintAction.DropSelf`, as Buckle's: choosing it sends tg's drag of the
own mob onto the vehicle (`/obj/vehicle/sealed/mouse_drop_receive`, `mob_try_enter`), and the vehicle's own checks say why it refuses.

## 7. The inert guard (D132)

**The problem.** An inert or base item (868 inert `/obj/item` types on Meta; the PDA and the headset are base types whose own
code is not translated) is a real instance of its generated class. A click on it dispatches by C# virtual calls to the
nearest translated ancestor and would silently run that ancestor's behaviour where tg runs the type's own override.

**The rule.** Items whose type overrides nothing in the interaction chain (most clothing, paper, folders, toys) pick up,
drop, throw, store and equip by tg's base item code. Anything type-specific is refused with a "not ported yet" message,
never run as the parent's behaviour.

**The pieces.**
- `InertSeam.GuardedProcs` (frozen, C): the DM names of the chain's procs the guard covers: `attack_hand`,
  `attack_hand_secondary`, `click_alt`, `click_alt_secondary`, `click_ctrl`, `click_ctrl_shift`, `attack_self`,
  `attack_self_secondary`, `melee_attack_chain`, `pre_attack`, `pre_attack_secondary`, `attack`, `attack_secondary`,
  `afterattack`, `attackby`, `attackby_secondary`, `interact_with_atom`, `interact_with_atom_secondary`,
  `ranged_interact_with_atom`, `ranged_interact_with_atom_secondary`, `item_interaction`, `item_interaction_secondary`,
  `tool_act`, `pickup`, `equipped`, `dropped`, `throw_impact`, `hitby`.
- The generated table `Generated/Types/UndeclaredOverrides.g.cs` (the types tool, P1; `docs/ai/specs/types-converter.md`
  section 10): every tg definition of a guarded proc at a type whose C# class does not declare it.
- `InertSeam.HasUndeclaredOverride(datum, proc)` (P1): true when tg defines the proc at the object's runtime type, or at an
  ancestor below the class whose C# method would run, and that definition is in the table.
- `InertSeam.MissingOverride(datum, proc, user)` (P1): the guard. When `HasUndeclaredOverride` holds, it sends tg's
  `balloon_alert(user, "not ported yet")`, writes one host log line per type and proc (as `Dm.Deferred`'s first call at a
  site does), counts the refusal in `DmWorldState.DeferredCount` and its sites, and returns true; the caller returns at once
  with the proc's "nothing happened" value.

**The sites** (each translator puts the guard at the top of the translated base definition it owns, before tg's first line):

| Site (the base definition) | Owner | Guards | What "nothing happened" is |
|---|---|---|---|
| `/atom/attack_hand`, `attack_hand_secondary`, `/atom/click_alt`, `click_alt_secondary`, `click_ctrl`, `click_ctrl_shift` | P1 | the target | null for `attack_hand` (tg's "not handled"); `SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN` for `attack_hand_secondary`; `CLICK_ACTION_BLOCKING` for the alt and ctrl clicks |
| `/obj/item/melee_attack_chain`, `attack_self`, `attack_self_secondary`, `pre_attack*`, `attack`, `attack_secondary`, `afterattack` | P1 | the item | the value that stops the chain: null for `melee_attack_chain`; TRUE for `attack_self`, `attack_self_secondary`, `pre_attack` and `attack` (tg's cancel); `SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN` for `pre_attack_secondary` and `attack_secondary`; `afterattack` returns at once |
| `/atom/attackby`, `attackby_secondary` (the target); `/atom/base_item_interaction` (the target's `tool_act` when not in combat mode, and `item_interaction` or `item_interaction_secondary` by the click), `/obj/item/interact_with_atom*`, `ranged_interact_with_atom*` (the tool) | P1 | the target, then the tool | TRUE for `attackby` and `SECONDARY_ATTACK_CANCEL_ATTACK_CHAIN` for `attackby_secondary` (tg's "handled, stop"); `ITEM_INTERACT_BLOCKING` for the interactions |
| `/mob/put_in_hands` and `/obj/item/pickup` (for `pickup`, `equipped` and `dropped`: an item whose own equip or drop code is missing is not picked up at all), `/mob/equip_to_slot` | P2 | the item | FALSE (not picked up, not equipped) |
| `/atom/movable/throw_impact`, `/atom/hitby` | P2 | the thrown item, the hit atom | the item lands with no effect of its own (tg's base impact only) |
| `atom_storage`'s insert and open hooks (`attempt_insert`, `open_storage`) on an item whose storage procs are overridden | P3 | the storage's parent | FALSE |

- **P1's implementation (M4a P1).** `HasUndeclaredOverride` walks the runtime class up to the first class that declares
  a method of the slot's DM name (`proc` or `proc__proc`; a generated live-partial stub is a declaration), asking the
  generated table at each class; it allocates nothing for a class that declares the method. `MissingOverride` writes its
  log line and its count through `Dm.Deferred` with the site `inert guard: <type path> <proc>` (so tests and the soak read
  `DmWorldState.DeferredSites` and `DeferredCount`), then sends `balloon_alert(user, "not ported yet")` when there is a user.
  A guard at a base definition sees only calls that reach the base; a translated override in between that does not call its
  parent hides an untranslated override below it from the base's guard, which is why `base_item_interaction` checks the
  target's `tool_act` and `item_interaction` before dispatching them.
- **Rows at base types.** The table has rows at general types whose tg definitions are not declared yet: `/atom/movable`
  `attack_hand` (buckling.dm) and `hitby`, `/turf` `attack_hand` and `item_interaction` (turf.dm), `/obj/item`
  `attack_hand`, `click_ctrl`, `hitby` and `throw_impact` (items.dm, P2), `/obj/machinery` `attackby` and
  `attackby_secondary`, `/obj/effect` `attackby`, `/obj` `hitby`, `/mob/living` `click_ctrl_shift` and `hitby`,
  `/mob/living/carbon` `hitby` and `throw_impact`. Until their owners declare them, a click that reaches one of those slots
  on such a type is refused (for example an empty hand on a floor, or an item on a floor); this is the guard working as
  DV27 says, and each is reported to its owner.
- Examine is not guarded: an inert type's examine shows tg's base lines (name, description, the weight line), which is
  what tg would show minus the type's own lines.
- The DV row (`docs/ai/DEVIATIONS.md`, the inert guard) records the refusal as a deviation; T1 asserts that every Meta item
  type either passes the guard for pick-up or is refused with the log line, never a runtime; I1 counts refusals on a soak.

## 8. The FakeServer, version 4 (P7)

`FakeServerOptions.Inventory` (off by default) gives the client's interaction code a scripted server: tg's hand slots, swap,
drop and throw in `HudGroup.Static`, the 15 slots in `HudGroup.ToggleableInventory`, an open storage's cells, close button
and items in `HudGroup.Storage`, with tg's `screen_loc`s, and the scripted toolbox, welder and airlock of its doc comment
(`godot/scripts/Shared/FakeServer.cs`). It sends what the real server sends for the same state (entries, groups, the
`Thrown` flag, private images, `ContextHints`), so P7's code is tested against the frozen types before P6's codec and the
tg packages land. P7 owns both sides; the contract is the types and this section.

**As built (P7).** The throw button is in `HudGroup.Hotkeys` (tg's `HUD_GROUP_HOTKEYS`; this section said Static) and the hands, drop and swap are in `Static`. The extra option `FakeServerOptions.ThrowTicksPerTile` slows the flight for the shots. The fake's clicks: an entity click on a thing in reach takes it into the active hand (a hand with an item says "Your hand is full"), a right click on a toolbox opens it, a click on a hand box swaps to it or, on the active one, uses the item (a toolbox opens, a welding tool lights), a click on a slot equips or takes, a click on the storage's cells inserts the held item, on an item takes it, on `storage_close` closes; a click out of reach sends a `balloon` text ("Too far away!"). Version 12: the hand and slot entries list their item as a child (`VisChildren`), and a screen click on the item is tg's item `Click`: the active hand's own item is used, with the active hand empty a toolbox opens and another item goes to the active hand, with an item in the active hand it goes into the toolbox.

## 9. Feedback until M9 (D128)

- **Chat lines:** the last 6 lines of tg's `to_chat` (`ServerText`) fade in at the lower left. Since protocol version 24 (D154) a
  line's BYOND links (`<a href='byond://?...'>`) are drawn as links, and a left click on one in cursor mode sends it to the server as
  BYOND's `Topic` (protocol.md section 33): the self-check's line of an embedded object rips it out.
- **Balloon alerts:** tg's `balloon_alert` (P1 translates `balloon_alert.dm`) reaches the message sink's `Balloon(viewer,
  atom, text)` (presentation-seam.md 3); the host sends it as a `ServerText` of type `balloon` to the viewer, and the client
  shows it under the crosshair.
- **The progress bar:** tg's `/datum/progressbar` image in the user's `client.images` reaches the client as a private image
  (`ImageEntry`, attached to the target), drawn over the target, or under the crosshair when the target is off screen.
- **`visible_message`'s self message** is translated (P2, `mob.dm`); its viewers stay M9.
- **Examine** (minimal, D128): `/mob/verb/examinate`'s core (P2), `/atom/examine` with the `COMSIG_ATOM_EXAMINE` list, the
  name and description, `/obj/item/examine`'s weight line and the types' own lines where translated (the welder's fuel),
  sent as `to_chat` (P1). Identity, `examine_more` and the examine panel are M9.

## 10. What M4a does not do

Hands, doors and storage prediction (M10, D38); drag and drop (M9; since protocol version 17 ported, section 11); pulling (Ctrl-click, M9); the wires UI and hacking
(M5/M7); construction and deconstruction (M5); mobs thrown (grabs M9, space wind M4b); 3D hands (D104, D41).

## 11. Drag and drop (protocol version 17)

tg's `code/_onclick/drag_drop.dm` is translated whole (`src/Core/Tg/_onclick/drag_drop.cs`): `/atom/MouseDrop` (the drag as a click,
`client/check_drag_proximity` and `is_drag_clickable` with `screen_loc_to_offset`, `__HELPERS/screen_objs.cs`, and `getviewsize`/`view_to_pixels`,
`__HELPERS/view.cs`), `base_mouse_drop_handler` (the two signals, reach, usability), the roots `mouse_drop_dragged` and `mouse_drop_receive`
(with D132's guard: an inert type's own handler is refused with tg's "not ported yet", `InertSeam.GuardedProcs`), the client's `MouseDrag`
and `MouseDrop` bookkeeping, and `MouseDown`, `MouseUp`, `CanMobAutoclick`, `CanItemAutoclick`, `IsAutoclickable` (the host sends no
MouseDown or MouseUp: the autoclick of full-auto weapons arrives with them, `docs/ai/specs/not-translated.md`). BYOND's own halves are the engine
part's (`MouseDrop__d0`, `MouseDrag__d0`, `/atom/MouseDrag`'s root). The client sends a drop as protocol.md section 26 says; the server runs
tg's client procs through `ClientSeam.Drop` (DV45: one MouseDrag at the drop, `drag_start` dated back by the drag's length).

The receivers translated with it:

| Drag | Handler (tg) | What happens |
|---|---|---|
| a worn or held storage (a backpack, a box) onto a hand slot | `datum/storage/on_mousedrop_onto` (`storage.dm:801`) | `putItemFromInventoryInHandIfPossible`: off the back (or the other hand) into that hand |
| a storage onto its wearer | the same, `over_object == user` | `open_storage` |
| a storage onto a table, a floor or any obj | the same, `get_dumping_location`, `dump_content_at` (`:857`) | after tg's 2 s `do_after`, `remove_all` onto the place (a turf; an obj's turf; walls, windows, grilles and doors take nothing); onto another storage, an instant transfer |
| the held item onto a storage | `on_mousedropped_onto` (`:888`) | `attempt_insert` |
| an item of an open storage onto another of it | `mousedrop_receive` (`:524`) | the dragged item takes the other's place |
| an item of an open storage onto its free cells | `/atom/movable/screen/storage/cell/mouse_drop_receive` (`screen_objects.dm:673`) | the item goes last |
| an item or a mob onto an open closet | `/obj/structure/closet/mouse_drop_receive` (`closets.dm:987`) | an item onto the closet's tile; a mob stuffed in after 4 s (Paralyze, close) |
| a mob onto a movable that can buckle | `/atom/movable/mouse_drop_receive`, `mouse_buckle_handling` (`buckling.dm:49`, `:59`) | `user_buckle_mob` (M8y slice A): the mob is buckled (sits, lies), after 2 s when he is on the next tile |
| a mob onto a human | `/mob/living/carbon/human/mouse_buckle_handling` (`human.dm:870`) | nothing unless the human holds it in an aggressive grab (grabs, piggyback and fireman carry: M9) |
| a mob into the suit storage unit | `suit_storage_unit.dm:450` (M4c slice 4) | as tg |

A plain item dragged onto a table has no handler in tg (an item goes on a table by a click with it, `table_place`: the port's gesture
below makes that click); a short drag (under
0.1 s, within 16 screen pixels) is tg's click on the dragged thing, and so is a drag released over the dragged thing itself. The receivers of
mechanics not ported yet are rows of `docs/ai/specs/not-translated.md` (section 6) and of `docs/ai/specs/live-partial.txt` (leaning on walls,
windows and airlocks).

### 11.1 The drag gestures (M8y slice D, D224-D226, DV138, DV140, DV141)

tg first (D225): `base_mouse_drop_handler` sends its two signals as tg does; a listener that returns its cancel flag ends the drop. Then,
when the dragged thing is an item on the floor, held or worn, or in the dragger's open storage, and neither side has its own tg handler
(`mouse_drop_dragged`, `mouse_drop_receive`, an untranslated one included; `/atom/movable`'s buckling of a dropped mob is not one for an
item, nor is an open storage's cell for an item from elsewhere; an item dropped onto another of its own storage is tg's reorder), the drop
is a gesture (`DragGestures.Run`) of tg's own actions, after `can_perform_action` on the item (tg's lines):

| Over | The gesture |
|---|---|
| a hand slot (or the item held in it) | drag_pickup's `putItemFromInventoryInHandIfPossible` into that hand; an occupied hand: "hand full" |
| the dragger (the health doll in first person) | drag_pickup's `put_in_hands` (a held item stays); none free: "hands full" |
| a storage: a container in the world, worn or carried, an open storage's cells, an item shown in them | the storage's `attempt_insert` (tg's "too big!", "no room!") after `can_perform_action` on its holder |
| an equipment slot (or the item worn in it) | empty: the item into the active hand, then the slot's click (`attack_ui`, tg's "You are unable to equip that!"); a worn container: its `attempt_insert`; anything else worn: "slot taken" |
| a floor | not open: "no room"; reach checked; a floor or stored item first into the active hand; `dropItemToGround`, then onto the tile |
| a thing on a turf (a table, a closet, a machine, a mob) | reach checked; the item into the active hand (a held one's hand made active, a worn or stored one into an empty hand), then `ClickOn` the thing |

Into a hand: a held item's hand is made active (`swap_hand`); a floor item needs the empty active hand ("hands full"); an anchored one is
refused ("anchored"), a worn one that will not come off too ("can't take it off"). A refused step ends the gesture where it stands.

The client (`docs/ai/specs/apps.md`, the controls): in cursor mode (Tab) a left press that moves more than 4 pixels is a drag; the dragged
thing's icon follows the pointer and the world thing under it is outlined; the release over a HUD entry, an item in a slot, a world thing
or a cell sends the drop. A press and release without that move is the click it always was. The captured view has no drags. In first
person the own body is not under the pointer, so tg's drags onto oneself (a storage opened by dragging it onto its wearer) have no target
yet; the storage opens with a click.
