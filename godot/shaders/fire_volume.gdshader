// A burning tile (Render/FireVolume.cs, D334): tg's hotspot is the tile's air on fire, its sprite (icons/effects/fire.dmi) a
// turbulent sheet added over the tile (BLEND_ADD). Here the same air glows in a box from the floor to the ceiling: the ray from
// the eye through the box is cut by the scene's depth and marched in a few steps through a rising 3D noise, stepped at tg's 10
// frames a second; the fire met along the ray sets the glow, in hard bands of a fire palette tinted by the hotspot's colour.
// The box reaches past its tile, and the fire fades over the overlap, so two burning neighbours make one even fire and a fire's
// edge is ragged. Nothing absorbs: the glow only adds, as tg's sprite does.
shader_type spatial;
render_mode unshaded, cull_front, depth_draw_never, depth_test_disabled, blend_add, shadows_disabled, fog_disabled;

uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
uniform sampler3D noise_tex : repeat_enable, filter_linear;
uniform vec3 core_half = vec3(1.0, 1.5, 1.0); // half the tile's air: a box this big has full glow, the rest of the box is the overlap
uniform float overlap = 0.35; // metres each side: the box reaches this far past its tile, the glow fades over twice this
uniform float noise_scale = 0.32;
uniform float rise = 0.9; // metres a second the flames climb
uniform float glow = 0.9; // how fast the fire met along the ray brightens to the palest band (per metre of the hottest air)
uniform float near_fade = 1.2; // metres: air this near the eye glows less, or a fire around the eye would whiten the screen

const int STEPS = 8;
const float FRAME = 0.1; // tg's fire sprite steps at 10 frames a second
const float PIXELS_PER_METRE = 16.0; // tg's 32 pixels on a 2 m tile
const float BANDS = 8.0;

instance uniform vec3 tint = vec3(0.98, 0.627, 0.098);
instance uniform float cut = 0.5; // the heat the air needs to burn: the lower, the more of the tile burns
instance uniform float alpha = 1.0; // tg's alpha of the hotspot (update_color): a cool fire is fainter

varying vec3 v_world;
varying vec3 v_centre;

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_centre = MODEL_MATRIX[3].xyz;
}

// The partition of the overlap: 1 inside the tile, falling to 0 at the box's edge, and two neighbours' weights sum to 1.
float weight(vec3 p) {
	vec3 d = abs(p - v_centre);
	vec3 w = clamp((core_half + overlap - d) / (2.0 * overlap), 0.0, 1.0);
	return w.x * w.y * w.z;
}

// tg's sprite's steps: a pale heart, yellow, the fire's colour, red, a dark rim
vec3 palette(float f) {
	return f > 0.8 ? vec3(1.0, 0.93, 0.62)
		: f > 0.6 ? vec3(1.0, 0.8, 0.22)
		: f > 0.4 ? mix(vec3(1.0, 0.68, 0.1), tint, 0.5)
		: f > 0.2 ? tint * vec3(1.0, 0.72, 0.6)
		: tint * vec3(1.0, 0.45, 0.3);
}

void fragment() {
	vec3 eye = INV_VIEW_MATRIX[3].xyz;
	vec3 ray = v_world - eye;
	vec3 dir = normalize(ray);
	vec3 half_box = core_half + overlap;
	vec3 inv = 1.0 / dir;
	vec3 t0 = (v_centre - half_box - eye) * inv;
	vec3 t1 = (v_centre + half_box - eye) * inv;
	vec3 tn = min(t0, t1);
	vec3 tf = max(t0, t1);
	float t_in = max(max(max(tn.x, tn.y), tn.z), 0.0);
	float t_out = min(min(tf.x, tf.y), tf.z);

	// nothing behind a wall or the floor burns
	float depth = texture(depth_tex, SCREEN_UV).r;
	vec4 view = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, depth, 1.0);
	view.xyz /= view.w;
	t_out = min(t_out, length(view.xyz));
	if (t_out <= t_in)
		discard;

	float time = floor(TIME / FRAME) * FRAME;
	vec3 drift = vec3(time * 0.07, -time * rise, time * 0.05);
	float step_len = (t_out - t_in) / float(STEPS);
	float burn = 0.0; // the heat met along the ray, in metres of the hottest air
	for (int i = 0; i < STEPS; i++) {
		float t = t_in + (float(i) + 0.5) * step_len;
		vec3 p = eye + dir * t;
		vec3 q = (floor(p * PIXELS_PER_METRE) + 0.5) / PIXELS_PER_METRE; // tg's pixel grid
		// two octaves: big tongues and their ragged edges
		float n = texture(noise_tex, (q + drift) * noise_scale).r * 0.7
			+ texture(noise_tex, (q + drift * 0.7) * noise_scale * 2.7).r * 0.3;
		float heat = (n - cut) / (1.0 - cut);
		if (heat > 0.0)
			burn += min(heat, 1.0) * weight(p) * min(1.0, t / near_fade) * step_len;
	}
	// in hard steps, as tg's sprite: the more fire the ray met, the paler and brighter
	float v = floor((1.0 - exp(-burn * glow)) * BANDS) / BANDS;
	if (v <= 0.0)
		discard;
	ALBEDO = pow(palette(v) * (0.3 + 0.7 * v) * alpha, vec3(2.2)); // tg's colours are sRGB, ALBEDO is linear
}
