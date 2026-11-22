// DV374: a mob the own SEE_MOBS shows (tg's TRAIT_THERMAL_VISION: the thermal scanners, the x-ray goggles) behind the walls (Render/Billboard.cs,
// ThroughWalls): the mob's picture again, on the picture's own billboard and depth (picture_billboard.gdshader), drawn only where the world in
// front of it hides it (the scene's depth is nearer than its own), and never over the things in the own hands (their slice next to the near
// plane, held_item_view.gdshaderinc: 0.94 and up, Godot's reversed z).
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, depth_test_disabled, shadows_disabled;

#include "res://shaders/depth_order.gdshaderinc"

uniform sampler2D picture : source_color, filter_nearest, repeat_disable;
uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
uniform vec4 tint : source_color = vec4(1.0);
instance uniform float depth_rank = 0.0;
instance uniform bool depth_snap = false;
// DV434: a walking mob's tilt about its feet in the picture's plane (radians, Billboard.Sway; Shared/Play/WalkSway.cs)
instance uniform float sway = 0.0;

void vertex() {
	VERTEX.xy = mat2(vec2(cos(sway), sin(sway)), vec2(-sin(sway), cos(sway))) * VERTEX.xy;
	vec3 right = normalize(vec3(MAIN_CAM_INV_VIEW_MATRIX[0].x, 0.0, MAIN_CAM_INV_VIEW_MATRIX[0].z));
	vec3 up = vec3(0.0, 1.0, 0.0);
	vec3 back = cross(right, up);
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(vec4(right, 0.0), vec4(up, 0.0), vec4(back, 0.0), MODEL_MATRIX[3]);
	MODELVIEW_MATRIX = depth_ordered(MODELVIEW_MATRIX, VIEW_MATRIX, PROJECTION_MATRIX, MODEL_MATRIX[3].xyz, depth_snap, depth_rank);
}

void fragment() {
	vec4 c = texture(picture, UV);
	float scene = texture(depth_tex, SCREEN_UV).r;
	// the mob's own picture is at its depth (not nearer): there it is seen and not drawn again
	if (c.a < 0.5 || scene <= FRAGCOORD.z * 1.0002 || scene >= 0.94) {
		discard;
	}
	ALBEDO = c.rgb * tint.rgb;
	ALPHA = tint.a;
}
