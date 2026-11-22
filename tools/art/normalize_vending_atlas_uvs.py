"""Keep per-appearance atlas offsets in materials so catalog meshes stay reusable."""
import bpy,re,hashlib,json
r=bpy.app.driver_namespace['original_atlas_repair']
shared={};parts={};done_materials=set()
def clean(name):return re.sub(r'\.\d{3}$','',name)
for state in r['VEND']:
    key=re.sub(r'[^a-z0-9]+','_',state.lower());relative='station_props/vending/vending_'+key+'.blend'
    a=r['PREPARED'][relative];path=r['ROOT']/r['SOURCES']['vending']['png'];tx,ty,_,_=r['tile'](path,state);im=r['original'](path);w,h=im.size
    for ob in a['objects']:
        if ob.type!='MESH':continue
        uv=ob.data.uv_layers.active
        for p in ob.data.polygons:
            m=ob.material_slots[p.material_index].material
            if not m.name.startswith('Original_'):continue
            for li in p.loop_indices:
                u,v=uv.data[li].uv;uv.data[li].uv=((u*w-tx)/32,1-((1-v)*h-ty)/32)
        for slot in ob.material_slots:
            m=slot.material
            if not m.name.startswith('Original_') or m.as_pointer() in done_materials:continue
            done_materials.add(m.as_pointer());nodes=m.node_tree.nodes;links=m.node_tree.links
            bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED');tex=bs.inputs['Base Color'].links[0].from_node
            coord=nodes.new('ShaderNodeTexCoord');mapping=nodes.new('ShaderNodeMapping');mapping.inputs['Scale'].default_value=(32/w,32/h,1);mapping.inputs['Location'].default_value=(tx/w,1-(ty+32)/h,0)
            links.new(coord.outputs['UV'],mapping.inputs['Vector']);links.new(mapping.outputs['Vector'],tex.inputs['Vector'])
            for old in list(nodes):
                if old.type=='MAPPING' and old!=mapping:
                    loc=old.inputs['Location'].default_value.copy();old.inputs['Scale'].default_value=(32/w,32/h,1);old.inputs['Location'].default_value=(loc.x+tx/w,loc.y+1-(ty+32)/h,0)
        # Canonicalize float noise when matching compatible UV layouts.
        sig=hashlib.sha256(str(([tuple(round(c,6) for c in v.co) for v in ob.data.vertices],[(tuple(p.vertices),p.material_index) for p in ob.data.polygons],[tuple(round(c,6) for c in x.uv) for x in uv.data])).encode()).hexdigest()
        materials=[s.material for s in ob.material_slots]
        if sig in shared:ob.data=shared[sig]
        else:shared[sig]=ob.data
        for i,m in enumerate(materials):ob.material_slots[i].link='OBJECT';ob.material_slots[i].material=m
        parts[clean(ob.name)]=ob
    r['save'](relative)
for relative in ['station_props/vending_catalog.blend','station_props/vending_machines_preview.blend']:
    a=r['PREPARED'][relative]
    for ob in a['objects']:
        if ob.type!='MESH':continue
        src=parts.get(clean(ob.name))
        assert src is not None,ob.name
        ob.data=src.data
        for i,s in enumerate(src.material_slots):ob.material_slots[i].link='OBJECT';ob.material_slots[i].material=s.material
    r['save'](relative)
print('Reusable vending mesh datablocks:',len(shared))
