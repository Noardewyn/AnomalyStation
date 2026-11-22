// The airflow streams (D141, DEVIATION DV34; docs/ai/specs/apps.md 2.19): thin ribbons along the paths the air takes to a
// hole, rebuilt every frame by AirflowLayer and turned to face the camera. COLOR carries the stream's colour (white, or the
// colour of the gas it carries) and its alpha (born transparent, semi-transparent in the middle of its life, gone at its end,
// thinner at both ends of the visible piece); UV.y runs across the ribbon for a soft edge. Lit by the light grid like the gas
// haze (light-grid.md section 7), with a little glow of its own. Over a light surface a white stream would vanish (the
// station's floors are near white: the owner's floor streams were hardly seen, 2026-10-14), so it darkens by the brightness
// of what is behind it: a white haze over space and dark walls, a grey streak over a white floor.
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, blend_mix, shadows_disabled;

#include "res://shaders/light_grid.gdshaderinc"

uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform float self_light = 0.18;
uniform float dark_over_light = 0.42; // how dark the streak gets over a white background (a share of its colour)

varying vec3 v_world;

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float across = 1.0 - abs(UV.y * 2.0 - 1.0);
	float edge = smoothstep(0.0, 0.75, across);
	vec3 light = max(anomaly_grid_light(v_world), vec3(self_light));
	vec3 behind = texture(screen_tex, SCREEN_UV).rgb;
	float bright = smoothstep(0.35, 0.8, dot(behind, vec3(0.299, 0.587, 0.114)));
	ALBEDO = COLOR.rgb * light * mix(1.0, dark_over_light, bright);
	ALPHA = clamp(COLOR.a * edge * (1.0 + 0.6 * bright), 0.0, 1.0);
}
