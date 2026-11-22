"""Explicit geometry families, source-plane interpretation and texture reuse."""
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/station_props/vending'
catalog=json.loads((OUT/'source_catalog.json').read_text())
groups={
'wardrobe':('Shared tall display and full-width delivery slot','atmosdrobe bardrobe cargodrobe centdrobe chapdrobe chefdrobe chemdrobe clothes coroner_drobe curadrobe detdrobe engidrobe genedrobe hydrobe janidrobe lawdrobe medidrobe robodrobe scidrobe secdrobe virodrobe'),
'snack':('Shared product window, right keypad and lower dispenser','snack snackblue snackorange snackgreen snackteal sustenance sustenance_labor nutri seeds'),
'branded_drink':('Shared solid graphic front, side controls and recessed delivery slot','Cola_Machine cola_black red_cola space_up pwr_game shamblers_juice'),
'drink_display':('Large inset graphic display, centered delivery slot','soda starkist'),
'magic':('Same display and delivery geometry; distinct magical artwork','MagiVend RunicVendor'),
'wide_medical':('Wide upright cabinet, tall left display and lower-right dispenser','med drug'),
'wide_service':('Wide cabinet with left service display, right keypad and small dispenser','cart sec'),
'wide_electronics':('Wide horizontal display with broad lower dispenser; widths differ by source','robotics modularpc'),
'wide_food':('Wide solid advertising panel, right keypad and broad dispenser','snackdonk syndi nt-donk liberationstation'),
'compact_product':('Compact centered product display; exact recess sizes selected per sprite','cigs generic parts games'),
'industrial':('Tall industrial body, lower panel and asymmetric control recess','tool engivend'),
'custom':('Wide custom product display and broad lower tray','custom greed'),
'coffee':('Coffee panel above a centered cup recess','coffee'),
'dinnerware':('Tall open-looking shelf display and right controls','dinnerware'),
'engi':('Wide industrial panel with lower horizontal item display','engi'),
'sovietsoda':('Separate protruding upper sign and lower dispensing cavity','sovietsoda'),
'boozeomat':('Extra-wide cabinet: bottle window, separate keypad and delivery recess','boozeomat'),
'hotdog':('Protruding central dispenser between deep side casings','hotdog-vendor'),
'wall_medical':('Shallow wall-mounted compact dispenser, not a shortened floor cabinet','wallmed'),
'theater':('Large costume display, small delivery opening','theater'),
'cytopro':('Narrow portrait display, side controls and lower delivery opening','cytopro'),
}
mapping={};rows=[]
for family,(note,words) in groups.items():
    states=words.split()
    for name in states:
        assert name not in mapping;mapping[name]=family
    rows.append({'family':family,'states':states,'reuse':note})
assert set(mapping)==set(catalog['vendors']),(set(catalog['vendors'])-set(mapping),set(mapping)-set(catalog['vendors']))
data={'appearance_count':len(mapping),'family_count':len(rows),'families':rows,'state_to_family':mapping,'sharing_rule':'Same family plus exact source silhouette, width and recess layout. Geometry and UV data are linked; per-state material selects atlas tile. Different silhouettes/layouts keep separate meshes.','source_interpretation':'Upper horizontal cap and edge bands are mapped to physical roof and stepped side edges. The front begins below the roof band. Side and rear service textures are derived separately from source colours and trim, never a repeated full frontal sprite.','state_policy':'67 declared appearance states, not 287 independent machines. Off/broken/deny/animation frames are state variants; frame metadata is preserved, no animations are added.'}
(OUT/'geometry_families.json').write_text(json.dumps(data,indent=2)+'\n')
lines=['# Vending machine geometry classification','',f"{len(mapping)} machine appearances in the supplied atlas, grouped into {len(rows)} construction families. The source atlas is 800 × 800, tiles are 32 × 32; all 287 states retain source frame/direction metadata in the adjacent JSON.",'',data['sharing_rule'],'',data['source_interpretation'],'','| Family | Reusable appearances | Geometry |','|---|---|---|']
lines += [f"| {r['family']} | {', '.join(r['states'])} | {r['reuse']} |" for r in rows]
lines+=['',data['state_policy']]
(OUT/'CLASSIFICATION.md').write_text('\n'.join(lines)+'\n')
print('Classified',len(mapping),'appearances in',len(rows),'families')
