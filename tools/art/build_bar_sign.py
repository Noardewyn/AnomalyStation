"""Original-atlas rectangular bar signs with separate emissive regions."""
from pathlib import Path
import bpy,bmesh,ast,json
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/bar_sign';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={};shared=[]
p=ROOT/'godot/assets/tg/icons/obj/machines/barsigns.png';IM=bpy.data.images.load(str(p),check_existing=False);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
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

def uv(st,x,y):
 tx,ty,_,_=ST[st]['rects'][0];return ((tx+x)/IM.size[0],1-(ty+y)/IM.size[1])
def trans(p):return ((p[0]-32)*S,-p[1]*S,(p[2]-12.5)*S)
PIX=list(IM.pixels)
variants=['empty', 'thegreytide', 'honkednloaded', 'slipperyshots', 'thecavern', 'theouterspess', 'syndibarsign', 'maidcafe', 'the_lightbulb', 'goose', 'chalkboard_menu', 'le_cafe_silencieux', 'rock-bottom', 'the-red-mons', 'the-wizard', 'thesmallrounyinn', 'cold-ones', 'thesingulo', 'officerbeersky']
special_variants=['maltesefalcon','oldcockinn','alohasnackbar','maltroach','neon-flamingo','wrong-turn','pun-puns-pub','doctors-orders']
special_variants += ['scotchservinwill', 'thebark', 'thedrunkcarp', 'theharmbaton', 'empbarsign', 'thenest', 'thecoderbus', 'theadminbus', 'thewretchedhive', 'robustacafe', 'emergencyrumparty', 'combocafe', 'vladssaladbar', 'theshaken', 'thealenath', 'thenet', 'orangejuice', 'the-assembly-line', 'little_treats', 'bargonia', 'cult-cove', 'slowdive', 'therune', 'moths-moths-moths']
variants += special_variants
special_cache={}
for st in variants:
 key='bar_sign_'+st;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ovs=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':st,'overlay_states':ovs,'frame':0}
 mat=material('Bar_Sign_'+st+'_Original');nodes=mat.node_tree.nodes;links=mat.node_tree.links;t=next(n for n in nodes if n.type=='TEX_IMAGE');co=nodes.new('ShaderNodeTexCoord');mp=nodes.new('ShaderNodeMapping');tx,ty,_,_=ST[st]['rects'][0];mp.inputs['Location'].default_value=(tx/IM.size[0],-ty/IM.size[1],0);links.new(co.outputs['UV'],mp.inputs['Vector']);links.new(mp.outputs['Vector'],t.inputs['Vector'])
 if st in special_variants:
  # Preserve original nonrectangular outlines, including protruding decorations.
  silhouette={(x,29-y) for y in range(32) for x in range(64) if PIX[((IM.size[1]-1-ty-y)*IM.size[0]+tx+x)*4+3]>0}
  # Pick a fully opaque casing strip near the source's left border for hidden views.
  best=None
  for bx in range(12):
   for by in range(32):
    for bw in range(1,5):
     for bh in range(1,17):
      if bx+bw>64 or by+bh>32:continue
      if all((xx,29-yy) in silhouette for xx in range(bx,bx+bw) for yy in range(by,by+bh)):
       score=bw*bh
       if best is None or score>best[0]:best=(score,(bx,by,bx+bw,by+bh))
  strip=best[1]
  def edge_tex(p,c,a,sg):
   x,y,z=p
   if a==1 and sg==1:return uv('empty',x,30-z)
   return patch('empty',strip,y/3 if a==0 else x/64,(32-z)/32 if a!=2 else y/3)
  signature=tuple(sorted(silhouette))
  if signature not in special_cache:
   ob=(corner_safe_solid if st=='little_treats' else solid)('Shaped_Bar_Sign_Housing',{(x,y,z) for x,z in silhouette for y in range(3)},trans,edge_tex,mat);special_cache[signature]=ob
  else:
   ob=special_cache[signature].copy();scene.collection.objects.link(ob);parts.append(ob);ob.material_slots[0].link='OBJECT';ob.material_slots[0].material=mat
 elif not shared:
  def tex(p,c,a,sg):
   x,y,z=p
   if a==1 and sg==1:return uv('empty',x,30-z)
   if a==2 and sg==1:return patch('empty',(3,3,61,5),(x-2)/60,y/3)
   if a==0:return patch('empty',(2,5,4,29),y/3,(25-z)/25)
   if a==2:return patch('empty',(3,28,61,30),(x-2)/60,y/3)
   return patch('empty',(2,5,4,29),(x-2)/60,(25-z)/25)
  solid('Continuous_Bar_Sign_Housing',{(x,y,z) for x in range(2,62) for y in range(3) for z in range(25)},trans,tex,mat)
  shared=list(parts)
 else:
  for src in shared:
   ob=src.copy();scene.collection.objects.link(ob);parts.append(ob);ob.material_slots[0].link='OBJECT';ob.material_slots[0].material=mat
 mask=st+'-light-mask'
 if mask in ST:
  mx,my,_,_=ST[mask]['rects'][0];verts=[];faces=[];coords=[]
  for y in (range(32) if st in special_variants else range(5,30)):
   for x in (range(64) if st in special_variants else range(2,62)):
    alpha=PIX[((IM.size[1]-1-my-y)*IM.size[0]+mx+x)*4+3]
    if alpha<.5:continue
    corners=[(x,y+1),(x+1,y+1),(x+1,y),(x,y)];n=len(verts);verts.extend([trans((xx,3.01,30-yy)) for xx,yy in corners]);faces.append(tuple(range(n,n+4)));coords.append([uv('empty',xx,yy) for xx,yy in corners])
  if faces:
   glow=mat.copy();glow.name='Bar_Sign_'+st+'_Light_Overlay';bs=next(n for n in glow.node_tree.nodes if n.type=='BSDF_PRINCIPLED');t=next(n for n in glow.node_tree.nodes if n.type=='TEX_IMAGE');glow.node_tree.links.new(t.outputs['Color'],bs.inputs['Emission Color']);bs.inputs['Emission Strength'].default_value=1
   me=bpy.data.meshes.new('Light_Overlay');me.from_pydata(verts,[],faces);me.materials.append(glow);u=me.uv_layers.new()
   for poly,cc in zip(me.polygons,coords):
    for li,co in zip(poly.loop_indices,cc):u.data[li].uv=co
   bm=bmesh.new();bm.from_mesh(me);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0000001);bmesh.ops.dissolve_limit(bm,angle_limit=.001,verts=list(bm.verts),edges=list(bm.edges),delimit={'UV'});bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
   ob=bpy.data.objects.new('Light_Overlay',me);scene.collection.objects.link(ob);parts.append(ob);ovs.append(mask)
 for ob in parts:ob['source_state']=st
bpy.app.driver_namespace['bar_sign_build']=globals()
