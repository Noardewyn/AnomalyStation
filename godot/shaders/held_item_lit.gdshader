// D285: a thing in the own hands in first person (Hud/ViewModel): tg's picture made solid (SpriteExtrusion), its pixels sharp and its faint and
// empty pixels cut (held_item_glow.gdshader draws the faint ones), lit by the light grid where the eye is (docs/ai/specs/light-grid.md section 7)
// with a fixed key direction, so its edges read as the solid's sides. It is drawn with its own field of view (the player's does not stretch it)
// and in front of the world (its depth squeezed next to the near plane, its own faces still in order), so it never sinks into a wall.
shader_type spatial;
render_mode cull_disabled, diffuse_burley, specular_disabled, shadows_disabled;

#include "res://shaders/light_grid.gdshaderinc"
#include "res://shaders/world_light.gdshaderinc"
#include "res://shaders/held_item_view.gdshaderinc"

uniform sampler2D picture : source_color, filter_nearest, repeat_disable;
uniform float facet_contrast = 0.35;
// D288 (the owner, 2026-10-29): a light that is on in the hand (tg's "-on" look: a flashlight, a desk lamp) glows, its bright pixels most
uniform float lit = 0.0;

varying vec3 v_world;
varying vec3 v_normal;

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	v_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
	POSITION = held_view_position(PROJECTION_MATRIX, MODELVIEW_MATRIX * vec4(VERTEX, 1.0), 0.0);
}

void fragment() {
	vec4 texel = texture(picture, UV);
	ALBEDO = texel.rgb;
	ALPHA = texel.a;
	ALPHA_SCISSOR_THRESHOLD = 0.5;
	ROUGHNESS = 0.95;
	vec3 key = normalize(vec3(-0.45, 0.8, 0.35));
	float facet = 1.0 - facet_contrast + facet_contrast * clamp(dot(normalize(v_normal), key) * 0.5 + 0.5, 0.0, 1.0);
	EMISSION = texel.rgb * anomaly_grid_light(v_world) * facet;
	EMISSION += texel.rgb * anomaly_sky_light(v_world, normalize(v_normal));
	float bright = smoothstep(0.5, 0.85, dot(texel.rgb, vec3(0.299, 0.587, 0.114)));
	EMISSION += texel.rgb * lit * (0.35 + 2.0 * bright);
	// held right before the eye, it takes no shadow: the sun only where the sky over it is open
	anomaly_sun_share = anomaly_open_sky(v_world, vec3(0.0));
}
