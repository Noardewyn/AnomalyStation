"""Full-depth vending family with original front/roof UVs and derived service surfaces.

Run definitions through Blender MCP, then build_batch(names), inspect and export_batch(names).
"""
from pathlib import Path
from collections import defaultdict,Counter
import bpy,bmesh,json,math,hashlib,struct,importlib.util,re
from mathutils import Vector,Matrix
ROOT=Path(__file__).resolve().parents[2];OUT=ROOT/'godot/assets/models/station_props/vending';DOC=ROOT/'docs/ai/art'
CAT=json.loads((OUT/'source_catalog.json').read_text());FAMILIES=json.loads((OUT/'geometry_families.json').read_text())
PNG=ROOT/'godot/assets/tg/icons/obj/machines/vending.png';META=json.loads(PNG.with_suffix('.json').read_text());STATES={s['name']:s for s in META['states']}
spec=importlib.util.spec_from_file_location('vendor_mesh_kernel',ROOT/'tools/art/station_prop_geometry.py');G=importlib.util.module_from_spec(spec);spec.loader.exec_module(G)
IMAGE=bpy.data.images.load(str(PNG),check_existing=True);IMAGE.pack();PIXELS=list(IMAGE.pixels)
PX=.06;ASSETS={};MESH_CACHE={};REPORT={};TEXTURES={};(OUT/'textures').mkdir(exist_ok=True)
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')

def source_pixel(state,x,y):
    r=STATES[state]['rects'][0];i=((IMAGE.size[1]-1-r[1]-int(y))*IMAGE.size[0]+r[0]+int(x))*4
    return PIXELS[i:i+4]

def atlas_uv(state,x,y):
    # Tile-local coordinates allow actual shared geometry across materials.
    return x/32,1-y/32

def material(name,image,rect=None,mask=False,glass=False):
    m=bpy.data.materials.new(name);m.use_nodes=True;m.use_backface_culling=True
    bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.78;bs.inputs['Specular IOR Level'].default_value=.10
    if glass:
        bs.inputs['Base Color'].default_value=(.36,.52,.56,1);bs.inputs['Alpha'].default_value=.15;bs.inputs['Roughness'].default_value=.2;m.surface_render_method=G.enum(m,'surface_render_method','BLENDED');return m
    tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image;tex.interpolation=G.enum(tex,'interpolation','Closest')
    if rect:
        coord=m.node_tree.nodes.new('ShaderNodeTexCoord');mapping=m.node_tree.nodes.new('ShaderNodeMapping')
        mapping.inputs['Scale'].default_value=(32/image.size[0],32/image.size[1],1);mapping.inputs['Location'].default_value=(rect[0]/image.size[0],1-(rect[1]+32)/image.size[1],0)
        m.node_tree.links.new(coord.outputs['UV'],mapping.inputs['Vector']);m.node_tree.links.new(mapping.outputs['Vector'],tex.inputs['Vector'])
    m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color'])
    if mask:
        m.node_tree.links.new(tex.outputs['Alpha'],bs.inputs['Alpha']);m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Emission Color']);bs.inputs['Emission Strength'].default_value=.4;m.surface_render_method=G.enum(m,'surface_render_method','DITHERED')
    return m

GLASS=material('Vending_Glass_Closed_Cyan',None,glass=True)

