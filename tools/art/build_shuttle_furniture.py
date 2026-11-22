"""Source-audited shuttle equipment and modular seating, original atlas UVs."""
from pathlib import Path
import bpy,bmesh,json,math,struct,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/station_props/shuttle_furniture'
meta=json.loads((OUT/'source_states.json').read_text());materials={};report={};assets={}
def enum(o,p,v):return next(i.identifier for i in o.bl_rna.properties[p].enum_items if i.identifier==v)
for key in meta:
 im=bpy.data.images.load(str(OUT/'textures'/(key+'.png')),check_existing=True);im.pack()
 m=bpy.data.materials.new('SF_'+key);m.use_nodes=True
 bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.8
 tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=im;tex.interpolation=enum(tex,'interpolation','Closest');m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color']);materials[key]=m
def uvrect(key,state,rect,direction=0):
 r=meta[key]['states'][state]['rects'][direction*meta[key]['states'][state]['frames']];W,H=meta[key]['sheet'];x,y,w,h=rect
 return [((r[0]+xx)/W,1-(r[1]+yy)/H) for xx,yy in [(x,y+h),(x+w,y+h),(x+w,y),(x,y)]]
def start(name):
 global scene,parts,root
 scene=bpy.data.scenes.new('SF_'+name);bpy.context.window.scene=scene;parts=[]
 root=bpy.data.objects.new(name,None);scene.collection.objects.link(root);root['forward_godot']='+Z';root['floor_origin']=True
class Builder:
 def __init__(self,key,state):self.key=key;self.state=state;self.v=[];self.f=[];self.uv=[]
 def box(self,bounds,front,top=None,side=None,back=None):
  x0,x1,y0,y1,z0,z1=bounds;n=len(self.v);self.v.extend([(x0,y0,z0),(x1,y0,z0),(x1,y1,z0),(x0,y1,z0),(x0,y0,z1),(x1,y0,z1),(x1,y1,z1),(x0,y1,z1)])
  quads=[(0,1,5,4),(2,3,7,6),(1,2,6,5),(3,0,4,7),(4,5,6,7),(3,2,1,0)]
  for ids,rect in zip(quads,[front,back or side or front,side or front,side or front,top or front,side or front]):self.f.append([n+i for i in ids]);self.uv.append(uvrect(self.key,self.state,rect))
 def finish(self,name,origin=(0,0,0)):
  me=bpy.data.meshes.new(name);me.from_pydata([Vector(v)-Vector(origin) for v in self.v],[],self.f);me.materials.append(materials[self.key]);layer=me.uv_layers.new(name='Original_Atlas_UV')
  for p,uv in zip(me.polygons,self.uv):
   for li,c in zip(p.loop_indices,uv):layer.data[li].uv=c
  bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));assert all(e.is_manifold for e in bm.edges);bm.to_mesh(me);bm.free();me.calc_loop_triangles()
  ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob);ob.location=origin;ob.parent=root;ob['source_state']=self.state;parts.append(ob);return ob
def save(name,notes=''):
 bpy.context.view_layer.update()
 for o in scene.objects:o.select_set(True)
 bpy.context.view_layer.objects.active=parts[0]
 path=OUT/(name+'.glb');bpy.ops.export_scene.gltf(filepath=str(path),use_selection=True,use_active_scene=True,export_animations=False,export_extras=True)
 blob=path.read_bytes();n=struct.unpack_from('<I',blob,12)[0];g=json.loads(blob[20:20+n]);tail=blob[20+n:]
 for s in g.get('samplers',[]):s.update(minFilter=9728,magFilter=9728)
 for nd in g['nodes']:nd['name']=nd['name'].split('.')[0]
 sourcebytes={(OUT/'textures'/(k+'.png')).read_bytes() for k in meta}
 for img in g.get('images',[]):
  v=g['bufferViews'][img['bufferView']];off=v.get('byteOffset',0);assert tail[8+off:8+off+v['byteLength']] in sourcebytes
 raw=json.dumps(g,separators=(',',':')).encode();raw+=b' '*(-len(raw)%4);path.write_bytes(struct.pack('<III',0x46546c67,2,20+len(raw)+len(tail))+struct.pack('<II',len(raw),0x4e4f534a)+raw+tail)
 bpy.data.libraries.write(str(OUT/(name+'.blend')),{scene},path_remap='RELATIVE',fake_user=True,compress=True)
 tris=sum(len(o.data.loop_triangles) for o in parts);report[name]={'triangles':tris,'meshes':len(parts),'notes':notes,'parts':[o.name.split('.')[0] for o in parts]};assets[name]=(scene,list(parts))
