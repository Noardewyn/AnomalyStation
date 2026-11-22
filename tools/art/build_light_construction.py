"""Add wall-light construction stages without replacing completed fixture assets."""
from pathlib import Path
import bpy,bmesh,ast,json,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/machines/light_construction';OUT.mkdir(parents=True,exist_ok=True)
S=.9/32;IM={};META={};PIX={};SAFE={};MATS={};ASSETS={}
p=ROOT/'godot/assets/tg/icons/obj/lighting.png';im=bpy.data.images.load(str(p),check_existing=False);IM['lighting']=im;PIX['lighting']=list(im.pixels);source=json.loads(p.with_suffix('.json').read_text());META['lighting']={s['name']:s for s in source['states']}
tree=ast.parse((ROOT/'tools/art/build_lighting_assets.py').read_text().replace("['bulb-empty','bulb-broken']","['bulb-empty','bulb-broken','bulb-construct-stage1']"));exec(compile(ast.Module(body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in ['rgba','uv','safe','material','voxel']],type_ignores=[]),'lighting_helpers','exec'))
metal=material('Light_Construction_Original_Atlas')
for family in ['tube','bulb']:
 for stage in [1,2]:
  key=f'light_{family}_construction_stage_{stage}';state=f'{family}-construct-stage{stage}';scene=bpy.data.scenes.new(key);bpy.context.window.scene=scene;scene.unit_settings.system='METRIC';parts=[]
  if family=='tube':
   cells={(x,y,z) for x in list(range(4,9))+list(range(23,28)) for y in range(5) for z in range(4) if rgba('lighting','tube-empty',x,31-z)[3]>.99 and not (x in [8,23] and y in [2,3] and z in [1,2])}
   cells|={(x,0,z) for x in range(8,24) for z in [1,2]}
   insert=(0,-2.5*S,1.5*S)
  else:
   cells={(x,y,z) for x in range(13,19) for y in range(6) for z in range(3) if z==0 or not (14<=x<18 and 1<=y<5)}
   insert=(0,-3*S,2*S)
  parts.append(voxel('Continuous_Frame_With_Lined_Socket',cells,state,patchbox=(4,28,8,32) if family=='tube' else (13,30,19,32)))
  if stage==2:
   if family=='tube':
    wire={(8,3,1),(8,3,2),(9,3,1)};neutral={(22,3,2),(23,3,2)}
   else:
    wire={(14,3,2),(14,3,3),(15,3,2)};neutral={(17,3,2),(17,3,3)}
   parts.append(voxel('Removable_Live_Wire',wire,state,patchbox=(8,29,10,31) if family=='tube' else (14,28,16,30)))
   parts.append(voxel('Removable_Neutral_Wire',neutral,state,patchbox=(22,29,24,30) if family=='tube' else (17,28,18,30)))
  socket=bpy.data.objects.new('Future_Lamp_Insert',None);scene.collection.objects.link(socket);socket.location=insert;socket['accepts_after_assembly']='tube' if family=='tube' else 'bulb'
  ASSETS[key]={'scene':scene,'parts':parts,'base_state':state,'overlay_states':[]}
cache={}
for a in ASSETS.values():
 for ob in a['parts']:
  me=ob.data;sig=repr(([tuple(v.co) for v in me.vertices],[tuple(p.vertices) for p in me.polygons],[tuple(v.uv) for v in me.uv_layers.active.data]))
  if sig in cache:ob.data=cache[sig]
  else:cache[sig]=me
META=source
bpy.app.driver_namespace['light_construction_build']=globals()
print('Built four wall light construction presets')
