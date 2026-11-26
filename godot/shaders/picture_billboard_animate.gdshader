// The material of a picture that plays BYOND's animate() (M4v slice 1, protocol version 9): picture_billboard.gdshader plus the animation's
// look over the composed picture (Shared/Icons/AnimationPlayback.cs): a 2D transform in the quad's own plane about a pivot (tg's transform
// after the picture's own; pixel_w as a move to the side), and a colour and alpha factor (tg's color and alpha). An alpha below one is
// drawn as a screen-door dither, so the picture stays in the opaque pass. Render/EntityLayer.cs swaps it in while an animation plays
// (and keeps it on a short-lived visual, M4v slice 3).
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
// The transform's linear part (a, b, d, e of BYOND's matrix: x' = a x + b y, y' = d x + e y), its move (metres) and the point of the
// quad it turns about (metres, in the quad's own plane); the colour factor (rgb) and the alpha factor (a).
uniform vec4 affine = vec4(1.0, 0.0, 0.0, 1.0);
uniform vec2 offset = vec2(0.0, 0.0);
uniform vec2 pivot = vec2(0.0, 0.0);
uniform vec4 modulate = vec4(1.0);
// M4v slice 3: a short-lived visual on a wall decal (Shared/World/VisualAnchor.cs) lies flat in its node's plane instead of turning to the viewer.
uniform bool fixed_plane = false;
// Its place in its tile's order (Billboard.SetDepthRank: Shared/World/DepthRank.cs, metres), snapped to its tile's centre (an item) or not (a mob).
// An item on the floor faces the eye fully (its up is the camera's up, tilted back about its feet: the owner, 2026-11-19); a mob turns about the vertical axis only
instance uniform bool face_eye = false;
// a thing tuned to lie on the floor (WorldLooks' floor mode; a floor-layer thing that moved): flat, its picture's up pointing north
instance uniform bool lie_flat = false;
instance uniform float depth_rank = 0.0;
instance uniform bool depth_snap = false;
// DV434: a walking mob's tilt about its feet in the picture's plane (radians, Billboard.Sway; Shared/Play/WalkSway.cs)
instance uniform float sway = 0.0;
// T5a: the own body for its own eye, its picture's right on the view's left (picture_billboard.gdshader's face_back)
instance uniform bool face_back = false;

varying vec3 v_world;

void vertex() {
	vec2 p = VERTEX.xy - pivot;
	VERTEX.xy = vec2(affine.x * p.x + affine.y * p.y, affine.z * p.x + affine.w * p.y) + pivot + offset;
	VERTEX.xy = mat2(vec2(cos(sway), sin(sway)), vec2(-sin(sway), cos(sway))) * VERTEX.xy;
	if (!fixed_plane) {
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
	}
	NORMAL = vec3(0.0, 0.0, 1.0);
	v_world = MODEL_MATRIX[3].xyz + vec3(0.0, 0.3, 0.0);
}

void fragment() {
	vec4 c = texture(picture, UV) * modulate;
	// Interleaved gradient noise: a stable threshold per screen pixel for a faded picture, the scissor otherwise.
	float noise = fract(52.9829189 * fract(dot(FRAGCOORD.xy, vec2(0.06711056, 0.00583715))));
	if (c.a < (modulate.a < 0.999 ? max(noise, 0.004) : scissor)) {
		discard;
	}
	c.rgb = anomaly_previewed(c.rgb, v_world - vec3(0.0, 0.3, 0.0), 0.0); // the editor's preview, asked where the picture stands
	ALBEDO = fullbright > 0.0 ? vec3(0.0) : c.rgb;
	ROUGHNESS = 1.0;
	EMISSION = c.rgb * (fullbright > 0.0 ? vec3(fullbright) : anomaly_grid_light(v_world));
	if (!FRONT_FACING) {
		NORMAL = -NORMAL;
	}
	vec3 wn = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	EMISSION += ALBEDO * anomaly_sky_light(v_world, wn);
	anomaly_sun_share = anomaly_sun_share_at(v_world, wn, length(VERTEX));
}