# Seating modules: 0.9m pitch, flat contiguous ends, terminal arms only.
for family,prefix in [('sofa','sofa'),('corporate_sofa','corp_sofa'),('wooden_pew','pew')]:
 for kind in (['middle','left','right'] if family=='wooden_pew' else ['middle','left','right','corner']):
  state=prefix+('middle' if kind=='middle' else 'corner' if kind=='corner' else 'end_'+kind)
  name=family+'_'+kind;start(name);b=Builder('wide',prefix+'middle');pew=family=='wooden_pew'
  # A seat cushion and a sloping-looking layered back, with original front texels.
  b.box((-.45,.45,-.45,.45,.34,.46),(0,23,32,5),(0,17,32,8),(12,20,8,5))
  b.box((-.45,.45,.31,.45,.46,.94),(0,5,32,12),(0,4,32,2),(12,7,8,10))
  if not pew:b.box((-.44,.44,.25,.32,.51,.89),(0,6,32,11),(0,5,32,2),(12,8,4,8))
  for x in [-.34,.34]:
   for y in [-.27,.25]:b.box((x-.045,x+.045,y-.045,y+.045,0,.35),(3,27,3,3),side=(3,27,2,3))
  if kind in ['left','right']:
   # Source left/right are sprite directions; model names follow visible arm side.
   x=-.405 if kind=='left' else .405
   b.state=state
   # Pew direction names match visible ends; sofa ends are opposite in SOUTH.
   sx=(1 if kind=='left' else 28) if pew else (28 if kind=='left' else 1)
   b.box((x-.045,x+.045,-.45,.31,.44,.65),(sx,8,3,18),side=(sx,8,3,18),top=(sx,5,3,3))
  if kind=='corner':b.box((.31,.45,-.45,.31,.46,.94),(12,7,8,10),side=(0,5,32,12),top=(0,4,32,2))
  b.finish('Body');root['grid_pitch_m']=.9;save(name,'Module; pitch 0.90m. Ends join along X; corner turns 90 degrees.')
