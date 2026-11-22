# Mapping guide

How to make and change maps with Anomaly Editor, the game's map editor. The editor is part of the game: it needs no server and no code.

## 1. Start

- From a release: open the `editor` folder beside the game and run `AnomalyEditor.exe` (Windows), `AnomalyEditor.x86_64`
  (Linux) or `Anomaly Editor.app` (Mac). The game's own program has no editor.
- From the sources: `godot-mono --path godot -- --editor=PATH`. A PATH that does not exist yet is a new map of
  `--editor-size=W,H,Z` (64 by 64 tiles, one level, when the size is not given).
- **A new station**: File, New map asks its size and then its file. The file window opens in the `map_files` of a maps
  folder (the open map's, else the game's own `maps`): type the station's name there and the map is made as
  `map_files/NAME/NAME.as3dmap` and saved at once, with its json `map_jsons/name.json`. It is that folder's map from
  then on: it has the folder's shuttles and CentCom when you play it (sections 7 to 9). A file named anywhere else is a
  lone map, written when you first save it.
- The editor's program (`editor/AnomalyEditor.exe`, `editor/AnomalyEditor.x86_64`) starts as the editor when double-clicked:
  a copy of it whose name holds `AnomalyEditor` or `MapEditor` does too.
- File, Recent maps lists the last ten maps you opened or saved, the newest first: a click opens one.

A station takes half a minute to open: the editor shows a map by starting the game's own world on it, so what you see
is what the game will draw. A small map opens in a few seconds. The editor starts in a small window in the middle of the
screen: its name, and a bar along the bottom that fills as the world starts, with what it does now written over it.

## 2. The window

