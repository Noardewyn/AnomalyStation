"""Topology and UV revision, executed in the live Blender builder namespace.

Keep source files untouched. Replacement geometry is built before old parts are removed.
"""
import bpy
import bmesh
import json
from pathlib import Path


def activate(ns, key):
    a = ns['ASSETS'][key]
    ns.update(KEY=key, SCENE=a['scene'], ROOTOB=a['root'])
    bpy.context.window.scene = a['scene']
    ns['select'](a['source'], a['state'])
    return a


def components(mesh):
    adj = {v.index: set() for v in mesh.vertices}
    for e in mesh.edges:
        a, b = e.vertices
        adj[a].add(b); adj[b].add(a)
    remaining = set(adj); count = 0
    while remaining:
        count += 1; pending = [remaining.pop()]
        while pending:
            fresh = adj[pending.pop()] & remaining
            remaining -= fresh; pending.extend(fresh)
    return count


def fuse(ns, key, objects, name):
    """Exact solid union removes all interior faces; keep material UV seams."""
    if len(objects)<2: return objects[0] if objects else None
    a=activate(ns,key); target=objects[0]
    target.data=target.data.copy()
    for ob in objects:
        if ob!=target: ob.data=ob.data.copy()
        mats=[s.material for s in ob.material_slots]
        for i,mat in enumerate(mats):
            ob.material_slots[i].link='DATA';ob.data.materials[i]=mat
    bpy.ops.object.select_all(action='DESELECT')
    target.select_set(True);bpy.context.view_layer.objects.active=target
    for ob in objects[1:]:
        mod=target.modifiers.new('Remove_Internal_Interfaces','BOOLEAN')
        mod.operation=ns['G'].enum(mod,'operation','UNION')
        mod.solver=ns['G'].enum(mod,'solver','EXACT');mod.object=ob
        bpy.ops.object.modifier_apply(modifier=mod.name)
    # Boolean material indices can contain several slots referring to the same
    # atlas. Collapse those indices before simplifying coplanar topology.
    mats=list(target.data.materials);unique=[];remap={}
    for i,mat in enumerate(mats):
        if mat not in unique:unique.append(mat)
        remap[i]=unique.index(mat)
    indices=[remap[p.material_index] for p in target.data.polygons]
    target.data.materials.clear()
    for mat in unique:target.data.materials.append(mat)
    for p,i in zip(target.data.polygons,indices):p.material_index=i
    bm=bmesh.new();bm.from_mesh(target.data)
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0000001)
    bmesh.ops.dissolve_limit(bm,angle_limit=.001,verts=list(bm.verts),edges=list(bm.edges),delimit={'UV','MATERIAL'})
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    assert all(e.is_manifold for e in bm.edges),(key,name,'union edges')
    assert all(v.is_manifold for v in bm.verts),(key,name,'union vertices')
    bm.to_mesh(target.data);bm.free();target.data.update();target.data.calc_loop_triangles()
    for ob in objects[1:]:
        a['parts'].remove(ob);bpy.data.objects.remove(ob,do_unlink=True)
    target.name=key+'_'+name;target['part_name']=name
    target['connected_components']=components(target.data)
    return target


