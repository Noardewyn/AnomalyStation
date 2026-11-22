// D301: a shuttle engine's jet in flight: a cone that fades from the nozzle (its top, UV.y 0) outward and flickers.
shader_type spatial;
render_mode unshaded, blend_add, cull_disabled, depth_draw_never, shadows_disabled;

uniform float seed = 0.0;

void fragment() {
	float flicker = 0.85 + 0.15 * sin(TIME * 31.0 + seed) * sin(TIME * 17.0 + seed * 2.3);
	float fade = pow(clamp(1.0 - UV.y / flicker, 0.0, 1.0), 1.6);
	ALBEDO = mix(vec3(0.15, 0.45, 1.0), vec3(0.9, 0.97, 1.0), fade * fade);
	// soft sides: the cone's silhouette melts away, so no hard tube shows
	float facing = abs(dot(normalize(NORMAL), normalize(VIEW)));
	ALPHA = fade * facing * facing * 0.7;
}
