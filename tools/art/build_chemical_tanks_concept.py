"""Execute in the connected Blender via MCP. Non-destructive, isolated asset scene."""
import bpy, bmesh, math, json
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'godot/assets/models/station_props/chemical_tanks'
OUT.mkdir(parents=True,exist_ok=True)
scene=bpy.data.scenes.new('Chemical_Tanks_Concept')
bpy.context.window.scene=scene
scene.unit_settings.system='METRIC'
col=bpy.data.collections.new('Chemical_Tanks_Assets');scene.collection.children.link(col)
def mat(name,h,alpha=1):
    m=bpy.data.materials.new('CT_Flat_'+name);m.use_nodes=True
    rgb=[int(h[i:i+2],16)/255 for i in (0,2,4)]
    rgb=[v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in rgb]
    m.diffuse_color=(*rgb,alpha)
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*rgb,1);p.inputs['Roughness'].default_value=.72;p.inputs['Metallic'].default_value=.12;p.inputs['Alpha'].default_value=alpha
    if alpha<1:m.surface_render_method='DITHERED'
    return m
mats={k:mat(k,h) for k,h in dict(Frame='45464E',Band='73737A',Ivory='D8CDC0',Blue='168FDC',Cyan='30C7F4',Green='71BF53',Orange='F5A12C',Red='CA4035',White='E5E4E0',Dark='282D34').items()}
mats['Overlay']=mat('Tint_Reagent','FFFFFF',.62)
parts=[];root=None
def mesh(name,vs,fs,material):
    me=bpy.data.meshes.new('CT_'+name);me.from_pydata(vs,[],fs);me.materials.append(mats[material]);me.update()
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(me);bm.free()
    ob=bpy.data.objects.new(name,me);col.objects.link(ob);ob.parent=root;parts.append(ob);return ob
def box(name,c,s,m):
    x,y,z=c;a,b,d=[v/2 for v in s]
    return mesh(name,[(x+i*a,y+j*b,z+k*d) for k in [-1,1] for j in [-1,1] for i in [-1,1]],[(0,2,3,1),(4,5,7,6),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5)],m)
def loft(name,x,y,levels,m,n=8,axis='Z',cap=True):
    vs=[]
    for r,z in levels:
        for i in range(n):
            a=2*math.pi*i/n+math.pi/8
            u,v=r*math.cos(a),r*math.sin(a)
            vs.append((x+u,y+v,z) if axis=='Z' else ((z,x+u,y+v) if axis=='X' else (x+u,z,y+v)))
    fs=[(j*n+i,j*n+(i+1)%n,(j+1)*n+(i+1)%n,(j+1)*n+i) for j in range(len(levels)-1) for i in range(n)]
    if cap:
        if len(levels)==1:fs.append(tuple(range(n)))
        elif 'Strap' not in name:
            if name not in ['Main_Vessel','Auxiliary_Vessel']:fs.append(tuple(reversed(range(n))))
            fs.append(tuple((len(levels)-1)*n+i for i in range(n)))
    return mesh(name,vs,fs,m)
root=bpy.data.objects.new('water',None);col.objects.link(root)
box('Chassis',(0,0,.22),(1.18,.66,.14),'Frame')
for x in [-.55,.55]:
    box('Corner_Shoe',(x,-.29,.24),(.18,.19,.22),'Band')
    # Sloped upper handle is a four-sided swept beam, not a bevel modifier.
    yz=[(-.23,.32),(-.23,1.03),(-.13,1.17),(.24,1.17),(.24,.32)]
    vs=[(x+dx,y,z) for dx in [-.045,.045] for y,z in yz]
    # Narrow U rail with open interior, assembled from two upright beams and top beam.
    box('Front_Upright',(x,-.24,.69),(.09,.09,.78),'Frame')
    box('Back_Upright',(x,.24,.69),(.09,.09,.78),'Frame')
    box('Top_Grip',(x,0,1.08),(.09,.57,.09),'Band')
