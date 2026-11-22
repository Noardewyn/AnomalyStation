"""Reference-first station props, generated in the live Blender via MCP.

Creates isolated asset scenes and exports only this batch. No gameplay integration.
"""
from pathlib import Path
import bpy,bmesh,json,struct,hashlib,math,sys,importlib.util,array,collections
from mathutils import Vector,Matrix

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'godot/assets/models/station_props'
AUDIT=json.loads((OUT/'source_audit.json').read_text())
spec=importlib.util.spec_from_file_location('station_prop_geometry',ROOT/'tools/art/station_prop_geometry.py')
G=importlib.util.module_from_spec(spec);spec.loader.exec_module(G)
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
SOURCES={};MATERIALS={};ASSETS={};CACHE={};SCENE=None;ROOTOB=None;KEY=None


def source(key):
    if key not in SOURCES:
        if key in ('portable_gags','vendor_lights'):
            path=OUT/'textures'/('portable_canister_gags.png' if key=='portable_gags' else 'vending_light_layers.png')
            variants=json.loads((OUT/('canister_gags_variants.json' if key=='portable_gags' else 'vending_light_layers.json')).read_text())['variants']
            states={v['name']:{'name':v['name'],'frames':1,'dirs':1,'rects':[v['rect']]} for v in variants}
        else:
            rec=AUDIT['sources'][key];path=ROOT/rec['png'];states={s['name']:s for s in rec['states']}
        im=bpy.data.images.load(str(path),check_existing=True);im.pack()
        pixels=array.array('f',[0])*(im.size[0]*im.size[1]*4);im.pixels.foreach_get(pixels)
        SOURCES[key]={'key':key,'path':path,'image':im,'pixels':pixels,'states':states,'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
    return SOURCES[key]


def select(key,state,direction=0):
    G.SOURCE=source(key);G.STATE=state;G.DIR=direction;G.EXCLUDE=None
    return tile_material(key,state,direction)


def tile_material(key,state,direction=0,emission=0,mask=False,color_state=None):
    cache=(key,state,direction,emission,mask,color_state)
    if cache in MATERIALS:return MATERIALS[cache]
    src=source(key);s=src['states'][state];r=s['rects'][direction*s['frames']];w,h=src['image'].size
    label=('Overlay_' if mask else 'Atlas_')+key+'_'+state.replace('/','_')+('_D'+str(direction) if direction else '')
    m=bpy.data.materials.new(label);m.use_nodes=True;m.use_backface_culling=True
    bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    bs.inputs['Roughness'].default_value=.78;bs.inputs['Specular IOR Level'].default_value=.12
    t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=src['image'];t.interpolation=G.enum(t,'interpolation','Closest')
    mapping=m.node_tree.nodes.new('ShaderNodeMapping');coord=m.node_tree.nodes.new('ShaderNodeTexCoord')
    mapping.inputs['Scale'].default_value=(32/w,32/h,1)
    mapping.inputs['Location'].default_value=(r[0]/w,1-(r[1]+32)/h,0)
    m.node_tree.links.new(coord.outputs['UV'],mapping.inputs['Vector']);m.node_tree.links.new(mapping.outputs['Vector'],t.inputs['Vector'])
    color_texture=t
    if color_state:
        cr=src['states'][color_state]['rects'][0]
        color_texture=m.node_tree.nodes.new('ShaderNodeTexImage');color_texture.image=src['image']
        color_texture.interpolation=G.enum(color_texture,'interpolation','Closest')
        cm=m.node_tree.nodes.new('ShaderNodeMapping');cm.inputs['Scale'].default_value=(32/w,32/h,1)
        cm.inputs['Location'].default_value=(cr[0]/w,1-(cr[1]+32)/h,0)
        m.node_tree.links.new(coord.outputs['UV'],cm.inputs['Vector']);m.node_tree.links.new(cm.outputs['Vector'],color_texture.inputs['Vector'])
        m['color_state']=color_state
    m.node_tree.links.new(color_texture.outputs['Color'],bs.inputs['Base Color'])
    if emission:
        m.node_tree.links.new(color_texture.outputs['Color'],bs.inputs['Emission Color']);bs.inputs['Emission Strength'].default_value=emission
    if mask:
        m.node_tree.links.new(t.outputs['Alpha'],bs.inputs['Alpha']);m.surface_render_method=G.enum(m,'surface_render_method','DITHERED')
    m['atlas_state']=state;m['source_png_sha256']=src['sha256'];m['tile_rect_px']=r
    m['source_png']=str(src['path'].relative_to(ROOT)).replace('\\','/')
    MATERIALS[cache]=m;return m


def solid_material(name,color,alpha=1,metallic=0,roughness=.7):
    cache=('solid',name)
    if cache in MATERIALS:return MATERIALS[cache]
    m=bpy.data.materials.new(name);m.use_nodes=True;m.use_backface_culling=True
    bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    bs.inputs['Base Color'].default_value=tuple(color)+(1,);bs.inputs['Alpha'].default_value=alpha
    bs.inputs['Roughness'].default_value=roughness;bs.inputs['Metallic'].default_value=metallic
    if alpha<1:m.surface_render_method=G.enum(m,'surface_render_method','BLENDED')
    m['closed_transparent_solid']=alpha<1;MATERIALS[cache]=m;return m


GLASS=solid_material('Glass_Slight_Cyan_Closed',(.32,.55,.61),.27,0,.16)
WATER=solid_material('Water_Slight_Blue_Closed',(.04,.18,.32),.68,0,.22)
MIRROR=solid_material('Mirror_Polished_Reflection',(.72,.79,.82),1,1,.08)


def begin(key,atlas,state,category,description):
    global SCENE,ROOTOB,KEY
    KEY=key;SCENE=bpy.data.scenes.new('Props_'+key);bpy.context.window.scene=SCENE
    SCENE.unit_settings.system=G.enum(SCENE.unit_settings,'system','METRIC')
    ROOTOB=bpy.data.objects.new(key,None);SCENE.collection.objects.link(ROOTOB)
    ROOTOB['source_atlas']=atlas;ROOTOB['source_state']=state;ROOTOB['description']=description
    ASSETS[key]={'scene':SCENE,'root':ROOTOB,'parts':[],'source':atlas,'state':state,'category':category,'description':description}
    select(atlas,state)


def sample(region=None):
    # Most frequent opaque texel in the requested source patch.
    x0,y0,x1,y1=region or (0,0,32,32)
    colors=collections.defaultdict(list)
    for y in range(y0,y1):
        for x in range(x0,x1):
            rgba=G.pixel(G.STATE,x,y)
            if rgba[3]>.99:colors[tuple(round(v,4) for v in rgba)].append((x,y))
    assert colors,(G.SOURCE['key'],G.STATE,region)
    return max(colors.values(),key=len)[0]


def add(mesh,name,mat=None,pivot=(0,0,0),parent=None,share=True):
    mat=mat or tile_material(G.SOURCE['key'],G.STATE,G.DIR)
    ob=mesh.object(KEY+'_'+name,SCENE,mat,pivot)
    ob.parent=parent or ROOTOB
    if parent:
        bpy.context.view_layer.update();ob.location-=parent.matrix_world.translation
    ob['part_name']=name
    # Tile-local UVs permit real mesh sharing between atlas/material variants.
    if share:
        signature=(tuple(tuple(round(c,6) for c in v.co) for v in ob.data.vertices),
                   tuple(tuple(p.vertices) for p in ob.data.polygons),
                   tuple(tuple(round(c,6) for c in v.uv) for v in ob.data.uv_layers.active.data))
        if signature in CACHE:
            old=ob.data;ob.data=CACHE[signature];ob.material_slots[0].link='OBJECT';ob.material_slots[0].material=mat
            bpy.data.meshes.remove(old)
        else:CACHE[signature]=ob.data
    ASSETS[KEY]['parts'].append(ob);return ob


def box(name,bounds,patch=None,texel=None,mat=None,pivot=(0,0,0),parent=None):
    m=G.Mesh();m.box(bounds,G.STATE,texel or sample(),front=patch)
    return add(m,name,mat,pivot,parent)


def profile(name,rect,transform,thickness,texel=None,mat=None,pivot=(0,0,0),parent=None,exclude=None):
    texel=texel or sample(rect);G.EXCLUDE=exclude
    m=G.Mesh();m.profile(G.STATE,rect,transform,thickness,texel)
    G.EXCLUDE=None
    return add(m,name,mat,pivot,parent)


def prism(name,width,depth,z0,z1,step=.025,patch=None,texel=None,mat=None,center=(0,0),pivot=(0,0,0),parent=None):
    m=G.Mesh();plan=[(x+center[0],y+center[1]) for x,y in G.stepped_rectangle(width,depth,step)]
    G.prism(m,plan,z0,z1,G.STATE,texel or sample(),patch)
    return add(m,name,mat,pivot,parent)


def basin(name,width,depth,z0,z1,floor,wall=.035,center=(0,0),texel=None,inside=None,mat=None):
    m=G.Mesh();step=min(width,depth)*.1
    outer=[(x+center[0],y+center[1]) for x,y in G.stepped_rectangle(width,depth,step)]
    inner=[(x+center[0],y+center[1]) for x,y in G.stepped_rectangle(width-2*wall,depth-2*wall,step*.6)]
    G.vessel(m,outer,inner,z0,z1,floor,G.STATE,texel or sample(),inside)
    return add(m,name,mat)


def pivot(name,location):
    ob=bpy.data.objects.new(KEY+'_'+name,None);SCENE.collection.objects.link(ob);ob.parent=ROOTOB;ob.location=location
    ob['part_name']=name;return ob


def decal(name,state,transform,rect=None,emission=0,direction=0,thick=.001,color_state=None):
    atlas=G.SOURCE['key'];select(atlas,state,direction)
    mat=tile_material(atlas,state,direction,emission,True,color_state)
    if rect is None:
        visible=[(x,y) for y in range(32) for x in range(32) if G.pixel(state,x,y)[3]>.001]
        assert visible,(atlas,state)
        rect=(min(p[0] for p in visible),min(p[1] for p in visible),max(p[0] for p in visible)+1,max(p[1] for p in visible)+1)
    # Sprite overlays can contain only partially opaque pixels. Preserve their alpha
    # in a closed, thin carrier rather than discarding the original mask values.
    x0,y0,x1,y1=rect;corners=[(x0,y0),(x1,y0),(x1,y1),(x0,y1)]
    m=G.Mesh();coords=[G.uv(state,x,y) for x,y in corners]
    m.face([transform(x,y,0) for x,y in corners],coords)
    m.face([transform(x,y,thick) for x,y in corners],coords)
    transparent=next(((x,y) for y in range(32) for x in range(32) if G.pixel(state,x,y)[3]<.001),(0,0))
    edgeuv=G.uv(state,transparent[0]+.5,transparent[1]+.5)
    for a,b in zip(corners,corners[1:]+corners[:1]):m.face([transform(*a,0),transform(*b,0),transform(*b,thick),transform(*a,thick)],[edgeuv]*4)
    ob=add(m,name,mat);ob['source_overlay']=state
    return ob


# Storage rack: four usable shelves rather than a textured solid block.
begin('storage_rack','structures','rack','furniture','Open steel rack with four supported shelves')
steel=sample((6,8,27,25))
for i,z in enumerate((.13,.39,.65,.91)):
    prism('Shelf_'+str(i+1),.90,.62,z-.035,z,.02,patch=(5,23,28,27),texel=steel)
for sx in (-1,1):
    for sy in (-1,1):box('Upright_'+str(sx)+'_'+str(sy),(sx*.414-.022,sx*.414+.022,sy*.272-.022,sy*.272+.022,0,1.02),texel=steel)
for z in (.13,.39,.65,.91):
    box('Rear_Shelf_Rail_'+str(z),(-.414,.414,.264,.288,z+.02,z+.045),texel=steel)


# Solar pedestal and installed panel, with reusable yaw/tilt pivots.
def solar_base(key,installed):
    begin(key,'solar','sp_base','power','Solar pedestal with separated panel layers' if installed else 'Unfinished solar pedestal')
    blue=sample((12,23,19,28));metal=sample((10,20,22,24))
    box('North_South_Foot',(-.07,.07,-.40,.40,0,.055),texel=blue)
    box('East_West_Foot',(-.40,.40,-.07,.07,.01,.065),texel=blue)
    box('Pedestal',(-.07,.07,-.07,.07,.05,.58),texel=blue,patch=(14,8,18,23))
    profile('Power_Cable',(11,0,21,16),lambda x,y,t:((x-16)*.018,-.082-t,.61-y*.022),.018)
    if installed:
        yaw=pivot('Panel_Yaw_Pivot',(0,0,.61));tilt=pivot('Panel_Tilt_Pivot',(0,0,.64))
        # Parent while retaining the world position.
        tilt.parent=yaw;tilt.location=(0,0,.03)
        box('Panel_Swivel_Hub',(-.055,.055,-.055,.055,.565,.635),texel=blue,parent=yaw)
        select('solar','solar_panel_glass_edge')
        panel=profile('Panel_Backplate',(2,2,30,30),lambda x,y,t:((x-16)*.046,(16-y)*.046,.64-t),.035,parent=tilt,pivot=(0,0,.64))
        select('solar','solar_panel_glass')
        profile('Photovoltaic_Cells',(2,2,30,30),lambda x,y,t:((x-16)*.046,(16-y)*.046,.647-t),.006,parent=tilt,pivot=(0,0,.64))
        prism('Glass_Cover',1.288,1.288,.647,.653,.046,mat=GLASS,parent=tilt,pivot=(0,0,.64))
solar_base('solar_panel',True);solar_base('solar_panel_frame',False)


# Holopad is a shallow floor machine; active/ringing masks remain independent.
for key,state in [('holopad','holopad0'),('holopad_active','holopad1')]:
    begin(key,'floor',state,'machines','Thin floor holopad with separate projected-light mask')
    profile('Floor_Housing',(0,0,32,32),lambda x,y,t:((x-16)*.027,(16-y)*.027,.055-t),.055)
    if state=='holopad1':
        emitter=decal('Emitter_Overlay','holopad1_mask',lambda x,y,t:((x-16)*.027,(16-y)*.027,.058+t),emission=.6)
        mat=emitter.material_slots[0].material
        shader=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
        tex=next(n for n in mat.node_tree.nodes if n.type=='TEX_IMAGE')
        tint=mat.node_tree.nodes.new('ShaderNodeMixRGB');tint.blend_type=G.enum(tint,'blend_type','MULTIPLY');tint.inputs[0].default_value=1
        tint.inputs[2].default_value=tuple(((c/255+.055)/1.055)**2.4 for c in (125,180,225))+(1,)
        mat.node_tree.links.new(tex.outputs['Color'],tint.inputs[1])
        mat.node_tree.links.new(tint.outputs[0],shader.inputs['Base Color']);mat.node_tree.links.new(tint.outputs[0],shader.inputs['Emission Color'])
        mat['source_tint']='COLOR_AI_HOLOGRAM_BLUE #7DB4E1'


# All 39 machine states present in MetaStation, not refill items or unrelated variants.
for state in AUDIT['mapped_vendor_states']:
    key='vending_'+state.lower()
    begin(key,'vending',state,'vending','Mapped vendor with separate display, controls and illumination')
    bbox=(0,0,32,32)
    # The cabinet front follows the original alpha silhouette at one pixel per 5 cm.
    profile('Cabinet',bbox,lambda x,y,t:((x-16)*.05,-.27+t,(32-y)*.05),.54)
    # Display and controls retain the exact base image, isolated into separate surfaces.
    profile('Display_Layer',(6,9,23,25),lambda x,y,t:((x-16)*.05,-.274+t,(32-y)*.05),.002)
    profile('Control_Layer',(23,9,29,27),lambda x,y,t:((x-16)*.05,-.278+t,(32-y)*.05),.002)
    profile('Delivery_Flap',(6,27,23,31),lambda x,y,t:((x-16)*.05,-.289+t,(32-y)*.05),.008,pivot=(0,-.289,.25))
    info=AUDIT['mapped_vending'][state][0];light=info.get('light_mask')
    if light in source('vending')['states']:
        select('vendor_lights',state)
        layer=decal('Illumination_Overlay',state,lambda x,y,t:((x-16)*.05,-.285-t,(32-y)*.05),emission=.12)
        layer['original_light_mask']=light
    ROOTOB['source_light_mask']=light or '';ROOTOB['source_panel_type']=info.get('panel_type') or ''


# Disposal unit: open, lined hopper with hazard lip and a separate pull lever.
begin('disposal_unit','disposal','disposal','utility','Disposal with deep hopper, hazard rim and separate flush lever')
body=sample((9,16,24,29));dark=sample((9,3,24,10))
basin('Hopper_Housing',.62,.55,0,.91,.60,.055,texel=body,inside=dark)
box('Front_Control_Panel',(-.31,.31,-.278,-.254,.05,.57),patch=(8,15,24,28),texel=body)
profile('Hazard_Rear_Rim',(8,6,24,7),lambda x,y,t:((x-16)*.03875,.225-(y-6)*.025,.92-t),.025)
for sx in (-1,1):box('Hazard_Side_Rim_'+str(sx),(sx*.285-.025,sx*.285+.025,-.24,.23,.885,.925),texel=sample((8,6,11,14)))
lever=box('Flush_Lever',(.12,.27,-.32,-.275,.50,.56),texel=sample((19,15,25,19)),pivot=(.12,-.28,.53))
decal('Ready_Indicator','dispover-ready',lambda x,y,t:((x-16)*.03875,-.29-t,.91-(y-6)*.04136),emission=.4)


# Bathroom fixtures use real cavities and closed porcelain thickness.
def sink(key,state,width,depth):
    begin(key,'plumbing',state,'bathroom','Open ceramic basin, faucet and drain pipe')
    white=sample((10,5,21,13));inside=sample((12,8,20,11));metal=sample((14,1,19,6))
    basin('Ceramic_Basin',width,depth,.68,.84,.72,.035,texel=white,inside=inside)
    box('Drain_Pipe',(-.032,.032,.07,.13,.36,.72),texel=sample((13,13,18,20)))
    box('Wall_Drain_Elbow',(-.032,.032,.10,.32,.35,.414),texel=sample((13,13,18,20)))
    box('Faucet_Riser',(-.025,.025,depth/2-.06,depth/2-.01,.835,1.0),texel=metal)
    box('Faucet_Spout',(-.025,.025,.01,depth/2-.01,.95,1.0),texel=metal)
    for sx in (-1,1):box('Tap_'+str(sx),(sx*.12-.028,sx*.12+.028,depth/2-.09,depth/2-.025,.84,.88),texel=metal,pivot=(sx*.12,depth/2-.055,.86))
sink('washbasin','sink',.52,.40);sink('kitchen_sink','sink_alt',.76,.49)

begin('wall_shower','plumbing','shower','bathroom','Wall shower with stepped pipe elbows, valve and adjustable head')
metal=sample((14,3,18,18))
box('Wall_Valve_Box',(-.09,.09,.13,.22,.98,1.09),patch=(12,16,20,20),texel=metal)
box('Riser_Pipe',(-.025,.025,.15,.20,1.06,1.98),texel=metal)
box('Upper_Elbow',(-.025,.025,-.08,.20,1.93,1.98),texel=metal)
prism('Shower_Head',.23,.19,1.88,1.94,.035,texel=metal,center=(0,-.10),pivot=(0,-.06,1.94))
box('Temperature_Control',(-.045,.045,.105,.14,1.0,1.075),texel=metal,pivot=(0,.125,1.0375))

begin('toilet','plumbing','toilet00','bathroom','Toilet with lined bowl, hinged seat lid and separate cistern lid')
white=sample((11,5,21,12));shade=sample((11,22,21,27))
prism('Pedestal',.30,.40,0,.30,.045,texel=shade,center=(0,-.03))
basin('Porcelain_Bowl',.46,.59,.24,.45,.285,.045,center=(0,-.045),texel=white,inside=white)
prism('Bowl_Water',.23,.29,.286,.296,.03,mat=WATER,center=(0,-.06))
box('Cistern',(-.22,.22,.19,.38,.36,.80),patch=(10,3,22,11),texel=white)
box('Cistern_Lid',(-.235,.235,.18,.39,.80,.835),texel=white,pivot=(-.235,.39,.80))
profile('Seat_Lid',(10,12,22,24),lambda x,y,t:((x-16)*.034,.19-(y-12)*.039,.474-t),.025,texel=white,pivot=(0,.19,.46))
ROOTOB['hinge_axis']='Seat_Lid local X; rotate -90 degrees to raise'

begin('urinal','plumbing','urinal','bathroom','Wall-mounted urinal with recessed and lined opening')
white=sample((9,7,23,29));inside=sample((13,12,20,27))
profile('Ceramic_Frame',(7,5,25,32),lambda x,y,t:((x-16)*.027,-.13+t,1.10-(y-5)*.025),.06,texel=white,
        exclude=lambda x,y:11<=x<21 and 10<=y<28)
box('Recess_Back',(-.135,.135,-.02,.10,.50,.975),texel=inside,patch=(11,10,21,28))
box('Recess_Left',(-.135,-.108,-.13,.04,.50,.975),texel=white)
box('Recess_Right',(.108,.135,-.13,.04,.50,.975),texel=white)
box('Recess_Floor',(-.13,.13,-.13,.06,.48,.52),texel=white)
box('Flush_Pipe',(-.025,.025,.04,.09,1.02,1.25),texel=white)
box('Flush_Valve',(-.065,.065,.01,.11,1.20,1.26),texel=white,pivot=(0,.06,1.23))

begin('wall_mirror','plumbing','mirror','bathroom','Stepped oval wall mirror with original highlight pixels')
mirror_shader=next(n for n in tile_material('plumbing','mirror').node_tree.nodes if n.type=='BSDF_PRINCIPLED')
mirror_shader.inputs['Metallic'].default_value=.1;mirror_shader.inputs['Roughness'].default_value=.10
profile('Mirror_Frame',(9,4,23,23),lambda x,y,t:((x-16)*.035,.01+t,1.65-(y-4)*.035),.025)
profile('Reflective_Face',(10,5,22,22),lambda x,y,t:((x-16)*.035,.005+t,1.65-(y-4)*.035),.003)
ROOTOB['reflection']='Original sprite highlight texture; polished material is available in the library; no runtime reflection probe'


# Chemical dispensers: two bottles, open trolley frame and separate tap hardware.
for key,state in [('water_tank','water'),('fuel_tank','fuel')]:
    begin(key,'chemical_tanks',state,'tanks','Twin-vessel reagent trolley reconstructed from the original sprite')
    steel=sample((2,24,28,31));body=sample((18,7,27,13))
    box('Trolley_Base',(-.44,.44,-.29,.29,.06,.115),texel=steel,patch=(2,26,29,30))
    for sx in (-1,1):
        for sy in (-1,1):box('Trolley_Wheel_'+str(sx)+'_'+str(sy),(sx*.38-.035,sx*.38+.035,sy*.23-.04,sy*.23+.04,0,.08),texel=steel,pivot=(sx*.38,sy*.23,.04))
    profile('Left_Vessel',(3,4,15,26),lambda x,y,t:((x-16)*.032,-.16+t,.94-(y-4)*.035),.34)
    profile('Right_Vessel',(17,4,29,26),lambda x,y,t:((x-16)*.032,-.16+t,.94-(y-4)*.035),.34)
    for sx in (-1,1):
        box('Frame_Post_'+str(sx),(sx*.43-.022,sx*.43+.022,-.23,-.185,.10,.64),texel=steel)
    box('Front_Crossbar',(-.43,.43,-.235,-.19,.13,.17),texel=steel)
    box('Tap',(.17,.25,-.25,-.16,.43,.51),texel=sample((19,16,25,22)),pivot=(.21,-.16,.47))


# Real GAGS portable canister variants share meshes, using a separately saved derived sheet.
canvars=json.loads((OUT/'canister_gags_variants.json').read_text())['variants']
for variant in canvars:
    state=variant['name'];begin('portable_canister_'+state,'portable_gags',state,'tanks','Portable gas canister, original GAGS composition')
    steel=sample((8,16,24,28))
    profile('Vessel_Body',(7,10,25,30),lambda x,y,t:((x-16)*.036,-.22+t,.84-(y-10)*.039),.44)
    profile('Regulator_Head',(9,4,23,11),lambda x,y,t:((x-16)*.036,-.15+t,1.08-(y-4)*.034),.30)
    box('Handle_Top',(-.25,.25,.10,.15,1.11,1.16),texel=sample((10,3,22,6)))
    for sx in (-1,1):box('Handle_Post_'+str(sx),(sx*.225-.025,sx*.225+.025,.10,.15,.94,1.13),texel=steel)
    box('Valve_Control',(-.06,.06,-.19,-.14,.92,.99),texel=steel,pivot=(0,-.16,.955))
    for sx in (-1,1):box('Foot_'+str(sx),(sx*.18-.04,sx*.18+.04,-.17,.17,0,.07),texel=steel)
    ROOTOB['gas_type']=variant['type'];ROOTOB['gags_colors']=variant['colors']


# Stationary tanks form one continuous square-grid vessel. Connected sides have no border.
# Canonical modules are rotated, never duplicated by direction.
TANK_MODULES={'single':set(),'end':{'N'},'straight':{'N','S'},'corner':{'N','E'},'tee':{'N','E','W'},'center':{'N','S','E','W'}}
def tank_plan(connections):
    plan=[];s=.04
    for x,y,a,b in [(-.45,-.45,'S','W'),(.45,-.45,'S','E'),(.45,.45,'N','E'),(-.45,.45,'N','W')]:
        if a in connections or b in connections:plan.append((x,y));continue
        if x<0 and y<0:plan.extend([(x,y+s),(x+s,y+s),(x+s,y)])
        elif x>0 and y<0:plan.extend([(x-s,y),(x-s,y+s),(x,y+s)])
        elif x>0 and y>0:plan.extend([(x,y-s),(x-s,y-s),(x-s,y)])
        else:plan.extend([(x+s,y),(x+s,y-s),(x,y-s)])
    return plan
for suffix,connections in TANK_MODULES.items():
    begin('stationary_tank_'+suffix,'stationary','canister-0','stationary_tanks','0.90 m reusable connected pressure-tank module')
    white=sample((6,8,26,25));grid=.90
    # Solid top/bottom slabs meet precisely; only exterior faces receive edging/window bands.
    for name,z0,z1 in [('Lower_Core',.04,.39),('Upper_Core',.68,1.10)]:
        m=G.Mesh();G.prism(m,tank_plan(connections),z0,z1,G.STATE,white);add(m,name)
    for sx,sy,a,b in [(-1,-1,'S','W'),(1,-1,'S','E'),(1,1,'N','E'),(-1,1,'N','W')]:
        if not ({a,b}&connections):
            box('Foot_'+a+b,(sx*.36-.045,sx*.36+.045,sy*.36-.045,sy*.36+.045,0,.06),texel=white)
            for n,(xa,xb,ya,yb) in enumerate([(.37,.41,.37,.45),(.41,.45,.37,.41)]):
                xx=sorted((sx*xa,sx*xb));yy=sorted((sy*ya,sy*yb))
                box('Corner_Lining_'+a+b+'_'+str(n),(xx[0],xx[1],yy[0],yy[1],.38,.69),texel=white)
    for side in sorted(set('NSEW')-connections):
        # Every side is a rotated reusable lined wall with a small glass viewing band.
        angle={'S':0,'E':math.pi/2,'N':math.pi,'W':-math.pi/2}[side]
        left,right={'S':('W','E'),'E':('S','N'),'N':('E','W'),'W':('N','S')}[side]
        edge=pivot('Exterior_'+side,(0,0,0));edge.rotation_euler.z=angle
        edge0=-.45 if left in connections else -.41;edge1=.45 if right in connections else .41
        box('Lower_Edge_'+side,(edge0,edge1,-.45,-.42,.05,.415),texel=sample((6,24,26,28)),parent=edge)
        box('Upper_Edge_'+side,(edge0,edge1,-.45,-.42,.66,1.08),texel=white,parent=edge)
        box('Viewing_Band_'+side,(-.45 if left in connections else -.37,.45 if right in connections else .37,-.452,-.438,.405,.665),texel=white,mat=GLASS,parent=edge)
        if left not in connections:box('Window_Jamb_'+side+'_Left',(-.41,-.37,-.452,-.42,.39,.68),texel=white,parent=edge)
        if right not in connections:box('Window_Jamb_'+side+'_Right',(.37,.41,-.452,-.42,.39,.68),texel=white,parent=edge)
        select('stationary_misc','knob',{'S':0,'N':1,'E':2,'W':3}[side])
        box('Pipe_Port_'+side,(-.045,.045,-.50,-.43,.13,.22),texel=sample(),parent=edge)
        select('stationary','canister-0')
    ROOTOB['grid_m']=.90;ROOTOB['connections_canonical']=sorted(connections)


# Bed and stretcher: solid mattresses, independent pillow/rails and supported underframes.
for key,atlas,state,medical in [('station_bed','bed','bed',False),('emergency_stretcher','medical_bed','emerg_down',True)]:
    begin(key,atlas,state,'medical','Emergency wheeled stretcher' if medical else 'Station bed with separate pillow and mattress')
    height=.76 if medical else .55;steel=sample((2,28,29,32))
    box('Underframe',(-.93,.93,-.36,.36,height-.17,height-.10),texel=steel,patch=(1,27,30,30))
    prism('Mattress',1.80,.70,height-.10,height,.025,patch=(2 if medical else 1,16,29 if medical else 22,26),texel=sample((7,17,23,25)))
    head=-.72 if medical else .72
    prism('Pillow',.30,.60,height,height+.075,.03,patch=(3,15,7,24) if medical else (24,16,28,23),center=(head,0))
    for sx in (-1,1):
        for sy in (-1,1):
            box('Leg_'+str(sx)+'_'+str(sy),(sx*.81-.025,sx*.81+.025,sy*.29-.025,sy*.29+.025,.07 if medical else 0,height-.12),texel=steel)
            if medical:box('Caster_'+str(sx)+'_'+str(sy),(sx*.81-.055,sx*.81+.055,sy*.29-.025,sy*.29+.025,0,.10),texel=steel,pivot=(sx*.81,sy*.29,.05))
    for sy in (-1,1):
        z=height-.12 if medical else height+.05
        rail=box('Side_Rail_'+str(sy),(-.74,.74,sy*.365-.018,sy*.365+.018,z-.018,z+.018),texel=steel,pivot=(-.74,sy*.365,height))
        if medical:
            for sx in (-1,1):box('Rail_Post_'+str(sx)+'_'+str(sy),(sx*.69-.015,sx*.69+.015,sy*.365-.018,sy*.365+.018,height-.17,z+.015),texel=steel)


begin('hydroponics_tray','hydroponics','hydrotray3','hydroponics','Open hydroponics basin with separate control face and no plants')
metal=sample((2,18,30,25));interior=sample((5,20,27,23))
basin('Growing_Basin',.90,.70,.53,.75,.565,.04,texel=metal,inside=interior)
box('Front_Control_Assembly',(-.42,.42,-.37,-.34,.39,.53),patch=(2,26,30,31),texel=sample((3,27,29,31)))
for sx in (-1,1):
    for sy in (-1,1):box('Support_'+str(sx)+'_'+str(sy),(sx*.36-.025,sx*.36+.025,sy*.26-.025,sy*.26+.025,0,.56),texel=metal)
ROOTOB['overlay_mount']='Growing_Basin; plant and soil source overlays remain separate future content'

begin('soil_bed','hydroponics','soil','hydroponics','Low soil patch with original irregular stepped silhouette')
profile('Earth',(0,17,32,32),lambda x,y,t:((x-16)*.028125,.45-(y-17)/15*.90,.08-t),.08)


# Suit storage: lined chamber, two hinged glazed leaves, controls and original gear layers.
begin('suit_storage_classic','suit_storage','classic','machines','Classic suit storage with separately pivoted glazed doors and gear layers')
gold=sample((10,1,22,8));steel=sample((8,10,23,26))
prism('Base_Plinth',.90,.74,0,.16,.06,patch=(10,27,21,30),texel=gold)
prism('Top_Cap',.90,.74,1.64,1.79,.06,patch=(10,3,22,7),texel=gold)
box('Rear_Lining',(-.38,.38,.29,.35,.14,1.65),texel=steel)
for sx in (-1,1):box('Chamber_Side_'+str(sx),(sx*.40-.035,sx*.40+.035,-.32,.32,.14,1.65),texel=steel)
for sx in (-1,1):
    hinge=pivot('Door_Hinge_'+str(sx),(sx*.37,-.34,.16))
    x0,x1=(-.37,0) if sx<0 else (0,.37)
    box('Door_Glass_'+str(sx),(x0+.025,x1-.025,-.352,-.338,.20,1.61),texel=steel,mat=GLASS,parent=hinge)
    for x in (x0,x1):box('Door_Vertical_Rail_'+str(sx)+'_'+str(x),(x-.018,x+.018,-.367,-.328,.16,1.65),texel=steel,parent=hinge)
    for z in (.17,1.63):box('Door_Crossbar_'+str(sx)+'_'+str(z),(x0,x1,-.367,-.328,z-.018,z+.018),texel=gold,parent=hinge)
box('Control_Housing',(.44,.63,-.19,.15,.63,1.29),patch=(25,13,30,24),texel=gold)
for state,name in [('classic_suit','Stored_Suit_Overlay'),('classic_helm','Stored_Helmet_Overlay'),('classic_storage','Boot_Storage_Overlay')]:
    decal(name,state,lambda x,y,t:((x-15.5)*.052,.04+t,1.79-y*.055),thick=.025)
select('suit_storage','classic')
decal('Status_Lights','classic_lights_closed',lambda x,y,t:((x-15.5)*.052,-.375-t,1.79-y*.055),emission=.6)


# P.A.C.M.A.N. split into the two source-shaped housings, feet and access hardware.
begin('pacman_generator','engine','portgen0_0','power','P.A.C.M.A.N. portable generator with twin engine housings')
steel=sample((5,8,24,18));purple=sample((4,23,9,28))
for sx,rect in [(-1,(1,5,15,30)),(1,(15,5,29,30))]:
    profile('Engine_Housing_'+str(sx),rect,lambda x,y,t:((x-15)*.038,-.26+t,.95-(y-5)*.037),.52)
    for z in (.10,.24,.38):
        prism('Coil_'+str(sx)+'_'+str(z),.23,.56,z,z+.07,.035,texel=purple,center=(sx*.35,0))
box('Central_Bridge',(-.16,.16,-.22,.22,.40,.69),texel=steel)
box('Access_Panel',(-.12,.12,-.285,-.262,.27,.59),patch=(11,17,19,26),texel=steel,pivot=(-.12,-.27,.27))
box('Starter_Handle',(-.57,-.49,-.29,-.20,.54,.70),texel=sample((1,11,4,16)),pivot=(-.49,-.245,.62))


# The showcase type is a static deactivated cyborg, not a glass cabinet.
begin('old_cyborg_showcase','robot','robot_old','showcase','Static old cyborg exhibit with separated joints; no rig or animations')
scale=.055
parts=[('Head',(12,4,19,11),.26,(0,0,1.155)),('Torso',(10,11,21,22),.35,(0,0,.55)),
       ('Pelvis',(11,22,20,24),.27,(0,0,.44)),('Left_Upper_Arm',(7,13,10,18),.16,(-.39,0,1.045)),
       ('Left_Forearm',(7,18,10,22),.16,(-.39,0,.77)),('Right_Upper_Arm',(21,13,24,18),.16,(.39,0,1.045)),
       ('Right_Forearm',(21,18,24,22),.16,(.39,0,.77)),('Left_Thigh',(11,23,15,27),.22,(-.15,0,.495)),
       ('Left_Shin',(11,27,15,30),.22,(-.15,0,.275)),('Right_Thigh',(16,23,20,27),.22,(.15,0,.495)),
       ('Right_Shin',(16,27,20,30),.22,(.15,0,.275)),('Left_Foot',(9,29,15,32),.33,(-.16,-.07,.11)),
       ('Right_Foot',(16,29,22,32),.33,(.16,-.07,.11))]
for name,rect,depth,origin in parts:
    profile(name,rect,lambda x,y,t:((x-15.5)*scale,-depth/2+t,(32-y)*scale),depth,pivot=origin)
for name,rect in [('Left_Antenna',(12,3,13,4)),('Right_Antenna',(18,3,19,4))]:
    profile(name,rect,lambda x,y,t:((x-15.5)*scale,-.05+t,(32-y)*scale),.10)
select('robot','robot_old',1)
profile('Rear_Identification_Plate',(10,11,21,22),lambda x,y,t:((x-15.5)*scale,.183-t,(32-y)*scale),.006)


# Apply the connected-shell revision before any deliverable is exported.
revision_path=ROOT/'tools/art/revise_station_props.py'
revision={'__file__':str(revision_path)}
exec(compile(revision_path.read_text(encoding='utf-8'),str(revision_path),'exec'),revision)
revision['apply_all'](globals())


def export_asset(key,a):
    bpy.context.window.scene=a['scene'];bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='SELECT');bpy.context.view_layer.objects.active=a['parts'][0]
    path=OUT/a['category']/(key+'.glb');path.parent.mkdir(parents=True,exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=str(path),use_selection=True,use_active_scene=True,export_animations=False,export_extras=True)
    blob=path.read_bytes();n=struct.unpack_from('<I',blob,12)[0];g=json.loads(blob[20:20+n]);tail=blob[20+n:]
    for sampler in g.get('samplers',[]):sampler.update(minFilter=9728,magFilter=9728,wrapS=33071,wrapT=33071)
    for mat in g.get('materials',[]):
        mat['name']=mat['name'].split('.')[0]
        if mat['name'].startswith('Overlay_'):mat['alphaMode']='MASK';mat['alphaCutoff']=.4
        mat['doubleSided']=False
    for image in g.get('images',[]):
        view=g['bufferViews'][image['bufferView']];start=8+view.get('byteOffset',0)
        digest=hashlib.sha256(tail[start:start+view['byteLength']]).hexdigest()
        assert digest in {s['sha256'] for s in SOURCES.values()}
    assert not g.get('animations')
    raw=json.dumps(g,separators=(',',':')).encode();raw+=b' '*(-len(raw)%4)
    path.write_bytes(struct.pack('<III',0x46546c67,2,20+len(raw)+len(tail))+struct.pack('<II',len(raw),0x4e4f534a)+raw+tail)
    bpy.data.libraries.write(str(path.with_suffix('.blend')),{a['scene']},path_remap='RELATIVE',fake_user=True,compress=True)
    return path,g


