"""Original-atlas door buttons, service covers and interchangeable electronics."""
from pathlib import Path
import bpy,bmesh,ast,json,math,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/wall_healer';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={}
p=ROOT/'godot/assets/tg/icons/obj/machines/wall_healer.png';IM=bpy.data.images.load(str(p),check_existing=False);PIX=list(IM.pixels);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
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


metal=material('Wall_Healer_Original_Atlas')
COLORS=[('Libital','ECEC8D'),('Aiuri','8C93FF'),('Syriniver','8CDF24'),('Blood','C80000')]
def linear(x):return x/12.92 if x<=.04045 else ((x+.055)/1.055)**2.4
bar_mats={}
for on in [False,True]:
 for label,h in COLORS:
  m=material('Wall_Healer_'+label+('_Lit' if on else '_Unlit'));bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');t=next(n for n in m.node_tree.nodes if n.type=='TEX_IMAGE');col=[linear(int(h[i:i+2],16)/255) for i in [0,2,4]]+[1]
  mix=m.node_tree.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[2].default_value=col;m.node_tree.links.new(t.outputs['Color'],mix.inputs[1]);m.node_tree.links.new(mix.outputs[0],bs.inputs['Base Color'])
  if on:m.node_tree.links.new(mix.outputs[0],bs.inputs['Emission Color']);bs.inputs['Emission Strength'].default_value=.25
  m['source_tint']=col;bar_mats[(on,label)]=m
shadow=material('Wall_Healer_Shadow_Overlay');bs=next(n for n in shadow.node_tree.nodes if n.type=='BSDF_PRINCIPLED');t=next(n for n in shadow.node_tree.nodes if n.type=='TEX_IMAGE');shadow.node_tree.links.new(t.outputs['Alpha'],bs.inputs['Alpha'])
glass=material('Wall_Healer_Glass_Original',alpha=.12)
glass.surface_render_method='BLENDED';shadow.surface_render_method='BLENDED'
next(n for n in glass.node_tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Roughness'].default_value=.2
def trans(p):return ((p[0]-16.5)*S,-p[1]*S,(p[2]-15.5)*S)
def box(name,x0,x1,y0,y1,depth,thickness,st,mat,offset=0):
 cells={(x,0,z) for x in range(x0,x1) for z in range(32-y1,32-y0)}
 def tex(p,c,a,sg):return patch(st,(x0-offset,y0,x1-offset,y1),(p[0]-x0)/(x1-x0),(32-y0-p[2])/(y1-y0))
 return solid(name,cells,lambda p:trans((p[0],depth+p[1]*thickness,p[2])),tex,mat)
variants=[('level_'+str(i),[i]*4,True,False) for i in range(8)]+[('mixed',[0,2,4,6],True,False),('off',[0]*4,False,False),('open',[0]*4,True,True)]
for mode,levels,on,opened in variants:
 key='wall_healer_'+mode;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':'wall_healer','overlay_states':['bar'+str(i) for i in levels]+['bar_shadow']+(['bar_emissive'] if on else [])+(['open'] if opened else [])};scene['bar_levels']=levels
 cells={(x,d,z) for x in range(7,26) for d in range(4) for z in range(5,26)}
 for x0 in [9,13,17,21]:cells-={(x,3,z) for x in range(x0,x0+3) for z in range(14,21)}
 mouth={(x,z) for x in range(14,19) for z in range(8,12)}|{(x,10) for x in [13,19]}
 cells-={(x,d,z) for x,z in mouth for d in [2,3]}
 def tex(p,c,a,sg):
  x,d,z=p
  if a==1 and sg==1:return uv('wall_healer',x,32-z)
  if a==2 and sg==1:
   if c[2]==25:return patch('wall_healer',(7,5,26,7),(x-7)/19,d/4)
   return patch('wall_healer',(7,24,9,26),(x-7)/19,d/4)
  if a==0:return patch('wall_healer',(7 if sg<0 else 24,6,9 if sg<0 else 26,26),d/4,(26-z)/21)
  return patch('wall_healer',(7,24,26,27),(x-7)/19,(26-z)/21)
 solid('Continuous_Medical_Housing',cells,trans,tex,metal)
 for i,(label,h) in enumerate(COLORS):
  x=9+i*4;st='bar'+str(levels[i]);ob=box(label+'_Reservoir',x,x+3,11,18,3.02,.35,st,bar_mats[(on,label)],i*4);ob['source_state']=st;ob['source_tint_hex']='#'+h
  box(label+'_Glass',x,x+3,11,18,3.4,.12,'bar_emissive',glass)
 # Preserve the source shadow as a separate alpha overlay above the windows.
 verts=[trans((x,3.54,32-y)) for x,y in [(9,11),(24,11),(24,13),(9,13)]];me=bpy.data.meshes.new('Bar_Shadow_Overlay');me.from_pydata(verts,[],[(0,3,2,1)]);me.materials.append(shadow);u=me.uv_layers.new(name='Original_Atlas_UV')
 for f in me.polygons:
  for li in f.loop_indices:
   x,y=[(9,11),(24,11),(24,13),(9,13)][me.loops[li].vertex_index];u.data[li].uv=uv('bar_shadow',x,y)
 ob=bpy.data.objects.new('Bar_Shadow_Overlay',me);scene.collection.objects.link(ob);parts.append(ob)
 hinge=bpy.data.objects.new('Service_Cover_Hinge',None);scene.collection.objects.link(hinge);hinge.location=trans((16.5,3.3,8));hinge['axis']='X'
 cover=box('Service_Cover',14,19,22,24,3.3,.15,'wall_healer',metal)
 for v in cover.data.vertices:v.co-=Vector(hinge.location)
 cover.parent=hinge;cover.location=(0,0,0)
 if opened:
  hinge.rotation_euler.x=math.radians(105)
  box('Service_Contacts',15,18,22,24,2.05,.2,'open',metal)
 anchor=bpy.data.objects.new('Hand_Insertion_Point',None);scene.collection.objects.link(anchor);anchor.location=trans((16.5,4,10.5));anchor['insertion_axis']='local +Y'
cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;h=repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[[tuple(v.uv) for v in u.data] for u in me.uv_layers],[m.name for m in me.materials]))
  if h in cache:ob.data=cache[h]
  else:cache[h]=me
bpy.app.driver_namespace['wall_healer_build']=globals();print('Built',len(ASSETS),'wall healer presets')
