"""Original-atlas cell charger with removable standard power cells."""
from pathlib import Path
import bpy,bmesh,ast,json,math,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/cell_charger';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={}
p=ROOT/'godot/assets/tg/icons/obj/machines/cell_charger.png';IM=bpy.data.images.load(str(p),check_existing=False);PIX=list(IM.pixels);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
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


metal=material('Cell_Charger_Original_Atlas');glow=material('Cell_Charger_Indicator_Overlay',True)
bs=next(n for n in glow.node_tree.nodes if n.type=='BSDF_PRINCIPLED');t=next(n for n in glow.node_tree.nodes if n.type=='TEX_IMAGE');glow.node_tree.links.new(t.outputs['Alpha'],bs.inputs['Alpha']);glow.surface_render_method='BLENDED'
def trans(p):return ((p[0]-14.5)*S,(17-p[1])*S,p[2]*S)
def patchbase(p,c,a,sg):
 x,y,z=p
 if a==2 and sg==1:return patch('ccharger',(5,17,23,22),(x-5)/19,(y-12)/10)
 if a==1 and sg==1:return patch('ccharger',(9,20,23,23),(x-9)/14,1-z/3)
 return patch('ccharger',(9,16,22,18),(y-12)/10 if a==0 else (x-5)/19,z/3)
variants=[('empty',None,None,True)]+[('level_'+str(i),'cell',i,True) for i in range(5)]+[('off','cell',None,False)]+[(s,s,4,True) for s in ['hcell','scell','hpcell','icell','bscell']]
for mode,cell,level,on in variants:
 key='cell_charger_'+mode;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ovs=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':'ccharger','overlay_states':ovs}
 # Continuous L-shaped dock; the tall left column becomes a real vertical indicator tower.
 cells={(x,y,z) for x in range(5,24) for y in range(12,22) for z in range(3)}|{(x,y,z) for x in range(5,9) for y in range(13,19) for z in range(3,13)}
 def bodyuv(p,c,a,sg):
  x,y,z=p
  if c[0]<9 and c[2]>=3:
   if a==1:return patch('ccharger',(5,10,9,22),(x-5)/4,(13-z)/12)
   return patch('ccharger',(5,10,9,22),(y-13)/6,(13-z)/12 if a==0 else (x-5)/4)
  return patchbase(p,c,a,sg)
 solid('Continuous_Charger_Dock',cells,trans,bodyuv,metal)
 if cell:
  # Reusable standard cell family; APC's internal cell is a different depiction and atlas.
  cs={(x,y,z) for x in range(10,22) for y in range(12,19) for z in range(3,10)}
  def cuv(p,c,a,sg):
   x,y,z=p
   if a==1 and sg==1:return patch(cell,(10,13,22,21),(x-10)/12,(10-z)/7)
   if a==2 and sg==1:return patch(cell,(10,11,21,14),(x-10)/12,(y-12)/7)
   if a==0:return patch(cell,(20,13,22,21),(y-12)/7,(10-z)/7)
   return patch(cell,(10,15,22,21),(x-10)/12,(y-12)/7 if a==2 else (10-z)/7)
  ob=solid('Removable_Power_Cell',cs,trans,cuv,metal);pivot=Vector(trans((16,15.5,3)))
  for v in ob.data.vertices:v.co-=pivot
  ob.location=pivot;ob['source_state']=cell;ob['removal_axis']='Z'
  # Terminal blocks use the original red/blue top contacts, not coloured primitives.
  for x in [12,15]:
   solid('Cell_Terminal',{(xx,yy,0) for xx in range(x,x+2) for yy in range(12,14)},lambda p:trans((p[0],p[1],10+p[2]*.4)),lambda p,c,a,sg:patch(cell,(x,11,x+2,13),(p[0]-x)/2,(p[1]-12)/2),metal)
  # Stepped insulated connector arms follow the original standard lead pixels.
  st='ccharger-standard-on';ovs.append(st)
  for label,route,source_pixel in [('Blue',[(8,11),(9,11),(10,11),(11,11),(12,11),(12,12),(12,13)],(11,10)),('Red',[(8,9),(9,9),(10,9),(11,9),(12,9),(13,9),(14,9),(15,9),(15,10),(15,11),(15,12)],(14,10))]:
   solid(label+'_Connector_Lead',{(x,y,0) for x,y in route},lambda p:trans((p[0],12+(p[1]-10)*.5,10.5+p[2]*.3)),lambda p,c,a,sg,q=source_pixel:patch(st,(q[0],q[1],q[0]+1,q[1]+1),p[0]-c[0],p[1]-c[1]),metal)
 def overlay(st,name,where):
  verts=[];faces=[];coords=[]
  for y in range(32):
   for x in range(32):
    if pixel(st,x,y)[3]<.01:continue
    cc=[(x,y),(x+1,y),(x+1,y+1),(x,y+1)];n=len(verts)
    if where=='tower':verts.extend([trans((xx,19.01,22-yy)) for xx,yy in cc])
    else:verts.extend([trans((10+(xx-10),19.01,24-yy)) for xx,yy in cc])
    faces.append(tuple(range(n,n+4)));coords.append([uv(st,xx,yy) for xx,yy in cc])
  me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.materials.append(glow);u=me.uv_layers.new(name='Original_Atlas_UV')
  for f,cc in zip(me.polygons,coords):
   for li,co in zip(f.loop_indices,cc):u.data[li].uv=co
  ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob);parts.append(ob);ovs.append(st)
 if cell:
  if level is not None:overlay('ccharger-o'+str(level),'Dock_Status_Overlay','tower')
  if level is None or level>0:overlay('cell-standard-o'+('2' if level==4 else '1'),'Cell_Status_Overlay','cell')
# Contacts and the battery indicator follow the removable cell pivot.
for a in ASSETS.values():
 battery=next((o for o in a['parts'] if o.name.startswith('Removable_Power_Cell')),None)
 if battery:
  for child in a['parts']:
   if child.name.startswith(('Cell_Terminal','Cell_Status_Overlay')):
    child.parent=battery;child.location-=battery.location
cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;h=repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[[tuple(v.uv) for v in u.data] for u in me.uv_layers],[m.name for m in me.materials]))
  if h in cache:ob.data=cache[h]
  else:cache[h]=me
bpy.app.driver_namespace['cell_charger_build']=globals();print('Built',len(ASSETS),'cell charger presets')
