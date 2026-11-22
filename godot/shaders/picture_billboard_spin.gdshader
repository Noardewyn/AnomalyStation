// The material of a thrown picture (M4a P7, D127): picture_billboard.gdshader plus a roll of the quad about its middle, so that a thing in flight tumbles.
// Render/EntityLayer.cs swaps it in for the entities flagged Thrown and back when they land.
shader_type spatial;
render_mode cull_disabled, diffuse_burley, specular_disabled;

#include "res://shaders/light_grid.gdshaderinc"
#define ANOMALY_PICTURE_LIGHT
#include "res://shaders/world_light.gdshaderinc"
#include "res://shaders/depth_order.gdshaderinc"

uniform sampler2D picture : source_color, filter_nearest, repeat_disable;
uniform float scissor = 0.5;
// The roll in radians and the point of the quad it turns about (metres, in the quad's own plane).
uniform float spin = 0.0;
uniform vec2 pivot = vec2(0.0, 0.0);
// Its place in its tile's order (Billboard.SetDepthRank: Shared/World/DepthRank.cs, metres), snapped to its tile's centre (an item) or not (a mob).
// An item on the floor faces the eye fully (its up is the camera's up, tilted back about its feet: the owner, 2026-11-19); a mob turns about the vertical axis only
instance uniform bool face_eye = false;
// a thing tuned to lie on the floor (WorldLooks' floor mode; a floor-layer thing that moved): flat, its picture's up pointing north
instance uniform bool lie_flat = false;
instance uniform float depth_rank = 0.0;
instance uniform bool depth_snap = false;

varying vec3 v_world;

void vertex() {
	float c = cos(spin);
	float s = sin(spin);
	vec2 p = VERTEX.xy - pivot;
	VERTEX.xy = vec2(p.x * c - p.y * s, p.x * s + p.y * c) + pivot;
	// D288: turned to the main camera in the shadow passes too, so its shadow is the picture the eye sees and it never shades itself
	vec3 right = normalize(vec3(MAIN_CAM_INV_VIEW_MATRIX[0].x, 0.0, MAIN_CAM_INV_VIEW_MATRIX[0].z));
	vec3 up = face_eye ? normalize(cross(MAIN_CAM_INV_VIEW_MATRIX[2].xyz, right)) : vec3(0.0, 1.0, 0.0);
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
	v_world = MODEL_MATRIX[3].xyz + vec3(0.0, 0.3, 0.0);
}

void fragment() {
	vec4 c = texture(picture, UV);
	c.rgb = anomaly_previewed(c.rgb, v_world - vec3(0.0, 0.3, 0.0), 0.0); // the editor's preview, asked where the picture stands
	ALBEDO = c.rgb;
	ALPHA = c.a;
	ALPHA_SCISSOR_THRESHOLD = scissor;
	ROUGHNESS = 1.0;
	EMISSION = c.rgb * anomaly_grid_light(v_world);
	if (!FRONT_FACING) {
		NORMAL = -NORMAL;
	}
	vec3 wn = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	EMISSION += ALBEDO * anomaly_sky_light(v_world, wn);
	anomaly_sun_share = anomaly_sun_share_at(v_world, wn, length(VERTEX));
}
