import json, collections, sys, re, pickle
from dmm_parse import parse_dmm, ROOT
from meta_types import cat, eff, T
path=ROOT+'/_maps/map_files/MetaStation/MetaStation.dmm'
kd,g,c,mx,my=parse_dmm(path)
# per key: area path, other types
keyinfo={}
for k,ents in kd.items():
    area=None; others=[]
    for p,vs in ents:
        if p.startswith('/area'): area=p
        else: others.append(p)
    keyinfo[k]=(area,others)
dept=collections.defaultdict(lambda: {'tiles':0,'types':collections.Counter(),'areas':set()})
EXCL=('area','turf/space','effect/mapping_helpers','effect/landmark')
for z,cols in g.items():
    for (x,y0),col in cols.items():
        for k in col:
            area,others=keyinfo[k]
            if not area: continue
            parts=area.split('/')
            d='/'.join(parts[:4]) if area.startswith('/area/station') else '/'.join(parts[:3])
            dd=dept[d]; dd['tiles']+=1; dd['areas'].add(area)
            for o in others:
                if cat(o) in EXCL: continue
                dd['types'][o]+=1
rows=[]
for d,v in dept.items():
    rows.append((d,v['tiles'],len(v['areas']),len(v['types'])))
rows.sort(key=lambda r:-r[1])
for r in rows[:45]: print(r)
pickle.dump({d:(v['tiles'],len(v['areas']),dict(v['types'])) for d,v in dept.items()},open('meta_dept.pkl','wb'))
