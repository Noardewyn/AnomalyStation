// Gaps W1 (protocol version 74): BYOND's maptext on a world atom (Render/WorldMaptext.cs). The text box is drawn once into a texture; this quad covers the
// part of the box that can show (the box, or its alpha mask's bounds: Shared/Maptext/WorldMaptextPlacement.cs) and samples the box moved right by
// `shift` pixels (its animated maptext_x: tg's status display scrolls its long lines), clipped to the box. Unlit, as a screen's glowing text.
shader_type spatial;
render_mode cull_back, unshaded, depth_draw_opaque, shadows_disabled;

uniform sampler2D text : source_color, filter_nearest, repeat_disable;
// the quad's top left in box pixels (y down from the box's top), and its size in pixels
uniform vec4 region;
// the box's size in pixels
uniform vec2 box;
uniform float shift = 0.0;
// turned to the camera about the vertical axis with the picture it is on (a billboard's: picture_billboard_ontop.gdshader)
uniform bool billboard = false;

void vertex() {
	if (billboard) {
		vec3 right = normalize(vec3(MAIN_CAM_INV_VIEW_MATRIX[0].x, 0.0, MAIN_CAM_INV_VIEW_MATRIX[0].z));
		vec3 up = vec3(0.0, 1.0, 0.0);
		vec3 back = cross(right, up);
		MODELVIEW_MATRIX = VIEW_MATRIX * mat4(vec4(right * length(MODEL_MATRIX[0].xyz), 0.0), vec4(up * length(MODEL_MATRIX[1].xyz), 0.0), vec4(back, 0.0), MODEL_MATRIX[3]);
	}
}

void fragment() {
	vec2 px = region.xy + UV * region.zw;
	px.x -= shift;
	if (px.x < 0.0 || px.y < 0.0 || px.x >= box.x || px.y >= box.y) {
		discard;
	}
	vec4 c = texture(text, px / box);
	if (c.a < 0.5) {
		discard;
	}
	ALBEDO = c.rgb;
}
