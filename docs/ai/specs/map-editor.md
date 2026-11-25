# The map editor (plan, 2026-11-16, D336)

The owner (2026-11-15, 2026-11-16): a full map editor for the owner and for community mappers. It starts as its own
program, not from the game's menu. It is one program: nobody has to run a server to map. It shows a map through the game's
own start of a round, run inside the program, and its Play button runs the edited map in the real simulation. It saves our
`.as3dmap` (docs/ai/specs/map-format.md) and imports `.dmm`.

Its name is Anomaly Editor (the owner, 2026-11-18; `ClientArgs.EditorName`): its window, its loading screen and the game's
hints write it. This spec and the code go on calling it the map editor, which is what it is.

## 1. What the owner asked for

| # | Requirement |
|---|---|
| R1 | An engine-style 3D camera: fly, a faster flight on Shift, the wheel changes the flight speed, orbit around an object; select objects and see their properties |
| R2 | A new map by its size (x, y, z); open and save `.as3dmap`; import `.dmm` |
| R3 | Build and delete by rectangle, circle and zone; select with the mouse to delete or to move elsewhere; a wall mount picks its wall and its direction (lamps, wall monitors); cables, pipes and the like join by themselves |
| R4 | A Play button: run the edited map and play on it. (2026-11-17) Its player has the admin rights and the server's commands at once, for any cheat |
| R5 | Performance first: the graphics switches in plain sight (lighting, shadows, unlit areas, bloom) and one switch for the simplest unlit look, which is the default when the editor opens |
| R6 | Create and edit shuttles |
| R7 | The sky and the sun of a map: the sky's picture, the mapper's own images included; the sun's light, colour and size |
| R8 | The map's settings: the station's name, the default weather, the air and temperature outside (a planet) and the default air inside the rooms |
| R9 | (2026-11-16) What the game draws as a 3D model is a 3D model in the editor, and everything that joins in the game joins in the editor: walls, tables, cables, pipes and the machines on them, the things under the floor |
| R10 | (2026-11-16) The interface of a game engine's editor: panels that dock, menus, a tool bar, a clean look. Not the dev menu's ImGui windows. The same evening: the layout is rearranged by moving a panel from one dock slot to another, as in the engine's own editor (no floating windows); the interface's size is set inside the editor and fits the screen by itself; the layout is kept between runs |

Every parameter of every placed object can be edited (the format's overrides, map-format.md 4.3): MetaStation alone carries
10,119 of them. A parameter that only code can apply (a portable generator is started by a proc, not by `active = 1`) needs
a ready-made variant of the object; the properties panel shows what a type has, and Play shows the result.

## 2. What it stands on

| Piece | Where | Use |
|---|---|---|
| The map format and its reader | docs/ai/specs/map-format.md, `Tg/Infrastructure/MapLoader/MapText.cs` | The file the editor opens and saves |
| The editable document | `Tg/Infrastructure/MapLoader/MapDocument.cs` (ME1) | Tiles, undoable steps, the writer |
| The game's world inside a process | `ServerHost` as the tests build it, `src/Net/LoopbackTransport.cs`, `godot/scripts/Shared/GameClient.cs` | R9 and R4: the editor's view is the game's own init and replication; Play is the same world let run |
| tg's map template load | `Tg/modules/mapping/map_template.cs`, `reader.cs` | An edit of a tile in a world that stands: tg's ChangeTurf, init of the new atoms and the links of their neighbours |
| The feed seam | `godot/scripts/Shared/IWorldFeed.cs` | The scene draws what a feed's replica holds; the editor is one more feed |
| The dev menu | `GameScene.Dev*.cs`, `Dev/ImGuiLayer.cs`, `Shared/Dev/SpawnCatalog.cs`, `Dev/IconThumbs.cs` | The interface kit, the palette of every spawnable type with its picture |
| Wall mounts | `Shared/World/PropKinds.cs` (`IsWallMountType`), the dev spawn tool's `MountDir` | R3's wall and direction |
| The free camera | `CameraRig.cs` | R1's base |
| Fullbright and the render presets | `Light/LightLayer.cs`, `Render/Quality.cs` | R5 |
| The types' vars | `godot/scripts/Shared/Editor/TypeVars.cs`, the classes of the translated code | The properties panel: every var of a type with its value |
| The `.dmm` reader and converter | `tools/Anomaly.Tools.Maps` | R2's import |
| tg's map json | `maps/map_jsons/*.json`, `datums/map_config.cs` | R8: the name, the levels' traits (gravity, the five weathers, linkage, base turf), the shuttle slots |
| tg's map checks | `tools/maplint/lints/*.yml` and the map unit tests of the tg clone | The rules of a later check panel |

## 3. How it is built

**The program.** The editor is the client's code started in editor mode (`--editor[=MAP]`): no connection to make (its
start shows a loading screen, 5.1), no server to run, no player. ME10 packages it as its own executable beside the game, with the game's assets;
since 2026-11-19 it is a build of its own, and the game's build has no editor and no server (5.13).

**The document.** `MapDocument` holds the model of every tile (map-format.md 4.3: the ordered members with their overrides)
and changes tiles only inside steps, which undo and redo as one. It writes the file in the format's order, so a map that
was read and not edited is written byte for byte as it was read, and an edit of one object changes one line. The map json
lies beside it, as in tg's `_maps/`. The document is the truth: the view shows it and never changes it.

**The view (R9; the owner, 2026-11-16).** The editor shows a map as the game shows it, by the game's own code: `EditorFeed`
starts the game's world inside the program (`ServerHost` with `ServerConfig.MapEditor`, over a loopback network), tg's init
makes the map's atoms, and the game's replication sends them to the scene through a connection that has no mob on the map.
So a model, a joint of pipes, cables, tables or walls, a decal, a lamp's state and the way a thing faces are the game's,
not a guess: the first view (ME2) read each type's values with no init and could not join a pipe or pick a GAGS model.

- The world stands in the lobby with the round's start held (tg's delayed start), and is ticked only to start and for a
  second after an edit. Nothing of the map is played: no air flows, no machine works, no mob moves.
- The map is loaded alone: CentCom and Lavaland are not. The shuttle templates of the map's own maps folder are.
- DV504, an editor's world only: a random spawner spawns nothing and stays, a landmark is visible, and a spawner or a
  mapping helper that stands on the map is shown. They are the markers a mapper places. tg's roundstart mice are off. A
  mapping helper that does its work and deletes itself is seen by its work, and listed in its tile.
- Two of the layers a mapper switches (5.5) are the world's, also DV504. With the under-floor layer on, tg's undertile
  element leaves what an intact floor tile covers visible (a cable, a pipe, a disposal pipe, half clear over its tile as
  a T-ray scanner shows it); with the markers' layer off, the landmarks, the spawners and the helpers are hidden again.
  A switch makes every turf tell its things anew (tg's `levelupdate`), or sets the markers' invisibility, and the
  replication carries the change to the view.
- An edit of the document is given to the world as tg loads a map template into a running world (`MapEditSeam.SetTiles`):
  what stands on the changed tiles is deleted, and a template of those tiles is loaded with a ring of `/turf/template_noop`
  around them, so that `initTemplateBounds` links the neighbours' cables and pipes again. A tile keeps the area the map gives it (tg's ChangeTurf would move a floor laid on
  space into the nearstation area).
- The joints after an edit: the changed turfs and their neighbours are queued for tg's smoothing (`QUEUE_SMOOTH`,
  `QUEUE_SMOOTH_NEIGHBORS`: a template's load queues the new turfs alone), and the queue is worked off at once, since
  tg's smoothing subsystem fires in a round only and this world stands before it. A test holds the view after edits to
  the view of the same map opened anew.
- The world starts on another thread, and the loading screen (5.1) waits for it as the connect screen waits for a connection.
- The cost: a station's init. MetaStation is shown after about 25 to 35 s and holds about 2 GB; a room of a few dozen
  tiles after 3 s, the next map of a session sooner (section 6). An edit is shown in 20 to 100 ms.
- What it shows is one start of a round: a thing whose init picks at random (a poster's picture, a lamp that starts
  broken) shows one pick. The world's seed is fixed, so a map opens the same every time.

**Play.** The editor starts a second world on the document's text as it is, one that is let run: the game's own server
inside the program. It joins it with a mob, and Stop comes back to the editor's world at the same camera (section 5.7).

**Files.** Maps lie in a maps root of tg's layout (`map_jsons/<name>.json`, `map_files/<Name>/<Name>.as3dmap`,
`shuttles/...`): the repository's `maps/` for the stock maps, the user's folder for a mapper's own. D46 and D109 took
effect on 2026-11-17 (D337, section 5.15): the stock maps are checked in, with the owner's map patches folded into them.

**Settings tg's json has not.** One top-level key of the map json, which tg's `LoadConfig` and the host's `MapSeam` do
not read and so ignore: the station's name, the sky (a built-in one or an image file beside the map), the sun (colour,
energy, size, height), the default air inside and the air outside. The sky and the sun reach the clients at the tail of
`MapGrid` (a new protocol version) and the image through the run-time resource path with a larger cap.

## 4. The document (ME1, done)

`MapDocument` (`src/Core/Tg/Infrastructure/MapLoader/MapDocument.cs`), pure: no World, no tg code, no statics.

- `Create(width, height, levels, defaultMembers)` is a new map (1 to 255, 1 to 255, 1 to 64 levels: the editor makes no more, and the wire carries 80, for the levels a round adds); `Parse` and `From` read one.
- `Model(text)` and `Model(members)` give the document's model of a member list, one object per text, so tiles compare by
  reference; a text the format does not allow throws `MapTextException`.
- `Begin(label)`, `Set(x, y, z, model)`, `End()` are one step (a drag of the mouse); `Edit(label, tiles)` is a whole step
  that raises `Changed` once and leaves the document as it was when a tile is outside the map.
- `Undo()` and `Redo()` take back and repeat a step; the history keeps 1,000 steps. `IsDirty` and `MarkSaved()` know the
  saved state through undo and redo.
- `Changed` reports the changed tiles (before and after) to the view.
- `ToText()` writes the file.

Tests: `tests/Anomaly.Core.Tests/Tg/MapEditor/MapDocumentTests.cs` (the round trip of the fixture and of MetaStation, one edit
is one line, the order of chunks and lines, undo and redo, the history's limit, the size limits).

## 5. Stages

Hours are the lead's rough estimates.

| Stage | Content | Hours | State |
|---|---|---|---|
| ME0 | Measure the no-server view on MetaStation (section 6) | 1 | done |
| ME1 | The document: tiles, steps, undo, the writer, new maps (section 4) | 3 | done |
| ME2 | The editor mode: `--editor[=MAP]`, the editor feed with no player, the free camera, the simplest look as the default, open and save (section 5.1) | 6 | done |
| ME3 | The view through the game's own world (R9). Editing in the view: the palette, place, erase, pick, select, undo, the properties panel with every var of a type, the file menu with new map, open, save as and `.dmm` import (section 5.2). The interface in the engine's own controls, with docks, its size and the editor's settings file (R10, sections 5.3 and 5.4) | 32 | done |
| ME4 | The shapes and the helpers: line, rectangle, circle and zone; a selection moved, copied, cut and pasted; wall mounts that take their wall's type; a disposal run that turns as it is drawn; the layers (under the floor, markers, areas); search (section 5.5) | 12 | done |
| ME5 | The camera of R1 in full: the turn around the selection, the move across the view, the wheel toward the point under the mouse, focus, the top view. The view bar with the look's switches of R5 (lighting, shadows, dark areas, bloom). The small parts not drawn from afar (section 5.6) | 6 | done |
| ME6 | Play and Stop: a round of the map as it is, in the game's own server inside the program, joined at a tile of the map as an admin; Stop comes back to the editor as it was left (section 5.7) | 4 | done |
| ME6a | The owner's list of 2026-11-17 after his first hours in the editor: the levels (the level switch, the levels above hidden, a height for every shape), the place tool's modes (add, replace, attach), the preview of what is about to be placed, the search's finds marked on the map, the T-ray button, the map's border, Home and the camera's place in the status bar, the top view's keys, and the bugs he met (sections 5.8 and 5.9) | 10 | done |
| ME7 | The map's settings of R8 and R7: the Map panel, the map json's key, the station's name, each level's weather, the air inside and outside, the sky with the mapper's own picture, the sun (section 5.10) | 14 | done |
| ME8 | Shuttles: a new shuttle's map, a shuttle's template made from its file and its json (tg wants a type for every shuttle), the one-port check, the docks' rectangles, a map's shuttle slots with whether each shuttle fits its dock (section 5.11) | 7 | done |
| ME9 | Ceilings kept in the map: what is over a tile is the mapper's to say (section 5.12; the level switch is ME6a's, 5.8) | 4 | done |
| ME10 | The Checks panel (tg's lint rules of a tile and the editor's own), the editor started as a program of its own, the map's package, the mapping guide (section 5.13, docs/mapping-guide.md) | 10 | done |
| ME11 | The owner's list of 2026-11-17 after his first map: a thing and a selection turned, several rectangles selected, a copy pasted in another editor, a thing that faces as its map says (DV508), the map patches folded into the maps (sections 5.14 and 5.15) | 6 | done |
| ME12 | The owner's third list of 2026-11-17: keys that can be changed, the tools on any level with a lock, the erase tool by layers, ceilings by the tools, arrows for the way things face, the mouse's and the selection's marks, the level's buttons, the border over all levels, the New window's count, autosave (sections 5.16 to 5.21) | 10 | done |

About 119 hours.

### 5.1 The editor mode (ME2, done)

