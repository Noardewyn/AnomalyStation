// The flat stand-in shapes of EntityLayer (a capsule for a mob, a box for a thing whose picture is not there): a colour, lit by the light grid like every other world
// surface (docs/ai/specs/light-grid.md section 7), so that a stand-in in a lit room is not black. M3 I1c.
shader_type spatial;
render_mode diffuse_burley, specular_disabled;

#include "res://shaders/light_grid.gdshaderinc"
#include "res://shaders/world_light.gdshaderinc"

uniform vec4 color : source_color = vec4(0.62, 0.55, 0.42, 1.0);

varying vec3 v_world;

void vertex() {
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	vec3 shown = anomaly_previewed(color.rgb, v_world, 0.0); // the editor's preview
	ALBEDO = shown;
	ROUGHNESS = 0.8;
	EMISSION = shown * anomaly_grid_light(v_world);
	vec3 wn = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	EMISSION += shown * anomaly_sky_light(v_world, wn);
	anomaly_sun_share = anomaly_sun_share_at(v_world, wn, length(VERTEX));
}
