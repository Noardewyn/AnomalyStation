"""Wall notice board with original atlas and separate pinned paper meshes."""
from pathlib import Path
import bpy,bmesh,ast,json
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/furniture/notice_board';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;ASSETS={};MATS={};shared=[]
p=ROOT/'godot/assets/tg/icons/obj/wallmounts.png';IM=bpy.data.images.load(str(p),check_existing=False);META=json.loads(p.with_suffix('.json').read_text());ST={s['name']:s for s in META['states']}
for f,names in [('build_transport_assets.py',['solid']),('build_cell_charger.py',['patch','material'])]:
 tree=ast.parse((ROOT/'tools/art'/f).read_text());exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in names],type_ignores=[]),'helpers','exec'))
def uv(st,x,y):
 tx,ty,_,_=ST[st]['rects'][0];return ((tx+x)/IM.size[0],1-(ty+y)/IM.size[1])
def trans(p):return ((p[0]-16)*S,-p[1]*S,(p[2]-7.5)*S)
mat=material('Notice_Board_Housing_Original');cache={}
for count in range(9):
 key='notice_board_'+('empty' if count==0 else str(count)+'_notices');scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[];st='noticeboard';ovs=['notices_'+str(count)] if count else [];ASSETS[key]={'scene':scene,'parts':parts,'base_state':st,'overlay_states':ovs,'frame':0}
 if not shared:
  # Connected closed board with the original cork and wooden perimeter.
  cells={(x,y,z) for x in range(4,28) for y in range(2) for z in range(15)}
  def tx(p,c,a,sg):
   x,y,z=p
   if a==1 and sg==1:return uv(st,x,30-z)
   if a==2 and sg==1:return patch(st,(4,12,28,15),(x-4)/24,y/2)
   if a==0:return patch(st,(4,15,6,30),y/2,(15-z)/15)
   if a==2:return patch(st,(4,28,28,30),(x-4)/24,y/2)
   return patch(st,(6,16,26,28),(x-4)/24,(15-z)/15)
  def shape(p):
   x,y,z=p
   return trans((x,y,z))
  solid('Continuous_Notice_Board',cells,shape,tx,mat)
  shared=list(parts)
 else:
  for src in shared:
   ob=src.copy();scene.collection.objects.link(ob);parts.append(ob)
 if count:
  state=ovs[0];paper=material('Notice_Board_'+str(count)+'_Notices_Original');nodes=paper.node_tree.nodes;links=paper.node_tree.links;t=next(n for n in nodes if n.type=='TEX_IMAGE');co=nodes.new('ShaderNodeTexCoord');mp=nodes.new('ShaderNodeMapping');sx,sy,_,_=ST[state]['rects'][0];mp.inputs['Location'].default_value=((sx-224)/IM.size[0],(128-sy)/IM.size[1],0);links.new(co.outputs['UV'],mp.inputs['Vector']);links.new(mp.outputs['Vector'],t.inputs['Vector'])
  pixels=list(IM.pixels);remaining={(x,y) for y in range(32) for x in range(32) if pixels[((IM.size[1]-1-sy-y)*IM.size[0]+sx+x)*4+3]>.99};components=[]
  while remaining:
   seed=min(remaining);remaining.remove(seed);group={seed};pending=[seed]
   while pending:
    x,y=pending.pop()
    for q in [(x-1,y),(x+1,y),(x,y-1),(x,y+1)]:
     if q in remaining:remaining.remove(q);group.add(q);pending.append(q)
   components.append(group)
  for i,group in enumerate(components):
   x0=min(x for x,y in group);x1=max(x for x,y in group)+1;y0=min(y for x,y in group);y1=max(y for x,y in group)+1
   def pt(p,c,a,sg):
    if a==1:return uv('notices_1',p[0],30-p[2])
    return patch('notices_1',(x0,y0,x1,y1),(p[0]-x0)/(x1-x0),(30-y0-p[2])/(y1-y0))
   ob=solid('Pinned_Notice_'+str(i+1),{(x,0,29-y) for x,y in group},lambda p:trans((p[0],2.02+p[1]*.035,p[2])),pt,paper);ob['source_state']=state
   pivot=Vector(trans(((x0+x1)/2,2.02,30-y0)))
   for v in ob.data.vertices:v.co-=pivot
   ob.location=pivot;ob['removal_axis']='local -Y away from board'
   signature=tuple(sorted(group))
   if signature in cache:
    old=ob.data;ob.data=cache[signature];ob.material_slots[0].link='OBJECT';ob.material_slots[0].material=paper;bpy.data.meshes.remove(old)
   else:cache[signature]=ob.data
 for ob in parts:
  if 'source_state' not in ob:ob['source_state']='noticeboard'
bpy.app.driver_namespace['notice_board_build']=globals()