def derived(state,bounds):
    x0,y0,x1,y1=bounds;w=h=64;pixels=[0.]*(w*h*4)
    def sample(x,y):
        c=source_pixel(state,min(x1-1,max(x0,x)),min(y1-1,max(y0,y)))
        if c[3]<.99:c=source_pixel(state,x0+3,y0+3)
        return list(c[:3])+[1.]
    roof_colors=Counter(tuple(sample(x,y)) for y in range(y0+1,y0+4) for x in range(x0+2,x1-2))
    base=list(roof_colors.most_common(1)[0][0]);trim=sample(x0+1,y0+6);shadow=sample(x0,y0+10);light=sample(x0+2,y0+6)
    for y in range(64):
        for x in range(64):
            u=x%32;v=y%32
            if y<32:
                # Side: original edge strip at each height, extended into a framed panel.
                sy=min(y1-2,y0+5+int(v*(y1-y0-6)/32))
                c=sample(x1-1,sy)
                if 3<=u<=28:c=base
                if v in [1,3,28,30]:c=trim if v in [3,30] else light
                if u in [3,28]:c=trim
                if 8<=u<=23 and v in [20,22,24]:c=shadow
                if (u,v) in [(5,6),(26,6),(5,27),(26,27)]:c=light
            elif x<32:
                # Rear: perimeter seams, bolted access cover and ventilation slots.
                c=base
                if u in [1,30] or v in [1,30]:c=shadow
                if u in [3,28] or v==4:c=trim
                if 6<=u<=25 and 10<=v<=27:
                    c=trim if u in [6,25] or v in [10,27] else base
                    if 9<=u<=22 and v in [17,19,21,23]:c=shadow
                    if (u,v) in [(8,12),(23,12),(8,25),(23,25)]:c=light
            else:
                c=shadow
                if u in [1,30] or v in [1,30]:c=trim
                if 3<=u<=28 and v in [5,27]:c=base
            i=((63-y)*64+x)*4;pixels[i:i+4]=c
    im=bpy.data.images.new('Vending_Surfaces_'+state,width=64,height=64,alpha=True);im.pixels.foreach_set(pixels)
    path=OUT/'textures'/('vending_'+re.sub(r'[^a-z0-9]+','_',state.lower())+'_surfaces.png');im.filepath_raw=str(path);im.file_format=G.enum(im,'file_format','PNG');im.save();im.pack()
    TEXTURES[state]=str(path.relative_to(OUT));return im

def layout(state):
    family=FAMILIES['state_to_family'][state];x0,y0,x1,y1=CAT['vendors'][state]['opaque_bbox']
    top=y0+(3 if family=='wall_medical' else 5);right=x1-2
    depth=5 if family=='wall_medical' else (14 if family=='boozeomat' else 11)
    display=None;delivery=None
    if family in ['wardrobe','theater']:display=(x0+3,10,right-5,25);delivery=(x0+4,27,right-2,29)
    elif family in ['snack','compact_product']:display=(x0+2,10,right-5,25);delivery=(x0+3,27,right-5,29)
    elif family=='branded_drink':delivery=(x0+3,27,right-6,29)
    elif family in ['drink_display','magic']:display=(x0+3,10,right-6,25);delivery=(x0+4,27,right-3,29)
    elif family in ['wide_medical','wide_service']:display=(x0+2,9,right-8,28);delivery=(right-7,26,right-1,29)
    elif family=='wide_electronics':display=(x0+2,9,right-2,23);delivery=(x0+10,26,right-1,29)
    elif family=='wide_food':delivery=(x0+3,27,right-3,29)
    elif family=='custom':display=(x0+3,10,right-4,24);delivery=(x0+3,27,right-4,29)
    elif family=='coffee':display=(x0+4,12,right-3,22);delivery=(x0+6,25,right-5,28)
    elif family=='dinnerware':display=(x0+2,10,right-4,26);delivery=(x0+3,28,right-4,30)
    elif family=='industrial':display=(x0+4,17,right-6,25);delivery=(x0+3,27,right-6,29)
    elif family=='engi':display=(x0+3,17,right-1,24);delivery=(x0+3,27,right-9,29)
    elif family=='sovietsoda':display=(x0+3,8,right-3,16);delivery=(x0+5,25,right-4,28)
    elif family=='boozeomat':display=(x0+2,12,18,29);delivery=(21,23,27,29)
    elif family=='hotdog':display=(10,14,21,25);delivery=(11,26,20,28)
    elif family=='wall_medical':display=(x0+2,13,right-1,21);delivery=(x0+2,24,right-1,26)
    elif family=='cytopro':display=(x0+3,10,right-4,25);delivery=(x0+4,27,right-4,29)
    # Product windows differ even on cabinets with the same outer silhouette.
    overrides={
        'cigs':((7,10,20,21),(7,25,15,27)),
        'generic':((7,10,20,24),(9,27,21,29)),
        'parts':((8,10,19,24),(8,27,18,29)),
        'games':((7,16,20,25),(9,27,16,29)),
        'engivend':((8,10,14,25),(9,27,19,29)),
        'tool':(None,(9,27,20,29)),
        'centdrobe':((7,10,20,25),(9,27,20,29)),
        'greed':((6,9,23,23),(16,26,27,29)),
        'custom':((7,11,23,22),(7,26,22,28)),
        'robotics':((6,11,22,22),(14,26,26,29)),
        'modularpc':((6,16,24,22),(14,26,26,29)),
        'soda':((9,13,16,25),(9,27,20,29)),
        'starkist':((9,11,19,24),(9,26,21,28)),
        'wallmed':((11,12,21,20),(11,25,14,26)),
    }
    if state in overrides:display,delivery=overrides[state]
    def bounded(rect):
        return (max(x0+1,rect[0]),max(top+1,rect[1]),min(right-1,rect[2]),min(y1-1,rect[3])) if rect else None
    display,delivery=bounded(display),bounded(delivery)
    return family,(x0,top,right,y1),depth,display,delivery

