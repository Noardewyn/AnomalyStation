// D289: what a scanner shows through the walls (the T-ray copies, EntityLayer.UpdateFlatImage; the meson outline, Render/MesonLayer). Its depth
// is pinned just behind the held things' slice next to the near plane (held_item_view.gdshaderinc: 0.94 and up, Godot's reversed z), so it is
// drawn over every wall and floor of the world but under the things in the own hands.
shader_type spatial;
render_mode unshaded, cull_back, depth_draw_never, shadows_disabled;

uniform sampler2D picture : source_color, filter_nearest, repeat_disable, hint_default_white;
uniform vec4 tint : source_color = vec4(1.0);

void vertex() {
	POSITION = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	POSITION.z = POSITION.w * 0.93;
}

void fragment() {
	vec4 colour = texture(picture, UV) * tint;
	ALBEDO = colour.rgb;
	ALPHA = colour.a;
}
