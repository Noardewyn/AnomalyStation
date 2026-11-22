// BYOND's "rays" filter on a thing a 3D model draws (docs/ai/specs/models.md, "Filters"): the supermatter's rays (_sm_delam.dm:115, their size its energy, their
// colour yellow or red, their factor and density its damage, their offset turning back and forth over 10 s). A quad facing the eye at the model's middle, its
// pixels the 2D client's rays (AppearanceCompositor.Rays): ray cells round the circle, each one's strength a hash of the cell and the whole offset eased to the
// next, cut by the threshold, its length the size times (1 - factor + factor * strength), its alpha falling to its tip. Blended over the room as BYOND
// blends them, drawn under the crystal (its depth hides the half behind it), brighter than white for the environment's glow.
shader_type spatial;
render_mode blend_mix, unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled;

uniform vec4 colour : source_color = vec4(1.0, 0.82, 0.31, 1.0); // the filter's colour and alpha
uniform float size = 1.0; // pixels
uniform float density = 12.0;
uniform float factor = 1.0;
uniform float threshold = 0.0;
uniform float offset = 0.0; // the filter's offset now (its animate() chain played)
uniform float metres_per_pixel = 0.0625; // a 32-pixel icon over the 2 m tile
uniform float brightness = 2.2;

varying vec2 v_px;

void vertex() {
	// a unit quad (-0.5 .. 0.5) facing the eye, as wide as the rays reach
	v_px = VERTEX.xy * 2.0 * size;
	MODELVIEW_MATRIX = VIEW_MATRIX * mat4(INV_VIEW_MATRIX[0], INV_VIEW_MATRIX[1], INV_VIEW_MATRIX[2], MODEL_MATRIX[3]);
	VERTEX.xy *= 2.0 * size * metres_per_pixel;
}

// The 2D client's hash (AppearanceCompositor.Hash): 0..1 from a ray cell and a whole time.
float ray_hash(int cell, int time) {
	uint h = uint(cell) * 374761393u + uint(time % 1000) * 668265263u;
	h = (h ^ (h >> 13u)) * 1274126177u;
	h ^= h >> 16u;
	return float(h & 0xFFFFFFu) / float(0xFFFFFF);
}

void fragment() {
	float d = length(v_px);
	if (size <= 0.0 || d >= size) {
		discard;
	}
	int cells = max(1, int(density));
	float time = mod(mod(offset, 1000.0) + 1000.0, 1000.0);
	int t0 = int(floor(time));
	float ease = time - float(t0);
	ease = ease * ease * (3.0 - 2.0 * ease);
	float turn = fract(atan(v_px.y, v_px.x) / TAU + 1.0) * float(cells);
	int cell = int(floor(turn)) % cells;
	float across = turn - floor(turn);
	float strength = mix(ray_hash(cell, t0), ray_hash(cell, t0 + 1), ease) * pow(sin(PI * across), 2.0);
	float cut = clamp(threshold, 0.0, 0.999);
	strength = (strength - cut) / (1.0 - cut);
	float f = clamp(factor, 0.0, 1.0);
	float len = size * (1.0 - f + f * strength);
	if (strength <= 0.0 || d >= len) {
		discard;
	}
	ALBEDO = colour.rgb * brightness;
	ALPHA = colour.a * strength * (1.0 - d / len);
}
