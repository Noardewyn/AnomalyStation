"""Replace derived image dependencies with original TG atlas UVs in saved Blender assets.

Geometry coordinates, polygons, pivots and module transforms are preserved.
Run through Blender MCP; prepare a file, inspect, then save it.
"""
from pathlib import Path
from collections import Counter
import bpy,json,hashlib,math,struct,re,time
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];BASE=ROOT/'godot/assets/models';TG=ROOT/'godot/assets/tg'
AUDIT=json.loads((ROOT/'docs/ai/art/blend_atlas_audit.json').read_text())
RECIPES=json.loads((BASE/'station_props/surface_rebuild_recipes.json').read_text())
SOURCES=json.loads((BASE/'station_props/source_audit.json').read_text())['sources']
VEND=json.loads((BASE/'station_props/vending/source_catalog.json').read_text())['vendors']
TABLES=json.loads((BASE/'furniture/table_kits/variants.json').read_text())
COLORS={r['name']:r['colors'] for r in json.loads((BASE/'station_props/canister_gags_variants.json').read_text())['variants']}
IMAGES={};META={};PIXELS={};SAFE={};MATCACHE={};PREPARED={};LOG={}

def original(path):
    path=Path(path).resolve();key=str(path)
    if key not in IMAGES:
        im=bpy.data.images.load(key,check_existing=False);IMAGES[key]=im;META[key]=json.loads(path.with_suffix('.json').read_text());PIXELS[key]=list(im.pixels)
    return IMAGES[key]

def tile(path,state):
    im=original(path);meta=META[str(Path(path).resolve())];return next(s['rects'][0] for s in meta['states'] if s['name']==state)

def safe_rect(path,rect):
    """Largest fully opaque pixel rectangle inside the requested source patch."""
    path=str(Path(path).resolve());key=(path,tuple(rect))
    if key in SAFE:return SAFE[key]
    im=original(path);w,h=im.size;pix=PIXELS[path];x0,y0,x1,y1=map(int,rect)
    best=None;area=0
    for ya in range(max(0,y0),min(h,y1)):
        allowed=[True]*max(0,x1-x0)
        for yb in range(ya,min(h,y1)):
            for j,x in enumerate(range(x0,x1)):allowed[j]=allowed[j] and 0<=x<w and pix[((h-1-yb)*w+x)*4+3]>=.99
            run=0
            for j,good in enumerate(allowed):
                run=run+1 if good else 0
                if run*(yb-ya+1)>area:area=run*(yb-ya+1);best=(x0+j-run+1,ya,x0+j+1,yb+1)
    if best is None:
        # Search near the requested patch, never introduce invented padding pixels.
        candidates=[]
        for y in range(max(0,y0-12),min(h,y1+12)):
            for x in range(max(0,x0-12),min(w,x1+12)):
                if pix[((h-1-y)*w+x)*4+3]>=.99:candidates.append(((x-(x0+x1)/2)**2+(y-(y0+y1)/2)**2,(x,y,x+1,y+1)))
        assert candidates,('no opaque source',path,rect)
        best=min(candidates)[1]
    SAFE[key]=best;return best

def coords(path,rect,u,v):
    im=original(path);x0,y0,x1,y1=safe_rect(path,rect)
    # Non-zero UV area even on one-pixel flat patches, without crossing texel edges.
    return ((x0+.01+max(0,min(1,u))*(x1-x0-.02))/im.size[0],1-(y0+.01+max(0,min(1,v))*(y1-y0-.02))/im.size[1])

