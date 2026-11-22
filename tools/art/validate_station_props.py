"""Independently validate GLB triangles, image bytes, samplers and atlas UVs."""
from pathlib import Path
from PIL import Image
import json,struct,hashlib,io,math,collections,sys

ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/station_props'
audit=json.loads((OUT/'source_audit.json').read_text())
approved={rec['png_sha256']:rec['png'] for rec in audit['sources'].values()}
all_models='--all-models' in sys.argv
if all_models:
 for file in (ROOT/'godot/assets/tg').rglob('*.png'):approved[hashlib.sha256(file.read_bytes()).hexdigest()]=str(file.relative_to(ROOT))
for rec in audit['sources'].values():assert hashlib.sha256((ROOT/rec['png']).read_bytes()).hexdigest()==rec['png_sha256']

def accessor(g,blob,index):
 a=g['accessors'][index];v=g['bufferViews'][a['bufferView']]
 size={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4}[a['type']]
 fmt={5121:'B',5123:'H',5125:'I',5126:'f'}[a['componentType']]
 width=struct.calcsize('<'+fmt*size);stride=v.get('byteStride',width);start=v.get('byteOffset',0)+a.get('byteOffset',0)
 return [struct.unpack_from('<'+fmt*size,blob,start+i*stride) for i in range(a['count'])]

results={};failures=[]
volume_only='--volume-only' in sys.argv
volume_keys=set(json.loads((OUT/'volume_rebuild_validation.json').read_text())) if volume_only else set()
for file in sorted((ROOT/'godot/assets/models' if all_models else OUT).rglob('*.glb')):
 if 'backups' in file.parts:continue
 if volume_only and file.stem not in volume_keys:continue
 blob=file.read_bytes();n=struct.unpack_from('<I',blob,12)[0];g=json.loads(blob[20:20+n]);binary=blob[28+n:]
 assert len(blob)==struct.unpack_from('<I',blob,8)[0]
 assert not g.get('animations')
 assert all(s['magFilter']==9728 and s['minFilter'] in (9728,9984) for s in g.get('samplers',[])),(str(file),g.get('samplers'))
 images=[]
 for entry in g.get('images',[]):
  v=g['bufferViews'][entry['bufferView']];start=v.get('byteOffset',0);png=binary[start:start+v['byteLength']]
  digest=hashlib.sha256(png).hexdigest();assert digest in approved,(file.name,digest)
  images.append(Image.open(io.BytesIO(png)).convert('RGBA'))
 tris=sample_count=bad_uv=bad_area=masked=glass=semi=0;examples=[];mesh_triangles=[]
 for mesh in g['meshes']:
  mesh_triangles.append(0)
  for p in mesh['primitives']:
   assert p.get('mode',4)==4
   pos=accessor(g,binary,p['attributes']['POSITION']);idx=[i[0] for i in accessor(g,binary,p['indices'])]
   mat=g['materials'][p['material']];mode=mat.get('alphaMode','OPAQUE')
   if mode=='MASK':masked+=1
   if mode=='BLEND':glass+=1
   texture=mat.get('pbrMetallicRoughness',{}).get('baseColorTexture')
   if texture:
    image=images[g['textures'][texture['index']]['source']]
    trans=texture.get('extensions',{}).get('KHR_texture_transform',{});offset=trans.get('offset',[0,0]);scale=trans.get('scale',[1,1])
    tex=accessor(g,binary,p['attributes']['TEXCOORD_'+str(texture.get('texCoord',0))])
   for i in range(0,len(idx),3):
    tris+=1;mesh_triangles[-1]+=1;ids=idx[i:i+3];a,b,c=[pos[k] for k in ids]
    ab=[b[j]-a[j] for j in range(3)];ac=[c[j]-a[j] for j in range(3)]
    cross=[ab[1]*ac[2]-ab[2]*ac[1],ab[2]*ac[0]-ab[0]*ac[2],ab[0]*ac[1]-ab[1]*ac[0]]
    if sum(v*v for v in cross)<1e-24:bad_area+=1
    if not texture or mode!='OPAQUE':continue
    for aa in range(1,5):
     for bb in range(1,6-aa):
      weights=(aa/7,bb/7,1-(aa+bb)/7)
      u,v=[sum(tex[ids[k]][j]*weights[k] for k in range(3))*scale[j]+offset[j] for j in range(2)]
      assert -.00001<=u<=1.00001 and -.00001<=v<=1.00001,(file.name,u,v)
      x=max(0,min(image.width-1,int(u*image.width)));y=max(0,min(image.height-1,int(v*image.height)))
      sample_count+=1
      alpha=image.getpixel((x,y))[3]
      if 0<alpha<255:semi+=1
      if alpha==0:
       bad_uv+=1
       if len(examples)<8:examples.append({'mesh':mesh['name'],'material':mat['name'],'pixel':[x,y]})
 result={'unique_geometry_triangles':tris,'triangles':sum(mesh_triangles[node['mesh']] for node in g['nodes'] if 'mesh' in node),
         'opaque_uv_samples':sample_count,'transparent_pixel_hits':bad_uv,'source_semi_alpha_samples_on_opaque_surfaces':semi,'degenerate_triangles':bad_area,
         'masked_overlay_primitives':masked,'transparent_glass_water_primitives':glass,'embedded_png_bytes_verified':True,'nearest_verified':True,'examples':examples}
 results[file.stem]=result
 if bad_uv or bad_area:failures.append((file.stem,bad_uv,bad_area,examples))
(ROOT/'docs/ai/art/all_models_uv_validation.json' if all_models else OUT/('volume_export_validation.json' if volume_only else 'export_validation.json')).write_text(json.dumps(results,indent=2)+'\n')
print('Models',len(results),'triangles',sum(r['triangles'] for r in results.values()),'opaque UV samples',sum(r['opaque_uv_samples'] for r in results.values()))
print('Failures',json.dumps(failures,indent=2))
assert not failures
