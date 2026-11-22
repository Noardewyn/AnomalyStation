"""Original-atlas wall light switch with a separate rocker."""
from pathlib import Path
import bpy,bmesh,ast,json,math,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/light_switch';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={}
p=ROOT/'godot/assets/tg/icons/obj/machines/wallmounts.png';IM=bpy.data.images.load(str(p),check_existing=False);PIX=list(IM.pixels);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
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

metal=material('Light_Switch_Housing_Original')
def trans(p):return ((p[0]-16)*S,-p[1]*S,(p[2]-16)*S)
def rectuv(st,p,c,a,sg):
 x,y,z=p
 if a==1 and sg==1 and c[2]==19:return patch(st,(13,19,19,20),(x-13)/6,20-z)
 if a==1 and sg==1:return uv(st,x,32-z)
 # One-pixel dark end bands wrap onto both side faces and meet the dark roof.
 if a==0 and c[0] in (13,18) and c[2] in (12,19):return patch(st,(13,19,19,20),y/2,20-z if c[2]==19 else 13-z)
 if a==2 and c[2] in (12,19):return patch(st,(13,19,19,20),(x-13)/6,y/2)
 if a==1:return patch(st,(13,11,19,14),(x-13)/6,(20-z)/8)
 return patch(st,(13,13,15,20),y/2,(20-z)/8)
def overlay(st,mask,button,parent=None):
 verts=[];faces=[];uvs=[];mus=[];alphas=[]
 for y in range(13,20):
  for x in range(13,19):
   if (15<=x<17 and 14<=y<18)!=button or pixel(mask,x,y)[3]<.01:continue
   alphas.append(round(pixel(mask,x,y)[3],5));n=len(verts);cr=[(x,y+1),(x+1,y+1),(x+1,y),(x,y)]
   verts.extend([trans((xx,2.025 if button else 2.005,32-yy)) for xx,yy in cr]);faces.append(tuple(range(n,n+4)));uvs.append([uv(st,xx,yy) for xx,yy in cr]);mus.append([uv(mask,xx,yy) for xx,yy in cr])
 if not faces:return
 me=bpy.data.meshes.new('Emission_Overlay');me.from_pydata(verts,[],faces)
 for name,coords in [('Original_Atlas_UV',uvs),('Emission_Mask_UV',mus)]:
  layer=me.uv_layers.new(name=name)
  for f,cc in zip(me.polygons,coords):
   for li,co in zip(f.loop_indices,cc):layer.data[li].uv=co
 m=material('Light_Switch_Emission_Overlay_'+st);bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=IM;t.interpolation='Closest';u=m.node_tree.nodes.new('ShaderNodeUVMap');u.uv_map='Emission_Mask_UV';m.node_tree.links.new(u.outputs['UV'],t.inputs['Vector']);m.node_tree.links.new(t.outputs['Color'],bs.inputs['Emission Color']);bs.inputs['Emission Strength'].default_value=.4
 
 levels=sorted(set(alphas))
 for alpha in levels:
  mm=m.copy();bb=next(n for n in mm.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bb.inputs['Emission Strength'].default_value=alpha*(.4 if button else .12);me.materials.append(mm)
 for face,alpha in zip(me.polygons,alphas):face.material_index=levels.index(alpha)
 ob=bpy.data.objects.new('Rocker_Emission_Overlay' if button else 'Frame_Emission_Overlay',me);scene.collection.objects.link(ob);parts.append(ob)
 if parent:
  pivot=Vector(parent.location)
  for v in me.vertices:v.co-=pivot
  ob.parent=parent
for label in ['nopower','off','on']:
 st='light-'+label;key='light_switch_'+label;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':st,'overlay_states':[] if label=='nopower' else ['light-emissive-'+label]}
 cells={(x,y,z) for x in range(13,19) for y in range(2) for z in range(12,20)}-{(x,1,z) for x in range(15,17) for z in range(14,18)}
 solid('Continuous_Housing',cells,trans,lambda p,c,a,sg:rectuv('light-nopower',p,c,a,sg),metal,simplify=False)
 rocker=solid('Rocker',[(x,0,z) for x in range(15,17) for z in range(14,18)],lambda p:trans((p[0],1.45+.57*p[1],p[2])),lambda p,c,a,sg:uv(st,p[0],32-p[2]) if a==1 else patch('light-nopower',(15,14,17,18),(p[0]-15)/2,(18-p[2])/4),metal)
 pivot=bpy.data.objects.new('Rocker_Pivot',None);scene.collection.objects.link(pivot);pivot.location=trans((16,1.7,16));pivot['axis']='X'
 for v in rocker.data.vertices:v.co-=Vector(pivot.location)
 rocker.parent=pivot
 if label!='nopower':
  overlay(st,'light-emissive-'+label,False);overlay(st,'light-emissive-'+label,True,pivot)
 # Static poses only; both orientations fit entirely inside the lined recess.
 pivot.rotation_euler.x=math.radians(7 if label=='on' else -7 if label=='off' else 0)
cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;h=repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[[tuple(v.uv) for v in u.data] for u in me.uv_layers],[m.name for m in me.materials]))
  if h in cache:ob.data=cache[h]
  else:cache[h]=me
META={'width':32,'height':32,'states':[s for s in META['states'] if s['name'].startswith('light-')]}
bpy.app.driver_namespace['light_switch_build']=globals();print('Built',len(ASSETS),'light switch presets')

