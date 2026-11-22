"""Validate exported shell seams on every occupied 4x3 grid (4095 layouts)."""
import json, struct, math
from pathlib import Path
from collections import Counter
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'godot/assets/models/station_props/stationary_tanks/rounded'
info=json.loads((OUT/'model_info.json').read_text())
def turn(mask,steps):
    for _ in range(steps%4):mask=sum(b for a,b in [(4,1),(1,8),(8,2),(2,4),(16,128),(128,64),(64,32),(32,16)] if mask&a)
    return mask
def read(path):
    b=path.read_bytes();assert struct.unpack_from('<III',b)==(0x46546c67,2,len(b))
    n=struct.unpack_from('<I',b,12)[0];d=json.loads(b[20:20+n]);binary=b[28+n:]
    def acc(i):
        a=d['accessors'][i];v=d['bufferViews'][a['bufferView']];count={'SCALAR':1,'VEC3':3,'VEC2':2}[a['type']];fmt={5123:'H',5125:'I',5126:'f'}[a['componentType']];size=struct.calcsize(fmt)*count
        return [struct.unpack_from('<'+fmt*count,binary,v.get('byteOffset',0)+a.get('byteOffset',0)+j*v.get('byteStride',size)) for j in range(a['count'])]
    node=next(n for n in d['nodes'] if n.get('name','').startswith('Pressure_Shell'))
    prim=d['meshes'][node['mesh']]['primitives'][0]
    # glTF Y-up back to Blender; shell nodes have identity transform.
    points=[(x,-z,y) for x,y,z in acc(prim['attributes']['POSITION'])];ind=[i[0] for i in acc(prim['indices'])]
    edges=Counter();areas=[]
    for a,b,c in zip(ind[0::3],ind[1::3],ind[2::3]):
        p,q,r=points[a],points[b],points[c]
        u=[q[i]-p[i] for i in range(3)];v=[r[i]-p[i] for i in range(3)];cross=[u[1]*v[2]-u[2]*v[1],u[2]*v[0]-u[0]*v[2],u[0]*v[1]-u[1]*v[0]]
        assert sum(c*c for c in cross)>1e-15,(path,'degenerate triangle')
        for a,b in [(a,b),(b,c),(c,a)]:edges[tuple(sorted(tuple(round(v,6) for v in points[i]) for i in [a,b]))]+=1
    assert all(n<=2 for n in edges.values()),(path,'nonmanifold')
    return [e for e,n in edges.items() if n==1],len(points)
boundaries={};reports=[]
for m in info['modules']:
    edges,gpu=read(OUT/m['file']);assert m['vertices']<=500
    for state in m['states']:
        turns=next(t for t in range(4) if turn(m['canonical'],t)==state)
        # Four-way center has no rule rotation; its file already carries exact state.
        if state&15==15:turns=0
        def rotate(p):
            x,y,z=p
            for _ in range(turns):x,y=-y,x
            return x,y,z
        rotated=[tuple(rotate(p) for p in e) for e in edges]
        for bit,axis,sign in [(4,0,1),(8,0,-1),(1,1,1),(2,1,-1)]:
            own=[e for e in rotated if all(abs(p[axis]-sign)<1e-5 for p in e)]
            assert bool(own)==bool(state&bit),(state,bit,'missing or unexpected opening')
            boundaries[state,bit]=set(tuple(sorted((round(p[1-axis],5),round(p[2],5)) for p in e)) for e in own)
        assert all(any(all(abs(p[axis]-sign)<1e-5 for p in e) and state&bit for bit,axis,sign in [(4,0,1),(8,0,-1),(1,1,1),(2,1,-1)]) for e in rotated),(state,'unplanned hole')
    reports.append(dict(file=m['file'],vertices=m['vertices'],triangles=m['triangles'],shell_gpu_vertices=gpu))
def mask_at(cells,x,y):
    m=sum(b for b,dx,dy in [(4,1,0),(8,-1,0),(1,0,1),(2,0,-1)] if (x+dx,y+dy) in cells)
    for b,dx,dy,a,c in [(16,1,1,1,4),(128,-1,1,1,8),(32,1,-1,2,4),(64,-1,-1,2,8)]:
        if m&a and m&c and (x+dx,y+dy) in cells:m|=b
    return m
checks=0
for bits in range(1,1<<12):
    cells={(i%4,i//4) for i in range(12) if bits&(1<<i)}
    masks={(x,y):mask_at(cells,x,y) for x,y in cells}
    for (x,y),m in masks.items():
        for dx,dy,side,other in [(1,0,4,8),(0,1,1,2)]:
            if (x+dx,y+dy) in cells:
                assert boundaries[m,side]==boundaries[masks[x+dx,y+dy],other],(bits,(x,y),m,masks[x+dx,y+dy],side)
                checks+=1
report=dict(status='passed',layouts=4095,shared_edge_checks=checks,states=47,modules=reports)
(OUT/'validation.json').write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k!='modules'}))
