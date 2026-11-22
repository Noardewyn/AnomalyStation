"""Original-atlas fire alarm with removable cover, pull handle and source overlays."""
from pathlib import Path
import bpy,bmesh,ast,json,hashlib,math
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/fire_alarm';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={}
p=ROOT/'godot/assets/tg/icons/obj/machines/wallmounts.png';IM=bpy.data.images.load(str(p),check_existing=False);PIX=list(IM.pixels);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
tree=ast.parse((ROOT/'tools/art/build_transport_assets.py').read_text());exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='solid'],type_ignores=[]),'shared_boundary_mesh','exec'))
def uv(st,x,y,frame=0):
 tx,ty,_,_=ST[st]['rects'][frame];return ((tx+x)/IM.size[0],1-(ty+y)/IM.size[1])
def pixel(st,x,y):
 tx,ty,_,_=ST[st]['rects'][0];i=((IM.size[1]-1-ty-y)*IM.size[0]+tx+x)*4;return PIX[i:i+4]
def material(name,alpha=1,emissive=False):
 if name in MATS:return MATS[name]
 m=bpy.data.materials.new(name);m.use_nodes=True;bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.6;bs.inputs['Alpha'].default_value=alpha
 t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=IM;t.interpolation='Closest';m.node_tree.links.new(t.outputs['Color'],bs.inputs['Base Color'])
 if emissive:
  bs.inputs['Emission Strength'].default_value=1.2;m.node_tree.links.new(t.outputs['Color'],bs.inputs['Emission Color']);m.node_tree.links.new(t.outputs['Alpha'],bs.inputs['Alpha'])
 MATS[name]=m;return m
metal=material('Fire_Alarm_Housing_Original');glass=material('Fire_Alarm_Glass_Original',.88);effect=material('Fire_Alarm_Overlay_Original',emissive=True)
effect_unlit=material('Fire_Alarm_Overlay_Unlit_Original')
def trans(p):return ((p[0]-16)*S,-p[1]*S,(p[2]-7)*S)
def patch(st,box,u,v):
 x0,y0,x1,y1=box;return uv(st,x0+.025+(x1-x0-.05)*u,y0+.025+(y1-y0-.05)*v)
def bodyuv(st):
 def tex(p,c,a,sg):
  x,y,z=p
  if a==1 and sg==1:return uv(st,x,26-z)
  if a==1:return patch('fire_b0',(12,12,20,26),(x-12)/8,(14-z)/14)
  if a==2:return patch('fire0',(12,12,20,14),(x-12)/8,y/3)
  return patch('fire0',(12,13,14,25),y/3,(14-z)/14)
 return tex
def attach(ob,parent,pivot):
 for v in ob.data.vertices:v.co-=pivot
 ob.parent=parent;ob.location=(0,0,0)
def indicator(st,mask,parent,pivot):
 verts=[];faces=[];tex=[];em=[];lit=[]
 for y in range(32):
  for x in range(32):
   if pixel(st,x,y)[3]<.01:continue
   cr=[(x,y+1),(x+1,y+1),(x+1,y),(x,y)];n=len(verts)
   verts.extend([trans((xx,3.20,26-yy)) for xx,yy in cr]);faces.append(tuple(range(n,n+4)));tex.append([uv(st,xx,yy) for xx,yy in cr]);em.append([uv(mask,xx,yy) for xx,yy in cr]);lit.append(pixel(mask,x,y)[3]>.01)
 me=bpy.data.meshes.new('Status_Overlay');me.from_pydata(verts,[],faces);me.materials.append(effect);me.materials.append(effect_unlit)
 for face,is_lit in zip(me.polygons,lit):face.material_index=0 if is_lit else 1
 for name,arr in [('Original_Atlas_UV',tex),('Original_Emission_Mask',em)]:
  layer=me.uv_layers.new(name=name)
  for face,coords in zip(me.polygons,arr):
   for li,co in zip(face.loop_indices,coords):layer.data[li].uv=co
 ob=bpy.data.objects.new('Status_Overlay',me);scene.collection.objects.link(ob);parts.append(ob);attach(ob,parent,pivot);ob['source_state']=st;ob['emissive_state']=mask;return ob

