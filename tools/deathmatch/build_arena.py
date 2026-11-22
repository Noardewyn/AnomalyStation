"""Writes the first version of the deathmatch arena (docs/ai/specs/deathmatch): maps/map_files/deathmatch/arena.as3dmap and
maps/map_jsons/deathmatch_arena.json, from the plan's coordinates (docs/ai/specs/deathmatch/arena-plan.svg: one plan square is
one tile, x to the east and y to the SOUTH from the top left; the file's y runs north, so it is flipped on writing).

The map is edited in the map editor afterwards (D337); running this again writes the plan's version over those edits.
Run from the repository root: python tools/deathmatch/build_arena.py
"""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT_MAP = ROOT / 'maps/map_files/deathmatch/arena.as3dmap'
OUT_JSON = ROOT / 'maps/map_jsons/deathmatch_arena.json'

W, H, Z = 76, 54, 6
AREA = '/area/deathmatch'
NEAR = '/area/space/nearstation'
SPACE = '/area/space'
RWALL = '/turf/closed/wall/r_wall'
OPEN = '/turf/open/openspace'
ROOF = '/turf/open/floor/plating/airless'
GLASS_ROOF = '/turf/open/floor/glass/reinforced/airless'
IRON = '/turf/open/floor/iron'
PLATING = '/turf/open/floor/plating'
PAD = ['/obj/effect/turf_decal/box/red', '/obj/effect/spawner/random/deathmatch_item_pad']


def rect(x0, y0, x1, y1):
    return {(x, y) for x in range(x0, x1 + 1) for y in range(y0, y1 + 1)}


def octagon(x0, y0, x1, y1, cut):
    return {(x, y) for (x, y) in rect(x0, y0, x1, y1) if min(x - x0, x1 - x) + min(y - y0, y1 - y) >= cut}


def circle(x0, y0, x1, y1):
    cx, cy, r2 = (x0 + x1) / 2, (y0 + y1) / 2, ((x1 - x0 + 1) / 2 + 0.2) ** 2
    return {(x, y) for (x, y) in rect(x0, y0, x1, y1) if (x - cx) ** 2 + (y - cy) ** 2 <= r2}


class Region:
    def __init__(self, name, kind, cells, h, floor, ceiling=None):
        self.name, self.kind, self.cells, self.h, self.floor, self.ceiling = name, kind, cells, h, floor, ceiling


# kind: room, corridor, passage (a doorway from room to room), maint
REGIONS = [
    Region('spawn', 'room', rect(8, 5, 16, 11), 2, '/turf/open/floor/iron/dark', '/turf/open/floor/iron/white'),
    Region('bridge', 'room', rect(30, 5, 43, 8) | rect(34, 9, 39, 12), 2, '/turf/open/floor/carpet/blue', '/turf/open/floor/iron/dark'),
    Region('bar', 'room', rect(52, 5, 61, 12), 2, '/turf/open/floor/wood', '/turf/open/floor/iron/dark'),
    Region('medbay', 'room', rect(8, 14, 14, 21), 2, '/turf/open/floor/iron/white', '/turf/open/floor/iron/white'),
    Region('atrium', 'room', octagon(30, 17, 49, 32, 3), 5, IRON, IRON),
    Region('engineering', 'room', circle(57, 20, 68, 31), 3, '/turf/open/floor/engine'),
    Region('cargo', 'room', rect(44, 41, 63, 46) | rect(58, 34, 63, 40), 4, '/turf/open/floor/iron/textured_large', IRON),
    Region('server', 'room', rect(26, 42, 31, 46), 2, '/turf/open/floor/circuit/green', '/turf/open/floor/iron/dark'),
    Region('c1', 'corridor', rect(17, 6, 29, 8), 2, IRON),
    Region('c2', 'corridor', rect(36, 13, 37, 16), 2, IRON),
    Region('c3', 'corridor', rect(44, 7, 51, 7), 2, IRON),
    Region('c4', 'corridor', rect(60, 13, 61, 19), 2, IRON),
    Region('c5', 'corridor', rect(50, 25, 56, 27), 2, IRON),
    Region('c6', 'corridor', rect(44, 33, 45, 40), 2, IRON),
    Region('c7', 'corridor', rect(15, 20, 29, 20), 2, IRON),
    Region('c9', 'corridor', rect(38, 33, 38, 42), 2, IRON),
    Region('c10', 'corridor', rect(32, 43, 43, 44), 2, IRON),
    Region('p_spawn_medbay', 'passage', rect(11, 12, 11, 13), 2, IRON),
    Region('p_engineering_cargo', 'passage', rect(60, 32, 60, 33), 2, IRON),
    Region('maint', 'maint', rect(5, 5, 5, 49) | rect(5, 49, 71, 49) | rect(71, 5, 71, 49)
           | rect(6, 8, 7, 8) | rect(6, 17, 7, 17) | rect(28, 47, 28, 48) | rect(52, 47, 52, 48) | rect(69, 26, 70, 26)
           | rect(62, 8, 70, 8) | rect(20, 21, 20, 44) | rect(21, 44, 25, 44), 1, PLATING),
]
ROOMS = [r for r in REGIONS if r.kind == 'room']

