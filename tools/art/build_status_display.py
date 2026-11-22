"""Pixel-boundary APC enclosure with original-atlas removable covers and cell."""
from pathlib import Path
import bpy,bmesh,ast,json,math,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/status_display';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={}
p=ROOT/'godot/assets/tg/icons/obj/machines/status_display.png';IM=bpy.data.images.load(str(p),check_existing=False);PIX=list(IM.pixels);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
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
 if emission:m.node_tree.links.new(t.outputs['Color'],bs.inputs['Emission Color']);m.node_tree.links.new(t.outputs['Alpha'],bs.inputs['Alpha']);bs.inputs['Emission Strength'].default_value=1
 return m
metal=material('Status_Display_Housing_Original');glass=material('Status_Display_Glass_Original',alpha=.25);glow=material('Status_Display_Overlay_Original',True)
def trans(p):return ((p[0]-16.5)*S,-p[1]*S,(p[2]-16)*S)
def attach(ob,parent,pivot):
 for v in ob.data.vertices:v.co-=pivot
 ob.parent=parent;ob.location=(0,0,0)
def hinge(name,xyz):
 ob=bpy.data.objects.new(name,None);scene.collection.objects.link(ob);ob.location=trans(xyz);ob['axis']='X';return ob,Vector(ob.location)
def box(name,x0,x1,z0,z1,depth,thickness,st,rect):
 return solid(name,{(x,0,z) for x in range(x0,x1) for z in range(z0,z1)},lambda p:trans((p[0],depth+p[1]*thickness,p[2])),lambda p,c,a,sg:patch(st,rect,(p[0]-x0)/(x1-x0),(z1-p[2])/(z1-z0)),metal)
def overlay(st,name,depth,parent=None,pivot=None):
 verts=[];faces=[];coords=[]
 for y in range(32):
  for x in range(32):
   if pixel(st,x,y)[3]<.01:continue
   cr=[(x,y+1),(x+1,y+1),(x+1,y),(x,y)];n=len(verts);verts.extend([trans((xx,4.015 if y>=20 else depth,32-yy)) for xx,yy in cr]);faces.append(tuple(range(n,n+4)));coords.append([uv(st,xx,yy) for xx,yy in cr])
 me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.materials.append(glow);layer=me.uv_layers.new(name='Original_Atlas_UV')
 for f,tc in zip(me.polygons,coords):
  for li,co in zip(f.loop_indices,tc):layer.data[li].uv=co
 bm=bmesh.new();bm.from_mesh(me);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0000001);bmesh.ops.dissolve_limit(bm,angle_limit=.001,verts=list(bm.verts),edges=list(bm.edges),delimit={'UV'});bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
 ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob);parts.append(ob);ob['source_state']=st
 if parent:attach(ob,parent,pivot)
 return ob

unlit=material('Status_Display_Overlay_Unlit');bs=next(n for n in unlit.node_tree.nodes if n.type=='BSDF_PRINCIPLED');t=next(n for n in unlit.node_tree.nodes if n.type=='TEX_IMAGE');unlit.node_tree.links.new(t.outputs['Alpha'],bs.inputs['Alpha'])
def layer(st,name,mat):
 verts=[];faces=[];coords=[]
 for y in range(32):
  for x in range(32):
   if pixel(st,x,y)[3]<.01:continue
   cr=[(x,y+1),(x+1,y+1),(x+1,y),(x,y)];n=len(verts);verts.extend([trans((xx,4.015,32-yy)) for xx,yy in cr]);faces.append(tuple(range(n,n+4)));coords.append([uv(st,xx,yy) for xx,yy in cr])
 me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.materials.append(mat);u=me.uv_layers.new(name='Original_Atlas_UV')
 for f,co in zip(me.polygons,coords):
  for li,v in zip(f.loop_indices,co):u.data[li].uv=v
 bm=bmesh.new();bm.from_mesh(me);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0000001);bmesh.ops.dissolve_limit(bm,angle_limit=.001,verts=list(bm.verts),edges=list(bm.edges),delimit={'UV'});bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
 ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob);parts.append(ob);ob['source_state']=st;ob['emissive_mask_state']='outline' if mat==glow else ''
 return ob
# One shell for standard alert pictures and the original AI emote set.
alerts=['default','greenalert','bluealert','redalert','deltaalert','biohazard','lockdown','radiation']
ais=[n for n in ST if n.startswith('ai_') and not n.startswith('ai_status_')]
variants=[('blank',None),('text_ready',None)]+[(n,n) for n in alerts+ais]
for label,st in variants:
 key='status_display_'+label;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':'frame','overlay_states':[st] if st else []}
 silhouette={(x,31-y) for y in range(32) for x in range(32) if pixel('frame',x,y)[3]>.99}
 screen={(x,31-y) for y in range(32) for x in range(32) if pixel('outline',x,y)[3]>.99}
 cells={(x,y,z) for x,z in silhouette for y in range(4)}-{(x,3,z) for x,z in screen}
 def shelltex(p,c,a,sg):
  if a==1 and sg==1:return uv('frame',p[0],32-p[2])
  if a==1:return patch('frame',(2,3,30,7),p[0]/32,(29-p[2])/24)
  if a==2:return patch('frame',(2,4,30,7),p[0]/32,p[1]/4)
  return patch('frame',(0,7,3,25),p[1]/4,(29-p[2])/24)
 solid('Continuous_Frame',cells,trans,shelltex,metal)
 solid('Recessed_Glass',{(x,0,z) for x,z in screen},lambda p:trans((p[0],3.85+p[1]*.13,p[2])),lambda p,c,a,sg:uv('frame',p[0],32-p[2]),glass)
 if st:layer(st,'Picture_Overlay',unlit if st=='ai_off' else glow)
 if label=='text_ready':
  # Editable blank surface slots; runtime maptext is deliberately not baked.
  for name,y0,y1,source_y in [('Text_Line_1',10,16,-4),('Text_Line_2',17,23,-11)]:
   cr=[(5,y1),(27,y1),(27,y0),(5,y0)];me=bpy.data.meshes.new(name);me.from_pydata([trans((x,4.015,32-y)) for x,y in cr],[],[(0,1,2),(0,2,3)]);me.materials.append(metal);u=me.uv_layers.new(name='Original_Atlas_UV');local=me.uv_layers.new(name='Text_Surface_UV')
   for f in me.polygons:
    for li in f.loop_indices:
     x,y=cr[me.loops[li].vertex_index];u.data[li].uv=uv('frame',x,y);local.data[li].uv=((x-5)/22,(y1-y)/(y1-y0))
   ob=bpy.data.objects.new(name+'_Overlay',me);scene.collection.objects.link(ob);parts.append(ob);ob['source_maptext_y']=source_y;ob['source_max_static_width_px']=22;ob['purpose']='Blank surface for a separately rendered dynamic text line'
cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;h=hashlib.sha256(repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[tuple(v.uv) for v in me.uv_layers.active.data],[m.name for m in me.materials])).encode()).hexdigest()
  if h in cache:
   old=ob.data;ob.data=cache[h]
   if old!=ob.data and old.users==0:bpy.data.meshes.remove(old)
  else:cache[h]=me
bpy.app.driver_namespace['status_display_build']=globals();print('Built',len(ASSETS),'status display presets')
