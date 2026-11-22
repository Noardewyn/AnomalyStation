import json, collections, re
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
def children(p):
    pre=p+'/'
    return [k for k in T if k.startswith(pre)]
roots=['/obj/item','/obj/structure','/obj/machinery','/obj/effect','/obj','/mob/living/carbon/human','/mob/living/basic','/mob/living/simple_animal','/mob/living/silicon','/mob/living/carbon','/mob/living','/mob','/turf','/turf/open/floor','/turf/closed/wall','/area','/obj/item/clothing','/obj/item/gun','/obj/item/food','/obj/item/reagent_containers','/obj/item/stack','/obj/item/storage','/obj/item/mod','/obj/item/organ','/obj/item/bodypart','/obj/vehicle','/obj/projectile','/obj/docking_port','/obj/structure/closet','/obj/machinery/computer','/obj/item/circuitboard','/obj/item/toy','/obj/item/weapon' ]
print('total types',len(T))
for r in roots:
    ch=[k for k in T if k==r or k.startswith(r+'/')]
    withstate=[k for k in ch if eff(k,'icon_state') is not None]
    ownstate=[k for k in ch if 'icon_state' in T[k]['vars']]
    pairs=set((eff(k,'icon'),eff(k,'icon_state')) for k in withstate)
    # leaf types: no children
    leaves=[k for k in ch if not any(o.startswith(k+'/') for o in ch)]
    abstract=[k for k in ch if T[k]['abstract']]
    print(f'{r:42s} types={len(ch):5d} leaves={len(leaves):5d} eff_icon_state={len(withstate):5d} own={len(ownstate):5d} uniq(icon,state)={len(pairs):5d} abstract={len(abstract)}')
# top level roots
c=collections.Counter(k.split('/')[1] for k in T)
print(c.most_common(15))
c2=collections.Counter('/'.join(k.split('/')[1:3]) for k in T if k.startswith('/obj') or k.startswith('/mob') or k.startswith('/turf'))
print(c2.most_common(30))