AIRLOCK = '/obj/machinery/door/airlock/'
# the door of a corridor where it enters a room; a passage is a vault door at both ends, maintenance has its hatches
DOORS = {
    ('c1', 'spawn'): 'vault', ('c1', 'bridge'): 'command',
    ('c2', 'bridge'): 'command', ('c2', 'atrium'): 'vault',
    ('c3', 'bridge'): 'command', ('c3', 'bar'): 'public',
    ('c4', 'bar'): 'public', ('c4', 'engineering'): 'vault',
    ('c5', 'atrium'): 'vault', ('c5', 'engineering'): 'engineering',
    ('c6', 'atrium'): 'public', ('c6', 'cargo'): 'vault',
    ('c7', 'medbay'): 'medical', ('c7', 'atrium'): 'public',
    ('c9', 'atrium'): 'public',
    ('c10', 'server'): 'research', ('c10', 'cargo'): 'vault',
}

# cover standing in a room: (cells, levels high); windows are one level, a reinforced window on a grille
WALLS = [
    (rect(10, 9, 14, 9), 1),  # spawn: a low wall before the exits
    (rect(54, 6, 54, 6) | rect(59, 6, 59, 6) | rect(54, 11, 54, 11) | rect(59, 11, 59, 11), 2),  # the bar's pillars
    (rect(34, 21, 37, 21), 3), (rect(34, 28, 37, 28), 1), (rect(39, 24, 40, 25), 5),  # atrium: tall, low, the pillar
    (rect(60, 22, 61, 22), 1),  # engineering
    (rect(45, 46, 48, 46), 1), (rect(55, 44, 56, 46), 4),  # cargo: low, tall
]
WINDOWS = rect(36, 11, 39, 11) | rect(9, 17, 12, 17) | rect(42, 21, 45, 21) | rect(42, 28, 45, 28) | rect(64, 28, 65, 28) | rect(58, 37, 61, 37)
GLASS_ROOF_CELLS = rect(36, 21, 43, 28)  # the atrium's middle, under the stars

THINGS = {}  # (x, y) -> list of members on level 1


def put(x, y, *members):
    THINGS.setdefault((x, y), []).extend(members)


# spawn: ten late-join landmarks (the round's start falls back to them with no job landmarks) and the ghosts' start
for x in range(9, 14):
    put(x, 6, '/obj/effect/landmark/latejoin')
    put(x, 7, '/obj/effect/landmark/latejoin')
put(15, 6, '/obj/effect/landmark/observer_start')
# bridge: consoles along the north wall, facing the room, with their chairs
for x, console in ((32, 'security'), (33, 'crew'), (36, 'communications'), (37, 'station_alert'), (40, 'crew'), (41, 'security')):
    put(x, 5, f'/obj/machinery/computer/{console}{{dir = 2}}')
