"""Wall emergency tool cabinets with removable tools and hinged original-atlas glass."""
from pathlib import Path
import bpy,bmesh,ast,json,math,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/structures/wall_volume_finish';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={};shared=[]
for f,names in [('build_transport_assets.py',['solid']),('build_cell_charger.py',['patch','material'])]:
 tree=ast.parse((ROOT/'tools/art'/f).read_text());exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'helpers','exec'))
# Duplicate vertices at point-only contacts instead of welding four surface sheets.
# This preserves every original pixel edge while keeping each surface manifold.
source=(ROOT/'tools/art/build_transport_assets.py').read_text();tree=ast.parse(source)
fn=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='solid')
code=ast.get_source_segment(source,fn).replace('def solid(', 'def corner_safe_solid(')
a=code.index(' def vi(p):');b=code.index(' for c in sorted(cells):',a)
code=code[:a]+""" def vi(p,c):
  local={(x,y,z) for x in [p[0]-1,p[0]] for y in [p[1]-1,p[1]] for z in [p[2]-1,p[2]] if (x,y,z) in cells}
  component={c};pending=[c]
  while pending:
   q=pending.pop()
   for axis in range(3):
    for sign in [-1,1]:
     n=list(q);n[axis]+=sign;n=tuple(n)
     if n in local and n not in component:component.add(n);pending.append(n)
  key=(p,min(component))
  if key not in lut:lut[key]=len(verts);verts.append(transform(p))
  return lut[key]
"""+code[b:]
code=code.replace('vi(p) for p in corners','vi(p,c) for p in corners');exec(compile(code,'corner_safe_boundary','exec'))


BATCH={};cache={}
def atlas(rel):
 global IM,META,ST,PIX
 p=ROOT/('godot/assets/tg/icons/obj/'+rel+'.png');IM=bpy.data.images.load(str(p),check_existing=False);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']};PIX=list(IM.pixels)
def uv(st,x,y):
 tx,ty,_,_=ST[st]['rects'][0];return ((tx+x)/IM.size[0],1-(ty+y)/IM.size[1])
def alpha(st,x,y):
 tx,ty,_,_=ST[st]['rects'][0];return PIX[((IM.size[1]-1-ty-y)*IM.size[0]+tx+x)*4+3]
def trans(p):return ((p[0]-16)*S,-p[1]*S,(p[2]-8)*S)
def begin(key,st):
 global scene,parts
 scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];ASSETS[key]={'scene':scene,'parts':parts,'base_state':st,'overlay_states':[],'frame':0}
def cells(x0,x1,y0,y1,z0,z1):return {(x,y,z) for x in range(x0,x1) for y in range(y0,y1) for z in range(z0,z1)}
def shifted(st,base,name):
 m=material(name);n=m.node_tree.nodes;l=m.node_tree.links;t=next(n for n in n if n.type=='TEX_IMAGE');co=n.new('ShaderNodeTexCoord');mp=n.new('ShaderNodeMapping');a,b,_,_=ST[st]['rects'][0];c,d,_,_=ST[base]['rects'][0];mp.inputs['Location'].default_value=((a-c)/IM.size[0],(d-b)/IM.size[1],0);l.new(co.outputs['UV'],mp.inputs['Vector']);l.new(mp.outputs['Vector'],t.inputs['Vector']);return m
def pivot(ob,at,parent=None):
 v=Vector(trans(at))
 for x in ob.data.vertices:x.co-=v
 ob.location=v if parent is None else (0,0,0);ob.parent=parent
 return ob
def share():
 for ob in parts:
  me=ob.data;sig=repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[tuple(v.uv) for v in me.uv_layers.active.data]))
  if sig in cache:
   old=me;mats=[s.material for s in ob.material_slots];ob.data=cache[sig]
   for sl,m in zip(ob.material_slots,mats):sl.link='OBJECT';sl.material=m
   bpy.data.meshes.remove(old)
  else:cache[sig]=me
  ob['source_state']=ASSETS[next(reversed(ASSETS))]['base_state']
def finish(family):
 BATCH[family]={'ASSETS':dict(ASSETS),'META':META,'ROOT':ROOT,'OUT':ROOT/('godot/assets/models/structures/'+family)};BATCH[family]['OUT'].mkdir(parents=True,exist_ok=True);ASSETS.clear()
