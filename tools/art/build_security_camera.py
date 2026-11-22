"""Reference-first security camera, original atlas only. Run through Blender MCP."""
from pathlib import Path
import bpy,bmesh,json,math,hashlib,ast
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/security_camera';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={}
path=ROOT/'godot/assets/tg/icons/obj/machines/camera.png'
IM=bpy.data.images.load(str(path),check_existing=False);PIX=list(IM.pixels);META=json.loads(path.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
# Reuse the checked closed boundary-mesh builder, without executing its asset generation.
tree=ast.parse((ROOT/'tools/art/build_transport_assets.py').read_text())
exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='solid'],type_ignores=[]),'shared_boundary_mesh','exec'))
def uv(state,x,y,di=0,frame=3):
 s=ST[state];tx,ty,_,_=s['rects'][di*s['frames']+min(frame,s['frames']-1)]
 return ((tx+x)/IM.size[0],1-(ty+y)/IM.size[1])
def pixel(state,x,y,frame=0):
 tx,ty,_,_=ST[state]['rects'][frame];i=((IM.size[1]-1-ty-y)*IM.size[0]+tx+x)*4;return PIX[i:i+4]
def mat(name,alpha=1,emission=False):
 if name in MATS:return MATS[name]
 m=bpy.data.materials.new(name);m.use_nodes=True;bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.65;bs.inputs['Alpha'].default_value=alpha
 t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=IM;t.interpolation='Closest';m.node_tree.links.new(t.outputs['Color'],bs.inputs['Base Color'])
 if emission:
  m.node_tree.links.new(t.outputs['Color'],bs.inputs['Emission Color']);m.node_tree.links.new(t.outputs['Alpha'],bs.inputs['Alpha']);bs.inputs['Emission Strength'].default_value=1
 MATS[name]=m;return m
metal=mat('Camera_Housing_Original');glass=mat('Camera_Lens_Glass_Original',.88);overlaymat=mat('Camera_State_Overlay_Original',emission=True)
def trans(p):return ((p[0]-2.5)*S,-p[1]*S,(p[2]-2)*S)
def patch(state,box,u,v,di=0,frame=3):
 x0,y0,x1,y1=box;return uv(state,x0+.02+(x1-x0-.04)*u,y0+.02+(y1-y0-.04)*v,di,frame)
def parent_at(ob,parent,pivot):
 for v in ob.data.vertices:v.co-=pivot
 ob.parent=parent;ob.location=(0,0,0)
def overlay(name,state,kind='indicator'):
 verts=[];faces=[];coords=[]
 frame=3 if kind=='indicator' else 0
 for y in range(32):
  for x in range(32):
   if pixel(state,x,y,min(frame,ST[state]['frames']-1))[3]<.01:continue
   cr=[(x,y+1),(x+1,y+1),(x+1,y),(x,y)];n=len(verts)
   for xx,yy in cr:
    if kind=='indicator':
     if y<21:verts.append(((xx-5.5)*S,-(12-(21-yy))*S,5.025*S))
     else:verts.append(((xx-5.5)*S,-12.025*S,(27-yy-2)*S))
    elif kind=='panel':verts.append(((4.20-2.5)*S,-(yy-24)*S,(10-xx-2)*S))
    else:verts.append(((xx-5.5)*S,-12.08*S,(32-yy-2)*S))
   faces.append(tuple(range(n,n+4)));coords.append([uv(state,xx,yy,frame=frame) for xx,yy in cr])
 me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.materials.append(overlaymat if kind!='panel' else metal);layer=me.uv_layers.new(name='Original_Overlay_UV')
 for p,tc in zip(me.polygons,coords):
  for li,co in zip(p.loop_indices,tc):layer.data[li].uv=co
 ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob);parts.append(ob);ob['source_state']=state;ob['source_frame']=frame;return ob

