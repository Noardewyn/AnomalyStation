import re, sys, json, collections, os
ROOT='/Users/alexey/Desktop/projects/SpaceStation3D/tgstation'
def parse_dmm(path):
    text=open(path,encoding='utf-8',errors='replace').read()
    lines=text.split('\n')
    i=0;n=len(lines)
    keydefs={}   # key -> list of (path, varsblock)
    grids={}
    cells=collections.Counter()
    maxx=0; maxy=0
    while i<n:
        ln=lines[i]
        m=re.match(r'^"([A-Za-z]+)" = \($',ln)
        if m:
            k=m.group(1); i+=1
            ents=[]; cur=None
            while i<n:
                l=lines[i]
                if l.startswith('\t'):
                    if l.strip() in ('},','})'):
                        if cur is not None: ents.append(cur); cur=None
                        if l.strip()=='})': i+=1; break
                    else:
                        if cur is not None: cur[1].append(l.strip())
                else:
                    s=l.rstrip()
                    end=s.endswith(')') and not s.endswith('{')
                    s2=s.rstrip(',)').rstrip('{')
                    if s.endswith('{'):
                        cur=[s2,[]]
                    else:
                        ents.append([s2,[]])
                    if end: i+=1; break
                i+=1
            keydefs[k]=ents
            continue
        m=re.match(r'^\((\d+),(\d+),(\d+)\) = \{"$',ln)
        if m:
            x,y0,z=int(m.group(1)),int(m.group(2)),int(m.group(3)); i+=1
            col=[]
            while i<n and lines[i]!='"}':
                col.append(lines[i]); i+=1
            grids.setdefault(z,{})[(x,y0)]=col
            maxx=max(maxx,x); maxy=max(maxy,y0+len(col)-1)
            for k in col: cells[k]+=1
        i+=1
    return keydefs,grids,cells,maxx,maxy
def summarize(path):
    kd,g,c,mx,my=parse_dmm(path)
    zs=sorted(g.keys())
    types=collections.Counter(); variants=set()
    for k,cnt in c.items():
        for p,vs in kd.get(k,[]):
            types[p]+=cnt
            variants.add((p,tuple(vs)))
    return dict(path=os.path.relpath(path,ROOT),size=os.path.getsize(path),w=mx,h=my,z=zs,keys=len(kd),used_keys=len(c),tiles=sum(c.values()),uniq_types=len(types),uniq_variants=len(variants),types=types)
if __name__=='__main__':
    s=summarize(sys.argv[1]); t=s.pop('types'); print(s); print(t.most_common(8))
