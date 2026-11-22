"""Original-atlas rectangular bar signs with separate emissive regions."""
from pathlib import Path
import bpy,bmesh,ast,json
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/structures/warning_sign';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={};shared=[]
p=ROOT/'godot/assets/tg/icons/obj/signs.png';IM=bpy.data.images.load(str(p),check_existing=False);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
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

def uv(st,x,y):return (x/IM.size[0],1-y/IM.size[1])
def trans(p):return ((p[0]-16)*S,-p[1]*S*.35,(p[2]-16)*S)
PIX=list(IM.pixels);cache={}
variants=json.loads((OUT/'source_audit.json').read_text())['variants']
for st in variants:
 key='warning_sign_'+st;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':st,'overlay_states':[],'frame':0}
 tx,ty,_,_=ST[st]['rects'][0]
 shape={(x,31-y) for y in range(32) for x in range(32) if PIX[((IM.size[1]-1-ty-y)*IM.size[0]+tx+x)*4+3]>.5}
 # Rear uses a continuous opaque border row, preserving pixel variation and excluding symbols.
 rows=[]
 for z in sorted({z for x,z in shape}):
  xs=sorted(x for x,zz in shape if zz==z)
  if xs==list(range(xs[0],xs[-1]+1)):rows.append((len(xs),z,xs[0],xs[-1]+1))
 width,zz,x0,x1=max(rows,key=lambda q:(q[0],-q[1])); strip=(x0,31-zz,x1,32-zz)
 mat=material('Warning_Sign_'+st+'_Original');nodes=mat.node_tree.nodes;links=mat.node_tree.links;t=next(n for n in nodes if n.type=='TEX_IMAGE');co=nodes.new('ShaderNodeTexCoord');mp=nodes.new('ShaderNodeMapping');mp.inputs['Location'].default_value=(tx/IM.size[0],-ty/IM.size[1],0);links.new(co.outputs['UV'],mp.inputs['Vector']);links.new(mp.outputs['Vector'],t.inputs['Vector'])
 signature=tuple(sorted(shape))
 if signature in cache:
  ob=cache[signature].copy();scene.collection.objects.link(ob);parts.append(ob);ob.material_slots[0].link='OBJECT';ob.material_slots[0].material=mat
 else:
  def tex(p,c,a,sg):
   x,y,z=p
   if a==1 and sg==1:return uv(st,x,32-z)
   if a==1:return patch(st,strip,x/32,(32-z)/32)
   # Follow the actual edge pixel through the complete physical thickness.
   cx,cy,cz=c
   return uv(st,cx+.02+y*.96,32-z) if a==0 else uv(st,x,31-cz+.02+y*.96)
  ob=corner_safe_solid('Continuous_Stepped_Sign_Plate',{(x,0,z) for x,z in shape},trans,tex,mat);cache[signature]=ob
 ob['source_state']=st;ob['thickness_m']=.35*S;ob['mounting']='wall; rotate reusable object, do not duplicate directional variants'
bpy.app.driver_namespace['warning_sign_build']=globals()
