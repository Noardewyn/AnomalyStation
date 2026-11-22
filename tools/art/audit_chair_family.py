"""Read source DMI/JSON and validate exported GLB geometry, UVs and pixel sampling."""
from pathlib import Path
from PIL import Image
import json, hashlib, struct, math, re

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'godot/assets/models/furniture/chair_family'
SRC=ROOT/'godot/assets/tg/icons/obj/chairs.png'
DMI=ROOT.parent/'tgstation/icons/obj/chairs.dmi'
META=json.loads(SRC.with_suffix('.json').read_text())
im=Image.open(SRC).convert('RGBA');source=Image.open(DMI)
assert source.convert('RGBA').tobytes()==im.tobytes()
states={};first=0
for part in source.info['Description'].split('state = ')[1:]:
    name=re.match(r'"([^"]+)"',part)[1]
    dirs=int(re.search(r'dirs = (\d+)',part)[1]) if 'dirs = ' in part else 1
    frames=int(re.search(r'frames = (\d+)',part)[1]) if 'frames = ' in part else 1
    states[name]={'dirs':dirs,'frames':frames,'first':first};first+=dirs*frames
selected=['stool','officechair_dark','officechair_white','comfychair','comfychair_armrest','bar','wooden_chair','wooden_chair_wings']
audit={
    'dmi_source':str(DMI),'png_source':str(SRC.relative_to(ROOT)),
    'png_sha256':hashlib.sha256(SRC.read_bytes()).hexdigest(),
    'dmi_sha256':hashlib.sha256(DMI.read_bytes()).hexdigest(),
    'png_pixels_equal_original_dmi':True,'atlas_px':list(im.size),
    'tile_px':[META['width'],META['height']],'direction_order':['S','N','E','W'],
    'dm_source':'code/game/objects/structures/beds_chairs/chair.dm',
    'dm_usage':{
        'wooden_chair':'/obj/structure/chair/wood; line 181',
        'wooden_chair_wings':'/obj/structure/chair/wood/wings; line 196',
        'comfychair':'/obj/structure/chair/comfy; line 200; has_armrest=TRUE; tint variants at lines 215-228',
        'comfychair_armrest':'update_overlays at lines 110-121; ABOVE_MOB_LAYER when occupied',
        'officechair_dark':'/obj/structure/chair/office; line 297; unanchored, noisy movement',
        'officechair_white':'/obj/structure/chair/office/light; line 321',
        'stool':'/obj/structure/chair/stool; line 327',
        'bar':'/obj/structure/chair/stool/bar; line 368'},
    'states':{}}
for s in META['states']:
    if s['name'] not in selected:continue
    original=states[s['name']]
    assert all(original[k]==s[k] for k in ['dirs','frames','first'])
    rects=[[(n%13)*32,(n//13)*32,32,32] for n in range(s['first'],s['first']+s['dirs']*s['frames'])]
    assert s['rects']==rects
    audit['states'][s['name']]=s
(OUT/'source_audit.json').write_text(json.dumps(audit,indent=2)+'\n',encoding='utf-8')


def accessor(g,bin_data,index):
    a=g['accessors'][index];v=g['bufferViews'][a['bufferView']]
    size={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
    fmt={5120:'b',5121:'B',5122:'h',5123:'H',5125:'I',5126:'f'}[a['componentType']]
    width=struct.calcsize('<'+fmt*size);stride=v.get('byteStride',width)
    start=v.get('byteOffset',0)+a.get('byteOffset',0)
    return [struct.unpack_from('<'+fmt*size,bin_data,start+i*stride) for i in range(a['count'])]


results={}
for path in sorted(OUT.glob('*.glb')):
    blob=path.read_bytes();n=struct.unpack_from('<I',blob,12)[0]
    g=json.loads(blob[20:20+n]);bin_data=blob[28+n:]
    assert struct.unpack_from('<I',blob,8)[0]==len(blob)
    assert not g.get('animations')
    assert all(s['minFilter']==s['magFilter']==9728 for s in g['samplers'])
    assert all(m.get('alphaMode','OPAQUE')=='OPAQUE' for m in g['materials'])
    for img in g['images']:
        v=g['bufferViews'][img['bufferView']];start=v.get('byteOffset',0)
        assert bin_data[start:start+v['byteLength']]==SRC.read_bytes()
    triangles=samples=0;bad=[];degenerate=[];min_area=float('inf')
    for mesh in g['meshes']:
        for p in mesh['primitives']:
            assert p.get('mode',4)==4
            pos=accessor(g,bin_data,p['attributes']['POSITION'])
            tex=accessor(g,bin_data,p['attributes']['TEXCOORD_0'])
            idx=[v[0] for v in accessor(g,bin_data,p['indices'])]
            mat=g['materials'][p['material']]
            transform=mat['pbrMetallicRoughness']['baseColorTexture'].get('extensions',{}).get('KHR_texture_transform',{})
            offset=transform.get('offset',[0,0]);scale=transform.get('scale',[1,1])
            for i in range(0,len(idx),3):
                triangles+=1;ids=idx[i:i+3];a,b,c=[pos[k] for k in ids]
                ab=[b[k]-a[k] for k in range(3)];ac=[c[k]-a[k] for k in range(3)]
                cross=[ab[1]*ac[2]-ab[2]*ac[1],ab[2]*ac[0]-ab[0]*ac[2],ab[0]*ac[1]-ab[1]*ac[0]]
                area=math.sqrt(sum(v*v for v in cross))/2;min_area=min(area,min_area)
                if area<1e-12:degenerate.append([mesh['name'],i//3])
                # Sample triangle interiors; boundaries intentionally follow pixel edges.
                for aa in range(1,7):
                    for bb in range(1,8-aa):
                        wa=aa/9;wb=bb/9;wc=1-wa-wb
                        u,v=[sum(tex[ids[j]][k]*w for j,w in enumerate((wa,wb,wc)))*scale[k]+offset[k] for k in range(2)]
                        assert 0<=u<=1 and 0<=v<=1,(path.name,u,v)
                        x=min(im.width-1,int(u*im.width));y=min(im.height-1,int(v*im.height))
                        samples+=1
                        if im.getpixel((x,y))[3]<255:bad.append([mesh['name'],i//3,x,y])
    results[path.stem]={'exported_triangles':triangles,'uv_interior_samples':samples,
        'transparent_pixel_hits':len(bad),'transparent_pixel_examples':bad[:12],
        'degenerate_triangles':len(degenerate),'degenerate_examples':degenerate[:12],
        'minimum_triangle_area_m2':min_area,'embedded_png_exact':True,'nearest':True}
(OUT/'export_validation.json').write_text(json.dumps(results,indent=2)+'\n',encoding='utf-8')
print(json.dumps(results,indent=2))
assert all(r['transparent_pixel_hits']==0 and r['degenerate_triangles']==0 for r in results.values())
