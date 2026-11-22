"""Original atlas wall clock with interchangeable digit overlays."""
from pathlib import Path
import bpy,bmesh,ast,json
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/digital_clock';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={}
p=ROOT/'godot/assets/tg/icons/obj/digital_clock.png';IM=bpy.data.images.load(str(p),check_existing=False);PIX=list(IM.pixels);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
for file,names in [('build_transport_assets.py',['solid']),('build_cell_charger.py',['uv','pixel','patch','material'])]:
 tree=ast.parse((ROOT/'tools/art'/file).read_text());exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'helpers','exec'))
metal=material('Digital_Clock_Original_Atlas');glow=material('Digital_Clock_Digit_Overlay',True);glass=material('Digital_Clock_Glass',alpha=.10)
for m in [glow,glass]:m.surface_render_method='BLENDED'
bs=next(n for n in glow.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Emission Strength'].default_value=1.2
bs=next(n for n in glass.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.22
base='digital_clock_base'
def trans(p):return ((p[0]-16.5)*S,-p[1]*S,(p[2]-6.5)*S)
def bodyuv(p,c,a,sg):
 x,y,z=p
 if a==1 and (sg==1 or c[1]==0):return uv(base,x,21-z)
 if a==2:return patch(base,(6,8,27,11) if sg==1 else (6,19,27,21),(x-5)/23,y/3)
 return patch(base,(5,11,7,20),y/3 if a==0 else (x-5)/23,1-z/13)
def glyph(st,label,shift):
 verts=[];faces=[];coords=[]
 for yy in range(32):
  for xx in range(32):
   if pixel(st,xx,yy)[3]<.01:continue
   cr=[(xx,yy),(xx+1,yy),(xx+1,yy+1),(xx,yy+1)];n=len(verts)
   verts.extend([trans((x,2.77,21-y)) for x,y in cr]);faces.append(tuple(range(n,n+4)));coords.append([uv(st,x,y) for x,y in cr])
 me=bpy.data.meshes.new(label);me.from_pydata(verts,[],faces);me.materials.append(glow);u=me.uv_layers.new(name='Original_Atlas_UV')
 for f,cc in zip(me.polygons,coords):
  for li,co in zip(f.loop_indices,cc):u.data[li].uv=co
 ob=bpy.data.objects.new(label,me);scene.collection.objects.link(ob);ob.location.x=shift*S;ob['source_state']=st;ob['pixel_w']=shift;parts.append(ob);ovs.append(st)
for display in ['off','1256','0347','8901']:
 key='digital_clock_'+display;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ovs=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':base,'overlay_states':ovs}
 mask={(x,20-y) for y in range(32) for x in range(32) if pixel(base,x,y)[3]>.99}
 cells={(x,y,z) for x,z in mask for y in range(3) if y==0 or not(7<=x<26 and 2<=z<9)}
 solid('Continuous_Stepped_Housing',cells,trans,bodyuv,metal)
 solid('Recessed_Display', {(x,0,z) for x in range(7,26) for z in range(2,9)},lambda p:trans((p[0],1+p[1]*1.7,p[2])),lambda p,c,a,sg:patch(base,(7,12,26,19),(p[0]-7)/19,(9-p[2])/7),metal)
 solid('Closed_Tinted_Glass', {(x,0,z) for x in range(7,26) for z in range(2,9)},lambda p:trans((p[0],2.82+p[1]*.12,p[2])),lambda p,c,a,sg:patch(base,(7,12,26,19),(p[0]-7)/19,(9-p[2])/7),glass)
 if display!='off':
  for digit,label,shift in zip(display,['Hours_Tens','Hours_Ones','Minutes_Tens','Minutes_Ones'],[-14,-10,-4,0]):glyph('+'+digit,label+'_Overlay',shift)
  glyph('+separator','Separator_Overlay',0)
cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;sig=repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[tuple(v.uv) for v in me.uv_layers.active.data],[m.name for m in me.materials]))
  if sig in cache:ob.data=cache[sig]
  else:cache[sig]=me
bpy.app.driver_namespace['digital_clock_build']=globals()
print('Built four digital clock presets; 89:01 is an emagged display example.')
