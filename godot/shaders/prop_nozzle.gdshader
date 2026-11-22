// The nozzle shader (a part's "shader": "nozzle"): the energy core of a shuttle's propulsion or burst engine. It is lit by itself in the part's blue (the exhaust of
// tg's propulsion sprite) and never goes white: a hot core and bands of energy running out of the nozzle only lift the blue toward a lighter blue. The pulse (the
// catalog's frames) is the engine's flicker.
shader_type spatial;
render_mode blend_mix, cull_back, depth_draw_opaque, diffuse_burley, specular_disabled, shadows_disabled;

#include "res://shaders/model_pulse.gdshaderinc"

uniform vec3 tint = vec3(0.087, 0.35, 0.85); // the part's colour (linear)
uniform vec3 halo = vec3(0.3, 0.6, 1.0); // the rim's colour (linear): a lighter blue, never white
uniform float glow = 2.2; // the self-lit strength; kept so that the brightest pixel is still blue (bloom whitens above it)
uniform float speed = 6.0; // how fast the bands run along the axis (metres a second, scaled)

varying vec3 v_local;

void vertex() {
	v_local = VERTEX;
}

void fragment() {
	float strength;
	vec3 scale;
	anomaly_pulse(TIME, strength, scale);
	float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	// bands of energy running along the nozzle's axis (the model's Z), out of the engine
	float band = 0.5 + 0.5 * sin(v_local.z * 28.0 - TIME * speed);
	float rim = pow(1.0 - facing, 2.0);
	vec3 colour = tint * (0.7 + 0.5 * band) + halo * rim * 0.5;
	ALBEDO = vec3(0.0);
	ROUGHNESS = 1.0;
	EMISSION = colour * scale * glow * (0.6 + 0.4 * strength);
}