# Safe: front source starts below the four-pixel top projection.
atlas('storage/storage');base='wall_safe';metal=material('Wall_Safe_Shell_Original')
for label,st,opened in [('unlocked','wall_safe',False),('locked','wall_safe_locked',False),('broken_lock','wall_safe_broken',False),('open','wall_safe',True)]:
 begin('wall_safe_'+label,st)
 shell={(x,y,z) for x in range(7,25) for y in range(6) for z in range(10) if alpha(base,x,27-z)>.5}-cells(9,23,1,6,2,9)
 def tx(p,c,a,sg):
  x,y,z=p
  if a==1 and sg==1 and y==6:return uv(base,x,28-z)
  if a==2 and sg==1:return patch(base,(9,14,23,18),(x-7)/18,y/6)
  if a==0:return patch(base,(7,18,9,26),y/6,(10-z)/10)
  return patch(base,(9,16,23,19),(x-7)/18,(10-z)/10 if a==1 else y/6)
 solid('Continuous_Recessed_Safe_Shell',shell,trans,tx,metal)
 hinge=bpy.data.objects.new('Safe_Door_Left_Hinge',None);scene.collection.objects.link(hinge);hinge.location=trans((8,6,5));hinge['axis']='Z';hinge['open_degrees']=105
 dm=shifted(st,base,'Safe_Door_'+label+'_Original')
 def dt(p,c,a,sg):
  if a==1 and sg==1:return uv(base,p[0],28-p[2])
  return patch(base,(9,18,17,20),(p[0]-8)/16,(p[2]-1)/8)
 door=solid('Safe_Door',cells(8,24,0,1,1,9),lambda p:trans((p[0],6+p[1]*.6,p[2])),dt,dm);pivot(door,(8,6,5),hinge)
 # Pixel-stepped dial, matching the source six-pixel ring.
 ring={(x,0,z) for x in range(10,16) for z in range(1,7) if not ((x in [10,15]) and (z in [1,6]))}
 def dialuv(p,c,a,sg):
  if a==1:return uv(base,p[0],28-p[2])
  return patch(base,(11,21,14,22),(p[0]-10)/6,p[1])
 ob=solid('Rotating_Stepped_Dial',ring,lambda p:trans((p[0],6.6+p[1]*.6,p[2])),dialuv,metal);pivot(ob,(13,6.6,4));ob.parent=hinge;ob.location-=hinge.location;ob['rotation_axis']='local Y'
 for x in [18,20,22]:
  for z in [2,4,6]:
   ob=solid('Keypad_Key',cells(x,x+1,0,1,z,z+1),lambda p:trans((p[0],6.6+p[1]*.15,p[2])),lambda p,c,a,sg:patch(base,(c[0],27-c[2],c[0]+1,28-c[2]),p[0]-c[0],p[2]-c[2]),metal);pivot(ob,(8,6,5),hinge)
 if opened:hinge.rotation_euler.z=-math.radians(105)
 share()
finish('wall_safe')
# Frames: connected stepped wooden ring, physical rear board and independent picture insert.
atlas('signs');base='frame-monkey'
for label,st in [('photo_empty','frame-overlay'),('portrait_monkey','frame-monkey'),('portrait_fruit','frame-fruit'),('portrait_rat','frame-rat'),('portrait_ratvar','frame-ratvar')]:
 begin('picture_frame_'+label,st);fm=shifted(st,base,'Frame_'+label+'_Original');bm=material('Frame_Backboard_Original')
 shape=cells(8,24,0,1,8,24)-cells(11,21,0,1,11,21)
 shape|=cells(9,23,1,2,9,23)-cells(10,22,1,2,10,22)
 def ft(p,c,a,sg):
  if a==1 and sg==1:return uv(base,p[0],32-p[2])
  if a==0:return patch(base,(8,8,10,24),p[1]/2,(24-p[2])/16)
  if a==2:return patch(base,(8,8,24,10),(p[0]-8)/16,p[1]/2)
  return patch(base,(8,8,24,10),(p[0]-8)/16,(24-p[2])/16)
 solid('Continuous_Stepped_Wood_Frame',shape,lambda p:trans((p[0],p[1]*.65,p[2]-8)),ft,fm)
 def backtx(p,c,a,sg):return patch('frame-empty',(8,8,10,24),(p[0]-9)/14,(23-p[2])/14)
 ob=solid('Removable_Rear_Board',cells(9,23,0,1,9,23),lambda p:trans((p[0],-.2+p[1]*.2,p[2]-8)),backtx,bm)
 if label=='photo_empty':
  matshape=cells(11,21,0,1,11,21)-cells(12,20,0,1,12,20)
  solid('Photo_White_Mat',matshape,lambda p:trans((p[0],.3+p[1]*.1,p[2]-8)),lambda p,c,a,sg: uv(base,p[0],32-p[2]) if a==1 else patch(base,(11,11,12,12),p[0]-c[0],p[2]-c[2]),fm)
 if label!='photo_empty':
  def pt(p,c,a,sg):
   if a==1:return uv(base,p[0],32-p[2])
   return patch(base,(11,11,21,12),(p[0]-11)/10,p[1])
  ob=solid('Replaceable_Portrait_Insert',cells(11,21,0,1,11,21),lambda p:trans((p[0],.2+p[1]*.1,p[2]-8)),pt,fm);pivot(ob,(16,.2,8));ob['removal_axis']='local -Y'
 slot=bpy.data.objects.new('Photo_Overlay_Slot',None);scene.collection.objects.link(slot);slot.location=trans((16,.35,8));slot['photo_size_m']=[10*S,10*S];slot['source']='runtime framed photo overlay, no invented photo texture'
 share()
