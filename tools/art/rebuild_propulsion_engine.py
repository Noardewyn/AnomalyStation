"""Wall-mounted propulsion correction. Execute inside interactive Blender via MCP.
Reuses the original atlas/export helpers, without rebuilding the other furniture.
"""
from pathlib import Path
import os
helper = Path(__file__).with_name('build_shuttle_furniture.py')
exec(compile(helper.read_text(encoding='utf-8').split('# Seating modules:')[0], str(helper), 'exec'))

for suffix, state in [('', 'propulsion'), ('_left', 'propulsion_l'), ('_right', 'propulsion_r')]:
    name = 'propulsion_engine' + suffix
    start(name)
    b = Builder('shuttle', state)
    rings = [(.8,.88),(.52,.88),(.48,.67),(.20,.67),(.16,.83),(-.42,.83),(-.48,.70),(-.72,.57),(-.80,.43),(-.82,.31),(-.60,.31)]
    angles = [math.tau*j/8 + math.pi/8 for j in range(8)]
    for y, radius in rings:
        b.v.extend([(radius*math.cos(a), y, 1+radius*math.sin(a)) for a in angles])
    # Opaque, restrained source regions. No exhaust blue or transparent sprite margins.
    bands = [(9,4,14,2), (10,8,12,2), (11,10,10,3), (10,13,12,2),
             (9,16,14,3), (9,21,14,2), (10,23,12,2), (11,25,10,1),
             (12,27,8,1), (1.5,15.5,0,0)]
    for k in range(len(rings)-1):
        for j in range(8):
            b.f.append([k*8+j,k*8+(j+1)%8,(k+1)*8+(j+1)%8,(k+1)*8+j])
            # Repeat the same original casing band around all eight sectors.
            rect = bands[k]
            b.uv.append(uvrect('shuttle',state,rect))
    cap=list(range((len(rings)-1)*8,len(rings)*8))
    b.f.append(cap)
    b.uv.append([uvrect('shuttle',state,(1.5,15.5,0,0))[0]]*8)
    # Exact 2 x 2 m plate with a real opening joined to the casing; no hidden
    # overlapping disk at the connection. Back plane is at the tile's rear edge.
    outer=len(b.v)
    for y in (.8, 1.0):
        for a in angles:
            x,z=math.cos(a),math.sin(a); q=max(abs(x),abs(z))
            b.v.append((x/q,y,1+z/q))
    # Add square corners as well as radial points so the plate is truly square.
    # Replace the eight radial outer points with corners / edge midpoints.
    # Match winding and angular order to casing (22.5, 67.5, ... degrees).
    perimeter=[(1,0),(1,1),(0,1),(-1,1),(-1,0),(-1,-1),(0,-1),(1,-1)]
    for layer,y in enumerate((.8,1.0)):
        for j,(x,z) in enumerate(perimeter):b.v[outer+layer*8+j]=(x,y,1+z)
    W,H=meta['shuttle']['sheet']
    def plate_uv(face):
        # Continuous planar projection of the original metal panel tile.
        return [((257+(b.v[i][0]+1)*15)/W,1-(321+(2-b.v[i][2])*15)/H) for i in face]
    for j in range(8):
        nxt=(j+1)%8
        face=[j,nxt,outer+nxt,outer+j]
        b.f.append(face);b.uv.append(plate_uv(face))
        face=[outer+j,outer+nxt,outer+8+nxt,outer+8+j]
        b.f.append(face);b.uv.append(uvrect('shuttle',state,(9,4,14,2)))
    face=list(range(outer+8,outer+16))
    b.f.append(face);b.uv.append(plate_uv(face))
    ob=b.finish('Engine')
    # Separate closed energy insert, recessed and clear of nozzle walls and cap.
    e=Builder('shuttle',state)
    for y in (-.69,-.66):
        e.v.extend([(.285*math.cos(a),y,1+.285*math.sin(a)) for a in angles])
    e.f=[list(range(8)),list(range(8,16))]
    e.f.extend([[j,(j+1)%8,8+(j+1)%8,8+j] for j in range(8)])
    im=next(n.image for n in materials['shuttle'].node_tree.nodes if n.type=='TEX_IMAGE')
    pixels=list(im.pixels);r=meta['shuttle']['states'][state]['rects'][0]
    choices=[]
    for yy in range(350,min(H,424)):
        for xx in range(0,min(W,320)):
            p=pixels[((H-1-yy)*W+xx)*4:((H-1-yy)*W+xx)*4+4]
            if p[3]>.1 and p[2]>p[0]+.05: choices.append((p[2]-p[0],xx,yy))
    _,xx,yy=max(choices)
    uv=((xx+.5)/W,1-(yy+.5)/H)
    e.uv=[[uv]*len(f) for f in e.f]
    energy=e.finish('Energy_Core')
    energy['active_only']=True
    energy['purpose']='Separate energy insert; hide when inactive; Effect_UV is normalized for an animated material.'
    layer=energy.data.uv_layers.new(name='Effect_UV')
    for loop in energy.data.loops:
        v=energy.data.vertices[loop.vertex_index].co
        layer.data[loop.index].uv=(v.x/.57+.5,(v.z-1)/.57+.5)
    energy.data.uv_layers.active_index=0
    mat=materials['shuttle'].copy();mat.name='Engine_Energy'
    bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    tex=next(n for n in mat.node_tree.nodes if n.type=='TEX_IMAGE')
    mat.node_tree.links.new(tex.outputs['Color'],bs.inputs['Emission Color'])
    bs.inputs['Emission Strength'].default_value=2.0
    energy.data.materials.clear();energy.data.materials.append(mat)
    root['grid_pitch_m']=2.0
    root['mount_plate_m']=[2.0,2.0,.2]
    root['axis_height_m']=1.0
    save(name,'Wall plate 2x2 m; symmetric casing UVs; original metal panel. Separate active-only Energy_Core with Effect_UV.')
    if not suffix:
        review_scene=scene

