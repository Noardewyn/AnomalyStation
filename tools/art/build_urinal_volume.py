"""Wall emergency tool cabinets with removable tools and hinged original-atlas glass."""
from pathlib import Path
import bpy,bmesh,ast,json,math,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/station_props/bathroom';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={};shared=[]
for f,names in [('build_transport_assets.py',['solid']),('build_cell_charger.py',['patch','material'])]:
 tree=ast.parse((ROOT/'tools/art'/f).read_text());exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'helpers','exec'))
# Duplicate vertices at point-only contacts instead of welding four surface sheets.
# This preserves every original pixel edge while keeping each surface manifold.
source=(ROOT/'tools/art/build_transport_assets.py').read_text();tree=ast.parse(source)
fn=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='solid')
code=ast.get_source_segment(source,fn).replace('def solid(', 'def corner_safe_solid(')
a=code.index(' def vi(p):');b=code.index(' for c in sorted(cells):',a)
code=code[:a]+""" def vi(p,c):
  local={(x,y,z) for x in [p[0]-1,p[0]] for y in [p[1]-1,p[1]] for z in [p[2]-1,p[2]] if (x,y,z) in cells}
  component={c};pending=[c]
  while pending:
   q=pending.pop()
   for axis in range(3):
    for sign in [-1,1]:
     n=list(q);n[axis]+=sign;n=tuple(n)
     if n in local and n not in component:component.add(n);pending.append(n)
  key=(p,min(component))
  if key not in lut:lut[key]=len(verts);verts.append(transform(p))
  return lut[key]
"""+code[b:]
code=code.replace('vi(p) for p in corners','vi(p,c) for p in corners');exec(compile(code,'corner_safe_boundary','exec'))



p=ROOT/'godot/assets/tg/icons/obj/watercloset.png';IM=bpy.data.images.load(str(p),check_existing=False);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']};PIX=list(IM.pixels);st='urinal'
def uv(st,x,y):return (x/IM.size[0],1-y/IM.size[1])
def alpha(x,y):return PIX[((IM.size[1]-1-y)*IM.size[0]+x)*4+3]
def trans(p):return ((p[0]-16)*S,-p[1]*S,.4+p[2]*S)
scene=bpy.data.scenes.new('urinal');bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ASSETS={'urinal':{'scene':scene,'parts':parts,'base_state':st,'frame':0,'overlay_states':[]}}
mat=material('Urinal_Porcelain_Original_Atlas')
def depth(z):return 9 if z<9 else 8 if z<15 else 6
cells={(x,y,z) for x in range(7,25) for z in range(24) for y in range(depth(z)) if alpha(x,31-z)>.5}
cells-={(x,y,z) for x in range(12,20) for z in range(5,22) for y in range(2,10)}
def tx(p,c,a,sg):
 x,y,z=p
 if a==1 and sg==1:
  if y==2:return patch(st,(12,10,20,25),(x-12)/8,(22-z)/17)
  return uv(st,x,32-z)
 if a==0:
  if x in [12,20] and 5<=z<=22:return patch(st,(11,10,13,25),(y-2)/7,(22-z)/17)
  return patch(st,(22,14,24,26),y/9,(24-z)/24)
 if a==2 and sg==1:
  if z<=9:return patch(st,(12,26,20,28),(x-12)/8,y/9)
  return patch(st,(10,7,23,9),(x-7)/18,y/9)
 return patch(st,(9,14,11,26),(x-7)/18,(24-z)/24 if a==1 else y/9)
corner_safe_solid('Continuous_Lined_Ceramic_Bowl',cells,trans,tx,mat)
pipe={(x,0,z) for x in range(14,18) for z in range(24,29) if alpha(x,31-z)>.5}
def pt(p,c,a,sg):
 if a==1:return uv(st,p[0],32-p[2])
 return patch(st,(15,3,17,8),p[1],(29-p[2])/5)
solid('Flush_Riser_Pipe',pipe,lambda p:trans((p[0],1+p[1]*2,p[2])),pt,mat)
valve={(x,0,z) for x in range(13,19) for z in range(29,32) if alpha(x,31-z)>.5}
def vt(p,c,a,sg):
 if a==1:return uv(st,p[0],32-p[2])
 return patch(st,(14,1,18,3),(p[0]-13)/6,p[1])
ob=corner_safe_solid('Flush_Valve_Head',valve,lambda p:trans((p[0],.5+p[1]*3,p[2])),vt,mat)
cap={(x,y,0) for x in range(13,19) for y in range(3,7)}
ob=solid('Removable_Drain_Protector',cap,lambda p:trans((p[0],p[1],5+p[2]*.15)),lambda p,c,a,sg:patch(st,(13,25,17,26),(p[0]-13)/6,(p[1]-3)/4),mat)
pivot=Vector(trans((16,5,5)))
for v in ob.data.vertices:v.co-=pivot
ob.location=pivot;ob['removal_axis']='local Z';ob['source_behavior']='screwdriver removes cap; no alternative sprite or animation'
for o in parts:o['source_state']='urinal'
bpy.app.driver_namespace['wall_volume_finish_build']={'BATCH':{'urinal':{'ROOT':ROOT,'OUT':OUT,'ASSETS':ASSETS,'META':META}}}
p=ROOT/'tools/art/export_wall_volume_finish.py';code=p.read_text().replace('target=Vector((1.575,0,-.5))','target=Vector((1.05,0,.85))').replace('data.ortho_scale=4.6','data.ortho_scale=3.5').replace('Vector((0,-.10,-.41))','Vector((0,-.10,.24))')
exec(compile(code,str(p),'exec'),{'__file__':str(p)})
