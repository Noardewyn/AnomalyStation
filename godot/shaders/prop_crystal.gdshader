// The crystal shader (docs/ai/specs/models.md, a part's "shader": "crystal"): the supermatter's crystal, a see-through gem. The room behind it (the opaque pass,
// the screen texture) shows through each facet, bent by the facet's slant and coloured by the crystal; the facets are shaded by their normals as the flat props
// are, a band of light runs up them and single facets sparkle; the silhouette has a halo; and it glows by itself, as tg draws its sprite on the emissive plane
// (sm-emissive), by the strength of the frame of tg's icon state it plays (the pulse). The owner (2026-10-21): the crystal is see-through, by a shader of its own.
// It is drawn opaque over what it bends (ALPHA 1, depth written), so the crystals of one cluster need no sorting among themselves.
shader_type spatial;
render_mode blend_mix, cull_back, depth_draw_always, diffuse_burley, specular_disabled, shadows_disabled;

#include "res://shaders/light_grid.gdshaderinc"
#include "res://shaders/model_pulse.gdshaderinc"

uniform sampler2D screen : hint_screen_texture, filter_linear_mipmap, repeat_disable;
uniform vec3 tint = vec3(0.68, 0.35, 0.02); // the part's colour (linear: its glTF base colour)
uniform vec3 halo = vec3(1.0, 0.75, 0.2); // the silhouette's glow (linear)
uniform float clarity = 0.7; // how much of the room behind shows through a facet that faces the eye
// the self-lit share of the colour at a frame of strength 1: a soft glow in any light, its peaks over the environment glow's threshold so a faint halo
// blooms round it (the owner, 2026-10-21: a little emission in its normal state too)
uniform float glow = 0.55;

varying vec3 v_world;
varying vec3 v_local;

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_local = VERTEX;
}

void fragment() {
	float strength;
	vec3 scale;
	anomaly_pulse(TIME, strength, scale);
	vec3 body = tint * scale;
	float facing = clamp(dot(NORMAL, VIEW), 0.0, 1.0);
	vec3 up = (INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz;
	float facet = anomaly_facet(v_local);
	// the flat props' shading (prop_atlas.gdshader's v_shade), each facet a little lighter or darker
	float shade = (0.55 + 0.3 * facing + 0.15 * max(up.y, 0.0)) * (0.85 + 0.3 * facet);
	// the room behind, bent more where the facet slants away, tinted by the crystal's hue (not darkened by it: its lines and edges stay readable)
	vec2 bend = NORMAL.xy * (0.03 + 0.07 * (1.0 - facing));
	vec3 hue = body / max(max(body.r, body.g), max(body.b, 0.001));
	vec3 behind = textureLod(screen, SCREEN_UV - bend, 0.5).rgb * mix(vec3(1.0), hue, 0.7);
	vec3 surface = body * shade * anomaly_grid_light(v_world);
	vec3 colour = mix(surface, behind, clarity * (0.4 + 0.6 * facing));
	// a band of light running up the facets, and a sparkle now and then on some of them
	float band = pow(0.5 + 0.5 * sin(v_world.y * 4.0 - TIME * 1.7 + facet * 6.2832), 8.0);
	float sparkle = step(0.55, facet) * pow(max(sin(TIME * (1.3 + 2.0 * facet) + facet * 40.0), 0.0), 40.0);
	colour += body * glow * strength * (0.6 + 0.8 * band);
	colour += halo * pow(1.0 - facing, 3.0) * (0.5 + 0.5 * strength);
	colour += vec3(1.0, 0.95, 0.8) * sparkle * 2.0;
	ALBEDO = vec3(0.0);
	ROUGHNESS = 1.0;
	EMISSION = colour;
	ALPHA = 1.0;
}
