"""Audit every declared vendor appearance in this atlas and render reference sheets."""
from pathlib import Path
from PIL import Image,ImageDraw
import json,hashlib,re
ROOT=Path(__file__).resolve().parents[2];TG=ROOT.parent/'tgstation'
OUT=ROOT/'godot/assets/models/station_props/vending';ART=ROOT/'docs/ai/art'
PNG=ROOT/'godot/assets/tg/icons/obj/machines/vending.png';META=json.loads(PNG.with_suffix('.json').read_text());STATES={s['name']:s for s in META['states']}
image=Image.open(PNG).convert('RGBA');original=Image.open(TG/'icons/obj/machines/vending.dmi')
assert image.tobytes()==original.convert('RGBA').tobytes();assert (META['width'],META['height'])==(32,32)
parser=(ROOT/'docs/ai/research/10-scripts/dm_types.py').read_text().split('for dp,dn,fn in os.walk')[0]
ns={};exec(parser,ns);ns['KEEPVARS'].update(['light_mask','panel_type','parent_type'])
for path in sorted((TG/'code/modules/vending').rglob('*.dm')):ns['parse_file'](str(path))
def resolve(path,var,seen=None):
    seen=set() if seen is None else seen
    while path and path not in seen:
        seen.add(path);values=ns['types'].get(path,{}).get('vars',{})
        if values.get(var):return values[var].strip('"\'')
        path=values.get('parent_type') or path.rsplit('/',1)[0]
    return None
records={};other=[]
for path in ns['types']:
    if not path.startswith('/obj/machinery/vending'):continue
    state=resolve(path,'icon_state')
    if state not in STATES:
        if state:other.append({'type':path,'state':state,'icon':resolve(path,'icon')})
        continue
    records.setdefault(state,{'state':state,'metadata':STATES[state],'types':[]})['types'].append({'type':path,'light_mask':resolve(path,'light_mask'),'panel_type':resolve(path,'panel_type')})
names=sorted(records)
for batch in range((len(names)+33)//34):
    subset=names[batch*34:(batch+1)*34]
    out=Image.new('RGB',(6*190,((len(subset)+5)//6)*180),(39,45,54));draw=ImageDraw.Draw(out)
    for i,name in enumerate(subset):
        x,y,w,h=STATES[name]['rects'][0];tile=image.crop((x,y,x+w,y+h));records[name]['opaque_bbox']=tile.getbbox()
        tile=tile.resize((128,128),Image.Resampling.NEAREST);ox=i%6*190;oy=i//6*180
        out.paste(tile,(ox+20,oy+28),tile);draw.text((ox+6,oy+5),name,fill='white')
    out.save(ART/f'vending_all_reference_{batch+1}.png')
report={'source_png':str(PNG.relative_to(ROOT)),'png_sha256':hashlib.sha256(PNG.read_bytes()).hexdigest(),'pixels_equal_original_dmi':True,'tile_px':[32,32],'sheet_px':list(image.size),'state_count':len(STATES),'vendors':records,'other_atlases':other}
(OUT/'source_catalog.json').write_text(json.dumps(report,indent=2)+'\n')
print('Vendor appearances:',len(names),'Other atlas states:',sorted({x['state'] for x in other}))
