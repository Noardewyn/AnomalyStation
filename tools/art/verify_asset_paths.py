"""Check registered model paths and current preview dependencies after renames."""
from pathlib import Path
import json,re
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'godot/assets/models';OUT=BASE/'station_props'
paths=set()
def walk(value):
    if isinstance(value,str) and value.endswith(('.glb','.blend','.tres')):paths.add(value)
    elif isinstance(value,dict):
        for v in value.values():walk(v)
    elif isinstance(value,list):
        for v in value:walk(v)
source=(BASE/'models.json').read_text()
source=re.sub(r'"(?:\\.|[^"\\])*"|//[^\n]*',lambda m:'' if m[0].startswith('//') else m[0],source)
walk(json.loads(source))
missing=[s for s in paths if not (BASE/s).exists()]
print('Registry references:',len(paths),'missing:',missing)
assert not missing
count=0
for p in (OUT/'previews').glob('*.tscn'):
    for s in re.findall(r'path="([^"]+)"',p.read_text()):
        q=OUT/s[6:] if s.startswith('res://') else p.parent/s
        count+=1
        assert q.exists(),(p,s)
print('Preview references:',count,'all present')
plan=json.loads((ROOT/'docs/ai/art/asset_cleanup_manifest.json').read_text())
remaining=[s for s in plan['delete'] if (ROOT/s).exists()]
assert not remaining,remaining
print('Cleanup targets removed:',len(plan['delete']))