- `--editor[=PATH]` (docs/ai/specs/apps.md 2.8) skips the connect screen: `EditorSession` (`godot/scripts/Shared/Editor`) opens the file's
  document, or makes a new map of `--editor-size=W,H,Z` for a file that does not exist yet, and `EditorFeed` shows it (since
  ME3 through the game's own world, section 3). The connection has no mob.
- **The loading screen** (the owner, 2026-11-18). While the map's world starts, the window shows the editor's name in its
  top left corner and a bar along its bottom, with what the start does now written over it, in place of the connect
  screen's fields, which the editor has no use for (`ConnectScreen.ShowEditorLoading`). At the program's start the window
  is a small one with no border in the middle of the screen, a third of the screen wide (`App.TakeFirstWindow`). It
  stays so until the map's scene is made and has drawn its first frames, which takes seconds: the loading screen's last
  picture is kept over them (`App.HoldSplash`), and then the window has its own size and border back
  (`App.EndSplashWindow`; at once when the start fails). Given its size before the scene was made, the window stood
  large with the loading screen in it for those seconds (the owner, 2026-11-18). The interface is laid out in the small
  window meanwhile: nothing of that arrangement is written to the settings, and the panels go where the mapper left them
  once more when the window has its size. Another map opened later is waited for on the same screen in
  the editor's own window. The engine makes the window about 0.7 s before any of the program's code runs, so which window
  it makes is told to it by the start before (the owner, 2026-11-18: the game's large window was seen for a moment at
  the editor's start). A start of the editor leaves a note, `user://first_window.cfg`, of its small window with no
  border; `project.godot` names that file as its file of overrides (`application/config/project_settings_override`), so
  the engine of the next start reads the note as project settings and makes that window itself, and nothing else is
  seen first. A start of the game takes the note away. The first start after the other program therefore shows the
  other one's window for a moment, and `App.TakeFirstWindow` gives it its own: the editor its small window, the game
  the project's window in the middle of the screen, filling a screen that has no room for it. A size given on the
  command line (`--resolution`) wins over the note's, and the Godot editor reads no note. The engine's boot picture
  is off (`application/boot_splash/show_image`): the window opens in the loading screen's own colour, in the game as
  well. The bar
  is `EditorFeed.StartProgress`. The making of the world is about half of a start and says nothing of how far it is, so
  the bar creeps by the clock there and never reaches that part's end. Then the Master's subsystems are counted as each
  finishes its init (`StepProfiler.InitsDone` of `ServerHost.InitsExpected`, read across threads), and the line names the
  one that runs. Then come the lobby's first seconds and the join, and one frame with the bar full before the making of the
  scene holds the picture. A start that fails writes why there, in red. The log's line of a start says how long each
  part took (`EditorFeed.StartTimes`): the Outpost on a busy machine, 20 s, of which the world was made in 11 and
  initialized in 7.5.
- `GameScene.Editor.cs` is the scene's editor mode: the free camera, flown with the right mouse button held (the mouse
  turns the view, W A S D Q E fly, Shift is faster, the wheel changes the speed); the rest of the camera is 5.6. The
  game's interface is hidden by the scene's own switch, and the game's options are never written from the editor.
- The simplest look is the default (R5): every surface lit and the view drawn unshaded, the low preset at the full
  resolution, so no lamp, shadow or bloom is worked out. The view bar's `Simple look` switch gives the game's look by
  the player's graphics options instead, part by part (5.6). Neither has the game's distance fog.
- The bar shows the file, its size, the frame rate and `Save` (also Ctrl+S and the console's `editor save`). A save writes
  beside the file first and takes its place only when whole.

### 5.2 Editing in the view (ME3, done)

**The tools** (`GameScene.Editor.cs`; the tool bar's buttons, keys 1 to 4). The left mouse button is the armed tool's, on the tile
and the thing under the mouse (the game's own picker; through the ceilings from above the walls, on them from under: 5.18):

| Tool | A click | A drag |
|---|---|---|
| Select | The thing under the mouse, or the tile's turf, is the selected member of its tile; from under it, a ceiling is (5.18). What is selected keeps its frame (5.20) | A rectangle of tiles is selected; a selected thing or rectangle is moved (5.5) |
| Place | The palette's type goes on the tile: a turf takes the place of the tile's turfs, an area of its area, anything else is one more thing. Against a wall's side a thing goes on the tile in front of that face, and wall furniture hangs on that wall (5.5) | Every tile the mouse crosses, one step; or the shape chosen (5.5) |
| Erase | What is highlighted is taken away: the thing under the mouse, or one layer of the tile (5.17); with Shift the tile becomes the level's empty tile (5.17) | The same kind on every tile the mouse crosses, one step; or on the shape chosen |
| Pick | The thing or the turf under the mouse, with its vars, is what the place tool places, and the palette shows its type (with any tool: Ctrl + click, or the command key with a click; on a Mac the system gives Ctrl + click as the right button, and it picks all the same) | |

The keys a mapper starts with (each can be changed: 5.16): Ctrl+Z and Ctrl+Shift+Z (or Ctrl+Y) take back and repeat a
step, Delete takes the selection away, R turns the way a placed thing faces, F brings the selection into the view, Home
goes to the map's first tile and T is the top view (5.6), + and - (or Page Up and Page Down, or . and ,) change the
level, L locks the tools to it and Tab changes the place tool's mode (5.8, 5.17), F5 plays the map (5.7). Esc gives up a
drag or a paste, then puts the tool away, then drops the selection. In the editor every key and button is the
editor's: the game's own do nothing.

**The palette**: every type of tg with its picture (the dev menu's `SpawnCatalog` and thumbnails), in four lists (objects,
turfs, mobs, and the areas, which the catalog reads for the editor), by category and by search, with favourites. The
mapping helpers, the landmarks and the spawners are listed.

**The tile and the properties.** The Tile panel lists the selected tile's members in the file's order (the things, the
turf, the area), each with its picture; a thing has a button that takes it away, and the tile one that makes it the map's
default. The Properties panel lists the selected member's vars: every var of its type with its value (`TypeVars`: read from
the type's class, as the spawn catalog reads a name), the member's own where the map sets it. A value that is edited sets
the var on the member (a literal of the format: `"text"`, a number, `null`, a path, a `list(...)`); a value made the type's
own again, or emptied, takes the var off the member. A var the list has not got is set by its name.

**The changes of a tile** are `MapEdits` (`Place`, `Remove`, `SetVar`): each gives the tile's new model with the members in
the format's order; a thing the tile has already is not placed twice; a new var goes to its place among the member's vars
by name, as tg's map files keep them.

**The files** (the File menu). New map (its size, then its file), Open, Save (Ctrl+S), Save as, and Import a `.dmm`
map: tg's map converted by the converter of `tools/Anomaly.Tools.Maps` (its sources are compiled into the client) into a
`.as3dmap` beside it, unknown paths left out, and opened. Leaving a map with changes that are not saved asks first, the
window's close button too. Another map is another world: the open one is stopped and the loading screen waits for the next.
Recent maps (the owner, 2026-11-18) lists the last ten maps that were opened or saved, the newest first, and opens the
one that is clicked as Open does. The editor's settings keep them (`files/recent`, `RecentMaps.With`); the open map heads
the list but is not shown in it, and a file that is gone is not shown either.

**The console's lines** (a scripted run's `--dev-after`): `editor save`, `saveas PATH`, `open PATH`, `new PATH W H Z`,
`import DMM [PATH]`, `undo`, `redo`, `place X Y Z MEMBER`, `erase X Y Z INDEX`, `set X Y Z INDEX VAR [LITERAL]`,
`select X Y Z [INDEX]`, `log [LINES]` (what the world behind the view wrote: the runtime errors of the map's init; its
first line counts the shown things, the frames a second, the draw calls and the lamps drawn as lights, its second is
the camera as `--view` has it), section 5.5's `tool NAME`, `shape NAME`, `brush MEMBER`,
`region X0 Y0 X1 Y1 [Z]`, `copy`, `cut`, `paste X Y [Z]`, `move DX DY`, `delete`, `find TEXT`,
`layer under|markers|areas|border|preview on|off`, section 5.8's `level N`, `above on|off`, `mode add|replace|attach`,
`height N` and `home`, section 5.13's `checks` and `package ZIP`, section 5.12's `ceiling none|lattice|catwalk|TYPE|rule`, section 5.10's `setting KEY [VALUE]`, section 5.11's `newshuttle PATH W H` and `layer docks on|off`, and section 5.6's `view X Y EYE YAW PITCH` (the camera over a tile: its height in
metres, its yaw and pitch in degrees), `top on|off`, `focus`, `look simple|lighting|shadows|dark|bloom on|off`, and
section 5.7's `play [X Y [Z]]` (again while the round starts: given up) and, in the round, `editor stop`; section 5.14's
`region add ...`, `rotate [ccw]` and `clipboard on`; section 5.16's `key FUNCTION KEY|none|default`, `keys` and
`do FUNCTION` (a function by its id, as its key runs it); section 5.17's `lock on|off`; section 5.21's `autosave` and
`restore [N]`; and for a scripted check `state` (the level, the tool, the mode, the camera's place, what is selected and
what the mouse is on), `tile X Y Z` (what the map has on a tile) and `shapes X Y Z` (what a click is tested against on a tile).

### 5.3 The interface (ME3, done; R10)

The owner (2026-11-16): the first interface, windows of the dev menu's ImGui, was not a normal editor's; he wants docks and
the look of a game engine's editor. So the interface is the engine's own controls (`godot/scripts/Editor/EditorShell.cs`,
built by `GameScene.EditorUi.cs`), in a flat dark theme of its own:

- **The menus**: File (new map, new shuttle, open, save, save as, import, export the map's package, quit), Edit (undo, redo, cut, copy, paste, delete, find on the
  map), View (focus on the selection, the top view, the simple look, the three layers of 5.5, each panel shown or
  hidden), in the window on every system. An item shows its function's key beside it and does not take it: the keys
  are the view's (5.16), by default the system's command key (Command on a Mac, Ctrl elsewhere) with N, O, S, Shift+S,
  Q, Z, Shift+Z, X, C, V and F, and Delete. A text field that has the keys keeps its own cut, copy and paste. Edit has
  Turn right, Turn left and Hotkeys; View has the level's functions, the arrows (5.19) and the lock (5.17) too.
- **The tool bar**: the four tools, the shape the place and the erase tools work by, the place tool's mode and the
  height (5.8), the level that is edited (two large buttons with the level between them: 5.20), `Lock` (5.17) and the
  switch that shows the levels above it (5.8), undo and redo, Turn (5.14), Play (5.7), the map's name and size.
- **The view bar**: over the top edge of the 3D view, as an engine editor's view has its own: Focus, Home, Top view,
  T-ray (the under-floor layer of 5.5) and Arrows (5.19), then the look's switches, Simple look and the four parts of the game's look (5.6). It goes onto a second line where the
  view is narrow, and only its buttons take the mouse.
- **The docks**: five slots around the 3D view, as the engine's own editor has slots: left top, left bottom, right top,
  right bottom, and one under the view. A panel is a tab of a dock and is dragged by its title to another dock: onto its
  row of tabs, or anywhere on a dock that is empty. A dock with no panel takes no room, and a side with no dock none;
  while a panel is dragged every dock is there to drop it on. The borders between the docks and the view, and between
  the two docks of a side, are dragged to size them; a dock is never narrower than its panel's content. A border is a
  thin dark line with no gap beside it (the owner, 2026-11-17: the map showed through the gaps), taken by the mouse a
  little to either side. The panels:
  Palette (left top), Tile (right top), Map (right top, behind the Tile: the map's own settings, 5.10), Properties
  (right bottom: a var's value is edited in its cell), Checks (under the view: 5.13), Log (under the
  view: what the world behind the view wrote, the runtime errors of the map's init among it) and Search (under the view, behind the Log: 5.5). The View menu hides and shows each.
- **The interface's size**: View, Interface size. Automatic (the default) is in proportion to the screen's height, 100%
  at 1080 pixels, in steps of a quarter, so the text and the docks take the same share of a screen of any resolution;
  or 75% to 300% (100%: a 13 pixel font). A change makes the interface again at that size, with the panels where they
  are. It is the editor's own: the game's dev windows scale does not move it.
- **The status bar**: what is under the mouse (the tile, the thing, the area) or the armed tool's hint (while a mouse
  button holds the camera, what the mouse and the keys do to it; over the view but off the map, that no tile is under
  the mouse), the last notice, where the camera is (the tile it is over, the edited level, its height over that level),
  whether an edit is still being shown, the build's version (the game's version tag is hidden: the tool bar is in its
  corner) and the frame rate. A line at the mouse says what a click does there.
- **The windows that ask**: the new map's size, and what to do with changes that are not saved (save, do not save,
  cancel) before a map is left by New, Open, Import, Quit or the window's close button. Files are chosen in the
  system's own file window.
- The palette's list (`TypeListView`) draws only the rows in view, so the list of every type scrolls as a short one.

The mouse over the free middle is the 3D view's; a click there takes the keys back from a panel's field. Not yet: a dock
split by dropping a panel on a border. A panel in a window of its own is not wanted (the owner, 2026-11-16).

Checked in the real client by script (`--keys`, apps.md 3): a menu opened by a click, a border dragged wider and narrower,
a panel dragged to another dock and onto an empty one, a panel hidden by the View menu, another size picked, the
arrangement found again at the next start, a type picked in the palette and placed with the mouse, Command+Z, a var's
value typed into its cell and saved, both windows that ask.

### 5.4 The editor's settings (ME3, done)

What a mapper arranged is kept between runs in the editor's own file, `map_editor.cfg` in the user's data folder (the
engine's `user://`; `MapEditorSettings` over the engine's `ConfigFile`). `--editor-settings=PATH` names another file: a
scripted check gives its own, so that it leaves the mapper's alone. The game's options file is not the editor's and is
never written from it.

| Section | Keys |
|---|---|
| `view` | `scale` (the interface's size in percent, 0: automatic), `simple_look`, the parts of the game's look of 5.6 (`lighting`, `shadows`, `dark`, `bloom`), `fly_speed` (the flight's speed in percent), the layers of 5.5: `under_floor`, `markers`, `areas`, `border`, `docks`, and `preview` (5.9), `arrows` (5.19) and `level_lock` (5.17) |
| `layout` | per dock (`LeftTop`, `LeftBottom`, `RightTop`, `RightBottom`, `Bottom`) its panels in order and `<dock>_front`, the one in front; `hidden`, the panels the View menu hid; `border_left`, `border_right`, `border_bottom` (a side's width, the bottom dock's height) and `border_left_rows`, `border_right_rows`, in units of the interface's size, so they hold at another size |
| `palette` | `favourites_0` to `favourites_3`: the starred types of the objects, the turfs, the mobs and the areas. Until the file has them the lists are the dev menu's |
| `keys` | The functions whose keys the mapper changed, each by its id with its keys (5.16); a function the section does not name has its own |

The file is written when what it holds has changed (looked at once a second) and before the interface is made again at
another size. A file that is not there, a key it has not got or a value of another kind gives the default; a panel the
file does not name stays in the dock it is built in, and a name with no panel is passed over.

### 5.5 The shapes and the helpers (ME4, done; R3)

**The shapes.** The place and the erase tools work by the shape picked in the tool bar (`MapShapes`):

| Shape | The mouse | The tiles |
|---|---|---|
| Free | A click or a drag | Every tile the mouse crosses, the ones a fast drag skips between two frames too |
| Line | Pressed on one tile, let go on another | The straight line between the two |
| Rectangle, Filled rectangle | The same | The border of the rectangle between the two, or all of it |
| Circle, Filled circle | The same | The circle that fits in that rectangle (an ellipse when it is not a square): its border, or the tiles whose middle is inside |
| Zone | A click | The tiles joined to the clicked one by their sides that have its turf and its area: a room's floor up to its walls, a run of wall. A zone of more than 4,096 tiles is refused (the space around a station) |

While the button is held the tiles the shape will change are marked, and a zone's are marked under the mouse before the
click. The shape is one step. The place tool puts the palette's type on each tile as a click does; the erase tool
takes every thing of each tile away and leaves its turf and area, or with Shift makes the tiles the map's default.

**The selection.** With the select tool a click selects a thing or a tile, and a drag over tiles a rectangle of them (a
drag that starts on a thing moves that thing; with Shift it selects a rectangle from there too). The selection is:

- moved by dragging it: a rectangle takes all its tiles hold with it (their turfs, areas and things), takes the place
  of the tiles where it is let go, leaves the level's empty tile (5.17) behind, and goes no further than the map's edge; a
  thing goes to the other tile with its vars;
- copied or cut (the Edit menu, the command key with C or X): the tiles of the rectangle as they are, the selected
  thing, or the selected tile. Paste (V) makes the copy follow the mouse, marked on the map, until a click puts it
  there (Esc: nowhere). Tiles take the place of the tiles there, a thing is one more thing of its tile. The copy is kept
  as text, so it is pasted into another map opened in the same run;
- deleted (Delete): a thing is taken away, a rectangle's tiles become the level's empty tile (5.17).

A click inside the selected rectangle that drags it nowhere selects what was clicked.

**Wall mounts.** A thing placed against a wall's side goes on the tile in front of that face and hangs on that wall
(`MapEdits.Mounted`): a family with tg's directional types (`MAPPING_DIRECTIONAL_HELPERS`: a lamp, an alarm, an APC, a
sign, an intercom, a camera) takes the type of that side (`/obj/machinery/light` or `.../directional/south` clicked on
a tile's north wall is placed as `/obj/machinery/light/directional/north`, whose dir and pixel offset are the type's
own), and other wall furniture is turned to the wall. Off a wall, the palette's Facing (R) picks the side the same way.
The line at the mouse names the wall.

**What joins.** Cables and pipes join what is beside them by tg's own init, in an edit as in a map's load (section 3;
a test lays a cable under a tile beside another and finds the two joined). A piece of a run that tg turns by its `dir` instead (a disposal pipe's segment, a heat exchange pipe:
straight along its dir, bent with a diagonal one) is turned by the way it is drawn: with the Free shape or the Line the
run goes from tile to tile by their sides, each piece lies along it, and the piece at a corner is the bend of the two
sides it joins (`MapEdits.RunPieces`). Placing a type that lies under the floor tiles shows the under-floor layer.

**The layers** (the View menu; kept in the settings):

| Layer | Shows | When a map opens |
|---|---|---|
| Under the floor | The cables, pipes and disposal pipes that an intact floor tile covers, half clear over the tile (the world's: section 3) | off, as the game looks |
| Markers | The landmarks, the spawners and the mapping helpers (the world's) | on |
| Areas | A colour over every tile of an area, the same colour in every run, none over the map's default area (the editor's own squares) | off |
| Shuttle docks | The border of the rectangle each docking port's shuttle takes (5.11): a dock's in blue, a shuttle's own in pink | on |
| Map border | A yellow line around the tiles of the edited level, at its floor and at its walls' tops, with a post at each corner: an empty map is all space and shows nothing else of where it is | on |

The under-floor layer has a button of its own on the view bar, T-ray (the owner, 2026-11-17): what a T-ray scanner
shows in the game, but steady and over the whole map.

**Search** (the Search panel; Edit, Find on the map). The words typed are looked for in every member of every tile
(`MapSearch`): a member is found when its text, as the file writes it (the type's path with the vars the map sets), or
its type's name holds every word, whatever the case: `airlock command`, `name = "Bridge"`, `req_access`. The list shows
the first 500 with their tile, name and text, and counts all; it is looked for again when the map changes. A click on
a row selects the member and brings the camera over its tile. Every find (the first 5,000) is marked on the map by an
orange box over its tile, drawn through the walls, so that where the finds are is seen at once from any side; an empty
search field takes the marks away.

Checked in the real client by script: a filled rectangle, a circle and a line drawn with the mouse; a zone given an
area; a disposal run drawn free and by a line, its bends in the saved file; a lamp and a fire alarm clicked onto a
north and a west wall and saved as those sides' types; a rectangle selected by a drag (with Shift over things), moved
by dragging it, copied and pasted with the command key and a click, cut and deleted; a thing dragged to another tile;
Esc giving a paste up; a row of the search clicked; the menus; the three layers, on MetaStation too (section 6).

Not yet: the same for conveyor belts, whose corners need the direction they carry (tg has two types for them);
replacing every found member at once; a picture of the copy while it follows the mouse (its tiles are marked);
turning a copy.

**Left for later stages.** Markers for the mapping helpers that delete themselves. (The owner's map patches were
folded into the stock maps on 2026-11-17: section 5.15.)

Tests: `tests/Anomaly.Server.Tests/Client/Editor/EditorFeedTests.cs` (a room shown by the game's init: its joints, markers,
members, edits by template load, undo back to the start, the under-floor and the markers' layers; MetaStation from its
maps folder; a new map saved and opened again; the arguments), `MapEditsTests.cs` (the changes of a tile, a type's
vars), `MapShapesTests.cs` (the shapes' tiles, a zone, a rectangle copied, pasted and moved, a thing moved, a run's
pieces, a wall mount's type, the search).

### 5.6 The camera and the look (ME5, done; R1, R5)

**The camera** (`GameScene.Editor.cs`, `CameraRig`):

| The mouse and the keys | The camera |
|---|---|
| The right button held | Flies: the mouse turns the view, W A S D move along it, Q and E down and up, Shift is faster. The wheel changes the flight's speed, 0.1 to 20 times (said in the status bar, kept in the settings) |
| The middle button held, or Alt with the left | Turns around a point: what is selected while it is in the view, else the floor in the middle of the view. The point stays where it is on the screen |
| The same with Shift | Moves across its view: the point of the floor under the mouse stays under it |
| The wheel; on a trackpad two fingers moved, or a pinch | Goes toward the point of the floor under the mouse or away from it, 15% of the way a notch, so that the point stays under the mouse; no nearer to it than 2 m, no higher than 650 m |
| F, the bar's Focus | Goes where what is selected fills the view: the selected rectangle, thing or tile; with nothing selected, the whole map. The view keeps its direction (it is turned down to 45 degrees if it looked along the floor or up) |
| T, the bar's Top view | The top view, and back |

**The top view** is the flat view of the map (section 7: there is no 2D view of sprites, this takes its place):
straight down, north up, with no perspective, so every tile is a square and a wall hides nothing beside it. It opens
over the point the view looked at, as far from it, and T gives back the view it was entered from, over the same point.
In it the right or the middle button, and Alt with the left, move the map under the mouse; W A S D move it at any
time (but while a field is typed in or a command key is held), Shift faster, and Q and E change its scale; the wheel
changes the scale about the point under the mouse; nothing but T leaves it. Its scale follows the camera's height as a perspective's does (a
height shows as much of the map in both), so the wheel, the keys and what is drawn by its distance work alike in both.
Behind the map it has a plain dark ground: with no perspective the sky is drawn as streaks.

Focus, the top view and a row of the search put their point in the middle of the free part of the window, between the
docks, which is not the window's middle, where the camera looks.

**The look (R5).** The view bar's switches, kept in the settings:

| Switch | On | Off |
|---|---|---|
| Simple look (on when the editor is first opened) | Every surface lit and drawn unshaded: the fastest view | The game's look, by the player's graphics options and the four switches below |
| Lighting | The map's own light: the lamps, and the dark where there are none | Every surface lit |
| Shadows | The shadows of the lamps and of the sun, as many as the player's options give | None |
| Dark areas | The dark where no lamp lights, as in the game | Lifted (a surface's least light is 0.3), so that what stands there is seen while the lamps' light is looked at |
| Bloom | The glow around bright lamps and screens | None |

The game draws the lamps for a player among them: the nearest as lights with shadows, the others only while the player
sees their tile along the floor, none further than the options' light distance. From above a station that lights the
rooms under the camera and leaves the others dark, or shines through their walls. So the light the editor shows goes by
where its camera is:

- **from above the walls**, tg's own light, tile by tile, over the whole map: the client's light grid, which works
  tg's falloff out with the walls in the way (the game's light before D288, which the client kept). Which rooms are lit
  and which are dark is seen at once, from any distance and in the top view, and no lamp shines through a wall;
- **under the walls**, flown into a room, the game's: the lamps as lights with their shadows and the bloom, and the
  room under its ceiling, as a player sees it.

The light is the edited level's (the game takes the level at the camera's height, which is far above it here). The
world behind the view stands before the round: tg's lamps are lit by their area's power as the map's init leaves it, so
an area with no APC is powered and tg's space is not, and a lamp placed by an edit lights up when the edit is shown.

**The far view.** From high above a station most model parts are a pixel or two, and each is a draw call of every chunk
that has one (the things are batched by chunk and model part). In the editor a chunk's model part is not drawn from
further than 470 times the radius of its largest instance, where it is about four pixels on a screen 1,440 pixels high,
plus the chunk's own reach (`ChunkBatch.SmallPartRange`; the engine's own test of a node's `VisibilityRangeEnd`, so it
takes no frame time). The tiles, the windows, grilles and other shaped pieces, and the flat pictures are always drawn,
and the game draws every part as before. Section 6 has the numbers.

Checked in the real client by script, the mouse and the keys through `--keys` (which gained drags of the right and the
middle button, the wheel at a point and Alt): F on a selected tile; the middle button, and Alt with the left, turning
the view around it (as far from it after as before); Shift with the middle button moving the view; the wheel toward a
point and away from it; T, the right and the middle button moving the map, the wheel changing its scale, T giving
the first view back; the view bar's buttons and the View menu's two items; every switch of the look in a room with two lamps,
from above and from inside; a lamp placed by an edit lighting the room; MetaStation from above with its dark
maintenance tunnels, in the top view too.

Not yet: the pieces' kit (every window, grille or table of a chunk draws the vertices of all the kit's shapes, 3.2 of
the 6.9 million triangles of MetaStation's map: drawn shape by shape they would give the whole-map view up to a
quarter more frames, and the game some); a choice of the graphics preset inside the editor (it is the game's options);
the top view turned by quarter turns; shadows of every lamp from above (they are the nearest few's, as in the game).
A trackpad's gestures were checked by script (`--keys` takes `pan=DXxDY@XxY` and `pinch=FACTOR@XxY`, 2026-11-17): two
fingers moved and a pinch over the view go toward the point under the mouse and away from it, and over a dock they do
nothing to the view. The engine hands every gesture to the scene twice (its own gesture dispatch, then the general one
of every event), so a unit of a move counts twice and a pinch by its factor squared; it is the speed the trackpad has
had here and in the game's own zoom, and it is left so. Not checked: a trackpad under a hand (the feel), Windows and
Linux.

Tests: `Client/Interaction/DragModelTests` (the script's new steps). The camera and the look are the scene's own, with
the engine: they are checked in the client, as above.

### 5.7 Play and Stop (ME6, done; R4)

**Play** (the tool bar's button, F5, the console's `editor play [X Y [Z]]`) runs the map as it is in a round of the game
and puts the mapper into it.

- **The round** is `PlayWorld` (`Shared/Editor/PlayWorld.cs`): the game's own server (`ServerHost`) inside the program, on
  a thread of its own with a large stack, paced at tg's 20 ticks a second (`RealtimePacer`), listening on a free port of
  this machine alone (127.0.0.1). The view joins it as the game client joins any server (`GameClient` over the real
  transport). So what is played is the real thing: the round's init resolves the random spawners and takes the
  landmarks away, the power flows, the air moves, the shuttles of the maps folder are there.
- **The map it loads** is the document's text at the moment Play is pressed, the changes that are not saved included;
  the map's file is not written. A map of a maps folder (a json of its `map_jsons` names the file) is loaded from that
  folder, with CentCom, the shuttles and its other files, and the document's text takes the file's place
  (`ServerConfig.MapTexts`, `MapSelection.Texts`). A map of no maps folder (a new map, a lone file) gets a scratch folder
  with its text and the least json, removed when the round is over.
- **A lone map's shuttles** (the owner, 2026-11-17: a dock on a lone map failed the shuttles' init and nothing said so). The
  scratch folder gets a copy of the game's own `shuttles` (`EditorFeed.GameMaps`: the nearest `maps` with `map_jsons`
  above the program, or a build's beside its executable), so a lone map's docks load tg's shuttles. It has no CentCom:
  the shuttles that start there stand at the stand-ins of CentCom's docks on the reserved level (DV190). That level is
  as large as the world, and tg keeps 16 tiles of border on it, so a world as small as a small map had no room for the
  emergency shuttle's 50 by 50 dock: the stand-ins were not made, the subsystem's init failed, no dock loaded its
  shuttle and an arrival stood on the floor. A world with no CentCom level whose map has a dock is now grown, after the
  station is loaded at its own place, to at least 96 by 96 (DV509, `SSmapping.Initialize`): the border and 64 tiles
  between, for that dock and for the transit of a shuttle 32 tiles long. The map keeps its coordinates; the tiles beside
  it are space. The editor's own world of such a map is grown too, so a dock's shuttle stands at it in the view; the
  camera starts over the map, not over the world's middle.
- **It is another world** than the one that shows the map (section 3), which stands, is not ticked, and keeps the
  document's edits and its undo: nothing that happens in a round changes the map.
- **No lobby**: the round starts at once and the join is tg's late join as an assistant (D270's path of the tests and the
  test room), on the tile the mapper chose (`ServerConfig.JoinAt`, in place of the map's late-join landmarks: a new map
  has none and could not be joined). The tile is the selected one, or the middle of the selected rectangle, else the
  tile in the middle of the view; and from there the nearest tile a mob stands on (`MapEdits.Standable`: an open turf
  that is no space, no open space, no chasm and no lava).
- **Or as a new arrival** (the owner, 2026-11-17: the arrival is tested from the editor too): the tool bar's choice beside Play, "On the tile"
  or "As an arrival" (kept in the editor's settings; the console's `editor play arrival`). The round then has no join tile
  (`ServerConfig.JoinAsArrival`) and the join is tg's own late join with no spawn override: a seat of the arrivals shuttle the map's dock loads,
  buckled and announced, else a late-join landmark. A map with neither has no arrival point (`MapChecks.HasArrival`), and Play says so and does
  not start.
- **The mapper is the round's admin** (the owner, 2026-11-17): a connection from this machine holds tg's `!localhost!`
  rank with every right (`ServerConfig.LocalAdmin`, tg's `ENABLE_LOCALHOST_RANK`), the dev commands are on, and the
  server console's commands are taken from the player's console (`ClientConsole`): the dev menu, `god me`, `spawn`,
  `teleport`, `status` and the rest of docs/ai/specs/apps.md 1.6.
- **While the round starts** the editor stays as it is: the button counts the seconds, and pressed again gives the
  round up. A round that does not start says so in the status bar, and its lines go to the Log panel, under the map's
  own world's ("the last round played"), as a played round's do after Stop.
- **The scene changes** to the game's (`App.StartPlay`): the whole window is the game, in the first person, with its
  HUD, its sounds and the player's graphics options. The editor's interface is not there until Stop; a line at the top
  names the map and has the Stop button.

**Stop** ends the round and brings the editor's scene back (`App.StopPlay`): F8, the Stop button (when the mouse is
free), "Back to the editor" in the Esc menu (the Quit row's place), the window's close button, the console's
`editor stop`, and by itself when the round's connection ends (the editor then says why). The round's thread runs its
last tick, closes its connection and removes its scratch files. The editor comes back on the map's own world as it was
left (`EditorResumeState`): the camera, the top view too, the level, the tool and its shape, the palette's type and its
facing, the picked brush, the selection.

The scene is changed at the start of a frame, not in the middle of the one that asked (a key, a console line, the
round's end): the rest of that frame still runs on a scene that is whole.

A scripted run goes on across Play and Stop: `--keys`, `--dev-after` and `--screenshot-after` keep counting from the
editor's first scene, each step done once (in the game every scene runs the script from its own start), and the
camera's arguments (`--view`, `--camera`, `--yaw`, `--pitch`) are the editor's, not the round's. It goes on across
another map opened too (`App.OpenEditor`: `editor new`, `open`, `newshuttle`), which passes through the connect screen:
the steps had started over there, so the line that opened the map ran again and its question took the steps after it.

Checked by script in the real client: a room built in a new map that was never saved, played from the selected tile
in the first person, walked in, `status` and `god me` answered by the round; F8 back to the same camera, tool and
selection, with the top view on too; a second round at another tile; the Stop button with the mouse; the Esc menu's row
and its page (seen with their text; its last click was not made by the script); a round given up while it starts;
MetaStation with its CentCom and Lavaland (section 6).

Not yet: the map changed from the editor while its round runs (the round would take an edit as the map's own world
does, `ServerHost.SetMapTiles`, with a switch between the two scenes that leaves the round running; the owner,
2026-11-17: not now); the game inside
the editor's view, between the docks (the game's scene in a view of its own: its HUD, its windows and its mouse are
made for the whole window); Stop that does not build the editor's scene again (11 s for MetaStation); a start at a
job's own start point; a part of a large map played alone; a lone map played with CentCom itself (save it into a maps
folder, 5.10); a quiet round (it has tg's random events and antagonists,
as a no-lobby server's). The round's memory is given back by the collector, not at once.

Tests: `Client/Editor/PlayWorldTests` (a round of a map of a maps folder with a change that is not saved: the join on
the asked tile, on the floor the file has not got, the file as it was, the `!localhost!` rank; `PlayWorld` on a new
map, joined over the real network, the dev access and a console command answered, stopped; the tile a player starts
on; a round joined as a new arrival: the player buckled in a seat of the arrivals shuttle),
`Client/Editor/NewStationTests` (a lone map of 48 by 48 tiles with an arrivals dock, played from its scratch folder:
the game's shuttles are there, CentCom's stand-in docks stand, the arrival is seated, the map is where it was built).

### 5.8 The levels and the place tool's modes (ME6a, done; the owner, 2026-11-17)

The owner after his first hours in the editor: a map of several levels could not be made at all (no level could be
reached but the first, and a block could not be put on a block), and a tool needs more ways to place.

**The levels.** A map has 1 to 64 levels. The levels of one map file are one stack: tg links them when the map loads
(`height_autosetup`), so the game draws them over one another and the editor's world does too.

- **The edited level** is switched in the tool bar (`Level`: a button down, the level and how many there are, a
  button up), with + and -, Page Up and Page Down or the . and , keys (5.16), and by the console's `editor level N`.
  The camera stays where it is (the owner, 2026-11-17: it went up or down with the level, and what he was looking
  at left the view). In the top view it does go with the level, which keeps that picture as it was: the top view's
  scale is the camera's height over the edited level. The status bar says the level with the camera's place.
- **The view** shows the edited level and the levels under it; the levels above are hidden, so the edited one is seen
  from above. `Above` in the tool bar shows them too, as the game draws them. Levels that are no part of the edited
  one's stack (the world's reserved level) are not drawn.
- **A pick** stays among the shown levels: a ray goes no higher than the top one shown
  (`WorldPickAdapter.PickTopLevel`). How low it goes is the lock's to say (5.17): by default down through the open
  tiles to what is seen on a lower level, and with the tools locked no lower than the edited level, whose open tile is
  the hit and not the floor seen through it (`PickFloorLevel`).
- **A drag** goes on over the level it began on: over a hit of another level, the tile of its own level under the mouse.
- **The height** (`Height` in the tool bar, `editor height N`): the levels a click, a stroke or a shape of the place
  and the erase tools goes up through. 1 is the level clicked alone; with 3 a rectangle of walls is a wall three levels
  high, a filled rectangle of floor a floor on each of three levels, a circle a tower. The marks and the preview show
  every level of it.
- What a tool places on a level above the edited one while those are hidden makes that level the edited one (a block
  attached on a block's top).

**The place tool's modes** (the tool bar; Tab goes through them; `editor mode NAME`):

| Mode | A click puts the palette's type |
|---|---|
| Add | On the tile under the mouse: a turf in the turf's place, a thing one more thing; against a wall's side a thing goes on the tile in front of it and wall furniture hangs on that wall (5.5) |
| Replace | In the place of what is clicked (`MapEdits.ReplaceOn`): a thing where the clicked thing stood; a turf in the clicked floor's or wall's place; a turf in a clicked thing's place (a wall where a full-tile window stood); a thing in a wall's place, on plating. With a shape, every one of the clicked type the shape covers gives its place and the rest of its tiles is left alone: the iron floor of a room becomes wood and its walls stand, every table of a rectangle becomes another |
| Attach | On the tile the clicked face looks at: beside the side of a wall, of a window or of anything that fills its tile; over a block's top, which is the level above; on a floor's own tile. A click at a time: held, the next tile would be the one beside what the click has just placed. A shape begins on the attached tile and goes on over its level |

**Home and the map's edge.** `Home` on the view bar (the Home key, `editor home`) puts the camera over the map's first
tile, 1, 1, its south-west corner; the map's border (5.5) is drawn; and over the view but off the map the status bar
says that no tile is under the mouse. The owner had flown far off an empty map, which is all space, and no tool did
anything there with nothing to say why.

**What was wrong** (found by the owner, each fixed with the list):

- The letter n could not be typed into a field: the game's push-to-talk key is N, and the game's keys were still read
  in the editor before its fields. In the editor none of the game's keys is read.
- The wheel over a panel moved the camera: the engine passes a wheel that a control did not take through the controls
  that stop the other buttons, so a list at its end, or a panel's empty part, gave it to the view. What the interface
  did not take is the view's only over the view.
- The wheel over a slider of the Map panel changed the slider's setting as well as wheeling the panel (the owner's
  test, 2026-11-18: wheeling the panel down and up again raised the sun by 19 degrees unseen, and a save wrote it).
  The panel's sliders do not take the wheel.
- Ctrl + click picked nothing on a Mac, where the system gives it as the right button: it turned the camera.
- The palette's `Helpers` box seemed to do nothing: it added the helpers to a list that showed none of them in view.
  It lists the helpers alone now, as `Favourites` lists the favourites; the whole list has them always.
- A map of 16 levels could not be shown: the world adds a reserved level, and the wire's limit was 16
  (`Limits.MaxMapLevels`, now 80; a map's own limit is 64, `MapDocument.MaxLevels`). The New map window says how many
  tiles a size is, and when that takes a minute or minutes to open.
- A pick from a camera outside the map's rectangle passed through the map's walls and things to the floor behind them
  (`WorldPicker` walked the cells from the camera's own and left the walk at once): it is walked from where the ray
  enters the rectangle.
- In an editor's world what hangs on a wall fell apart when the wall's tile was edited (tg's wall mounts drop with
  their wall), though the map still had it: there it stays (DV504).

Checked in the real client by script: on a new map of three levels, a wall attached on a wall's top (the level
follows), one beside it and one on the floor of the upper level; a filled rectangle of floor three levels high; a
table replaced by a chair; the levels above hidden and shown; the level switched; Home; the top view moved by W and D
with no button held; the wheel over the docks and the letter n in the search; the borders between the docks.

Tests: `Client/Editor/MapEditsTests` (the replace rule), `Client/Interaction/WorldPickerTests` (a ray from outside
the map), the protocol's limits (`Protocol/CodecTests`, `CodecV2Tests`), `MapDocumentTests` (64 levels).

Not yet: shapes that are not the same on every level (a dome, a stair); the level below drawn fainter; the top view
of one level alone with the levels under it hidden.

### 5.9 The preview (ME6a, done; the owner, 2026-11-17)

The owner: in place of the blue square on the tile, what is about to be placed is shown, the thing itself, with the
blue of the highlight on it.

The preview is made by the world behind the view, as the placed thing will be made (`EditorFeed.SetPreview`): the
tiles the tool would change are loaded as it would leave them, without the document being changed, so the preview has
the thing's model, its joints with what stands beside it (a table joins the tables, a wall the walls, a pipe the
pipes) and the wall it hangs on. Nothing but the game's own init could show that (section 3).

- **What is shown**: the brush on the tile under the mouse, as the armed mode would place it (added, replacing what is
  there, attached, mounted on the wall under the mouse); while a shape is dragged, on every tile of the shape, and
  under the mouse the zone a zone's click would fill; on every level of the height. A copy that follows the mouse
  (paste) is shown as a click would paste it. A preview of more than 48 tiles is not made: those tiles are marked as
  before (5.5).
- **The blue**: the world's materials draw it, so a model, a picture, a wall and a floor take it alike whatever draws
  them (tg's `color` var, which the first version set on the previewed member, left the 3D models of chairs, girders
  and pipes as they were). The view gives the shaders a list of boxes (`PreviewTint`; `light_grid.gdshaderinc`,
  `anomaly_previewed`, the globals `anomaly_preview_*`): one per previewed thing, the box that holds the shapes a click is
  tested against (`PreviewBoxes`), and one per previewed turf, its tile's block for a wall and its floor's plane for a
  floor. A surface inside a box of its kind (a turf's for the floors' and the walls' materials, a thing's for every
  other) gets a blue cast and a little blue of its own, so a dark thing shows it too and a thing's own picture stays
  readable. A picture turned to the eye is asked where it stands, since its depth on the screen is not its own
  (`depth_order.gdshaderinc`). With no preview the count is 0 and the shaders read nothing: the game is as it was. The
  highlight is drawn around the previewed thing's own shape once the world shows it, where it was a square on the
  tile. Until then (the world answers within a few frames) the square is drawn.
- **It is none of the document's**: saving, Play, the search, the Tile panel and undo do not know it. A thing of the
  preview is no member of its tile (`EditorFeed.PreviewMember`, `IsPreview`), a pick passes through it, and a
  previewed turf is a wall or not for a pick as the document's turf is (`WorldPickAdapter.PickSkips`,
  `WallOverride`): so the mouse stays over what is really there, and the preview does not move itself.
- **When**: the world is asked once the mouse has rested 70 ms on a tile, so a mouse that crosses the map asks for
  nothing; a tile that changed is shown as the document has it, and a tile the click would not change (the thing is
  there already) shows no preview. A preview that moves is two small loads, the tile left and the tile reached.
- `View, Preview of what the place tool places` turns it off (`editor layer preview off`, kept in the settings): the
  square on the tile is drawn as before.

What it costs: a previewed tile is loaded as an edit is (20 to 100 ms on MetaStation, section 6), once when the mouse
comes to rest and once when it leaves. An area has no preview (it has nothing to draw).

Known limits of the blue: another thing that stands inside a previewed thing's box takes it too; a thing that a
previewed thing's init makes beside itself (an APC's terminal) does not.

A tile's turf is not laid on top of the one before it (`MapEditSeam.SetTiles` loads its template with
`should_place_on_top` off): as tg lays a shuttle or a ruin, every edit and every preview of a tile put the old turf
under the new one, and after a handful the tile had more base turfs than tg allows (`baseturfs_string_list`, over ten),
which stopped its load with a runtime. The editor's world needs no base turfs; a played round loads the map's text anew.

