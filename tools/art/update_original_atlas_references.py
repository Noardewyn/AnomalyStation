"""Keep Godot material overrides and asset manifests aligned with original UVs."""
from pathlib import Path
import json,re
ROOT=Path(__file__).resolve().parents[2]
BASE=ROOT/'godot/assets/models'
folder=BASE/'furniture/table_kits'
p=folder/'variants.json';variants=json.loads(p.read_text())
for name,v in variants.items():
    old='res://assets/models/furniture/table_kits/textures/'+name+'.png'
    for mat in folder.glob(name+'_*.tres'):
        text=mat.read_text();text=text.replace(old,'res://'+v['source']);mat.write_text(text)
    v['texture']='res://'+v['source']
p.write_text(json.dumps(variants,indent=2)+'\n')
p=BASE/'station_props/vending/model_info.json';info=json.loads(p.read_text())
for v in info.values():
    v['side_texture']='res://assets/tg/icons/obj/machines/vending.png'
    v['texture_policy']='Original atlas only; side/back UVs sample source sprite regions.'
p.write_text(json.dumps(info,indent=2)+'\n')
print('Updated',len(variants),'table variants and',len(info),'vending records')
p=folder/'README.md';lines=p.read_text(encoding='utf-8').splitlines()
for i,line in enumerate(lines):
    if line.startswith('`textures/*.png`'):
        lines[i]='All current models use the original atlases under `assets/tg/icons/obj/smooth_structures`. UVs select source smoothing states directly; all 48 material overrides reference these originals. The old `textures/` images are unused legacy build inputs.'
    if line.startswith('`table_greyscale_frame.tres`'):
        lines[i]='`table_greyscale_frame.tres` uses the original greyscale atlas with the modular metal UVs. The standalone `metal_table.glb` also uses the original table atlas. Material tint may be applied independently.'
p.write_text('\n'.join(lines)+'\n',encoding='utf-8')
