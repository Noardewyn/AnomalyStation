// The sun's lens flare (the owner, 2026-10-26: a cinematic look): a screen-filling quad drawn over everything, added on top. Where the sun
// stands on the screen, how much of its disc the scene's depth leaves open to the sky (a wall, the hull, a person hide it; a window's glass
// writes no depth and lets it through), and from that the camera's artefacts: a soft glow, a short horizontal streak (an anamorphic lens's),
// and a few ghosts, the lens's reflections, strung on the line from the sun through the screen's centre.
shader_type spatial;
render_mode unshaded, depth_draw_never, depth_test_disabled, cull_disabled, blend_add, shadows_disabled, fog_disabled;

uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
uniform vec3 sun_dir = vec3(0.0, 1.0, 0.0);
uniform float strength = 1.0;
uniform bool ghosts = true; // the third-person view shows the streak only
uniform float sun_size = 0.035; // its disc's radius, radians (the sky's)

// A few small ghosts in the aperture's shape (a six-bladed iris), faint, with a coloured fringe, as photographs of the sun show them.
const int GHOSTS = 4;
const float GHOST_AT[GHOSTS] = float[](0.6, 1.25, 1.55, 1.9);          // along the line, 1 = the screen's centre
const float GHOST_SIZE[GHOSTS] = float[](0.012, 0.022, 0.035, 0.05);  // radius, in screen heights
const vec3 GHOST_TINT[GHOSTS] = vec3[](vec3(1.0, 0.75, 0.45), vec3(0.5, 0.85, 0.75), vec3(0.6, 0.6, 1.0), vec3(0.9, 0.6, 0.5));

// a hexagon's distance (1 on its edge)
float hexagon(vec2 p) {
	p = abs(p);
	return max(p.x * 0.866 + p.y * 0.5, p.y);
}

void vertex() {
	POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

float sky_at(vec2 uv) {
	if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0)
		return 0.0;
	return texture(depth_tex, uv).r <= 0.0000001 ? 1.0 : 0.0;
}

void fragment() {
	vec4 clip = PROJECTION_MATRIX * (VIEW_MATRIX * vec4(sun_dir, 0.0));
	if (clip.w <= 0.0)
		discard; // behind the camera
	vec2 sun_uv = clip.xy / clip.w * 0.5 + 0.5;
	float aspect = VIEWPORT_SIZE.x / VIEWPORT_SIZE.y;
	// the disc's radius on the screen (uv of its height), from the projection's vertical scale
	float disc = sun_size * PROJECTION_MATRIX[1][1] * 0.5;
	// how open the disc is: its centre and eight points of its rim
	float open = sky_at(sun_uv);
	for (int i = 0; i < 8; i++) {
		float a = float(i) * 0.7853982;
		open += sky_at(sun_uv + vec2(cos(a) / aspect, sin(a)) * disc * 0.8);
	}
	open /= 9.0;
	// less as the sun leaves the screen's middle
	vec2 from_centre = (sun_uv - 0.5) * vec2(aspect, 1.0);
	float k = open * strength * clamp(1.2 - length(from_centre), 0.0, 1.0);
	if (k <= 0.001)
		discard;
	vec2 p = (SCREEN_UV - 0.5) * vec2(aspect, 1.0);
	vec2 s = from_centre;
	vec3 col = vec3(0.0);
	// the glow and a short, thin streak about the sun
	float d = length(p - s);
	col += vec3(1.0, 0.9, 0.75) * 0.2 * exp(-d * 10.0);
	col += vec3(0.75, 0.85, 1.0) * 0.22 * exp(-abs(p.y - s.y) * 420.0) * exp(-abs(p.x - s.x) * 7.0);
	// the ghosts: a faint fill, a little brighter at the rim
	for (int i = 0; i < (ghosts ? GHOSTS : 0); i++) {
		float g = hexagon((p - s * (1.0 - GHOST_AT[i])) / GHOST_SIZE[i]);
		col += GHOST_TINT[i] * (smoothstep(1.0, 0.85, g) * 0.03 + smoothstep(0.08, 0.0, abs(g - 0.95)) * 0.025);
	}
	ALBEDO = col * k;
}
