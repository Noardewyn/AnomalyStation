"""Original-atlas modular light fixtures and replaceable emitters; Blender MCP."""
from pathlib import Path
import bpy,bmesh,json,math,hashlib,struct,re,time
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/lighting';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32
IM={};META={};PIX={};SAFE={};ASSETS={};MATS={}
for key in ['lighting','lighting_overlay']:
 p=ROOT/('godot/assets/tg/icons/obj/'+key+'.png');im=bpy.data.images.load(str(p),check_existing=False);IM[key]=im;PIX[key]=list(im.pixels);META[key]={s['name']:s for s in json.loads(p.with_suffix('.json').read_text())['states']}
def rgba(key,state,x,y):
 im=IM[key];tx,ty,_,_=META[key][state]['rects'][0];idx=((im.size[1]-1-ty-y)*im.size[0]+tx+x)*4;return PIX[key][idx:idx+4]
def uv(key,state,x,y):
 tx,ty,_,_=META[key][state]['rects'][0];w,h=IM[key].size;return ((tx+x)/w,1-(ty+y)/h)
def safe(state,box):
 k=(state,box)
 if k in SAFE:return SAFE[k]
 x0,y0,x1,y1=box;best=None;area=0
 for ya in range(y0,y1):
  ok=[True]*(x1-x0)
  for yb in range(ya,y1):
   run=0
   for j,x in enumerate(range(x0,x1)):
    ok[j]=ok[j] and rgba('lighting',state,x,yb)[3]>.99;run=run+1 if ok[j] else 0
    if run*(yb-ya+1)>area:area=run*(yb-ya+1);best=(x0+j-run+1,ya,x0+j+1,yb+1)
 assert best,(state,box)
 SAFE[k]=best;return best
def material(name,key='lighting',glass=False,emission=False):
 if name in MATS:return MATS[name]
 m=bpy.data.materials.new(name);m.use_nodes=True;bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.65 if not glass else .3
 tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=IM[key];tex.interpolation='Closest';m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
 if glass:bs.inputs['Alpha'].default_value=.88
 if emission:
  m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Emission Color']);m.node_tree.links.new(tex.outputs['Alpha'],bs.inputs['Alpha']);bs.inputs['Emission Strength'].default_value=1.5
 MATS[name]=m;return m
metal=material('Light_Housing_Original');glass=material('Light_Frosted_Glass_Original',glass=True);dead=material('Light_Damaged_Glass_Original');glow=material('Light_Overlay_Original','lighting_overlay',emission=True)
def voxel(name,cells,state,view='wall',mat=None,patchbox=None):
 cells=set(cells);verts=[];faces=[];coords=[];lookup={}
 if patchbox is None:patchbox=(4,28,9,32) if state.startswith('tube') else ((13,29,19,32) if state.startswith('bulb') else (10,10,22,22))
 patch=safe(state,patchbox)
 bounds=[(min(p[i] for p in cells),max(p[i] for p in cells)+1) for i in range(3)]
 def vert(p):
  if p not in lookup:lookup[p]=len(verts);verts.append(((p[0]-16)*S,-p[1]*S,p[2]*S))
  return lookup[p]
 # Each solid voxel contributes only boundary faces; no internal surfaces.
 for cell in sorted(cells):
  for axis in range(3):
   for sign in [-1,1]:
    neighbor=list(cell);neighbor[axis]+=sign
    if tuple(neighbor) in cells:continue
    other=[a for a in range(3) if a!=axis];corners=[]
    for u,v in [(0,0),(1,0),(1,1),(0,1)]:
     p=list(cell);p[axis]+=sign>0;p[other[0]]+=u;p[other[1]]+=v;corners.append(tuple(p))
    faces.append([vert(p) for p in corners]);tc=[]
    for p in corners:
     if view=='wall' and state not in ['bulb-empty','bulb-broken'] and axis==1 and sign==1 and rgba('lighting',state,cell[0],31-cell[2])[3]>.99:
      tc.append(uv('lighting',state,p[0],32-p[2]))
     elif view=='floor' and axis==2 and sign==1:
      tc.append(uv('lighting',state,p[0],p[1]))
     else:
      a,b=other;u=(p[a]-bounds[a][0])/(bounds[a][1]-bounds[a][0]);v=(p[b]-bounds[b][0])/(bounds[b][1]-bounds[b][0]);x0,y0,x1,y1=patch
      tc.append(uv('lighting',state,x0+.03+u*(x1-x0-.06),y0+.03+v*(y1-y0-.06)))
    coords.append(tc)
 me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.materials.append(mat or metal);layer=me.uv_layers.new(name='Original_Atlas_UV')
 for p,tc in zip(me.polygons,coords):
  for li,co in zip(p.loop_indices,tc):layer.data[li].uv=co
 bm=bmesh.new();bm.from_mesh(me);assert all(e.is_manifold for e in bm.edges),(name,'initial');bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bmesh.ops.dissolve_limit(bm,angle_limit=.01,verts=list(bm.verts),edges=list(bm.edges),delimit={'UV'});bmesh.ops.triangulate(bm,faces=list(bm.faces));bmesh.ops.dissolve_degenerate(bm,dist=.000001,edges=list(bm.edges));bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));assert all(e.is_manifold for e in bm.edges),name;bm.to_mesh(me);bm.free();me.calc_loop_triangles()
 ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob);ob['source_state']=state;return ob
