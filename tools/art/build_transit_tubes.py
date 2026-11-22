"""Round modular transit system, source-audited ports and station mechanics."""
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
code=(ROOT/'tools/art/build_atmosphere_wave.py').read_text().split("start('portable_air_pump')")[0]
exec(compile(code.replace('machines/atmosphere_wave','transit_tubes'),'transit_helpers','exec'))
exec((ROOT/'tools/art/polish_perspective_meshes.py').read_text().split('def neutral_original_uv')[0])
N=12;R=.72;RI=.685;Z=.94
connections={}

def sweep(name,path,outer=R,inner=RI,angles=None,st='straight',rect=(13,16,1,1),transparent=True,end_tangents=None):
    """Closed thick shell, open circular ports; no single-sided torn edges."""
    b=Builder('transit',st);angles=angles or [j*math.tau/N for j in range(N+1)];closed=abs(angles[-1]-angles[0]-math.tau)<1e-5
    if closed:angles=angles[:-1]
    count=len(angles);steps=len(path);uv=uvrect('transit',st,rect);uvpin=[((uv[0][0]+uv[2][0])/2,(uv[0][1]+uv[2][1])/2)]*4
    for radius in [outer,inner]:
        for i,point in enumerate(path):
            tangent=Vector(path[min(i+1,steps-1)])-Vector(path[max(i-1,0)])
            if end_tangents and i in [0,steps-1]:tangent=Vector(end_tangents[0 if i==0 else 1])
            tangent.normalize();side=Vector((-tangent.y,tangent.x,0));p=Vector(point)
            for a in angles:b.v.append(tuple(p+side*(radius*math.cos(a))+Vector((0,0,radius*math.sin(a)))))
    def quad(ids):b.f.append(ids);b.uv.append(uvpin)
    for layer in range(2):
        base=layer*steps*count
        for i in range(steps-1):
            for j in range(count if closed else count-1):
                q=(j+1)%count;ids=[base+i*count+j,base+i*count+q,base+(i+1)*count+q,base+(i+1)*count+j];quad(ids if layer==0 else ids[::-1])
    for i in [0,steps-1]:
        for j in range(count if closed else count-1):
            q=(j+1)%count;quad([i*count+j,i*count+q,steps*count+i*count+q,steps*count+i*count+j])
    if not closed:
        for j in [0,count-1]:
            for i in range(steps-1):quad([i*count+j,(i+1)*count+j,steps*count+(i+1)*count+j,steps*count+i*count+j])
    ob=b.finish(name)
    if transparent:alpha(ob,.23)
    return ob

def collar(name,p,tangent,outer=.75,inner=.684):
    t=Vector(tangent).normalized();p=Vector(p)
    return sweep(name,[tuple(p-t*.018),tuple(p+t*.018)],outer,inner,st='closed_station0',rect=(8,7,1,1),transparent=False)

def ports(name,entries):
    connections[name]=[]
    for label,p,d in entries:
        ob=anchor('Port_'+label,p);ob['outward_blender']=list(d);ob['inner_radius_m']=RI
        connections[name].append({'direction':label,'position_blender':p,'outward_blender':d})
    root['module_pitch_m']=2.;root['tube_center_height_m']=Z;root['tube_inner_radius_m']=RI

def bezier(points,n=6):
    a,b,c,d=[Vector(p) for p in points]
    return [tuple((1-t)**3*a+3*(1-t)**2*t*b+3*(1-t)*t*t*c+t**3*d) for t in [i/n for i in range(n+1)]]

for diagonal in [False,True]:
    for crossing in [False,True]:
        name='tube_'+('diagonal' if diagonal else 'straight')+('_crossing' if crossing else '');start(name)
        a=(-1,-1,Z) if diagonal else (0,-1,Z);z=(1,1,Z) if diagonal else (0,1,Z);t=Vector(z)-Vector(a)
        sweep('Round_Glass_Shell',[a,z]);collar('Start_Collar',Vector(a)+t.normalized()*.035,t);collar('End_Collar',Vector(z)-t.normalized()*.035,t)
        if crossing:
            # Source crossing remains a two-port tube; markings indicate density=FALSE.
            for offset in [-.23,.23]:collar('Crossing_Marker_'+str(offset),Vector((0,0,Z))+t.normalized()*offset,t,outer=.727,inner=.721)
        ports(name,[('SW' if diagonal else 'S',a,tuple(-t.normalized())),('NE' if diagonal else 'N',z,tuple(t.normalized()))])
        root['source_density']=not crossing
        done(name,'12-sided round, thick transparent shell; exact 2m cardinal or 2sqrt(2)m diagonal pitch. '+('Source crossing is walkable density=FALSE with two ports, not a four-way routing junction. Collision handling must honour source passability.' if crossing else 'Rotate cardinal directions; diagonal end centers lie on cell corners and use diagonal neighbour spacing.'))

