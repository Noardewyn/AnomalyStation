// The third-person view's line of sight, as Space Station 14 draws it (the owner, 2026-10-16): a screen-filling quad that
// shades map surfaces the own mob cannot see. The pixel's world point comes from the scene's depth; empty sky is left clear. A point is in
// sight when the straight line from the eye to it on the tile plane crosses no opaque cell other than the eye's and its own
// (FovMask.Sees, step for step). A pixel on an opaque cell (a wall's face or top, a closed airlock) takes the cell's own answer instead
// (FovMask: the cell is in sight when any of it is), so a face on the border of two walls is not shadowed by the neighbour.
// fov_opaque is FovMask.Texels: one texel per cell of the window, 0 clear, 170/255 an opaque cell in sight, 255 one out of sight.
shader_type spatial;
render_mode unshaded, depth_draw_never, depth_test_disabled, cull_disabled, blend_mix, shadows_disabled, fog_disabled;

uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
uniform sampler2D fov_opaque : filter_nearest;
uniform vec2 fov_origin;   // the window's south-west cell, tg tiles
uniform float fov_side;    // cells per side of the window
uniform vec2 fov_eye;      // the eye, tg tiles (a pose's X and Y)
uniform float fov_darkness = 1.0;
uniform float fov_edge_fade = 4.0; // cells over which the window's edge fades out
const float BORDER = 0.03; // tiles: a pixel this close to an opaque cell is on its face

void vertex() {
	POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

float opaque_at(ivec2 cell) {
	ivec2 i = cell - ivec2(fov_origin);
	int side = int(fov_side);
	if (i.x < 0 || i.y < 0 || i.x >= side || i.y >= side) {
		return 1.0;
	}
	return texelFetch(fov_opaque, i, 0).r;
}

// 1.0 when the point (tiles) is seen from the eye.
float sees(vec2 p) {
	ivec2 target = ivec2(floor(p + 0.5));
	vec2 u = fov_eye + 0.5;
	vec2 d = p + 0.5 - u;
	ivec2 c = ivec2(floor(u));
	ivec2 stp = ivec2(d.x > 0.0 ? 1 : -1, d.y > 0.0 ? 1 : -1);
	vec2 t_delta = vec2(d.x != 0.0 ? abs(1.0 / d.x) : 1e30, d.y != 0.0 ? abs(1.0 / d.y) : 1e30);
	vec2 t_max = vec2(
		d.x != 0.0 ? (d.x > 0.0 ? float(c.x + 1) - u.x : u.x - float(c.x)) * t_delta.x : 1e30,
		d.y != 0.0 ? (d.y > 0.0 ? float(c.y + 1) - u.y : u.y - float(c.y)) * t_delta.y : 1e30);
	int steps = int(fov_side) * 4;
	for (int n = 0; n < steps; n++) {
		if ((c.x == target.x && c.y == target.y) || min(t_max.x, t_max.y) >= 1.0) {
			return 1.0;
		}
		if (t_max.x < t_max.y) {
			c.x += stp.x;
			t_max.x += t_delta.x;
		} else {
			c.y += stp.y;
			t_max.y += t_delta.y;
		}
		if (c.x == target.x && c.y == target.y) {
			return 1.0;
		}
		if (opaque_at(c) > 0.3) {
			return 0.0;
		}
	}
	return 1.0;
}

void fragment() {
	float depth = texture(depth_tex, SCREEN_UV).r;
	// Empty depth is the sky. Only geometry belonging to the map receives the visibility mask.
	if (depth <= 0.0000001) {
		discard;
	}
	vec4 view = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, depth, 1.0);
	view.xyz /= view.w;
	vec3 world = (INV_VIEW_MATRIX * vec4(view.xyz, 1.0)).xyz;
	// World metres to tg tiles (TileSpace: X = (x - 0.5) * 2, Z = -(y - 0.5) * 2).
	vec2 p = vec2(world.x * 0.5 + 0.5, -world.z * 0.5 + 0.5);
	float r = fov_side * 0.5 - 0.5;
	// On an opaque cell, or on the face of one: the cells it touches decide (any opaque one in sight lights it).
	ivec2 cell = ivec2(floor(p + 0.5));
	vec2 f = p + 0.5 - vec2(cell); // 0..1 inside the cell
	ivec2 side = ivec2(f.x < BORDER ? -1 : f.x > 1.0 - BORDER ? 1 : 0, f.y < BORDER ? -1 : f.y > 1.0 - BORDER ? 1 : 0);
	float wall = 0.0, wall_seen = 0.0;
	for (int k = 0; k < 4; k++) {
		ivec2 o = ivec2(k & 1, k >> 1) * side;
		if (k > 0 && o == ivec2(0)) {
			continue;
		}
		float t = opaque_at(cell + o);
		if (t > 0.3) {
			wall = 1.0;
			wall_seen = max(wall_seen, t < 0.85 ? 1.0 : 0.0);
		}
	}
	float seen = wall > 0.5 ? wall_seen : sees(p);
	// The window's edge fades into the dark instead of ending in a line.
	vec2 rel = abs(p - fov_eye);
	float edge = max(rel.x, rel.y);
	seen *= clamp((r - edge) / max(fov_edge_fade, 0.001), 0.0, 1.0);
	ALBEDO = vec3(0.0);
	ALPHA = (1.0 - seen) * fov_darkness;
}
