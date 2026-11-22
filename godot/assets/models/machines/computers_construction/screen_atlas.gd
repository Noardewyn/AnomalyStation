extends RefCounted
## Standalone utility: call with an imported screen MeshInstance3D and one binding frame.
## The binding contains a Godot-space offset relative to the exported default UVs.
static func apply_frame(screen: MeshInstance3D, frame: Dictionary) -> void:
	var material := screen.get_surface_override_material(0) as StandardMaterial3D
	if material == null:
		var source := screen.get_active_material(0) as StandardMaterial3D
		if source == null:
			push_error("Screen requires a StandardMaterial3D from the GLB import.")
			return
		material = source.duplicate() as StandardMaterial3D
		screen.set_surface_override_material(0, material)
	var offset: Array = frame["godot_uv1_offset"]
	material.uv1_offset = Vector3(float(offset[0]), float(offset[1]), 0.0)
	material.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
