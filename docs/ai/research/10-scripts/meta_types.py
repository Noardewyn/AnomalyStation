import json, collections, sys, re
from dmm_parse import summarize, ROOT
T=json.load(open('dm_types.json'))
def anc(p):
    while '/' in p[1:]:
        p=p.rsplit('/',1)[0]
        yield p
def eff(p,var):
    if var in T.get(p,{}).get('vars',{}): return T[p]['vars'][var]
    for a in anc(p):
        if a in T and var in T[a]['vars']: return T[a]['vars'][var]
    return None
def cat(p):
    if p.startswith('/area'): return 'area'
    if p.startswith('/turf/closed'): return 'turf/closed'
    if p.startswith('/turf/open/space'): return 'turf/space'
    if p.startswith('/turf'): return 'turf/open'
    if p.startswith('/obj/effect/turf_decal'): return 'effect/turf_decal'
    if p.startswith('/obj/effect/mapping_helpers'): return 'effect/mapping_helpers'
    if p.startswith('/obj/effect/landmark'): return 'effect/landmark'
    if p.startswith('/obj/effect/spawner'): return 'effect/spawner'
    if p.startswith('/obj/effect'): return 'effect/other'
    if p.startswith('/obj/structure/cable') : return 'structure/cable'
    if p.startswith('/obj/machinery/atmospherics'): return 'machinery/atmos'
    if p.startswith('/obj/structure/disposalpipe'): return 'structure/disposalpipe'
    if p.startswith('/obj/structure'): return 'structure'
    if p.startswith('/obj/machinery'): return 'machinery'
    if p.startswith('/obj/item'): return 'item'
    if p.startswith('/obj'): return 'obj/other'
    if p.startswith('/mob'): return 'mob'
    return 'other'
if __name__=='__main__':
    s=summarize(ROOT+'/_maps/map_files/'+sys.argv[1])
    types=s['types']
    print(sys.argv[1],'uniq types',len(types))
    bycat=collections.defaultdict(lambda:[0,0])
    for p,c in types.items():
        b=bycat[cat(p)]; b[0]+=1; b[1]+=c
    for k,v in sorted(bycat.items(), key=lambda kv:-kv[1][0]): print(f'{k:26s} types={v[0]:5d} instances={v[1]}')
    # visual dedupe: (icon, icon_state) with directional stripped
    def vkey(p):
        ic=eff(p,'icon'); st=eff(p,'icon_state')
        return (ic,st)
    visual=collections.defaultdict(set)
    unresolved=collections.Counter()
    for p in types:
        c=cat(p)
        if c in ('area','turf/space'): continue
        ic,st=vkey(p)
        if ic is None or st is None: unresolved[c]+=1
        visual[c].add((ic,st))
    print('visual uniq (icon,state) per cat:',{k:len(v) for k,v in visual.items()})
    print('unresolved icon/state types per cat:',dict(unresolved))
    # pareto among 3D-relevant types (not area, not space turf, not decals/helpers/landmarks/spawner/cable?)
    excl=('area','turf/space','effect/mapping_helpers','effect/landmark')
    rel=[(p,c) for p,c in types.items() if cat(p) not in excl]
    rel.sort(key=lambda x:-x[1])
    tot=sum(c for _,c in rel)
    acc=0; marks={0.5:None,0.8:None,0.9:None,0.95:None,0.99:None}
    for i,(p,c) in enumerate(rel,1):
        acc+=c
        for m in marks:
            if marks[m] is None and acc>=m*tot: marks[m]=i
    print('pareto relevant types',len(rel),'instances',tot,marks)
    # singletons
    print('types with 1 instance',sum(1 for _,c in rel if c==1),'<=3',sum(1 for _,c in rel if c<=3),'<=10',sum(1 for _,c in rel if c<=10))
    # top 40
    for p,c in rel[:40]: print(c,p)
