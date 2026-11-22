# The Anomaly Station map format (`.as3dmap`)

Status: v1 (the M3 contracts step, M3.0), 2026-10-08. Owner of the format: this spec (frozen; changes through the lead).
Owner of the converter (`tools/Anomaly.Tools.Maps`) and of the engine reader (`Tg/Infrastructure/MapLoader/**`): M3 P2.
Design basis: the M3 scout's `scope.md` 2b (option B, "key-less tile lines"), D46, D108, D109.

## 1. What it is

Our own text form of a tg map (D46): diff-friendly (one edited object is one changed line), sparse (bare default tiles are
left out), chunked (32 x 32 tile sections that do not depend on each other) and in a stable order, so that two conversions of
one `.dmm` are byte-equal. It carries exactly what tg's map reader (`code/modules/mapping/reader.dm`) gets out of a `.dmm`
or TGM file: per tile the ordered list of atoms (movables, then the turf or turfs, then the area last) with their var
overrides as DM literals. Nothing else: no derived data, no appearance, no tg code. The values stay in DM literal syntax, so
the C# reader needs no DM compiler (D110: OpenDream is a local tool, never needed by the game).

Meta (255 x 255 x 1): 18,347 tile lines, 3,026,948 bytes (M3 P2's converter, 0.25 s; tg's TGM file is 2.29 MB). The format has no keys, so
tg's key churn on edits (and its need for mapmerge2) does not exist here.

## 2. Where maps live (D109)

- The maps live in the folder `maps/` at the repository root and are **checked in** (D337, 2026-11-17: the map editor edits them, so our
  format is the source of truth, as D109 planned). They were converted from the read-only tg clone (`../tgstation`, or `ANOMALY_TG`) by
  `tools/Anomaly.Tools.Maps`, and until then the folder was git-ignored and converted again by the scripts whenever it was stale. Tests that
  need a map of the folder find it there.
- Layout, mirroring tg's `_maps/` so that tg's `/datum/map_config` paths resolve unchanged:
  - `maps/map_jsons/<map json name>.json`: tg's map json copied verbatim (`_maps/map_jsons/metastation.json`, tg's
    `MAP_DIRECTORY_MAPS`: `map_name`, `map_path`, `map_file`, `shuttles`, `traits`, `job_changes`, ...);
  - `maps/map_files/<map_path>/<map_file stem>.as3dmap`: the converted map (`maps/map_files/MetaStation/MetaStation.as3dmap`);
  - `maps/.as3dmaps-stamp`: the tg commit and the tool version that first wrote the folder (`tg <commit>`, `tool <n>`,
    `format <n>`, one per line). `tools/convert-maps.sh` converts only into a folder that holds no maps; `--force` writes tg's
    maps over the checked-in ones (the edits made in the map editor are then lost from the files, and git shows what changed),
    and with `--map NAME` the tool converts that one.
- Test fixtures: hand-written small maps in tg's TGM or DMM syntax live in `tests/fixtures/maps/*.dmm` (checked in, M3 P2),
  converted at test time by the converter's library entry point.

## 3. The converter's command line (M3 P2 implements)

```
dotnet run --project tools/Anomaly.Tools.Maps -- [--tg <tg clone>] [--out <folder>] [--map <json name>]... [--all-station-maps]
                                              [--check] [--allow-bad-paths] [--types <types-index.tsv>]
                                              [--od-json <tgstation.json>]
```

- `--tg` (default `../tgstation`, or `ANOMALY_TG`): the tg clone. `--out` (default `maps`): the output folder.
- `--map metastation` (repeatable; default: `metastation`, D108): which map jsons of `_maps/` to convert, with every `.dmm`
  of their `map_file` (a list of files for multi-z maps).
