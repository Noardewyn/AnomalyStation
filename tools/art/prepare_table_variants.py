from pathlib import Path
from PIL import Image
from collections import Counter
import json,hashlib
ROOT=Path(__file__).resolve().parents[2]
SRC=ROOT/'godot/assets/tg/icons/obj/smooth_structures'
OUT=ROOT/'godot/assets/models/furniture/table_kits';(OUT/'textures').mkdir(parents=True,exist_ok=True)
families={'metal':['table','table_greyscale'],'wood':['wood_table','poker_table'],'cloth':[p.stem for p in sorted(SRC.glob('fancy_table*.png'))],'glass':['glass_table','plasmaglass_table'],'framed_glass':['rglass_table','rplasmaglass_table','titaniumglass_table','plastitaniumglass_table','alien_table'],'reinforced':['reinforced_table','brass_table'],'rolling':['rollingtable']}
manifest={}
for family,names in families.items():
 for name in names:
  path=SRC/(name+'.png');im=Image.open(path).convert('RGBA')
  def sample(x,y):
   c=im.getpixel((int(x),int(y)))
   return c[:3]+(255,) if c[3] else im.getpixel((16,12))[:3]+(255,)
  out=Image.new('RGBA',(128,128),sample(16,24))
  def patch(x,y,w,h,fn):
   for j in range(-1,h+1):
    for i in range(-1,w+1):out.putpixel((x+i,y+j),fn(max(0,min(w-1,i)),max(0,min(h-1,j))))
  for mask in range(16):
   def top(x,y,m=mask):
    inner_corner=m==7
    if inner_corner:m=3  # Reserved atlas slot for the two-leg concave turn.
    # A modular surface is rotationally symmetric at joining edges.
    # Keep source texels, but do not bake a whole isolated frame/reflection
    # into every connected cell. The old clamped crop left internal rails.
    distances=[(y,1),(27-x,2),(27-y,4),(x,8)]
    d=min([v for v,bit in distances if not m&bit],default=28)
    if inner_corner:d=min(d,max(27-x,y))
    if d<4:return sample(16,2+d)
    if family=='cloth':
     # Corner embroidery exists only at a real external corner.
     for cx,cy,bits in [(0,0,9),(27,0,3),(27,27,6),(0,27,12)]:
      dx=abs(x-cx);dy=abs(y-cy)
      if m&bits==0 and dx<9 and dy<9:return sample(2+dx,2+dy)
    # Reflection stripes are meaningful on an isolated pane, but cannot be
    # rotated independently across a continuous multi-cell glass surface.
    if m==0 and family in ['glass','framed_glass']:
     return sample(2+x,2+y*20/27)
    return interior
   interior=Counter(sample(x,y) for x in range(8,25) for y in range(8,20)).most_common(1)[0][0]
   patch(1+30*(mask%4),1+30*(mask//4),28,28,top)
  patch(122,12,1,3,lambda x,y:sample(16,22+y))
  patch(122,1,3,6,lambda x,y:sample(3+x,25+y))
  patch(122,19,2,2,lambda x,y:sample(16,24))
  # Additional full-width cloth/frieze strip, inside the unused right margin.
  patch(122,25,3,28,lambda x,y:sample(2+y,25+x*3) if family=='cloth' else sample(2+y,24+x))
  out.save(OUT/'textures'/(name+'.png'))
  manifest[name]={'family':family,'texture':'textures/'+name+'.png','source':str(path.relative_to(ROOT/'godot')).replace('\\','/'),'source_sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
(OUT/'variants.json').write_text(json.dumps(manifest,indent=2)+'\n')
print({k:len(v) for k,v in families.items()})
