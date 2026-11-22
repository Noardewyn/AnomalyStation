"""Original-atlas grinder with stepped circular volumes and removable glass beaker."""
from pathlib import Path
import bpy,bmesh,ast,json,math
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/reagent_grinder';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={}
p=ROOT/'godot/assets/tg/icons/obj/machines/kitchen.png';IM=bpy.data.images.load(str(p),check_existing=False);PIX=list(IM.pixels);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
for file,names in [('build_transport_assets.py',['solid']),('build_cell_charger.py',['uv','pixel','patch','material'])]:
 tree=ast.parse((ROOT/'tools/art'/file).read_text());exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'helpers','exec'))
metal=material('Grinder_Original_Atlas');glass=material('Grinder_Beaker_Glass',alpha=.25);glass.surface_render_method='BLENDED';next(n for n in glass.node_tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Roughness'].default_value=.2
glow=material('Grinder_Ready_Overlay',True)
def trans(p):return ((p[0]-17)*S,(7-p[1])*S,p[2]*S)
def cross(cx,cy,r):
 return {(x,y) for x in range(cx-r,cx+r) for y in range(cy-r,cy+r) if (x+.5-cx)**2+(y+.5-cy)**2<=r*r}
for loaded,on,label in [(False,False,'empty_off'),(False,True,'empty_ready'),(True,False,'beaker_off'),(True,True,'beaker_ready')]:
 key='reagent_grinder_'+label;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ovs=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':'juicer','overlay_states':ovs}
 cells={(x,y,z) for z in range(8) for x,y in cross(17,7,3 if z==0 else 5 if z==1 else 6 if z==7 else 7)}
 cells|={(x,y,z) for x in range(12,16) for y in range(2,6) for z in range(7,20)}
 cells|={(x,y,z) for z in range(19,24) for x,y in cross(17,7,5 if z==19 else 6 if z==20 else 7) if z<22 or not(14<=x<20 and 4<=y<10)}
 cells|={(x,y,z) for x in range(17,21) for y in range(7,10) for z in range(17,20)}
 def tex(p,c,a,sg):
  x,y,z=p;u=(x-10)/14 if a!=0 else y/14
  if c[2]<8:
   return patch('juicer',(12,23,22,28),u,(8-z)/8 if a!=2 else y/14)
  if c[2]>=19:
   return patch('juicer',(12,6,22,12),u,(24-z)/5 if a!=2 else y/14)
  if c[0]<16:return patch('juicer',(12,14,16,22),(x-12)/4 if a!=0 else (y-2)/4,(20-z)/13)
  return patch('juicer',(17,14,21,17),(x-17)/4 if a!=0 else (y-7)/3,(20-z)/3)
 solid('Continuous_Base_Column_And_Hopper',cells,trans,tex,metal)
 cap={(x,y,z) for z in range(24,30) for x,y in cross(17,7,2 if z==29 else 3)}
 ob=solid('Separate_Hopper_Cap',cap,trans,lambda p,c,a,sg:patch('juicer',(14,2,20,5),(p[0]-14)/6 if a!=0 else (p[1]-4)/6,(30-p[2])/6 if a!=2 else (p[1]-4)/6),metal);pivot=Vector(trans((17,7,24)))
 for v in ob.data.vertices:v.co-=pivot
 ob.location=pivot;ob['note']='Separate for editing; source has no animated lid state.'
 if loaded:
  # Closed vessel wall surface, open mouth, integral bottom; all steps land on pixel edges.
  outer=cross(19,10,3);inner=cross(19,10,2)
  cup={(x,y,z) for x,y in outer for z in range(8,16) if z==8 or (x,y) not in inner}
  ob=solid('Removable_Open_Beaker',cup,trans,lambda p,c,a,sg:patch('juicer-beaker',(16,17,22,24),(p[0]-16)/6 if a!=0 else (p[1]-7)/6,(16-p[2])/8 if a!=2 else (p[1]-7)/6),glass)
  pivot=Vector(trans((19,10,8)))
  for v in ob.data.vertices:v.co-=pivot
  ob.location=pivot;ob['removal_axis']='Z';ovs.append('juicer-beaker')
 if on:
  # One source indicator pixel, not an operating animation.
  verts=[trans(p) for p in [(18,10.015,17),(19,10.015,17),(19,10.015,18),(18,10.015,18)]];me=bpy.data.meshes.new('Ready_Overlay');me.from_pydata(verts,[],[(0,1,2,3)]);me.materials.append(glow);u=me.uv_layers.new(name='Original_Atlas_UV')
  for li,co in zip(me.polygons[0].loop_indices,[(18,16),(19,16),(19,15),(18,15)]):u.data[li].uv=uv('juicer-on',*co)
  ob=bpy.data.objects.new('Ready_Overlay',me);scene.collection.objects.link(ob);parts.append(ob);ovs.append('juicer-on')
cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;sig=repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[tuple(v.uv) for v in me.uv_layers.active.data],[m.name for m in me.materials]))
  if sig in cache:ob.data=cache[sig]
  else:cache[sig]=me
bpy.app.driver_namespace['reagent_grinder_build']=globals();print('Built four grinder combinations')
