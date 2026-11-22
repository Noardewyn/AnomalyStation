"""The tram port (DV403): cuts tests/fixtures/maps/t2/tram out of the converted Tramstation (maps/, from tools/convert-maps.sh).

Level 2's tram line (x 60-190, y 112-126: the three platforms, the car at the central one, the crossings and the sensors) becomes the
upper level of a two-level map, its open space kept (the car stands over it, as on Tramstation) over a plating level; every area is tg's
always powered test room (the cabinet runs with no APC), and a late join spot stands in the car. Run from the repository root: python tools/fixtures/cut-tram-line.py
"""
import os
import re

SOURCE = 'maps/map_files/tramstation/tramstation.as3dmap'
OUT = 'tests/fixtures/maps/t2/tram'
X0, X1, Y0, Y1, Z = 60, 190, 112, 126, 2
AREA = '/area/misc/testroom'
LATEJOIN = (123, 119)  # in the car, the middle row (it rides with it)

line_re = re.compile(r'^(\d+),(\d+): (.*)$')
tiles = {}
z = None
with open(SOURCE, encoding='utf-8') as f:
    for raw in f:
        raw = raw.rstrip('\n')
        if raw.startswith('[chunk'):
            z = int(raw.strip('[]').split()[3])
            continue
        m = line_re.match(raw)
        if not m or z != Z:
            continue
        x, y = int(m.group(1)), int(m.group(2))
        if X0 <= x <= X1 and Y0 <= y <= Y1:
            tiles[(x, y)] = m.group(3)

width, height = X1 - X0 + 1, Y1 - Y0 + 1
rows = []
for y in range(Y1, Y0 - 1, -1):
    for x in range(X0, X1 + 1):
        parts = tiles.get((x, y), '/turf/open/space/openspace | /area/space').split(' | ')
        parts[-1] = AREA
        if parts[-2].startswith('/turf/open/space'):
            parts[-2] = '/turf/open/openspace'
        if (x, y) == LATEJOIN:
            parts.insert(0, '/obj/effect/landmark/latejoin')
        text = ' | '.join(parts)
        if text != '/turf/open/floor/plating | ' + AREA:  # the default: the format leaves it out
            rows.append((x - X0 + 1, y - Y0 + 1, text))

chunk = 32
out_dir = os.path.join(OUT, 'map_files', 'T2Tram')
os.makedirs(out_dir, exist_ok=True)
os.makedirs(os.path.join(OUT, 'map_jsons'), exist_ok=True)
with open(os.path.join(out_dir, 'T2Tram.as3dmap'), 'w', encoding='utf-8', newline='\n') as f:
    f.write('anomaly-map 1\n')
    f.write('source _maps/map_files/T2Tram/T2Tram.dmm tg 1d99bd7f\n')
    f.write('size %d %d 2\n' % (width, height))  # level 1: all the default plating
    f.write('chunk %d\n' % chunk)
    f.write('default /turf/open/floor/plating | %s\n' % AREA)
    for cy in range(0, height, chunk):
        for cx in range(0, width, chunk):
            body = [r for r in rows if cx < r[0] <= cx + chunk and cy < r[1] <= cy + chunk]
            if not body:
                continue
            f.write('\n[chunk %d %d 2]\n' % (cx + 1, cy + 1))
            body.sort(key=lambda r: (-r[1], r[0]))
            for (x, y, text) in body:
                f.write('%d,%d: %s\n' % (x, y, text))
with open(os.path.join(OUT, 'map_jsons', 'tram.json'), 'w', encoding='utf-8', newline='\n') as f:
    f.write('''{
	"version": 1,
	"map_name": "T2 tram",
	"map_path": "map_files/T2Tram",
	"map_file": "T2Tram.dmm",
	"traits": [
		{
			"Linkage": "Cross",
			"Gravity": 1,
			"Baseturf": "/turf/open/floor/plating"
		},
		{
			"Linkage": "Cross",
			"Gravity": 1,
			"Baseturf": "/turf/open/openspace"
		}
	]
}
''')
print('wrote %d tiles (%d x %d)' % (len(rows), width, height))
