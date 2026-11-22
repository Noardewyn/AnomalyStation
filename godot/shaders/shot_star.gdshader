// D176: a muzzle flash (Render/ShotFlash.cs): a unit quad turned to the eye with a star drawn in it, white in the middle and the flash's
// colour out along its four long rays and four short ones, added to the view. One material for every flash: the colour and strength are per instance.
shader_type spatial;
render_mode blend_add, unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

instance uniform vec4 tint : source_color = vec4(1.0);
instance uniform float strength = 1.0;
instance uniform float turn = 0.0; // radians: each flash's rays at their own angle

varying vec2 v_p;

void vertex() {
	v_p = VERTEX.xy * 2.0; // -1 .. 1
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0] * length(MODEL_MATRIX[0].xyz), INV_VIEW_MATRIX[1] * length(MODEL_MATRIX[1].xyz),
		INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
}

void fragment() {
	float c = cos(turn), s = sin(turn);
	vec2 p = vec2(c * v_p.x - s * v_p.y, s * v_p.x + c * v_p.y);
	float d = length(p);
	float ball = exp(-d * d * 18.0);
	float rays = exp(-abs(p.x) * 30.0) * max(0.0, 1.0 - abs(p.y)) + exp(-abs(p.y) * 30.0) * max(0.0, 1.0 - abs(p.x));
	vec2 q = vec2(p.x + p.y, p.x - p.y) * 0.7071;
	rays += 0.5 * (exp(-abs(q.x) * 40.0) * max(0.0, 1.0 - 1.6 * abs(q.y)) + exp(-abs(q.y) * 40.0) * max(0.0, 1.0 - 1.6 * abs(q.x)));
	float a = clamp(ball + rays, 0.0, 1.5) * strength;
	ALBEDO = mix(tint.rgb, vec3(1.0), clamp(ball * 1.5, 0.0, 1.0)) * a * 2.0;
	ALPHA = 1.0;
}
