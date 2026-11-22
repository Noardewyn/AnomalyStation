"""Continuous pixel-boundary console shells, original atlas on every surface."""
from pathlib import Path
import bpy,bmesh,json,math,struct,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/consoles'
SRC=ROOT/'godot/assets/tg/icons/obj/machines/computer.png'
states={s['name']:s for s in json.loads(SRC.with_suffix('.json').read_text())['states']}
im=bpy.data.images.load(str(SRC),check_existing=False);W,H=im.size;pixels=list(im.pixels)
def alpha(x,y):return pixels[((H-1-y)*W+x)*4+3]
def material(name,overlay=False):
 m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes;bs=next(n for n in n if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.8
 t=n.new('ShaderNodeTexImage');t.image=im;t.interpolation='Closest';m.node_tree.links.new(t.outputs['Color'],bs.inputs['Base Color'])
 if overlay:
  m.node_tree.links.new(t.outputs['Alpha'],bs.inputs['Alpha']);m.node_tree.links.new(t.outputs['Color'],bs.inputs['Emission Color']);bs.inputs['Emission Strength'].default_value=.5
 return m
bodymat=material('Console_Original_Atlas');overmat=material('Console_Overlay_Original_Atlas',True)
def rect(state,d=0):return states[state]['rects'][d]
def atlas(state,x,y,d=0):
 tx,ty,_,_=rect(state,d);return ((tx+x)/W,1-(ty+y)/H)
safe_cache={}
def safe(state,box,d=0):
 key=(state,box,d)
 if key in safe_cache:return safe_cache[key]
 tx,ty,_,_=rect(state,d);x0,y0,x1,y1=box;best=None;area=0
 for ya in range(y0,y1):
  ok=[True]*(x1-x0)
  for yb in range(ya,y1):
   run=0
   for j,x in enumerate(range(x0,x1)):
    ok[j]=ok[j] and alpha(tx+x,ty+yb)>.99;run=run+1 if ok[j] else 0
    if run*(yb-ya+1)>area:area=run*(yb-ya+1);best=(x0+j-run+1,ya,x0+j+1,yb+1)
 assert best,key
 safe_cache[key]=best;return best
def patch(state,box,u,v,d=0):
 x0,y0,x1,y1=safe(state,box,d);return atlas(state,x0+.02+u*(x1-x0-.04),y0+.02+v*(y1-y0-.04),d)
def profile(y,bronze):
 knots=[(2,.15,.90),(4,-.01,.86),(18,-.09,.30),(22,-.31,.24),(27,-.22,.12),(31,-.32,0)] if bronze else [(1,.18,.90),(7,-.01,.84),(18,-.09,.32),(25,-.37,.22),(28,-.26,0)]
 for a,b in zip(knots,knots[1:]):
  if a[0]<=y<=b[0]:
   t=(y-a[0])/(b[0]-a[0]);z=a[2]*(1-t)+b[2]*t
   pedestal_top=.24 if bronze else .22
   z=z*3 if z<=pedestal_top else z+2*pedestal_top
   return (a[1]*(1-t)+b[1]*t,z)
 raise ValueError(y)
ASSETS={}
def build(key,bronze):
 state='ratvarcomputer1' if bronze else 'computer';screen='ratvar1' if bronze else 'generic';keyboard='ratvar_key1' if bronze else 'generic_key'
 sc=bpy.data.scenes.new(key+'_Revised');bpy.context.window.scene=sc;tx,ty,_,_=rect(state)
 # The clockwork front contains painted black negative space; open it physically.
 cells={(x,y) for y in range(2 if bronze else 1,31 if bronze else 28) for x in range(32) if alpha(tx+x,ty+y)>.99}
 if bronze:
  cells={p for p in cells if not(22<=p[1]<27 and not(5<=p[0]<8 or 24<=p[0]<27))}
  # Perspective-only right-hand ornament belongs to the side, not the front silhouette.
  cells={p for p in cells if not(12<=p[1]<19 and p[0]>=30)}
 scale=.04;back=.29;verts=[];faces=[];uvs=[];index={}
 def vertex(v):
  k=tuple(round(c,7) for c in v)
  if k not in index:index[k]=len(verts);verts.append(k)
  return index[k]
 def point(x,y,recess=0,rear=False):
  yy,z=profile(y,bronze);return ((x-16)*scale,back if rear else yy+recess,z)
 def face(v,uv):
  ids=[vertex(p) for p in v]
  if len(set(ids))<3:return
  faces.append(ids);uvs.append(uv)
 def inset(x,y):return .024 if ((4 if bronze else 5)<=x<(28 if bronze else 27) and (4 if bronze else 8)<=y<17) else 0
 def backuv(x,y):
  return patch(state,(4,2,28,7) if not bronze else (4,27,28,30),x/32,(y-(2 if bronze else 1))/(29 if bronze else 27))
 for x,y in sorted(cells):
  corners=[(x,y+1),(x+1,y+1),(x+1,y),(x,y)];dep=inset(x,y)
  face([point(a,b,dep) for a,b in corners],[atlas(state,a,b) for a,b in corners])
  face([point(a,b,rear=True) for a,b in reversed(corners)],[backuv(a,b) for a,b in reversed(corners)])
  for (a,b),(c,d),neighbor in [(corners[0],corners[1],(x,y+1)),(corners[1],corners[2],(x+1,y)),(corners[2],corners[3],(x,y-1)),(corners[3],corners[0],(x-1,y))]:
   if neighbor in cells:
    other=inset(*neighbor)
    if dep>=other:continue
    v=[point(a,b,dep),point(c,d,dep),point(c,d,other),point(a,b,other)]
    uv=[patch(state,(2,7,5,17),u,vv) for u,vv in [(0,0),(1,0),(1,1),(0,1)]]
   else:
    v=[point(a,b,dep),point(c,d,dep),point(c,d,rear=True),point(a,b,rear=True)]
    # Console rendering remaps every body UV into the live front tile.
    # Keep the side panels in that same tile: directional frame coordinates
    # would land on transparent pixels when remapped to the front frame.
    if a==c:
     direction=0
     box=(2,4,5,18) if bronze else ((27,7,30,18) if a>16 else (2,7,5,18))
     uv=[patch(state,box,u,(yy-(2 if bronze else 1))/(29 if bronze else 27),direction) for u,yy in [(0,b),(0,d),(1,d),(1,b)]]
    else:uv=[patch(state,(3,2,29,7) if not bronze else (4,2,28,4), xx/32,vv) for xx,vv in [(a,0),(c,0),(c,1),(a,1)]]
   face(v,uv)
 me=bpy.data.meshes.new(key+'_Shell');me.from_pydata(verts,[],faces);me.materials.append(bodymat);layer=me.uv_layers.new(name='Atlas_UV')
 for p,coords in zip(me.polygons,uvs):
  for li,co in zip(p.loop_indices,coords):layer.data[li].uv=co
 bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
 # Weld and dissolve only continuous UV regions; preserve stepped silhouettes and recesses.
 bmesh.ops.dissolve_limit(bm,angle_limit=.01,verts=list(bm.verts),edges=list(bm.edges),delimit={'UV'})
 bmesh.ops.triangulate(bm,faces=list(bm.faces),quad_method='BEAUTY',ngon_method='BEAUTY')
 bmesh.ops.dissolve_degenerate(bm,dist=.000001,edges=list(bm.edges))
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bad=sum(not e.is_manifold for e in bm.edges)
 assert bad==0,(key,bad)
 bm.to_mesh(me);bm.free();me.calc_loop_triangles();body=bpy.data.objects.new('Body',me);sc.collection.objects.link(body)
 def overlay(name,st,yrange):
  vs=[];fs=[];coords=[]
  for y in range(*yrange):
   # Follow the real recessed surface; split at recess and casing boundaries.
   left,right=(4,28) if bronze else (5,27)
   for xa,xb in [(0,left),(left,right),(right,32)]:
    cr=[(xa,y+1),(xb,y+1),(xb,y),(xa,y)]
    start=len(vs);dep=inset(xa,y)
    for x,yy in cr:
     p=point(x,yy,dep);vs.append((p[0],p[1]-.0015,p[2]))
    fs.append(tuple(range(start,start+4)));coords.append(cr)
  mesh=bpy.data.meshes.new(name);mesh.from_pydata(vs,[],fs);mesh.materials.append(overmat);uv=mesh.uv_layers.new(name='Atlas_UV');uv2=mesh.uv_layers.new(name='Tile_UV')
  for p,cr in zip(mesh.polygons,coords):
   for li,(x,y) in zip(p.loop_indices,cr):uv.data[li].uv=atlas(st,x,y);uv2.data[li].uv=(x/32,1-y/32)
  bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.000001);bmesh.ops.dissolve_limit(bm,angle_limit=.01,verts=list(bm.verts),edges=list(bm.edges),delimit={'UV'});bm.to_mesh(mesh);bm.free()
  ob=bpy.data.objects.new(name,mesh);sc.collection.objects.link(ob);return ob
 scr=overlay('Screen_Overlay',screen,(4 if bronze else 7,18));keys=overlay('Keyboard_Overlay',keyboard,(18,22 if bronze else 25))
 root=bpy.data.objects.new(key,None);sc.collection.objects.link(root)
 for ob in [body,scr,keys]:ob.parent=root
 root['source_atlas']='res://assets/tg/icons/obj/machines/computer.png';root['height_m']=1.38 if bronze else 1.34
 root['pedestal_height_m']=.72 if bronze else .66
 ASSETS[key]={'scene':sc,'parts':[body,scr,keys],'root':root,'base_state':state,'screen_state':screen,'keyboard_state':keyboard}
 print(key,'body triangles',len(me.loop_triangles),'vertices',len(me.vertices),'manifold',bad==0)
for key,bronze in [('console_standard',False),('console_bronze',True)]:build(key,bronze)
bpy.app.driver_namespace['console_rebuild']=globals()



