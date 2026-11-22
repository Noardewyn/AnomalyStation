// D298: dark outlines at the edges of the geometry, from the depth and the normals of the opaque pass: a quad over the whole
// view (its vertex shader puts it at the near plane), drawn first among the transparent things so the sprites and the held
// items drawn after it cover its lines. Creases and silhouettes show by the depth's second difference (a floor seen at a
// grazing angle shows none), corners by the normals; the lines fade out with the distance.
shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, depth_test_disabled, blend_mix, fog_disabled, shadows_disabled;

uniform sampler2D depth_tex : hint_depth_texture, filter_nearest;
uniform sampler2D normal_tex : hint_normal_roughness_texture, filter_nearest;
uniform float width = 1.0;
uniform float strength = 0.6;
uniform float fade = 30.0;

void vertex() {
    POSITION = vec4(VERTEX.xy, 1.0, 1.0);
}

float linear_depth(vec2 uv, mat4 inv_projection) {
    vec4 view = inv_projection * vec4(uv * 2.0 - 1.0, texture(depth_tex, uv).r, 1.0);
    return -view.z / view.w;
}

vec3 normal_at(vec2 uv) {
    return texture(normal_tex, uv).xyz * 2.0 - 1.0;
}

void fragment() {
    vec2 px = width * max(1.0, VIEWPORT_SIZE.y / 1080.0) / VIEWPORT_SIZE;
    vec2 uv = SCREEN_UV;
    float c = linear_depth(uv, INV_PROJECTION_MATRIX);
    float l = linear_depth(uv - vec2(px.x, 0.0), INV_PROJECTION_MATRIX);
    float r = linear_depth(uv + vec2(px.x, 0.0), INV_PROJECTION_MATRIX);
    float d = linear_depth(uv - vec2(0.0, px.y), INV_PROJECTION_MATRIX);
    float u = linear_depth(uv + vec2(0.0, px.y), INV_PROJECTION_MATRIX);
    float crease = (abs(l + r - 2.0 * c) + abs(u + d - 2.0 * c)) / max(c, 0.001);
    float edge = smoothstep(0.03, 0.09, crease);
    vec3 n = normal_at(uv);
    float bend = 1.0 - min(min(dot(n, normal_at(uv - vec2(px.x, 0.0))), dot(n, normal_at(uv + vec2(px.x, 0.0)))),
        min(dot(n, normal_at(uv - vec2(0.0, px.y))), dot(n, normal_at(uv + vec2(0.0, px.y)))));
    edge = max(edge, smoothstep(0.25, 0.6, bend));
    ALBEDO = vec3(0.0);
    ALPHA = edge * strength * (1.0 - smoothstep(fade * 0.5, fade, c));
}
