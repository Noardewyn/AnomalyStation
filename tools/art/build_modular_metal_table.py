"""Modular tabletops selected by cardinal adjacency, plus independent supports."""
from pathlib import Path
import bpy, json, struct
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT/'godot/assets/models/furniture/metal_table/modular'
OUT.mkdir(parents=True,exist_ok=True)
source = bpy.data.images.load(str(ROOT/'godot/assets/tg/icons/obj/smooth_structures/table.png'),check_existing=True)
sw,sh = source.size
sp = list(source.pixels)
def sample(x,y):
    k=((sh-1-y)*sw+x)*4
    return sp[k:k+4]
size=128
pixels=sample(4,24)*(size*size)
patches={}
def patch(name,x,y,w,h,fn):
    patches[name]=(x,y,w,h)
    for py in range(-1,h+1):
        for px in range(-1,w+1):
            k=((size-1-y-py)*size+x+px)*4
            pixels[k:k+4]=fn(min(w-1,max(0,px)),min(h-1,max(0,py)))
# Bits: north +Y = 1, east +X = 2, south -Y = 4, west -X = 8.
for mask in range(16):
    def color(x,y,m=mask):
        edge=(y==0 and not m&1) or (x==27 and not m&2) or (y==27 and not m&4) or (x==0 and not m&8)
        return sample(2,2) if edge else sample(4,4)
    patch(str(mask),1+30*(mask%4),1+30*(mask//4),28,28,color)
patch('rim',122,12,1,3,lambda x,y:sample(4,22+y))
patch('leg',122,1,3,6,lambda x,y:sample(3+x,25+y) if sample(3+x,25+y)[3] else sample(4,24))
patch('dark',122,19,2,2,lambda x,y:sample(4,24))
image=bpy.data.images.new('Modular_Table_Atlas',width=size,height=size,alpha=False)
image.pixels.foreach_set(pixels)
image.update()
image.filepath_raw=str(OUT/'modular_table_albedo.png')
image.file_format=next(i.identifier for i in image.bl_rna.properties['file_format'].enum_items if i.identifier=='PNG')
image.save();image.pack()
mat=bpy.data.materials.new('Modular_Table_Metal')
mat.use_nodes=True
mat.use_backface_culling=True
bsdf=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
for key,value in [('Roughness',.85),('Metallic',.1),('Specular IOR Level',.15)]:
    bsdf.inputs[key].default_value=value
tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=image
tex.interpolation=next(i.identifier for i in tex.bl_rna.properties['interpolation'].enum_items if i.identifier=='Closest')
mat.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
if bpy.context.object and bpy.context.object.mode!='OBJECT':bpy.ops.object.mode_set(mode='OBJECT')
kit=bpy.data.scenes.new('Modular_Table_Compact_Kit')
bpy.context.window.scene=kit
def make(name,mask=None):
    leg=mask is None
    lo,hi=(-.05,.05) if leg else (-.45,.45)
    z0,z1=(0,.668) if leg else (.66,.75)
    verts=[(lo,lo,z0),(hi,lo,z0),(hi,hi,z0),(lo,hi,z0),(lo,lo,z1),(hi,lo,z1),(hi,hi,z1),(lo,hi,z1)]
    faces=[(3,2,1,0),(4,5,6,7)]
    names=['dark','dark' if leg else str(mask)]
    for i,bit in enumerate([4,2,1,8]):
        if leg or not mask&bit:
            j=(i+1)%4
            faces.append((i,j,4+j,4+i));names.append('leg' if leg else 'rim')
    mesh=bpy.data.meshes.new(name+'_Mesh');mesh.from_pydata(verts,[],faces);mesh.materials.append(mat)
    uv=mesh.uv_layers.new(name='Modular_Atlas_UV')
    for poly,p in zip(mesh.polygons,names):
        x,y,w,h=patches[p]
        for loop,(u,v) in zip(poly.loop_indices,[(0,1),(1,1),(1,0),(0,0)]):
            uv.data[loop].uv=((x+.001+u*(w-.002))/size,1-(y+.001+v*(h-.002))/size)
    mesh.update();mesh.calc_loop_triangles()
    obj=bpy.data.objects.new(name,mesh);kit.collection.objects.link(obj)
    obj['grid_pitch_m']=.9
    if not leg:obj['neighbor_mask']=mask
    return obj
def export(obj,path):
    for other in kit.objects:other.select_set(False)
    obj.select_set(True);bpy.context.view_layer.objects.active=obj
    bpy.ops.export_scene.gltf(filepath=str(path),use_selection=True,use_active_scene=True,export_animations=False,export_extras=True)
    blob=path.read_bytes();n=struct.unpack_from('<I',blob,12)[0];g=json.loads(blob[20:20+n])
    for sampler in g.get('samplers',[]):sampler.update(minFilter=9728,magFilter=9728)
    payload=json.dumps(g,separators=(',',':')).encode();payload+=b' '*(-len(payload)%4)
    tail=blob[20+n:]
    path.write_bytes(struct.pack('<III',0x46546C67,2,20+len(payload)+len(tail))+struct.pack('<II',len(payload),0x4E4F534A)+payload+tail)
    assert len(g['meshes'])==1
    assert sum(g['accessors'][p['indices']]['count']//3 for m in g['meshes'] for p in m['primitives'])==len(obj.data.loop_triangles)

exec(compile((ROOT/'tools/art/modular_table_compact_tail.py').read_text(encoding='utf-8-sig'), 'modular_table_compact_tail.py', 'exec'))

