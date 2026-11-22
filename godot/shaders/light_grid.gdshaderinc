// The light grid's shader include (docs/ai/specs/light-grid.md section 7; names in scripts/Shared/Light/LightGridUniforms.cs).
// Contract: the M3 contracts step; body: M3 P7. Every world material of M3 P6 includes it and adds
// ALBEDO * anomaly_grid_light(world_pos) to EMISSION. The globals are declared in project.godot's [shader_globals].

global uniform sampler2D anomaly_light_grid;
global uniform vec2 anomaly_light_grid_size;
global uniform vec3 anomaly_light_grid_origin;
global uniform float anomaly_light_tile_metres;
global uniform vec3 anomaly_light_ambient;
global uniform float anomaly_light_strength;
global uniform float anomaly_light_cap; // the most a surface gets after the scale (tg: never brighter than its sprite); 0 for no cap
global uniform vec3 anomaly_light_floor; // protocol 31: tg's lighting cutoff (the mesons' and night vision's), the least light a surface gets
global uniform sampler2D anomaly_light_cells; // per tile (texel (x, y) = tile (x, y)): DoorCellLight's code * 127, 0 for most cells
// D288: the light of space (SkyLight.cs): per tile 1 where the cell is open to the sky; the ambient cube's faces; how far the sun's shadow decides alone
global uniform sampler2D anomaly_exterior;
global uniform vec3 anomaly_sky_up;
global uniform vec3 anomaly_sky_side;
global uniform vec3 anomaly_sky_down;
global uniform float anomaly_sun_reach;
global uniform float anomaly_level_height;
global uniform float anomaly_light_grid_levels; // D325: the levels of the camera's multi-z stack in the textures, a block of rows each, lowest first

// The map editor's preview (docs/ai/specs/map-editor.md 5.9): what a tool is about to place is drawn in the highlight's light blue. The previewed
// things' boxes (world metres) are the texels of anomaly_preview_boxes: (i, 0) a box's least corner and (i, 1) its greatest; the first's alpha is 1
// for a turf's box (the floors' and the walls' materials take it) and 0 for a thing's (every other material). anomaly_preview_min and _max hold
// them all. anomaly_preview_count is 0 outside the editor and while nothing is previewed: nothing is read then.
global uniform sampler2D anomaly_preview_boxes;
global uniform float anomaly_preview_count;
global uniform vec3 anomaly_preview_min;
global uniform vec3 anomaly_preview_max;

// A surface's colour, in the preview's blue where world_pos is in a previewed box of its kind (turf: 1 for a floor or a wall, 0 for a thing).
// A picture turned to the eye gives the place it stands at, not the fragment's: its depth is not its own (depth_order.gdshaderinc).
vec3 anomaly_previewed(vec3 colour, vec3 world_pos, float turf) {
	if (anomaly_preview_count < 0.5 || any(lessThan(world_pos, anomaly_preview_min)) || any(greaterThan(world_pos, anomaly_preview_max))) {
		return colour;
	}
	int count = int(anomaly_preview_count + 0.5);
	for (int i = 0; i < count; i++) {
		vec4 low = texelFetch(anomaly_preview_boxes, ivec2(i, 0), 0);
		vec3 high = texelFetch(anomaly_preview_boxes, ivec2(i, 1), 0).rgb;
		if (abs(low.a - turf) < 0.5 && all(greaterThanEqual(world_pos, low.rgb)) && all(lessThanEqual(world_pos, high))) {
			// a blue cast and a little blue of its own: a dark thing shows it too, and a thing's own picture stays readable
			return colour * vec3(0.72, 0.9, 1.18) + vec3(0.0, 0.015, 0.05);
		}
	}
	return colour;
}

// D325: which block of rows (the level of the stack, 0 the lowest) a point at world_pos reads; rows per block of a texture `rows` high in all.
int anomaly_stack_block(vec3 world_pos) {
	float n = max(anomaly_light_grid_levels, 1.0);
	return int(clamp(floor((world_pos.y - anomaly_light_grid_origin.y) / max(anomaly_level_height, 0.01)), 0.0, n - 1.0));
}

// How open to the sky the cell a surface faces is (0 to 1): the cell half a tile along its normal, so a hull's outer face reads the space
// before it and its inner face the room; blended between the four tile centres around that point.
float anomaly_open_sky(vec3 world_pos, vec3 world_normal) {
	vec3 q = world_pos + world_normal * (0.5 * anomaly_light_tile_metres);
	vec2 t = vec2(q.x - anomaly_light_grid_origin.x, anomaly_light_grid_origin.z - q.z) / anomaly_light_tile_metres + vec2(0.5);
	ivec2 size = textureSize(anomaly_exterior, 0);
	int rows = size.y / int(max(anomaly_light_grid_levels, 1.0));
	ivec2 block = ivec2(0, anomaly_stack_block(world_pos) * rows);
	size.y = rows;
	vec2 f = fract(t);
	ivec2 c0 = clamp(ivec2(floor(t)), ivec2(0), size - ivec2(1)) + block;
	ivec2 c1 = clamp(ivec2(floor(t)) + ivec2(1), ivec2(0), size - ivec2(1)) + block;
	float s00 = texelFetch(anomaly_exterior, ivec2(c0.x, c0.y), 0).r;
	float s10 = texelFetch(anomaly_exterior, ivec2(c1.x, c0.y), 0).r;
	float s01 = texelFetch(anomaly_exterior, ivec2(c0.x, c1.y), 0).r;
	float s11 = texelFetch(anomaly_exterior, ivec2(c1.x, c1.y), 0).r;
	return mix(mix(s00, s10, f.x), mix(s01, s11, f.x), f.y);
}