- `--check`: convert into memory, compare with the files on disk, and exit 1 when they differ (the freshness check).
- `--types` (default `docs/ai/specs/generated/types-index.tsv`): the type index the validation reads (section 6). Without
  the default index (before P1's index is on main) the tool checks only the syntax and the member order, and says so.
- `--od-json`: OpenDream's compiled JSON of tg (D110, a local tool output), the source of the var check: P1's index has
  type and proc rows but no var rows (`var` rows, `var<TAB>owner<TAB>name`, are read when an index has them).
- `--allow-bad-paths`: write a map whose validation found unknown paths or vars (they are left out of the output and listed);
  without it such a map is refused (exit 2) and nothing is written.
- `--all-station-maps`: every json of `_maps/map_jsons` (all of tg's station maps convert and validate at 1d99bd7f).
- Exit codes: 0 done, 1 `--check` found a difference, 2 a validation error (or a round trip failure under `--check`, or
  a malformed `.dmm`), 3 a usage or I/O error. It prints one line per
  map (tiles, lines, atoms, distinct types, bytes, time) and the bad paths.
- A library entry point (`Anomaly.Tools.Maps.MapConverter.Convert(string dmmText, string sourceName, TypeIndex types)`)
  returns the converted text and the validation report, for tests and fixtures; `MapConverter.RoundTrip` is the
  round-trip check. The `default` line is the most frequent model (ties: the ordinal smallest member text).

## 4. The file

UTF-8 without a byte order mark, `\n` line ends, no tabs, no trailing spaces. A `#` starts a comment line (only at the
start of a line; the converter writes none except in the header). Empty lines separate sections and carry nothing.

```
anomaly-map 1
source _maps/map_files/MetaStation/MetaStation.dmm tg 1d99bd7f
size 255 255 1
chunk 32
default /turf/open/space/basic | /area/space

[chunk 129 1 1]
134,32: /obj/structure/lattice | /turf/open/space/basic | /area/space/nearstation
135,31: /obj/structure/lattice/catwalk | /obj/structure/marker_beacon/purple | /turf/open/space/basic | /area/space/nearstation
...

[chunk 97 33 1]
120,64: /obj/machinery/light/small/directional/north | /obj/effect/turf_decal/stripes/line{dir = 1} | /turf/open/floor/iron | /area/station/hallway/secondary/exit/departure_lounge
...
```

### 4.1 Header (in this order, each exactly once; `source` may be missing)

| Line | Meaning |
|---|---|
| `anomaly-map 1` | the magic and the format version (this spec is version 1) |
| `source <path> tg <commit>` | the converted file, relative to the tg clone, and the tg commit (informative; the converter always writes it, the reader accepts a file without it, such as the M3 scout's probe output `meta.as3dmap.b`) |
| `size <W> <H> <Z>` | the map's extent: `world.maxx`/`maxy` it needs, and its number of z levels (1 to 255, 1 to 255, 1 to 64) |
| `chunk <N>` | the chunk side in tiles; always 32 in version 1 |
| `default <members>` | the members (section 4.3) of every tile that has no line |

### 4.2 Chunks and tile lines

- A chunk section starts with `[chunk <X> <Y> <Z>]`: the chunk's lowest tile coordinates, `X = 1 + 32 * i`,
  `Y = 1 + 32 * j`, and its level `Z`. A chunk with no line is not written.
- Chunks are ordered by Z, then Y, then X, all increasing. Inside a chunk the lines are ordered by y **decreasing** (tg's top
  row first, as in a TGM column block), then x increasing.
- A tile line is `<x>,<y>: <members>` with the tile's map coordinates (from 1, inside the chunk and inside `size`). A tile
  whose members equal the `default` line exactly is not written; every other tile is written exactly once.
- Multi-z maps: each level's tiles are in the chunks of its `Z`. The z linkage stays in the map json's `traits`, as in tg.

### 4.3 Members

`<members>` is one or more atoms separated by ` | ` (space, bar, space), **in tg's order inside the model**: the movables in
the order the `.dmm` model lists them, then the turf or turfs, then exactly one area, last (`build_coordinate`,
`reader.dm:892-982`, relies on that order: the area first, then the first turf, then the further turfs piled as underlays,
then the movables created in member order). The converter never reorders. A member is a typepath, optionally followed by
its overrides:

```
member    := typepath [ "{" override { "; " override } "}" ]
override  := varname " = " value
typepath  := "/" segment { "/" segment }         (segment: [A-Za-z0-9_]+)
varname   := [A-Za-z_][A-Za-z0-9_]*
```

Overrides keep the `.dmm`'s order (tg's mapmerge writes them sorted by name; the converter copies what it reads). The
separators ` | `, `{`, `}` and `; ` are recognised only outside strings, resources and list parentheses.

### 4.4 Values

A value is the DM literal text exactly as the `.dmm` holds it, trimmed. The reader gives it the meaning of tg's
`/datum/parsed_map/proc/parse_constant` (`reader.dm:1063-1102`), in tg's order:

1. empty text: `""`;
2. a number: whatever BYOND's `text2num` reads as a number (`-1`, `0.5`, `1e+006`), as a `float`;
3. a string: `"..."` (the text between the first and the last character, **not unescaped**, as tg's `copytext(text, 2, -1)`:
   a `\"` or `\n` inside stays as written, and text macros such as `\improper` stay in the text);
4. a list: `list(...)`, read by tg's `readlist` (`reader.dm:1004`): items separated by `,` outside quotes and parentheses,
   an item `key = value` is an association (the key parsed as a constant, a bare word as text), nested `list(...)` allowed;
5. a typepath: a text that `text2path` resolves (`/obj/item/crowbar`), as the C# `Type` of its class;
6. a resource: `'path'` (BYOND's `file()`), as a `DmResource` of the path between the quotes;
7. `null`;
8. anything else: the text itself (tg's fallback). The converter's validation (section 6) flags it.

Not supported, as in tg: `new()`, `newlist()`, `icon()`, `matrix()`, `sound()`, and "pops" (`/obj{name="foo"}` as a value).

## 5. The engine reader and the load (M3 P2; contracts in `Tg/Infrastructure/MapLoader/`)

- **The engine part** replaces exactly `parsed_map`'s DM text parsing (`New`'s grid parsing, `tgm_build_cache`,
  `dmm_build_cache`, `readlist`, `parse_constant`, `find_next_delimiter_position`: scope.md 2c): `MapText.Parse` reads a file
  into a `ParsedMapText` (header, models by distinct member list, tiles) without a World; values stay `MapValue`s until the
  load turns them into DM values inside the world (lists are created then, one per atom, as tg's preloader deep-copies them).
- **Translated tg code** does everything else: `load_map`, `/datum/parsed_map/load` and `_load_impl` (the bounds, the
  `SSatoms.map_loader_begin/stop`, `AfterChange` of the loaded turfs), `build_coordinate` with its area cache
  (`loaded_areas`, `GLOB.areas_by_type`), `create_atom`, `instance_atom`, the preloader (`world.preloader_setup`,
  `preloader_load`), `ChangeTurf`, SSmapping's station path (`loadWorld`, `LoadGroup`, `add_new_zlevel`, `space_level`, z
  traits). The `parsed_map` datum keeps tg's vars; its `gridSets` and `modelCache` are filled from `ParsedMapText` by the
  engine part (one `grid_set` per chunk line run is not needed: one per level, in tg's shape).
- **The host side** (M3 P2): `MapSeam.Select(world, new MapSelection(folder, name))` copies `<folder>/map_jsons/<name>.json`
  into the world's file seam at `_maps/map_jsons/<name>.json` (tg's `MAP_DIRECTORY_MAPS`) and every map file the json names,
  `<folder>/<map_path>/<stem>.as3dmap`, at `_maps/<map_path>/<map_file>`, under tg's own file name (`MetaStation.dmm`, holding
  our format), so tg's `load_map_config`, `LoadConfig`'s `fexists` and `LoadGroup`'s `file("_maps/...")` read them unchanged;
  `TgWorld.CompiledMap` (the boot's `compiledMap` step) sets `world.maxx` and `maxy` to the largest map file's size, as
  BYOND's compiled-in maps size the world (D108 loads no compiled-in level). SSmapping's translated PreInit treats the
  selection as tg's `FORCE_MAP`. The engine part of `/datum/parsed_map/New` (`modules/mapping/reader.cs`) gives the datum
  tg's TGM shape: one `/datum/grid_set` per column (x) and level with the keys top row first, a model key per distinct
  model (letters of one length, as tg's keys), and `tgm_build_cache` fills `modelCache` with tg's
  `list(members, members_attributes)` (text values through tg's `apply_text_macros`).
- **The preloader's var set** is by name (tg: `what.vars[attribute] = value`, `preloader.dm:20-35`): `MapVars.TrySet`
  assigns a field of the instance's class by its DM name (rules 2.1, 2.4: `__proc` never applies to vars), converting the
  value by the field's C# type (rules 3.5: `float`, `string`, `Type`, `DmList`, `object`), deep-copying lists (tg's
  `deep_copy_list`). An unknown var is tg's runtime ("undefined variable"), reported through the runtime sink.

- **The editable document** (the map editor, D336): `MapDocument` holds a parsed file's tiles for editing, in undoable steps,
  and writes them back in section 4's order, byte for byte for a file that was not edited (docs/ai/specs/map-editor.md 4).

## 6. Validation (the converter, against `types-index.tsv`)

The converter refuses (section 3) a map where any of these fails; each finding names the `.dmm` line and the model key:

1. every typepath of a member exists as a type in `types-index.tsv` (tg's `bad_paths`, `reader.dm:778`, which tg reports
   and skips; we refuse so that a map never loads half);
2. the last member is an `/area`, there is at least one `/turf`, and no `/area` or `/turf` stands among the movables;
3. every override's var exists on the member's type (declared at it or an ancestor in the index);
4. every value parses by section 4.4 rules 1-7 (the fallback text of rule 8 is an error: tg would silently read it as text;
   a bare word is allowed as a list key), is on one line, and no list has a plain item before an association (tg's
   `readlist` misreads that: its search for the `=` runs past the item's comma);
5. a typepath value names an existing type;
6. a tile has one line, inside `size`, inside its chunk.

**The one change to tg's content (D147):** a door standing on bare plating (an airlock, firelock or pod door with no window
or grille, the turf exactly `/turf/open/floor/plating`) is written with the covered floor of its cardinal neighbour
(`DoorFloors.cs`), so first person does not see the plating's pipes and cables under it.

`--check` also asserts the round trip (with D147's door floors expected): converting the output's models back into tg's model form gives the `.dmm`'s models
(same members, same order, same overrides) for every tile.

## 7. Tests (M3 P2)

The round trip on Meta and on the fixtures; a one-object edit changes one line; byte-equal output on a second run; each
validation rule on a fixture; the reader's values against tg's `parse_constant` cases; Meta's counts (65,025 tiles, 171
areas, 2,702 types, 35,364 movables) through `MapText.Parse`.

Where they are: the converter's in `tools/Anomaly.Tools.Maps/Tests` (a test project beside the tool, since `Anomaly.Core.Tests`
does not reference the tool; the lead adds it to the solution or moves the files into `tests/Anomaly.Core.Tests/Tools/Maps`),
the reader's in `tests/Anomaly.Core.Tests/Tg/M3Maps/P2`, the fixtures in `tests/fixtures/maps` (`p2_small.dmm` in TGM,
`p2_small_dmmsyntax.dmm` the same map in DMM syntax, `p2_small.as3dmap` the expected output).
