// The turf kit's shader (M3 P6, docs/ai/specs/apps.md 2.9): floors, ceilings and walls of a chunk in one MultiMesh. tg's pictures are
// the layers of a Texture2DArray; an instance carries its layer and which part of the shared tile mesh it draws (the vertices of
// the other parts are moved out of the view). Lit by the light grid (docs/ai/specs/light-grid.md 7) plus Godot's own shadowed lights.
shader_type spatial;
render_mode cull_back, diffuse_burley;

#include "res://shaders/light_grid.gdshaderinc"
#include "res://shaders/world_light.gdshaderinc"

uniform sampler2DArray pictures : source_color, filter_nearest_mipmap_anisotropic, repeat_disable;
uniform float ceiling_dim = 0.55;
uniform bool ceilings = true;
uniform float tile_glow = 1.6; // a glowing tile's own light over its picture (as a lit model piece's, prop_atlas): over 1 it blooms

varying flat float v_layer;
varying flat float v_part;
varying flat float v_condition;
varying vec3 v_world;

void vertex() {
	// INSTANCE_CUSTOM: x = layer in the array, y = part (0 floor, 1 ceiling, 2 wall), z = the layer of a wall's SIDE picture plus one (0: none). M3 R3 (D121): the side faces of
	// a wall (UV2.y = 1) show its type's one straight east-west picture whatever junction its own picture (the top face's) has.
	float want = floor(INSTANCE_CUSTOM.y + 0.5);
	float part = floor(UV2.x + 0.5);
	// DV397: a ladder hatch's floor (4) and ceiling (5) are drawn as the floor (0) and the ceiling (1)
	float kind = want > 3.5 ? want - 4.0 : want;
	v_layer = floor(INSTANCE_CUSTOM.x + 0.5);
	if (kind > 1.5 && floor(UV2.y + 0.5) > 0.5 && INSTANCE_CUSTOM.z > 0.5) { // walls (2) and diagonal corner walls (3)
		v_layer = floor(INSTANCE_CUSTOM.z + 0.5) - 1.0;
	}
	// A wall box's own picture of each side (the instance colour: south, east, north, west, each its layer plus one; 0 keeps the one above), framed where the face
	// meets what the wall does not join (the owner, 2026-11-10)
	if (kind > 1.5 && kind < 2.5 && floor(UV2.y + 0.5) > 0.5) {
		float face = NORMAL.z > 0.5 ? COLOR.r : NORMAL.x > 0.5 ? COLOR.g : NORMAL.z < -0.5 ? COLOR.b : COLOR.a;
		if (face > 0.5) { v_layer = floor(face + 0.5) - 1.0; }
	}
	if (kind > 2.5 && floor(UV2.y + 0.5) < 0.5) {
		// The top of a diagonal corner wall: its picture already is the one turned for its junction, so the mesh's turn (the instance's basis) must not turn it again:
		// the uv follows the vertex's place in the unturned tile (north up).
		vec3 turned = (MODEL_MATRIX * vec4(VERTEX, 0.0)).xyz;
		UV = vec2(turned.x * 0.5 + 0.5, turned.z * 0.5 + 0.5);
	}
	v_part = kind;
	v_condition = INSTANCE_CUSTOM.w;
	// A wall with nothing over it (w = 3) has its top at the full height, level with its sides: sunk TopSink (WorldGeometry.TopSink) the sides of the walls beside it
	// stood over it as lines between the blocks (the owner, 2026-11-10); the sink only keeps the top under the floor of a level above.
	if (kind > 1.5 && floor(UV2.y + 0.5) < 0.5 && v_condition > 2.5 && NORMAL.y > 0.5) {
		VERTEX.y += 0.02;
	}
	// D288: a hidden ceiling still shades the station from the sun (its shadow pass is the one drawn with an orthographic projection).
	if (part != want || (kind == 1.0 && !ceilings && PROJECTION_MATRIX[3][3] < 0.5)) {
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
	if (c.a < 0.5) { discard; }
	vec3 albedo = c.rgb;
	if (v_part > 0.5 && v_part < 1.5) {
        if (v_condition < 0.0 && (UV.x < 0.04 || UV.y < 0.04 || UV.x > 0.96 || UV.y > 0.96)) { albedo = vec3(0.9, 0.4, 0.05); }
		albedo *= ceiling_dim;
	}
	// DV265: a wet floor (w = 2, TileKit.IsWet) is darker and glossy, so tg's pale water overlay reads as water in 3D
	bool wet = v_part < 0.5 && v_condition > 1.5 && v_condition < 2.5;
	// a polished floor (w = 4, TurfStyle.Polished) is smooth enough for the screen-space reflections, where the graphics have them on
	bool polished = v_part < 0.5 && v_condition > 3.5 && v_condition < 4.5;
	// a floor or a ceiling's block that gives light (w = 5, a ceiling's -5 unsupported: a light floor, lava) shines by itself
	bool glows = v_part < 1.5 && abs(v_condition) > 4.5;
	if (wet) { albedo *= vec3(0.55, 0.6, 0.68); }
	albedo = anomaly_previewed(albedo, v_world, 1.0); // the editor's preview of a turf
	ALBEDO = albedo;
	ROUGHNESS = wet ? 0.12 : polished ? 0.45 : 1.0;
	SPECULAR = wet ? 0.8 : polished ? 0.2 : 0.0;
	// The light grid lights what the surface shows; the sample sits just off the surface so a wall face reads its own tile's corners.
	vec3 wn = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	// D325: a floor and the ceiling under it share a height (a level's foot is the head of the one below): a centimetre along the
	// normal's vertical puts each in its own level of a multi-z stack
	vec3 p = v_world + vec3(0.0, wn.y * 0.01, 0.0);
	vec3 grid = anomaly_grid_light(p);
	EMISSION = albedo * grid;
	EMISSION += albedo * anomaly_sky_light(p, wn);
	// the grid's light is emission, which no specular sees: the water's sheen grows towards a grazing view (Schlick's fresnel)
	if (wet) { EMISSION += grid * 0.55 * pow(1.0 - clamp(dot(NORMAL, VIEW), 0.0, 1.0), 3.0); }
	if (glows) { EMISSION += c.rgb * tile_glow; }
	anomaly_sun_share = anomaly_sun_share_at(p, wn, length(VERTEX));
}
