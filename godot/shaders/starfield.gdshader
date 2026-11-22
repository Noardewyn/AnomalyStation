// The black starfield behind the station (M3 P6, D116: space is dark; stars are not light). Hash-placed points on the sky sphere.
// Over it tg's parallax pictures (icons/effects/parallax.dmi) as far bodies: the planet with its moon straight below the station, the ice moon
// above, single rocks of its asteroids layer beside it (Starfield.cs places them), and the sun at SSsun's azimuth.
shader_type sky;

uniform float density = 0.0035;
uniform float brightness = 1.0;

uniform sampler2D sheet : source_color, filter_nearest, repeat_disable;
// Each body faces the station with its picture's up fixed in the world: the world's up, or north for one near the zenith or the nadir
// (the planet below), so it never turns as the camera turns. Its centre on the sky, 1 / tan(half its width and height), its picture's
// rect in the sheet (uv), and its gain (the dark rocks are brightened).
const int MAX_BODIES = 24;
uniform int body_count = 0;
uniform vec3 body_center[MAX_BODIES];
uniform vec2 body_scale[MAX_BODIES];
uniform vec4 body_rect[MAX_BODIES];
uniform float body_gain[MAX_BODIES];
// The ice moon on its orbit (Starfield.SetMoon): like a body, drawn before body moon_before (behind the planet) or after them all.
uniform vec3 moon_center = vec3(0.0, 1.0, 0.0);
uniform vec2 moon_scale;
uniform vec4 moon_rect;
uniform int moon_before = 0;
uniform vec3 sun_dir = vec3(0.0, 0.57, -0.82);
uniform float sun_size = 0.035; // its disc's radius, radians
// a map's own sky and sun (protocol 87): no sun, its colour (the glow about it and the disc), and the stars with none of the far bodies
uniform bool sun_on = true;
uniform vec3 sun_glow = vec3(1.0, 0.92, 0.78);
uniform vec3 sun_disc = vec3(4.0, 3.7, 3.2);
uniform bool bodies_on = true;
// A ship in hyperspace (Starfield.SetHyperspace): the tunnel streaming along hyper_flow replaces the sky.
uniform vec3 hyper_flow = vec3(0.0, 0.0, 1.0);
uniform float hyper_amount = 0.0;
uniform float hyper_phase = 0.0;

float hash(vec3 p) {
	p = fract(p * 0.3183099 + vec3(0.71, 0.113, 0.419));
	p *= 17.0;
	return fract(p.x * p.y * p.z * (p.x + p.y + p.z));
}

vec4 picture(vec3 dir, vec3 center, vec2 scale, vec4 rect, float gain) {
	float p = dot(dir, center);
	if (p <= 0.0)
		return vec4(0.0);
	vec3 right = normalize(cross(center, abs(center.y) > 0.9 ? vec3(0.0, 0.0, -1.0) : vec3(0.0, 1.0, 0.0)));
	vec3 up = cross(right, center);
	vec2 q = vec2(dot(dir, right), dot(dir, up)) * scale / p;
	// stop a texel short of the state's edge: the sheet's neighbouring state would bleed in as a stripe
	float px = rect.z * float(textureSize(sheet, 0).x);
	float edge = px > 400.0 ? 1.0 - 2.0 / px : 1.0; // whole states only: the rocks' boxes are tight
	if (abs(q.x) > edge || abs(q.y) > edge)
		return vec4(0.0);
	vec4 c = texture(sheet, rect.xy + vec2(q.x * 0.5 + 0.5, 0.5 - q.y * 0.5) * rect.zw);
	c.rgb *= gain;
	return c;
}

float noise(vec3 p) {
	vec3 i = floor(p);
	vec3 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(
		mix(mix(hash(i), hash(i + vec3(1.0, 0.0, 0.0)), f.x), mix(hash(i + vec3(0.0, 1.0, 0.0)), hash(i + vec3(1.0, 1.0, 0.0)), f.x), f.y),
		mix(mix(hash(i + vec3(0.0, 0.0, 1.0)), hash(i + vec3(1.0, 0.0, 1.0)), f.x), mix(hash(i + vec3(0.0, 1.0, 1.0)), hash(i + vec3(1.0, 1.0, 1.0)), f.x), f.y),
		f.z);
}