// The light of space a surface gets (add ALBEDO times it to EMISSION): the stars and the planet as an ambient cube by the world normal, where the sky is open.
vec3 anomaly_sky_light(vec3 world_pos, vec3 world_normal) {
	float open = anomaly_open_sky(world_pos, world_normal);
	vec3 n2 = world_normal * world_normal;
	return open * (n2.y * (world_normal.y > 0.0 ? anomaly_sky_up : anomaly_sky_down) + (n2.x + n2.z) * anomaly_sky_side);
}

// How much of the sun a surface may get: within anomaly_sun_reach of the camera its shadow decides (walls and ceilings shade it, windows let
// it in), beyond that only where the sky is open (no shadow is drawn there). view_distance: the fragment's distance from the camera.
// Under a covered cell the band just below the ceiling gets none: the shadow leaks at the joint of a wall and the ceiling (its bias,
// wider in the farther and coarser splits), and the sun through a window reaches that high only at the window.
float anomaly_sun_share_at(vec3 world_pos, vec3 world_normal, float view_distance) {
	float near = anomaly_sun_reach > 0.0 ? 1.0 - smoothstep(anomaly_sun_reach * 0.85, anomaly_sun_reach, view_distance) : 0.0;
	float lh = max(anomaly_level_height, 0.01);
	// moved along the normal and a little down: a floor (at the level's foot) and a ceiling (at its head, the same height) fall on their sides
	float below = lh - mod(world_pos.y + world_normal.y * 0.02 - 0.005 - anomaly_light_grid_origin.y, lh);
	float band = smoothstep(0.1 + 0.02 * view_distance, 0.25 + 0.03 * view_distance, below);
	return max(near * band, anomaly_open_sky(world_pos, world_normal));
}

// The light the grid gives a surface point at world_pos (metres): tg's lighting object blends the four corner values of a
// tile bilinearly, and so does this. Corner (i, j) is the texel (i, j): the south-west corner of tile (i + 1, j + 1), at
// world X = origin.x + i * tile and world Z = origin.z - j * tile (north is -Z). The corners are fetched one by one and
// blended here, so the result does not depend on the sampler state a global texture gets.
vec3 anomaly_grid_light(vec3 world_pos) {
	vec2 p = vec2(world_pos.x - anomaly_light_grid_origin.x, anomaly_light_grid_origin.z - world_pos.z) / anomaly_light_tile_metres;
	ivec2 size = ivec2(anomaly_light_grid_size);
	// D325: the level of the stack the point is on reads its own block of rows of the grid and the cells
	int rows = size.y / int(max(anomaly_light_grid_levels, 1.0));
	ivec2 block = ivec2(0, anomaly_stack_block(world_pos) * rows);
	size.y = rows;
	// A closed door's cell (DoorCellLight): the point reads the corners of the tile edge on its side of the door's plane, as a wall's face does.
	// A face on the edge it shares with a door's cell (a wall's jamb) takes the door's code: floor(p) on that edge flips between the two cells
	// pixel by pixel, and the wall's own blend would mix in the corners of the room behind the door.
	float snap = 0.0;
	for (int i = 0; i < 4 && snap == 0.0; i++) {
		vec2 q = p + vec2(i % 2 == 0 ? -1e-3 : 1e-3, i < 2 ? -1e-3 : 1e-3);
		snap = floor(texelFetch(anomaly_light_cells, clamp(ivec2(floor(q)) + ivec2(1), ivec2(0), size - ivec2(1)) + block, 0).r * 2.0 + 0.5);
	}
	if (snap == 1.0) {
		p.x = floor(p.x) + step(0.5, fract(p.x));
	} else if (snap == 2.0) {
		p.y = floor(p.y) + step(0.5, fract(p.y));
	}
	vec2 f = fract(p);
	ivec2 c0 = clamp(ivec2(floor(p)), ivec2(0), size - ivec2(1));
	ivec2 c1 = clamp(c0 + ivec2(1), ivec2(0), size - ivec2(1));
	c0 += block;
	c1 += block;
	vec3 l00 = texelFetch(anomaly_light_grid, ivec2(c0.x, c0.y), 0).rgb;
	vec3 l10 = texelFetch(anomaly_light_grid, ivec2(c1.x, c0.y), 0).rgb;
	vec3 l01 = texelFetch(anomaly_light_grid, ivec2(c0.x, c1.y), 0).rgb;
	vec3 l11 = texelFetch(anomaly_light_grid, ivec2(c1.x, c1.y), 0).rgb;
	vec3 light = mix(mix(l00, l10, f.x), mix(l01, l11, f.x), f.y);
	light = max(light, anomaly_light_floor);
	light *= anomaly_light_strength;
	if (anomaly_light_cap > 0.0) {
		light = min(light, vec3(anomaly_light_cap));
	}
	return light + anomaly_light_ambient;
}
