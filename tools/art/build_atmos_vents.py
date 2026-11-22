"""Original-atlas floor vent pumps and scrubbers."""
from pathlib import Path
import bpy,bmesh,ast,json,math,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/atmos_vents';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={}
p=ROOT/'godot/assets/tg/icons/obj/machines/atmospherics/unary_devices.png';IM=bpy.data.images.load(str(p),check_existing=False);PIX=list(IM.pixels);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
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



metal=material('Atmos_Vent_Original_Atlas');glow=material('Atmos_Vent_Indicator_Original',True)
def trans(p):return ((p[0]-15.5)*S,(15.5-p[1])*S,p[2]*S)
def fixcorners(mask):
 mask=set(mask)
 for _ in range(10):
  add=set()
  for y in range(5,26):
   for x in range(5,26):
    a=(x,y);b=(x+1,y);c=(x,y+1);d=(x+1,y+1)
    if a in mask and d in mask and b not in mask and c not in mask:add.add(b)
    elif b in mask and c in mask and a not in mask and d not in mask:add.add(a)
  if not add:break
  mask|=add
 return mask
def tex(st,p,c,a,sg):
 x,y,z=p
 if a==2 and sg==1:return uv(st,x,y)
 return patch(st,(12,7,19,8) if st.startswith('scrub') else (7,7,24,8),(y-6)/19 if a==0 else (x-6)/19,z/3)
def tileoverlay(st,name,z):
 vs=[];fs=[];cs=[]
 for y in range(32):
  for x in range(32):
   if pixel(st,x,y)[3]<.01:continue
   cr=[(x,y),(x+1,y),(x+1,y+1),(x,y+1)];n=len(vs);vs.extend([trans((xx,yy,z)) for xx,yy in cr]);fs.append(tuple(range(n,n+4)));cs.append([uv(st,xx,yy) for xx,yy in cr])
 me=bpy.data.meshes.new(name);me.from_pydata(vs,[],fs);me.materials.append(glow);u=me.uv_layers.new(name='Original_Atlas_UV')
 for f,cc in zip(me.polygons,cs):
  for li,co in zip(f.loop_indices,cc):u.data[li].uv=co
 ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob);parts.append(ob)
for family,states in [('vent',['off','out','in','welded','overclocked','broken']),('scrub',['off','on','wide','purge','welded'])]:
 base=family+'_off';occupied={(x,y) for y in range(32) for x in range(32) if pixel(base,x,y)[3]>.99}
 inner={q for q in occupied if all((q[0]+dx,q[1]+dy) in occupied for dx in range(-2,3) for dy in range(-2,3))};rim=occupied-inner
 for state in states:
  st=family+'_'+state if state not in ['overclocked','broken'] else 'vent_out';key=('vent_pump_' if family=='vent' else 'vent_scrubber_')+state
  scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':st,'overlay_states':[state] if state in ['overclocked','broken'] else []}
  # Continuous bowl: closed bottom, connected side walls, open well above it.
  cells={(x,y,0) for x,y in occupied}|{(x,y,z) for x,y in rim for z in [1,2]}
  solid('Continuous_Recessed_Housing',cells,trans,lambda p,c,a,sg:patch(base,(15,10,16,11) if family=='scrub' else (9,8,10,9),p[0]-c[0],p[1]-c[1]) if a==2 and sg==1 and c[2]==0 else tex(base,p,c,a,sg),metal)
  # Original source bands become actual raised bars; dark slots remain recessed.
  if family=='vent':
   grate={(x,y) for x,y in inner if y in [9,11,13,15,17,19,21]}
   grate|={(x,y) for x,y in inner if x in [8,22]}
   if state=='welded':grate=inner
   solid('Welded_Grille' if state=='welded' else 'Removable_Grille',{(x,y,0) for x,y in grate},lambda p:trans((p[0],p[1],2.35+p[2]*.45)),lambda p,c,a,sg:tex(st,p,c,a,sg),metal)
   rotor={(x,y) for x in range(11,20) for y in range(11,20) if x in [14,15,16] or y in [14,15,16]}
   solid('Internal_Fan', {(x,y,0) for x,y in rotor},lambda p:trans((p[0],p[1],1.25+p[2]*.3)),lambda p,c,a,sg:tex(base,p,c,a,sg),metal)
  else:
   rotor=fixcorners({(x,y) for x,y in inner if max(pixel(base,x,y)[:3])>.15})
   if state=='welded':rotor=inner
   solid('Welded_Cover' if state=='welded' else 'Removable_Rotor',{(x,y,0) for x,y in rotor},lambda p:trans((p[0],p[1],2.35+p[2]*.45)),lambda p,c,a,sg:tex(st,p,c,a,sg),metal)
  # Indicators remain independent, with their own source-state UVs.
  for side in [6,23]:
   strip={(x,y,0) for x in range(side,side+2) for y in range(13,18) if (x,y) in occupied}
   solid('Mode_Indicator',strip,lambda p:trans((p[0],p[1],3.005+p[2]*.08)),lambda p,c,a,sg:uv(st,p[0],p[1]) if a==2 and sg==1 else patch(base,(12,7,19,8) if family=='scrub' else (7,7,24,8),p[0]-c[0],p[1]-c[1]),metal if state in ['off','welded'] else glow)
  capst=family+'_cap';cap={(x,y,0) for x in range(11,20) for y in range(24,27) if pixel(capst,x,y)[3]>.01}
  solid('Underfloor_Pipe_Cap',cap,lambda p:trans((p[0],p[1],.2+p[2]*.7)),lambda p,c,a,sg:uv(capst,p[0],p[1]) if a==2 and sg==1 and c[1]<26 else patch(capst,(12,24,19,25),(p[0]-11)/9,p[2]),metal,simplify=False)
  if state in ['broken','overclocked']:tileoverlay(state,'Fan_Status_Overlay',2.81)
  # Separate mechanical parts have centered pivots, ready for later interaction.
  for ob in parts:
   if any(n in ob.name for n in ['Removable','Internal_Fan']):
    pivot=Vector((0,0,2.35*S if 'Removable' in ob.name else 1.25*S))
    for v in ob.data.vertices:v.co-=pivot
    ob.location=pivot;ob['pivot_axis']='Z';ob['removal_axis']='Z'
cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;h=repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[[tuple(v.uv) for v in u.data] for u in me.uv_layers],[m.name for m in me.materials]))
  if h in cache:ob.data=cache[h]
  else:cache[h]=me
META={'width':32,'height':32,'states':[s for s in META['states'] if s['name'].startswith(('vent_','scrub_')) or s['name'] in ['broken','overclocked']]}
bpy.app.driver_namespace['atmos_vents_build']=globals();print('Built',len(ASSETS),'vent and scrubber presets')
