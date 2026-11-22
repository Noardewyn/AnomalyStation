shader_type spatial;
render_mode cull_back;

uniform sampler2D atlas : source_color, filter_nearest, repeat_disable;
// Pixel rectangle of a 32x32 state frame in the ORIGINAL atlas.
uniform vec4 atlas_rect = vec4(0.0, 0.0, 32.0, 32.0);
uniform float emission_strength : hint_range(0.0, 4.0) = 0.5;

void fragment() {
    vec2 size = vec2(textureSize(atlas, 0));
    vec2 pixel = atlas_rect.xy + clamp(UV2 * atlas_rect.zw, vec2(0.5), atlas_rect.zw - vec2(0.5));
    vec4 texel = texture(atlas, pixel / size);
    ALBEDO = texel.rgb;
    ALPHA = texel.a;
    ALPHA_SCISSOR_THRESHOLD = 0.1;
    EMISSION = texel.rgb * emission_strength;
    ROUGHNESS = 0.72;
}