variants=[('unpowered','fire0',None,None),*[(f'level_{i}','fire0',f'fire_{i}','fire_level_e') for i in range(4)],('alerting','fire0','fire_alerting','fire_alerting_e'),('detection_disabled','fire0','fire_disabled','fire_level_e'),('offstation','fire0','fire_offstation','fire_level_e'),('emagged','fire0','fire_emag','fire_emag_e'),('broken','firex',None,None),('frame','fire_b0',None,None),('circuit_unwired','fire_b1',None,None),('panel_open','fire_b2',None,None)]
for label,base,overlay,mask in variants:
 key='fire_alarm_'+label;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[]
 ASSETS[key]={'scene':scene,'parts':parts,'base_state':base,'overlays':[overlay] if overlay else [],'emissive_mask':mask}
 footprint={(x,z) for x in range(12,20) for z in range(14) if pixel(base,x,25-z)[3]>.99}
 cells={(x,y,z) for x,z in footprint for y in range(3)}
 cells-={(x,y,z) for x in range(13,19) for y in [1,2] for z in range(4,9)}
 # The service opening has a solid back, side walls, top and sill.
 solid('Continuous_Housing',cells,trans,bodyuv(base),metal)
 if label not in ['frame']:
  board=solid('Removable_Circuit_Board',{(x,0,z) for x in range(13,19) for z in range(5,8)},lambda p:trans((p[0],1+p[1]*.15,p[2])),lambda p,c,a,sg:patch('fire_b1',(13,18,19,21),(p[0]-13)/6,(8-p[2])/3),metal)
  board['source_state']='fire_b1'
 if label not in ['frame','circuit_unwired']:
  pivot=Vector(trans((13,2.8,9)));hinge=bpy.data.objects.new('Service_Cover_Hinge',None);scene.collection.objects.link(hinge);hinge.location=pivot;hinge['axis']='Z'
  cover=solid('Service_Cover',{(x,0,z) for x in range(13,19) for z in range(4,9)},lambda p:trans((p[0],2.8+p[1]*.2,p[2])),lambda p,c,a,sg:patch('fire0',(13,17,19,22),(p[0]-13)/6,(9-p[2])/5),metal);attach(cover,hinge,pivot)
  lens=solid('Indicator_Glass',{(x,0,z) for x in range(14,18) for z in range(4,8)},lambda p:trans((p[0],3.01+p[1]*.16,p[2])),lambda p,c,a,sg:patch('fire0',(14,18,18,22),(p[0]-14)/4,(8-p[2])/4),glass);attach(lens,hinge,pivot)
  if overlay:indicator(overlay,mask,hinge,pivot)
  if label=='panel_open':hinge.rotation_euler.z=math.radians(-105)
 handlecells={(x,0,z) for x in range(13,19) for z in range(4) if pixel(base,x,25-z)[3]>.99}
 def handleuv(p,c,a,sg):
  if a==1 and sg==1:return uv(base,p[0],26-p[2])
  return patch('fire0',(13,22,19,26),(p[0]-13)/6,(4-p[2])/4)
 ob=solid('Pull_Handle',handlecells,lambda p:trans((p[0],3+p[1],p[2])),handleuv,metal)
 pivot=Vector(trans((16,3,4)))
 for v in ob.data.vertices:v.co-=pivot
 ob.location=pivot;ob['hinge_axis']='X';ob['source_region']='lower orange handle, y22..26'
 if label=='panel_open':
  # Each wire is one closed stepped mesh, with orthogonal bridges at diagonal pixel contacts.
  for label,xrange in [('Left',range(10,12)),('Right',range(20,23))]:
   source={(x,25-yy) for x in xrange for yy in range(15,19) if pixel('fire_b2',x,yy)[3]>.99};connected=set(source)
   for x,z in source:
    for dx in [-1,1]:
     if (x+dx,z+1) in source:connected.add((x+dx,z))
   def wt(p,c,a,sg):
    x,z=min(source,key=lambda q:abs(q[0]-c[0])+abs(q[1]-c[2]));return uv('fire_b2',x+.5,25-z+.5)
   ob=solid('Service_Wire_'+label,{(x,0,z) for x,z in connected},lambda p:trans((p[0],2.5+p[1]*.2,p[2])),wt,metal,simplify=False);ob['source_state']='fire_b2'
cache={}
for a in ASSETS.values():
 a['overlay_states']=a['overlays']
 for ob in a['parts']:
  me=ob.data;sig=hashlib.sha256(repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[[tuple(u.uv) for u in layer.data] for layer in me.uv_layers],[m.name for m in me.materials])).encode()).hexdigest()
  if sig in cache:ob.data=cache[sig]
  else:cache[sig]=me
bpy.app.driver_namespace['fire_alarm_build']=globals();print('Built',len(ASSETS),'fire alarm states')