for x in (33, 36, 40):
    put(x, 6, '/obj/structure/chair/office{dir = 1}')
# bar: a counter of tables with stools
for x, y in ((56, 8), (57, 8), (56, 9), (57, 9)):
    put(x, y, '/obj/structure/table/wood')
for x, y in ((55, 8), (55, 9), (58, 8), (58, 9), (56, 10), (57, 10)):
    put(x, y, '/obj/structure/chair/stool/bar')
# medbay: beds
for x, y in ((10, 15), (12, 15), (10, 20), (12, 20)):
    put(x, y, '/obj/structure/bed')
# atrium: crates and tables to hide behind
for x, y in ((33, 19), (46, 19), (35, 25), (44, 25), (33, 26), (46, 26)):
    put(x, y, '/obj/structure/closet/crate')
for x, y in ((38, 19), (41, 19), (38, 30), (41, 30)):
    put(x, y, '/obj/structure/table/reinforced')
# engineering: racks; the mech bay (a recharge port facing the mech's recharge floor, its console beside the port)
put(58, 25, '/obj/structure/rack')
put(58, 26, '/obj/structure/rack')
put(64, 30, '/obj/vehicle/sealed/mecha/marauder/loaded')
put(65, 30, '/obj/machinery/mech_bay_recharge_port{dir = 8}')
put(65, 31, '/obj/machinery/computer/mech_bay_power_console{dir = 1}')
# cargo: crates, an ore box and the second mech bay
for x, y in ((47, 42), (48, 42), (50, 43), (52, 42), (53, 42), (61, 44), (62, 44), (62, 45), (60, 39), (63, 39)):
    put(x, y, '/obj/structure/closet/crate')
put(49, 45, '/obj/structure/ore_box')
put(58, 42, '/obj/vehicle/sealed/mecha/gygax/dark/loaded')
put(59, 42, '/obj/machinery/mech_bay_recharge_port{dir = 8}')
put(59, 43, '/obj/machinery/computer/mech_bay_power_console{dir = 2}')
# server: two rows of servers
for x, y in ((28, 43), (28, 45), (30, 43), (30, 45)):
    put(x, y, '/obj/machinery/rnd/server')
# the weapon pads
for x, y in ((31, 7), (42, 7), (56, 5), (61, 12), (9, 21), (32, 24), (48, 24), (32, 30), (47, 30), (40, 30), (64, 23), (59, 29),
             (47, 44), (51, 45), (62, 35), (26, 42), (36, 43), (5, 31), (34, 49), (71, 16), (20, 34)):
    put(x, y, *PAD)

RECHARGE_FLOORS = {(64, 30), (58, 42)}

# healing: a NanoMed on a wall of each room (three in the medbay) and of some corridors: (region, the point it goes nearest to)
WALLMEDS = [('spawn', (16, 11)), ('bridge', (30, 8)), ('bar', (61, 5)), ('medbay', (8, 14)), ('medbay', (14, 14)), ('medbay', (8, 21)),
            ('atrium', (39, 17)), ('atrium', (31, 31)), ('engineering', (66, 21)), ('cargo', (44, 41)), ('server', (31, 46)),
            ('c1', (23, 6)), ('c4', (60, 16)), ('c10', (34, 43)), ('maint', (5, 40))]