def overlay(name,state,family):
 # Independent state overlay, following the exposed emitter face.
 im=IM['lighting_overlay'];coords=[];verts=[];faces=[]
 for y in range(32):
  for x in range(32):
   if rgba('lighting_overlay',state,x,y)[3]<.01:continue
   if family!='floor':
    solid=tube_cells() if family=='tube' else bulb_cells()
    depths=[p[1]+1 for p in solid if p[0]==x and p[2]==31-y]
    if not depths:continue
    depth=max(depths)+.015
   cr=[(x,y+1),(x+1,y+1),(x+1,y),(x,y)];start=len(verts)
   for xx,yy in cr:
    if family=='floor':verts.append(((xx-16)*S,-yy*S,.001))
    else:
     verts.append(((xx-16)*S,-depth*S,(32-yy)*S))
   faces.append(tuple(range(start,start+4)));coords.append([uv('lighting_overlay',state,xx,yy) for xx,yy in cr])
 me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.materials.append(glow);layer=me.uv_layers.new(name='Original_Overlay_UV')
 for p,tc in zip(me.polygons,coords):
  for li,co in zip(p.loop_indices,tc):layer.data[li].uv=co
 bm=bmesh.new();bm.from_mesh(me);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001);bmesh.ops.dissolve_limit(bm,angle_limit=.01,verts=list(bm.verts),edges=list(bm.edges),delimit={'UV'});bm.to_mesh(me);bm.free()
 ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob);ob['source_state']=state;return ob
def tube_cells(broken=False):
 cross={(1,1),(2,0),(2,1),(2,2),(3,0),(3,1),(3,2),(4,1)}
 return {(x,y,z) for x in range(8,24) for y,z in cross if not broken or x<11 or x>=21 or (x==11 and z==1) or (x==20 and z==0)}
def bulb_cells(broken=False):
 widths={2:2,3:2,4:3,5:3,6:2}
 if not broken:widths[7]=1
 return {(x,y,z) for z,w in widths.items() for x in range(16-w,16+w) for y in range(3-w,3+w) if not broken or z<6 or x<14 or x>=17}
