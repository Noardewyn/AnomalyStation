// A mirror's glass (Render/MirrorPass.cs): the room as the mirror's camera sees it through the glass's outline, laid on a quad before tg's
// mirror picture. The camera looks out of the wall, so its picture's left is the quad's right. The shape is tg's mirror_mask (watercloset.dmi):
// 12 by 17 pixels with the corners cut.
shader_type spatial;
render_mode unshaded, cull_back, shadows_disabled;

uniform sampler2D view : source_color, filter_linear, repeat_disable;

void fragment() {
	vec2 px = UV * vec2(12.0, 17.0);
	float row = floor(min(px.y, 17.0 - px.y));
	float half_width = row < 1.0 ? 3.0 : row < 2.0 ? 4.0 : row < 4.0 ? 5.0 : 6.0;
	if (abs(px.x - 6.0) > half_width) {
		discard;
	}
	// a little of the light is lost in the glass, a touch of its tint is added
	ALBEDO = texture(view, vec2(1.0 - UV.x, UV.y)).rgb * vec3(0.86, 0.9, 0.92);
}
