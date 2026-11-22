"""Eight original-atlas doors plus existing shared CentCom hatch in preview."""
from pathlib import Path
import bpy,bmesh,json,math,struct,hashlib,os
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];SRC=ROOT/'godot/assets/tg/icons/obj/doors';OUT=ROOT/'godot/assets/models/doors'
def en(o,p,v):return next(i.identifier for i in o.bl_rna.properties[p].enum_items if i.identifier==v)
def material(name,image,glass=False,textured=True):
    m=bpy.data.materials.new(name);m.use_nodes=True;m.use_backface_culling=True
    s=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');s.inputs['Roughness'].default_value=.25 if glass else .8
    if textured:
        n=m.node_tree.nodes.new('ShaderNodeTexImage');n.image=image;n.interpolation=en(n,'interpolation','Closest');m.node_tree.links.new(n.outputs['Color'],s.inputs['Base Color'])
    else:s.inputs['Base Color'].default_value=(.10,.16,.18,1)
    if glass:s.inputs['Alpha'].default_value=.35;m.surface_render_method=en(m,'surface_render_method','BLENDED')
    return m
def part(name,cells,glass_cells,origin,offset,depth=.08,uvbox=None,unit=1,outline=None,band=frozenset()):
    vertices=[];faces=[];slots=[];lookup={}
    def v(x,y,d,c):
        k=(x,y,d,c)
        if k not in lookup:lookup[k]=len(vertices);vertices.append((x*unit/16-1-origin[0],d,2-y*unit/16-origin[2]))
        return lookup[k]
    def quad(points,slot,c):faces.append([v(*p,c) for p in points]);slots.append(slot)
    if outline:
        for sign in [-1,1]:quad([(x,y,sign*depth) for x,y in outline],0,0)
        for a,b in zip(outline,outline[1:]+outline[:1]):quad([(*a,-depth),(*b,-depth),(*b,depth),(*a,depth)],2,0)
    # band: a thinner step of the face's own material (a firelock frame's retracted leaves, thinner than the leaves so a shut door covers it)
    for area,d,c in [(cells-glass_cells-band,depth,0),(cells&glass_cells,depth/3,1),(band,depth*.4,0)]:
        for x,y in area:
            for sign in [-1,1]:quad([(x,y,sign*d),(x+1,y,sign*d),(x+1,y+1,sign*d),(x,y+1,sign*d)],c,c)
            for dx,dy,a,b in [(-1,0,(x,y),(x,y+1)),(1,0,(x+1,y),(x+1,y+1)),(0,-1,(x,y),(x+1,y)),(0,1,(x,y+1),(x+1,y+1))]:
                if (x+dx,y+dy) not in area:quad([(*a,-d),(*b,-d),(*b,d),(*a,d)],1 if c else 2,c)
    me=bpy.data.meshes.new(name);me.from_pydata(vertices,[],faces)
    for m in mats:me.materials.append(m)
    for p,s in zip(me.polygons,slots):p.material_index=s
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.normal_update();bmesh.ops.dissolve_limit(bm,angle_limit=.001,use_dissolve_boundaries=False,verts=list(bm.verts),edges=list(bm.edges),delimit={'MATERIAL'});bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));assert all(e.is_manifold for e in bm.edges);bm.to_mesh(me);bm.free();me.update()
    uv=me.uv_layers.new(name='Original_Atlas_UV')
    for p in me.polygons:
        for li in p.loop_indices:
            co=me.vertices[me.loops[li].vertex_index].co+Vector(origin);px=(co.x+1)*16;py=(2-co.z)*16
            if uvbox:
                x,y,w,h=uvbox;px=x+px/32*w;py=y+py/32*h
            uv.data[li].uv=((rect[0]+px)/W,1-(rect[1]+py)/H)
    me.calc_loop_triangles();ob=bpy.data.objects.new(name,me);scene.collection.objects.link(ob);ob.location=origin;ob['open_offset_blender']=offset;ob['open_offset_godot']=[offset[0],offset[2],-offset[1]];return ob
