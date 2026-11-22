"""Audit embedded GLB textures against untouched TG atlas files, without accepting derivatives."""
from pathlib import Path
from collections import defaultdict,Counter
import json,hashlib,struct,io
from PIL import Image
ROOT=Path(__file__).resolve().parents[2];MODELS=ROOT/'godot/assets/models';ATLAS=ROOT/'godot/assets/tg'
originals=defaultdict(list);local=defaultdict(list)
for base,index in [(ATLAS,originals),(MODELS,local)]:
    for p in base.rglob('*.png'):index[hashlib.sha256(p.read_bytes()).hexdigest()].append(str(p.relative_to(ROOT)))
report={};unknown={}
for p in sorted(MODELS.rglob('*.glb')):
    raw=p.read_bytes();n=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+n]);binary=raw[28+n:];images=[]
    for im in g.get('images',[]):
        if 'bufferView' in im:
            view=g['bufferViews'][im['bufferView']];start=view.get('byteOffset',0);data=binary[start:start+view['byteLength']]
        else:data=(p.parent/im['uri']).read_bytes()
        sha=hashlib.sha256(data).hexdigest();record={'name':im.get('name'),'sha256':sha,'original_atlases':originals.get(sha,[]),'local_files':local.get(sha,[])}
        if not record['original_atlases']:
            image=Image.open(io.BytesIO(data));record['size']=list(image.size);unknown[sha]=record
        images.append(record)
    report[str(p.relative_to(MODELS))]={'images':images,'original_only':all(i['original_atlases'] for i in images),'image_count':len(images),'textureless_materials':[m.get('name') for m in g.get('materials',[]) if not m.get('pbrMetallicRoughness',{}).get('baseColorTexture')]}
out=ROOT/'docs/ai/art/original_atlas_audit.json';out.write_text(json.dumps({'models':report,'unmatched_images':unknown},indent=2)+'\n')
print('GLBs',len(report),'original-only',sum(v['original_only'] for v in report.values()),'with derived/unmatched images',sum(not v['original_only'] for v in report.values()),'unique unmatched',len(unknown))
for folder,count in Counter(str(Path(k).parent) for k,v in report.items() if not v['original_only']).items():print(folder,count)
for sha,r in unknown.items():
    if '/vending/textures/' not in ' '.join(r['local_files']):print(r['name'],r.get('size'),r['local_files'])
