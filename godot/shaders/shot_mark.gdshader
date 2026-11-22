// The mark a shot leaves where it hit (Render/ShotMark.cs; the owner, 2026-11-11): a bullet's hole or an energy shot's scorch. A small box
// about the hit point darkens whatever opaque surface lies inside its ball (found from the depth, as ambient_occlusion.gdshader), so it
// needs neither the face's normal nor a decal (a decal's albedo would miss the light grid's emission). Darkest in the middle, soft outside.
shader_type spatial;
render_mode unshaded, cull_front, depth_draw_never, depth_test_disabled, blend_mul, fog_disabled, shadows_disabled;

uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
instance uniform float strength = 1.0;

void fragment() {
	vec4 view = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, texture(depth_tex, SCREEN_UV).r, 1.0);
	vec3 world = (INV_VIEW_MATRIX * vec4(view.xyz / view.w, 1.0)).xyz;
	float radius = length(MODEL_MATRIX[0].xyz) * 0.5; // the unit box scaled to the mark's diameter
	float d = length(world - NODE_POSITION_WORLD) / radius;
	ALBEDO = vec3(1.0 - (1.0 - smoothstep(0.3, 1.0, d)) * 0.85 * strength);
}