def revise_canister(ns,key):
    a=activate(ns,key);old=list(a['parts']);G=ns['G'];state=a['state']
    q=[(0,-8),(4,-8),(4,-7),(6,-7),(6,-6),(7,-6),(7,-4),(8,-4)]
    outline=[]
    for turn in range(4):
        for x,y in q:
            for _ in range(turn): x,y=-y,x
            outline.append((x,y))
    # Horizontal shoulders change size at a single elevation, so the silhouette
    # follows whole-pixel steps rather than bevelled or diagonal shortcuts.
    layers=[(.035,.6875),(.138,.6875),(.138,.8125),(.216,.8125),(.216,1),(.70,1),(.70,.875),(.79,.875),(.79,.75),(.85,.75)]
    m=G.Mesh();base=ns['sample']((13,18,20,26));const=G.uv(state,base[0]+.5,base[1]+.5)
    rings=[[(x*.03825*s,y*.032*s,z) for x,y in outline] for z,s in layers]
    m.face(rings[0],[const]*len(outline));m.face(rings[-1],[const]*len(outline))
    for level in range(len(rings)-1):
        for i in range(len(outline)):
            j=(i+1)%len(outline)
            pp=[rings[level][i],rings[level][j],rings[level+1][j],rings[level+1][i]]
            if level in (0,2,4):
                # Only the forward-facing central strip carries front artwork.
                # Other sides use an opaque source strip at identical density.
                front=outline[i][1]<=0 and outline[j][1]<=0
                coords=[G.uv(state,15.5+x/.03825,min(29,10+(.84-z)/.039)) for x,y,z in pp] if front else [G.uv(state,16.5,22.5)]*4
            else: coords=[const]*4
            m.face(pp,coords)
    body=ns['add'](m,'Stepped_Vessel')
    ns['profile']('Regulator_Housing',(11,6,21,11),lambda x,y,t:((x-16)*.036,-.15+t,1.01-(y-6)*.034),.28)
    steel=ns['sample']((10,3,22,6))
    ns['box']('Handle_Top',(-.25,.25,.10,.15,1.11,1.16),texel=steel)
    for sx in (-1,1): ns['box']('Handle_Post_'+str(sx),(sx*.225-.025,sx*.225+.025,.10,.15,.79,1.13),texel=steel)
    ns['box']('Valve_Control',(-.06,.06,-.19,-.145,.92,.99),texel=ns['sample']((12,5,21,10)),pivot=(0,-.16,.955))
    for sx in (-1,1): ns['box']('Foot_'+str(sx),(sx*.18-.04,sx*.18+.04,-.17,.17,0,.08),texel=base)
    new=[ob for ob in a['parts'] if ob not in old and ob.get('part_name')!='Valve_Control']
    shell=fuse(ns,key,new,'Vessel_And_Handle')
    assert components(shell.data)==1,(key,'floating housing')
    for ob in old:
        a['parts'].remove(ob);bpy.data.objects.remove(ob,do_unlink=True)
    a['root']['revision']=2
    a['description']='Stepped pressure vessel with continuous shoulder, regulator housing, carry handle and feet; separate valve'
    return {'shell_components':components(shell.data),'triangles':len(shell.data.loop_triangles)}


def oval_plan(w,d):
    quarter=[(0,-8),(4,-8),(4,-7),(6,-7),(6,-6),(7,-6),(7,-4),(8,-4)]
    result=[]
    for turn in range(4):
        for x,y in quarter:
            for _ in range(turn):x,y=-y,x
            result.append((x*w/16,y*d/16))
    return result