def describe(im,ob):
    p=Path(bpy.path.abspath(im.filepath));name=p.stem
    if str(TG).lower() in str(p).lower():return None
    if name.startswith('vending_') and name.endswith(('_surfaces','_lights')):
        stem=name[8:].rsplit('_',1)[0];state=next(s for s in VEND if re.sub(r'[^a-z0-9]+','_',s.lower())==stem)
        return {'kind':'vending_light' if name.endswith('_lights') else 'vending_side','path':ROOT/SOURCES['vending']['png'],'state':state}
    if name.endswith('_surfaces') and name[:-9] in RECIPES:
        key=name[:-9];r=RECIPES[key]
        if key.startswith('portable_canister_'):
            variant=key[len('portable_canister_'):]
            state='/obj/machinery/portable_atmospherics/canister'+('' if variant=='generic' else '/'+variant)
            return {'kind':'round','path':ROOT/SOURCES['map_objects']['png'],'state':state,'recipe':r,'key':key}
        return {'kind':'round','path':ROOT/SOURCES[r['source']]['png'],'state':r['state'],'recipe':r,'key':key}
    if name=='metal_chair_albedo':return {'kind':'chair','path':TG/'icons/obj/chairs.png'}
    if name=='metal_table_albedo':return {'kind':'single_table','path':TG/'icons/obj/smooth_structures/table.png'}
    if name=='modular_table_albedo':return {'kind':'table','path':TG/'icons/obj/smooth_structures/table.png'}
    if name in TABLES:return {'kind':'table','path':ROOT/'godot'/TABLES[name]['source']}
    raise RuntimeError(('Unmapped image',str(p),ob.name))

