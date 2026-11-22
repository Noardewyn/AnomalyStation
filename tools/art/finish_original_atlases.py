"""Batch migration helpers for the live Blender original-atlas repair namespace."""
import bpy, json, hashlib, struct
from pathlib import Path
r=bpy.app.driver_namespace['original_atlas_repair']

def signature(objects):
    result=[]
    for o in objects:
        if o.type!='MESH':continue
        h=hashlib.sha256()
        for v in o.data.vertices:h.update(struct.pack('<3f',*v.co))
        for p in o.data.polygons:h.update(str(tuple(p.vertices)).encode())
        result.append((h.hexdigest(),tuple(o.location),tuple(o.rotation_euler),tuple(o.scale)))
    return sorted(result)

def batch(count=10):
    pending=[k.replace('\\','/') for k,v in r['AUDIT'].items() if any(not x['original'] for x in v) and k.replace('\\','/') not in r['LOG']]
    for relative in pending[:count]:
        path=r['BASE']/relative
        with bpy.data.libraries.load(str(path),link=False) as (src,dst):dst.scenes=list(src.scenes)
        original_scenes=dst.scenes
        before=signature({o for s in original_scenes for o in s.objects})
        if relative not in r['PREPARED']:r['prepare'](relative)
        assert before==signature(r['PREPARED'][relative]['objects']),('Geometry changed',relative)
        r['save'](relative)
        for sc in original_scenes:bpy.data.scenes.remove(sc)
    print('REMAINING',max(0,len(pending)-count))

def export_modules(relative):
    a=r['PREPARED'][relative];folder=a['path'].parent;sc=a['scenes'][0];bpy.context.window.scene=sc
    for entry in json.loads((folder/'modules.json').read_text())['modules']:
        name=Path(entry['file']).stem
        ob=next(o for o in sc.objects if o.name.split('.')[0]==name)
        old=ob.location.copy();bpy.ops.object.select_all(action='DESELECT');ob.select_set(True);bpy.context.view_layer.objects.active=ob
        try:
            ob.location=(0,0,0)
            path=folder/entry['file']
            bpy.ops.export_scene.gltf(filepath=path.as_posix(),use_selection=True,use_active_scene=True,export_animations=False,export_extras=True)
            r['patch_glb'](path)
        finally:ob.location=old
    print('EXPORTED MODULES',relative)

bpy.app.driver_namespace['finish_original_atlases']=globals()
