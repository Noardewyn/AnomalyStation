// DV247 (gaps F1): tg's foam and foamed metal (effects_foam.dm) as a textured volume on their tile: each face shows the entity's composed
// picture (tg's icon state, its flicked dissolve frames, its colour and alpha), lit by the light grid like every other world surface.
#include "res://shaders/light_grid.gdshaderinc"
#define ANOMALY_PICTURE_LIGHT
#include "res://shaders/world_light.gdshaderinc"

uniform sampler2D picture : source_color, filter_nearest, repeat_disable;
uniform float scissor = 0.5;

varying vec3 v_world;

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec4 c = texture(picture, UV);
	ALBEDO = c.rgb;
	ALPHA = c.a;
#ifndef FOAM_TRANSLUCENT
	ALPHA_SCISSOR_THRESHOLD = scissor;
#endif
	ROUGHNESS = 1.0;
	EMISSION = c.rgb * anomaly_grid_light(v_world + vec3(0.0, 0.3, 0.0));
	if (!FRONT_FACING) {
		NORMAL = -NORMAL;
	}
	vec3 wn = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	EMISSION += ALBEDO * anomaly_sky_light(v_world, wn);
	anomaly_sun_share = anomaly_sun_share_at(v_world, wn, length(VERTEX));
}