def build(state):
    family,(x0,top,right,bottom),depth,display,delivery=layout(state)
    key='vending_'+re.sub(r'[^a-z0-9]+','_',state.lower());scene=bpy.data.scenes.new('Vending_'+state);bpy.context.window.scene=scene
    root=bpy.data.objects.new(key,None);scene.collection.objects.link(root)
    mats=[material('Vending_Front_'+state,IMAGE,STATES[state]['rects'][0]),material('Vending_Service_'+state,derived(state,CAT['vendors'][state]['opaque_bbox']))]
    width=right-x0;height=bottom-top
    mount=.90 if family=='wall_medical' else 0
    cells={};frontdepth={}
    def inside(rect,sx,sy):return rect and rect[0]<=sx<rect[2] and rect[1]<=sy<rect[3]
    for sx in range(x0,right):
        for sy in range(top,bottom):
            if source_pixel(state,sx,sy)[3]<.99:continue
            inset=2 if inside(display,sx,sy) else 0
            if inside(delivery,sx,sy):inset=3
            frontdepth[sx,sy]=inset
            for yy in range(inset,depth):cells[sx-x0,yy,bottom-sy-1]=0
    groups=defaultdict(set)
    for p in cells:
        for axis in range(3):
            axes=[i for i in range(3) if i!=axis]
            for sign in [-1,1]:
                q=list(p);q[axis]+=sign
                if tuple(q) in cells:continue
                plane=p[axis]+(sign>0)
                role='side'
                if axis==1:role='back' if sign>0 else 'front'
                elif axis==2:role='roof' if sign>0 and plane==height else 'inside'
                if axis==0 and plane not in [0,width]:role='inside'
                groups[axis,plane,sign,role].add((p[axes[0]],p[axes[1]]))
    quads=[]
    for (axis,plane,sign,role),area in groups.items():
        while area:
            u,v=min(area);w=1
            while (u+w,v) in area:w+=1
            h=1
            while all((u+i,v+h) in area for i in range(w)):h+=1
            area.difference_update((u+i,v+j) for i in range(w) for j in range(h));axes=[i for i in range(3) if i!=axis];points=[]
            for aa,bb in [(u,v),(u+w,v),(u+w,v+h),(u,v+h)]:
                p=[0,0,0];p[axis]=plane;p[axes[0]]=aa;p[axes[1]]=bb;points.append(tuple(p))
            quads.append((points,role))
    corners={p for points,_ in quads for p in points};mesh=G.Mesh();material_indices=[]
    def world(p):return ((p[0]-width/2)*PX,(p[1]-depth/2)*PX,p[2]*PX+mount)
    for points,role in quads:
        split=[]
        for a,b in zip(points,points[1:]+points[:1]):
            axis=next(i for i in range(3) if a[i]!=b[i]);fixed=[i for i in range(3) if i!=axis]
            edge=[p for p in corners if all(p[i]==a[i] for i in fixed) and min(a[axis],b[axis])<=p[axis]<=max(a[axis],b[axis])]
            edge.sort(key=lambda p:(p[axis]-a[axis])/(b[axis]-a[axis]));split+=edge[:-1]
        coords=[]
        for x,y,z in split:
            if role=='front':co=atlas_uv(state,x0+x,bottom-z)
            elif role=='roof':co=atlas_uv(state,x0+.5+x/max(1,width)*(width-1),top-.5-y/depth*(top-CAT['vendors'][state]['opaque_bbox'][1]-1))
            elif role=='side':co=((.5+y/depth*31)/64,1-(.5+(1-z/height)*31)/64)
            elif role=='back':co=((.5+x/width*31)/64,1-(32.5+(1-z/height)*31)/64)
            else:
                axis=next(i for i in range(3) if all(p[i]==split[0][i] for p in split));axes=[i for i in range(3) if i!=axis];den=[width,depth,height]
                co=((32.5+split[0][0]*0+31*(x,y,z)[axes[0]]/den[axes[0]])/64,1-(32.5+31*(x,y,z)[axes[1]]/den[axes[1]])/64)
            coords.append(co)
        mesh.face([world(p) for p in split],coords);material_indices.append(0 if role in ['front','roof'] else 1)
    # Create without dissolving material boundaries, then recalculate normals.
    me=bpy.data.meshes.new(key+'_Body');me.from_pydata(mesh.v,[],mesh.f);me.materials.clear()
    for m in mats:me.materials.append(m)
    uv=me.uv_layers.new(name='Surface_UV')
    for p,coords,index in zip(me.polygons,mesh.uvs,material_indices):
        p.material_index=index
        for li,co in zip(p.loop_indices,coords):uv.data[li].uv=co
    bm=bmesh.new();bm.from_mesh(me);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    assert all(e.is_manifold for e in bm.edges),(state,'body edges')
    assert all(v.is_manifold for v in bm.verts),(state,'body vertices')
    assert bm.calc_volume(signed=True)>0
    bm.to_mesh(me);bm.free();me.update();me.calc_loop_triangles()
    ob=bpy.data.objects.new(key+'_Cabinet',me);scene.collection.objects.link(ob);ob.parent=root;parts=[ob]
    def panel(name,rect,inset,mat,thickness=.006):
        a,b,c,d=rect;xmin=(a-x0-width/2)*PX;xmax=(c-x0-width/2)*PX;zmin=(bottom-d)*PX+mount;zmax=(bottom-b)*PX+mount
        yf=-depth*PX/2+inset;yb=yf+thickness
        points=[(xmin,yf,zmin),(xmax,yf,zmin),(xmax,yb,zmin),(xmin,yb,zmin),(xmin,yf,zmax),(xmax,yf,zmax),(xmax,yb,zmax),(xmin,yb,zmax)]
        mm=G.Mesh()
        for inds in [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]:
            mm.face([points[i] for i in inds],[atlas_uv(state,a,d),atlas_uv(state,c,d),atlas_uv(state,c,b),atlas_uv(state,a,b)])
        p=mm.object(key+'_'+name,scene,mat,pivot=(xmin,yb,zmax));p.parent=root;parts.append(p);return p
    if display:
        panel('Display_Insert',display,2*PX-.008,mats[0])
        if family not in ['hotdog','sovietsoda']:panel('Display_Glass',display,PX*.6,GLASS,.012)
    if delivery:panel('Delivery_Flap',delivery,3*PX-.009,mats[0])
    if family=='hotdog':panel('Projecting_Center_Housing',(10,8,21,14),-.07,mats[0],.075)
    if family=='sovietsoda':panel('Raised_Sign',(8,7,23,16),-.04,mats[0],.045)
    # Source lighting stays an independent layer, split into strips matching recess depth.
    masks=[t['light_mask'] for t in CAT['vendors'][state]['types'] if t.get('light_mask') in STATES]
    if masks:
        mask=masks[0];pix=[0.]*(32*32*4)
        for y in range(32):
            for x in range(32):
                c=source_pixel(state,x,y);alpha=source_pixel(mask,x,y)[3]
                if not(x0<=x<right and top<=y<bottom):alpha=0
                pix[((31-y)*32+x)*4:((31-y)*32+x)*4+4]=list(c[:3])+[alpha]
        im=bpy.data.images.new('Vending_Lights_'+state,width=32,height=32,alpha=True);im.pixels.foreach_set(pix);path=OUT/'textures'/(key+'_lights.png');im.filepath_raw=str(path);im.file_format=G.enum(im,'file_format','PNG');im.save();im.pack()
        lm=material('Vending_Light_'+state,im,mask=True)
        cuts_x=sorted({x0,right}|{max(x0,min(right,r[i])) for r in [display,delivery] if r for i in [0,2]})
        cuts_y=sorted({top,bottom}|{max(top,min(bottom,r[i])) for r in [display,delivery] if r for i in [1,3]})
        for a,c in zip(cuts_x,cuts_x[1:]):
            for b,d in zip(cuts_y,cuts_y[1:]):
                if not any(pix[((31-y)*32+x)*4+3]>.4 for y in range(b,d) for x in range(a,c)):continue
                inset=2*PX-.010 if inside(display,a,b) else -.002
                if inside(delivery,a,b):inset=3*PX-.011
                panel('Light_Overlay_'+str(a)+'_'+str(b),(a,b,c,d),inset,lm,.001)
    bpy.context.view_layer.update()
    for p in parts:
        p.data.calc_loop_triangles()
        check=bmesh.new();check.from_mesh(p.data)
        assert all(e.is_manifold for e in check.edges) and all(v.is_manifold for v in check.verts),(state,p.name,'non-manifold')
        assert check.calc_volume(signed=True)>0,(state,p.name,'non-positive volume')
        check.free()
        signature=(family,tuple(tuple(round(c,6) for c in v.co) for v in p.data.vertices),tuple((tuple(q.vertices),q.material_index) for q in p.data.polygons),tuple(tuple(round(c,7) for c in q.uv) for q in p.data.uv_layers.active.data))
        assigned=[s.material for s in p.material_slots]
        if signature in MESH_CACHE:
            p.data=MESH_CACHE[signature]
            for i,m in enumerate(assigned):p.material_slots[i].link='OBJECT';p.material_slots[i].material=m
        else:MESH_CACHE[signature]=p.data
    ASSETS[state]={'scene':scene,'root':root,'parts':parts,'key':key,'family':family}
    REPORT[state]={'key':key,'family':family,'source_rect':STATES[state]['rects'][0],'front_source_rect':[x0,top,right,bottom],'depth_m':depth*PX,'width_m':width*PX,'height_m':height*PX,'mount_height_m':mount,'display_rect':display,'delivery_rect':delivery,'triangles':sum(len(p.data.loop_triangles) for p in parts),'parts':[p.name for p in parts],'side_texture':TEXTURES[state],'closed_manifold':True,'positive_volume':True,'flat_shading':True}
    bounds=[p.matrix_world@Vector(v) for p in parts for v in p.bound_box]
    REPORT[state]['dimensions_m']=[max(v[i] for v in bounds)-min(v[i] for v in bounds) for i in range(3)]
    print('BUILT',state,REPORT[state]['triangles'])

