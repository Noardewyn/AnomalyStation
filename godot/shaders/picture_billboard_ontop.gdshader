// DV232: tg's point arrow (POINT_PLANE, over everything and the lighting): the picture billboard (picture_billboard.gdshader) drawn unlit in
// its own colours with no depth test, so no model or wall face in front of the pointed point hides it.
shader_type spatial;
render_mode cull_disabled, unshaded, depth_test_disabled, depth_draw_never;

uniform sampler2D picture : source_color, filter_nearest, repeat_disable;
uniform float scissor = 0.5;

void vertex() {
	vec3 right = normalize(vec3(MAIN_CAM_INV_VIEW_MATRIX[0].x, 0.0, MAIN_CAM_INV_VIEW_MATRIX[0].z));
	vec3 up = vec3(0.0, 1.0, 0.0);
	vec3 back = cross(right, up);
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(vec4(right * length(MODEL_MATRIX[0].xyz), 0.0), vec4(up * length(MODEL_MATRIX[1].xyz), 0.0), vec4(back, 0.0), MODEL_MATRIX[3]);
}

void fragment() {
	vec4 c = texture(picture, UV);
	if (c.a < scissor) {
		discard;
	}
	ALBEDO = c.rgb;
}
