"""Original-atlas floor vent pumps and scrubbers."""
from pathlib import Path
import bpy,bmesh,ast,json,math,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/gas_pumps';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={}
p=ROOT/'godot/assets/tg/icons/obj/machines/atmospherics/binary_devices.png';IM=bpy.data.images.load(str(p),check_existing=False);PIX=list(IM.pixels);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
tree=ast.parse((ROOT/'tools/art/build_transport_assets.py').read_text());exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='solid'],type_ignores=[]),'boundary_mesh','exec'))
def uv(st,x,y):
 tx,ty,_,_=ST[st]['rects'][0];return ((tx+x)/IM.size[0],1-(ty+y)/IM.size[1])
def pixel(st,x,y):
 tx,ty,_,_=ST[st]['rects'][0];i=((IM.size[1]-1-ty-y)*IM.size[0]+tx+x)*4;return PIX[i:i+4]
def patch(st,box,u,v):
 x0,y0,x1,y1=box;return uv(st,x0+.02+(x1-x0-.04)*max(0,min(1,u)),y0+.02+(y1-y0-.04)*max(0,min(1,v)))
def material(name,emission=False,alpha=1):
 m=bpy.data.materials.new(name);m.use_nodes=True;bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.7;bs.inputs['Alpha'].default_value=alpha
 t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=IM;t.interpolation='Closest';m.node_tree.links.new(t.outputs['Color'],bs.inputs['Base Color'])
 if emission:m.node_tree.links.new(t.outputs['Color'],bs.inputs['Emission Color']);bs.inputs['Emission Strength'].default_value=.4
 return m



metal=material('Gas_Pump_Original_Atlas');glow=material('Gas_Pump_Display_Emission',True)
pipepath=ROOT/'godot/assets/tg/icons/obj/pipes_n_cables/pipe_underlays.png'
PIM=bpy.data.images.load(str(pipepath),check_existing=False);PMETA=json.loads(pipepath.with_suffix('.json').read_text());PST={s['name']:s for s in PMETA['states']}
pipe_mat=material('Gas_Pump_Pipe_Original_Atlas');next(n for n in pipe_mat.node_tree.nodes if n.type=='TEX_IMAGE').image=PIM

def trans(p):return ((p[0]-15.5)*S,(20.5-p[1])*S,p[2]*S)
def pipeuv(st,x,y):
 tx,ty,_,_=PST[st]['rects'][0];return ((tx+x)/PIM.size[0],1-(ty+y)/PIM.size[1])