def build(family,status):
 global scene
 key='light_'+family+'_'+status;scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene
 root=bpy.data.objects.new(key,None);scene.collection.objects.link(root);parts=[]
 state=family+('-'+status if status in ['burned','broken'] else ('_emergency' if status=='emergency' else ''))
 if family=='tube':
  housing={(x,y,z) for x in list(range(4,9))+list(range(23,28)) for y in range(0,5) for z in range(0,4) if rgba('lighting','tube-empty',x,31-z)[3]>.99 and not (x in [8,23] and y in [2,3] and z in [1,2])}
  housing|={(x,0,z) for x in range(8,24) for z in [1,2]}
  parts.append(voxel('Housing_and_Sockets',housing,'tube-empty'))
  if status!='empty':parts.append(voxel('Removable_Tube',tube_cells(status=='broken'),state,mat=glass if status not in ['burned','broken'] else dead,patchbox=(9,29,23,32)))
  insert=(0,-2.5*S,1.5*S)
 elif family=='bulb':
  housing={(x,y,z) for x in range(13,19) for y in range(0,6) for z in range(0,3) if z==0 or not (14<=x<18 and 1<=y<5)}
  parts.append(voxel('Housing_and_Socket',housing,'bulb-empty'))
  if status!='empty':parts.append(voxel('Removable_Bulb',bulb_cells(status=='broken'),state,mat=glass if status not in ['burned','broken'] else dead,patchbox=(14,25,18,29)))
  insert=(0,-3*S,2*S)
 else:
  mask={(x,y) for x in range(10,22) for y in range(10,22) if rgba('lighting','floor-empty',x,y)[3]>.99}
  housing={(x,y,z) for x,y in mask for z in range(-9,0) if z==-9 or not(13<=x<19 and 13<=y<19)}
  parts.append(voxel('Recessed_Housing',housing,'floor-empty',view='floor',patchbox=(10,14,12,18)))
  socket_cells={(x,y,z) for x in range(13,19) for y in range(0,6) for z in range(0,3) if z==0 or not (14<=x<18 and 1<=y<5)}
  holder=voxel('Internal_Bulb_Socket',socket_cells,'bulb-empty');holder.location=(0,-13*S,-9*S);parts.append(holder)
  # Same bulb geometry as the wall fixture, seated below the walkable lens.
  if status!='empty':
   ob=voxel('Removable_Bulb',bulb_cells(status=='broken'),'bulb'+('-'+status if status in ['burned','broken'] else ''),mat=glass if status not in ['burned','broken'] else dead,patchbox=(14,25,18,29));ob.location=(0,-13*S,-9*S);parts.append(ob)
   lens={(x,y,-1) for x in range(12,20) for y in range(12,20) if status!='broken' or x<14 or x>=18 or y>=18}
   parts.append(voxel('Removable_Protective_Lens',lens,state,view='floor',mat=glass if status not in ['burned','broken'] else dead,patchbox=(12,12,20,20)))
  insert=(0,-16*S,-7*S)
 if status in ['on','nightshift','emergency']:
  st=family+('_'+status if status!='on' else '');parts.append(overlay('Emission_Overlay',st,family))
 socket=bpy.data.objects.new('Lamp_Insert',None);scene.collection.objects.link(socket);socket.location=insert;socket.parent=root;socket['accepts']='/obj/item/light/tube' if family=='tube' else '/obj/item/light/bulb'
 for ob in parts:ob.parent=root
 root['family']=family;root['status']=status;root['atlas']='res://assets/tg/icons/obj/lighting.png';root['mount']='floor' if family=='floor' else 'wall';root['grid_m']=.9
 # Move removable part origins to the mating point, retaining geometry placement.
 for ob in parts:
  if ob.name.startswith(('Removable_Tube','Removable_Bulb')):
   delta=Vector(insert)-ob.location
   for v in ob.data.vertices:v.co-=delta
   ob.location=insert
 if family=='floor':
  for ob in parts:ob.location.y+=16*S
  socket.location.y+=16*S;insert=(insert[0],insert[1]+16*S,insert[2])
 ASSETS[key]={'scene':scene,'root':root,'parts':parts,'family':family,'status':status,'insert':insert}
 print('Built',key)
for family in ['tube','bulb','floor']:
 for status in ['off','on','empty','burned','broken','nightshift','emergency']:build(family,status)
# Share meshes when coordinates, UVs and material slots are identical.
shared={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;sig=hashlib.sha256(str(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[tuple(x.uv) for x in me.uv_layers.active.data],[m.name for m in me.materials])).encode()).hexdigest()
  if sig in shared:ob.data=shared[sig]
  else:shared[sig]=me
bpy.app.driver_namespace['lighting_build']=globals()



