// tg's explosion (/obj/effect/explosion, Render/BlastVolume.cs, D303): a burst of round camera-facing puffs on tg's pixel grid, thrown out to the 3 tiles of tg's 96-pixel
// sprite, burning down from pale yellow through orange and red to dark smoke as they eat themselves away. No pixel is see-through.
shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled, skip_vertex_transform;

const float PIXELS_PER_METRE = 16.0; // tg's 32 pixels on a 2 m tile
const float NEAR_HIDE = 0.5; // metres: a puff this near the eye would fill the view with flat colour

instance uniform float life = 0.0; // 0 at the blast, 1 at its end
instance uniform float blast_scale = 1.0; // the blast's tg transform: far ones of a big explosion are smaller
instance uniform float lift = 1.0; // metres over the floor of its middle: a storey's middle, or under the ceiling for a blast at the top (DV578)

varying flat float seed;
varying flat float size;

float hash(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
	vec2 i = floor(p), f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x), f.y);
}

void vertex() {
	vec3 tile = MODEL_MATRIX[3].xyz;
	seed = hash(tile.xz + COLOR.r * 113.0);
	float turn = hash(tile.xz) * 6.2832; // each blast of a big explosion throws its puffs its own way
	vec3 thrown = vec3(VERTEX.x * cos(turn) - VERTEX.z * sin(turn), VERTEX.y, VERTEX.x * sin(turn) + VERTEX.z * cos(turn)) * blast_scale;
	float push = 1.0 - (1.0 - life) * (1.0 - life);
	size = COLOR.g * 3.0 * (0.45 + 0.75 * push) * blast_scale;
	vec3 centre = tile + vec3(0.0, lift, 0.0) + thrown * vec3(1.0, 0.4, 1.0) * push; // flattened: a room's ceiling would cut a round ball
	float shown = distance(centre, INV_VIEW_MATRIX[3].xyz) > NEAR_HIDE ? size : 0.0;
	vec3 world = centre + (INV_VIEW_MATRIX[0].xyz * (UV.x - 0.5) + INV_VIEW_MATRIX[1].xyz * (UV.y - 0.5)) * shown;
	VERTEX = (VIEW_MATRIX * vec4(world, 1.0)).xyz;
}

void fragment() {
	float pixels = max(floor(size * PIXELS_PER_METRE), 1.0);
	vec2 px = floor(UV * pixels);
	vec2 at = (px + 0.5) / pixels;
	float n = noise(px / 3.0 + seed * 91.0);
	float d = distance(at, vec2(0.5)) * 2.0 + (n - 0.5) * 0.6;
	float gone = life * life * 1.1; // the puff eats itself away from its noise
	if (d > 1.0 || n < gone - 0.1)
		discard;
	float heat = (1.0 - d) * 0.6 + n * 0.4 + 0.75 - life * 1.35 - seed * 0.15;
	vec3 colour = heat > 0.85 ? vec3(1.0, 0.95, 0.68)
		: heat > 0.65 ? vec3(1.0, 0.8, 0.22)
		: heat > 0.42 ? vec3(0.98, 0.5, 0.1)
		: heat > 0.2 ? vec3(0.78, 0.22, 0.07)
		: heat > 0.0 ? vec3(0.38, 0.17, 0.12)
		: vec3(0.2, 0.19, 0.19);
	ALBEDO = pow(colour, vec3(2.2)); // tg's colours are sRGB, ALBEDO is linear
}