configs=[('centcom','airlocks/centcom/centcom','closed','side'),('highsec','airlocks/highsec/highsec','closed','highsec'),('firelock_heavy','doorfire','door_closed','fire'),('firelock_glass','doorfireglass','door_closed','fire_glass'),('shutters','shutters','closed','slab'),('shutters_window','shutters_window','closed','window_slab'),('windoor','windoor','left','windoor'),('morgue','doormorgue','door_closed','slab'),('shutters_radiation','shutters_radiation','closed','slab'),('blastdoor','blastdoor','closed','blast')]
groups=[];results={};allcells={(x,y) for x in range(32) for y in range(32)}
only=set(filter(None,os.environ.get('DOORS','').split(',')))  # DOORS=firelock_glass,firelock_heavy rebuilds just those, without the shared preview
for key,source,state,kind in configs:
    if only and key not in only:continue
    folder=OUT/key;folder.mkdir(parents=True,exist_ok=True);path=SRC/(source+'.png')
    meta=json.loads(path.with_suffix('.json').read_text());st=next(s for s in meta['states'] if s['name']==state);rect=st['rects'][0]
    im=bpy.data.images.load(str(path),check_existing=True);im.pack();W,H=im.size
    face=material(key+'_Original_Atlas',im);glazing=material(key+'_Glass',im,True,kind!='windoor');edge=material(key+'_Dark_Edges',im,False,False)
    bs=next(n for n in edge.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Base Color'].default_value=(.035,.035,.04,1)
    mats=[face,glazing,edge];scene=bpy.data.scenes.new(key+'_Door');bpy.context.window.scene=scene;objects=[];glass=set()
    if kind=='fire_glass':
        # Blue window texels are actual inserts, including the two small lower panes.
        pixels=list(im.pixels)
        for x,y in allcells:
            i=((H-1-rect[1]-y)*W+rect[0]+x)*4;r,g,b,a=pixels[i:i+4]
            if b>r*1.12 and g>r*1.08 and b>.12:glass.add((x,y))
    if kind=='window_slab':
        # Complete octagonal window, including the white/cyan reflection stripes.
        for y in range(6,14):
            left=11 if y in [6,13] else 10 if y in [7,12] else 8
            glass.update((x,y) for x in range(left,32-left))
    if kind in ['side','highsec']:
        # Follow whole pixel boundaries; never bisect texels or bevel the steps.
        if kind=='side':
            line=[(16,0),(16,5),(18,5),(18,9),(16,9),(16,10),(18,10),(18,14),(16,14),(16,20),(18,20),(18,24),(16,24),(16,25),(18,25),(18,29),(16,29),(16,32)]
        else:
            line=[(16,0),(16,12),(15,12),(15,13),(14,13),(14,14),(13,14),(13,17),(14,17),(14,18),(15,18),(15,19),(16,19),(16,32)]
        for left in [True,False]:
            outline=([(0,0)]+line+[(0,32)]) if left else ([(32,0),(32,32)]+list(reversed(line)))
            objects.append(part('Door_Left' if left else 'Door_Right',set(),set(),(-.5 if left else .5,0,0),(-1.3 if left else 1.3,0,0),.11,outline=outline))
    elif kind.startswith('fire'):
        inner={(x,y) for x,y in allcells if 3<=x<29 and 2<=y<29}
        # Only the pixels tg draws get geometry: its sprites carry alpha-2 click catchers (door_open's retracted leaves, the middle of its top and sill,
        # the glass door's window rims) that the shader discards, so a face over them was a hole into the part (the owner, 2026-10-27). The frame is
        # door_open's drawn pixels; those over the leaves are a thin step behind them, so a shut door shows door_closed.
        pixels=list(im.pixels)
        def drawn(name):
            r=next(s for s in meta['states'] if s['name']==name)['rects'][0]
            return {(x,y) for x,y in allcells if pixels[((H-1-r[1]-y)*W+r[0]+x)*4+3]>=.5}
        frame=drawn('door_open');glass|=inner-drawn('door_closed')  # the window rims' checkered catchers: glass, drawn by their alpha (a hole there is not manifold)
        objects.append(part('Frame',frame,set(),(0,0,0),(0,0,0),.13,band=frame&inner))
        # Dark outline stays on the upper leaf, as in the user's pixel-edge sketch.
        def fire_seam(x):
            if kind=='fire_glass':return 15 if 13<=x<20 or x<5 or x>=28 else 17
            return 16 if 13<=x<20 or x<5 or x>=28 else 18
        for upper in [True,False]:
            # the ring's undrawn pixels are notches the leaves fill when shut (a slot under or over a shut door otherwise)
            cells={(x,y) for x,y in inner|(allcells-inner-frame) if (y<fire_seam(min(max(x,3),28)) if (x,y) in inner else y<16)==upper}
            objects.append(part('Door_Upper' if upper else 'Door_Lower',cells,glass,(0,0,1.5 if upper else .5),(0,0,.95 if upper else -.85)))
    elif kind=='blast':
        for left in [True,False]:
            cells={(x,y) for x,y in allcells if (x<16)==left}
            objects.append(part('Door_Left' if left else 'Door_Right',cells,set(),(-.5 if left else .5,0,0),(-1.05 if left else 1.05,0,0),.14))
    elif kind=='windoor':
        # Source is an edge-on plan symbol; height and the window frame are reconstructed.
        glass={(x,y) for x,y in allcells if 1<=x<31 and 1<=y<31}
        objects.append(part('Sliding_Glass_Door',allcells,glass,(0,0,0),(-2.05,0,0),.045,uvbox=(0,27,32,5)))
    else:objects.append(part('Shutter' if key!='morgue' else 'Morgue_Door',allcells,glass,(0,0,0),(0,0,2.05),.06))
    root=bpy.data.objects.new(key,None);scene.collection.objects.link(root)
    for ob in objects:ob.parent=root;ob.select_set(True)
    root.select_set(True);bpy.context.view_layer.objects.active=objects[0]
    target=folder/(key+'.glb');bpy.ops.export_scene.gltf(filepath=str(target),use_selection=True,use_active_scene=True,export_animations=False,export_extras=True)
    blob=target.read_bytes();n=struct.unpack_from('<I',blob,12)[0];g=json.loads(blob[20:20+n]);tail=blob[20+n:]
    for s in g['samplers']:s.update(minFilter=9728,magFilter=9728)
    for nd in g['nodes']:nd['name']=nd['name'].split('.')[0]
    raw=json.dumps(g,separators=(',',':')).encode();raw+=b' '*(-len(raw)%4);target.write_bytes(struct.pack('<III',0x46546c67,2,20+len(raw)+len(tail))+struct.pack('<II',len(raw),0x4e4f534a)+raw+tail)
    assert len(g['meshes'])==len(objects)
    for image in g['images']:
        v=g['bufferViews'][image['bufferView']];a=v.get('byteOffset',0);assert tail[8+a:8+a+v['byteLength']]==path.read_bytes()
    info={'source':str(path.relative_to(ROOT/'godot')).replace('\\','/'),'state':state,'rect':rect,'source_sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'parts':{o.name.split('.')[0]:{'triangles':len(o.data.loop_triangles),'open_offset_godot':list(o['open_offset_godot'])} for o in objects},'triangles':sum(len(o.data.loop_triangles) for o in objects),'nearest':True,'original_png_embedded':True,'glass_alpha':.35 if glass else None,'notes':'Reconstructed full height from edge-on source.' if kind=='windoor' else 'Closed tile; source direction 0. Rotate whole model for other cardinal directions.'}
    (folder/'model_info.json').write_text(json.dumps(info,indent=2)+'\n');bpy.data.libraries.write(str(folder/(key+'.blend')),{scene},path_remap='RELATIVE',fake_user=True,compress=True)
    results[key]=info;groups.append((key,objects))
if only:raise SystemExit(0)
# Reuse the corrected hatch instead of generating another mesh family.
hatch=bpy.data.scenes.get('Hatch_Airlock')
if hatch is None:
    with bpy.data.libraries.load(str(OUT/'hatch/hatch_airlock.blend'),link=False) as (available,loaded):loaded.scenes=[s for s in available.scenes if s.startswith('Hatch_Airlock')][:1]
    hatch=loaded.scenes[0]
groups.append(('hatch_centcom',[o for o in hatch.objects if o.type=='MESH']))
preview=bpy.data.scenes.new('Additional_Doors_Preview');bpy.context.window.scene=preview
for i,(name,parts) in enumerate(groups):
    x=(i%3)*3;y=(i//3)*3.2
    for original in parts:
        ob=original.copy();ob.parent=None;ob.name=name+'_'+original.name;preview.collection.objects.link(ob);ob.location=original.location+Vector((x,y,0))
    data=bpy.data.curves.new('Label_'+name,'FONT');data.body=name;data.size=.13;ob=bpy.data.objects.new('Label_'+name,data);preview.collection.objects.link(ob);ob.location=(x-1,y-.1,-.23);ob.rotation_euler.x=math.pi/2
world=bpy.data.worlds.new('Additional_Doors_World');world.use_nodes=True;preview.world=world
bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.12,.14,.18,1);bg.inputs['Strength'].default_value=.8
for name,rot,energy in [('Key',(25,-30,-25),2),('Fill',(45,20,140),1)]:
    ld=bpy.data.lights.new(name,next(i.identifier for i in bpy.types.Light.bl_rna.properties['type'].enum_items if i.identifier=='SUN'));ld.energy=energy;ld.angle=.25;ob=bpy.data.objects.new(name,ld);preview.collection.objects.link(ob);ob.rotation_euler=[math.radians(v) for v in rot]
data=bpy.data.cameras.new('Additional_Doors_Camera');data.type=en(data,'type','ORTHO');data.ortho_scale=12.5;cam=bpy.data.objects.new('Additional_Doors_Camera',data);preview.collection.objects.link(cam);target=Vector((3,3.2,.8));cam.location=target+Vector((3,-14,10));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();preview.camera=cam
preview.render.resolution_x=1500;preview.render.resolution_y=1300;preview.render.resolution_percentage=100;preview.render.image_settings.file_format=en(preview.render.image_settings,'file_format','PNG');preview.render.filepath=str(ROOT/'docs/ai/art/additional_doors_preview.png');bpy.ops.render.render(write_still=True)
bpy.data.libraries.write(str(OUT/'additional_doors_preview.blend'),{preview},path_remap='RELATIVE',fake_user=True,compress=True)
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            sp=area.spaces.active;sp.shading.type=en(sp.shading,'type','MATERIAL');sp.region_3d.view_location=target;sp.region_3d.view_distance=15;sp.region_3d.view_rotation=Vector((1,-6,4)).to_track_quat('Z','Y');sp.overlay.show_floor=False
print(json.dumps({k:{'parts':len(v['parts']),'triangles':v['triangles']} for k,v in results.items()}))