for family,modes in [('pressure',['off','on']),('volume',['off','on','overclocked'])]:
 for mode in modes:
  source='pump' if family=='pressure' else 'volpump';st=source+('_off-1' if mode=='off' else '_on-1');base='pump_off-1';key=family+'_gas_pump_'+mode
  scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':st,'overlay_states':[] if mode=='off' else [st+'-emissive']}
  # One continuous stepped shell, wider than the pipe, with a recessed top display.
  footprint={(x,y) for x in range(12,19) for y in range(13,28) if pixel(base,x,y)[3]>.99}
  cells={(x,y,z) for x,y in footprint for z in range(2,6)}-{(x,y,5) for x in range(14,17) for y in range(15,24)}
  def shell_uv(p,c,a,sg):
   x,y,z=p
   if a==2 and sg==1:return uv(base,x,y)
   if a==0:return patch(base,(12,14,14,25),(z-2)/4,(y-13)/15)
   if a==1:return patch(base,(13,14,18,15),(x-12)/7,(z-2)/4)
   return patch(base,(12,14,14,25),(x-12)/7,(y-13)/15)
  solid('Continuous_Pump_Housing',cells,trans,shell_uv,metal)
  # The panel is a closed removable part, with its original off/on state and light mask.
  def screen_uv(p,c,a,sg):
   if a==2 and sg==1:return uv(st,p[0],p[1])
   return patch(base,(12,14,14,25),(p[0]-14)/3,(p[1]-15)/9)
  ob=solid('Removable_Indicator_Panel',{(x,y,0) for x in range(14,17) for y in range(15,24)},lambda p:trans((p[0],p[1],5.05+p[2]*.15)),screen_uv,metal,simplify=False)
  pivot=Vector(trans((15.5,19.5,5.05)))
  for v in ob.data.vertices:v.co-=pivot
  ob.location=pivot;ob['removal_axis']='Z';ob['source_state']=st
  if mode!='off':
   ob.data.materials.append(glow)
   for face in ob.data.polygons:
    center=face.center+pivot;x=round(center.x/S+15.5-.5);y=round(20.5-center.y/S-.5)
    if face.normal.z>.9 and pixel(st+'-emissive',x,y)[3]>.01:face.material_index=1
  bm=bmesh.new();bm.from_mesh(ob.data);bmesh.ops.dissolve_limit(bm,angle_limit=.001,verts=list(bm.verts),edges=list(bm.edges),delimit={'UV','MATERIAL'});bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(ob.data);bm.free();ob.data.update()
  # Two separable pipe underlays. Cross-section is stepped along whole-pixel boundaries.
  for side,yr,ps in [('Input',range(0,16),'intact_1_3'),('Output',range(16,32),'intact_2_3')]:
   cross={(x,z) for x in range(13,18) for z in range(5) if (x not in [13,17] or z in [1,2,3])}
   pcells={(x,y,z) for x,z in cross for y in yr}
   def puv(p,c,a,sg):
    x,y,z=p
    if a==2 and sg==1:return pipeuv(ps,x,y)
    if a==0:return pipeuv(ps,13.02+(z/5)*4.96,y)
    if a==1:return pipeuv(ps,13.02+(x-13)/5*4.96,1.02+(z/5)*12.96 if side=='Input' else 17.02+(z/5)*12.96)
    return pipeuv(ps,x,y)
   solid(side+'_Pipe_Underlay',pcells,lambda p:((p[0]-15.5)*S,(16-p[1])*S,p[2]*S),puv,pipe_mat)
  # Original semi-transparent red flow mark, laid over the output pipe without baking it.
  m=material('Gas_Pump_Flow_Overlay');bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');t=next(n for n in m.node_tree.nodes if n.type=='TEX_IMAGE');m.node_tree.links.new(t.outputs['Alpha'],bs.inputs['Alpha'])
  me=bpy.data.meshes.new('Flow_Direction_Overlay');vs=[trans((x,y,5.005)) for x,y in [(14,30),(17,30),(17,32),(14,32)]];me.from_pydata(vs,[],[(3,2,1,0)]);me.materials.append(m);u=me.uv_layers.new(name='Original_Atlas_UV')
  for face in me.polygons:
   for li in face.loop_indices:
    x,y=[(14,30),(17,30),(17,32),(14,32)][me.loops[li].vertex_index];u.data[li].uv=uv(st,x,y)
  ob=bpy.data.objects.new('Flow_Direction_Overlay',me);scene.collection.objects.link(ob);parts.append(ob)
  if mode=='overclocked':
   ov='vpumpoverclock';ASSETS[key]['overlay_states'].append(ov)
   solid('Overclock_Indicator',{(15,y,0) for y in range(16,23)},lambda p:trans((p[0],p[1],5.21+p[2]*.03)),lambda p,c,a,sg:patch(ov,(15,16,16,23),p[0]-15,(p[1]-16)/7),glow)
cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;h=repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[[tuple(v.uv) for v in u.data] for u in me.uv_layers],[m.name for m in me.materials]))
  if h in cache:ob.data=cache[h]
  else:cache[h]=me
META={'width':32,'height':32,'states':[s for s in META['states'] if s['name'].startswith(('pump_','volpump_','vpumpoverclock'))]}
bpy.app.driver_namespace['gas_pumps_build']=globals();print('Built',len(ASSETS),'gas pumps')
