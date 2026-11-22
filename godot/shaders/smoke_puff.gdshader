// A tile of tg's fluid smoke (Render/SmokeVolume.cs, D303): round camera-facing puffs on tg's pixel grid, in hard grey bands, thinned by an ordered dither
// instead of blending, so a thick cloud costs little. With the eye inside the tile, one full-screen quad of the same dither stands for being in the cloud.
shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled, skip_vertex_transform;

const float PIXELS_PER_METRE = 16.0; // tg's 32 pixels on a 2 m tile
const float NEAR_HIDE = 0.9; // metres: a puff this near the eye is left to the full-screen quad
const float SCREEN_PIXEL = 6.0; // the full-screen dither's cell, in screen pixels

instance uniform vec3 tint = vec3(1.0);
instance uniform float density = 1.0;

varying flat float seed;
varying flat float size;
varying flat float inside;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

float bayer(vec2 p) {
	int x = int(mod(p.x, 4.0)), y = int(mod(p.y, 4.0));
	int cell = x + y * 4;
	const float m[16] = { 0.0, 8.0, 2.0, 10.0, 12.0, 4.0, 14.0, 6.0, 3.0, 11.0, 1.0, 9.0, 15.0, 7.0, 13.0, 5.0 };
	return (m[cell] + 0.5) / 16.0;
}

void vertex() {
	vec3 tile = MODEL_MATRIX[3].xyz;
	vec3 eye = INV_VIEW_MATRIX[3].xyz;
	inside = COLOR.b;
	seed = hash(tile.xz + COLOR.r * 113.0);
	size = COLOR.g * 2.0;
	if (inside > 0.5) {
		bool in_tile = abs(eye.x - tile.x) < 1.0 && abs(eye.z - tile.z) < 1.0 && eye.y > tile.y && eye.y < tile.y + 3.0;
		POSITION = in_tile ? vec4(UV * 2.0 - 1.0, 1.0, 1.0) : vec4(2.0, 2.0, 2.0, 1.0);
	} else {
		float t = floor(TIME * 10.0) / 10.0;
		vec3 centre = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz + vec3(sin(t * 0.7 + seed * 6.3), cos(t * 0.5 + seed * 9.1) * 0.5, cos(t * 0.6 + seed * 4.7)) * 0.12;
		float shown = distance(centre, eye) > NEAR_HIDE ? size : 0.0;
		vec3 world = centre + (INV_VIEW_MATRIX[0].xyz * (UV.x - 0.5) + INV_VIEW_MATRIX[1].xyz * (UV.y - 0.5)) * shown;
		VERTEX = (VIEW_MATRIX * vec4(world, 1.0)).xyz;
		POSITION = PROJECTION_MATRIX * vec4(VERTEX, 1.0); // a shader that writes POSITION anywhere must write it everywhere
	}
}

void fragment() {
	vec3 colour;
	if (inside > 0.5) {
		vec2 cell = floor(FRAGCOORD.xy / SCREEN_PIXEL);
		if (bayer(cell) > density * 0.8)
			discard;
		colour = vec3(0.5);
	} else {
		float pixels = max(floor(size * PIXELS_PER_METRE), 1.0);
		vec2 px = floor(UV * pixels);
		vec2 at = (px + 0.5) / pixels;
		float t = floor(TIME * 10.0) / 10.0;
		float n = noise(px / 4.0 + seed * 91.0 + vec2(t * 0.35, -t * 0.25));
		float d = distance(at, vec2(0.5)) * 2.0 + (n - 0.5) * 0.5;
		if (d > 1.0 || bayer(px + seed * 16.0) > density)
			discard;
		float light = (1.0 - at.y) * 0.55 + (1.0 - d) * 0.3 + n * 0.25;
		colour = light > 0.72 ? vec3(0.66) : light > 0.52 ? vec3(0.54) : light > 0.34 ? vec3(0.43) : vec3(0.33);
	}
	ALBEDO = pow(colour * tint, vec3(2.2)); // tg's colours are sRGB, ALBEDO is linear
}