# Shuttle chair with independent hinged overhead restraint.
start('shuttle_seat');b=Builder('chairs','shuttle_chair')
b.box((-.25,.25,-.29,.25,.36,.48),(9,24,14,3),(8,19,16,7),(8,20,3,7))
b.box((-.24,.24,.12,.27,.46,1.12),(9,4,14,15),(10,3,12,2),(7,7,3,16))
b.box((-.16,.16,.1,.28,1.1,1.27),(11,2,10,4),side=(11,2,2,4))
for x in [-.29,.29]:b.box((x-.045,x+.045,-.25,.2,.43,.69),(5 if x<0 else 24,14,3,10),side=(5,15,3,9))
b.box((-.22,.22,-.22,.22,.08,.35),(10,27,12,3),side=(10,27,3,3));b.box((-.29,.29,-.30,.30,0,.08),(9,29,14,2),side=(9,29,14,2));b.finish('Seat')
b=Builder('chairs','shuttle_chair_down_front')
for x in [-.17,.17]:b.box((x-.025,x+.025,-.13,-.07,.66,1.16),(10 if x<0 else 20,9,2,14),side=(10,9,2,14))
b.box((-.20,.20,-.15,-.05,.80,.85),(10,14,12,2),side=(10,14,12,2));ob=b.finish('Safety_Restraint',(0,.12,1.16));ob['open_rotation_x_degrees']=-110
save('shuttle_seat','Seat and hinged restraint. Seat height 0.48m; total height 1.27m.')
# Shelf frame and individually removable book groups; source uses book-0..book-5.
start('bookcase');b=Builder('library','book-0')
for x in [-.79,.79]:b.box((x-.06,x+.06,-.23,.22,0,1.8),(1 if x<0 else 29,3,2,28),side=(2,5,2,24))
b.box((-.73,.73,.16,.23,.08,1.7),(5,9,22,3),side=(3,5,2,23))
b.box((-.85,.85,-.23,.23,1.53,1.8),(1,0,30,6),(3,1,26,4),(3,2,2,3))
for z,sy in [(.03,29),(.53,22),(1.03,14),(1.51,6)]:b.box((-.73,.73,-.23,.20,z,z+.055),(4,sy,24,2),(4,sy,24,2),(3,sy,2,2))
b.finish('Cabinet')
for row,sy in enumerate([24,16,8]):
 b=Builder('library','book-5')
 for j in range(8):
  x=-.63+j*.165;h=.29+(.03 if j%3 else 0);b.box((x,x+.13,-.20,.11,.085+row*.5,.085+row*.5+h),(5+j*2.6,sy,2.4,5),side=(5+j*2.6,sy,1,5),top=(5+j*2.6,sy,2.4,1))
 b.finish('Books_Row_'+str(row+1))
save('bookcase','Real shelves, separate book rows. Game fill states book-0 through book-5; no joining behaviour in current code.')
# Heater and nozzle bodies match their direction: exhaust faces -Y (Godot +Z).
start('engine_heater');b=Builder('shuttle','heater')
b.box((-.88,.88,-.75,.72,.12,1.0),(3,24,26,7),(3,8,26,20),(3,10,5,18))
for x in [-.67,.67]:b.box((x-.12,x+.12,-.68,.64,1.0,1.16),(4,10,4,17),(4,10,4,17),(4,10,2,17))
b.box((-.36,.36,-.48,.40,1.0,1.12),(12,10,8,13),(12,10,8,13),(12,23,8,2))
for x in [-.5,.5]:b.box((x-.10,x+.10,-.88,-.72,.35,.63),(11,27,3,5),side=(11,27,3,5))
b.box((-.78,.78,-.6,.60,0,.12),(6,28,20,3),side=(6,28,20,3));b.finish('Heater');save('engine_heater','Standalone heater; code explicitly sets engine_power=0. No automatic heater connection in current source.')
for suffix,state in [('', 'propulsion'),('_left','propulsion_l'),('_right','propulsion_r')]:
 start('propulsion_engine'+suffix);b=Builder('shuttle',state)
 # Octagonal stepped casing, with a recessed closed exhaust cavity.
 rings=[(.8,.88),(.52,.88),(.48,.67),(.20,.67),(.16,.83),(-.42,.83),(-.48,.70),(-.72,.57),(-.80,.43),(-.82,.31),(-.60,.31)]
 for y,r in rings:
  b.v.extend([(r*math.cos(math.tau*j/8+math.pi/8),y,.94+r*math.sin(math.tau*j/8+math.pi/8)) for j in range(8)])
 for k in range(len(rings)-1):
  for j in range(8):
   b.f.append([k*8+j,k*8+(j+1)%8,(k+1)*8+(j+1)%8,(k+1)*8+j]);b.uv.append(uvrect('shuttle',state,(3+j*3.2,2+k*2.4,3.2,2.4)))
 for ids in [list(reversed(range(8))),list(range((len(rings)-1)*8,len(rings)*8))]:
  b.f.append(ids);r=meta['shuttle']['states'][state]['rects'][0];W,H=meta['shuttle']['sheet'];b.uv.append([((r[0]+16)/W,1-(r[1]+31)/H)]*8)
 for x in [-.62,.62]:b.box((x-.11,x+.11,-.48,.65,0,.34),(3,15,4,10),side=(3,15,4,10))
 b.finish('Engine');root['grid_pitch_m']=2.;save('propulsion_engine'+suffix,'Directional propulsion variant. Source has explicit left/centre/right appearances, not automatic smoothing.')