for sign,label in [(-1,'left'),(1,'right')]:
    name='tube_curve_'+label;start(name);path=bezier([(sign,-1,Z),(sign*.42,-.42,Z),(0,.35,Z),(0,1,Z)])
    sweep('Curved_Glass_Shell',path,end_tangents=[(-sign,1,0),(0,1,0)]);collar('Diagonal_Collar',Vector(path[0])+Vector((-sign,1,0)).normalized()*.035,(-sign,1,0));collar('North_Collar',(0,.965,Z),(0,1,0))
    ports(name,[('SW' if sign<0 else 'SE',path[0],(sign/math.sqrt(2),-1/math.sqrt(2),0)),('N',path[-1],(0,1,0))])
    done(name,'45-degree cardinal-to-diagonal transition reconstructed as a curved round shell. Mirrored curves have different connection handedness; each may be rotated. No stepped square pixel outline.')

def solid_branches(radius):
    b=Builder('transit','straight')
    for end in [(0,1,Z),(-1,-1,Z),(1,-1,Z)]:
        p=Vector((0,0,Z));q=Vector(end);t=(q-p).normalized();side=Vector((-t.y,t.x,0));base=len(b.v)
        for point in [p-t*.28,q+t*.025]:
            for j in range(N):b.v.append(tuple(point+side*(radius*math.cos(j*math.tau/N))+Vector((0,0,radius*math.sin(j*math.tau/N)))))
        uv=uvrect('transit','straight',(13,16,1,1));pin=((uv[0][0]+uv[2][0])/2,(uv[0][1]+uv[2][1])/2)
        for j in range(N):k=(j+1)%N;b.f.append([base+j,base+k,base+N+k,base+N+j]);b.uv.append([pin]*4)
        for ids in [list(range(N))[::-1],list(range(N,2*N))]:b.f.append([base+i for i in ids]);b.uv.append([pin]*N)
    ob=b.finish('Junction_Volume');union_rigid_components(ob);return ob

start('tube_junction');outer=solid_branches(R);inner=solid_branches(RI)
for ob in scene.objects:ob.select_set(False)
outer.select_set(True);bpy.context.view_layer.objects.active=outer
modifier=outer.modifiers.new('Hollow_Connected_Interior','BOOLEAN');modifier.operation='DIFFERENCE';modifier.object=inner;modifier.solver='EXACT';bpy.ops.object.modifier_apply(modifier=modifier.name)
parts.remove(inner);bpy.data.objects.remove(inner,do_unlink=True);outer.name='Y_Junction_Glass';alpha(outer,.23)
# Cut faces at the exact modular end planes; branch volumes extend only for Boolean subtraction.
bm=bmesh.new();bm.from_mesh(outer.data)
for p,t in [((0,1,Z),(0,1,0)),((-1,-1,Z),(-1,-1,0)),((1,-1,Z),(1,-1,0))]:
    bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),plane_co=Vector(p),plane_no=Vector(t).normalized(),clear_outer=True,clear_inner=False,dist=1e-6)
# Bridge outer/inner edge rings into annular end faces; do not cap the travel bore.
for p,t in [((0,1,Z),(0,1,0)),((-1,-1,Z),(-1,-1,0)),((1,-1,Z),(1,-1,0))]:
    origin=Vector(p);normal=Vector(t).normalized()
    edges=[e for e in bm.edges if e.is_boundary and all(abs((v.co-origin).dot(normal))<1e-5 for v in e.verts)]
    if edges:bmesh.ops.bridge_loops(bm,edges=edges)
layer=bm.loops.layers.uv.active;uv=uvrect('transit','straight',(13,16,1,1));pin=((uv[0][0]+uv[2][0])/2,(uv[0][1]+uv[2][1])/2)
for f in bm.faces:
    for loop in f.loops:loop[layer].uv=pin
bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-5)
for _ in range(3):
    bmesh.ops.dissolve_degenerate(bm,dist=1e-5,edges=list(bm.edges))
    bmesh.ops.dissolve_limit(bm,angle_limit=1e-5,use_dissolve_boundaries=False,verts=list(bm.verts),edges=list(bm.edges),delimit={'UV','MATERIAL','NORMAL'})
    bmesh.ops.triangulate(bm,faces=list(bm.faces),ngon_method='EAR_CLIP')
for _ in range(24):
    tiny=[f for f in bm.faces if len(f.verts)==3 and (f.verts[1].co-f.verts[0].co).cross(f.verts[2].co-f.verts[0].co).length<2e-10]
    if not tiny:break
    for face in tiny:
        if face.is_valid:bmesh.ops.rotate_edges(bm,edges=[max(face.edges,key=lambda e:e.calc_length())],use_ccw=False)
bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
bm.to_mesh(outer.data);bm.free();outer.data.calc_loop_triangles()
for label,p,t in [('N',(0,1,Z),(0,1,0)),('SW',(-1,-1,Z),(-1,-1,0)),('SE',(1,-1,Z),(1,-1,0))]:collar(label+'_Collar',Vector(p)-Vector(t).normalized()*.035,t)
ports('tube_junction',[('N',(0,1,Z),(0,1,0)),('SW',(-1,-1,Z),(-.70710678,-.70710678,0)),('SE',(1,-1,Z),(.70710678,-.70710678,0))])
root['source_default_priority']='SW';root['source_flipped_priority']='SE'
for sign,label in [(-1,'left'),(1,'right')]:
    b=Builder('transit','junction0' if sign<0 else 'junction1');b.box((sign*.29-.10,sign*.29+.10,-.27,-.07,1.668,1.674),(23,5,5,5),(23 if sign<0 else 5,5,5,5),(13,16,1,1));ob=b.finish('Route_Arrow_'+label);ob.scale=(1,1,1) if sign<0 else (0,0,0);state('prefer_'+label,ob,True);state('prefer_'+('right' if sign<0 else 'left'),ob,False)
done('tube_junction','True three-way connected hollow Y-shell with Boolean-united walls, no intersecting glass inside the fork. N/SW/SE ports. Source junction0/1 use the same geometry and opposite preferred exits, exposed as prefer_left/right state clips; gameplay selects exit direction.')

for terminal in [False,True]:
    for dispenser in [False,True]:
        name='tube_'+('terminus' if terminal else 'station')+('_dispenser' if dispenser else '');start(name)
        st=('open_terminusdispenser0' if terminal else 'open_dispenser0') if dispenser else ('closed_terminus0' if terminal else 'closed_station0')
        # End sleeves plus upper/lower arcs leave finished side boarding apertures.
        sweep('Left_End_Sleeve',[(-1,0,Z),(-.66,0,Z)])
        sweep('Right_End_Sleeve',[(.66,0,Z),(1,0,Z)])
        for label,a0,a1 in [('Upper',math.pi/3,2*math.pi/3),('Lower',4*math.pi/3,5*math.pi/3)]:
            sweep(label+'_Shell',[(-.66,0,Z),(.66,0,Z)],angles=[a0+(a1-a0)*j/2 for j in range(3)])
        for x in [-.965,.965]:collar('End_Rim_'+str(x),(x,0,Z),(1,0,0))
        b=Builder('transit',st)
        for x in [-.72,.58]:b.box((x,x+.14,-.51,.51,.03,.34),(8,7,1,1),side=(8,7,1,1))
        b.finish('Station_Supports')
        for sign,label in [(-1,'front'),(1,'back')]:
            a0,a1=(2*math.pi/3,4*math.pi/3) if sign<0 else (-math.pi/3,math.pi/3)
            door=sweep('Boarding_Door_'+label,[(-.65,0,Z),(.65,0,Z)],outer=.751,inner=.729,angles=[a0+(a1-a0)*j/4 for j in range(5)],st=st,rect=(12,14,1,1),transparent=not dispenser)
            # Pivot on tube axis, keeping the rolled door entirely below the ceiling.
            for v in door.data.vertices:v.co.z-=Z
            door.location.z=Z;angle=sign*math.radians(120)
            if dispenser:
                # Source dispenser stays open; it does not run close_animation().
                state('boarding_'+label,door,False);state('boarding_'+('back' if label=='front' else 'front'),door,True)
                if label=='front':door.scale=(0,0,0);base_poses[door]['scale']=door.scale.copy()
            else:
                motion('open_'+label,door,'rotation_euler',[(0,0,0),(angle,0,0)],.6)
                motion('close_'+label,door,'rotation_euler',[(angle,0,0),(0,0,0)],.6)
            anchor('Boarding_'+label,(0,sign*.98,.22))
        if terminal:
            b=Builder('transit',st);profile(b,[(0,.735),(.06,.735)],(28,11,1,1),axis='Y',n=N)
            b.v=[(-.99-v[1],v[0],Z+v[2]) for v in b.v];b.finish('Terminal_End_Cap')
        if dispenser:
            b=Builder('transit',st);b.box((-.20,.20,-.20,.20,.245,.28),(14,15,4,4),(14,15,4,4),(8,7,1,1));b.finish('Pod_Materializer')
        entries=[('E',(1,0,Z),(1,0,0))]
        if not terminal:entries.append(('W',(-1,0,Z),(-1,0,0)))
        ports(name,entries);anchor('Pod_Dock',(0,0,Z));root['reverse_launch']=terminal;root['dispenser']=dispenser
        done(name,('Source dispenser stays open, spawns temporary pod on boarding and deletes arriving pod. Two boarding-side state choices, no invented closing cycle. ' if dispenser else 'Two separately animated boarding-side covers for normal/flipped placement. Original open/close duration .6 seconds; runtime selects boarding side and five-second departure cooldown. ')+('Single east port and physical end cap; reverse-launch terminus. ' if terminal else 'Through station, east/west ports. ')+'Round 12-sided bore; geometry floor origin and Pod_Dock at tube axis.')

