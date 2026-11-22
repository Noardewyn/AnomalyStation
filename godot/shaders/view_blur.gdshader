// Protocol version 29 (the eye blur): tg's gaussian blur filter on the game plane, drawn over the 3D view and under the HUD
// (Render/ViewBlurLayer.cs). A 7 x 7 gaussian of standard deviation sigma (screen pixels) over the screen texture: the taps are a
// sigma apart and read from the mip level that pre-averages that spacing, so a wide blur stays smooth at 49 reads a pixel.
// Gaps RG3 (the drugs' trips): the plane's waves push the point a pixel reads from (BYOND "wave", Shared/Play/ViewBlur.WavePush), its
// radial blur smears outward from the centre (BYOND "radial_blur", 9 taps along the radius, each at the gaussian's mip level when
// both are on), and its colour filters run as steps, each in its colour space (RGB, HSV, HSL or HCY: ViewBlur.ApplySteps).
shader_type canvas_item;

uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
uniform float sigma = 0.0;
// protocol version 31: the game plane's colour matrices (tg's client colours: the mesons' tint), out = r * row_r + g * row_g + b * row_b
// + constant; gaps RG3: as steps (ViewBlur.ColorSteps), each in its colour space: step i's rows are color_rows[4i .. 4i + 2], its
// constant color_rows[4i + 3]
uniform int color_steps = 0;
uniform vec4 color_rows[16];
uniform int color_space[4];
// gaps RG3: the waves (ViewBlur.Waves): wave_a = (direction x, direction y, period, amplitude), wave_b = (phase, sideways, 0, 0)
uniform int wave_count = 0;
uniform vec4 wave_a[2];
uniform vec4 wave_b[2];
// gaps RG3: the radial blur (ViewBlur.Radial): size, centre's offset from the view's centre (x right, y up), the radius left sharp
uniform float radial_size = 0.0;
uniform vec2 radial_centre = vec2(0.0);
uniform float radial_offset = 0.0;

const float TAU_ = 6.28318530718;

float hue_of(vec3 c, float mx, float chroma) {
	if (chroma <= 0.0) return 0.0;
	float h;
	if (mx == c.r) h = mod((c.g - c.b) / chroma, 6.0);
	else if (mx == c.g) h = (c.b - c.r) / chroma + 2.0;
	else h = (c.r - c.g) / chroma + 4.0;
	return fract(h / 6.0 + 1.0);
}

vec3 to_space(vec3 c, int space) {
	float mx = max(c.r, max(c.g, c.b));
	float mn = min(c.r, min(c.g, c.b));
	float chroma = mx - mn;
	float h = hue_of(c, mx, chroma);
	if (space == 1) return vec3(h, mx <= 0.0 ? 0.0 : chroma / mx, mx);
	if (space == 2) {
		float l = (mx + mn) / 2.0;
		return vec3(h, chroma <= 0.0 ? 0.0 : chroma / (1.0 - abs(2.0 * l - 1.0)), l);
	}
	return vec3(h, chroma, dot(c, vec3(0.299, 0.587, 0.114)));
}

vec3 hue_rgb(float h, float chroma) {
	float hh = h * 6.0;
	float x = chroma * (1.0 - abs(mod(hh, 2.0) - 1.0));
	int i = int(hh);
	if (i == 0) return vec3(chroma, x, 0.0);
	if (i == 1) return vec3(x, chroma, 0.0);
	if (i == 2) return vec3(0.0, chroma, x);
	if (i == 3) return vec3(0.0, x, chroma);
	if (i == 4) return vec3(x, 0.0, chroma);
	return vec3(chroma, 0.0, x);
}

vec3 from_space(vec3 v, int space) {
	float h = fract(fract(v.x) + 1.0);
	float p = clamp(v.y, 0.0, 1.0);
	float q = clamp(v.z, 0.0, 1.0);
	if (space == 3) {
		vec3 c = hue_rgb(h, p);
		return clamp(c + (q - dot(c, vec3(0.299, 0.587, 0.114))), 0.0, 1.0);
	}
	float chroma;
	float m;
	if (space == 1) {
		chroma = q * p;
		m = q - chroma;
	} else {
		chroma = (1.0 - abs(2.0 * q - 1.0)) * p;
		m = q - chroma / 2.0;
	}
	return clamp(hue_rgb(h, chroma) + m, 0.0, 1.0);
}

void fragment() {
	vec2 uv = SCREEN_UV;
	vec2 view_px = 1.0 / SCREEN_PIXEL_SIZE;
	if (wave_count > 0) {
		// the point x right, y up from the view's centre, in screen pixels
		vec2 v = vec2(uv.x * view_px.x - view_px.x * 0.5, view_px.y * 0.5 - uv.y * view_px.y);
		vec2 push = vec2(0.0);
		for (int i = 0; i < 2; i++) {
			if (i >= wave_count) break;
			vec2 dir = wave_a[i].xy;
			float along = dot(v, dir) / wave_a[i].z;
			float amount = wave_a[i].w * sin(TAU_ * (along - wave_b[i].x));
			push += (wave_b[i].y > 0.5 ? vec2(-dir.y, dir.x) : dir) * amount;
		}
		uv += vec2(push.x, -push.y) * SCREEN_PIXEL_SIZE;
	}
	float lod = sigma > 0.0 ? max(log2(sigma) - 0.5, 0.0) : 0.0;
	if (radial_size > 0.0) {
		vec2 centre = view_px * 0.5 + vec2(radial_centre.x, -radial_centre.y);
		vec2 v = uv * view_px - centre;
		float d = length(v);
		float reach = max(0.0, d - radial_offset);
		float s = radial_size * reach / 2.0;
		if (d < 0.001 || s < 0.05) {
			COLOR = textureLod(screen_tex, uv, lod);
		} else {
			vec2 dir = v / d * SCREEN_PIXEL_SIZE;
			vec4 sum = vec4(0.0);
			float weights = 0.0;
			for (int k = -4; k <= 4; k++) {
				float t = float(k) * (s * 3.0 / 4.0);
				float w = exp(-t * t / (2.0 * s * s));
				sum += textureLod(screen_tex, uv + dir * t, lod) * w;
				weights += w;
			}
			COLOR = vec4((sum / weights).rgb, 1.0);
		}
	} else if (sigma <= 0.0) {
		COLOR = texture(screen_tex, uv);
	} else {
		vec2 px = SCREEN_PIXEL_SIZE * sigma;
		vec4 sum = vec4(0.0);
		float weights = 0.0;
		for (int y = -3; y <= 3; y++) {
			for (int x = -3; x <= 3; x++) {
				// taps one sigma apart: the weight of a gaussian of deviation 1 tap
				float w = exp(-0.5 * float(x * x + y * y));
				sum += textureLod(screen_tex, uv + vec2(float(x), float(y)) * px, lod) * w;
				weights += w;
			}
		}
		COLOR = vec4((sum / weights).rgb, 1.0);
	}
	for (int i = 0; i < 4; i++) {
		if (i >= color_steps) break;
		int space = color_space[i];
		vec3 c = space == 0 ? COLOR.rgb : to_space(COLOR.rgb, space);
		vec3 o = c.x * color_rows[i * 4].xyz + c.y * color_rows[i * 4 + 1].xyz + c.z * color_rows[i * 4 + 2].xyz + color_rows[i * 4 + 3].xyz;
		COLOR = vec4(space == 0 ? clamp(o, 0.0, 1.0) : from_space(o, space), 1.0);
	}
}
