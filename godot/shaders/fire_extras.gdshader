// A very hot fire's extras (Render/FireVolume.cs, D334, DV433): tg's update_color adds three overlays over the hotspot, all
// added (BLEND_ADD) and coloured by the hotspot's colour matrix, which at these heats turns a pixel into its brightness times
// the heat's colour (the hotspot's light colour, tint): effects.dmi "shieldsparkles" (cyan pluses and dots, 6 frames at 10 a
// second, alpha the sparkle amount), fire.dmi "overcharged" (white branching bolts in bursts: 9 frames of 1, 1, 50, 1, 1, 1,
// 20, 1 and 40 ticks, the long ones empty) and atmospherics.dmi "fusion_gas" (white dashes, 6 frames, alpha the fusion amount).
// Each is drawn as its pixels in the tile's air: one quad per sparkle, dash or bolt segment, placed by the vertex shader at
// random points of the tile that change with the sprite's frame, camera-facing and a tg pixel (1/16 m) thick.
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, blend_add, shadows_disabled, fog_disabled;

const float PIXEL = 1.0 / 16.0; // tg's 32 pixels on a 2 m tile
const float SPARKLE_LUMA = 0.76; // the brightness of shieldsparkles' cyan (#7FD8FF-ish) under the greyscale matrix
const int BOLT_SEGMENTS = 10;

uniform vec3 half_air = vec3(1.0, 1.0, 1.0); // half the tile's air; the node is its middle

instance uniform vec3 tint = vec3(1.0);
instance uniform float sparkle = 0.0;
instance uniform float lightning = 0.0;
instance uniform float fusion = 0.0;

varying flat float brightness;
varying flat float plus; // a sparkle drawn as tg's five-pixel plus (else a filled quad)

float hash(vec3 p) {
	return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453);
}

vec3 spot(vec3 seed) {
	return (vec3(hash(seed), hash(seed + 17.0), hash(seed + 41.0)) * 2.0 - 1.0) * (half_air - PIXEL * 3.0);
}

// overcharged's frame now, or -1 while it shows nothing (its delays in ticks: 1 1 50 1 1 1 20 1 40, frames 2, 6 and 8 empty)
float bolt_frame(float tick) {
	float t = mod(tick, 116.0);
	return t < 1.0 ? 0.0 : t < 2.0 ? 1.0 : t < 52.0 ? -1.0 : t < 53.0 ? 3.0 : t < 54.0 ? 4.0 : t < 55.0 ? 5.0 : t < 75.0 ? -1.0 : t < 76.0 ? 7.0 : -1.0;
}

void vertex() {
	vec3 tile = MODEL_MATRIX[3].xyz;
	float kind = floor(COLOR.g * 255.0 + 0.5); // 0 sparkle, 1 dash, 2 bolt segment
	float number = floor(COLOR.r * 255.0 + 0.5);
	float tick = floor(TIME * 10.0); // tg's ticks, one a frame for the sparkles and the dashes
	vec3 right = INV_VIEW_MATRIX[0].xyz, up = INV_VIEW_MATRIX[1].xyz;
	vec3 a = vec3(0.0), b = vec3(0.0);
	float width = 0.0;
	brightness = 0.0;
	plus = 0.0;
	if (kind < 0.5) {
		float frame = mod(tick, 6.0);
		vec3 at = spot(tile + vec3(number, frame, 3.0));
		plus = hash(tile + vec3(number, 0.0, 9.0)) < 0.5 ? 1.0 : 0.0;
		float size = (plus > 0.5 ? 5.0 : 1.0) * PIXEL;
		a = at - right * size * 0.5;
		b = at + right * size * 0.5;
		width = size;
		brightness = sparkle * SPARKLE_LUMA;
	} else if (kind < 1.5) {
		float frame = mod(tick, 6.0);
		vec3 at = spot(tile + vec3(number, frame, 5.0));
		vec3 flat_right = normalize(vec3(right.x, 0.0, right.z) + vec3(1e-4, 0.0, 0.0)); // tg's dashes lie level
		float size = (hash(tile + vec3(number, frame, 6.0)) < 0.5 ? 2.0 : 3.0) * PIXEL;
		a = at - flat_right * size * 0.5;
		b = at + flat_right * size * 0.5;
		width = PIXEL;
		brightness = fusion;
	} else {
		float frame = bolt_frame(tick);
		if (frame >= 0.0 && lightning > 0.5) {
			// a jagged walk from a random point of the tile: this quad is one of its segments
			float bolt = floor(COLOR.b * 255.0 + 0.5);
			float segment = mod(number, float(BOLT_SEGMENTS));
			vec3 seed = tile + vec3(bolt, frame, 11.0);
			vec3 p = spot(seed);
			vec3 heading = normalize(vec3(hash(seed + 1.0), hash(seed + 2.0), hash(seed + 3.0)) * 2.0 - 1.0);
			vec3 next = p;
			for (int i = 0; i <= BOLT_SEGMENTS; i++) {
				vec3 kink = vec3(hash(seed + float(i) * 7.0), hash(seed + float(i) * 7.0 + 0.5), hash(seed + float(i) * 7.0 + 0.25)) * 2.0 - 1.0;
				next = clamp(p + normalize(heading + kink * 0.9) * 0.22, -half_air, half_air);
				if (float(i) >= segment)
					break;
				p = next;
			}
			a = p;
			b = next;
			width = PIXEL;
			brightness = 0.9;
		}
	}
	// the quad from a to b, a pixel wide, turned to the eye
	vec3 eye = INV_VIEW_MATRIX[3].xyz - tile; // the node is only moved, never turned or scaled
	vec3 along = b - a;
	vec3 across = normalize(cross(along + vec3(1e-5), eye - (a + b) * 0.5)) * width * 0.5;
	if (kind < 0.5)
		across = up * width * 0.5;
	VERTEX = mix(a, b, UV.x) + across * (UV.y * 2.0 - 1.0);
	if (brightness <= 0.0)
		VERTEX = vec3(0.0);
}

void fragment() {
	if (plus > 0.5) {
		vec2 px = floor(UV * 5.0);
		if (px.x != 2.0 && px.y != 2.0)
			discard;
	}
	ALBEDO = pow(clamp(tint * brightness, 0.0, 1.0), vec3(2.2)); // tg's colours are sRGB, ALBEDO is linear
}
