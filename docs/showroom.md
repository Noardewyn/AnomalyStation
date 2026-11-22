# Showroom map

A map for checking the art: every object type MetaStation places, set out in rows, so its 3D model (or its sprite) can
be seen next to the others without walking the station. It was generated once from MetaStation's types; now it is
`maps/map_files/Showroom/Showroom.as3dmap` (config `maps/map_jsons/showroom.json`), edited with the map editor like any
other map, and loaded by the real server with `--map showroom`.

## Run

Start the server on it and join:

```bash
dotnet run -c Release --project src/Server -- --realtime --map showroom --no-interest --no-lobby
```

```bash
godot-mono --path godot -- --connect=127.0.0.1:7777 --name=Owner
```

| Option | Why |
|---|---|
| `--map showroom` | load the showroom instead of MetaStation (`maps/map_jsons/showroom.json`) |
| `--no-interest` | send every thing to the client, so the far rows are drawn too (without it only the things near you are) |
| `--no-lobby` | start the round at once, without tg's lobby and its countdown, whatever `config/server.txt` says (it has `LOBBY`); every join comes in as an assistant. Without it, press Start Now in the lobby |
| `--client-console` | type the server console's commands (`spawn`, `teleport me X Y`) in the client's dev console, to move about the rows quickly |

For screenshots without walking, the client's free camera can be put anywhere: `--camera=free --view=X,Y,EYE,YAW,PITCH`
(tile X, Y, eye height in metres, yaw 0 north, pitch negative down) with `--screenshot-after=S,PATH`.

## Layout

The map is 161 tiles wide and about 220 long, inside a reinforced wall, all of it tg's Test Room area
(`/area/misc/testroom`): it needs no power and is fully lit. Players come in at the bottom left (late-join landmarks, an
Assistant's and a Station Engineer's start).

1. **Every type alone.** Each type MetaStation places, with its commonest var edits there, in a 3 x 3 cell, one free
   tile between cells. Rows are sorted by type path, so a family stays together, and a new row starts at each top family:
   floor decals (`/obj/effect/decal`, `/obj/effect/turf_decal`) first, then `/obj/machinery`, `/obj/structure` and
   `/obj/vehicle`.
   - A wall-mounted thing (a `/directional/` subtype, a pixel offset of half a tile or more) hangs on a wall tile of its
     cell, on the side it is mounted on; a thing that sat on a wall tile on Meta (a sign) is on a wall tile.
   - A door (and a false wall) stands in a doorway: walls on both sides of it, so its leaves slide into them as on the station.
   - A thing that hides under floor tiles (pipes, cables, disposal pipes, ducts, terminals, vents, scrubbers, meters,
     nav beacons) stands on plating, so it shows.
2. **The joining kinds.** Tables, full-tile windows (each on a grille), grilles, catwalks, smart pipes and cables, each
   type as a lone piece, a run of three each way, a 3 x 3 block and a plus, so every junction of their smoothing shows.
3. **Seats.** Every sofa, bench and pew family of tg's type tree (all colours, not only Meta's): a left-middle-right row
   facing each of the four ways, and its corner and solo pieces each way.
4. **Turned kinds.** Railings (with the corner piece), conveyors (a run of three), transit tubes and directional windows,
   each turned the four ways.

Items, spawners, landmarks, mapping helpers and docking ports are left out; mobs are not objects.

## Expected warnings

The objects stand without their surroundings, so the server log has a few warnings that are not faults: duplicate APCs in
the one area, the airlock pumps that find no airlocks to cycle, and the shuttles' missing docking ports (no shuttle comes
to this map). Buttons, switches and consoles are not linked to what they work on Meta.

