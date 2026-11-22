"""Independent checks for rack GLB, atlas bytes and non-collapsed UV coverage."""
from pathlib import Path
from PIL import Image
import ast,struct,json,io,hashlib
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/furniture/storage_rack'
tree=ast.parse((ROOT/'tools/art/validate_station_props.py').read_text())
node=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='accessor')
exec(compile(ast.Module(body=[node],type_ignores=[]),'accessor','exec'))
b=(OUT/'storage_rack.glb').read_bytes();n=struct.unpack_from('<I',b,12)[0];g=json.loads(b[20:20+n]);binary=b[28+n:]
assert not g.get('animations')
assert all(s['magFilter']==s['minFilter']==9728 for s in g['samplers'])
source=ROOT/'godot/assets/tg/icons/obj/structures.png';source_bytes=source.read_bytes()
ims=[]
for image in g['images']:
    view=g['bufferViews'][image['bufferView']];start=view.get('byteOffset',0);png=binary[start:start+view['byteLength']]
    assert png==source_bytes
    ims.append(Image.open(io.BytesIO(png)).convert('RGBA'))
triangles=samples=0
for mesh in g['meshes']:
    for p in mesh['primitives']:
        pos=accessor(g,binary,p['attributes']['POSITION']);uv=accessor(g,binary,p['attributes']['TEXCOORD_0']);ids=[v[0] for v in accessor(g,binary,p['indices'])]
        mat=g['materials'][p['material']];tex=mat['pbrMetallicRoughness']['baseColorTexture'];im=ims[g['textures'][tex['index']]['source']]
        for i in range(0,len(ids),3):
            indices=ids[i:i+3];a,b,c=[pos[j] for j in indices];ab=[b[j]-a[j] for j in range(3)];ac=[c[j]-a[j] for j in range(3)]
            cross=[ab[1]*ac[2]-ab[2]*ac[1],ab[2]*ac[0]-ab[0]*ac[2],ab[0]*ac[1]-ab[1]*ac[0]]
            assert sum(v*v for v in cross)>1e-24
            a,b,c=[uv[j] for j in indices]
            assert abs((b[0]-a[0])*(c[1]-a[1])-(c[0]-a[0])*(b[1]-a[1]))>1e-14
            for aa in range(1,5):
                for bb in range(1,6-aa):
                    weights=(aa/7,bb/7,1-(aa+bb)/7)
                    u,v=[sum(uv[indices[k]][j]*weights[k] for k in range(3)) for j in range(2)]
                    assert 0<=u<=1 and 0<=v<=1
                    assert im.getpixel((min(im.width-1,int(u*im.width)),min(im.height-1,int(v*im.height))))[3]==255
                    samples+=1
            triangles+=1
report={'triangles':triangles,'opaque_uv_samples':samples,'transparent_pixel_hits':0,'degenerate_triangles':0,'collapsed_uv_triangles':0,'embedded_original_png_exact':True,'nearest':True,'animations':False,'atlas_sha256':hashlib.sha256(source_bytes).hexdigest()}
(OUT/'export_validation.json').write_text(json.dumps(report,indent=2)+'\n')
print(report)
