// The material of a mob's or a dynamic item's picture (Render/Billboard.cs, D104): tg's composed sprite on a quad, a Y-axis billboard
// (it turns about the vertical axis to face the camera). A lying mob is the same billboard over a picture turned a quarter in its own
// plane (Shared/Icons/LyingPicture.cs). M3 I1c: it lights like every other world
// surface (docs/ai/specs/light-grid.md section 7): ALBEDO stays for the real shadowed Godot lights, and ALBEDO * anomaly_grid_light adds the grid's
// light to EMISSION. The StandardMaterial3D it replaces had no grid term, so a mob under a lit lamp was lit by the few shadowed
// Light3D nodes alone (and by their angle to a normal that faces the camera): dark and shaded in a lit room while the props around it,
// which do have the term (prop_atlas.gdshader), looked right (the owner's walk, 2026-10-09).
shader_type spatial;
render_mode cull_disabled, diffuse_burley, specular_disabled;

#include "res://shaders/light_grid.gdshaderinc"
#define ANOMALY_PICTURE_LIGHT
#include "res://shaders/world_light.gdshaderinc"
#include "res://shaders/depth_order.gdshaderinc"

uniform sampler2D picture : source_color, filter_nearest, repeat_disable;
uniform float scissor = 0.5;
// DV67: a picture tg draws above the lighting (the singularity's ABOVE_LIGHTING_PLANE) glows in its own colours whatever the light, this many
// times as bright (0: lit as any picture; Billboard.Fullbright)
instance uniform float fullbright = 0.0;
// Its place in its tile's order (Billboard.SetDepthRank: Shared/World/DepthRank.cs, metres), snapped to its tile's centre (an item) or not (a mob).
// An item on the floor faces the eye fully (its up is the camera's up, tilted back about its feet: the owner, 2026-11-19); a mob turns about the vertical axis only
instance uniform bool face_eye = false;
// a thing tuned to lie on the floor (WorldLooks' floor mode; a floor-layer thing that moved): flat, its picture's up pointing north
instance uniform bool lie_flat = false;
instance uniform float depth_rank = 0.0;
instance uniform bool depth_snap = false;
// DV434: a walking mob's tilt about its feet in the picture's plane (radians, Billboard.Sway; Shared/Play/WalkSway.cs)
instance uniform float sway = 0.0;
// T5a: the own body for its own eye (Billboard.FacesBack): the eye is ahead of it, so the card shows the face a viewer ahead sees, its picture's
// right on the view's left
instance uniform bool face_back = false;

varying vec3 v_world;

void vertex() {
	VERTEX.xy = mat2(vec2(cos(sway), sin(sway)), vec2(-sin(sway), cos(sway))) * VERTEX.xy;
	// Turned about the vertical axis only: the picture's right is the camera's right on the ground plane (as the props' billboards).
	// D288: turned to the main camera in the shadow passes too, so its shadow is the picture the eye sees and it never shades itself
	vec3 right = normalize(vec3(MAIN_CAM_INV_VIEW_MATRIX[0].x, 0.0, MAIN_CAM_INV_VIEW_MATRIX[0].z));
	vec3 up = face_eye ? cross(MAIN_CAM_INV_VIEW_MATRIX[2].xyz, right) : vec3(0.0, 1.0, 0.0);
	up = normalize(vec3(up.x, max(up.y, 0.0), up.z)); // T5a: past straight down the camera's up points into the floor; there it lies flat
	if (face_back) {
		right = -right;
	}
	vec3 back = cross(right, up);
	if (lie_flat) {
		right = vec3(1.0, 0.0, 0.0);
		up = vec3(0.0, 0.0, -1.0);
		back = vec3(0.0, 1.0, 0.0);
	}
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(vec4(right, 0.0), vec4(up, 0.0), vec4(back, 0.0), MODEL_MATRIX[3]);
	MODELVIEW_MATRIX = depth_ordered(MODELVIEW_MATRIX, VIEW_MATRIX, PROJECTION_MATRIX, MODEL_MATRIX[3].xyz, depth_snap, depth_rank);
	MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
	NORMAL = vec3(0.0, 0.0, 1.0);
	// The grid is a floor grid (the light of the tile under the feet, blended over its corners); a picture reads it where it stands.
	v_world = MODEL_MATRIX[3].xyz + vec3(0.0, 0.3, 0.0);
}

void fragment() {
	vec4 c = texture(picture, UV);
	c.rgb = anomaly_previewed(c.rgb, v_world - vec3(0.0, 0.3, 0.0), 0.0); // the editor's preview, asked where the picture stands
	ALBEDO = fullbright > 0.0 ? vec3(0.0) : c.rgb;
	ALPHA = c.a;
	ALPHA_SCISSOR_THRESHOLD = scissor;
	ROUGHNESS = 1.0;
	EMISSION = c.rgb * (fullbright > 0.0 ? vec3(fullbright) : anomaly_grid_light(v_world));
	if (!FRONT_FACING) {
		NORMAL = -NORMAL;
	}
	vec3 wn = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	EMISSION += ALBEDO * anomaly_sky_light(v_world, wn);
	anomaly_sun_share = anomaly_sun_share_at(v_world, wn, length(VERTEX));
}