loft('Main_Vessel',-.23,0,[(.267,.32),(.315,.40),(.315,1.36),(.267,1.44)],'Blue')
for z in [.40,1.04]:loft('Main_Strap',-.23,0,[(.326,z),(.326,z+.115)],'Band')
loft('Fill_Cap',-.23,0,[(.19,1.44),(.19,1.49),(.16,1.53)],'Frame')
loft('Cap_Inset',-.23,0,[(.137,1.531)],'Ivory')
loft('Auxiliary_Vessel',.305,0,[(.165,.32),(.202,.38),(.202,1.31),(.16,1.38)],'Ivory')
for z in [.40,.87]:loft('Auxiliary_Strap',.305,0,[(.214,z),(.214,z+.10)],'Frame')
# Front painted stripe: single coplanar quad, hidden behind straps and valve.
mesh('Auxiliary_Stripe',[(.283,-.188,.38),(.327,-.188,.38),(.327,-.188,1.31),(.283,-.188,1.31)],[(0,1,2,3)],'Cyan')
loft('Tap_Valve',.305,.78,[(.102,-.20),(.102,-.27)],'Frame',axis='Y')
loft('Tap_Dial',.305,.78,[(.071,-.271)],'Cyan',axis='Y')
box('Tap_Bridge',(.10,-.15,.78),(.20,.12,.09),'Frame')
box('Readout_Bezel',(.29,-.341,.23),(.23,.018,.095),'Dark')
box('Readout',(.29,-.353,.23),(.155,.009,.043),'Cyan')
# Colour band is paint on the lower barrel, zero thickness (slight offset avoids z fighting).
loft('Lower_Colour_Band',-.23,0,[(.316,.53),(.316,.61)],'Blue',cap=False)
common=list(parts)
for x in [-.49,.49]:
    for y in [-.23,.23]:loft('Wheel',y,.105,[(.105,x-.045),(.105,x+.045)],'Dark',n=6,axis='X')
portable=parts[len(common):]
for x in [-.49,.49]:
    for y in [-.23,.23]:box('Anchor_Foot',(x,y,.08),(.18,.18,.16),'Band')
