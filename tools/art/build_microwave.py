"""Original-atlas standard microwave with cavity and hinged glass door."""
from pathlib import Path
import bpy,bmesh,ast,json,math
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/microwave';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={}
p=ROOT/'godot/assets/tg/icons/obj/machines/microwave.png';IM=bpy.data.images.load(str(p),check_existing=False);PIX=list(IM.pixels);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
for file,names in [('build_transport_assets.py',['solid']),('build_cell_charger.py',['uv','pixel','patch','material'])]:
 tree=ast.parse((ROOT/'tools/art'/file).read_text());exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'helpers','exec'))
metal=material('Microwave_Original_Atlas');glass=material('Microwave_Door_Glass',alpha=.62);glass.surface_render_method='BLENDED'
next(n for n in glass.node_tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Roughness'].default_value=.25
def trans(p):return ((p[0]-16.5)*S,(9-p[1])*S,p[2]*S)
variants=[('idle','mw','back_off','door_off',False),('open','mwo','back_open','door_off',True),('cooking','mw1','back_on','door_on',False),('charging','mw1','back_on','door_charge',False),('dirty','mwbloody','back_off','door_off',False),('broken','mwb','back_off','door_off',True),('service','mw-o','back_off','door_off',False),('dirty_open','mwbloodyo','back_open','door_off',True),('dirty_operating','mwbloody1','back_on','door_on',False)]
for mode,st,back,door,isopen in variants:
 key='microwave_'+mode;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ovs=[back,door,'door_handle'];ASSETS[key]={'scene':scene,'parts':parts,'base_state':st,'overlay_states':ovs}
 # An integral cabinet with rear, bottom, roof and side walls around a real chamber.
 cells={(x,y,z) for x in range(4,29) for y in range(18) for z in range(16) if y==0 or z<2 or z>=15 or x<6 or x>=21}
 if mode=='service':cells-={(x,y,z) for x in range(22,28) for y in range(16,18) for z in range(2,14)}
 def bodyuv(p,c,a,sg):
  x,y,z=p
  if a==2 and sg==1 and c[2]==15:return patch(st,(4,4,29,12),(x-4)/25,y/18)
  if a==1 and sg==1 and c[1]==17:
   if pixel(st,c[0],27-c[2])[3]>.99:return uv(st,x,28-z)
  if a==1 and sg==1 and c[1]==0 and 6<=c[0]<21 and 2<=c[2]<15:return patch('mw',(5,5,27,10),(x-6)/15,(15-z)/13)
  if a==1 and sg==-1:return patch(st,(4,4,29,12),(x-4)/25,(16-z)/16)
  if a==0:return patch('mw',(4,4,29,12),y/18,(16-z)/16)
  return patch('mw',(27,12,29,27),y/18 if a==0 else (x-4)/25,(16-z)/16 if a!=2 else y/18)
 solid('Continuous_Cabinet_With_Lined_Chamber',cells,trans,bodyuv,metal)
 solid('Chamber_Back_Layer',{(x,0,z) for x in range(6,21) for z in range(2,15)},lambda p:trans((p[0],1.01+p[1]*.06,p[2])),lambda p,c,a,sg:patch(back,(7,13,20,25),(p[0]-6)/15,(15-p[2])/13),metal)
 hinge=bpy.data.objects.new('Door_Hinge',None);scene.collection.objects.link(hinge);hinge.location=trans((6,18,2));hinge['axis']='Z';hinge['open_angle_degrees']=-105
 def parentdoor(ob):
  ob.parent=hinge;ob.location=-hinge.location
  return ob
 frame={(x,y,z) for x in range(6,21) for y in [18] for z in range(2,16) if x<7 or x>=20 or z<3 or z>=15}
 def duv(p,c,a,sg):
  x,y,z=p
  if a==1 and sg==1 and pixel('mw_complete',c[0],27-c[2])[3]>.99:return uv('mw_complete',x,28-z)
  return patch('mw_complete',(6,12,7,26),(y-18),1-(z-2)/14)
 parentdoor(solid('Closed_Door_Frame',frame,trans,duv,metal))
 pane={(x,0,z) for x in range(7,20) for z in range(3,15) if mode!='broken' or x<9 or x>=18 or z<5}
 gst=('mwbloody' if mode=='dirty_open' else st) if mode.startswith('dirty') or mode=='broken' else door
 box=(7,13,20,25)
 parentdoor(solid('Closed_Tinted_Door_Glass',pane,lambda p:trans((p[0],18.35+p[1]*.14,p[2])),lambda p,c,a,sg:patch(gst,box,(p[0]-7)/13,(15-p[2])/12),glass))
 handle={(x,y,z) for x in [19] for y in [19,20] for z in range(3,9)}|{(18,20,8)}
 parentdoor(solid('Door_Handle',handle,trans,lambda p,c,a,sg:patch('door_handle',(19,20,20,25),p[1]-c[1],(9-p[2])/6),metal))
 if isopen:hinge.rotation_euler.z=math.radians(-105)
 if mode=='service':
  solid('Service_Wiring_Board',{(x,0,z) for x in range(22,28) for z in range(2,14)},lambda p:trans((p[0],16.1+p[1]*.1,p[2])),lambda p,c,a,sg:patch('mw-o',(22,14,27,26),(p[0]-22)/6,(14-p[2])/12),metal)
  h=bpy.data.objects.new('Service_Cover_Hinge',None);scene.collection.objects.link(h);h.location=trans((28,18,2))
  ob=solid('Service_Cover',{(x,0,z) for x in range(22,28) for z in range(2,14)},lambda p:trans((p[0],18+p[1]*.4,p[2])),lambda p,c,a,sg:patch('mw',(22,14,27,26),(p[0]-22)/6,(14-p[2])/12),metal);ob.parent=h;ob.location=-h.location;h.rotation_euler.z=math.radians(105)
# Engineering chassis: distinct wide door and lower battery compartment.
for mode,raw,rawback,rawdoor,isopen in variants+[(f'cell_{n}','mw','back_off','door_off',False) for n in [0,25,75]]:
 st='engi_'+raw;back='engi_'+rawback;door='engi_'+rawdoor
 key='microwave_engineering_'+mode;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ovs=[back,door,'engi_door_handle'];ASSETS[key]={'scene':scene,'parts':parts,'base_state':st,'overlay_states':ovs}
 cells={(x,y,z) for x in range(4,28) for y in range(18) for z in range(17) if y==0 or z<7 or z>=16 or x<5 or x>=27}
 if mode=='service':cells-={(x,y,z) for x in range(6,22) for y in range(16,18) for z in range(2,6)}
 def euv(p,c,a,sg):
  x,y,z=p
  if a==1 and sg==1 and c[1]==17 and pixel(st,c[0],27-c[2])[3]>.99:return uv(st,x,28-z)
  if a==2 and sg==1 and c[2]==16:return patch(st,(4,4,28,11),(x-4)/24,y/18)
  return patch('engi_mw',(4,21,28,27),y/18 if a==0 else (x-4)/24,(17-z)/17 if a!=2 else y/18)
 solid('Engineering_Continuous_Cabinet',cells,trans,euv,metal)
 solid('Engineering_Chamber_Back',{(x,0,z) for x in range(5,27) for z in range(7,16)},lambda p:trans((p[0],1.01+p[1]*.06,p[2])),lambda p,c,a,sg:patch(back,(6,12,26,20) if rawback=='back_open' else (7,13,22,19),(p[0]-5)/22,(16-p[2])/9),metal)
 hinge=bpy.data.objects.new('Engineering_Door_Hinge',None);scene.collection.objects.link(hinge);hinge.location=trans((16,18,17));hinge['axis']='X';hinge['open_angle_degrees']=-110
 frame={(x,0,z) for x in range(4,28) for z in range(7,17) if x<7 or x>=22 or z<9 or z>=16}
 def ef(p,c,a,sg):
  return patch('engi_mw_complete',(4,11,28,21),(p[0]-4)/24,(17-p[2])/10) if a==1 else patch('engi_mw_complete',(4,11,6,21),p[1],(17-p[2])/10)
 parentdoor(solid('Engineering_Door_Frame',frame,lambda p:trans((p[0],18+p[1]*.6,p[2])),ef,metal))
 pane={(x,0,z) for x in range(7,22) for z in range(9,16) if mode!='broken' or x<9 or x>=20 or z<11}
 gst='engi_mwbloody' if mode=='dirty_open' else st if mode.startswith('dirty') or mode=='broken' else door
 parentdoor(solid('Engineering_Door_Glass',pane,lambda p:trans((p[0],18.25+p[1]*.14,p[2])),lambda p,c,a,sg:patch(gst,(7,12,22,19),(p[0]-7)/15,(16-p[2])/7),glass))
 parentdoor(solid('Engineering_Door_Handle',{(x,0,z) for x in range(24,26) for z in range(10,15)},lambda p:trans((p[0],18.6+p[1],p[2])),lambda p,c,a,sg:patch('engi_door_handle',(24,13,26,18),(p[0]-24)/2,(15-p[2])/5),metal))
 if isopen:hinge.rotation_euler.x=math.radians(-110)
 if mode=='service':
  solid('Engineering_Wiring_Board',{(x,0,z) for x in range(6,22) for z in range(2,6)},lambda p:trans((p[0],16.1+p[1]*.1,p[2])),lambda p,c,a,sg:patch(st,(6,22,22,26),(p[0]-6)/16,(6-p[2])/4),metal)
  h=bpy.data.objects.new('Lower_Service_Cover_Hinge',None);scene.collection.objects.link(h);h.location=trans((14,18,2))
  ob=solid('Engineering_Service_Cover',{(x,0,z) for x in range(6,22) for z in range(2,6)},lambda p:trans((p[0],18+p[1]*.4,p[2])),lambda p,c,a,sg:patch('engi_mw',(6,22,22,26),(p[0]-6)/16,(6-p[2])/4),metal);ob.parent=h;ob.location=-h.location;h.rotation_euler.x=math.radians(100)
# Original emissive masks and cell overlays remain independent sheets.
glow=material('Microwave_Emission_Overlay',True)
bs=next(n for n in glow.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Emission Strength'].default_value=1.0
def effect(a,st,label):
 verts=[];faces=[];coords=[]
 for y in range(32):
  for x in range(32):
   if pixel(st,x,y)[3]<.01:continue
   cr=[(x,y),(x+1,y),(x+1,y+1),(x,y+1)];n=len(verts)
   engineering=a['base_state'].startswith('engi_');top=11 if engineering else 12;height=17 if engineering else 16
   if y<top:verts.extend([trans((xx,(yy-4)*18/(top-4),height+.015)) for xx,yy in cr])
   else:verts.extend([trans((xx,18.015,28-yy)) for xx,yy in cr])
   faces.append(tuple(range(n,n+4)));coords.append([uv(st,xx,yy) for xx,yy in cr])
 me=bpy.data.meshes.new(label);me.from_pydata(verts,[],faces);me.materials.append(glow);u=me.uv_layers.new(name='Original_Atlas_UV')
 for f,cc in zip(me.polygons,coords):
  for li,co in zip(f.loop_indices,cc):u.data[li].uv=co
 ob=bpy.data.objects.new(label,me);a['scene'].collection.objects.link(ob);ob['source_state']=st;a['parts'].append(ob);a['overlay_states'].append(st)
for key,a in ASSETS.items():
 st=a['base_state'];engineering=key.startswith('microwave_engineering_');mode=key.replace('microwave_engineering_' if engineering else 'microwave_','')
 if mode in ['service','broken'] or mode.startswith('dirty'):continue
 effect(a,'emissive_'+st,'Emission_Overlay')
 if engineering:effect(a,'engi_cell_'+(mode.split('_')[1] if mode.startswith('cell_') else '100'),'Battery_Level_Overlay')

cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;sig=repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[tuple(v.uv) for v in me.uv_layers.active.data],[m.name for m in me.materials]))
  if sig in cache:ob.data=cache[sig]
  else:cache[sig]=me
bpy.app.driver_namespace['microwave_build']=globals();print('Built',len(ASSETS),'microwave presets')
