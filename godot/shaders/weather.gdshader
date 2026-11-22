// Gaps E2a: a level's running weather (tg's area overlays from weather_effects.dmi and glow_weather.dmi) as a haze in the covered cells:
// a quad over the whole view (as outline.gdshader) that marches each pixel's view ray from the camera to the opaque surface behind it
// through the weather's cell texture, on its level only, and gathers a drifting noise there. Seen from inside, the storm fills the room;
// seen from outside, it hangs in the covered rooms and corridors. The lava weather lies on the floor only.
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, depth_test_disabled, blend_mix, fog_disabled, shadows_disabled;

uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
uniform sampler2D cells : filter_linear, repeat_disable;
uniform vec4 box = vec4(1.0, 1.0, 1.0, 1.0); // the cell texture's first column and row, its width and height (BYOND tiles)
uniform float tile_size = 2.0;
uniform float floor_y = 0.0;
uniform float level_height = 2.0;
uniform float max_distance = 48.0;
uniform vec4 colour : source_color = vec4(1.0);
uniform float density = 0.25; // per metre in a fully covered cell
uniform float wind = 0.0;      // -1 west, 1 east
uniform float drift = 1.0;     // how fast the noise moves
uniform float grain = 0.6;     // the noise's scale (per metre)
uniform float floor_only = 0.0; // 1: the lava's sheet on the floor
uniform float glow = 0.0;      // tg's glowing overlay: the haze lights itself

const int STEPS = 28;

float hash(vec3 p) {
    p = fract(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return fract((p.x + p.y) * p.z);
}
float noise(vec3 p) {
    vec3 i = floor(p), f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(mix(mix(hash(i), hash(i + vec3(1, 0, 0)), f.x), mix(hash(i + vec3(0, 1, 0)), hash(i + vec3(1, 1, 0)), f.x), f.y),
               mix(mix(hash(i + vec3(0, 0, 1)), hash(i + vec3(1, 0, 1)), f.x), mix(hash(i + vec3(0, 1, 1)), hash(i + vec3(1, 1, 1)), f.x), f.y), f.z);
}
float fbm(vec3 p) {
    float v = 0.0, a = 0.5;
    for (int i = 0; i < 3; i++) { v += a * noise(p); p *= 2.03; a *= 0.5; }
    return v;
}

float covered(vec3 p) {
    vec2 tile = vec2(p.x / tile_size + 1.0, -p.z / tile_size + 1.0); // tile X spans [X, X + 1)
    vec2 uv = (tile - box.xy) / box.zw;
    if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0)
        return 0.0;
    return texture(cells, uv).r;
}

void vertex() {
    POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

void fragment() {
    float depth = texture(depth_tex, SCREEN_UV).r;
    vec4 view = INV_PROJECTION_MATRIX * vec4(SCREEN_UV * 2.0 - 1.0, depth, 1.0);
    view.xyz /= view.w;
    vec3 surface = (INV_VIEW_MATRIX * vec4(view.xyz, 1.0)).xyz;
    vec3 eye = CAMERA_POSITION_WORLD;
    vec3 ray = surface - eye;
    float length_to = min(length(ray), max_distance);
    ray = normalize(ray);
    float top = floor_y + mix(level_height, 0.35, floor_only);
    float step_len = length_to / float(STEPS);
    float jitter = hash(vec3(FRAGCOORD.xy, TIME * 60.0));
    vec3 flow = vec3(wind * 1.5, -0.4, 0.25) * TIME * drift;
    float gathered = 0.0;
    for (int i = 0; i < STEPS; i++) {
        vec3 p = eye + ray * (step_len * (float(i) + jitter));
        if (p.y < floor_y || p.y > top)
            continue;
        float c = covered(p);
        if (c <= 0.001)
            continue;
        gathered += c * (0.35 + 0.9 * fbm(p * grain + flow)) * step_len;
    }
    float alpha = 1.0 - exp(-gathered * density);
    ALBEDO = colour.rgb * (1.0 + glow * 0.6);
    ALPHA = clamp(alpha * colour.a, 0.0, 0.75); // the far end of a long stormy corridor still shows
}
