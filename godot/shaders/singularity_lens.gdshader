// M5x slice 5 (D150, DV50): the gravitational lensing the owner asked for around tg's singularity. A camera-facing disc behind the
// singularity's billboard bends the opaque scene drawn before it toward its centre, more strongly closer in, and fades out at its rim.
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, blend_mix, shadows_disabled;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
// how far a point at the hole's edge is pulled toward the centre (a share of its distance to it)
uniform float strength = 0.45;
// the radius, as a share of the disc's, inside which the picture of the singularity sits (the bending is strongest there)
uniform float hole = 0.22;

// DV67: a soft glow of the singularity's colours around its hole, so the dark sprite stands out of the dark (SingularityLens.SetHalo)
uniform vec3 halo_color = vec3(0.0);
uniform float halo_strength = 0.0;

varying vec2 centre_screen;

void vertex() {
	// a billboard: the quad faces the camera, at the node's place
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
	vec4 c = PROJECTION_MATRIX * MODELVIEW_MATRIX * vec4(0.0, 0.0, 0.0, 1.0);
	centre_screen = (c.xy / c.w) * 0.5 + 0.5;
}

void fragment() {
	vec2 q = UV - 0.5;
	float r = length(q) * 2.0; // 0 at the centre, 1 at the rim
	if (r > 1.0) {
		discard;
	}
	// the pull grows inward from the rim to the hole, as 1/r beyond it
	float fall = smoothstep(1.0, hole, r) * (hole / max(r, hole));
	vec2 from_centre = SCREEN_UV - centre_screen;
	vec2 bent = SCREEN_UV - from_centre * strength * fall;
	// the glow is brightest just outside the hole and fades out well before the rim
	float glow = halo_strength * exp(-pow((r - hole) / 0.22, 2.0)) * smoothstep(1.0, 0.6, r);
	ALBEDO = texture(screen_tex, bent).rgb + halo_color * glow;
	ALPHA = smoothstep(1.0, 0.85, r);
}
