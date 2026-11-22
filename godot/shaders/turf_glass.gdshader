// The turf kit's shader (M3 P6, docs/ai/specs/apps.md 2.9): floors, ceilings and walls of a chunk in one MultiMesh. tg's pictures are
// the layers of a Texture2DArray; an instance carries its layer and which part of the shared tile mesh it draws (the vertices of
// the other parts are moved out of the view). Lit by the light grid (docs/ai/specs/light-grid.md 7) plus Godot's own shadowed lights.
shader_type spatial;
render_mode cull_disabled, diffuse_burley;

#include "res://shaders/light_grid.gdshaderinc"
#include "res://shaders/world_light.gdshaderinc"

uniform sampler2DArray pictures : source_color, filter_nearest_mipmap_anisotropic, repeat_disable;
uniform float ceiling_dim = 0.55;
uniform bool ceilings = true;

varying flat float v_layer;
varying flat float v_part;
varying flat float v_condition;
varying vec3 v_world;

void vertex() {
	// INSTANCE_CUSTOM: x = layer in the array, y = part (0 floor, 1 ceiling, 2 wall), z = the layer of a wall's SIDE picture plus one (0: none). M3 R3 (D121): the side faces of
	// a wall (UV2.y = 1) show its type's one straight east-west picture whatever junction its own picture (the top face's) has.
	float want = floor(INSTANCE_CUSTOM.y + 0.5);
	float part = floor(UV2.x + 0.5);
	float kind = want > 3.5 ? want - 4.0 : want; // DV397: a ladder hatch's floor (4) and ceiling (5)
	v_layer = floor(INSTANCE_CUSTOM.x + 0.5);
	if (want > 1.5 && floor(UV2.y + 0.5) > 0.5 && INSTANCE_CUSTOM.z > 0.5) {
		v_layer = floor(INSTANCE_CUSTOM.z + 0.5) - 1.0;
	}
	v_part = kind;
	v_condition = INSTANCE_CUSTOM.w;
	if (part != want || (kind == 1.0 && !ceilings)) {
		// The other parts of the shared mesh collapse to a point: degenerate triangles are never drawn.
		VERTEX = vec3(0.0);
	}
	v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	// One whole sprite per face: uv 0..1, clamped (no wrap, so no mipmap bleed from the opposite edge).
	// Mesa's Intel driver makes a nearest filter with anisotropy a linear one: a texel's centre reads the same under both, and the
	// unsnapped uv's gradients keep the mipmap and anisotropy choice.
	vec2 uv = clamp(UV, vec2(0.0), vec2(1.0));
	vec2 texels = vec2(textureSize(pictures, 0).xy);
	vec2 centre = (min(floor(uv * texels), texels - 1.0) + 0.5) / texels;
	vec4 c = textureGrad(pictures, vec3(centre, v_layer), dFdx(uv), dFdy(uv));
	// tg's glass pictures run from faint to nearly solid: kept within a band, so a floor of glass is always there and always seen through
	ALPHA = c.a < 0.02 ? 0.0 : clamp(c.a, 0.3, 0.75);
	vec3 albedo = c.rgb;
	if (v_part > 0.5 && v_part < 1.5) {
        if (v_condition < 0.0 && (UV.x < 0.04 || UV.y < 0.04 || UV.x > 0.96 || UV.y > 0.96)) { albedo = vec3(0.9, 0.4, 0.05); }
		albedo *= ceiling_dim;
	}
	albedo = anomaly_previewed(albedo, v_world, 1.0); // the editor's preview of a turf
	ALBEDO = albedo;
	ROUGHNESS = 0.2;
	// The light grid lights what the surface shows; the sample sits just off the surface so a wall face reads its own tile's corners.
	vec3 wn = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	vec3 p = v_world + vec3(0.0, wn.y * 0.01, 0.0); // D325: each side of a level's boundary reads its own level (turf_kit.gdshader)
	EMISSION = albedo * anomaly_grid_light(p);
	EMISSION += albedo * anomaly_sky_light(p, wn);
	anomaly_sun_share = anomaly_sun_share_at(p, wn, length(VERTEX));
	// the windows' sheen toward a grazing view (prop_atlas.gdshaderinc), on the face turned to the eye
	float sheen = FRONT_FACING ? pow(1.0 - clamp(abs(dot(NORMAL, VIEW)), 0.0, 1.0), 4.0) : 0.0;
	EMISSION += (anomaly_grid_light(p) + anomaly_sky_light(p, wn)) * vec3(0.6, 0.72, 0.9) * sheen * 0.12;
	ALPHA = min(ALPHA + sheen * 0.08, 1.0);
}