REPORT={}
for key,a in ASSETS.items():
    path,g=export_asset(key,a)
    bounds=[o.matrix_world@Vector(c) for o in a['parts'] for c in o.bound_box]
    checks={'non_manifold_edges':0,'non_manifold_vertices':0,'zero_area_faces':0,'non_positive_volume_meshes':0}
    for ob in a['parts']:
        bm=bmesh.new();bm.from_mesh(ob.data)
        checks['non_manifold_edges']+=sum(not e.is_manifold for e in bm.edges)
        checks['non_manifold_vertices']+=sum(not v.is_manifold for v in bm.verts)
        checks['zero_area_faces']+=sum(f.calc_area()<1e-12 for f in bm.faces)
        checks['non_positive_volume_meshes']+=int(bm.calc_volume(signed=True)<=0);bm.free()
    assert not any(checks.values()),(key,checks)
    REPORT[key]={'file':str(path.relative_to(OUT)).replace('\\','/'),'atlas':a['source'],'state':a['state'],
        'description':a['description'],'category':a['category'],'meshes':len(a['parts']),
        'triangles':sum(len(o.data.loop_triangles) for o in a['parts']),
        'dimensions_m':[round(max(v[i] for v in bounds)-min(v[i] for v in bounds),4) for i in range(3)],
        'bounds_min_m':[round(min(v[i] for v in bounds),4) for i in range(3)],
        'bounds_max_m':[round(max(v[i] for v in bounds),4) for i in range(3)],
        'materials':[m['name'] for m in g.get('materials',[])],
        'parts':[o.get('part_name',o.name) for o in a['parts']],**checks}
