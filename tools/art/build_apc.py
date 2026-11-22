"""Pixel-boundary APC enclosure with original-atlas removable covers and cell."""
from pathlib import Path
import bpy,bmesh,ast,json,math,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/apc';OUT.mkdir(parents=True,exist_ok=True)
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
 if emission:m.node_tree.links.new(t.outputs['Color'],bs.inputs['Emission Color']);m.node_tree.links.new(t.outputs['Alpha'],bs.inputs['Alpha']);bs.inputs['Emission Strength'].default_value=1
 return m
metal=material('APC_Housing_Original');glass=material('APC_Display_Glass_Original',alpha=.94);glow=material('APC_Overlay_Original',True)
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
variants=[('closed_base','apc0',[],False,True),('charging','apc0',['apcox-1','apco3-1','apco0-3','apco1-3','apco2-3'],False,True),('charged','apc0',['apcox-1','apco3-2','apco0-3','apco1-3','apco2-3'],False,True),('not_charging','apc0',['apcox-1','apco3-0','apco0-3','apco1-3','apco2-3'],False,True),('unlocked','apc0',['apcox-0','apco3-2','apco0-3','apco1-3','apco2-3'],False,True),('channels_off','apc0',['apcox-1','apco3-0','apco0-0','apco1-0','apco2-0'],False,True),('channels_auto_off','apc0',['apcox-1','apco3-0','apco0-1','apco1-1','apco2-1'],False,True),('channels_manual_on','apc0',['apcox-1','apco3-2','apco0-2','apco1-2','apco2-2'],False,True),('breaker_off','apc0',['apcox-1','apco3-0'],False,True),('open_cell','apc2',[],True,True),('open_empty','apc1',[],True,False),('cover_removed_cell','apc2-nocover',[],True,True),('cover_removed_empty','apc1-nocover',[],True,False),('maintenance','apcmaint',[],True,False),('wiring_open','apcewires',[],False,True),('broken','apc-b',[],False,True),('broken_removed_cell','apc2-b-nocover',[],True,True),('broken_removed_empty','apc1-b-nocover',[],True,False),('hacked_flicker','apc0',['apcemag'],False,True)]
for label,base,ovs,opened,cell in variants:
 key='apc_'+label;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':base,'overlay_states':ovs}
 removed='nocover' in base;broken='broken' in label;maint=label=='maintenance';wireopen=opened or label in ['wiring_open','broken']
 # The tray has a back and four lined walls, never an open extrusion.
 cells={(x,y,z) for x in range(8,25) for y in range(4) for z in range(12,22)}-{(x,y,z) for x in range(10,24) for y in [1,2,3] for z in range(13,21)}
 if not removed:cells|={(x,y,22) for x in [8,9,23,24] for y in [2,3]}
 def casing(p,c,a,sg):
  if a==1 and sg==1:return patch('apc1-nocover',(8,10,25,20),(p[0]-8)/17,(22-p[2])/10)
  if a==1:return patch('apc1',(10,3,20,10),(p[0]-8)/17,(22-p[2])/10)
  if a==2:return patch('apc0',(9,9,24,11),(p[0]-8)/17,p[1]/4)
  return patch('apc0',(8,10,10,20),p[1]/4,(22-p[2])/10)
 solid('Lined_Enclosure',cells,trans,casing,metal)
 box('Interior_Backplate',10,24,13,21,1,.15,'apcmaint' if maint else ('apc1-b-nocover' if broken else 'apc1-nocover'),(10,11,24,19))
 if not maint:box('Removable_Control_Board',11,19,14,20,1.2,.25,'apc1-nocover',(11,12,19,18))
 if cell:box('Removable_Power_Cell',11,19,14,19,1.6,1.3,'apc2-nocover',(11,13,19,18))
 # Lower wiring chamber and its independently hinged service flap.
 if not removed:
  lower={(x,y,z) for x in range(9,24) for y in range(4) for z in range(9,12)}-{(x,y,z) for x in range(10,18) for y in [1,2,3] for z in [10,11]}
  def lowtex(p,c,a,sg):
   if a==1 and sg==1:return uv('apc0',p[0],32-p[2])
   return patch('apc0',(9,20,24,23),(p[0]-9)/15,(12-p[2])/3)
  solid('Lower_Control_Housing',lower,trans,lowtex,metal)
  box('Wire_Terminal_Block',10,18,10,12,1.1,.3,'apcmaint' if maint else 'apcewires',(10,20,18,22))
  if not broken:
   wh,wp=hinge('Wiring_Cover_Hinge',(10,4,9));ob=box('Wiring_Cover',10,18,9,12,4,.2,'apc0',(10,20,18,23));attach(ob,wh,wp)
   if wireopen:wh.rotation_euler.x=math.radians(110)
  else:
   tail={(x,0,31-y) for y in range(23,29) for x in range(10,18) if pixel('apc-b',x,y)[3]>.99}
   def tailtex(p,c,a,sg):
    if a==1:return uv('apc-b',p[0],32-p[2])
    return uv('apc-b',c[0]+.5,31-c[2]+.5)
   solid('Damaged_Wiring',tail,lambda p:trans((p[0],3.5+p[1]*.4,p[2])),tailtex,metal)
 if not removed:
  ch,cp=hinge('Main_Cover_Hinge',(8,4.05,23))
  cells={(x,0,z) for x in range(8,25) for z in range(12,23)}-{(x,0,z) for x in range(10,19) for z in range(14,21)}
  if broken:cells-={(24,0,z) for z in [15,16,17]}
  def covertex(p,c,a,sg):
   if a==1 and sg==1:return uv('apc-b' if broken else 'apc0',p[0],32-p[2])
   if a==1:return patch('apc1',(8,2,25,10),(p[0]-8)/17,(23-p[2])/11)
   return patch('apc0',(9,9,24,11),(p[0]-8)/17,(23-p[2])/11)
  ob=solid('Main_Cover',cells,lambda p:trans((p[0],4.05+p[1]*.35,p[2])),covertex,metal);attach(ob,ch,cp)
  def displaytex(p,c,a,sg):
   return patch('apc1' if a==1 and sg==-1 else ('apc-b' if broken else 'apc0'),(10,4,19,9) if a==1 and sg==-1 else (10,11,19,18),(p[0]-10)/9,(21-p[2])/7)
  ob=solid('Display_Back',{(x,0,z) for x in range(10,19) for z in range(14,21)},lambda p:trans((p[0],4.08+p[1]*.1,p[2])),displaytex,metal);attach(ob,ch,cp)
  ob=solid('Display_Glass',{(x,0,z) for x in range(10,19) for z in range(14,21)},lambda p:trans((p[0],4.2+p[1]*.12,p[2])),lambda p,c,a,sg:patch('apc-b' if broken else 'apc0',(10,11,19,18),(p[0]-10)/9,(21-p[2])/7),glass);attach(ob,ch,cp)
  if opened:ch.rotation_euler.x=math.radians(-165)
  for st in ovs:
   name='Lock' if st.startswith('apcox') else ('Charge' if st.startswith('apco3') else ('Hacked' if st=='apcemag' else ['Equipment','Lighting','Environment'][int(st[4])]))
   overlay(st,name+'_Overlay',4.43,None if name=='Lock' else ch,None if name=='Lock' else cp)
 # Deliberately keep effect animation frames as metadata, not fake geometry.
 for ob in parts:ob['source_atlas']='res://assets/tg/icons/obj/machines/wallmounts.png'
cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;data=repr(([(tuple(v.co)) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[tuple(v.uv) for v in me.uv_layers.active.data],[m.name for m in me.materials]));h=hashlib.sha256(data.encode()).hexdigest()
  if h in cache:old=ob.data;ob.data=cache[h];bpy.data.meshes.remove(old)
  else:cache[h]=me
META={'width':32,'height':32,'states':[s for s in META['states'] if s['name'].startswith('apc')]}
bpy.app.driver_namespace['apc_build']=globals();print('Built',len(ASSETS),'APC variants')