Checked in the real client by script: a table previewed beside two tables, joined to them, and gone when another
brush was picked; a wall; a lamp on the north wall under the mouse, then placed by a click as one lamp; on
MetaStation a table and a wall previewed with no long frame. The blue by the shaders: a chair, a girder, a pipe, a
wall, a wood floor, a crowbar, a cable, an airlock, a lamp and an APC, each in the blue, and nothing blue with the
select tool; a copy of two tables and a chair shown where a click would paste it.

Tests: `Client/Editor/EditorFeedTests` (a preview is shown by the world and is none of the document's: its thing has
no member and the tile's other things keep theirs, it moves, an edit of its tile takes it away, and with no preview the
view is the one before it; the previewed tiles say whether their turf is the preview's own; a tile previewed twelve
times over loads every time), `Client/Editor/PreviewBoxesTests` (a turf's box, a thing's box over its shapes, the
picture's box where it stands, the cap).

### 5.10 The map's settings (ME7, done; R7, R8)

A map names its station and has a sky, a sun, an air, a mine, and a weather and a base turf of each level. The mapper sets them in the **Map panel**
(a tab of the right top dock, behind the Tile), and the view shows the sky and the sun at once.

**Where they are kept.** The map json: the one of a maps folder that names the map's file (`map_jsons/NAME.json`, tg's
layout), else a json of the file's own, beside it with its name (`mymap.as3dmap`, `mymap.json`: tg's four keys with
`map_path` "." and the file's name). A lone map that sets nothing has no json of its own. The json is written when the map
is saved, tg's own keys as they were; a map saved under another name takes its settings and its sky's picture along.