(OUT/'model_info.json').write_text(json.dumps(REPORT,indent=2)+'\n',encoding='utf-8')


def make_preview(name,keys,columns,spacing=(1.9,2.2),camera_direction=(3,-7,5),render_size=(1600,1200)):
    scene=bpy.data.scenes.new(name);bpy.context.window.scene=scene
    world=bpy.data.worlds.new(name+'_World');world.use_nodes=True;scene.world=world
    bg=next(n for n in world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.08,.11,.15,1);bg.inputs['Strength'].default_value=.6
    for i,key in enumerate(keys):
        a=ASSETS[key];offset=Vector(((i%columns)*spacing[0],(i//columns)*spacing[1],0))
        # Preserve transformed module sides and independently pivoted parts.
        for original in a['parts']:
            ob=original.copy();ob.parent=None;ob.matrix_world=original.matrix_world.copy();ob.location+=offset
            ob.name='Preview_'+key+'_'+original.get('part_name','Part');scene.collection.objects.link(ob)
    rows=(len(keys)+columns-1)//columns
    target=Vector(((min(columns,len(keys))-1)*spacing[0]/2,(rows-1)*spacing[1]/2,.70))
    span=max(columns*spacing[0],rows*spacing[1])
    for label,loc,power in [('Key',(-3,-4,8),1800),('Fill',(span,-2,6),1200),('Rim',(2,span,7),1600)]:
        data=bpy.data.lights.new(name+'_'+label,'AREA');data.energy=power;data.size=max(5,span/2)
        ob=bpy.data.objects.new(data.name,data);scene.collection.objects.link(ob);ob.location=target+Vector(loc)
        ob.rotation_euler=(target-ob.location).to_track_quat('-Z','Y').to_euler()
    data=bpy.data.cameras.new(name+'_Camera');data.type=G.enum(data,'type','ORTHO');data.ortho_scale=span*1.28
    cam=bpy.data.objects.new(data.name,data);scene.collection.objects.link(cam);cam.location=target+Vector(camera_direction)*max(1,span/7)
    cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();scene.camera=cam
    scene.render.engine='BLENDER_EEVEE_NEXT';scene.render.resolution_x,scene.render.resolution_y=render_size;scene.render.resolution_percentage=100
    scene.view_settings.view_transform=G.enum(scene.view_settings,'view_transform','Standard')
    scene.render.image_settings.file_format=G.enum(scene.render.image_settings,'file_format','PNG')
    scene.render.filepath=str(ROOT/'docs/ai/art'/(name.lower()+'.png'))
    bpy.data.libraries.write(str(OUT/(name.lower()+'.blend')),{scene},path_remap='RELATIVE',fake_user=True,compress=True)
    return scene


PROP_KEYS=[k for k,a in ASSETS.items() if a['category'] not in ('vending','stationary_tanks') and (not k.startswith('portable_canister_') or k=='portable_canister_generic')]
PROP_PREVIEW=make_preview('Station_Props_Preview',PROP_KEYS,5,(2.0,2.4),render_size=(1800,1500))
VENDING_PREVIEW=make_preview('Vending_Machines_Preview',[k for k in ASSETS if k.startswith('vending_')],8,(1.6,1.5),camera_direction=(1,-7,3),render_size=(2000,1500))


def make_catalog(name,keys,columns,spacing=(1.6,2.0),quarter=False,resolution=(2000,1500)):
    scene=bpy.data.scenes.new(name)
    bpy.context.window.scene=scene;scene.world=PROP_PREVIEW.world
    labelmat=solid_material('Preview_Label',(.65,.73,.82),roughness=1)
    for i,key in enumerate(keys):
        a=ASSETS[key];x=(i%columns)*spacing[0];z=-(i//columns)*spacing[1]
        zmin=REPORT[key]['bounds_min_m'][2]
        rotate=Matrix.Rotation(math.radians(22) if quarter else 0,4,'Z')
        transform=Matrix.Translation((x,0,z-zmin))@rotate
        for original in a['parts']:
            ob=original.copy();ob.parent=None;ob.matrix_world=transform@original.matrix_world
            ob.name='Catalog_'+key+'_'+original.get('part_name','Part');scene.collection.objects.link(ob)
        text=bpy.data.curves.new('Label_'+key,'FONT');text.body=key.replace('vending_','');text.align_x='CENTER';text.size=.10
        text.materials.append(labelmat);label=bpy.data.objects.new(text.name,text);scene.collection.objects.link(label)
        label.location=(x,-.5,z-.17);label.rotation_euler.x=math.pi/2
    rows=(len(keys)+columns-1)//columns
    target=Vector(((columns-1)*spacing[0]/2,0,-(rows-1)*spacing[1]/2+.7))
    for label,loc,power in [('Key',(-3,-5,5),1600),('Fill',(7,-3,2),1000)]:
        data=bpy.data.lights.new(name+'_'+label,'AREA');data.energy=power;data.size=9
        ob=bpy.data.objects.new(data.name,data);scene.collection.objects.link(ob);ob.location=target+Vector(loc)
        ob.rotation_euler=(target-ob.location).to_track_quat('-Z','Y').to_euler()
    data=bpy.data.cameras.new(name+'_Camera');data.type=G.enum(data,'type','ORTHO')
    data.ortho_scale=max(columns*spacing[0]+.8,rows*spacing[1]*resolution[0]/resolution[1])*1.06
    cam=bpy.data.objects.new(data.name,data);scene.collection.objects.link(cam);cam.location=target+Vector((0,-20,1 if quarter else 0))
    cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();scene.camera=cam
    scene.render.engine='BLENDER_EEVEE_NEXT';scene.render.resolution_x,scene.render.resolution_y=resolution;scene.render.resolution_percentage=100
    scene.view_settings.view_transform=G.enum(scene.view_settings,'view_transform','Standard')
    scene.render.image_settings.file_format=G.enum(scene.render.image_settings,'file_format','PNG')
    scene.render.filepath=str(ROOT/'docs/ai/art'/(name.lower()+'.png'))
    bpy.data.libraries.write(str(OUT/(name.lower()+'.blend')),{scene},path_remap='RELATIVE',fake_user=True,compress=True)
    return scene


VENDOR_CATALOG=make_catalog('Vending_Catalog',[k for k in ASSETS if k.startswith('vending_')],8,(1.6,2.0))
PROP_CATALOG=make_catalog('Station_Props_Catalog',PROP_KEYS,5,(2.2,2.4),True,(1800,2000))

# Assemble reusable stationary modules into straight, L, and 4x3 arrangements.
ASSEMBLIES=bpy.data.scenes.new('Stationary_Tank_Assemblies');bpy.context.window.scene=ASSEMBLIES
ASSEMBLIES.world=PROP_PREVIEW.world
ASSEMBLY_DATA={};direction_vectors={'N':(0,1),'S':(0,-1),'E':(1,0),'W':(-1,0)}
for label,cells,offset in [('Straight',{(x,0) for x in range(4)},(0,0)),
                         ('L_Shape',{(0,0),(1,0),(2,0),(0,1),(0,2)},(5.0,0)),
                         ('Rectangle',{(x,y) for x in range(4) for y in range(3)},(9.0,0))]:
    ASSEMBLY_DATA[label]=[]
    for x,y in sorted(cells):
        wanted={d for d,(dx,dy) in direction_vectors.items() if (x+dx,y+dy) in cells}
        found=None
        for suffix,canonical in TANK_MODULES.items():
            for turns in range(4):
                rot=Matrix.Rotation(turns*math.pi/2,3,'Z')
                rotated={(round((rot@Vector((*direction_vectors[d],0))).x),round((rot@Vector((*direction_vectors[d],0))).y)) for d in canonical}
                if rotated=={direction_vectors[d] for d in wanted}:found=(suffix,turns);break
            if found:break
        assert found,(label,x,y,wanted)
        suffix,turns=found;transform=Matrix.Translation((offset[0]+x*.9,offset[1]+y*.9,0))@Matrix.Rotation(turns*math.pi/2,4,'Z')
        for original in ASSETS['stationary_tank_'+suffix]['parts']:
            ob=original.copy();ob.parent=None;ob.matrix_world=transform@original.matrix_world
            ob.name=label+'_'+str(x)+'_'+str(y)+'_'+original.get('part_name','Part');ASSEMBLIES.collection.objects.link(ob)
        ASSEMBLY_DATA[label].append({'cell':[x,y],'module':suffix,'rotation_z_degrees':turns*90,'connections':sorted(wanted)})
for original in PROP_PREVIEW.objects:
    if original.type in ('LIGHT','CAMERA'):
        ob=original.copy();ASSEMBLIES.collection.objects.link(ob)
        if ob.type=='CAMERA':
            ob.data=ob.data.copy();ob.data.ortho_scale=15.8;target=Vector((6,.8,.5));ob.location=target+Vector((3,-12,8));ob.rotation_euler=(target-ob.location).to_track_quat('-Z','Y').to_euler();ASSEMBLIES.camera=ob
ASSEMBLIES.render.engine='BLENDER_EEVEE_NEXT';ASSEMBLIES.render.resolution_x=1800;ASSEMBLIES.render.resolution_y=800
ASSEMBLIES.view_settings.view_transform=G.enum(ASSEMBLIES.view_settings,'view_transform','Standard')
ASSEMBLIES.render.image_settings.file_format=G.enum(ASSEMBLIES.render.image_settings,'file_format','PNG')
ASSEMBLIES.render.filepath=str(ROOT/'docs/ai/art/stationary_tank_assemblies.png')
bpy.data.libraries.write(str(OUT/'stationary_tank_assemblies.blend'),{ASSEMBLIES},path_remap='RELATIVE',fake_user=True,compress=True)
(OUT/'stationary_assembly_examples.json').write_text(json.dumps(ASSEMBLY_DATA,indent=2)+'\n')


def write_godot_preview(scene,keys,columns,spacing,filename):
    folder=OUT/'previews';folder.mkdir(exist_ok=True)
    lines=['[gd_scene load_steps='+str(len(keys)+2)+' format=3]','']
    for i,key in enumerate(keys,1):lines.append('[ext_resource type="PackedScene" path="../'+REPORT[key]['file']+'" id="'+str(i)+'"]')
    lines+=['','[sub_resource type="Environment" id="Studio"]','background_mode = 1',
            'background_color = Color(0.08, 0.11, 0.15, 1)','ambient_light_energy = 0.8',
            '','[node name="Preview" type="Node3D"]',
            '','[node name="Environment" type="WorldEnvironment" parent="."]','environment = SubResource("Studio")',
            '','[node name="Key" type="DirectionalLight3D" parent="."]','rotation_degrees = Vector3(-50, -30, 0)','light_energy = 1.0','shadow_enabled = true',
            '','[node name="Fill" type="DirectionalLight3D" parent="."]','rotation_degrees = Vector3(-25, 140, 0)','light_energy = 0.6']
    c=Matrix(((1,0,0),(0,0,1),(0,-1,0)));cam=scene.camera;basis=c@cam.rotation_euler.to_matrix();pos=c@cam.location
    vals=[basis[r][col] for col in range(3) for r in range(3)]+list(pos)
    lines+=['','[node name="Camera" type="Camera3D" parent="."]',
            'transform = Transform3D('+', '.join(str(round(v,7)) for v in vals)+')','projection = 1',
            'size = '+str(cam.data.ortho_scale*min(1,scene.render.resolution_y/scene.render.resolution_x)),'current = true']
    for i,key in enumerate(keys):lines+=['','[node name="'+key+'" parent="." instance=ExtResource("'+str(i+1)+'")]',
        'position = Vector3('+str((i%columns)*spacing[0])+', 0, '+str(-(i//columns)*spacing[1])+')']
    (folder/filename).write_text('\n'.join(lines)+'\n',encoding='utf-8')
write_godot_preview(PROP_PREVIEW,PROP_KEYS,5,(2.0,2.4),'station_props_preview.tscn')
write_godot_preview(VENDING_PREVIEW,[k for k in ASSETS if k.startswith('vending_')],8,(1.6,1.5),'vending_preview.tscn')
(OUT/'project.godot').write_text('config_version=5\n[application]\nconfig/name="Station prop previews"\nrun/main_scene="res://previews/station_props_preview.tscn"\n[display]\nwindow/size/viewport_width=1600\nwindow/size/viewport_height=1000\n[rendering]\nrenderer/rendering_method="gl_compatibility"\n',encoding='utf-8')

LIBRARY_SCENES={a['scene'] for a in ASSETS.values()}|{PROP_PREVIEW,VENDING_PREVIEW,VENDOR_CATALOG,PROP_CATALOG,ASSEMBLIES}
bpy.data.libraries.write(str(OUT/'station_props_library.blend'),LIBRARY_SCENES,path_remap='RELATIVE',fake_user=True,compress=True)
bpy.context.window.scene=PROP_PREVIEW
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            sp=area.spaces.active;sp.shading.type=G.enum(sp.shading,'type','MATERIAL')
            sp.region_3d.view_location=(4,4,.7);sp.region_3d.view_distance=17
            sp.region_3d.view_rotation=Vector((3,-7,5)).to_track_quat('Z','Y');sp.region_3d.view_perspective='ORTHO'
print('Exported',len(ASSETS),'assets;',sum(r['triangles'] for r in REPORT.values()),'triangles total')