# Shared shuttle airlock. Fill_closed is composited per pixel without changing PNGs.
def door_piece(name,cells,rectstate,offset,depth=.10):
 vs=[];fs=[];lookup={}
 def idx(x,y,d):
  k=(x,y,d)
  if k not in lookup:lookup[k]=len(vs);vs.append((x/16-1,d,2-y/16))
  return lookup[k]
 def q(points):fs.append([idx(*p) for p in points])
 for x,y in cells:
  for d in [-depth,depth]:q([(x,y,d),(x+1,y,d),(x+1,y+1,d),(x,y+1,d)])
  for dx,dy,a,c in [(-1,0,(x,y),(x,y+1)),(1,0,(x+1,y),(x+1,y+1)),(0,-1,(x,y),(x+1,y)),(0,1,(x,y+1),(x+1,y+1))]:
   if (x+dx,y+dy) not in cells:q([(*a,-depth),(*c,-depth),(*c,depth),(*a,depth)])
 me=bpy.data.meshes.new(name);me.from_pydata(vs,[],fs);me.materials.append(materials['airlock'])
 # Use a second material with the same atlas, UVs selecting fill_closed where needed.
 layer=me.uv_layers.new(name='Original_Atlas_UV');W,H=meta['airlock']['sheet']
 im=next(n.image for n in materials['airlock'].node_tree.nodes if n.type=='TEX_IMAGE');pixels=list(im.pixels);r=meta['airlock']['states'][rectstate]['rects'][0];fill=meta['airlock']['states']['fill_closed']['rects'][0]
 for p in me.polygons:
  center=sum((me.vertices[i].co for i in p.vertices),Vector())/len(p.vertices)
  # A side face lies on a pixel edge: it reads the pixel half a pixel inside its own cell, not the transparent one beyond (the client's alpha scissor drops it).
  shift=Vector()
  if abs(p.normal.y)<.5:
   probe=center+p.normal/32;shift=p.normal/32 if (math.floor((probe.x+1)*16),math.floor((2-probe.z)*16)) in cells else -p.normal/32
  center=center+shift;x=min(31,max(0,int((center.x+1)*16)));y=min(31,max(0,int((2-center.z)*16)))
  a=pixels[((H-1-r[1]-y)*W+r[0]+x)*4+3];use=fill if a<.1 and rectstate=='closed' else r
  for li in p.loop_indices:
   co=me.vertices[me.loops[li].vertex_index].co+shift;layer.data[li].uv=((use[0]+(co.x+1)*16)/W,1-(use[1]+(2-co.z)*16)/H)
 bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.normal_update();bmesh.ops.dissolve_limit(bm,angle_limit=.001,verts=list(bm.verts),edges=list(bm.edges),delimit={'UV'});bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));assert all(e.is_manifold for e in bm.edges);bm.to_mesh(me);bm.free();me.calc_loop_triangles()
 ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob);ob.parent=root;ob['open_offset_blender']=offset;parts.append(ob)
start('escape_pod_airlock')
im=next(n.image for n in materials['airlock'].node_tree.nodes if n.type=='TEX_IMAGE');pixels=list(im.pixels);W,H=im.size
def opaque(state):
 r=meta['airlock']['states'][state]['rects'][0]
 return {(x,y) for x in range(32) for y in range(32) if pixels[((H-1-r[1]-y)*W+r[0]+x)*4+3]>.1}