def main():
    region_of = {}
    for r in REGIONS:
        for c in r.cells:
            assert c not in region_of or region_of[c].kind == r.kind == 'maint', (c, r.name, region_of[c].name)
            region_of[c] = r
    for (x, y) in region_of:
        assert 3 <= x <= W - 2 and 3 <= y <= H - 2, (x, y)

    def near(c, d=2):
        x, y = c
        return [(x + i, y + j) for i in range(-d, d + 1) for j in range(-d, d + 1) if (i, j) != (0, 0)]

    # doors: a corridor, passage or maintenance tile within two tiles of a room's floor (both tiles of a two-tile wall)
    doors = {}
    for c, r in region_of.items():
        if r.kind == 'room':
            continue
        rooms = {region_of[n].name for n in near(c) if n in region_of and region_of[n].kind == 'room'}
        if not rooms:
            continue
        assert len(rooms) == 1 or r.kind == 'passage', (c, r.name, rooms)
        if r.kind == 'maint':
            doors[c] = AIRLOCK + 'maintenance_hatch'
        elif r.kind == 'passage':
            doors[c] = AIRLOCK + 'vault'
        else:
            doors[c] = AIRLOCK + DOORS[(r.name, rooms.pop())]

    # walls: two tiles of reinforced wall round every floor, as high as the tallest floor they stand beside
    walls = {}
    for c, r in region_of.items():
        for n in near(c):
            if n not in region_of:
                walls[n] = max(walls.get(n, 0), r.h)
    # above a door, wall up to the tallest of the floors beside it
    door_top = {c: max(region_of[n].h for n in near(c) + [c] if n in region_of) for c in doors}

    wall_cells = {}
    for cells, levels in WALLS:
        for c in cells:
            assert region_of.get(c) and region_of[c].kind == 'room' and levels <= region_of[c].h, c
            wall_cells[c] = levels
    for c in WINDOWS:
        assert region_of.get(c) and region_of[c].kind == 'room', c

    def is_wall(c):
        return c in walls or c in wall_cells

    # wall mounts: a tile beside a wall, the mount on that wall
    dirs = (('north', 0, -1), ('south', 0, 1), ('west', -1, 0), ('east', 1, 0))
    lit = set()
    mounts = {}

    def mount_dir(c):
        for name, dx, dy in dirs:
            n = (c[0] + dx, c[1] + dy)
            if n in walls and n not in doors:
                return name
        return None

    for c, r in sorted(region_of.items()):
        if c in doors or c in wall_cells or c in WINDOWS:
            continue
        x, y = c
        d = mount_dir(c)
        if d is None:
            continue
        step = 6 if r.kind == 'maint' else 4
        along = x if d in ('north', 'south') else y
        if along % step != (0 if d in ('north', 'west') else step // 2):
            continue
        lamp = '/obj/machinery/light/small/directional/' if r.kind == 'maint' else '/obj/machinery/light/directional/'
        mounts.setdefault(c, []).append(lamp + d)
        lit.add(c)
    for name, (tx, ty) in WALLMEDS:
        r = next(r for r in REGIONS if r.name == name)
        spots = [c for c in r.cells if c not in doors and c not in wall_cells and c not in WINDOWS and c not in lit and c not in mounts
                 and mount_dir(c)]
        c = min(spots, key=lambda c: ((c[0] - tx) ** 2 + (c[1] - ty) ** 2, c))
        mounts.setdefault(c, []).append('/obj/machinery/vending/wallmed/directional/' + mount_dir(c))

    for c in THINGS:
        assert c in region_of and c not in doors and c not in wall_cells and c not in WINDOWS, c

    # the levels
    tiles = {}  # (x, y, z) -> members text

    def tile(c, z, members):
        tiles[(c[0], c[1], z)] = ' | '.join(members)

    for c, r in region_of.items():
        if c in doors:
            tile(c, 1, [doors[c], r.floor, AREA])
            for z in range(2, door_top[c] + 1):
                tile(c, z, [RWALL, AREA])
            continue
        if c in wall_cells:
            for z in range(1, wall_cells[c] + 1):
                tile(c, z, [RWALL, AREA])
        floor = '/turf/open/floor/iron/recharge_floor' if c in RECHARGE_FLOORS else r.floor
        ceiling = None if c in GLASS_ROOF_CELLS else r.ceiling
        for z in range(1, r.h + 1):
            if (c[0], c[1], z) in tiles:
                continue
            turf = floor if z == 1 else OPEN
            if ceiling and z == r.h:
                turf += f'{{ceiling = "{ceiling}"}}'
            members = []
            if z == 1:
                members = THINGS.get(c, []) + mounts.get(c, [])
                if c in WINDOWS:
                    members = ['/obj/structure/grille', '/obj/structure/window/reinforced/fulltile'] + members
            tile(c, z, members + [turf, AREA])
        if r.h + 1 <= Z:
            # a roof beside a taller floor's open space would vent that air to space: wall it instead
            if any(n in region_of and region_of[n].h > r.h for n in near(c, 1)):
                tile(c, r.h + 1, [RWALL, AREA])
            else:
                tile(c, r.h + 1, [GLASS_ROOF if c in GLASS_ROOF_CELLS else ROOF, NEAR])
    for c, h in walls.items():
        for z in range(1, h + 1):
            tile(c, z, [RWALL, AREA])
    for x in range(1, W + 1):
        for y in range(1, H + 1):
            if (x, y, 1) not in tiles:
                tile((x, y), 1, ['/turf/open/space/basic', SPACE])

    default = '/turf/open/space/openspace | /area/space'
    lines = ['anomaly-map 1', f'size {W} {H} {Z}', 'chunk 32', f'default {default}']
    chunks = {}
    for (x, y, z), members in tiles.items():
        if members == default:
            continue
        fy = H + 1 - y
        key = (z, (fy - 1) // 32, (x - 1) // 32)
        chunks.setdefault(key, []).append((fy, x, members))
    for (z, j, i) in sorted(chunks):
        lines.append('')
        lines.append(f'[chunk {1 + 32 * i} {1 + 32 * j} {z}]')
        for fy, x, members in sorted(chunks[(z, j, i)], key=lambda t: (-t[0], t[1])):
            lines.append(f'{x},{fy}: {members}')
    OUT_MAP.parent.mkdir(parents=True, exist_ok=True)
    OUT_MAP.write_text('\n'.join(lines) + '\n', encoding='utf-8', newline='\n')

    config = {
        'version': 1,
        'map_name': 'Deathmatch Arena',
        'map_path': 'map_files/deathmatch',
        'map_file': 'arena.dmm',
        'traits': [{'Linkage': 'Self'}] + [{'Baseturf': '/turf/open/space/openspace', 'Linkage': 'Self'} for _ in range(Z - 1)],
        'deathmatch': {'autorespawn_seconds': 10, 'scoreboard': 1},
    }
    OUT_JSON.write_text(json.dumps(config, indent=2) + '\n', encoding='utf-8', newline='\n')
    pads = sum(1 for m in THINGS.values() if PAD[1] in m)
    print(f'{OUT_MAP.relative_to(ROOT)}: {len(tiles)} tiles, {len(doors)} door tiles, {pads} pads, {len(lit)} lamps')
    if len(sys.argv) > 2 and sys.argv[1] == '--preview':
        render(Path(sys.argv[2]), region_of, doors, walls, wall_cells, mounts)


# --- the design picture: the map's level 1 seen from above, tile by tile, with a cut through it showing the heights ---

FLOOR_COLOURS = {
    '/turf/open/floor/iron/dark': (70, 74, 82), '/turf/open/floor/carpet/blue': (64, 96, 170), '/turf/open/floor/wood': (150, 104, 60),
    '/turf/open/floor/iron/white': (226, 230, 232), IRON: (150, 154, 158), '/turf/open/floor/engine': (112, 112, 104),
    '/turf/open/floor/iron/textured_large': (168, 140, 100), '/turf/open/floor/circuit/green': (40, 120, 70), PLATING: (110, 106, 98),
}
DOOR_COLOURS = {'vault': (150, 30, 30), 'command': (40, 90, 200), 'engineering': (230, 190, 40), 'medical': (120, 200, 230),
                'public': (210, 210, 200), 'research': (150, 80, 200), 'maintenance_hatch': (200, 140, 40)}
NAMES = {'spawn': 'Spawn', 'bridge': 'Bridge', 'bar': 'Bar', 'medbay': 'Medbay', 'atrium': 'Atrium', 'engineering': 'Engineering',
         'cargo': 'Cargo', 'server': 'Server'}


def render(path, region_of, doors, walls, wall_cells, mounts):
    from PIL import Image, ImageDraw, ImageFont
    T = 16  # pixels a tile
    top_h, cut_h, legend_h = H * T, 9 * T + 30, 120
    img = Image.new('RGB', (W * T, top_h + cut_h + legend_h), (255, 255, 255))
    d = ImageDraw.Draw(img)
    try:
        font, small = ImageFont.truetype('arial.ttf', 15), ImageFont.truetype('arial.ttf', 11)
    except OSError:
        font = small = ImageFont.load_default()

    def box(c, colour, inset=0, outline=None):
        x, y = c
        d.rectangle([(x - 1) * T + inset, (y - 1) * T + inset, x * T - 1 - inset, y * T - 1 - inset], fill=colour, outline=outline)

    def centre(c):
        return (c[0] - 0.5) * T, (c[1] - 0.5) * T

    for x in range(1, W + 1):
        for y in range(1, H + 1):
            box((x, y), (14, 18, 38))  # space
    for c, h in walls.items():
        g = 150 - 15 * h
        box(c, (g, g, g + 6))
    for c, r in region_of.items():
        box(c, FLOOR_COLOURS[r.floor])
    for c in RECHARGE_FLOORS:
        box(c, (60, 70, 60))
        d.rectangle([(c[0] - 1) * T + 2, (c[1] - 1) * T + 2, c[0] * T - 3, c[1] * T - 3], outline=(240, 200, 40))
    for c in GLASS_ROOF_CELLS:  # the glass roof, seen through
        x0, y0 = (c[0] - 1) * T, (c[1] - 1) * T
        d.line([x0 + 3, y0 + T - 4, x0 + T - 4, y0 + 3], fill=(120, 170, 230))
    for c, levels in wall_cells.items():
        g = 150 - 15 * levels
        box(c, (g, g, g + 6), 1, (30, 30, 30))
        d.text(centre(c), str(levels), fill=(255, 255, 255), font=small, anchor='mm')
    for c in WINDOWS:
        box(c, (150, 200, 240), 3, (40, 90, 150))
    for c, door in doors.items():
        box(c, DOOR_COLOURS[door.rsplit('/', 1)[1]], 2, (20, 20, 20))
    for c, members in THINGS.items():
        cx, cy = centre(c)
        text = ' '.join(members)
        if PAD[1] in text:
            d.rectangle([cx - 6, cy - 6, cx + 6, cy + 6], outline=(220, 40, 40), width=2)
            d.ellipse([cx - 4, cy - 4, cx + 4, cy + 4], fill=(240, 120, 30))
        elif 'mecha/' in text:
            d.rectangle([cx - 7, cy - 7, cx + 7, cy + 7], fill=(20, 150, 110), outline=(0, 0, 0))
            d.text((cx, cy), 'M', fill=(255, 255, 255), font=small, anchor='mm')
        elif 'latejoin' in text:
            d.ellipse([cx - 4, cy - 4, cx + 4, cy + 4], fill=(100, 200, 60), outline=(20, 60, 10))
        elif 'computer' in text or 'rnd/server' in text or 'recharge_port' in text:
            d.rectangle([cx - 6, cy - 5, cx + 6, cy + 5], fill=(40, 50, 70), outline=(120, 220, 255))
        elif 'crate' in text or 'ore_box' in text:
            d.rectangle([cx - 6, cy - 6, cx + 6, cy + 6], fill=(150, 110, 50), outline=(70, 50, 20))
        elif 'table' in text or 'rack' in text:
            d.rectangle([cx - 7, cy - 7, cx + 7, cy + 7], fill=(190, 190, 195), outline=(80, 80, 80))
        elif 'bed' in text:
            d.rectangle([cx - 5, cy - 7, cx + 5, cy + 7], fill=(250, 250, 250), outline=(80, 120, 160))
        elif 'chair' in text or 'stool' in text:
            d.ellipse([cx - 4, cy - 4, cx + 4, cy + 4], fill=(60, 60, 60))
    for c, members in mounts.items():
        cx, cy = centre(c)
        for m in members:
            if 'wallmed' in m:
                d.rectangle([cx - 5, cy - 2, cx + 5, cy + 2], fill=(220, 60, 120))
                d.rectangle([cx - 2, cy - 5, cx + 2, cy + 5], fill=(220, 60, 120))
            elif 'light' in m:
                d.ellipse([cx - 2, cy - 2, cx + 2, cy + 2], fill=(255, 230, 90))
    for name, label in NAMES.items():
        r = next(r for r in REGIONS if r.name == name)
        mx, my = sum(c[0] for c in r.cells) / len(r.cells), sum(c[1] for c in r.cells) / len(r.cells)
        cx, cy = centre(min(r.cells, key=lambda c: ((c[0] - mx) ** 2 + (c[1] - my) ** 2, c)))
        d.text((cx, cy), f'{label} h{r.h}', fill=(255, 255, 255), font=font, anchor='mm', stroke_width=3, stroke_fill=(0, 0, 0))

    # the cut: west to east through the plan's row 25 (the Atrium, the corridor and Engineering), level 1 at the bottom
    row, base = 25, top_h + cut_h - 12
    d.text((8, top_h + 6), f'Cut west to east through row {row}: each band is one level (2 m)', fill=(0, 0, 0), font=font)
    for x in range(1, W + 1):
        c = (x, row)
        for z in range(1, Z + 1):
            x0, y0 = (x - 1) * T, base - z * T
            colour = None
            if c in walls and z <= walls[c] or c in wall_cells and z <= wall_cells[c] or c in doors and z > 1:
                colour = (100, 100, 108)
            elif c in doors:
                colour = DOOR_COLOURS[doors[c].rsplit('/', 1)[1]]
            elif c in region_of and z <= region_of[c].h:
                colour = FLOOR_COLOURS[region_of[c].floor] if z == 1 else (225, 236, 245)
            elif c in region_of and z == region_of[c].h + 1:
                colour = (120, 170, 230) if c in GLASS_ROOF_CELLS else (90, 86, 80)
            if colour:
                d.rectangle([x0, y0, x0 + T - 1, y0 + T - 1], fill=colour)
    for z in range(1, Z + 1):
        d.text((W * T - 4, base - z * T + T / 2), f'L{z}', fill=(0, 0, 0), font=small, anchor='rm')

    # the legend
    y0 = top_h + cut_h + 8
    items = [((14, 18, 38), 'Space'), ((105, 105, 111), 'Reinforced wall, 2 thick'), ((120, 120, 126), 'Cover wall (number = levels)'),
             ((150, 200, 240), 'Window, 1 level'), (DOOR_COLOURS['vault'], 'Vault door'), (DOOR_COLOURS['command'], 'Command airlock'),
             (DOOR_COLOURS['engineering'], 'Engineering airlock'), (DOOR_COLOURS['medical'], 'Medical airlock'),
             (DOOR_COLOURS['public'], 'Public airlock'), (DOOR_COLOURS['research'], 'Research airlock'),
             (DOOR_COLOURS['maintenance_hatch'], 'Maintenance hatch'), ((240, 120, 30), 'Weapon pad'), ((20, 150, 110), 'Mech on recharge floor'),
             ((100, 200, 60), 'Player spawn'), ((220, 60, 120), 'NanoMed'), ((255, 230, 90), 'Lamp'), ((150, 110, 50), 'Crate'),
             ((40, 50, 70), 'Console, server, charger')]
    for i, (colour, label) in enumerate(items):
        lx, ly = 10 + (i % 6) * 200, y0 + (i // 6) * 34
        d.rectangle([lx, ly, lx + 18, ly + 18], fill=colour, outline=(0, 0, 0))
        d.text((lx + 26, ly + 9), label, fill=(0, 0, 0), font=small, anchor='lm')
    img.save(path)
    print(f'preview: {path}')


if __name__ == '__main__':
    import sys
    main()
