"""Read saved image datablocks without changing the live user's scenes."""
from pathlib import Path
import bpy,json,hashlib
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'godot/assets/models';ORIGINALS={}
for p in (ROOT/'godot/assets/tg').rglob('*.png'):ORIGINALS[hashlib.sha256(p.read_bytes()).hexdigest()]=str(p)
FILES=sorted(BASE.rglob('*.blend'));RESULTS={}
def batch(start,end):
    for path in FILES[start:end]:
        with bpy.data.libraries.load(str(path),link=False) as (src,dst):dst.images=list(src.images)
        records=[]
        for im in dst.images:
            resolved=Path(bpy.path.abspath(im.filepath,start=str(path.parent))) if im.filepath else None
            packed=im.packed_file
            data=bytes(packed.data) if packed else (resolved.read_bytes() if resolved and resolved.exists() else b'')
            sha=hashlib.sha256(data).hexdigest();original=ORIGINALS.get(sha)
            records.append({'image':im.name,'filepath':im.filepath,'resolved':str(resolved),'packed':bool(packed),'sha256':sha,'original':original,'size':list(im.size),'source':im.source})
            bpy.data.images.remove(im)
        RESULTS[str(path.relative_to(BASE))]=records
    (ROOT/'docs/ai/art/blend_atlas_audit.json').write_text(json.dumps(RESULTS,indent=2)+'\n')
    print('Audited',len(RESULTS),'of',len(FILES),'BLENDs')
bpy.app.driver_namespace['blend_atlas_audit']=globals()
