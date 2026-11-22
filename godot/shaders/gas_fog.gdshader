// tg's gas overlays as a haze that fills the cell (M4b slice 3, D18; docs/ai/specs/apps.md 2.16). One box per cell that shows
// gas (GasLayer's MultiMesh), drawn by its back faces so a camera inside the gas still sees it: the ray from the eye through
// the pixel is cut by the box and by the scene's depth, and the gas over that length gives the alpha (1 - exp(-density *
// length)): two neighbouring cells of the same gas add up to one cell twice as long, so the cell edges do not show. The
// colour and the density are per cell (INSTANCE_CUSTOM: rgb, density per metre), taken from tg's own gas sprites and
// levels; a slow 3D noise moves through the gas the way tg's sprites animate. Lit by the light grid like every other
// world surface (light-grid.md section 7): a gas in a dark room is dark.
shader_type spatial;
render_mode unshaded, cull_front, depth_draw_never, depth_test_disabled, blend_mix, shadows_disabled, fog_disabled;

#include "res://shaders/light_grid.gdshaderinc"

uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
uniform sampler3D noise_tex : repeat_enable, filter_linear;
uniform float noise_scale = 0.22;
uniform float noise_speed = 0.12;
uniform float noise_strength = 0.75;

varying vec3 v_world;
varying vec3 v_min;
varying vec3 v_max;
varying vec4 v_gas;

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	// The box is a unit cube scaled and moved (never turned): its corners in the world.
	vec3 a = (MODEL_MATRIX * vec4(-0.5, -0.5, -0.5, 1.0)).xyz;
	vec3 b = (MODEL_MATRIX * vec4(0.5, 0.5, 0.5, 1.0)).xyz;
	v_min = min(a, b);
	v_max = max(a, b);
	v_gas = INSTANCE_CUSTOM;
}

void fragment() {
	vec3 eye = (INV_VIEW_MATRIX * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
	vec3 ray = v_world - eye;
	float far = length(ray);
	vec3 dir = ray / far;
	vec3 inv = 1.0 / dir;
	vec3 t0 = (v_min - eye) * inv;
	vec3 t1 = (v_max - eye) * inv;
	vec3 tn = min(t0, t1);
	vec3 tf = max(t0, t1);
	float t_in = max(max(max(tn.x, tn.y), tn.z), 0.0);
	float t_out = min(min(tf.x, tf.y), tf.z);

	// The scene's depth along the ray: nothing behind a wall or the floor is gas.
	float depth = texture(depth_tex, SCREEN_UV).r;
	vec4 view = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, depth, 1.0);
	view.xyz /= view.w;
	vec3 scene = (INV_VIEW_MATRIX * vec4(view.xyz, 1.0)).xyz;
	t_out = min(t_out, length(scene - eye));

	float length_in = max(t_out - t_in, 0.0);
	if (length_in <= 0.0) {
		discard;
	}
	vec3 mid = eye + dir * (t_in + t_out) * 0.5;
	vec3 drift = vec3(TIME * noise_speed, TIME * noise_speed * 0.35, -TIME * noise_speed * 0.6);
	float n = texture(noise_tex, mid * noise_scale + drift).r;
	float density = v_gas.a * max(0.0, 1.0 + noise_strength * (n * 2.0 - 1.0));
	ALBEDO = v_gas.rgb * anomaly_grid_light(mid);
	ALPHA = clamp(1.0 - exp(-density * length_in), 0.0, 1.0);
}