for temporary in [False,True]:
    name='transit_pod_temporary' if temporary else 'transit_pod';st='temppod' if temporary else 'pod';start(name)
    # Compact capsule axis Y, sized to fit the .685m bore, occupant attached inside.
    sweep('Pod_Glass_Capsule',[(0,-.48,Z),(0,.48,Z)],outer=.56,inner=.53,st=st,rect=(13,15,1,1),transparent=not temporary)
    for y in [-.48,.48]:
        b=Builder('transit',st);profile(b,[(0,.568),(.035,.568)],(10,6,1,1) if temporary else (12,9,1,1),axis='Y',center=(0,y+.0175,Z),n=N);b.finish('Pod_End_'+str(y))
    b=Builder('transit',st);b.box((-.30,.30,-.29,.25,.43,.52),(11,26,10,2),(11,26,10,2),(11,26,1,1));b.box((-.30,.30,.20,.27,.52,1.08),(11,5,10,3),side=(11,5,1,1));b.finish('Pod_Seat')
    choices('Occupancy_Light','transit',[st,st+'_occupied'],(-.10,.10,-.575,-.56,.95,1.06),(12,3,7,2))
    anchor('Occupant',(0,0,.53));root['path_axis']='Blender +Y';root['path_center_local_blender']=[0,0,Z]
    if temporary:
        b=Builder('transit',st);b.box((-.04,.04,-.45,.45,1.507,1.511),(15,9,2,15),(15,9,2,15),(10,6,1,1));b.finish('Hazard_Seam')
        for ob in list(parts):
            state('materialized',ob,True);state('dissolved',ob,False)
    done(name,'Round capsule fits tube bore; separate occupant socket and original empty/occupied indicator states. Move the root along the network at floor-origin height, orient +Y along travel. '+('Source temporary pod materializes at dispenser and is deleted on station arrival: materialized/dissolved visibility clips supplied.' if temporary else 'Reusable pod, deconstructed into the same unattached model; contents are runtime actors, not painted onto glass.'))

(OUT/'connections.json').write_text(json.dumps(connections,indent=2))
tail=(ROOT/'tools/art/build_medical_wave.py').read_text().split("preview=bpy.data.scenes.new('Medical_Wave_Review')")[1]
tail=tail.replace('Medical','Transit_Tubes').replace('medical','transit_tubes').replace('(index%6)*3','(index%5)*3.4').replace('(index//6)*3.2','(index//5)*3.4').replace('Vector((7.5,4.8,.65))','Vector((6.8,3.4,.8))')
exec("preview=bpy.data.scenes.new('Transit_Tubes_Review')"+tail)