| Part | What it holds |
|---|---|
| Menus | File (new map, new shuttle, open, save, import a `.dmm`, export a package), Edit (undo, copy, paste, turn, find, Hotkeys), View (the levels, the layers, the arrows, the panels, the interface's size) |
| Tool bar | The four tools, the shape, the place tool's mode, the height, the level with its two buttons, Lock, Above, undo and redo, Turn, Play |
| View bar (over the 3D view) | Focus, Home, Top view, T-ray, Arrows, and the look's switches |
| Docks | The panels: Palette, Tile, Map, Properties, Log, Search, Checks. Drag a panel by its title to another dock; drag the borders to size them |
| Status bar | What is under the mouse, the last notice, where the camera is |

## 3. The camera

| Do | To |
|---|---|
| Hold the right mouse button | Fly: the mouse turns the view, W A S D move, Q and E go down and up, Shift is faster, the wheel changes the speed |
| Hold the middle button (or Alt with the left) | Turn around what is selected; with Shift, move across the view |
| The wheel | Go toward the point under the mouse, or away from it |
| F | Bring what is selected into the view (the whole map when nothing is) |
| Home | Go to the map's first tile, 1, 1 |
| T | The top view: straight down, no perspective. W A S D move the map, Q and E change its scale |

The yellow line is the map's border: no tool does anything outside it.

## 4. Building

Pick a type in the **Palette** (objects, turfs, mobs, areas; search by name or path; star your favourites) and click
on the map. What is about to be placed is shown under the mouse, joined to what is beside it.

Each of the palette's lists is a tree by the types' paths: machinery, under it power, under it the RTG. Click a
branch's arrow to unfold it; a branch says how many types it holds. A branch that is a type itself is picked by its
row, and a second click on the row unfolds it. A search, and the favourites, list their types flat, each with its
whole path. A type picked off the map (Ctrl + click) is unfolded to in the list.

| Tool | Key | Does |
|---|---|---|
| Select | 1 | Click a thing or a tile; drag over tiles to select a rectangle, and with Shift one more; drag the selection to move it; R turns it; Delete takes it away. What is selected keeps an orange frame |
| Place | 2 | Click or drag to place the palette's type. R turns it |
| Erase | 3 | Click or drag to take away what is highlighted, a layer at a time (below); with Shift the whole tile becomes the level's empty tile |
| Pick | 4 | Click a thing, a turf or a ceiling to place more of it, with its vars. Ctrl + click does it with any tool |

**The erase tool takes what the mouse marks.** On a thing: that thing. On a wall or a floor tile: the wall or the
tile, and its plating stays with all that stands there. On plating: the floor, and space is left. So a tile comes
apart a layer at a click. Held and dragged it goes on with the same kind (walls, or floor tiles), and a mouse that
stands still takes one tile however long the button is held. A shape takes the kind you press on, over its tiles:
press a wall and a rectangle takes the walls in it.

**Areas are erased in the palette's Areas.** An area is under no mouse, so while the palette shows the areas the erase
tool takes a tile's area and nothing else, with Shift too, and so does Delete on the selection: the tile gets the map's
default area, and its walls, floors and things stay.

**Shapes** (place and erase): free, a line, a rectangle or a circle (their border or filled), and the zone of a clicked
tile (the tiles joined to it that have its turf and its area: a room's floor).

**The place tool's modes** (Tab goes through them):

- **Add**: on the tile under the mouse. A turf takes the tile's turf, an area its area, anything else is one more thing.
  Clicked against a wall, a lamp, an alarm or a sign hangs on that wall, facing the right way.
- **Replace**: what you click gives its place. With a shape, every one of the clicked kind in the shape is replaced and
  the rest is left: a room's iron floor becomes wood and its walls stand.
- **Attach**: on the tile the clicked face looks at. Click a wall's side to build beside it, its top to build on the
  level above.

**An empty tile.** Where a tool takes a tile away whole (erase with Shift, a floor erased down to nothing, Delete or
Cut on selected tiles, the place a moved or turned selection leaves, the Tile panel's `Empty tile`), what is left is
the level's empty tile: space on a station's map, and on a level with a base turf (the Map panel, section 7) that
turf. A new map's upper levels have open space as their base turf, so a tile emptied there is open space again, which
things fall through and the level below is seen through. A shuttle's map always leaves tg's noop tile.

**Several levels.** `Level` in the tool bar (its two buttons, or + and -) is the level you edit; the levels above it
are hidden unless `Above` shows them. The camera stays where it is when the level changes. `Height` makes a click or a shape go up through that many levels: a rectangle of
walls with a height of 3 is three levels of wall.

The level you edit changes only when you change it. Build on a block's top and the block is on the level above: the
view then shows that level too, so that you see what you built, and the next click on empty space is still the edited
level's. `Above`, or a change of the level, hides it again.

A tool works on what the mouse is on, on any level you see and from any distance, with the camera above the map, under
it or to a side of it: with ten levels built, click a block of the first and the tool builds on it, erases it, picks
it or selects it there. A click on empty space is the edited level's. `Lock` (L) keeps every tool on the edited level
alone: use it to floor over a hole through which a lower level is seen.

**Floors, walls and plating.** A tile has one turf. A floor tile lies on plating and a wall stands on plating: place a
wall on a floor and it takes the tile's place, not the plating's. The erase tool takes the wall or the tile and leaves
that plating; once more, and the plating goes. From under a level you see that plating, under floors and walls alike.

**Ceilings.** Fly under the walls (the right mouse button, then S or Q) and look up, or look at a level from under it:
the ceiling is drawn, and the tools work on it.

- On a level that has a level over it, the ceiling is the floor of the level above, and that floor has two sides. A
  floor's type placed from under it is laid under its plating, on the side you look at; the floor on top stays as it
  is. Where there is no floor above yet, plating is made there, in the room's area. Plating itself takes a laid tile
  off again.
- Glass is the one floor that takes the plating's place: placed from above or from under, it is a pane seen through
  from both sides.
- On the map's top level the ceiling is the map's own: any floor's type, a lattice or a catwalk, and the Tile panel's
  `Ceiling` sets it for the selected tiles from above too.
- Anything else you place on a ceiling goes under it: a wall hangs from it, a lamp is in the room. The replace mode
  takes the place of what you clicked.
- The erase tool takes a ceiling a layer at a time: the tile first. Then, on the top level, the plating; under a level
  above, the floor of that level, whole.
- Pick and select work on a ceiling as on a floor.

**Turning.** With the select tool, `Turn` in the tool bar (R; Shift + R the other way) turns what is selected a
quarter: a thing where it stands, or the selected tiles about their middle with all that stands on them. Furniture
selected on one floor turns without the floor; a room with its walls turns whole, and the tiles it leaves become the
level's empty tile. A thing faces in the game the way you turned it. A thing you never turned may be turned by the
game itself: a closet, a crate or a machine with a 3D model faces away from the wall behind it. Turn it once, or set
its `dir` in the Properties panel, and it stays as you put it.

**Copies.** Copy, cut and paste work on the selected rectangles together and between editors: copy in one window,
paste in another, into another map.

Cables and pipes join what is beside them by themselves. A disposal pipe drawn free or by a line turns along the way
it is drawn. Ctrl+Z takes a step back.

## 5. A thing's vars

Select a thing: the **Tile** panel lists what its tile holds, and the **Properties** panel every var of its type.
Type a value into a var's cell to set it on this thing (`"text"`, a number, `null`, a path, a `list(...)`); the cross
gives the type's own value back.

The Tile panel also sets the **ceiling** over the selected tile or rectangle: the game's rule (a ceiling over what is
indoors), none (open to the sky), glass, a catwalk, any floor.

## 6. Seeing more

- **T-ray** (view bar): the cables, pipes and disposal pipes under the floor. The mouse marks the cable or the pipe
  itself, and a click selects it.
- **Arrows** (view bar): an arrow over every chair, closet, machine, mob, spawner and landmark, pointing the way it
  faces. A thing drawn as a flat picture shows you the same side wherever it faces: the arrow tells.
- **View, Markers**: the landmarks, spawners and mapping helpers. **Areas**: a colour over each area.
  **Shuttle docks**: where a docked shuttle stands.
- **Search** (Edit, Find on the map): type words of a type's name or path, or a var the map sets
  (`airlock command`, `name = "Bridge"`). Every find is boxed on the map, through the walls; a row goes to its tile.
- **Look**: `Simple look` is the fast view. Switch it off for the game's lighting, shadows and bloom; fly under the
  ceilings to see a room as a player does.

## 7. The map's settings

The **Map** panel (beside the Tile panel):

- the map's name and the station's name;
- the **mine**: tg's own (a round loads Lavaland as the mining level, as for a map that says nothing), none, Lavaland,
  or ice (the map's own levels are the mine). A small station with no mine wants **None**: its round starts much
  sooner without Lavaland;
- the **sky**: space as the game has it, the stars alone, a planet's sky of two colours, or your own panorama picture
  (twice as wide as high, a png or a jpg of at most 4 MB; it is copied beside the map and travels with it);
- the **sun**: on or off, its colour, strength, size and height, and whether it stands still;
- the **air** a room starts with, and the air outside on a planet, written as tg writes a gas mix
  (`o2=22;n2=82;TEMP=293.15`: moles on a tile and kelvin);
- each level's **weather**, gravity and **base turf**: what a turf of the level is when it is destroyed or scraped
  away. tg's own is space, which nothing falls or looks through; a level over another wants open space
  (`/turf/open/space/openspace`), a level over a planet's floors open air (`/turf/open/openspace`), and Other takes any
  turf's path;
- the **shuttles** of the map's slots and of its docks, with whether each fits its dock, and a **Show** box that
  stands the shuttle on the map where it docks (section 9).

They are saved in the map's json: `map_jsons/NAME.json` of a maps folder, or `NAME.json` beside a lone map's file.
Undo takes a changed setting back like any other step.

A new map of several levels starts ready for building up: its first level is space, the levels above it are open
space, and each of those has open space as its base turf already.

**A maps folder** is a folder laid out as the game's `maps`: `map_jsons` with a json for each station, `map_files` with
a folder for each station's files, `shuttles`. A map is a maps folder's when a json there names it, and the editor
writes that json itself: save a map as `map_files/NAME/NAME.as3dmap` of a folder that has `map_jsons` (Save, Save As,
or New) and `map_jsons/name.json` is written with it. A file named straight in `map_files` is given its own folder
there. The status bar says when a save made the map the folder's; if a json of that name is another map's already, it
is left alone and the map stays a lone one: choose another name. A lone map has no shuttle slots to choose.

## 8. Play

**Play** (F5) starts a round on the map as it is, unsaved changes too, and puts you on the selected tile as an admin:
the dev menu and the server's console commands work at once. The choice beside the button says where you start: **On
the tile**, or **As an arrival**, which seats you in the map's arrivals shuttle as a player who joins a round in
progress (a map with no arrivals shuttle's dock and no late-join landmark has no such start, and Play says so). **F8** (or Stop, or "Back to the editor" in the Esc menu)
ends the round and returns to the editor as you left it. Nothing done in a round changes the map.

A map of a maps folder is played with that folder's CentCom and shuttles. A lone map is played alone, with the game's
own shuttles: its docks load them (the arrivals shuttle at its dock), and the shuttles that start at CentCom wait at
stand-ins of CentCom's docks, on a level of their own. This works on a map of any size.

## 9. Shuttles

A shuttle is a map of its own. File, New shuttle makes one: build its hull and place one docking port of the
shuttle's own (`/obj/docking_port/mobile`, or its family's: `.../supply` for the cargo shuttle) where it meets its
dock. Save it in a maps folder as `shuttles/PORT/PORT_NAME.as3dmap` (`shuttles/cargo/cargo_mine.as3dmap`: the file
window opens in the folder's `shuttles`); the Map panel sets its name, description and price. A station names it in its shuttle slots (the Map panel) and needs a dock
for it: a stationary docking port whose `shuttle_id` is `PORT_home`, with the `width`, `height`, `dwidth` and
`dheight` of the room the shuttle takes. The Map panel says when a shuttle does not fit. The slots are a maps folder's
map's (section 7): a lone map plays with the game's own shuttles.

To see a shuttle at its dock, tick **Show** in its row of the Map panel: the shuttle stands on the map where it docks,
turned as the dock faces, with a pink line around what it takes. It is not part of the map: nothing of it is saved and
the tools pass through it. A dock that loads its own shuttle at the round's start (the arrivals shuttle: its
`roundstart_template` var) has a row too. When the shuttle cannot stand there, a window says why: no dock, no file,
too wide or too long and by how many tiles, tiles past the map's edge. A line in amber says how many of the shuttle's
tiles stand on walls or things of the map: the shuttle docks all the same and destroys them.

## 10. Before you share a map

- Open the **Checks** panel: it lists what tg's own map rules forbid (two airlocks on a tile, a cable twice, an
  airlock in a wall, a disposal unit with no trunk) and what a new map often lacks (a room left in the area of space,
  a lamp hung on no wall, no arrival point). A row goes to its tile.
- A map players can join needs `/obj/effect/landmark/latejoin` and the jobs' `/obj/effect/landmark/start/...`.
- Give every room an area (the palette's Areas with the zone shape) and an APC, or it has no power.
- Play it.
- **File, Export the map's package** writes one zip with the map, its json, its sky's picture and its shuttles, laid
  out as a maps folder. A server's owner unpacks it into the server's `maps` folder and starts the server with
  `--map=NAME`: the json's name, which is the file's in small letters, with `_` for anything but letters, digits, `_`
  and `-` (`Fort Nine.as3dmap` is `--map=fort_nine`). A map you made in the game's own `maps` folder is there already.

## 11. Keys

These are the keys you start with. **Edit, Hotkeys** lists every function of the editor with its keys: choose one,
press `Set key` and then the key you want, with Ctrl, Shift, Alt or Command if you like. A key another function had is
taken from it. Esc and the mouse are fixed.

| Key | Does |
|---|---|
| 1, 2, 3, 4 | Select, place, erase, pick |
| Tab | The place tool's next mode |
| R | Turn what is placed; with the select tool, what is selected (Shift + R: the other way) |
| + and - (or Page Up, Page Down, or . and ,) | The level above, the level below |
| L | Lock the tools to the edited level, or free them |
| F, Home, T | Focus, the map's first tile, the top view |
| Ctrl/Command + Z, Shift + Z | Undo, redo |
| Ctrl/Command + C, X, V | Copy, cut, paste (click where it goes; a copy from another editor too) |
| Ctrl/Command + S, O, N, F | Save, open, new map, find |
| Delete | Take the selection away |
| Esc | Give up a drag or a paste, then put the tool away, then drop the selection |
| F5, F8 | Play, stop |

## 12. If the editor stops

While a map has changes you have not saved, the editor keeps a copy of them every minute. If it crashes or is killed,
the next start lists what it kept: **Restore** opens the map with those changes, still unsaved, and a save writes them
to the map's file. Saving, or closing the editor yourself, takes the copy away.

## 13. Limits to know

- A map is at most 255 by 255 tiles and 64 levels. A large map takes minutes to open and gigabytes of memory.
- Things stand on tg's grid: a tile, a direction, pixel offsets. There are no free heights or angles.
- What a thing's init picks at random (a poster's picture) shows one pick in the editor.

## 14. An example: the Outpost

`maps/map_files/Outpost/Outpost.as3dmap` is a small station made wholly in this editor, with its own emergency shuttle
(`maps/shuttles/emergency/emergency_outpost.as3dmap`). Open it to see how a station that works is put together, or
play it (`--map outpost`). It has four levels: engineering with atmos over it, a lift and a ladder between them, an
arrival and a departure corridor with their docks, a gravity generator, and outdoors, behind an airlock, a pad of
generators. `OutpostRoundTests` plays a round on it: power, light, gravity, air, both shuttles.

What tg's rules ask of such a map and of its crew:

- A canister placed on a connector stands loose: a wrench joins it, and until then the pipes get no air.
- An RTG makes its power (1 kW) from the round's start; a P.A.C.M.A.N. makes none until someone switches it on. Count
  what the rooms use (the power monitor shows it) against what the generators make.
- Anything outdoors needs a space suit on the way: put a suit storage unit by the airlock
  (`/obj/machinery/suit_storage_unit/standard_unit`, with `storage_type` set to `/obj/item/tank/internals/oxygen` for
  the air to breathe). Its wearer opens the tank's valve and switches the suit's thermal regulator on, or freezes.
- The emergency shuttle cannot be called in a round's first 20 minutes.
