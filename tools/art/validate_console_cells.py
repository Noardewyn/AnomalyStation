"""Check console body UVs after the runtime's CellUvs remapping to live tiles."""
from pathlib import Path
from PIL import Image
import ast, json, math, struct
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'godot/assets/models/machines/consoles'
tree=ast.parse((ROOT/'tools/art/validate_station_props.py').read_text())
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='accessor'],type_ignores=[]),'glb_accessor','exec'))
atlas=ROOT/'godot/assets/tg/icons/obj/machines/computer.png'
im=Image.open(atlas).convert('RGBA');states={s['name']:s for s in json.loads(atlas.with_suffix('.json').read_text())['states']}
report={}
for key,names in [('console_standard',['computer']),('console_bronze',['ratvarcomputer1','ratvarcomputer2','ratvarcomputer3','ratvarcomputer4'])]:
 raw=(OUT/(key+'.glb')).read_bytes();n=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+n]);blob=raw[28+n:]
 node=next(n for n in g['nodes'] if n['name']=='Body')
 for name in names:
  tx,ty,_,_=states[name]['rects'][0];count=bad=0
  for p in g['meshes'][node['mesh']]['primitives']:
   uv=accessor(g,blob,p['attributes']['TEXCOORD_0']);ids=[i[0] for i in accessor(g,blob,p['indices'])]
   ox=math.floor((min(u[0] for u in uv)*im.width+.01)/32)*32
   oy=math.floor((min(u[1] for u in uv)*im.height+.01)/32)*32
   local=[(max(0,min(32,u*im.width-ox)),max(0,min(32,v*im.height-oy))) for u,v in uv]
   assert all(0<=u*im.width-ox<=32.0001 and 0<=v*im.height-oy<=32.0001 for u,v in uv),(key,'body crosses tile boundary')
   for i in range(0,len(ids),3):
    pts=[local[j] for j in ids[i:i+3]]
    for a in range(1,24):
     for b in range(1,25-a):
      w=(a/25,b/25,1-(a+b)/25);x,y=[min(31,int(sum(pts[k][d]*w[k] for k in range(3)))) for d in range(2)]
      count+=1;bad+=im.getpixel((tx+x,ty+y))[3]!=255
  report[key+'/'+name]={'samples':count,'nonopaque_hits':bad}
  assert bad==0,(key,name,bad)
(OUT/'runtime_cell_validation.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report,indent=2))