for family in ['camera','xraycamera']:
 for status in ['on','in_use','off','emp','panel_open']:
  key=('security_camera' if family=='camera' else 'security_camera_xray')+'_'+status
  scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[]
  ASSETS[key]={'scene':scene,'parts':parts,'status':status,'family':family}
  # Wall plate and stepped arm are one connected solid, not intersecting blocks.
  bracket={(x,0,z) for x in range(-1,6) for z in range(4)}
  bracket|={(x,y,z) for y in range(1,7) for x in [1,2,3] for z in range(1+(y>=3),3+(y>=3))}
  # Service pocket with lined walls; the cover is an independent hinged part.
  bracket|={(x,y,z) for x in [1,2,3] for y in range(2,6) for z in range(1,5) if x==1 or y in [2,5] or z in [1,4]}
  bracket-={(x,y,z) for x in [2,3] for y in [3,4] for z in [2,3]}
  def bt(p,c,a,sg):
   x,y,z=p
   if c[1]==0:return patch('camera',(1,30,8,32),(x+1)/7,z/4)
   if a==2:return patch('camera',(4,26,6,29),(x-1)/5,y/7)
   return patch('camera',(4,26,6,29),y/7,z/5)
  solid('Wall_Plate_and_Stepped_Arm',bracket,trans,bt,metal)
  hinge=bpy.data.objects.new('Head_Pan_Pivot',None);scene.collection.objects.link(hinge);pivot=Vector(trans((2.5,7,3)));hinge.location=pivot;hinge['rotation_axis']='Z';hinge['source_scan_frames']=7
  # Five-pixel wide head, six pixels deep, four high; squared one-pixel steps.
  cross={(x,z) for x in range(5) for z in range(3,7) if not (x in [0,4] and z in [3,6])}
  head={(x,y,z) for x,z in cross for y in range(6,12)}
  # Actual recessed front aperture, with a closed rear wall and four lined sides.
  head-={(2,y,z) for y in [10,11] for z in [4,5]}
  def ht(p,c,a,sg):
   x,y,z=p
   if a==2:return patch('camera',(3,22,8,25),x/5,(12-y)/6)
   if a==0:return patch('camera',(22,4,26,7),(12-y)/6,(7-z)/4,2)
   return patch('camera',(3,22,8,25),x/5,(7-z)/4)
  ob=solid('Stepped_Camera_Head',head,trans,ht,metal);parent_at(ob,hinge,pivot)
  if family=='xraycamera':
   # The blue upgrade pixel extends one full pixel beyond the normal head.
   ob=solid('Xray_Module_Dock',{(-1,y,5) for y in [10,11]},trans,lambda p,c,a,sg:patch('camera',(4,22,7,24),(p[1]-10)/2,p[2]-5),metal);parent_at(ob,hinge,pivot)
  # Separate glass with finite thickness inside the aperture, not an alpha hole.
  lens={(2,0,z) for z in [4,5]}
  def lt(p,c,a,sg):return patch('camera_off',(4,21,5,22),p[0]-2,(6-p[2])/2,frame=0)
  ob=solid('Recessed_Lens_Glass',lens,lambda p:trans((p[0],10.8+p[1]*.2,p[2])),lt,glass);parent_at(ob,hinge,pivot)
  # Hinge on rear end of a real service door beside the arm.
  panelpivot=Vector(trans((4,3,2)))
  ob=solid('Service_Panel', {(0,y,z) for y in [3,4] for z in [2,3]},lambda p:trans((4+p[0]*.18,p[1],p[2])),lambda p,c,a,sg:patch('camera',(4,26,6,29),(p[1]-3)/2,(4-p[2])/2),metal)
  for v in ob.data.vertices:v.co-=panelpivot
  ob.location=panelpivot;ob['hinge_axis']='Z'
  if status=='panel_open':
   ob.rotation_euler.z=math.radians(-100);door=ob;marker=overlay('Service_Panel_State_Overlay','camera_panel','panel');parent_at(marker,door,panelpivot)
  if status in ['on','in_use','panel_open']:
   state=family+('_in_use' if status=='in_use' else '_on');ob=overlay('Status_Emission_Overlay',state);parent_at(ob,hinge,pivot)
  if status in ['off','emp']:
   # The source disabled state rests at one end of its scanning arc.
   hinge.rotation_euler.z=math.radians(25)
  if status=='emp':overlay('EMP_Discharge_Overlay',family+'_emp','emp')
  ASSETS[key]['base_state']=family+('_off' if status in ['off','emp'] else '')
  ASSETS[key]['overlay_states']=[o['source_state'] for o in parts if 'source_state' in o]

cache={}
for a in ASSETS.values():
 for o in a['parts']:
  me=o.data;sig=hashlib.sha256(repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[tuple(u.uv) for u in me.uv_layers.active.data],[m.name for m in me.materials])).encode()).hexdigest()
  if sig in cache:o.data=cache[sig]
  else:cache[sig]=me
bpy.app.driver_namespace['security_camera_build']=globals()
print('Built',len(ASSETS),'camera states with',len(cache),'shared meshes')
