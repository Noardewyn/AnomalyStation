"""Missing solar variants, reusing the existing solar pedestal and pivots."""
from pathlib import Path
import bpy,bmesh,ast,json,math,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/station_props/power';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={}
p=ROOT/'godot/assets/tg/icons/obj/machines/solar.png';IM=bpy.data.images.load(str(p),check_existing=False);PIX=list(IM.pixels);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
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



metal=material('Solar_Additions_Original');glass=material('Solar_Additions_Tinted_Glass',alpha=.22)
# Load existing authored asset rather than regenerating a second pedestal.
with bpy.data.libraries.load(str(OUT/'solar_panel.blend'),link=False) as (src,dst):dst.scenes=src.scenes
reference=dst.scenes[0]
original={o.name.split('.')[0]:o for o in reference.objects}
def clone_base(key,tracker):
 global scene,parts
 scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;parts=[];mapping={}
 for src in reference.objects:
  if any(n in src.name for n in ['Panel_Backplate','Photovoltaic_Cells','Glass_Cover']):continue
  ob=src.copy();scene.collection.objects.link(ob);mapping[src]=ob;ob.name=key+'_'+src.name.split('solar_panel_',1)[-1].split('.')[0]
  if ob.type=='MESH':parts.append(ob)
 for src,ob in mapping.items():ob.parent=mapping.get(src.parent)
 if tracker:
  mats={}
  for ob in parts:
   ob.data=ob.data.copy()
   for i,m in enumerate(ob.data.materials):
    if m not in mats:
     mm=m.copy();mm.name='Solar_Tracker_Base_Original'
     for node in mm.node_tree.nodes:
      if node.type=='MAPPING':node.inputs['Location'].default_value=(.25,.25,0)
     mats[m]=mm
    ob.data.materials[i]=mats[m]
   if 'Pedestal' in ob.name or 'Swivel_Hub' in ob.name:
    ob.data.materials.clear();ob.data.materials.append(metal)
    for face in ob.data.polygons:
     zc=sum(ob.data.vertices[i].co.z for i in face.vertices)/len(face.vertices)
     upright=zc>.08
     rect=(14,1,18,24) if upright else (13,19,19,27)
     for li in face.loop_indices:
      v=ob.data.vertices[ob.data.loops[li].vertex_index].co
      u=(v.x+.07)/.14 if abs(face.normal.y)>.5 else (v.y+.07)/.14
      vv=(.58-v.z)/.53 if upright else (v.x+.4)/.8
      ob.data.uv_layers.active.data[li].uv=patch('tracker_base',rect,u,vv)
 ASSETS[key]={'scene':scene,'parts':parts,'base_state':'tracker_base' if tracker else 'sp_base','overlay_states':[]}
 return next(o for o in scene.objects if 'Tilt_Pivot' in o.name)
def planar(name,st,bottom,thickness,parent,mat):
 occupied={(x,y,0) for y in range(32) for x in range(32) if pixel(st,x,y)[3]>.99}
 def tex(p,c,a,sg):
  if a==2:return uv(st,p[0],p[1])
  # Each edge samples its own opaque boundary pixel, avoiding transparent padding.
  return patch(st,(c[0],c[1],c[0]+1,c[1]+1),p[0]-c[0] if a!=0 else p[1]-c[1],p[2])
 ob=solid(name,occupied,lambda p:((p[0]-16)*.046,(16-p[1])*.046,bottom+p[2]*thickness),tex,mat)
 ob.parent=parent;ob['source_state']=st;return ob
pivot=clone_base('solar_panel_broken',False)
planar('Broken_Backplate','solar_panel_glass-b',-.035,.035,pivot,metal)
planar('Broken_Photovoltaic_Layer','solar_panel_glass-b',.001,.006,pivot,metal)
planar('Broken_Glass_Cover','solar_panel_glass-b',.007,.006,pivot,glass)
ASSETS['solar_panel_broken']['overlay_states']=['solar_panel_glass-b']
pivot=clone_base('solar_tracker',True)
planar('Tracker_Edge','tracker_edge',-.035,.035,pivot,metal)
planar('Tracker_Surface','tracker',.001,.006,pivot,metal)
ASSETS['solar_tracker']['overlay_states']=['tracker_edge','tracker']
# The source's unmounted tracker assembly uses tracker_base, without dish overlays.
clone_base('solar_tracker_frame',True)
for ob in list(scene.objects):
 if any(n in ob.name for n in ['Pivot','Swivel_Hub']):
  if ob in parts:parts.remove(ob)
  bpy.data.objects.remove(ob,do_unlink=True)
cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;h=repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[tuple(v.uv) for v in me.uv_layers.active.data],[m.name for m in me.materials]))
  if h in cache:ob.data=cache[h]
  else:cache[h]=me
META={'width':32,'height':32,'states':[s for s in META['states'] if s['name'] in ['sp_base','solar_panel_glass-b','tracker_base','tracker','tracker_edge']]}
bpy.app.driver_namespace['solar_additions_build']=globals();print('Added',len(ASSETS),'missing solar variants; existing intact and frame files preserved')
