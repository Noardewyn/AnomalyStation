"""Independently inspect all exported vending GLBs, including transformed atlas UVs."""
from pathlib import Path
from PIL import Image
from collections import Counter
import ast,struct,json,io,hashlib,math
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/station_props/vending'
catalog=json.loads((OUT/'source_catalog.json').read_text());info=json.loads((OUT/'model_info.json').read_text())
source=ROOT/catalog['source_png'];assert hashlib.sha256(source.read_bytes()).hexdigest()==catalog['png_sha256']
approved={hashlib.sha256(source.read_bytes()).hexdigest()}
tree=ast.parse((ROOT/'tools/art/validate_station_props.py').read_text());node=next(n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name=='accessor');exec(compile(ast.Module(body=[node],type_ignores=[]),'accessor','exec'))
results={};errors=[]
assert len(info)==67 and set(info)==set(catalog['vendors'])
for state,rec in info.items():
    path=OUT/(rec['key']+'.glb');blob=path.read_bytes();n=struct.unpack_from('<I',blob,12)[0];g=json.loads(blob[20:20+n]);binary=blob[28+n:]
    assert len(blob)==struct.unpack_from('<I',blob,8)[0] and not g.get('animations')
    assert all(s['magFilter']==s['minFilter']==9728 for s in g['samplers'])
    images=[]
    for im in g['images']:
        view=g['bufferViews'][im['bufferView']];start=view.get('byteOffset',0);png=binary[start:start+view['byteLength']]
        assert hashlib.sha256(png).hexdigest() in approved,(state,'unexpected texture')
        images.append(Image.open(io.BytesIO(png)).convert('RGBA'))
    triangles=samples=transparent=collapsed=degenerate=nonmanifold=0
    for mesh in g['meshes']:
        edges=Counter()
        for p in mesh['primitives']:
            pos=accessor(g,binary,p['attributes']['POSITION']);ids=[v[0] for v in accessor(g,binary,p['indices'])]
            mat=g['materials'][p['material']];texture=mat.get('pbrMetallicRoughness',{}).get('baseColorTexture');mode=mat.get('alphaMode','OPAQUE')
            if texture:
                uv=accessor(g,binary,p['attributes']['TEXCOORD_'+str(texture.get('texCoord',0))]);im=images[g['textures'][texture['index']]['source']]
                trans=texture.get('extensions',{}).get('KHR_texture_transform',{});offset=trans.get('offset',[0,0]);scale=trans.get('scale',[1,1])
                assert not trans.get('rotation',0)
            for i in range(0,len(ids),3):
                indices=ids[i:i+3];vertices=[tuple(round(c,6) for c in pos[j]) for j in indices]
                for a,b in zip(vertices,vertices[1:]+vertices[:1]):edges[tuple(sorted((a,b)))]+=1
                a,b,c=[pos[j] for j in indices];ab=[b[j]-a[j] for j in range(3)];ac=[c[j]-a[j] for j in range(3)]
                cross=[ab[1]*ac[2]-ab[2]*ac[1],ab[2]*ac[0]-ab[0]*ac[2],ab[0]*ac[1]-ab[1]*ac[0]]
                if sum(v*v for v in cross)<1e-24:degenerate+=1
                triangles+=1
                if not texture:continue
                a,b,c=[uv[j] for j in indices]
                if abs((b[0]-a[0])*(c[1]-a[1])-(c[0]-a[0])*(b[1]-a[1]))<1e-14:collapsed+=1
                if mode!='OPAQUE':continue
                for aa in range(1,5):
                    for bb in range(1,6-aa):
                        weights=(aa/7,bb/7,1-(aa+bb)/7)
                        u,v=[sum(uv[indices[k]][j]*weights[k] for k in range(3))*scale[j]+offset[j] for j in range(2)]
                        assert -.00001<=u<=1.00001 and -.00001<=v<=1.00001
                        alpha=im.getpixel((max(0,min(im.width-1,int(u*im.width))),max(0,min(im.height-1,int(v*im.height)))))[3]
                        if alpha==0:transparent+=1
                        samples+=1
        nonmanifold+=sum(n!=2 for n in edges.values())
    result={'triangles':triangles,'opaque_uv_samples':samples,'transparent_pixel_hits':transparent,'collapsed_uv_triangles':collapsed,'degenerate_triangles':degenerate,'nonmanifold_edges':nonmanifold,'embedded_pngs_verified':True,'nearest':True,'animations':False}
    results[state]=result
    if transparent or collapsed or degenerate or nonmanifold:errors.append((state,result))
(OUT/'export_validation.json').write_text(json.dumps(results,indent=2)+'\n')
print('Models',len(results),'Triangles',sum(r['triangles'] for r in results.values()),'Samples',sum(r['opaque_uv_samples'] for r in results.values()))
print('Errors',json.dumps(errors,indent=2));assert not errors
