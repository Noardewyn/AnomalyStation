"""Extract SMES source states and frame timings without changing artwork."""
from pathlib import Path
from PIL import Image, ImageDraw
import json, re, sys

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'godot/assets/models/machines/smes'
OUT.mkdir(parents=True, exist_ok=True)
source = ROOT.parent / 'tgstation/icons/obj/machines/engine/other.dmi'
im = Image.open(source)
description = im.info['Description']
states = []
offset = 0
for block in re.split(r'(?=state = )', description)[1:]:
    name = re.search(r'state = "(.*?)"', block).group(1)
    dirs = int(re.search(r'dirs = (\d+)', block).group(1))
    frames = int(re.search(r'frames = (\d+)', block).group(1))
    delay = re.search(r'delay = ([^\n]+)', block)
    record = dict(name=name, dirs=dirs, frames=frames, first_tile=offset,
                  delay_deciseconds=[float(x) for x in delay.group(1).split(',')] if delay else None)
    if name.startswith('smes'):
        states.append(record)
    offset += dirs * frames
def pixel(state, x, y):
    tile = next(s['first_tile'] for s in states if s['name'] == state)
    return im.convert('RGBA').getpixel((tile % (im.width//32)*32+x, tile//(im.width//32)*32+y))

def hex_color(rgb):
    return '#' + ''.join(f'{v:02X}' for v in rgb[:3])

# The full gauge's left column is the sprite's face colour, its centre column the painted highlight.
palette = [dict(level=level, color=hex_color(pixel('smes-og5', 12, 24-level)),
                inactive_color=hex_color(pixel('smes', 12, 24-level))) for level in range(1,6)]
if '--metadata-only' not in sys.argv:
    sheet = Image.new('RGB', (800, max(1, len(states))*148), '#55555b')
    draw = ImageDraw.Draw(sheet)
    for row, state in enumerate(states):
        draw.text((8,row*148+5), f"{state['name']} | {state['frames']} frames | delay {state['delay_deciseconds']}", fill='white')
        for frame in range(min(6,state['frames'])):
            tile = state['first_tile'] + frame*state['dirs']
            x, y = tile%(im.width//32)*32, tile//(im.width//32)*32
            patch = im.crop((x,y,x+32,y+32)).convert('RGBA').resize((112,112),Image.Resampling.NEAREST)
            sheet.paste(patch,(8+frame*128,row*148+28),patch)
    sheet.save(OUT/'source_states.png')
(OUT/'source_states.json').write_text(json.dumps(dict(source=str(source),size=im.size,states=states,
    charge_palette=palette, charge_formula='clamp(floor(5 * total_charge / total_capacity), 0, 5)'),indent=2))
print(json.dumps(states,indent=2))