**A map joins a maps folder by being saved in it** (the owner, 2026-11-17: a new map saved into a maps folder stayed a
lone one, since only a json names a map and nothing wrote one but the package, unpacked by hand). A save (Save, Save
As, a new map's first) of a file at a station's place of a maps folder, `map_files/NAME/NAME.as3dmap` of a folder that
has `map_jsons` (`EditorFeed.PlaceOf`), that no json names writes `map_jsons/name.json` itself, settings or none: the
json the package writes (`map_path` `map_files/NAME`, `map_file` `NAME.dmm`), with what the map's own json held. The
json's name is the file's in small letters, a plain word as the server's `--map` takes (`EditorFeed.JsonName`: `_` for
anything but letters, digits, `_` and `-`). From then on `EditorFeed.FindJson` finds it: the Map panel's shuttle slots
are enabled and Play loads the folder (5.7). A json the map had beside its file at that place is taken away, its text
being in the folder's now; one that could not be read is left. A json of that name that is another map's is not
written over: the map stays a lone one and the save's notice says so. Only that place joins: a shuttle's file, CentCom's
or a second file of a station's folder gets no json.

- **A file named straight in `map_files`** of a maps folder gets tg's place there, a folder of its own name
  (`EditorSession.Placed`: `map_files/Fort.as3dmap` is `map_files/Fort/Fort.as3dmap`), in File, New, in Save As and in
  the console's `editor new` and `saveas`.