finish('picture_frame')
# Ticket machine: separate display and dispenser, wall rail and removable paper.
atlas('service/bureaucracy');base='ticketmachine'
for label,st in [('ready','ticketmachine'),('empty','ticketmachine_empty'),('off','ticketmachine_off')]:
 begin('ticket_machine_'+label,st);m=shifted(st,base,'Ticket_Machine_'+label+'_Original')
 def bodytx(p,c,a,sg):
  x,y,z=p
  if a==1 and sg==1 and y==4:return uv(base,x,25-z)
  if a==2 and sg==1:return patch(base,(4,8,19,12),(x-3)/17,y/4)
  if a==0:return patch(base,(3,12,5,24),y/4,(13-z)/13)
  return patch(base,(4,9,19,12),(x-3)/17,(13-z)/13 if a==1 else y/4)
 solid('Continuous_Display_Housing',cells(3,20,0,4,0,13)-cells(5,18,2,4,4,12),trans,bodytx,m)
 sm=shifted(st,base,'Ticket_Display_'+label+'_Original')
 if label!='off':
  bs=next(n for n in sm.node_tree.nodes if n.type=='BSDF_PRINCIPLED');t=next(n for n in sm.node_tree.nodes if n.type=='TEX_IMAGE');sm.node_tree.links.new(t.outputs['Color'],bs.inputs['Emission Color']);bs.inputs['Emission Strength'].default_value=.25
 def screen(p,c,a,sg):return patch(base,(5,13,18,21),(p[0]-5)/13,(12-p[2])/8)
 solid('Recessed_Display_Surface',cells(5,18,0,1,4,12),lambda p:trans((p[0],3.6+p[1]*.12,p[2])),screen,sm)
 # The mounting rail connects both original adjacent units.
 solid('Shared_Wall_Mounting_Rail',cells(0,32,0,1,1,4),lambda p:trans((p[0],-.5+p[1]*.7,p[2])),lambda p,c,a,sg:patch(base,(1,21,3,23),p[0]/32,p[2]/4),m)
 def disp(p,c,a,sg):
  x,y,z=p
  if a==1 and sg==1 and y==5:return uv(base,x,25-z)
  if a==2 and sg==1:return patch(base,(23,13,28,16),(x-22)/7,y/5)
  if a==0:return patch(base,(22,16,23,24),y/5,(9-z)/9)
  return patch(base,(22,14,29,16),(x-22)/7,(9-z)/9 if a==1 else y/5)
 solid('Continuous_Ticket_Dispenser',{(x,y,z) for x in range(22,29) for y in range(5) for z in range(9) if alpha(base,x,24-z)>.5}-cells(24,27,3,5,3,4),trans,disp,m)
 # The outlet has a closed dark rear wall, floor, roof and jambs.
 solid('Lined_Ticket_Outlet',cells(24,27,0,1,3,4),lambda p:trans((p[0],3+p[1]*.1,p[2])),lambda p,c,a,sg:patch(base,(24,21,27,22),(p[0]-24)/3,p[2]-3),m)
 tail={(x,0,24-y) for x in range(22,28) for y in range(25,29) if alpha(base,x,y)>.5}
 def tailuv(p,c,a,sg):
  if a==1:return uv(base,p[0],25-p[2])
  return patch(base,(c[0],24-c[2],c[0]+1,25-c[2]),p[0]-c[0],p[2]-c[2])
 solid('Lower_Dispenser_Bracket',tail,lambda p:trans((p[0],3.8+p[1]*.4,p[2])),tailuv,m)
 slot=bpy.data.objects.new('Queue_Number_Overlay_Slot',None);scene.collection.objects.link(slot);slot.location=trans((11.5,3.75,8));slot['source']='MAPTEXT(current_number), generated by game; intentionally no replacement digit atlas'
 share()
finish('ticket_machine')
bpy.app.driver_namespace['wall_volume_finish_build']=globals()
