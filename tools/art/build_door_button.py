"""Original-atlas door buttons, service covers and interchangeable electronics."""
from pathlib import Path
import bpy,bmesh,ast,json,math,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/door_button';OUT.mkdir(parents=True,exist_ok=True)
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


metal=material('Door_Button_Original');lit=material('Door_Button_Lit_Original',True)
def trans(p):return ((p[0]-16)*S,-p[1]*S,(p[2]-15.5)*S)
def attach(ob,parent):
 for v in ob.data.vertices:v.co-=Vector(parent.location)
 ob.parent=parent;ob.location=(0,0,0)
def box(name,rect,zdepth,thickness,st,parent=None):
 x0,y0,x1,y1=rect
 cells={(x,0,z) for x in range(x0,x1) for z in range(32-y1,32-y0)}
 def tex(p,c,a,sg):
  if a==1 and sg==1:return uv(st,p[0],32-p[2])
  return patch(st,rect,(p[0]-x0)/(x1-x0),(32-y0-p[2])/(y1-y0))
 ob=solid(name,cells,lambda p:trans((p[0],zdepth+p[1]*thickness,p[2])),tex,metal)
 if parent:attach(ob,parent)
 return ob
def effect(st,name,parent,mask=False):
 verts=[];faces=[];coords=[];alphas=[]
 for y in range(32):
  for x in range(32):
   alpha=pixel(st,x,y)[3]
   if alpha<.01:continue
   cr=[(x,y+1),(x+1,y+1),(x+1,y),(x,y)];n=len(verts);verts.extend([trans((xx,2.51 if mask else 2.52,32-yy)) for xx,yy in cr]);faces.append(tuple(range(n,n+4)));coords.append([uv(closed,xx,yy) if mask else uv(st,xx,yy) for xx,yy in cr]);alphas.append(round(alpha,4))
 me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);u=me.uv_layers.new(name='Original_Atlas_UV')
 for f,cc in zip(me.polygons,coords):
  for li,co in zip(f.loop_indices,cc):u.data[li].uv=co
 if mask:
  levels=sorted(set(alphas))
  for a in levels:
   m=material('Door_Button_Mask_Emission',True);next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Emission Strength'].default_value=a*.4;me.materials.append(m)
  for f,a in zip(me.polygons,alphas):f.material_index=levels.index(a)
 else:
  m=material('Door_Button_Feedback_Overlay',True);bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');t=next(n for n in m.node_tree.nodes if n.type=='TEX_IMAGE');m.node_tree.links.new(t.outputs['Alpha'],bs.inputs['Alpha']);me.materials.append(m)
 ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob);parts.append(ob);ob['source_state']=st;attach(ob,parent)
variants=[]
for warning in [False,True]:
 for mode in ['normal','nopower','success','error','open_empty','open_board','open_device','open_signaler','open_board_device','open_board_signaler']:variants.append((warning,mode))
variants.append((False,'assembly'))
for warning,mode in variants:
 skin='button-warning' if warning else 'button';opened=mode.startswith('open') or mode=='assembly';closed=skin+'-nopower' if opened or mode=='nopower' else skin
 key='door_button_'+('warning_' if warning else '')+mode;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;parts=[];scene.unit_settings.system='METRIC';ASSETS[key]={'scene':scene,'parts':parts,'base_state':'button_assembly' if mode=='assembly' else skin+'-open' if opened else closed,'overlay_states':[]}
 # Closed housing with a real recessed compartment and top perspective mapped to roof.
 cells={(x,y,z) for x in range(11,21) for y in range(2) for z in range(12,19)}-{(x,1,z) for x in range(12,20) for z in range(13,18)}
 def caseuv(p,c,a,sg):
  x,y,z=p
  if a==1 and sg==1:return uv(skin+'-open',x,32-z)
  if a==2 and sg==1:return patch(skin,(11,11,21,13),(x-11)/10,y/2)
  return patch(skin+'-nopower',(11,13,13,20),y/2 if a==0 else (x-11)/10,(19-z)/7)
 solid('Continuous_Housing',cells,trans,caseuv,metal)
 hinge=bpy.data.objects.new('Cover_Hinge',None);scene.collection.objects.link(hinge);hinge.location=trans((16,2.05,13));hinge['axis']='X'
 box('Service_Cover',(12,13,20,19),2.05,.25,closed,hinge)
 button=box('Push_Button',(15,15,17,17),2.30,.18,closed,hinge);button['press_axis']='local Y';button['press_travel_m']=.003
 if opened:hinge.rotation_euler.x=math.radians(110)
 for component,st,rect in [('board','button-overlay-board',(12,14,16,19)),('device','button-overlay-device',(16,14,20,19)),('signaler','button-overlay-signaler',(16,14,20,19))]:
  if component in mode:
   box(component.title()+'_Module',rect,1.10,.45,st);ASSETS[key]['overlay_states'].append(st)
 if not opened and mode!='nopower':
  effect('button-light-mask','Glow_Mask_Overlay',hinge,True);ASSETS[key]['overlay_states'].append('button-light-mask')
 if mode in ['success','error']:
  effect('button-overlay-'+mode,'Feedback_Overlay',hinge);ASSETS[key]['overlay_states'].append('button-overlay-'+mode)
cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;h=repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[[tuple(v.uv) for v in u.data] for u in me.uv_layers],[m.name for m in me.materials]))
  if h in cache:ob.data=cache[h]
  else:cache[h]=me
META={'width':32,'height':32,'states':[s for s in META['states'] if s['name'].startswith('button')]}
bpy.app.driver_namespace['door_button_build']=globals();print('Built',len(ASSETS),'door button presets')