- **File, New map** offers that place: its file window opens in the `map_files` of the open map's maps folder, else of
  the game's own (`EditorFeed.GameMaps`); a new shuttle's in that folder's `shuttles`. A new station whose file is at a
  station's place is saved at once, json and all (`EditorOpen`, and `--editor=PATH` of a new file), so that it is the
  folder's map before anything is built: a map that is not on disk is played from a scratch folder. A new map anywhere
  else is written at its first save, as before.
A change of the settings is a step of the document's history (`MapDocument.Record`, `EditorSession.Recorded`): undo
and redo give the settings and the json back as they were then, among the tiles' steps in the order they were made.
Changes of one setting that follow each other within a second are one step, so a slider dragged is taken back at once.
A map whose every change was taken back needs no save.

| Setting | In the json | In a round |
|---|---|---|
| The map's name | tg's `map_name` | What the game calls the map |
| The station's name | `anomaly.station_name` | tg's `station_name()`: before the config's name and tg's random one (DV505) |
| The sky | `anomaly.sky`: `kind` (`space`: the game's own with its planet, moon and rocks; `stars`; `colours`; `image`), `top` and `horizon` (`#rrggbb`), `image` (a file beside the map's), `stars` (their brightness, 0 to 4) | Sent with the map (protocol 87, `MapLook`); the picture's bytes travel with it, at most 4 MiB |
| The sun | `anomaly.sun`: `on`, `color`, `energy` (0 to 8; 0.7 the game's), `size` (the disc's radius in degrees; 2), `elevation` (1 to 90 degrees; 35), `azimuth` (degrees clockwise from north; absent: it goes round as tg's) | The light's strength and colour, the disc and the flare are the client's (protocol 87); a fixed azimuth fixes tg's `SSsun`, so the solar panels face it (DV505) |
| The air inside | `anomaly.air.inside`: a gas mix as tg writes one, `o2=22;n2=82;TEMP=293.15` (moles of each gas on a tile, kelvin) | A floor of the station's levels that has tg's default mix starts with this one (DV505: `turf_open.Initialize`); a floor whose type has an air of its own keeps it |
| The air outside | `anomaly.air.outside` | The mix of the station's levels' turfs that are open to a planet's sky (tg's `planetary_atmos`). Space is always a vacuum |
| A level's weather, its gravity | tg's own `traits`, an object a level: `Weather_Ashstorm`, `Weather_Snowstorm`, `Weather_Rainstorm`, `Weather_Sandstorm`, `Weather_Voidstorm`, `Gravity` | tg's `SSweather` and its gravity. A json that gets traits gets tg's default linkage for every level, which tg gives only while a map names none; when every level is the default again the key goes |
| A level's base turf | tg's own `traits`: `Baseturf`, a turf's path. The panel offers tg's own (no trait: `/turf/open/space`), open space over space (`/turf/open/space/openspace`), open air over a floor (`/turf/open/openspace`) and Other, any turf's path typed in (a path the game has no turf of is told and not set) | What a turf of the level becomes when it is destroyed or scraped away to its bottom (tg's `/turf/baseturf_bottom` in `ChangeTurf`). With tg's own, a hole in an upper level is plain space that nothing falls or looks through |
| The mine | tg's own `minetype`: absent (tg's own, `lavaland`), `none`, `lavaland`, `ice` (the values `datum_map_config` and `SSmapping.loadWorld` read) | `lavaland` loads the maps folder's Lavaland as the mining level; `none` and `ice` load none, so a small map's round starts much sooner |

**A new map of several levels** (File, New map; `EditorSession.New`) is space on its first level
(`/turf/open/space/basic`) and open space on the levels above it (`/turf/open/space/openspace`,
`MapDocument.Create`'s upper members), and its json gets the `Baseturf` trait of open space for each of those levels,
with no step of the history. A new shuttle's map is tg's noop tile on every level and gets no trait. A new map whose
json names traits already keeps them.

`MapSettings` (`Tg/Infrastructure/MapLoader/MapSettings.cs`) reads and writes the key: a value that is missing, of
another kind or outside its range is the game's own, only what differs from the game's own is written, and the game's
own of everything writes no key. The host reads it when the map is mounted (`MapSeam.Mount`, `MapSettingsSeam`).

**The sky** of two colours is a shader of its own (`sky_colours.gdshader`): one colour overhead, one at the horizon, a
darker shade of it below, and the sun's disc drawn as the starfield draws its own, so it is there in the editor's
unshaded look too. The picture is a panorama (twice as wide as high, its middle row the horizon) laid around the map;
choosing one copies the file beside the map's file, where the json names it. No sky gives light (D116): what is open to
it is lit by the sun.

**In the editor** the view's sky and sun are the settings as they are, saved or not, and a change shows at once (the
sun goes to a fixed place at once, where a round's glides). The air and the weather are a round's: Play runs the map
with the settings as they are (the map json's text goes with the map's, `PlayWorld`).

The console's `editor setting KEY [VALUE]` sets one by name for a scripted check: `station_name`, `map_name`, `sky`,
`sky_top`, `sky_horizon`, `sky_image`, `stars`, `sun on|off`, `sun_color`, `sun_energy`, `sun_size`, `sun_elevation`,
`sun_azimuth [DEGREES]`, `air_inside [MIX]`, `air_outside [MIX]`, `weather LEVEL ash|snow|rain|sand|void|none`,
`baseturf LEVEL [TURF]` and `minetype [none|lavaland|ice]` (no value: tg's own).

Checked in the real client by script: the sky of two colours, the picture and the stars alone in the view; the sun's
colour, size and height; the panel; the json a save wrote; a round played from it with the sky, the sun and the
station's name in its greeting; a new map of three levels with the mine set to none and two base turfs set by
`editor setting`: the panel's Mine row and each level's base turf row (a preset, and Other with its path), the three
steps taken back by undo, and the json a save wrote.

Not yet: tg's other json keys in the panel (the shuttles are ME8; the ruin levels, `job_changes`); a
level's linkage; a preview of the weather in the view; ambient light from a planet's sky.

Tests: `Tg/MapEditor/MapSettingsTests` (core: read, written, the defaults, bad values, a gas mix),
`Client/Editor/PlayWorldTests` (a round has the station's name, the room's air, the fixed sun, the weather trait and
its client the sky; the json saved and read again; a lone map's json beside its file; a new map's open space above
and its base turf, the base turf and the mine written, read back and taken back by undo; the round of Play's own
server config has the upper level's base turf under what is scraped away there, and loads a mining level for tg's own
mine and none for `none`), `Protocol/ProtocolV87Tests`,
`Client/Editor/NewStationTests` (a new map saved at a station's place is the folder's, its json written and read
again, its shuttle slot kept there; a file named straight in `map_files`; a lone map saved into the folder and one
that stood there with its own json; a name another map's json has; the server plays it by its json's name).

### 5.11 Shuttles (ME8, done; R6)

A shuttle is a map of its own, loaded over a dock as a template. tg keeps it as a file of `_maps/shuttles/PORT/` and a
type in the code for every shuttle (its name, its description, its price); the station's map json names the shuttle of
each slot, and the station's map has a dock for each (a stationary docking port).

- **A shuttle's map is opened and edited as any map.** File, New shuttle (`editor newshuttle PATH W H`) makes one: a
  map of one level whose every tile is tg's tile that leaves the map under it as it is (`/turf/template_noop`), so a
  shuttle of any outline loads over its dock without the tiles around it. The mapper builds the hull and places one
  docking port of the shuttle's own (`/obj/docking_port/mobile` or its family's type: `.../supply` for cargo).
- **A shuttle needs no code** (DV506): a file `shuttles/PORT/PORT_NAME.as3dmap` of a maps folder that has no template
  type of its own gets a template made from the file when a round starts (`SSmapping.preloadShuttleTemplates`,
  `MapSeam.ShuttleFiles`): its port id and suffix from the file's name, and its name, description and price from the
  json beside the file (`anomaly.shuttle`: `name`, `description`, `credit_cost`; no price: not for sale). The Map panel has
  the three fields when the open map is a shuttle.
- **The one-port check**: the Map panel says when a shuttle's map has more than one port of its own.
- **The docks' layer** (View, Shuttle docks; on): the border of the rectangle each docking port's shuttle takes, by
  tg's own numbers (`MapDocks`: `return_coords` from a dock's `width`, `height`, `dwidth`, `dheight` and `dir`). A
  shuttle's own port's rectangle is its whole map: tg takes a shuttle's size from its template
  (`calculate_docking_port_information`).
- **A map's shuttle slots** (the Map panel, for a map of a maps folder): the cargo, emergency, ferry and whiteship
  shuttles of the map json's `shuttles`, each picked from the files of `shuttles/SLOT` in the maps folder (or tg's own),
  with the map's dock for it (the stationary port whose `shuttle_id` is `SLOT_home`) and whether the shuttle fits it:
  tg's `canDock` on the shuttle's and the dock's four numbers, with how many tiles it is too wide or too long and where.
  What is wrong is written in red (no dock, a shuttle that does not fit, a named shuttle whose file is not there), a
  shuttle that fits in green (the owner, 2026-11-18). A slot the map does not name has tg's own shuttle (`cargo_box`,
  `emergency_box`, `ferry_fancy`, `whiteship_box`), measured like a named one when its file is in the maps folder; a map
  of no maps folder is measured against the game's own folder, whose shuttles its Play has.
