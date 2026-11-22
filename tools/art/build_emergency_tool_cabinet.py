"""Wall emergency tool cabinets with removable tools and hinged original-atlas glass."""
from pathlib import Path
import bpy,bmesh,ast,json,math,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/furniture/emergency_tool_cabinet';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={};shared=[]
p=ROOT/'godot/assets/tg/icons/obj/wallmounts.png';IM=bpy.data.images.load(str(p),check_existing=False);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
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

PIX=list(IM.pixels)
def uv(st,x,y):
 tx,ty,_,_=ST[st]['rects'][0];return ((tx+x)/IM.size[0],1-(ty+y)/IM.size[1])
def pixel(st,x,y):
 tx,ty,_,_=ST[st]['rects'][0];return PIX[((IM.size[1]-1-ty-y)*IM.size[0]+tx+x)*4:((IM.size[1]-1-ty-y)*IM.size[0]+tx+x)*4+4]
def trans(p):return ((p[0]-16)*S,-p[1]*S,(p[2]-7.5)*S)
def attach(ob,hinge):
 for v in ob.data.vertices:v.co-=hinge.location
 ob.parent=hinge;ob.location=(0,0,0)
cache={}
configs=[('empty',False,False,0,True),('loaded',True,False,0,True),('unlocked',True,False,0,False),('open',True,True,0,False),('open_empty',False,True,0,False)]+[('damage_'+str(i),True,False,i,True) for i in [1,2,3]]+[('broken',True,False,4,True),('broken_open',True,True,4,False)]
for family,base,tool in [('fire_axe','fireaxe','axe'),('mech_removal','mechremoval','crowbar'),('rescue_jaws','jaws_of_recovery','jaws')]:
 for label,loaded,opened,damage,locked in configs:
  key=family+'_cabinet_'+label;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ovs=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':base,'overlay_states':ovs,'frame':0}
  metal=material('Cabinet_'+family+'_Original')
  nodes=metal.node_tree.nodes;links=metal.node_tree.links;t=next(n for n in nodes if n.type=='TEX_IMAGE');co=nodes.new('ShaderNodeTexCoord');mp=nodes.new('ShaderNodeMapping');tx0,ty0,_,_=ST[base]['rects'][0];mp.inputs['Location'].default_value=((tx0-224)/IM.size[0],-ty0/IM.size[1],0);links.new(co.outputs['UV'],mp.inputs['Vector']);links.new(mp.outputs['Vector'],t.inputs['Vector'])
  cells={(x,y,z) for x in range(2,30) for y in range(4) for z in range(15) if not(z==0 and x in (2,29))};cells-={(x,y,z) for x in range(3,29) for y in range(1,4) for z in range(2,14)}
  def tx(p,c,a,sg):
   x,y,z=p
   if a==1 and sg==1:return uv('fireaxe',x,24-z)
   if a==2 and sg==1:return patch('fireaxe',(3,7,29,9),(x-2)/28,y/4)
   if a==0:return patch('fireaxe',(2,9,3,23),y/4,(15-z)/15)
   if a==1:return patch('fireaxe',(2,9,5,23),(x-2)/28,(15-z)/15)
   return patch('fireaxe',(3,8,29,9),(x-2)/28,(15-z)/15 if a==1 else y/4)
  corner_safe_solid('Continuous_Cabinet_Shell',cells,trans,tx,metal)
  if loaded:
   ovs.append(tool);tm=material('Cabinet_'+tool+'_Original');shape={(x,23-y) for y in range(32) for x in range(32) if pixel(tool,x,y)[3]>.5}
   def tt(p,c,a,sg):
    if a==1:return uv(tool,p[0],24-p[2])
    return patch(tool,(c[0],23-c[2],c[0]+1,24-c[2]),p[0]-c[0],p[2]-c[2])
   ob=corner_safe_solid('Removable_'+tool,{(x,0,z) for x,z in shape},lambda p:trans((p[0],1.3+p[1]*.8,p[2])),tt,tm)
   pivot=Vector(trans((16,1.3,7)))
   for v in ob.data.vertices:v.co-=pivot
   ob.location=pivot;ob['removal_axis']='local -Y'
  gs='glass'+(str(damage) if damage else '');ovs.append(gs+('_raised' if opened else ''))
  hinge=bpy.data.objects.new('Upper_Door_Hinge',None);scene.collection.objects.link(hinge);hinge.location=trans((16,4,15));hinge['axis']='X';hinge['open_degrees']=105
  gm=material('Cabinet_'+gs+'_Glass_Original');bs=next(n for n in gm.node_tree.nodes if n.type=='BSDF_PRINCIPLED');t=next(n for n in gm.node_tree.nodes if n.type=='TEX_IMAGE');gm.node_tree.links.new(t.outputs['Alpha'],bs.inputs['Alpha']);gm.surface_render_method='BLENDED'
  shape={(x,23-y) for y in range(9,22) for x in range(3,29) if pixel(gs,x,y)[3]>0}
  def gt(p,c,a,sg):
   if a==1:return uv(gs,p[0],24-p[2])
   return patch(gs,(c[0],23-c[2],c[0]+1,24-c[2]),p[0]-c[0],p[2]-c[2])
  ob=corner_safe_solid('Closed_Thickness_Door_Glass',{(x,0,z) for x,z in shape},lambda p:trans((p[0],4+p[1]*.12,p[2])),gt,gm);attach(ob,hinge)
  # Opaque handle fills the deliberate handle cutout in source glass artwork.
  hm=material('Cabinet_Handle_Original');ob=solid('Door_Handle',{(x,0,z) for x in range(13,19) for z in range(3,5)},lambda p:trans((p[0],4.13+p[1]*.25,p[2])),lambda p,c,a,sg:patch('glass',(13,20,19,21),(p[0]-13)/6,(5-p[2])/2),hm);attach(ob,hinge)
  lock='unlocked' if opened or not locked else 'locked'
  if not opened:ovs.append(lock)
  lm=material('Cabinet_'+lock+'_Overlay',emission=True);bs=next(n for n in lm.node_tree.nodes if n.type=='BSDF_PRINCIPLED');t=next(n for n in lm.node_tree.nodes if n.type=='TEX_IMAGE');lm.node_tree.links.new(t.outputs['Alpha'],bs.inputs['Alpha']);lm.surface_render_method='BLENDED'
  corners=[(10,23),(22,23),(22,16),(10,16)];me=bpy.data.meshes.new('Lock_Overlay');me.from_pydata([trans((x,4.40,24-y)) for x,y in corners],[],[(0,1,2),(0,2,3)]);me.materials.append(lm);u=me.uv_layers.new()
  for poly in me.polygons:
   for li in poly.loop_indices:
    x,y=corners[me.loops[li].vertex_index];u.data[li].uv=uv(lock,x,y)
  ob=bpy.data.objects.new('Lock_Overlay',me);scene.collection.objects.link(ob);parts.append(ob);attach(ob,hinge)
  if opened:hinge.rotation_euler.x=-math.radians(105)
  for ob in parts:
   me=ob.data;signature=repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[tuple(v.uv) for v in me.uv_layers.active.data],[m.name for m in me.materials]))
   # Material names are intentionally excluded: identical UV/geometry can share slots via object overrides.
   signature=repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[tuple(v.uv) for v in me.uv_layers.active.data]))
   if signature in cache:
    old=ob.data;mats=[slot.material for slot in ob.material_slots];ob.data=cache[signature]
    for slot,mt in zip(ob.material_slots,mats):slot.link='OBJECT';slot.material=mt
    bpy.data.meshes.remove(old)
   else:cache[signature]=me
bpy.app.driver_namespace['emergency_tool_cabinet_build']=globals()
