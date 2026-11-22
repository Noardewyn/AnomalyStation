// D285: the first-person held things' projection (held_item_lit.gdshader, held_item_glow.gdshader): the camera's projection with the view model's
// own field of view, and the depth squeezed into the slice next to the near plane (Godot's reversed z: 1 is near), kept in order.
uniform float view_fov = 62.0;

vec4 held_view_position(mat4 projection, vec4 view_position, float lift) {
	// scaled, not replaced: the camera's matrix keeps its aspect and its signs (Vulkan's flipped y)
	float k = (1.0 / tan(radians(view_fov) * 0.5)) / abs(projection[1][1]);
	projection[0][0] *= k;
	projection[1][1] *= k;
	vec4 clip = projection * view_position;
	clip.z = clip.w * (0.94 + lift) + clip.z * 0.05;
	return clip;
}