- **A dock's own shuttle** has a row after the slots': a dock that loads a shuttle at the round's start names it in its
  `roundstart_template` var, the map's or its type's (the arrivals shuttle, the mining and labour shuttles, the escape
  pods, the auxiliary base). The row says the shuttle and where the dock is; the file is tg's own for the template,
  `shuttles/FOLDER/PORT_SUFFIX` (`MapDocks.TemplateOf`: the template's `prefix`, `port_id` and `suffix`).
- **A shuttle shown at its dock** (the owner, 2026-11-18). Every row has a **Show** box: ticked, the shuttle stands on
  the map where it docks, for this run of the editor. `MapDocks.Docked` stands it there as tg does (`action_load`,
  `initiate_docking`): the shuttle's own port on the dock's tile, the shuttle turned about it until it faces as the
  dock does, its things turned with it. A tile of the shuttle's map that is no part of it (`template_noop`) takes no
  tile of the map; where the shuttle has a turf the map's tile shows the shuttle's and keeps only its own docking ports.
  The tiles are shown through the preview (5.9), so the world makes them as it makes a placed thing and the document
  has none of it: nothing of a shown shuttle is saved, no tool acts on it and no click meets it (a click goes to the
  map's own tile under it). It is drawn as it is, with none of the preview's blue, and the rectangle it takes has a pink
  border (the dock's own is blue). The tiles are made again when the map, the shuttle's file or the slot's choice
  changes; a tile a tool previews on shows the tool's preview while it does.
- **What is wrong with a shuttle at its dock** is found for every row, shown or not: no dock, no file of it in the maps
  folder, more ports of its own than one, a shuttle that does not fit (tg's `canDock`), tiles that would stand past the
  map's edge. The row's line says it in red, and ticking Show opens a window that says each thing in a line; a shuttle
  that cannot be stood at a dock at all (no dock, no file) is not ticked. What a fitting shuttle stands on is a note in
  amber, not an error: tg docks it all the same and destroys the walls and things under it. Lattice and effects
  (landmarks, helpers) are not counted: tg's own stations have them under their docks.

Checked in the real client by script: a dock placed on a map and its rectangle drawn; a new shuttle's map made and
shown; on the Outpost the emergency and the arrivals shuttles ticked and shown at their docks, a slot with no dock
refused with its window, a dock made too narrow shown with the shuttle past it and its window; on MetaStation the
emergency shuttle of 350 tiles shown at 48 frames a second where the view had 51 without it. Found by it: a console
line that left the map (`editor new`, `open`, `newshuttle`) changed the scene from the middle of a frame; it is changed
at the start of the next one now, as Play's and Stop's are (5.7). And on MetaStation: a mining shuttle's file was looked
for under its port's name and not tg's folder, and tg's own lattice under a dock counted as destroyed.

Not yet: a slot's dock told by its `json_key` (it is told by its `shuttle_id`, `SLOT_home`); a shuttle's own vars in
a form (they are the port's, in the Properties panel); turning a shuttle's map. Not checked: a tool's preview on a tile
of a shown shuttle.

Tests: `Client/Editor/MapDocksTests` (a dock's rectangle by its vars and its dir, a shuttle's size, the fit, the cargo
shuttle and MetaStation's dock, a shuttle stood at a dock facing each way, what it stands on and what is past the map,
a dock's own shuttle by its template, the Outpost's shuttle in its dock), `Client/Editor/PlayWorldTests` (a shuttle of
the maps folder with no type of its own has a template in a round, with its json's data).

### 5.12 Ceilings (ME9, done)

The game puts a ceiling over a tile by a rule (D295: plating over what is indoors; nothing over space, the solars and
tg's outdoor areas; on a level with a level above, that level's turf is the ceiling). A map can say otherwise, tile by
tile (DV507): the `ceiling` var of the tile's turf, kept in the map's file as any var is
(`/turf/open/floor/iron{ceiling = "none"}`).

| `ceiling` | Over the tile |
|---|---|
| not set | The game's rule |
| `"none"` | Nothing: open to the sky (a courtyard, a hangar's mouth, a pit of a planet's base) |
| `"lattice"`, `"catwalk"` | A lattice or a catwalk |
| a floor type's path (`"/turf/open/floor/glass"`, `".../glass/reinforced"`, `".../engine"`) | That floor's block. A path that is no floor's leaves the rule |

The ceiling system reads it when the map is loaded (`CeilingSystem.Initialize`, `StartOf`) and when a template is
(a shuttle's own roof, an edit of the editor's world); in a round the ceiling is the system's own and is built and
broken there.

A tile under a level above has that level's turf over it and no ceiling of its own (D325). There the var lays a floor
tile under the plating of that turf: a floor type's path that is neither plating nor glass is the picture the tile
sees over it (`CeilingSystem.ShownAt`), for as long as the turf above has plating (a floor that is not glass, or a
wall). The rules of ceilings (support, air, damage) do not see it, and the clients get it as they get a ceiling. In a
round a player lays such a tile from below with a floor tile and pries it off with a crowbar (docs/ai/specs/ceilings.md,
"Under a level above"). The var's other words (`"none"`, `"lattice"`, `"catwalk"`, plating, glass) say nothing there.

**In the editor**: the Tile panel has `Ceiling` for the selected tile, and for every tile of a selected rectangle (one
step); the tiles whose ceiling the map sets are marked on the edited level, the ones open to the sky in blue and the
others in green; and the ceiling itself is seen from under the walls, where the view draws the room as a player sees
it (5.6), and there the tools work on it (5.18). The var is in the Properties panel with the turf's others.

On a level that has a level over it the Tile panel's `Ceiling` does what the place tool does from under the ceiling
(5.18): a floor's type is laid under the plating of the level above, glass takes that floor's place, plating or the
game's rule takes a laid tile off; nothing, a lattice and a catwalk are refused with a line that says why. The checks
name a word that lays no tile there, and a tile with no plating over it (`ceiling_under_level`, 5.13).

Tests: `Client/Editor/PlayWorldTests` (a round's ceilings are the map's: none, glass, a catwalk, the rule for a value
that is no ceiling, the rule's plating, none over space).

Not yet: a ceiling's marks drawn as the ceiling's own picture; ceilings shown from above as a see-through layer.

### 5.13 The checks, the program and the package (ME10, done)

**The Checks panel** (a tab of the dock under the view; `editor checks`) lists what `MapChecks` finds on the map, with
the tile, what is wrong and the member it is about; a row selects the member and brings the camera over its tile, and
the tiles are boxed in red in the view while the panel is in front. The map is checked again when it changes.

| Rule | Finds | From |
|---|---|---|
| `multiple_airlocks`, `multiple_firelocks`, `multiple_chairs`, `multiple_closets`, `multiple_girders`, `multiple_grilles`, `multiple_lattice`, `multiple_stairs`, `multiple_tables`, `multiple_unary_components` | Two of the family on one tile | tg's lints of those names (`tools/maplint/lints`) |
| `multiple_machinery`, `multiple_windows`, `identical_cables`, `identical_pipes`, `identical_disposal_pipes` | The same one twice on a tile (written alike) | tg's |
| `wall_stacking`, `window_spawner`, `grille_stacking` | An airlock, a lattice or a window in a wall; a window spawner in a wall; a cable under a plain grille | tg's |
| `disposals_without_trunks` | A disposal unit with no trunk under it | tg's |
| `space_area` | A floor or a wall in the area of space itself (not tg's near-station area): the room has no area | the editor's own |
| `wall_mount` | Wall furniture of a directional type with no wall and no full window on its side | the editor's own |
| `no_arrivals` | No late-join and no start landmark: nobody can join a round of the map | the editor's own |
| `ceiling_under_level` | On a level that has a level over it: a `ceiling` that is no floor tile to lay there, or a laid tile with no plating over it, open space or glass (5.12) | the editor's own |
| `shuttle_ports` | A shuttle's map with more than one port of its own | the editor's own (5.11) |

A test holds tg's rules to tg's own station: they find nothing on MetaStation, which passes tg's lint. Whether a slot's
shuttle fits its dock is told in the Map panel (5.11).

**A program of its own.** The editor is the client built with the map editor in, started with `--editor`. The owner
(2026-11-19) asked that the game's own client carry no server: the editor's world and Play are the game's server inside
the program (`Anomaly.Server`), so the editor is built apart (this replaces the owner's 2026-11-18 "a wrapper that starts the
game with the editor's arguments, not a second build"). The client's project builds it in when `AnomalyEditor` is true: then
it references `Anomaly.Server` and the `.dmm` converter's sources and defines `ANOMALY_EDITOR`; otherwise `scripts/Editor/`,
`scripts/Shared/Editor/` and `GameScene.Editor*.cs` are left out, and `GameScene.NoEditor.cs` holds the few of their members
the game's code reads, inert (`EditorOn` is false). `AnomalyEditor` is on by default for a build in Godot's editor or by
`dotnet` (Debug, Release) and off for an export (ExportRelease, ExportDebug); `tools/test-quick.sh` builds the game's client
as ExportDebug, so the stand-ins keep up. The editor's arguments (`--editor`, `--editor-size`, `--editor-settings`) and
`ClientArgs.EditorByName` are the editor's build's only: the game's build takes `--editor` as an unknown option (the owner,
2026-11-19). The server's tests build the client's `Shared` code as the editor's (`ANOMALY_EDITOR`).

Each export script exports the project twice: the game, and with `AnomalyEditor=true` in the environment (which MSBuild reads
as a property) the editor, in the export's `editor/` folder (`editor/AnomalyEditor.exe`, `editor/AnomalyEditor.x86_64`, on a Mac
`editor/Anomaly Editor.app`, its bundle, executable and pack renamed by `export-macos.sh` before signing): its .NET data folder has the game's name, so it cannot stand beside the game's. It reads the
game's data beside it (`DataPaths` looks one folder above its executable, and above its `.app`; `TgStrings.Locate` and the
game's maps are found upward). The scripts check that the game's assemblies hold no `Anomaly.Server.dll` and the editor's do.
There is no launcher beside the game (the owner, 2026-11-22): the player opens `editor/` and runs the editor's executable, whose name starts it as the editor. The editor's executable under a name that holds "AnomalyEditor" or "MapEditor" (with or without a hyphen, an
underscore or a space between the words) starts as the editor by itself (`ClientArgs.EditorByName`), as the Windows and
Linux ones are named. The editor's build carries its own copy of the .NET runtime and the native libraries, which makes
the export's zip larger by about their size.

**The map's package** (File, Export the map's package; `editor package ZIP`): one zip laid out as a maps folder, for a
server's owner to unpack into the server's `maps`: `map_jsons/NAME.json` (the map's json with its settings, naming the
file's place in the package; its name is `EditorFeed.JsonName`'s, the one a save into a maps folder writes, 5.10), `map_files/NAME/NAME.as3dmap`, the sky's picture beside it, and the shuttles the json's
slots name with their own jsons (`shuttles/SLOT/ID`). The map is saved first: the package holds the files as they are.
A server started with `--map NAME` plays it.

**The mapping guide** is `docs/mapping-guide.md`: the editor for a mapper, start to package.

Checked in the real client by script: a map with two airlocks on a tile and a lamp on no wall checked (the panel, the
marks), and its package written with its json and file. The launchers' texts were written by the export's function
and read; no export was run.

Not yet: tg's lints of var edits (pixel shifts, names), which need tg's list of allowed edits; tg's map unit tests
that need a round (areas with no APC, pipes that lead nowhere); a package unpacked by the editor (a map is made in a
maps folder without one, 5.10); the exports
themselves, on any platform, since the launchers were added.

Tests: `Client/Editor/MapChecksTests` (what must not share a tile, the identical and the exact rules, the editor's own
checks, the list's limit; MetaStation passes tg's rules), `Client/Editor/EditorFeedTests` (the program's name, a size of
64 levels).

### 5.14 Turns, several rectangles, the clipboard (ME11, done; the owner, 2026-11-17)

The owner: a button that turns what is selected (chairs, crates, spawners, anything that has a way to face), and a
selected part of the map turned too; several parts selected at once with Shift; a selection copied in one editor and
pasted elsewhere on the map or on another map in another editor.

**The turn** (`Turn` in the tool bar, R with the select tool, Shift for the other way; Edit, Turn right and Turn left;
`editor rotate [ccw]`), a quarter each time, one undo step:

- **A selected thing** turns where it stands (`MapTurns.Turned`). A thing hung on a wall of its tile by its type
  (tg's `/directional/north` families: lamps, alarms, windoors) becomes the type of the next wall. Any other thing gets
  its `dir` written, turned from the way it is seen to face: the game turns some things by rules of its own (a closet
  away from the wall behind it), so the turn starts from what the view shows and not from what the map says. Its
  pixel offsets turn with it. A turf turns the `dir` it has; an area has none.
- **Selected tiles** turn about the middle of the box that holds them (`MapTurns.Turn`, `TurnBox`), every member
  turned with its tile. Tiles that differ in their turf or their area (a room with its walls) turn whole: the tiles
  they leave become the level's empty tile, as when a selection is moved. Tiles of one floor and one area (furniture
  on a floor) keep their floor: only what stands on them turns, and takes the place of what stands where it goes. A
  box whose sides are an odd and an even number of tiles has no tile in its middle: it steps half a tile one way on a
  turn and back on the next, so four turns bring every tile home. A turn that would reach past the map's edge is not
  made. A selected tile turns what stands on it.

**A thing faces as its map says (DV508).** The 3D models' facing rule (`ModelDirRules`, DV56: a modelled closet, crate
or machine is turned away from the wall behind it when it is made) took no notice of the map: a closet the mapper
turned was turned back at the round's start. A thing whose map sets its `dir` is noted when the loader sets the var
(`MapSeam.NoteMapDir` in tg's preloader), and the rule leaves it alone; tg's random spawner gives its `dir` to its pick
(`_random.dm:77`) and the note goes with it. A thing with no `dir` in the map is turned by the rule as before, so a
closet placed against a wall still faces the room by itself.

**Several rectangles.** With rectangles selected, Shift and a drag selects one more on the same level (Shift and a
click: its one tile); without Shift a drag is a new selection. They are moved, copied, cut, deleted, turned and given
a ceiling together. A copy of them is the box that holds them with no tile where none was selected
(`MapEdits.Copy(map, cells, z)`; `MapClip.Tiles` holds null there), so a paste leaves the tiles between them as they
are. `editor region add X0 Y0 X1 Y1`.

**The clipboard.** A copy is also put on the system's clipboard as a text (`MapClip.ToText`: a header, the size, and a
line of members for each tile it holds, as a map file writes them), and a paste takes what the clipboard holds when it
is such a text (`MapClip.Parse`), so a copy made in one editor is pasted in another, into another map. Any other text
on the clipboard leaves the editor's own last copy in use. A scripted run leaves the system's clipboard alone
(`editor clipboard on` turns it on).

Not yet: a copy that follows the mouse turned before it is pasted (paste it, then turn it: the pasted tiles are the
selection); rectangles of several levels in one selection; a docking port's box (`dwidth`, `dheight`) turned with it.

Checked in the real client by script: a closet the game shows facing east turned to the south and shown so; a chair
and two tables turned as tiles; two rectangles selected by Shift and a drag, copied (6 tiles) and pasted with the
tiles between them left alone; the copy pasted by Command + V and a click in a second editor.

Tests: `Client/Editor/MapTurnsTests` (a dir, a member by its dir, its wall or the way it is seen to face, a box that
comes home after four turns, tiles turned with what stands on them, furniture on one floor, several rectangles copied,
moved and pasted, the clip's text), `Client/Editor/EditorFeedTests` (a closet faces away from its wall, and as the map
says when it says), `Server/MapDirTests` (a random spawner's pick faces as the map turned the spawner).

### 5.15 The map patches folded, the maps checked in (ME11, done; the owner, 2026-11-17, D337)

The owner: with the editor the patch files are of no use; what they did is done in the editor.

Until now the maps were tg's, converted into a git-ignored `maps/`, and the owner's changes of them lay beside them in
`map_patches/<map>.json`, applied by the server once a map was loaded: 390 turns of things on MetaStation (closets,
vending machines, racks, canisters: what a 3D model draws and tg's sprite does not turn), three things taken away
there (the table under the power monitoring console, DV365; two start landmarks no one could walk out of, DV353), and
on the multi-z debug map the airless open space round the upper decks made space, with the tiny fan on the top deck's
airlock taken away (DV411).

- **The maps are the source and are checked in** (D109's plan, D337): `maps/` is no longer git-ignored, and
  `tools/convert-maps.sh` converts tg's maps only into a folder that holds none (`--force` writes them over the checked-in
  ones). The export and server scripts copy `maps/` as it is.
- **The patches are in the maps' own files**: a turn is the thing's `dir` var; where the thing is a random spawner's
  pick, which changes every round, it is the spawner's `dir`, which tg's spawner gives to its pick (DV508 makes the game
  keep both against the models' facing rule, through a spawner among the loot too); a thing taken away is not in its
  tile; the debug map's turfs are the other type. 373 of MetaStation's turns went into its file (77 of them on a
  spawner) and three into the maps of the shuttles that stand there at the round's start, the aux base's (a closet and a
  canister) and the mining shuttle's (a crate), turned back by the half turn the shuttles are docked with.
- **Eight turns were left out on purpose**: seven atmospherics components (a tank compressor at (132, 81), the
  freezers at (79, 107) and (143, 121), the portable connectors at (155, 107), (55, 123), (155, 131) and (155, 132))
  and the disposal outlet at (141, 116). Such a machine reads its `dir` when it is made and keeps what it read: the
  side its pipe joins, the way it throws. A patch turned its looks alone, after its init; a `dir` in the map would turn
  what it does too, and the pipes tg laid for it would no longer meet it. They face as their pipes say again; where a
  model looks wrong for its pipe, the model's own orientation in the catalog is what to change. (A mech bay port, whose
  four turns are folded, reads its `dir` again whenever it is turned, as tg's `setDir` has it, so its turn was never
  looks alone.)
- **Three could not be folded**, because no map has the thing: crates at (54, 113) and (61, 131), which a round had
  moved there, and a hard hat at (61, 113), an item, which steered nothing before either. Three more were second turns
  of one spawner's tile with the same way.
- **The patch files, their code and their options are gone**: `map_patches/`, `MapPatchFile` and `MapPatches` (the
  server), `MapPatchReader` (the client's scripted map), `--map-patches-folder`, `--map-edit` and the dev command
  `ceiling save`. The dev commands `rotate`, `mount` and `ceiling` stay, for the round.

Tests: `Integration/MapDirGameplayTests` (MetaStation through the real server: every modelled thing whose map says the
way it faces holds it through the round's start and the client sees it, about 300 things of which 79 are a spawner's pick;
the dev commands turn a closet and hang a fire alarm for the round), `Server/MapDirTests`, `Integration/
MultizDebugBreachTests` (the debug map's decks vent into space, from the map's own file now). The patch tests
(`MapPatchGameplayTests`, `MapPatchFamilyTests`, `MapRemovePatchTests`, the patch file's ceiling test) went with the
code.

### 5.16 The keys (ME12, done; the owner, 2026-11-17)

The owner: settings in which the keys of every function can be changed.

Every function of the editor has an id, and one table says which keys start it (`HotkeyMap`, engine-free; the table of
functions with the keys they start with is `EditorActions` in `GameScene.EditorKeys.cs`). A key pressed over the view is
looked up there and its function is run (`EditorDo`); a menu's item and a tool bar's button run the same function by its
id. A menu shows its item's key and does not take it: the keys are the view's, so that a field that is typed in keeps
its letters, and a menu that took a key too would run the function twice.

| Section | Functions (the keys they start with) |
|---|---|
| File | New map (command + N), new shuttle, open (O), save (S), save as (Shift + S), import a `.dmm`, export the package, quit (Q) |
| Edit | Undo (command + Z), redo (Shift + Z, Y), cut, copy, paste (X, C, V), delete (Delete, Backspace), turn right (R) and left (Shift + R), find (command + F), the Hotkeys window |
| Tools | The four tools (1 to 4), the next place mode (Tab), each place mode and each shape (no key) |
| Levels | Level up (=, Shift + =, the number pad's +, Page Up, .), level down (-, the number pad's -, Page Down, ,), the lock (L), the levels above |
| View | Focus (F), Home, the top view (T), the simple look and its parts, the layers of 5.5, the preview, the arrows, each panel, Play (F5) |
| Camera | Held while the camera flies: forward, back, left, right, up, down (W S A D E Q) and faster (Shift) |

The command key is Command on a Mac and Ctrl elsewhere. A function may have several keys; a key is a key of the keyboard
with any of Ctrl, Shift, Alt and the command key, by the key's place on the keyboard and not by the letter a layout
gives it.

- **The Hotkeys window** (Edit, Hotkeys): the functions by section with their keys. `Set key` waits for the next key
  pressed (Esc leaves the function as it is) and gives the function that key alone; `No key` takes its keys away;
  `Default` gives it the keys it started with; `All defaults` does that for every function. A key another function had
  is taken from it, and the window says which lost it. A function whose keys were changed is marked.
- **A held function** (the camera's flight) is asked for while its key is down and is started by no press, so its key
  is taken without modifiers. It is the same key as that key pressed alone or with Shift (the view has the keys while
  no command key is down), and another than that key with Ctrl, Alt or the command key: S flies back and the command key
  with S saves.
- **Fixed**: Esc; the mouse's buttons with their modifiers (Ctrl + click picks, Shift with the erase tool and with a
  selection); F8 in a played round, which is the round's own.
- Undo, redo and the level's two functions go on while their key is held; the others run once a press.
- The tips of the buttons and the status bar's lines name the keys as they are.

The changed functions are kept in the editor's settings (section `keys`, 5.4): what differs from the defaults, so a
default that changes in a later version reaches a mapper who never touched it. The console's
`editor key FUNCTION KEY|none|default` sets one (`cmd+shift+z`, `f5`, `equal`: the engine's names of the keys),
`editor keys` lists them and `editor do FUNCTION` runs one.

Tests: `Client/Editor/HotkeyMapTests`.

### 5.17 The tools on any level; the erase tool's layers (ME12, done; the owner, 2026-11-17)

**The lock.** The owner: with ten levels built, a block could not be added on the first without changing the level by
hand; the tools should work on any level, with a button that keeps them on the edited one.

- **Not locked (the default)**: a pick's ray goes down through the open tiles to what is seen there, a thing, a wall
  or a floor of a lower level, so a tool builds on it, erases it, picks it or selects it where it is. Where the ray
  finds nothing but space, the edited level's open tile is the hit after all: a click on empty space builds on the
  edited level and not on the lowest one.
- **Locked** (`Lock` in the tool bar, L, View's item, `editor lock on|off`; kept in the settings): every tool works
  on the edited level alone, and its open tiles are clicked, not what is seen through them. It is how a hole over a
  built level is closed.
- The place tool's preview is drawn by the world but is no part of the map: the pick takes a previewed tile as the
  map has it, for whether it is a wall (`WorldPickAdapter.WallOverride`) and for whether a ray goes down through it
  (`VoidOverride`).
- **From any place.** The camera may be far above the map, far under it, or to a side of it: the ray is taken from
  where it comes over the map's rectangle, so the pick begins on the level the ray is in there (`EditorPickAt`,
  `EditorEntersMap`). A ray that comes up to the edited level from under it does as one from above does: what is seen
  on the way up is the hit; with nothing seen, the edited level's tile where the ray meets its floor; and locked,
  that tile whatever hangs under it.
- **A held tool goes on at the height its button went down at**: where the mouse is on nothing, or on another level,
  the tile of the stroke's own level there (a rectangle of ceiling dragged across open space, 5.18).

**What was wrong** (the owner, 2026-11-17, on a map of ten levels seen from a side): nothing could be placed from far
away, only from almost on the level. The pick began on the camera's level and gave up where the ray crossed that
level's floor, which from beside the map is outside it; the checks of the list above had all looked straight down.

**The erase tool takes away what is highlighted.** The owner: Shift with the erase tool on a wall took the floor too,
sometimes several tiles; and plating could not be taken apart from the tile on it. Cause: a cleared tile showed what
was behind it, and the held tool took that as well.

| The button goes down on | A click takes away | What is left |
|---|---|---|
| A thing | That thing | The tile as it was |
| A wall | The wall | Its plating; what stands there and the area stay |
| A floor tile (any floor of tg's that is laid on plating) | The tile | Its plating, airless where the tile was |
| Plating, or any other open turf | The floor | The turf and the area of the level's empty tile; what stands there stays |
| A ceiling, from under it (5.18) | Its top layer | Plating under a tile; nothing under plating, a lattice or a catwalk |
| Anything, with Shift | The whole tile | The level's empty tile |

- **A held erase goes on with the same kind** over the tiles the mouse crosses (`EditorErase`): walls, or floor tiles,
  or floors, or ceilings. The tiles are found where the mouse's ray meets the height the button went down at, so a
  still mouse stays on its tile whatever it uncovers. A thing is taken by the thing under the mouse, and the next one
  only once the mouse has moved a few pixels.
- **A shape** (a line, a rectangle, a circle, a zone) takes the pressed kind on its tiles: pressed on a thing it takes
  the things, pressed on a wall the walls.
- The ceiling the map set over a tile is the tile's, not its floor's: a turf placed or left in a turf's place keeps
  the `ceiling` var (`MapEdits.Place`).
- The line at the mouse says what a click would take.

`MapEdits.LayerOf` names a turf's layer (a wall, a tile, the base) and `MapEdits.Peel` takes one away.

**A level's empty tile** (the owner's test station, 2026-11-17) is what every tool leaves where it takes a tile or its
turf away: the erase tool (with Shift, the floor's last layer, a floor taken whole from under it), Delete and Cut on
tiles, the place a moved or a turned selection leaves, the Tile panel's `Empty tile` and its Delete on a turf or an
area, and the console's `erase`. It is the map's default tile with the level's base turf for its turf, where the map
json names one (tg's `Baseturf` trait, 5.10), and the default tile itself on a level that names none. The default
tile's area stays. So a tile emptied on a level over another is open space again, where it had been the default
tile's plain space, which nothing falls or looks through. A shuttle's map (its default tile is tg's noop tile) leaves
the noop tile on every level, whatever its json says. `MapDocument.Empty(z)` gives the tile; the editor's session
supplies it from the json as it is, saved or not (`EditorSession.EmptyOf`), so a base turf changed in the Map panel,
or taken back by undo, holds for the next erase. The tiles that are empty already are not changed with it.
Checked in the real client by script: on a new map of two levels the console's `erase` of a floor's turf and Delete
on a selected tile of the upper level leave `/turf/open/space/openspace` (the Tile panel names it, its `Empty tile`
button is off, the saved file has no plain space on that level), and Delete on the first level leaves the default tile.

Tests: `Client/Editor/MapEditsTests` (a turf's layer; a wall, a tile and plating peeled, with what stands there, the
area and the ceiling kept; a stroke that takes one kind), `Client/Editor/PlayWorldTests` (a level's empty tile: the
erase, the clear, the move and the turn on a level with a base turf, on the first level and on a shuttle's map).

### 5.18 Ceilings by the tools (ME12, done; the owner, 2026-11-17)

The owner: a ceiling could be set only in the Tile panel; he wants to look at it and place a tile there, take it off
with the erase tool, select the floor, the ceiling and the plating apart, and attach other blocks to a ceiling.

**What a ceiling is.** A tile has one turf, and tg's turfs stand on one another: a floor tile lies on plating, and a
wall stands on plating (its `baseturfs`; the erase tool leaves that plating, 5.17). A glass floor has no plating: it
is one pane, seen through from both sides. Over a tile:

| The tile's level | Its ceiling is | Seen from under it as |
|---|---|---|
| Has a level over it | The turf of the tile above (D325): a floor, or the plating a wall stands on; and, laid under that plating, the floor tile the tile's own `ceiling` var names (5.12) | The laid tile; with none, plating; a glass floor as the glass it is |
| The top one of the map | The map's own word (the `ceiling` var, 5.12), else the game's rule | That floor's picture |

So a floor between two levels has two sides. The owner (2026-11-17): a tile is placed on the side that is looked at,
on the plating from above and under it from below; glass takes the plating's place, and no other tile does.

From above the walls a click goes through a room's ceiling, which is not drawn. With the camera under the tops of the
walls of the top level shown (the edited one while the levels above are hidden) the view draws the ceilings and the
undersides of the levels above (5.6), and there they are what the mouse is on, from inside a room and from far under
the level alike:

| Tool | On a ceiling, from under it |
|---|---|
| Place, a floor's type that is not glass | It is laid on the ceiling. On the top level it is the ceiling (the `ceiling` var of the tile under it). Under a level above it is laid under the plating of the tile above (the same var), and that tile's floor stays as it is; where the tile above is open space or glass, there is no plating to lie under and that tile is made of plating (`MapEdits.Ceiled`). Plating itself takes a laid tile off. With such a brush the place where a ceiling would be is clicked too, over a tile that has a floor. A click, a held stroke or a shape |
| Place, glass | On the top level it is the ceiling. Under a level above it takes the place of the floor of the tile above, plating and all, and a tile that lay under that plating goes; a wall of that level stays |
| Place, a lattice or a catwalk | The top level's ceiling; under a level above it is placed on the tile above where that has neither a floor nor a wall |
| Place, anything else (a wall, a lamp, a table) | It goes under the ceiling, on the tile the ceiling is over: a wall hangs from it, a lamp is in the room. In the replace mode it takes the place of what was clicked instead (on the top level's own ceiling that is refused, with a line that says why) |
| Place in the replace mode, what makes a ceiling | As above, on the ceilings of the shape that show what the clicked one showed (`MapEdits.UndersideOf`) |
| Erase | The top level's own ceiling loses its top layer: a tile's leaves plating; plating, a lattice or a catwalk leaves nothing (`"none"`). Under a level above the laid tile comes off and the plating stays; with no tile laid, the floor or the wall of the level above goes whole, down to the map's default turf (`MapEdits.Unturfed`) |
| Pick | What makes the ceiling: the laid tile's type, else the type of the floor above; on the top level its floor's type, a lattice or a catwalk |
| Select | The ceiling over the tile: the Tile panel says what it is, its frame is the ceiling's slab, and Delete takes its top layer off (under a level above, the laid tile). Under a level above with no tile laid: the turf of the tile above, framed on its underside |

- What is over a tile is the map's own word where it has one, else what the world shows there by the game's rule
  (`EditorCeilingAt`). A layer taken off a rule's plating is written as `"none"`.
- A stroke or a shape that began on the floor does not take to the ceiling, nor the other way.
- A ceiling's height is its level's: the tool bar's height builds none over it.
- The place where a ceiling would be, in a room taller than a level, is the top of its walls (the owner, 2026-11-25): where
  the tile over the empty ceiling is open and something stands on each side of it on that level before the map's edge,
  the ray goes on up, to the top level shown (`EditorRoomGoesUp`). A room under open space keeps its own level's
  ceiling; a floor between the two levels of a tall room is placed from above.
- Plating made over a room from under it is in the room's area where its own tile had none (the map's default): it
  would be a floor in the area of space, which the checks name.
- The preview shows no ceiling ahead: the line at the mouse says what a click does, and what it would not do (glass
  where a wall stands, a lattice where there is a floor).
- What is made from under it does not make its level the edited one: it is seen as the ceiling.
- With the levels above hidden, the floor of the first of them is still the ceiling of the edited level and is drawn
  from under it: it is clicked and its selection's frame is drawn, though its level is not shown.
- The mark of what the place tool would put under a ceiling is on the ceiling, where the mouse is; a wall's is its
  block.
- Not drawn, so not clicked: the underside of the map's first level (the game has no place to see it from).

**What was wrong** (the owner, 2026-11-17, on a map of ten levels):

- No ceiling could be placed or changed on any level but the top one. The tools wrote the `ceiling` var, which the
  game leaves out under a level above; from far under a level the place tool, with a floor chosen, was on the empty
  ceiling of the lowest level and not on the underside the mouse was on. The checks of the list had been made on a
  map of one level.
- A wall placed on a floor seemed to delete the floor: from under it the tile showed the wall's own picture and not
  plating (a rule of 2026-11-10 for the game's multi-z view: "a wall needs no floor under it"). The owner: a wall
  takes the place of the floor's tile at most, never of its plating. The underside of a wall of the level above is
  plating now, in the editor and in the game (`ChunkPlanner.UnderLevelAbove`), and the Tile panel says of a wall or
  a floor tile that it is on plating.

- A tile placed from under a level went on the floor's top (the owner, the same day): seen from under it nothing
  changed but for glass, and glass came with the plating's picture behind it. A level under another had no ceiling
  tile of its own in the game at all. It has one now (5.12, DV507), the tools lay a tile on the side looked at, and a
  glass floor has no underside tile: its one pane is drawn to both sides (`TurfStyle.PlatingUnder`; its underside
  had been the plating's picture drawn see-through).

Tests: `Client/Editor/MapEditsTests` (what makes a ceiling, a ceiling's layers, a floor placed under a set ceiling; a
tile laid under a level's plating, glass in the floor's place, plating made where there is none),
`Client/Editor/PlayWorldTests` (a round shows the laid tile under a floor and a wall, not under glass or open space,
and follows a change of the floor above and an edit; a player lays one from below and pries it off), `Client/World/VerticalJoinTests` (the underside of a wall is
plating), `Client/World/ChunkPlannerTests` (no underside tile under a glass floor), `Client/Editor/MapChecksTests`
(a ceiling var under another level).

Checked in the real client by script, on a map of 10 x 10 x 10 with a floor on level 4: from 19 m above and beside
the map and from 12 m under it, a click, a held stroke and a dragged rectangle of the place tool (add, attach), of
the erase tool and of the select tool; from inside a room of level 4, a ceiling made, selected, changed to glass and
taken away, a lamp placed under it, with the levels above shown and hidden; the top level's own ceilings; the lock
from under the level; and a map of one level as before. Then, with the tile under the plating: carpet, a circuit
floor and glass placed from 12 m under a floor (the floor's top stays iron; glass is one clear pane), picked,
selected, deleted and erased there; a tile placed from inside a room where there is no ceiling (plating is made over
it); a rectangle of tiles; and the saved map opened again.

### 5.19 The arrows (ME12, done; the owner, 2026-11-17)

The owner: a setting that shows by an arrow which way the things of the map face; a thing still drawn as a flat
picture shows one side to every viewer, and the way it faces cannot be read off it.

`Arrows` on the view bar (View's item; no key until one is given; kept in the settings) draws a flat arrow over each
thing of the shown levels that has a way to face, pointing that way, through what stands on its tile
(`FacingArrows`, one draw call). The way is the one the world shows, so a closet the game turned away from its wall
(DV56) shows where it faces in a round.

- **What gets one** (`FacingMarks`): furniture, machines, mobs and vehicles whose picture has a side for each way or
  that a 3D model draws; and the markers whose way goes to what they make, a random spawner (its pick takes it,
  DV508) and a landmark, whatever their picture.
- **What gets none**: a picture with one side; the runs that join their neighbours (cables, pipes, disposal pipes,
  lattices), what fills its tile (tables, windows, grilles, girders, airlocks, firelocks), and what hangs on a wall
  (lamps, signs, cameras, APCs), whose dir is the wall's side.
- They are found again when what the world shows has changed, and every two seconds besides: a picture that is still
  being read has no sides yet.

Tests: `Client/Editor/FacingMarksTests`.

### 5.20 The marks, the level's buttons, the border, the New window (ME12, done; the owner, 2026-11-17)

**What the mouse is over, and what is selected.** The owner: over a cable or a pipe under the floor the whole tile was
highlighted; in the select tool the mouse's highlight should be fainter and the selected thing's stronger and of
another style; and what is clicked should stay marked when the mouse goes elsewhere.

- **The mouse's mark** is the game's highlight, a see-through slab, with a thin frame around it in the same blue
  (`SelectionMark`). Both are around the drawn pixels of a flat picture, as around a standing one's
  (`HoverHighlight.BoxOf`), so a cable is a line across its tile and not the tile. In the select tool both are fainter.
- **The selection's mark** is a thick orange frame with a faint fill, drawn through the walls, so that what is
  selected is seen from anywhere and stays when the mouse leaves: a thing by the shapes a click is tested against, as
  the world draws it (one frame around all of them); a wall by its block; a floor, an area or a whole tile by the
  tile's square; a ceiling by its slab. The frames' lines are as thick on the screen at any distance.

**The level's buttons.** The owner: the number box's arrows were too small to hit. The tool bar has a button down, the
level with how many the map has, and a button up, each a large triangle; + and - are their keys (5.16).

**The border** is a box of lines around all the map's tiles, from the first level's floor to the top level's top, with
a ring at every level's floor and a post at each corner; the edited level's part is the bright one. (It was one level
high whatever the map's levels.)

**The New map window** says where a station's file goes (5.10) and counts the tiles as the numbers are typed, the levels among them. (A number box takes its text
only on Enter or when it loses the keys: 10 x 10 x 10 said 100 tiles, the levels typed last and not yet taken. The
numbers are taken before the map is made, too.)

### 5.21 Autosave (ME12, done; the owner, 2026-11-17)

The owner: the map saved to a temporary file now and then, and an offer to restore it when the editor crashed or was
closed with changes that were not saved.

- **The copy.** While the open map has something to save, a copy of it is written every minute to
  `map_editor_autosave/` beside the editor's settings file (the user's data folder; a scripted check's own folder):
  the map's text, the map json as a save would write it, and a record (the map's file, the time, the editor's
  process). It is written only when the map has changed since the last copy, never in the middle of a stroke, and
  once more before Play starts a round (`MapAutosave`, `GameScene.EditorAutosave.cs`). The map's own file is not
  touched.
- **Taken away** when the map is saved, and when it is left on purpose: another map opened or the editor closed, saved
  or not ("Do not save" is an answer).
- **Offered again.** A copy that is there when the editor starts was left by an editor that crashed or was stopped. A
  window lists such copies, the latest first, each with its map's file and the time it was kept, and whether the
  map's file was saved after it (restoring would take that save back). `Restore` opens the copy as the map, with its
  changes as changes that are not saved (`EditorSession.Restore`): the title has its star, the view shows the copy and
  not the older file, and a save writes it to the map's file. `Discard` takes a copy away; `Later` leaves them for the
  next start.
- **Several editors.** A copy is its editor's own, by its process: two editors with one map open keep one each, and
  the copy of an editor that still runs is not offered.
- The console's `editor autosave` writes the copy now and `editor restore [N]` lists the copies and restores one; a
  scripted run is asked nothing by itself.

Tests: `Client/Editor/MapAutosaveTests` (a copy kept, offered and restored as unsaved, with the map's settings; a map
of a maps folder shown from its copy; a new map that was never saved; two maps of one name, two editors of one map).

### 5.22 The palette as a tree; areas erased alone; the level that stays (ME13, done; the owner, 2026-11-17)

Three notes of the owner from a day of mapping.

**The lists of types are trees.** Each of the palette's four lists, and the dev menu's spawn panel in a round, shows its
types as a tree by their paths: `/obj/machinery/power/rtg` stands under power, under machinery. `SpawnTree`
(`godot/scripts/Shared/Dev/`, engine-free, tested) builds it from the list the filters leave and gives the rows to show
for the branches that are open. The part of the path every type of the list shares is no level (the Objects start at
machinery, item, structure; a category's list starts under its own prefix), and a type that is that very path stands
first. A branch says how many types it holds; one that is no type of the list (a base class the Abstract box leaves
out) has no picture and folds by its whole row; one that is a type is picked by its row and folds by its arrow or by a
second click. The open branches are kept for each list while the program runs. The turfs' list holds the full-tile
windows of `/obj` too, so `/turf` is a branch there and starts open. A search, and the favourites, list their types
flat with their whole paths, as before: a match is found by reading, not by walking down to it. A type picked off the
map is unfolded to. Both lists stay virtual: only the rows in view are drawn.

**Areas are erased in the palette's Areas, and only there.** An area is under no mouse: the erase tool took what the
mouse was on, a wall or a thing, when the mapper meant the area he had just painted. While the palette shows the areas,
`EditorEraseFor` gives the erase tool one kind, the area (`MapEdits.Unareaed`: the tile gets the area of the map's
default tile, its turf and its things stay), with Shift too, for a click, a held stroke and every shape; and Delete does
the same for the selected tile and the selected rectangles. In the other three lists the tools are as 5.17 has them.

**The edited level changes only by the mapper.** A tool that built above the edited level, a block attached on a
block's top, used to raise the edited level so that the block was seen while the levels above were hidden; the next
click on empty space then built a level higher than the mapper meant. Now the level stays and the view opens up to the
level that was built on (`EditorSeeBuilt`: the top level shown is the edited one or that one, `EditorTopShown`), with a
line that says so. A change of the level, or of Above, shows the levels by their rule again. With Above on nothing
changes: every level is shown.

**After Play and Stop (the owner, 2026-11-18).** Stop makes the editor's scene again on the map's own world, and the
renderer plans it from the map as the connection first received it; what was built since comes with the first rebuild of
its chunks, a few frames later. Two things went wrong there. A panel hung on a wall built since the map was opened was
planned before its wall was known and stood as a picture turned to the camera: `MapRenderer.HangAgainAround` now plans
again what stands beside a cell whose wall came or went, where the renderer's table of walls changes (`RebuildTurfs`),
so a wall taken down leaves no panel in the air either. A floor laid where the received map has space had its first
picture asked for when the icons already load on worker threads, got the flat tint of a missing picture and kept it: a
chunk made while a sheet of its pictures was loading is made again when a load lands (`_turfsAwaitingIcons`).

## 6. Measurements (ME0, 2026-11-16, the owner's Mac)

`godot-mono --path godot --resolution 1600x900 -- --fake-server --fake-map --fake-bots=0 --camera=free --view=128,128,110,0,-89 --quality=low`:

- MetaStation opens with no server: the world is built in 5.1 s (114 chunks, 3,833 walls, 12,195 floors, 12,735 props).
- The whole station from 110 m above: about 26 frames a second on the low preset with the game's light.
- The game's view is almost black from that height: its distance fog ends 40 tiles from the camera (the options' `fog`), so
  everything below is faded into the background. The editor draws a map with no distance fog.
- The editor (ME2), the same view in the simplest look at the full resolution: about 33 frames a second with the middle of
  the station on the screen. A copy of MetaStation opened and saved by the editor is byte for byte the file it was.
- The view through the game's world (ME3, debug builds): MetaStation alone (no CentCom, no Lavaland) is shown after 24 s in
  a test and 34.5 s in the client (tg's init of the atoms 10 s, the map's load 3 s; 21,971 things; about 1.9 GB after a
  collection); a 7 by 6 room after 3.5 s; a new 24 by 16 map after 9.5 s in the client and a second map of the same session
  after 0.7 s. An edit of MetaStation is loaded and shown in 40 to 110 ms, 20 ms once the path is warm. Atmospherics in the
  simplest look: 99 frames a second.
- The layers on MetaStation (ME4; 2560 by 1440, most of the station on the screen from 42 m): 28 frames a second as
  the map opens; 27 with the areas' layer; 20 with the under-floor layer, which shows 8,422 more things (30,393). The
  markers are 1,658 things. A search of the whole map lists its 32 command airlocks with no pause.
- The far view (ME5; 2560 by 1440, the simplest look, debug builds). A wide view is limited by its draw calls, not by
  the scene's own code (5 of a frame's 35 ms): 11,595 for most of the station from 42 m, 20,450 for the whole map from
  330 m. With the small parts not drawn from afar (5.6): the whole map from 330 m at 32 frames a second, from 14 (8,742
  draw calls); most of the station from 42 m at 34, from 29 (9,338); a view from 12 m at 71, as before. The top view:
  43 frames a second at 80 m (61 by 109 tiles), 84 at 30 m. The game's look with tg's light from above: 25 from 42 m,
  51 in the top view at 60 m. The map's triangles: 3.2 million in the shaped pieces (8,704 of them, each the whole kit
  of 372), 2.5 million in the models (22,768 instances of 1,676 meshes in 9,815 batches), 1.1 million in the tiles.
- Play (ME6, 2026-11-17; debug builds, 2560 by 1440). A room in a new 24 by 16 map: the round is joined 2.6 to 3.8 s
  after Play, a second one after 1.8 to 3 s, and Stop shows the editor again within a second. MetaStation, measured
  with a second editor open on MetaStation on the same machine, so on a loaded one: the round (the station, CentCom
  and Lavaland) is joined after 60 s and keeps tg's 20 ticks a second (519 ticks in 26.3 s); the program holds 7.2 GB
  while it is played, against 4.3 GB for an editor with MetaStation open and no round; Stop builds the editor's scene
  again in 11 s. In the round a frame took 57 ms on average (17 a second): 42 of them the renderer's (7,845 draw
  calls; the editor's own view of the same place drew 22 frames a second), 14.5 the scene's code; one frame in five
  had a collection, the round's world allocating on its own thread, and took about 14 ms longer.

## 7. Not in the first version

- Several mappers in one session.
- A flat 2D view of sprites: the top view of the 3D scene takes its place.
- Writing `.dmm`.
- Free heights and free angles: a thing stands on tg's grid with its direction, its pixel offsets and its vars.
- Sending a map from the editor to a server: maps travel as files.

## 8. Candidates the owner has not decided

A room brush (walls, floor, area and ceiling in one drag) and a border brush for floor markings; saved groups of objects
to stamp again; overlays of the power nets, the pipe nets, the disposal routes, the doors' access and the areas; a run of
the map with no player that reports errors and rooms without power or air; a picture of the whole map; a view of what
changed between two versions of a map; areas defined by data, so a new room's name needs no code.