// D300: the Stargate series' hyperspace, a tunnel of blue energy around the ship. Its wall is a cylinder about the flight's axis: clouds drawn out
// along it and bright rippling filaments stream past, white ahead of the ship and deep blue behind.
vec3 tunnel(vec3 dir) {
	vec3 side = normalize(cross(hyper_flow, abs(hyper_flow.y) > 0.9 ? vec3(0.0, 0.0, 1.0) : vec3(0.0, 1.0, 0.0)));
	vec3 over = cross(hyper_flow, side);
	vec2 around = vec2(dot(dir, side), dot(dir, over));
	float along = dot(dir, hyper_flow);
	float radius = max(length(around), 0.001);
	float depth = clamp(along / radius, -8.0, 8.0);
	float angle = atan(around.y, around.x) + 0.25 * sin(depth * 0.9 - hyper_phase * 1.3) + 0.08 * hyper_phase;
	float z = depth - hyper_phase * 3.0;
	// on a circle, so the noise has no seam where the angle wraps
	vec2 ring = vec2(cos(angle), sin(angle));
	float clouds = noise(vec3(ring * 2.5, z * 0.22)) * 0.6 + noise(vec3(ring * 6.0, z * 0.45 + 11.0)) * 0.3 + noise(vec3(ring * 13.0, z * 0.9 + 23.0)) * 0.1;
	float filaments = pow(1.0 - abs(2.0 * noise(vec3(ring * 9.0, z * 0.16 + 5.0)) - 1.0), 14.0);
	float fine = pow(1.0 - abs(2.0 * noise(vec3(ring * 24.0, z * 0.3 + 41.0)) - 1.0), 22.0);
	float wall = smoothstep(0.3, 0.75, clouds);
	vec3 c = mix(vec3(0.005, 0.03, 0.14), vec3(0.04, 0.26, 0.82), wall);
	c += vec3(0.45, 0.72, 1.0) * (filaments * 0.9 + fine * 0.5);
	// both ends melt into plain colour before depth's clamp: the pattern would pinch into a star inside a visible ring there
	float ahead = smoothstep(0.93, 0.99, -along); // the flight goes against the flow
	c = mix(c, vec3(0.55, 0.75, 1.0) * 0.9, ahead * ahead);
	c = mix(c, vec3(0.008, 0.05, 0.2), smoothstep(0.45, 0.96, along));
	return c;
}

void sky() {
	vec3 dir = normalize(EYEDIR);
	vec3 col = vec3(0.0, 0.0, 0.004);
	float scale = 140.0;
	vec3 cell = floor(dir * scale);
	vec3 local = fract(dir * scale);
	float h = hash(cell);
	if (h > 1.0 - density * 8.0) {
		vec3 center = vec3(hash(cell + 1.7), hash(cell + 4.3), hash(cell + 9.1));
		float d = length(local - center);
		float size = 0.09 + 0.12 * hash(cell + 2.2);
		float star = smoothstep(size, 0.0, d);
		float temp = hash(cell + 6.6);
		vec3 tint = mix(vec3(0.7, 0.8, 1.0), vec3(1.0, 0.85, 0.7), temp);
		col += tint * star * (0.4 + 0.6 * hash(cell + 3.3)) * brightness;
	}
	if (sun_on) {
		float a = acos(clamp(dot(dir, sun_dir), -1.0, 1.0));
		col += sun_glow * (exp(-a * 9.0) * 0.35 + exp(-a * 40.0) * 0.6);
		col = mix(col, sun_disc, smoothstep(sun_size, sun_size * 0.85, a));
	}
	vec4 moon = moon_rect.z > 0.0 && bodies_on ? picture(dir, moon_center, moon_scale, moon_rect, 1.0) : vec4(0.0); // none without the sheet
	int count = bodies_on ? min(body_count, MAX_BODIES) : 0;
	for (int i = 0; i < count; i++) {
		if (i == moon_before)
			col = mix(col, moon.rgb, moon.a);
		vec4 c = picture(dir, body_center[i], body_scale[i], body_rect[i], body_gain[i]);
		col = mix(col, c.rgb, c.a);
	}
	if (moon_before >= count)
		col = mix(col, moon.rgb, moon.a);
	if (hyper_amount > 0.0)
		col = mix(col, tunnel(dir), hyper_amount);
	COLOR = col;
}
