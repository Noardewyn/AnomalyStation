// Gaps E2a: the precipitation of a running weather around the camera (tg's /particles/weather sets, and the rain, snow, ash and dust of
// its overlays): particles born in a box over the camera that fall (or blow) at the weather's speed and show only inside the weather's
// covered cells of its level, so no rain falls in the dry room behind a wall.
shader_type particles;

uniform sampler2D cells : filter_nearest, repeat_disable;
uniform vec4 box = vec4(1.0, 1.0, 1.0, 1.0);
uniform float tile_size = 2.0;
uniform float floor_y = 0.0;
uniform float level_height = 2.0;
uniform vec3 extent = vec3(9.0, 2.0, 9.0); // half the box the particles are born in, around the emitter
uniform vec3 fall = vec3(0.0, -9.0, 0.0);  // the velocity, metres per second
uniform float wobble = 0.0;                // the snow's and the ash's sideways drift
uniform vec4 tint : source_color = vec4(1.0);

float rand(uint seed) {
    seed = (seed ^ 61u) ^ (seed >> 16u);
    seed *= 9u;
    seed = seed ^ (seed >> 4u);
    seed *= 0x27d4eb2du;
    seed = seed ^ (seed >> 15u);
    return float(seed % 100000u) / 100000.0;
}

float covered(vec3 p) {
    vec2 tile = vec2(p.x / tile_size + 1.0, -p.z / tile_size + 1.0);
    vec2 uv = (tile - box.xy) / box.zw;
    if (uv.x < 0.0 || uv.y < 0.0 || uv.x > 1.0 || uv.y > 1.0 || p.y < floor_y || p.y > floor_y + level_height)
        return 0.0;
    return texture(cells, uv).r;
}

void start() {
    uint seed = NUMBER * 7919u + RANDOM_SEED;
    vec3 origin = EMISSION_TRANSFORM[3].xyz;
    vec3 at = origin + (vec3(rand(seed), rand(seed + 1u), rand(seed + 2u)) * 2.0 - 1.0) * extent;
    at.y = floor_y + level_height * (0.55 + 0.45 * rand(seed + 3u));
    TRANSFORM = mat4(vec4(1, 0, 0, 0), vec4(0, 1, 0, 0), vec4(0, 0, 1, 0), vec4(at, 1.0));
    VELOCITY = fall * (0.8 + 0.4 * rand(seed + 4u));
    CUSTOM.x = rand(seed + 5u) * 6.2831;
    COLOR = tint;
}

void process() {
    vec3 at = TRANSFORM[3].xyz;
    if (wobble > 0.0)
        VELOCITY.xz = fall.xz + vec2(sin(TIME * 1.7 + CUSTOM.x), cos(TIME * 1.3 + CUSTOM.x)) * wobble;
    if (at.y < floor_y)
        ACTIVE = false;
    COLOR = vec4(tint.rgb, tint.a * covered(at));
}
