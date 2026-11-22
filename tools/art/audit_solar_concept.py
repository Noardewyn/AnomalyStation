"""Read the original solar DMI states, timings and the concept palette."""
from pathlib import Path
from PIL import Image, ImageDraw
import json, re, hashlib
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'godot/assets/models/station_props/power/solar_concept';OUT.mkdir(parents=True,exist_ok=True)
source=ROOT.parent/'tgstation/icons/obj/machines/solar.dmi'
im=Image.open(source);desc=im.info['Description'];states=[];tile=0
for block in re.split(r'(?=state = )',desc)[1:]:
    name=re.search(r'state = "(.*?)"',block).group(1)
    dirs=int(re.search(r'dirs = (\d+)',block).group(1));frames=int(re.search(r'frames = (\d+)',block).group(1))
    delay=re.search(r'delay = ([^\n]+)',block)
    states.append(dict(name=name,dirs=dirs,frames=frames,first_tile=tile,delay_deciseconds=delay.group(1) if delay else None))
    tile+=dirs*frames
sheet=Image.new('RGB',(720,((len(states)+3)//4)*150),'#30323a');d=ImageDraw.Draw(sheet)
for i,st in enumerate(states):
    x=i%4*180;y=i//4*150;idx=st['first_tile'];tx=idx%(im.width//32)*32;ty=idx//(im.width//32)*32
    p=im.crop((tx,ty,tx+32,ty+32)).convert('RGBA').resize((112,112),Image.Resampling.NEAREST)
    sheet.paste(p,(x+32,y+4),p);d.text((x+4,y+118),st['name'].replace('solar_panel_',''),fill='white')
sheet.save(OUT/'source_states.png')
# the owner's concept image (a local file, not in the repository): pass its path as the first argument
import sys
concept=Image.open(sys.argv[1]).convert('RGB')
palette=['#%02X%02X%02X'%concept.getpixel(p) for p in [(1028,902),(1113,902),(1192,902),(1272,902),(1353,902)]]
report=dict(source=str(source),source_sha256=hashlib.sha256(source.read_bytes()).hexdigest(),size=im.size,states=states,concept_palette=palette)
(OUT/'source_states.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
