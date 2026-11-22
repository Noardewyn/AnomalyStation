"""Wall igniter with original state textures and separate service cover."""
from pathlib import Path
import bpy,bmesh,ast,json
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/mounted_igniter';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={};shared=[]
p=ROOT/'godot/assets/tg/icons/obj/wallmounts.png';IM=bpy.data.images.load(str(p),check_existing=False);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
for f,names in [('build_transport_assets.py',['solid']),('build_cell_charger.py',['patch','material'])]:
 tree=ast.parse((ROOT/'tools/art'/f).read_text());exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'helpers','exec'))
PIX=list(IM.pixels)
def uv(st,x,y):return ((192+x)/IM.size[0],1-(192+y)/IM.size[1])
def trans(p):return ((p[0]-15)*S,-p[1]*S,(p[2]-5)*S)
cache={}
casing=material("Mounted_Igniter_Casing_Original")
variants=[('ready','migniter',0),('unpowered','migniter-p',0),('disabled','migniter-d',0)]+[('spark_'+str(i),'migniter-spark',i) for i in range(4)]
for label,st,frame in variants:
 key='mounted_igniter_'+label;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ovs=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':st,'overlay_states':ovs,'frame':frame}
 tx,ty,_,_=ST[st]['rects'][frame]
 def pixel(x,y):return PIX[((IM.size[1]-1-ty-y)*IM.size[0]+tx+x)*4:((IM.size[1]-1-ty-y)*IM.size[0]+tx+x)*4+4]
 mat=material('Mounted_Igniter_'+label+'_Original');nodes=mat.node_tree.nodes;links=mat.node_tree.links;t=next(n for n in nodes if n.type=='TEX_IMAGE');co=nodes.new('ShaderNodeTexCoord');mp=nodes.new('ShaderNodeMapping');mp.inputs['Location'].default_value=((tx-192)/IM.size[0],(192-ty)/IM.size[1],0);links.new(co.outputs['UV'],mp.inputs['Vector']);links.new(mp.outputs['Vector'],t.inputs['Vector'])
 # Only opaque source silhouette is physical. Translucent halo stays separate.
 silhouette={(x,19-y) for y in range(32) for x in range(32) if pixel(x,y)[3]>.9}
 # The cover sits above the wiring on the left side. Its open sprite is preserved.
 cover={(x,z) for x,z in silhouette if x<16 and (20-z)<(11 if label=='disabled' else 14)}
 for name,shape in [('Continuous_Igniter_Body',silhouette-cover),('Service_Cover',cover)]:
  if not shape:continue
  def tex(p,c,a,sg):
   if a==1 and sg==1:return uv(st,p[0],20-p[2])
   co=patch(st,(9,12,12,15),p[1]/3 if a==0 else (p[0]-8)/14,(10-p[2])/10 if a!=2 else p[1]/3)
   return (co[0]+32/IM.size[0],co[1])
  ob=solid(name,{(x,y,z) for x,z in shape for y in range(3)},trans,tex,mat)
  ob.data.materials.append(casing)
  for poly in ob.data.polygons:
   if poly.normal.y>-.99:poly.material_index=1
  pivot=Vector(trans((12,0,9))) if name=='Service_Cover' else Vector((0,0,0))
  for v in ob.data.vertices:v.co-=pivot
  ob.location=pivot
  if name=='Service_Cover':ob['hinge_axis']='local X';ob['pose']='source open pose' if label=='disabled' else 'closed';ob['animation']='none'
  signature=(name,tuple(sorted(shape)))
  if signature in cache:
   old=ob.data;ob.data=cache[signature];ob.material_slots[0].link='OBJECT';ob.material_slots[0].material=mat;bpy.data.meshes.remove(old)
  else:cache[signature]=ob.data
 verts=[];faces=[];coords=[]
 for y in range(32):
  for x in range(32):
   alpha=pixel(x,y)[3]
   if not 0<alpha<.9:continue
   corners=[(x,y+1),(x+1,y+1),(x+1,y),(x,y)];n=len(verts);verts.extend([trans((xx,3.015,20-yy)) for xx,yy in corners]);faces.append(tuple(range(n,n+4)));coords.append([uv(st,xx,yy) for xx,yy in corners])
 if faces:
  glow=mat.copy();glow.name='Mounted_Igniter_'+label+'_Effect_Overlay';bs=next(n for n in glow.node_tree.nodes if n.type=='BSDF_PRINCIPLED');t=next(n for n in glow.node_tree.nodes if n.type=='TEX_IMAGE');glow.node_tree.links.new(t.outputs['Alpha'],bs.inputs['Alpha']);glow.node_tree.links.new(t.outputs['Color'],bs.inputs['Emission Color']);bs.inputs['Emission Strength'].default_value=.4;glow.surface_render_method='BLENDED'
  me=bpy.data.meshes.new('Effect_Overlay');me.from_pydata(verts,[],faces);me.materials.append(glow);u=me.uv_layers.new()
  for poly,cc in zip(me.polygons,coords):
   for li,co in zip(poly.loop_indices,cc):u.data[li].uv=co
  ob=bpy.data.objects.new('Effect_Overlay',me);scene.collection.objects.link(ob);parts.append(ob);ovs.append(st+' translucent effect pixels')
 for ob in parts:ob['source_state']=st;ob['source_frame']=frame
bpy.app.driver_namespace['mounted_igniter_build']=globals()
