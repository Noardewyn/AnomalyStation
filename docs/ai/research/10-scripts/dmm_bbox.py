import os, sys, collections
from dmm_parse import parse_dmm, ROOT
def bbox(path):
    kd,g,c,mx,my=parse_dmm(path)
    def isspace(k):
        paths=[p for p,_ in kd.get(k,[])]
        return all(p.startswith('/turf/open/space') or p.startswith('/area/space') or p.startswith('/turf/open/openspace') or p.startswith('/area/misc/') for p in paths) 
    def isstation(k):
        return any(p.startswith('/area/station') for p,_ in kd.get(k,[]))
    out={}
    for z,cols in sorted(g.items()):
        xs=[];ys=[];tiles=0;stationtiles=0;nonspace=0
        for (x,y0),col in cols.items():
            H=len(col)
            for idx,k in enumerate(col):
                y=y0+H-1-idx
                if not isspace(k):
                    nonspace+=1; xs.append(x); ys.append(y)
                if isstation(k): stationtiles+=1
        if xs: out[z]=dict(bbox=(min(xs),max(xs),min(ys),max(ys)),w=max(xs)-min(xs)+1,h=max(ys)-min(ys)+1,nonspace=nonspace,stationtiles=stationtiles)
        else: out[z]=dict(nonspace=0)
    return out
if __name__=='__main__':
    for n in ['MetaStation/MetaStation.dmm','Deltastation/DeltaStation2.dmm','IceBoxStation/IceBoxStation.dmm','KiloStation/KiloStation.dmm','tramstation/tramstation.dmm','NebulaStation/NebulaStation.dmm','CatwalkStation/CatwalkStation_2023.dmm','Mining/Lavaland.dmm','generic/CentCom.dmm','debug/runtimestation.dmm']:
        p=ROOT+'/_maps/map_files/'+n
        print(n, bbox(p))