frame=opaque('open');cells=(opaque('closed')|opaque('fill_closed'))-frame
door_piece('Frame',frame,'open',(0,0,0))
for left in [True,False]:
 def seam(y):return y+6 if y<10 else 16 if y<24 else y-7
 region={(x,y) for x,y in cells if (x<seam(y))==left};door_piece('Door_Left' if left else 'Door_Right',region,'closed',(-1.3 if left else 1.3,0,0),.06) # thinner than the frame: no z-fight as they slide through it
save('escape_pod_airlock','/airlock/titanium, opaque fill_closed layer. Same type as named Emergency Shuttle Airlock.')
# Second named export shares the exact same mesh data and UVs.
root.name='escape_shuttle_airlock';save('escape_shuttle_airlock','Alias of escape_pod_airlock; same titanium door and shared geometry.')
# Review scene includes assembled modular furniture, not isolated thumbnails only.
preview=bpy.data.scenes.new('Shuttle_Furniture_Review');bpy.context.window.scene=preview
def instance(name,x,y,angle=0):
 for original in assets[name][1]:
  ob=original.copy();ob.parent=None;preview.collection.objects.link(ob);ob.name=name+'_'+original.name
  # Apply parent placement to the local hinge origin as well.
  pos=original.location.copy();c=math.cos(angle);s=math.sin(angle);ob.location=(x+c*pos.x-s*pos.y,y+s*pos.x+c*pos.y,pos.z);ob.rotation_euler.z+=angle
for i,family in enumerate(['sofa','corporate_sofa','wooden_pew']):
 for j,kind in enumerate(['left','middle','right']):instance(family+'_'+kind,j*.9,i*2)
 if family!='wooden_pew':instance(family+'_corner',3.8,i*2)
instance('shuttle_seat',5.3,0);instance('bookcase',5.3,2)
instance('engine_heater',0,6.6)
for j,name in enumerate(['propulsion_engine_left','propulsion_engine','propulsion_engine_right']):instance(name,2.2+j*2,6.6)
instance('escape_pod_airlock',0,9.3);instance('escape_shuttle_airlock',3,9.3)
world=bpy.data.worlds.new('SF_World');world.use_nodes=True;preview.world=world;next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs['Strength'].default_value=.6
for label,loc,power in [('Key',(-4,-5,12),2300),('Fill',(10,5,10),1800)]:
 ld=bpy.data.lights.new(label,'AREA');ld.energy=power;ld.size=8;ob=bpy.data.objects.new(label,ld);preview.collection.objects.link(ob);ob.location=loc;ob.rotation_euler=(Vector((2,4,0))-ob.location).to_track_quat('-Z','Y').to_euler()
camdata=bpy.data.cameras.new('SF_Camera');camdata.type=enum(camdata,'type','ORTHO');camdata.ortho_scale=15;cam=bpy.data.objects.new('SF_Camera',camdata);preview.collection.objects.link(cam);target=Vector((2.6,4.3,.65));cam.location=target+Vector((4,-14,12));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();preview.camera=cam
preview.render.engine='CYCLES';preview.cycles.samples=24;preview.render.resolution_x=1600;preview.render.resolution_y=1600;preview.render.resolution_percentage=100;preview.render.filepath=str(ROOT/'docs/ai/art/shuttle_furniture_preview.png')
bpy.data.libraries.write(str(OUT/'shuttle_furniture_review.blend'),{preview},path_remap='RELATIVE',fake_user=True,compress=True)
(OUT/'models.json').write_text(json.dumps(report,indent=2)+'\n')
for scr in bpy.data.screens:
 for area in scr.areas:
  if area.type=='VIEW_3D':
   sp=area.spaces.active;sp.shading.type=enum(sp.shading,'type','MATERIAL');sp.region_3d.view_location=target;sp.region_3d.view_distance=16;sp.region_3d.view_rotation=Vector((4,-14,12)).to_track_quat('Z','Y')
result={'models':report,'scene':preview.name}