# Service output is outside the game repository.
review=Path(os.environ['TEMP'])/'station_art_propulsion';review.mkdir(exist_ok=True)
(review/'geometry.json').write_text(json.dumps(report,indent=2))
bpy.context.window.scene=review_scene
review_scene.render.engine='BLENDER_EEVEE'
review_scene.world=bpy.data.worlds.new('PropulsionReviewWorld')
review_scene.world.color=(.16,.16,.16)
review_scene.render.resolution_x=900;review_scene.render.resolution_y=900;review_scene.render.resolution_percentage=100
cam_data=bpy.data.cameras.new('ReviewCamera');cam=bpy.data.objects.new('ReviewCamera',cam_data);review_scene.collection.objects.link(cam);review_scene.camera=cam
cam_data.type='ORTHO';cam_data.ortho_scale=3.5
for pos,power,size in [((2,-4,5),700,5),((-3,-1,2),400,4),((0,4,4),500,3)]:
    ld=bpy.data.lights.new('ReviewLight','AREA');ld.energy=power;ld.shape='DISK';ld.size=size
    lo=bpy.data.objects.new('ReviewLight',ld);review_scene.collection.objects.link(lo);lo.location=pos;lo.rotation_euler=(Vector((0,0,1))-lo.location).to_track_quat('-Z','Y').to_euler()
for label,pos in [('front',(2,-4,2.8)),('back',(-2,4,2.8)),('side',(4,0,1.5)),('left',(-4,0,1.5)),('top',(0,-.01,6))]:
    cam.location=pos;cam.rotation_euler=(Vector((0,0,1))-cam.location).to_track_quat('-Z','Y').to_euler()
    review_scene.render.filepath=str(review/(label+'.png'));bpy.ops.render.render(write_still=True)
