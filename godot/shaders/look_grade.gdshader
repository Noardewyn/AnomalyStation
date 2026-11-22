// D298: the look over the 3D view (under every HUD layer): the colour grade (saturation, contrast, split toning: cool shadows,
// warm lights), a film grain that moves every frame and a vignette.
shader_type canvas_item;

uniform sampler2D screen : hint_screen_texture, filter_nearest;
uniform float saturation = 1.0;
uniform float contrast = 1.0;
uniform float split = 0.0;
uniform float grain = 0.0;
uniform float vignette = 0.0;

float hash(vec2 p) { // a sin-free hash, as the screen effects' (ScreenEffectsOverlay)
    vec3 p3 = fract(vec3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return fract((p3.x + p3.y) * p3.z);
}

void fragment() {
    vec3 c = texture(screen, SCREEN_UV).rgb;
    float lum = dot(c, vec3(0.2126, 0.7152, 0.0722));
    c = mix(vec3(lum), c, saturation);
    c = (c - 0.5) * contrast + 0.5;
    c += split * (vec3(-0.03, 0.0, 0.05) * (1.0 - smoothstep(0.0, 0.5, lum)) + vec3(0.05, 0.02, -0.04) * smoothstep(0.5, 1.0, lum));
    // the grain: strongest in the middle tones, as film's
    float n = hash(FRAGCOORD.xy + fract(TIME * 7.13) * 917.0) - 0.5;
    c += n * grain * 0.12 * (0.4 + 0.6 * (1.0 - abs(lum * 2.0 - 1.0)));
    vec2 d = (SCREEN_UV - 0.5) * vec2(SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x, 1.0);
    c *= 1.0 - vignette * 0.75 * smoothstep(0.35, 0.95, length(d) * 1.2);
    COLOR = vec4(clamp(c, 0.0, 1.0), 1.0);
}
