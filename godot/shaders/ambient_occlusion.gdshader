// Ambient occlusion over the whole lit view: Godot's SSAO darkens only the ambient and the engine lamps, while the station is lit
// by the light grid, which the world's shaders write to EMISSION, so its corners and the floor under things stayed flat. A quad
// over the view (as outline.gdshader), drawn first among the transparent things, multiplies the opaque pass by the occlusion of a
// normal-oriented hemisphere of depth samples; bright pixels (a lamp's tube, a screen) keep their light, and it fades with distance.
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, depth_test_disabled, blend_mul, fog_disabled, shadows_disabled;

uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
uniform sampler2D screen_tex : hint_screen_texture, filter_nearest;
uniform int samples = 12;
uniform float radius = 1.2;
uniform float strength = 1.5;
uniform float fade = 40.0;

void vertex() {
	POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

// The view point at a pixel, by the farthest depth of the pixel and its right, upper and diagonal neighbours (reversed Z: the least).
// Translucent things (glass, a lamp's glow, smoke) are screen-door dithers in the opaque pass, so their depth is a pixel checker of the
// thing and what is behind it, which read as relief and blackened them (the owner, 2026-11-12); any 2 x 2 block of a dither below about
// 0.7 has a hole, so this sees through it: the occlusion is of what is behind, the same on the thing's pixels and its holes.
vec3 view_at(vec2 uv, vec2 texel, mat4 inv_projection) {
	vec4 d = textureGather(depth_tex, uv + texel * 0.25);
	vec4 view = inv_projection * vec4(uv * 2.0 - 1.0, min(min(d.x, d.y), min(d.z, d.w)), 1.0);
	return view.xyz / view.w;
}

void fragment() {
	vec2 uv = SCREEN_UV;
	vec2 texel = 1.0 / VIEWPORT_SIZE;
	vec3 p = view_at(uv, texel, INV_PROJECTION_MATRIX);
	float far = 1.0 - smoothstep(fade * 0.5, fade, -p.z);
	vec3 seen = texture(screen_tex, uv).rgb;
	float lit = smoothstep(1.0, 2.0, max(seen.r, max(seen.g, seen.b)));
	// the face's own normal, from the depth (outside the branch, for the derivatives): the normal buffer's normal-mapped sprites
	// would tip the hemisphere into a flat floor
	vec3 n = normalize(cross(dFdx(p), dFdy(p)));
	n = dot(n, p) > 0.0 ? -n : n;
	float occlusion = 0.0;
	if (far > 0.0 && lit < 1.0) {
		vec3 t = normalize(cross(n, abs(n.y) < 0.99 ? vec3(0.0, 1.0, 0.0) : vec3(1.0, 0.0, 0.0)));
		vec3 b = cross(n, t);
		float bias = 0.02 + 0.002 * -p.z;
		// Interleaved sampling with an edge-aware 2 x 2 filter (the owner, 2026-11-11: the static per-pixel grain read as broken textures):
		// the spiral's turn is a quarter by the pixel's screen parity, and a pixel averages the occlusion of its own point and of its right,
		// upper and diagonal neighbours' points that lie on its plane, each with that neighbour's turn, so every pixel sees all four turns
		// and the grain cancels; a neighbour off the plane (across an edge or a crease) is left out, so the contact lines stay sharp.
		int count = max(samples / 2, 4);
		float plane_tolerance = 0.01 + 0.004 * -p.z;
		float weight = 0.0;
		for (int k = 0; k < 4; k++) {
			vec2 cell = FRAGCOORD.xy + vec2(float(k % 2), float(k / 2));
			vec3 kp = k == 0 ? p : view_at(cell * texel, texel, INV_PROJECTION_MATRIX);
			if (k > 0 && abs(dot(kp - p, n)) > plane_tolerance)
				continue;
			float turn = (mod(floor(cell.x), 2.0) + 2.0 * mod(floor(cell.y), 2.0)) * 1.5707963 + 0.3;
			float o = 0.0;
			for (int i = 0; i < count; i++) {
				float u = (float(i) + 0.5) / float(count) * 0.9; // none closer than 18 degrees to the surface, which its own relief would cover
				float a = float(i) * 2.3999632 + turn;
				vec3 h = vec3(sqrt(u) * cos(a), sqrt(u) * sin(a), sqrt(1.0 - u)); // cosine-weighted direction
				float s = fract(u * 7.0 + turn * 0.159155);
				float reach = radius * mix(0.15, 1.0, s * s); // more samples near the point, where the contact darkening is
				vec3 q = kp + (t * h.x + b * h.y + n * h.z) * reach;
				vec4 clip = PROJECTION_MATRIX * vec4(q, 1.0);
				vec2 quv = clip.xy / clip.w * 0.5 + 0.5;
				if (quv.x < 0.0 || quv.y < 0.0 || quv.x > 1.0 || quv.y > 1.0)
					continue;
				float scene = view_at(quv, texel, INV_PROJECTION_MATRIX).z;
				// what is seen far in front of the sample (a table's leg before the floor behind it, a far wall behind an edge) does not
				// occlude: the depth says nothing of how thick it is, and taking it for a wall smeared dark patches beside thin things
				float near = 1.0 - smoothstep(0.5, 1.0, (scene - q.z) / (0.2 + reach));
				o += step(q.z + bias, scene) * near;
			}
			occlusion += o / float(count);
			weight += 1.0;
		}
		occlusion /= weight;
	}
	ALBEDO = vec3(max(1.0 - occlusion * strength * far * (1.0 - lit), 0.0));
}
