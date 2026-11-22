// The field shader (docs/ai/specs/models.md, a part's "shader": "field"): the supermatter's causality field, tg's causality_field overlay: a see-through box of
// tg's two-pixel checker (16 cells a metre: 32 pixels over the 2 m tile) with light running up it and its edges brighter, blended over the room. Its alpha is
// the part's own times the strength of the frame of tg's icon state it plays (the pulse: 175, 129, 73, 129 of 255). Drawn after the crystal it holds
// (render priority 1), seen from inside too.
shader_type spatial;
render_mode blend_mix, cull_disabled, depth_draw_never, diffuse_burley, specular_disabled, shadows_disabled;

#include "res://shaders/model_pulse.gdshaderinc"

uniform vec3 tint = vec3(0.03, 0.15, 0.46); // the part's colour (linear: its glTF base colour)
uniform float opacity = 0.45; // its glTF alpha

varying vec3 v_world;

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	float strength;
	vec3 scale;
	anomaly_pulse(TIME, strength, scale);
	vec3 n = abs((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	vec2 p = n.y > 0.5 ? v_world.xz : (n.x > 0.5 ? v_world.zy : v_world.xy);
	float checker = mod(floor(p.x * 16.0) + floor(p.y * 16.0), 2.0);
	float scan = pow(0.5 + 0.5 * sin(v_world.y * 6.0 - TIME * 2.5), 4.0);
	float edge = pow(1.0 - abs(dot(NORMAL, VIEW)), 2.0);
	ALBEDO = vec3(0.0);
	ROUGHNESS = 1.0;
	EMISSION = tint * scale * (1.3 + 0.5 * checker + 1.2 * scan + 1.5 * edge);
	ALPHA = clamp(opacity * strength * (0.8 + 0.2 * checker) + 0.25 * edge * strength, 0.0, 1.0);
}