def remap(d,uvs):
    path=d['path'];kind=d['kind'];cx=sum(u for u,v in uvs)/len(uvs);cy=sum(1-v for u,v in uvs)/len(uvs)
    if kind=='vending_light':
        tx,ty,_,_=tile(path,d['state']);im=original(path)
        return [((tx+u*32)/im.size[0],1-(ty+(1-v)*32)/im.size[1]) for u,v in uvs]
    if kind=='vending_side':
        tx,ty,_,_=tile(path,d['state']);x0,y0,x1,y1=VEND[d['state']]['opaque_bbox']
        if cy<.5:patch=(x0,y0+5,x0+3,y1-1);base=(0,0)
        elif cx<.5:patch=(x0+2,y1-8,x1-3,y1-2);base=(0,32)
        else:patch=(x0,y1-5,x0+3,y1-1);base=(32,32)
        rect=(tx+patch[0],ty+patch[1],tx+patch[2],ty+patch[3])
        return [coords(path,rect,(u*64-base[0]-.5)/31,((1-v)*64-base[1]-.5)/31) for u,v in uvs]
    if kind=='round':
        col=min(3,max(0,int(cx*4)));row=min(3,max(0,int(cy*4)));slot=row*4+col
        patch=d['recipe']['patches_xyxy'][slot%len(d['recipe']['patches_xyxy'])];tx,ty,_,_=tile(path,d['state'])
        rect=(tx+patch[0],ty+patch[1],tx+patch[2],ty+patch[3]);out=[]
        for u,v in uvs:
            a=(u*64-col*16-.5)/15;b=((1-v)*64-row*16-.5)/15
            if ('canister' in d['key'] or d['key'] in ['water_tank','fuel_tank']) and slot in [1,2,6]:a=2*a if a<=.5 else 2-2*a
            out.append(coords(path,rect,a,b))
        return out
    if kind=='chair':
        patches=[((1,1,15,15),(136,3,151,18)),((19,1,15,15),(168,9,183,24)),((1,19,17,17),(135,18,152,25)),((1,39,17,3),(135,24,152,27)),((21,19,17,3),(201,23,218,26)),((42,1,3,12),(135,25,138,31))]
        px,py=cx*64,cy*64
        for (x,y,w,h),rect in patches:
            if x-.1<=px<=x+w+.1 and y-.1<=py<=y+h+.1:return [coords(path,rect,(u*64-x)/w,((1-v)*64-y)/h) for u,v in uvs]
        # Former custom palette swatches now point at real metal-chair texels.
        return [coords(path,(135,24,138,27),u%1,(1-v)%1) for u,v in uvs]
    size=64 if kind=='single_table' else 128;px=cx*size;py=cy*size
    if (kind=='table' and px<120) or (kind=='single_table' and px<30 and py<30):
        mask=(int(py//30)*4+int(px//30)) if kind=='table' else 0
        # Smoothing bits in TG: N=1,S=2,E=4,W=8; include diagonals only on outer turns.
        junction={0:0,2:4,10:12,3:21,7:5,11:157,15:255}.get(mask,255)
        original(path)
        states=META[str(path.resolve())]['states']
        state=next((s['name'] for s in states if s['name'].endswith('-'+str(junction))),states[0]['name'])
        tx,ty,_,_=tile(path,state);x=1+30*(mask%4) if kind=='table' else 1;y=1+30*(mask//4) if kind=='table' else 1
        return [coords(path,(tx+2,ty+2,tx+30,ty+22),(u*size-x)/28,((1-v)*size-y)/28) for u,v in uvs]
    if kind=='single_table':
        if py>30:old=(1,32,28,3);rect=(2,22,30,25)
        elif px<37:old=(32,1,3,6);rect=(3,25,6,31)
        else:old=(38,1,4,4);rect=(4,24,6,25)
    elif py<9:old=(122,1,3,6);rect=(3,25,6,31)
    elif py<17:old=(122,12,1,3);rect=(15,22,17,25)
    elif py<24:old=(122,19,2,2);rect=(15,24,17,25)
    else:old=(122,25,3,28);rect=(2,25,30,31)
    x,y,w,h=old
    return [coords(path,rect,((1-v)*size-y)/h if py>=24 and kind=='table' else (u*size-x)/w,(u*size-x)/w if py>=24 and kind=='table' else ((1-v)*size-y)/h) for u,v in uvs]

def new_material(old,d):
    key=(old.as_pointer(),str(d['path']),d.get('tint'),d.get('state'),d['kind'])
    if key in MATCACHE:return MATCACHE[key]
    m=old.copy();m.name='Original_'+old.name;nodes=m.node_tree.nodes;links=m.node_tree.links
    bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    for n in list(nodes):
        if n.type not in ['BSDF_PRINCIPLED','OUTPUT_MATERIAL']:nodes.remove(n)
    t=nodes.new('ShaderNodeTexImage');t.image=original(d['path']);t.interpolation='Closest';links.new(t.outputs['Color'],bs.inputs['Base Color'])
    if d.get('tint'):
        rgb=tuple(int(d['tint'][i:i+2],16)/255 for i in [1,3,5]);linear=tuple(v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4 for v in rgb)
        mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=1;mix.inputs[2].default_value=(*linear,1);links.new(t.outputs['Color'],mix.inputs[1]);links.new(mix.outputs[0],bs.inputs['Base Color']);m['gags_color']=d['tint'];m['gltf_base_color_factor']=[*linear,1]
    if d['kind']=='vending_light':
        # Native separate emissive texture; no RGB/alpha recomposition into a new PNG.
        masks=[r['light_mask'] for r in VEND[d['state']]['types'] if r.get('light_mask')]
        meta=META[str(d['path'].resolve())];mask=next(s for s in meta['states'] if s['name']==masks[0]);tx,ty,_,_=tile(d['path'],d['state']);mx,my,_,_=mask['rects'][0];im=original(d['path'])
        e=nodes.new('ShaderNodeTexImage');e.image=im;e.interpolation='Closest';coord=nodes.new('ShaderNodeTexCoord');mapping=nodes.new('ShaderNodeMapping');mapping.inputs['Location'].default_value=((mx-tx)/im.size[0],(ty-my)/im.size[1],0);links.new(coord.outputs['UV'],mapping.inputs['Vector']);links.new(mapping.outputs['Vector'],e.inputs['Vector']);links.new(e.outputs['Color'],bs.inputs['Emission Color']);bs.inputs['Emission Strength'].default_value=.25;bs.inputs['Alpha'].default_value=1
    m['original_atlas']=str(d['path'].relative_to(ROOT));MATCACHE[key]=m;return m

def prepare(relative):
    path=BASE/relative;before={s.as_pointer() for s in bpy.data.scenes}
    with bpy.data.libraries.load(str(path),link=False) as (src,dst):dst.scenes=list(src.scenes)
    scenes=dst.scenes;objects={o for sc in scenes for o in sc.objects};changed_meshes={};changed=0
    for ob in objects:
        if ob.type!='MESH' or not ob.data.uv_layers:continue
        oldmats=[s.material for s in ob.material_slots];desc=[]
        for mat in oldmats:
            tex=next((n for n in mat.node_tree.nodes if n.type=='TEX_IMAGE' and n.image),None) if mat and mat.use_nodes else None
            desc.append(describe(tex.image,ob) if tex else None)
        if not any(desc):continue
        # Same source layout remains linked; material overrides retain appearance differences.
        key=(ob.data.as_pointer(),tuple((d['kind'],str(d['path']),d.get('state') if d['kind']=='round' else None) if d else None for d in desc))
        if key not in changed_meshes:
            me=ob.data.copy();uv=me.uv_layers.active
            for p in me.polygons:
                d=desc[p.material_index]
                if not d:continue
                beforeuv=[tuple(uv.data[li].uv) for li in p.loop_indices];after=remap(d,beforeuv)
                for li,co in zip(p.loop_indices,after):uv.data[li].uv=co
            changed_meshes[key]=me
        ob.data=changed_meshes[key]
        for i,d in enumerate(desc):
            if d:ob.material_slots[i].link='OBJECT';ob.material_slots[i].material=new_material(oldmats[i],d)
        # Avoid retaining superseded images through unused DATA material slots.
        for i,slot in enumerate(ob.material_slots):
            if slot.link=='OBJECT' and desc[i]:ob.data.materials[i]=slot.material
        changed+=1
    PREPARED[relative]={'path':path,'scenes':scenes,'objects':objects,'changed_objects':changed};bpy.context.window.scene=scenes[0]
    print('PREPARED',relative,changed,'objects',len(scenes),'scenes')
    return scenes

def patch_glb(path):
    raw=path.read_bytes();n=struct.unpack_from('<I',raw,12)[0];g=json.loads(raw[20:20+n]);tail=raw[20+n:]
    for s in g.get('samplers',[]):s.update(minFilter=9728,magFilter=9728)
    for nd in g['nodes']:nd['name']=re.sub(r'\.\d{3}$','',nd.get('name',''))
    payload=json.dumps(g,separators=(',',':')).encode();payload+=b' '*(-len(payload)%4);path.write_bytes(struct.pack('<III',0x46546c67,2,20+len(payload)+len(tail))+struct.pack('<II',len(payload),0x4e4f534a)+payload+tail)

def save(relative,export=True):
    a=PREPARED[relative];path=a['path'];paired=path.with_suffix('.glb')
    if export and paired.exists():
        sc=a['scenes'][0];bpy.context.window.scene=sc;bpy.ops.object.select_all(action='DESELECT')
        for o in sc.objects:
            if o.type in ['MESH','EMPTY']:o.select_set(True)
        for attempt in range(5):
            try:
                bpy.ops.export_scene.gltf(filepath=paired.as_posix(),use_selection=True,use_active_scene=True,export_animations=False,export_extras=True)
                patch_glb(paired)
                break
            except (RuntimeError,OSError):
                if attempt==4:raise
                time.sleep(.5)
    bpy.data.libraries.write(str(path),set(a['scenes']),path_remap='RELATIVE',fake_user=True,compress=True)
    LOG[relative]={'changed_objects':a['changed_objects'],'exported_paired_glb':export and paired.exists()};(ROOT/'docs/ai/art/original_atlas_changes.json').write_text(json.dumps(LOG,indent=2)+'\n');print('SAVED',relative)

bpy.app.driver_namespace['original_atlas_repair']=globals()
