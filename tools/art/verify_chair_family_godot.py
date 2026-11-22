"""Import the chair exports into a temporary Godot project without touching the game."""
from pathlib import Path
import shutil, subprocess, tempfile, json

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'godot/assets/models/furniture/chair_family'
godot=shutil.which('godot')
assert godot,'Godot executable not found'
keys=sorted(p.stem for p in OUT.glob('*.glb'))
gd='''extends SceneTree
var errors: Array[String] = []
var report: Dictionary = {}
func inspect(node: Node, row: Dictionary) -> void:
    if node is AnimationPlayer or node is CollisionObject3D:
        errors.append("Unexpected animation or collision: " + node.name)
    if node is MeshInstance3D:
        row["mesh_instances"] += 1
        for index in node.mesh.get_surface_count():
            var material: Material = node.get_active_material(index)
            if not material is StandardMaterial3D:
                errors.append("Unsupported material on " + node.name)
                continue
            row["materials"][material.resource_name] = {"filter": material.texture_filter, "offset": [material.uv1_offset.x, material.uv1_offset.y]}
            if material.texture_filter != BaseMaterial3D.TEXTURE_FILTER_NEAREST:
                errors.append("Filtering: " + node.name)
            if material.transparency != BaseMaterial3D.TRANSPARENCY_DISABLED:
                errors.append("Unexpected transparency: " + node.name)
            if material.albedo_texture == null:
                errors.append("Missing atlas: " + node.name)
    for child in node.get_children(): inspect(child, row)
func _initialize() -> void:
    var names = NAMES
    for key in names:
        var packed = load("res://" + key + ".glb") as PackedScene
        if packed == null:
            errors.append("Load failed " + key)
            continue
        var instance = packed.instantiate()
        var row = {"mesh_instances": 0, "materials": {}}
        inspect(instance, row)
        report[key] = row
        instance.free()
    var preview = load("res://chair_family_preview.tscn") as PackedScene
    if preview == null:
        errors.append("Preview failed to load")
    else:
        var instance = preview.instantiate()
        if instance.get_node_or_null("Camera") == null: errors.append("Preview camera missing")
        instance.free()
    var output = {"engine": Engine.get_version_info(), "models": report, "errors": errors, "preview_loads": preview != null}
    var file = FileAccess.open("res://validation_result.json", FileAccess.WRITE)
    file.store_string(JSON.stringify(output, "  "))
    print(JSON.stringify(output))
    quit(0 if errors.is_empty() else 1)
'''.replace('NAMES',json.dumps(keys))
with tempfile.TemporaryDirectory(prefix='chair-godot-') as folder:
    project=Path(folder)
    for file in list(OUT.glob('*.glb'))+[OUT/'chair_family_preview.tscn']:
        shutil.copy2(file,project/file.name)
    (project/'project.godot').write_text('config_version=5\n[application]\nconfig/name="Chair validation"\nrun/main_scene="res://chair_family_preview.tscn"\n[rendering]\nrenderer/rendering_method="gl_compatibility"\n',encoding='utf-8')
    (project/'verify.gd').write_text(gd,encoding='utf-8')
    for arguments in [['--editor','--import','--quit'],['--script','res://verify.gd']]:
        run=subprocess.run([godot,'--headless','--path',str(project)]+arguments,capture_output=True,text=True,timeout=180)
        if run.returncode:
            print(run.stdout);print(run.stderr);raise SystemExit(run.returncode)
    result=json.loads((project/'validation_result.json').read_text())
    (OUT/'godot_validation.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(result,indent=2))
