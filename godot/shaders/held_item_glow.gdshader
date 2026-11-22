// D285: the faint pixels of a held thing's picture (tg's rad glow outline, #39ff1430, irradiated.dm; a flame's soft edge) as a glow over the solid
// (held_item_lit.gdshader's next pass): added light in their colour, brightened by their faintness, pulsing slowly like tg's glow loop.
shader_type spatial;
render_mode unshaded, cull_disabled, blend_add, depth_draw_never, shadows_disabled;

#include "res://shaders/held_item_view.gdshaderinc"

uniform sampler2D picture : source_color, filter_nearest, repeat_disable;
uniform float strength = 2.5;

void vertex() {
	POSITION = held_view_position(PROJECTION_MATRIX, MODELVIEW_MATRIX * vec4(VERTEX, 1.0), 0.001);
}

void fragment() {
	vec4 texel = texture(picture, UV);
	if (texel.a <= 0.0 || texel.a >= 0.5)
		discard;
	float pulse = 0.75 + 0.25 * sin(TIME * 2.4);
	ALBEDO = texel.rgb * texel.a * strength * pulse;
}