loft('Pipe_Collar',0,.62,[(.137,.49),(.137,.60)],'Band',axis='X')
loft('Pipe_Outlet',0,.62,[(.095,.60),(.095,.75)],'Frame',axis='X')
stationary=parts[len(common)+len(portable):]
templates=list(parts)
states=[('water',1000,'water','Blue','Ivory','Cyan','Blue'),('fuel',1000,'fuel','Orange','Band','Orange','Orange'),('water_high',100000,'water','Green','Ivory','Cyan','Blue'),('fuel_high',5000,'fuel','Orange','Red','Orange','Red'),('foam',500,'firefighting_foam','White','White','White','White'),('fuel_stationary',1000,'fuel','Orange','Red','Orange','Red'),('water_stationary',1000,'water','Blue','Ivory','Cyan','Blue'),('tank_stationary',1000,None,'Ivory','Ivory','Band','Ivory')]
records=[];roots=[]
for index,(name,capacity,reagent,body,aux,accent,band) in enumerate(states):
    fixed=index>=5
    if index==0:r=root;obs=common+portable
    else:
        r=bpy.data.objects.new(name,None);col.objects.link(r);obs=[]
        for src in common+(stationary if fixed else portable):
            o=src.copy();o.data=src.data;col.objects.link(o);o.parent=r;obs.append(o)
    roots.append(r)
    r['icon_state']=name;r['capacity_units']=capacity;r['reagent_id']=reagent or '';r['anchored']=fixed;r['openable']=not fixed;r['accepts_rig']=reagent=='fuel';r['source_icon']='icons/obj/medical/chemical_tanks.dmi';r['origin']='floor center; Blender -Y front / glTF +Z front'
    for o in obs:
        target=body if o.name.startswith('Main_Vessel') else aux if o.name.startswith('Auxiliary_Vessel') else accent if o.name.startswith(('Auxiliary_Stripe','Tap_Dial','Readout.')) or o.name=='Readout' else band if o.name.startswith('Lower_Colour_Band') else None
        if target:o.material_slots[0].link='OBJECT';o.material_slots[0].material=mats[target]
    bpy.ops.object.select_all(action='DESELECT');r.select_set(True)
    for o in obs:o.select_set(True)
    bpy.context.view_layer.objects.active=r
    bpy.ops.export_scene.gltf(filepath=str(OUT/(name+'.glb')),export_format='GLB',use_selection=True,use_active_scene=True,export_extras=True,export_animations=False,export_texcoords=False,export_normals=True)
    vertices=sum(len(o.data.vertices) for o in obs)
    tris=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in obs)
    assert vertices<=500,(name,vertices)
    records.append(dict(state=name,capacity_units=capacity,reagent=reagent,anchored=fixed,vertices=vertices,triangles=tris,file=name+'.glb'))
    r.location=((index%3)*1.85,(index//3)*2.1,0)
# Unused templates are unlinked only from our new scene; original user scene is untouched.
for o in stationary:col.objects.unlink(o)
parts=[];root=bpy.data.objects.new('tank_chem_overlay',None);col.objects.link(root)
overlay=loft('Reagent_Overlay',-.23,0,[(.269,.322),(.317,.40),(.317,1.36),(.269,1.442)],'Overlay')
root['is_overlay']=True;root['base_state']='tank_stationary';root['show_when']='total_volume > 0';root['tint_source']='mix_color_from_reagents(reagents.reagent_list)';root['alpha']=.62
bpy.ops.object.select_all(action='DESELECT');root.select_set(True);overlay.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(OUT/'tank_chem_overlay.glb'),export_format='GLB',use_selection=True,use_active_scene=True,export_extras=True,export_animations=False)
root.location=(3.7,4.2,0)
for src in roots[7].children:
    ob=src.copy();col.objects.link(ob);ob.parent=root
demo=mat('Preview_Reagent_Green','65C779',.62);overlay.material_slots[0].link='OBJECT';overlay.material_slots[0].material=demo
root['preview_only_color']='#65C779; exported overlay is neutral white'
records.append(dict(state='tank_chem_overlay',is_overlay=True,vertices=32,triangles=60,base_state='tank_stationary',file='tank_chem_overlay.glb'))
(OUT/'model_info.json').write_text(json.dumps(dict(states=records,vertex_budget=500,scale='meters',height=1.53,source='src/Core/Tg/Generated/Types/obj.structure.g.cs; src/Core/Tg/modules/reagents/reagent_dispenser.cs',runtime_limitation='plumbed classes are DmInert; reagent overlay update not ported',overlay_contract=dict(visible='total_volume > 0',color='mix_color_from_reagents',alpha=.62)),indent=2))
# Review studio, never included in game exports.
studio=bpy.data.collections.new('Chemical_Tanks_Review_Only');scene.collection.children.link(studio)
def helper(name,data):
    o=bpy.data.objects.new(name,data);studio.objects.link(o);return o
cam=helper('CT_Review_Camera',bpy.data.cameras.new('CT_Camera'));scene.camera=cam
cam.location=(6.9,-10,10.2);target=Vector((1.8,2.1,.6));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=8.8
for name,loc,power,size in [('Key',(-3,-4,9),1800,7),('Fill',(7,1,6),1300,6),('Rim',(1,7,7),1800,5)]:
    o=helper('CT_'+name,bpy.data.lights.new('CT_'+name,'AREA'));o.location=loc;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler()
scene.world=bpy.data.worlds.new('CT_World');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.09,.12,.17,1);scene.world.node_tree.nodes['Background'].inputs[1].default_value=.5
me=bpy.data.meshes.new('CT_Floor');me.from_pydata([(-200,-200,-.015),(200,-200,-.015),(200,200,-.015),(-200,200,-.015)],[],[(0,1,2,3)]);me.materials.append(mat('Backdrop','252D3A'));helper('CT_Floor',me)
for i,r in enumerate(roots+[root]):
    data=bpy.data.curves.new('CT_Label','FONT');data.body=f'{i+1:02}  '+r.name;data.size=.12;data.align_x='CENTER';data.extrude=0
    ob=helper('CT_Label',data);ob.location=r.location+Vector((0,-.64,.012));data.materials.append(mats['White'])
scene.render.engine='CYCLES';scene.cycles.samples=32
scene.render.resolution_x=1500;scene.render.resolution_y=1400;scene.render.resolution_percentage=100
scene.view_settings.view_transform='Standard';scene.view_settings.exposure=-.45
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(ROOT/'docs/ai/art/chemical_tanks_concept_preview.png')
for area in bpy.context.screen.areas:
    if area.type=='VIEW_3D':
        area.spaces.active.region_3d.view_location=target;area.spaces.active.region_3d.view_distance=9;area.spaces.active.region_3d.view_rotation=cam.rotation_euler.to_quaternion();area.spaces.active.shading.type='MATERIAL'
bpy.data.libraries.write(str(OUT/'chemical_tanks.blend'),{scene},fake_user=True)
result={'assets':str(OUT),'states':records,'scene':scene.name}
