"""Validate and save assemblies using shared table meshes."""
from pathlib import Path
import bpy,json,math
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/furniture/table_kits'
kit=sorted([s for s in bpy.data.scenes if s.name.startswith('Table_Families_Kit')],key=lambda s:s.name)[-1]
scene=bpy.data.scenes.new('Table_Family_Assembly_Examples')
manifest=json.loads((OUT/'modules.json').read_text());records=manifest['modules'];assemblies=[]
def rotated(mask,turns):
 for _ in range(turns):mask=((mask>>1)|((mask&1)<<3))&15
 return mask
for row,family in enumerate(['wood','cloth','glass','framed_glass','reinforced']):
 for col,(shape,cells) in enumerate([('Row',[(0,0),(1,0),(2,0)]),('L',[(0,0),(1,0),(0,1)]),('Square',[(x,y) for x in range(3) for y in range(3)])]):
  cells=set(cells);feet=[];pieces=[]
  for x,y in sorted(cells):
   mask=sum(bit for dx,dy,bit in [(0,1,1),(1,0,2),(0,-1,4),(-1,0,8)] if (x+dx,y+dy) in cells)
   choices=[r for r in records if r['family']==family and not ('corner_1leg' in r['file'] if shape=='L' else 'corner_2legs' in r['file'])]
   match=next((r,t) for r in choices for t in range(4) if rotated(r['mask'],t)==mask)
   r,t=match;name=Path(r['file']).stem;src=next(o for o in kit.objects if o.name.split('.')[0]==name)
   ob=src.copy();ob.name=family+'_'+shape+'_'+str(x)+'_'+str(y);scene.collection.objects.link(ob);ob.location=(col*3.6+x*.9,row*3.6+y*.9,0);ob.rotation_euler.z=t*math.pi/2
   # Foot positions from the canonical previous metal kit layout.
   pos=[]
   if r['legs']==4:pos=[(-.385,-.385),(.385,-.385),(.385,.385),(-.385,.385)]
   elif 'end' in name:pos=[(-.385,-.385),(-.385,.385)]
   elif r['legs']:pos=[(-.385,-.385)]+([(.385,.385)] if r['legs']==2 else [])
   for px,py in pos:
    for _ in range(t):px,py=-py,px
    feet.append((round(x*.9+px,5),round(y*.9+py,5)))
   pieces.append({'cell':[x,y],'file':r['file'],'godot_y_degrees':t*90})
  assert len(feet)==len(set(feet))
  assemblies.append({'family':family,'shape':shape,'modules':pieces,'legs':len(feet)})
bpy.data.libraries.write(str(OUT/'table_family_examples.blend'),{scene},path_remap='RELATIVE',fake_user=True,compress=True)
(OUT/'assembly_examples.json').write_text(json.dumps(assemblies,indent=2)+'\n')
print('Validated 15 assemblies: three-cell rows, L corners, 3x3 surfaces; no duplicate supports.')
