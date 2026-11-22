"""Multi-angle inspection sheets and a linked-geometry showroom for vending assets."""
import bpy,math,json
from mathutils import Matrix,Vector
V=bpy.app.driver_namespace['vending_family'];ROOT=V['ROOT'];OUT=V['OUT'];DOC=V['DOC'];G=V['G']
QA=DOC/'quality/vending';QA.mkdir(exist_ok=True)

def setup(name,width,height):
    sc=bpy.data.scenes.new(name);bpy.context.window.scene=sc
    w=bpy.data.worlds.new(name+'_World');w.use_nodes=True;sc.world=w
    bg=next(n for n in w.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.08,.09,.12,1);bg.inputs['Strength'].default_value=.65
    for name,rot,energy in [('Key',(25,-25,-20),2.2),('Fill',(45,30,140),1.0)]:
        l=bpy.data.lights.new(name,'SUN');l.energy=energy;l.angle=.35;o=bpy.data.objects.new(name,l);sc.collection.objects.link(o);o.rotation_euler=tuple(math.radians(a) for a in rot)
    sc.render.engine='BLENDER_EEVEE_NEXT';sc.eevee.taa_render_samples=24
    sc.render.resolution_x=width;sc.render.resolution_y=height;sc.render.resolution_percentage=100
    sc.view_settings.view_transform=G.enum(sc.view_settings,'view_transform','Standard');sc.render.image_settings.file_format=G.enum(sc.render.image_settings,'file_format','PNG')
    return sc

def camera(sc,location,target,scale):
    data=bpy.data.cameras.new(sc.name+'_Camera');data.type=G.enum(data,'type','ORTHO');data.ortho_scale=scale
    cam=bpy.data.objects.new(data.name,data);sc.collection.objects.link(cam);cam.location=location;cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();sc.camera=cam

def copy_asset(sc,state,matrix):
    for p in V['ASSETS'][state]['parts']:
        o=p.copy();o.parent=None;sc.collection.objects.link(o);o.matrix_world=matrix@p.matrix_world

def label(sc,text,location,size=.15):
    font=bpy.data.curves.new(text,'FONT');font.body=text;font.size=size;font.align_x='CENTER'
    obj=bpy.data.objects.new(text,font);sc.collection.objects.link(obj);obj.location=location;obj.rotation_euler=(math.pi/2,0,0)
    mat=bpy.data.materials.get('Vending_Preview_Label')
    if not mat:
        mat=bpy.data.materials.new('Vending_Preview_Label');mat.use_nodes=True
        bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Base Color'].default_value=(.85,.88,.95,1);bs.inputs['Emission Color'].default_value=(.85,.88,.95,1);bs.inputs['Emission Strength'].default_value=.7
    font.materials.append(mat)

def sheet(index):
    states=sorted(V['ASSETS'])[index*8:(index+1)*8];rows=len(states)
    sc=setup('Vending_QA_'+str(index+1),1300,int(1300*rows*2.25/10.4))
    for row,state in enumerate(states):
        info=V['REPORT'][state];center=info['height_m']/2+info['mount_height_m']
        for col,angle in enumerate([0,-40,180,-90]):
            m=Matrix.Translation((col*2.6,0,(rows-1-row)*2.25+1.05))@Matrix.Rotation(math.radians(15),4,'X')@Matrix.Rotation(math.radians(angle),4,'Z')@Matrix.Translation((0,0,-center))
            copy_asset(sc,state,m)
            label(sc,state if col==0 else ['','front / right','rear','left'][col],(col*2.6,-1.2,(rows-1-row)*2.25+.03),.14)
    camera(sc,(3.9,-40,rows*2.25/2),(3.9,0,rows*2.25/2),max(10.4,rows*2.25))
    sc.render.filepath=str(QA/f'vending_views_{index+1:02}.png');bpy.ops.render.render(write_still=True)
    print('SHEET',index+1,str(sc.render.filepath))
    # QA scenes are disposable, but leave their saved images for review.
    bpy.context.window.scene=V['ASSETS'][states[0]]['scene']
    for o in list(sc.objects):bpy.data.objects.remove(o,do_unlink=True)
    bpy.data.scenes.remove(sc)

def showroom():
    sc=setup('Vending_All_67_Preview',2200,1800)
    for i,state in enumerate(sorted(V['ASSETS'])):
        a=V['ASSETS'][state];root=bpy.data.objects.new(a['key']+'_Preview',None);sc.collection.objects.link(root);root.location=((i%9)*2.3,(i//9)*2.5,0)
        for p in a['parts']:
            o=p.copy();o.parent=root;sc.collection.objects.link(o);o.matrix_basis=p.matrix_basis.copy()
    camera(sc,(27,-34,31),(9,8,0),32)
    sc.render.filepath=str(DOC/'vending_machines_preview.png');bpy.ops.render.render(write_still=True)
    bpy.data.libraries.write(str(OUT.parent/'vending_machines_preview.blend'),{sc},path_remap='RELATIVE',fake_user=True,compress=True)
    bpy.data.libraries.write(str(OUT.parent/'vending_catalog.blend'),{a['scene'] for a in V['ASSETS'].values()},path_remap='RELATIVE',fake_user=True,compress=True)
    meshes={}
    slots={}
    for state,a in V['ASSETS'].items():
        slots[state]=[]
        for p in a['parts']:
            meshes.setdefault(p.data.name,[]).append({'state':state,'part':p.name,'slots':[s.material.name for s in p.material_slots]})
            slots[state].append({'name':p.name,'mesh':p.data.name,'connected_components':1,'triangles':len(p.data.loop_triangles),'non_manifold':0,'origin_blender_m':list(p.location),'materials':[{'slot':i,'name':s.material.name} for i,s in enumerate(p.material_slots)]})
    (OUT/'mesh_reuse.json').write_text(json.dumps(meshes,indent=2)+'\n')
    (OUT/'material_slots.json').write_text(json.dumps(slots,indent=2)+'\n')
    print('Saved showroom and library',len(meshes),'unique mesh data blocks')

bpy.app.driver_namespace['vending_preview']=globals()