def ring_bowl(ns,name,layers,center=(0,0),texel=None,inside=None,oval=False):
    """One porcelain shell from bottom through rim to the lined bowl floor."""
    G=ns['G'];m=G.Mesh();c=texel or ns['sample']();d=inside or c
    rings=[[(x+center[0],y+center[1],z) for x,y in (oval_plan(w,h) if oval else G.stepped_rectangle(w,h,s))] for w,h,z,s in layers]
    uv=G.uv(G.STATE,c[0]+.5,c[1]+.5);inneruv=G.uv(G.STATE,d[0]+.5,d[1]+.5)
    m.face(rings[0],[uv]*len(rings[0]));m.face(rings[-1],[inneruv]*len(rings[-1]))
    for k,(ra,rb) in enumerate(zip(rings,rings[1:])):
        for i in range(len(ra)):
            j=(i+1)%len(ra);m.face([ra[i],ra[j],rb[j],rb[i]],[uv if k<len(rings)//2 else inneruv]*4)
    return ns['add'](m,name)


def remove_parts(a,parts):
    for ob in parts:
        a['parts'].remove(ob);bpy.data.objects.remove(ob,do_unlink=True)


def revise_sink(ns,key):
    a=activate(ns,key);old=[o for o in a['parts'] if o['part_name'] in ('Ceramic_Basin','Sculpted_Basin')]
    w,d=(.52,.40) if key=='washbasin' else (.76,.49)
    white=ns['sample']((10,5,21,8))
    shade=min(((x,y) for y in range(8,11) for x in range(12,20) if ns['G'].pixel(a['state'],x,y)[3]>.99),key=lambda p:sum(ns['G'].pixel(a['state'],*p)[:3]))
    ring_bowl(ns,'Sculpted_Basin',[(w-.12,d-.12,.68,.045),(w-.12,d-.12,.73,.045),
        (w-.06,d-.06,.73,.055),(w-.06,d-.06,.78,.055),(w,d,.78,.065),(w,d,.84,.065),
        (w-.07,d-.07,.84,.045),(w-.07,d-.07,.785,.045),
        (w-.14,d-.14,.785,.035),(w-.14,d-.14,.75,.035)],texel=white,inside=shade)
    remove_parts(a,old)
    for names,label in [(('Drain_Pipe','Wall_Drain_Elbow'),'Drain_Assembly'),(('Faucet_Riser','Faucet_Spout'),'Faucet_Assembly')]:
        ob=fuse(ns,key,[o for o in a['parts'] if o['part_name'] in names],label)
        if ob: assert components(ob.data)==1
    a['root']['revision']=2
    return {'parts':len(a['parts'])}


def revise_trolley_vessels(ns,key):
    a=activate(ns,key);G=ns['G'];state=a['state']
    old=[o for o in a['parts'] if o.get('part_name') in ('Left_Vessel','Right_Vessel')]
    for label,cx,tx in [('Left',-.224,9),('Right',.224,23)]:
        m=G.Mesh();c=G.uv(state,tx+.5,10.5)
        layers=[(.32,.29,.115),(.32,.29,.24),(.384,.34,.24),(.384,.34,.84),(.32,.29,.84),(.32,.29,.94)]
        rings=[[(x+cx,y,z) for x,y in G.stepped_rectangle(w,d,.064)] for w,d,z in layers]
        m.face(rings[0],[c]*12);m.face(rings[-1],[c]*12)
        for index,(ra,rb) in enumerate(zip(rings,rings[1:])):
            for i in range(12):
                j=(i+1)%12;pp=[ra[i],ra[j],rb[j],rb[i]]
                front=ra[i][1]<0 and ra[j][1]<0 and layers[index][2]!=layers[index+1][2]
                # The cylindrical cheek is not the transparent margin of the
                # sprite; keep its UV inside the opaque bottle body.
                lo,hi=(4,14.5) if label=='Left' else (18,26.5)
                uv=[G.uv(state,max(lo,min(hi,x/.032+16)),max(6,min(25.5,4+(.94-z)/.035))) for x,y,z in pp] if front else [c]*4
                m.face(pp,uv)
        ob=ns['add'](m,label+'_Vessel');assert components(ob.data)==1
    remove_parts(a,old)


def revise_static(ns,key):
    a=activate(ns,key)
    if key in ('water_tank','fuel_tank'):revise_trolley_vessels(ns,key)
    if key=='toilet':
        old=[o for o in a['parts'] if o.get('part_name') in ('Porcelain_Bowl','Seat_Lid')]
        white=ns['sample']((11,5,21,12))
        ring_bowl(ns,'Porcelain_Bowl',[(.34,.43,.24,0),(.34,.43,.33,0),(.46,.59,.33,0),(.46,.59,.45,0),
            (.37,.48,.45,0),(.37,.48,.385,0),(.25,.36,.385,0),(.25,.36,.305,0)],center=(0,-.045),texel=white,oval=True)
        m=ns['G'].Mesh();plan=[(x,y-.045) for x,y in oval_plan(.46,.59)]
        ns['G'].prism(m,plan,.449,.474,a['state'],white)
        ns['add'](m,'Seat_Lid',pivot=(0,.25,.46));remove_parts(a,old)
        next(o for o in a['parts'] if o.get('part_name')=='Bowl_Water').location.z+=.02
    if key.startswith('stationary_tank_'):
        shell=fuse(ns,key,[o for o in a['parts'] if not any(s in o.get('part_name','') for s in ('Viewing_Band','Pipe_Port'))],'Continuous_Tank_Shell')
        return {'shell_components':components(shell.data),'triangles':len(shell.data.loop_triangles)}
    if key=='station_bed' and not any(o.get('part_name','').startswith('Rail_Support') for o in a['parts']):
        for sx in (-1,1):
            for sy in (-1,1):ns['box']('Rail_Support_'+str(sx)+'_'+str(sy),(sx*.69-.018,sx*.69+.018,sy*.355-.025,sy*.355+.025,.39,.615),texel=ns['sample']((2,28,29,32)))
    if key=='urinal':
        ns['box']('Flush_Pipe_Socket',(-.025,.025,.04,.09,.94,1.06),texel=ns['sample']((9,7,23,29)))
    if key=='suit_storage_classic':
        ns['box']('Control_Mount',(.41,.47,-.14,.10,.69,1.23))
        body=[o for o in a['parts'] if o.get('part_name') in ('Base_Plinth','Top_Cap','Rear_Lining','Control_Housing','Control_Mount') or o.get('part_name','').startswith('Chamber_Side')]
        shell=fuse(ns,key,body,'Cabinet_Shell');assert components(shell.data)==1
        for sign in ('-1','1'):
            frame=[o for o in a['parts'] if o.get('part_name','').startswith('Door_Vertical_Rail_'+sign+'_') or o.get('part_name','').startswith('Door_Crossbar_'+sign+'_')]
            leaf=fuse(ns,key,frame,'Door_Frame_'+sign);assert components(leaf.data)==1
        return {'shell_components':components(shell.data)}
    groups={
        'storage_rack': [('Rack_Structure',lambda n:True)],
        'solar_panel':[('Pedestal_Assembly',lambda n:n in ('North_South_Foot','East_West_Foot','Pedestal'))],
        'solar_panel_frame':[('Pedestal_Assembly',lambda n:n in ('North_South_Foot','East_West_Foot','Pedestal'))],
        'wall_shower':[('Shower_Pipe_Assembly',lambda n:n in ('Wall_Valve_Box','Riser_Pipe','Upper_Elbow'))],
        'toilet':[('Porcelain_Body',lambda n:n in ('Pedestal','Porcelain_Bowl','Cistern'))],
        'urinal':[('Lined_Porcelain_Body',lambda n:n in ('Lined_Porcelain_Body','Ceramic_Frame','Recess_Back','Recess_Left','Recess_Right','Recess_Floor','Flush_Pipe','Flush_Pipe_Socket'))],
        'hydroponics_tray':[('Tray_And_Support_Frame',lambda n:not n.startswith('Control'))],
        'station_bed':[('Bed_Frame',lambda n:not any(s in n for s in ('Mattress','Pillow')))],
        'emergency_stretcher':[('Stretcher_Frame',lambda n:not any(s in n for s in ('Mattress','Pillow','Wheel','Caster')))],
        'water_tank':[('Trolley_Frame',lambda n:any(s in n for s in ('Trolley_Base','Frame_Post','Front_Crossbar')))],
        'fuel_tank':[('Trolley_Frame',lambda n:any(s in n for s in ('Trolley_Base','Frame_Post','Front_Crossbar')))],
        'pacman_generator':[('Generator_Casing',lambda n:not any(s in n for s in ('Access','Starter')))],
        'disposal_unit':[('Lined_Hopper_Housing',lambda n:not any(s in n for s in ('Lever','Indicator')))],
    }.get(key,[])
    report={}
    for name,predicate in groups:
        parts=[o for o in a['parts'] if predicate(o.get('part_name',''))]
        ob=fuse(ns,key,parts,name)
        if ob:report[name]={'components':components(ob.data),'triangles':len(ob.data.loop_triangles)}
    a['root']['revision']=2
    return report


def pixel_solid(ns, occupied, xs, ys, zs, texel, front_uv=True, transform=None):
    """Extract only the exterior of a rectilinear solid, then dissolve flat regions.

    Grid axes are sprite x, sprite y and physical depth. Neighbouring cells share
    vertices, including every depth transition. No intersecting interior boxes.
    """
    G = ns['G']; m = G.Mesh(); state = G.STATE
    constant = G.uv(state, texel[0]+.5, texel[1]+.5)
    floor = max(ys)
    def point(p):
        x, y, z = p
        if transform:return transform(xs[x],ys[y],zs[z])
        return ((xs[x]-16)*.05, zs[z], (floor-ys[y])*.05)
    directions = [((-1,0,0),[(0,0,0),(0,0,1),(0,1,1),(0,1,0)]),
                  ((1,0,0),[(1,0,0),(1,1,0),(1,1,1),(1,0,1)]),
                  ((0,-1,0),[(0,0,0),(1,0,0),(1,0,1),(0,0,1)]),
                  ((0,1,0),[(0,1,0),(0,1,1),(1,1,1),(1,1,0)]),
                  ((0,0,-1),[(0,0,0),(0,1,0),(1,1,0),(1,0,0)]),
                  ((0,0,1),[(0,0,1),(1,0,1),(1,1,1),(0,1,1)])]
    for x,y,z in sorted(occupied):
        for delta, corners in directions:
            if (x+delta[0],y+delta[1],z+delta[2]) in occupied: continue
            pp = [(x+a,y+b,z+c) for a,b,c in corners]
            coords = [G.uv(state,xs[a],ys[b]) for a,b,c in pp] if delta==(0,0,-1) and front_uv else [constant]*4
            m.face([point(p) for p in pp],coords)
    return m


def simplify_affine_surface(ob):
    """All coplanar faces in these constructed skins share one affine UV map."""
    ob.data=ob.data.copy();bm=bmesh.new();bm.from_mesh(ob.data)
    bmesh.ops.dissolve_limit(bm,angle_limit=.001,verts=list(bm.verts),edges=list(bm.edges),delimit=set())
    assert all(e.is_manifold for e in bm.edges)
    bm.to_mesh(ob.data);bm.free();ob.data.update();ob.data.calc_loop_triangles()


def revise_soil(ns,key):
    a=activate(ns,key);old=list(a['parts']);G=ns['G']
    cells={(x,y) for y in range(17,32) for x in range(32) if G.pixel(a['state'],x,y)[3]>.99}
    interior={p for p in cells if all((p[0]+dx,p[1]+dy) in cells for dx in range(-2,3) for dy in range(-2,3))}
    core={p for p in cells if all((p[0]+dx,p[1]+dy) in cells for dx in range(-4,5) for dy in range(-4,5))}
    occupied={(x,y,z) for x,y in cells for z in range(3) if z >= (0 if (x,y) in core else 1 if (x,y) in interior else 2)}
    mesh=pixel_solid(ns,occupied,list(range(33)),list(range(33)),[-.09,-.06,-.03,0],ns['sample']((5,20,26,29)),transform=lambda x,y,z:((x-16)*.028125,(24.5-y)*.06,-z))
    ob=ns['add'](mesh,'Terraced_Soil_Mound');simplify_affine_surface(ob)
    remove_parts(a,old);a['description']='Closed terraced soil mound following the original irregular pixel boundary'
    return {'components':components(ob.data),'triangles':len(ob.data.loop_triangles)}


def revise_vendor(ns, key):
    a=activate(ns,key); old=list(a['parts']); G=ns['G']; state=a['state']
    cells={(x,y) for y in range(32) for x in range(32) if G.pixel(state,x,y)[3]>.99}
    x0=min(x for x,y in cells); x1=max(x for x,y in cells)+1
    y1=max(y for x,y in cells)+1
    # Pixel-aligned recesses match the actual families in the source atlas.
    display=(x0+3,9,x1-6,25); delivery=(x0+4,27,x1-4,29)
    if state in ('cart','drug','med','sec'):
        display=(x0+2,9,x1-10,28); delivery=(x1-9,25,x1-2,28)
    elif state=='boozeomat':
        display=(3,11,18,28); delivery=(20,22,25,29)
    elif state=='modularpc':
        display=(x0+2,9,x1-3,23); delivery=(x0+10,27,x1-3,29)
    elif state in ('engivend','tool'):
        display=(x0+3,10,x1-10,24); delivery=(x0+4,27,x1-8,29)
    elif state in ('games','parts','nutri','seeds'):
        display=(x0+3,13,x1-7,24); delivery=(x0+5,27,x1-9,29)
    elif state=='coffee':
        display=(x0+5,11,x1-5,21); delivery=(x0+7,25,x1-7,27)
    elif state=='cigs':
        display=(x0+3,10,x1-6,21); delivery=(x0+3,26,x0+10,28)
    def inside(x,y,r): return r[0]<=x<r[2] and r[1]<=y<r[3]
    assert all((x,y) in cells for r in (display,delivery) for y in range(r[1],r[3]) for x in range(r[0],r[2])), state
    depth=[-.27,-.245,-.215,.27]
    xb={x for x,y in cells if (x-1,y) not in cells}|{x+1 for x,y in cells if (x+1,y) not in cells}
    yb={y for x,y in cells if (x,y-1) not in cells}|{y+1 for x,y in cells if (x,y+1) not in cells}
    xs=sorted(xb|{display[0],display[2],delivery[0],delivery[2]})
    ys=sorted(yb|{display[1],display[3],delivery[1],delivery[3]})
    filled={(ix,iy,z) for ix,x in enumerate(xs[:-1]) for iy,y in enumerate(ys[:-1]) for z in range(3)
            if (x,y) in cells and z >= (2 if inside(x,y,delivery) else 1 if inside(x,y,display) else 0)}
    # One connected skin includes all reveal walls and both recess backs.
    base=ns['sample']((x0+1,2,x1-1,6))
    shell=ns['add'](pixel_solid(ns,filled,xs,ys,depth,base),'Cabinet_Shell')
    simplify_affine_surface(shell)
    assert components(shell.data)==1
    shell['construction']='Continuous exterior skin with pixel-aligned recessed display and delivery pocket'
    shell['display_rect_px']=display; shell['delivery_rect_px']=delivery
    # Inset screen is a separate replaceable component seated against the recess.
    def panel(name,r,d,hinge=False):
        xx0,yy0,xx1,yy1=r
        return ns['profile'](name,r,lambda x,y,t:((x-16)*.05,d+t,(y1-y)*.05),.002,
            pivot=(((xx0+xx1)/2-16)*.05,d,(y1-yy0)*.05) if hinge else (0,0,0))
    panel('Inset_Display',display,-.248)
    panel('Delivery_Flap',delivery,-.241,True)
    light=ns['AUDIT']['mapped_vending'][state][0].get('light_mask')
    if light in ns['source']('vending')['states']:
        ns['select']('vendor_lights',state)
        # Each original lit texel sits at its actual panel depth. No luminous
        # sheet spanning the cavity and hiding its newly modelled reveal.
        for label,r,d in [('Display',display,-.2485),('Cabinet',(0,0,32,y1),-.2705)]:
            mask=lambda x,y: inside(x,y,display) if label=='Display' else not inside(x,y,display) and not inside(x,y,delivery)
            lit={(x,y) for y in range(r[1],r[3]) for x in range(r[0],r[2]) if mask(x,y) and G.pixel(state,x,y)[3]>.001}
            if not lit: continue
            # Greedy rectangular crops avoid introducing diagonal alpha geometry.
            while lit:
                x,y=min(lit,key=lambda p:(p[1],p[0])); end=x+1
                while (end,y) in lit: end+=1
                bottom=y+1
                while all((u,bottom) in lit for u in range(x,end)): bottom+=1
                lit.difference_update((u,v) for u in range(x,end) for v in range(y,bottom))
                ob=ns['decal']('Light_'+label,state,lambda u,v,t:((u-16)*.05,d-t,(y1-v)*.05),rect=(x,y,end,bottom),emission=.12)
                ob['original_light_mask']=light
    for ob in old:
        a['parts'].remove(ob); bpy.data.objects.remove(ob,do_unlink=True)
    a['root']['revision']=2
    a['description']='Continuous pixel-aligned cabinet shell with lined recesses, inset display and hinged delivery flap'
    return {'parts':len(a['parts']),'shell_components':components(shell.data),'shell_triangles':len(shell.data.loop_triangles)}


def run(ns, keys=None):
    report={}
    for key in keys or [k for k in ns['ASSETS'] if k.startswith('vending_')]:
        if key.startswith('vending_'): report[key]=revise_vendor(ns,key)
        elif key.startswith('portable_canister_'): report[key]=revise_canister(ns,key)
        elif key in ('washbasin','kitchen_sink'):report[key]=revise_sink(ns,key)
        elif key=='soil_bed':report[key]=revise_soil(ns,key)
        else:report[key]=revise_static(ns,key)
    print(json.dumps(report))
    return report


def clone_canister_variants(ns):
    template=ns['ASSETS']['portable_canister_generic']
    for key,a in ns['ASSETS'].items():
        if not key.startswith('portable_canister_') or key.endswith('_generic'):continue
        old=list(a['parts']);mat=ns['tile_material']('portable_gags',a['state'])
        for original in template['parts']:
            ob=original.copy();ob.name=key+'_'+original['part_name'];ob.parent=a['root']
            a['scene'].collection.objects.link(ob)
            for slot in ob.material_slots:slot.link='OBJECT';slot.material=mat
            a['parts'].append(ob)
        remove_parts(a,old);a['root']['revision']=2;a['description']=template['description']


def share_meshes(ns):
    cache={};shared=0
    for a in ns['ASSETS'].values():
        for ob in a['parts']:
            me=ob.data
            signature=(tuple(tuple(round(c,6) for c in v.co) for v in me.vertices),
                tuple((tuple(p.vertices),p.material_index) for p in me.polygons),
                tuple(tuple(round(c,6) for c in t.uv) for t in me.uv_layers.active.data),len(me.materials))
            if signature not in cache:cache[signature]=me;continue
            if cache[signature]==me:continue
            mats=[s.material for s in ob.material_slots];ob.data=cache[signature]
            for slot,mat in zip(ob.material_slots,mats):slot.link='OBJECT';slot.material=mat
            shared+=1
    print('Reused identical meshes:',shared)


def apply_all(ns):
    keys=[k for k in ns['ASSETS'] if not k.startswith('portable_canister_') or k=='portable_canister_generic']
    report=run(ns,keys);clone_canister_variants(ns);share_meshes(ns)
    (ns['OUT']/'topology_revision.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')


def revise_chairs(cns):
    from types import SimpleNamespace
    adapter={'ASSETS':cns['ASSETS'],'G':SimpleNamespace(enum=cns['enum']),'select':lambda *args:None}
    for key,a in cns['ASSETS'].items():
        a['source']='chairs'
        for ob in a['parts']:ob['part_name']=ob.name.split('.')[0]
        if key=='metal_stool':
            for sy in (-1,1):cns['box_part'](key,'Seat_Frame_Rail_'+str(sy),(-.24,.24,sy*.19-.025,sy*.19+.025,.36,.39),a['state'],(8,28))
            for sx in (-1,1):cns['box_part'](key,'Seat_Frame_Side_'+str(sx),(sx*.218-.025,sx*.218+.025,-.19,.19,.36,.39),a['state'],(8,28))
            for ob in a['parts']:ob['part_name']=ob.name.split('.')[0]
            groups=[('Welded_Stool_Frame',lambda n:n!='Seat')]
        elif key.startswith('office_chair_'):
            groups=[('Swivel_Seat_Assembly',lambda n:n in ('Seat','Backrest','Backrest_Support')),('Pedestal_And_Base',lambda n:n in ('Pedestal','Caster_Base'))]
        elif key=='comfy_chair':groups=[('Upholstered_Shell',lambda n:n!='Seat' and not n.startswith('Armrest'))]
        elif key=='bar_stool':groups=[('Pedestal_Assembly',lambda n:n in ('Pedestal','Pedestal_Foot')),('Rear_Loop',lambda n:n.startswith('Rear_Loop'))]
        else:groups=[('Wooden_Lower_Frame',lambda n:n!='Seat' and 'Backrest' not in n)]
        for name,predicate in groups:
            result=fuse(adapter,key,[o for o in a['parts'] if predicate(o['part_name'])],name)
            assert components(result.data)==1,(key,name,components(result.data))
        a['root']['revision']=2
    share_meshes(adapter)