def build_batch(names):
    for state in names:build(state)

def export_batch(names):
    for state in names:
        a=ASSETS[state];bpy.context.window.scene=a['scene'];bpy.ops.object.select_all(action='SELECT');bpy.context.view_layer.objects.active=a['parts'][0]
        path=OUT/(a['key']+'.glb');bpy.ops.export_scene.gltf(filepath=str(path),use_selection=True,use_active_scene=True,export_animations=False,export_extras=True)
        blob=path.read_bytes();n=struct.unpack_from('<I',blob,12)[0];g=json.loads(blob[20:20+n]);tail=blob[20+n:]
        for s in g.get('samplers',[]):s.update(minFilter=9728,magFilter=9728,wrapS=33071,wrapT=33071)
        for m in g['materials']:
            if m['name'].startswith('Vending_Light_'):m['alphaMode']='MASK';m['alphaCutoff']=.4
            m['doubleSided']=False
        raw=json.dumps(g,separators=(',',':')).encode();raw+=b' '*(-len(raw)%4)
        path.write_bytes(struct.pack('<III',0x46546c67,2,20+len(raw)+len(tail))+struct.pack('<II',len(raw),0x4e4f534a)+raw+tail)
        bpy.data.libraries.write(str(path.with_suffix('.blend')),{a['scene']},path_remap='RELATIVE',fake_user=True,compress=True)
    (OUT/'model_info.json').write_text(json.dumps(REPORT,indent=2)+'\n')
    print('EXPORTED',len(names))

bpy.app.driver_namespace['vending_family']=globals()
print('Ready:',len(CAT['vendors']),'appearances')
