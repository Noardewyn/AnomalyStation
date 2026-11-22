"""Verify used PNG pixels against tgstation DMI sources and material references."""
from pathlib import Path
from PIL import Image
import json,hashlib,re
ROOT=Path(__file__).resolve().parents[2];TG=ROOT/'godot/assets/tg';MODELS=ROOT/'godot/assets/models'
audit=json.loads((ROOT/'docs/ai/art/original_atlas_audit.json').read_text())
paths={p for rec in audit['models'].values() for im in rec['images'] for p in im['original_atlases']}
results={}
for relative in sorted(paths):
    png=ROOT/relative;source=ROOT.parent/'tgstation'/png.relative_to(TG).with_suffix('.dmi')
    assert source.exists(),str(source)
    a=Image.open(png).convert('RGBA');b=Image.open(source).convert('RGBA')
    equal=a.size==b.size and a.tobytes()==b.tobytes()
    results[relative]={'source_dmi':str(source),'size':list(a.size),'pixels_identical':equal,'sha256':hashlib.sha256(png.read_bytes()).hexdigest()}
    assert equal,relative
materials=[]
for path in MODELS.rglob('*.tres'):
    for ref in re.findall(r'path="(res://[^\"]+\.png)"',path.read_text()):
        assert ref.startswith('res://assets/tg/'),(path,ref)
        assert (ROOT/'godot'/ref[6:]).exists(),ref
        materials.append({'file':str(path.relative_to(ROOT)),'atlas':ref})
report={'sources':results,'material_overrides':materials}
(ROOT/'docs/ai/art/original_atlas_sources.json').write_text(json.dumps(report,indent=2)+'\n')
print('DMI pixel comparisons:',len(results),'all identical; original material references:',len(materials))
