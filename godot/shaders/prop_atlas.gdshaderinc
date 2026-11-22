// The body of prop_atlas.gdshader (the opaque pass) and of prop_glass.gdshader (GLASS_PASS: the see-through glass pixels, blended).
#include "res://shaders/light_grid.gdshaderinc"
#define ANOMALY_WRAP_VARYING
#include "res://shaders/world_light.gdshaderinc"
#include "res://shaders/depth_order.gdshaderinc"
global uniform float anomaly_lamp_glow; // the same for a lamp model's light layer: 4 (the owner, 2026-10-24); its flag is its glow plus 2 (MapRenderer)
global uniform float anomaly_model_glow; // how much a model's light layer adds over its lit picture: 0.5 (the owner, 2026-10-24; 2.5 overlit a vending machine's whole front); the dev menu tunes it

// The atlas's pages are the layers (AtlasBuilder.PageHeight rows each); a picture lies in one page. atlas_size: one page's size.
uniform sampler2DArray atlas : source_color, filter_nearest, repeat_disable;
uniform sampler2D metrics : filter_nearest, repeat_disable;
uniform vec2 atlas_size = vec2(2048.0, 2048.0);
uniform float scissor = 0.5;
const float MODEL_FLOOR_LIFT = 0.002; // metres: a model's floor-level vertices above the floor's plane, its ceiling-level ones under the ceiling's
#ifdef GLASS_PASS
#include "res://shaders/self_reflection.gdshaderinc"
uniform sampler2D glass_depth : hint_depth_texture, filter_nearest;
uniform sampler2D glass_screen : hint_screen_texture, filter_linear, repeat_disable;
#endif
const float GLASS_SOLID = 0.97; // a glass pixel at least this dense is solid (the old stipple drew every pixel of it)
const float POD_DOOR_LIP = 0.12; // metres of an open pod door or shutters left under the ceiling
uniform float wall_height = 2.0; // WorldScale.WallHeight (D119); the pod door rises by it, into the ceiling
uniform float item_scale = 0.5; // WorldScale.ItemScale (D121)
uniform float floor_lift = 0.0; // the least a floor decal floats above the floor (metres): the planner's height (FloorLayers.BaseLift and up) is all of it
uniform float frame_half = 1.0; // half the icon square (metres): its centre's height in a billboard's frame (BYOND turns a transform about it)

varying vec2 v_tex;
varying vec4 v_rect;
varying vec3 v_world;
// The scalars are packed into vectors: Godot spends a whole varying slot on each varying, and Intel's UHD (JSL) on Linux allows 29 with
// Godot's own (a player's log, 2026-11-14: 30 used, the shader failed to compile and every prop drew white). The names stay as macros.
varying flat vec4 v_flat_a;
varying flat vec3 v_flat_b;
varying vec3 v_smooth;
#define v_mode v_flat_a.x
// D288: 1 in a light's shadow pass (its eye is not the main camera's)
#define v_shadow_pass v_flat_a.y
#define v_part v_flat_a.z
#define v_flag v_flat_a.w
// a glass face of a 3D model part: its own alpha (0: not glass)
#define v_glass v_flat_b.x
// D152 (block windows): a window block's face draws its picture's see-through glass pixels as a stipple of this share of their alpha (0: cut by the scissor)
#define v_pane v_flat_b.y
// an airlock panel's local x after its slide: past the tile's edge (|x| > 1) it is in the jamb and not drawn, so an open door's panels never show in the next doorway or behind a window
#define v_door_x v_smooth.x
// a sliding model part's height after its slide, as -1 at the floor and 1 at the ceiling: beyond them it is not drawn
#define v_door_y v_smooth.y
// the world height above which a fragment is not drawn (a rising pod door is hidden by the ceiling); far above everything else
#define v_clip v_flat_b.z
varying flat vec4 v_modulate; // M4v: an animation's colour and alpha factors (a billboard's), else 1
// A flat-colour model piece (instance custom w = 2: its material is Flat, no shading painted into it, a canister's): the world has no directional light, so
// it is shaded by its face's normal, toward the eye and up lighter, its silhouette's faces darker (the owner, 2026-10-20: the canisters looked flat); else 1.
#define v_shade v_smooth.z

vec4 metric(int id, int k) {
	int t = id * 2 + k;
	return texelFetch(metrics, ivec2(t % 2048, t / 2048), 0);
}

void vertex() {
	float mode = floor(COLOR.r * 8.0 + 0.5);
	float want = floor(COLOR.g * 32.0 + 0.5);
	int id = int(INSTANCE_CUSTOM.x + 0.5) + int(INSTANCE_CUSTOM.y + 0.5) * 2048;
	v_mode = mode;
	v_part = want;
	v_flag = COLOR.b;
	v_glass = 0.0;
	v_shadow_pass = distance(INV_VIEW_MATRIX[3].xyz, MAIN_CAM_INV_VIEW_MATRIX[3].xyz) > 0.001 ? 1.0 : 0.0;
	v_pane = 0.0;
	v_clip = 100000.0;
	v_door_x = 0.0;
	v_door_y = 0.0;
	v_modulate = vec4(1.0);
	v_shade = 1.0;
	if (mode > 5.5) {
		// A 3D model part: the mesh is the part's own; it slides open by its UV2.x.
		vec4 rect = metric(id, 0);
		v_rect = rect;
		v_tex = UV;
		bool glass = UV2.y < -8.0;
		float steps = glass ? floor((-UV2.y + 8.0) / 16.0) : 0.0;
		vec2 slide = vec2(UV2.x, UV2.y + 16.0 * steps);
		v_glass = glass ? max(1.0 - (steps - 1.0) / 100.0, 0.001) : 0.0;
		// A model's vertices on the floor's plane stand 2 mm above it: a frame's floor track between its sill rails (the external airlock's, the firelocks')
		// lay on the floor tile and fought with it (the owner's play tests, 2026-10-16); 2 mm is under the floor decals' lowest layer (4 mm). The plane is the
		// model's own, before a part slides: an open external airlock's lower leaf sinks until its top is at the floor, and lifted it showed there as a strip
		// across the doorway (the owner, 2026-10-18).
		if (abs(VERTEX.y) < 0.0005) {
			VERTEX.y = MODEL_FLOOR_LIFT;
		}
		// As much under the ceiling's plane (a model fitted to the wall's height is built to it): a firelock frame's top and an airlock's leaves fought with the
		// ceiling over the doorway (the owner, 2026-10-18).
		if (abs(VERTEX.y - wall_height) < 0.0005) {
			VERTEX.y = wall_height - MODEL_FLOOR_LIFT;
		}
		VERTEX.xy += COLOR.a * slide;
		if (abs(slide.x) + abs(slide.y) > 0.0001) {
			// A sliding part is cut at the jamb as the box panels are, and at the floor and the ceiling (an external airlock's leaves, a hatch's sections).
			v_door_x = VERTEX.x;
			v_door_y = VERTEX.y * 2.0 / wall_height - 1.0;
		}
		v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
		vec3 n = mat3(MODEL_MATRIX) * NORMAL;
		if (INSTANCE_CUSTOM.w > 1.5 && length(n) > 0.00001) {
			n = normalize(n);
			vec3 to_eye = normalize(INV_VIEW_MATRIX[3].xyz - v_world);
			v_shade = 0.55 + 0.3 * abs(dot(n, to_eye)) + 0.15 * max(n.y, 0.0);
		}
	} else if (mode < 1.5 || mode > 2.5 && mode < 5.5) {
		// A picture quad: the mesh is a unit quad, x from -0.5 to 0.5 and y from 0 to 1, uv.y 0 at the bottom.
		bool item = mode > 2.5 && mode < 3.5;
		bool resting = mode > 4.5;
		bool billboard = mode < 0.5 || item || resting;
		bool floor_decal = mode > 3.5 && !resting;
		float frames = max(INSTANCE_CUSTOM.w, 1.0);
		float frame = 0.0;
		vec3 here = MODEL_MATRIX[3].xyz;
		if (billboard && frames > 1.5) {
			vec3 eye = INV_VIEW_MATRIX[3].xyz;
			float bearing = degrees(atan(eye.x - here.x, -(eye.z - here.z)));
			float relative = mod(bearing - INSTANCE_CUSTOM.z * 360.0 / 256.0 + 720.0, 360.0);
			frame = floor(mod((relative + 180.0 / frames) / (360.0 / frames), frames));
		}
		id += int(frame);
		vec4 rect = metric(id, 0);
		vec4 quad = metric(id, 1);
		v_rect = rect;
		v_tex = vec2(UV.x, UV.y);
		if (billboard) {
			// An item is drawn at item_scale about the bottom centre of its picture, which rests on the origin; a billboard keeps the picture's own place in its frame.
			float s = item ? item_scale : 1.0;
			float bottom = item || resting ? 0.0 : quad.w;
			VERTEX = vec3(s * (quad.z + VERTEX.x * quad.x), s * (bottom + VERTEX.y * quad.y), 0.0);
			// M4v: animate()'s look from the instance basis (identity: none), about the icon's centre (an item's and a resting one's frame is moved down by quad.w).
			vec2 pivot = vec2(0.0, s * (frame_half - (item || resting ? quad.w : 0.0)));
			mat2 turn = mat2(MODEL_MATRIX[0].xy, MODEL_MATRIX[1].xy);
			VERTEX.xy = turn * (VERTEX.xy - pivot) + pivot + vec2(MODEL_MATRIX[1].z, 0.0);
			v_modulate = vec4(1.0 - MODEL_MATRIX[2].x, 1.0 - MODEL_MATRIX[2].y, MODEL_MATRIX[2].z, 1.0 - MODEL_MATRIX[0].z);
			// A billboard turned about the vertical axis only: its right is the camera's right on the ground plane. An item faces the eye fully:
			// its up is the camera's up, tilted back about its feet (the owner, 2026-11-19; picture_billboard.gdshader's face_eye)
			vec3 right = normalize(vec3(INV_VIEW_MATRIX[0].x, 0.0, INV_VIEW_MATRIX[0].z));
			vec3 up = item ? normalize(cross(INV_VIEW_MATRIX[2].xyz, right)) : vec3(0.0, 1.0, 0.0);
			vec3 back = cross(right, up);
			MODELVIEW_MATRIX = VIEW_MATRIX * mat4(vec4(right, 0.0), vec4(up, 0.0), vec4(back, 0.0), MODEL_MATRIX[3]);
			// its place in its tile's order (the instance colour's green: DepthRank, metres); an item at its own depth, not its tile centre's (D329: an item
			// off the centre toward the eye, pushed back along the eye's ray, sank into the floor or the table it lies on)
			MODELVIEW_MATRIX = depth_ordered(MODELVIEW_MATRIX, VIEW_MATRIX, PROJECTION_MATRIX, here, !item, COLOR.g);
			MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
			NORMAL = vec3(0.0, 0.0, 1.0);
			v_world = here + vec3(0.0, 0.3, 0.0);
		} else if (floor_decal) {
			// A hatch lying on the floor, seen from above the way BYOND draws the map: the picture's right is east (+x) and its up is north (-z); the icon square's
			// centre is the tile's centre, so the picture's height above the square's bottom edge (quad.w + y, 2 m up to the square's top) runs from z = +1 to z = -1.
			// Its height is its tg layer's place in the stack of the floor layers (D122: the instance colour's green is metres * 16; the lattice lowest, then the disposals, the wires,
			// the pipes, the vents), so that things on one tile never fight; floor_lift is the least a thing floats above the floor.
			VERTEX = vec3(quad.z + VERTEX.x * quad.x, floor_lift + COLOR.g / 16.0, 1.0 - (quad.w + VERTEX.y * quad.y));
			// M4v slice 4: animate()'s look from the instance basis (identity: none), as a billboard's: the transform's linear part turns the picture in
			// the floor's plane about the icon's centre (the tile's centre; the picture's up is north, -z); the moves are the instance's origin.
			vec2 laid = mat2(MODEL_MATRIX[0].xy, MODEL_MATRIX[1].xy) * vec2(VERTEX.x, -VERTEX.z);
			VERTEX.x = laid.x;
			VERTEX.z = -laid.y;
			v_modulate = vec4(1.0 - MODEL_MATRIX[2].x, 1.0 - MODEL_MATRIX[2].y, MODEL_MATRIX[2].z, 1.0 - MODEL_MATRIX[0].z);
			MODELVIEW_MATRIX = VIEW_MATRIX * mat4(vec4(1.0, 0.0, 0.0, 0.0), vec4(0.0, 1.0, 0.0, 0.0), vec4(0.0, 0.0, 1.0, 0.0), MODEL_MATRIX[3]);
			MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
			NORMAL = vec3(0.0, 1.0, 0.0);
			v_world = here + VERTEX + vec3(0.0, 0.3, 0.0);
		} else {
			// A decal: flat on a wall face (or a camera's fin), centred on its mount height. Its yaw is the instance colour's alpha (turns), and the
			// basis carries animate()'s look as a billboard's (M4v slice 4): the transform about the icon's centre (frame_half above the frame's
			// bottom; the picture's middle is quad.w + quad.y / 2 above it), the sideways move in the decal's plane, the colour and alpha factors.
			VERTEX = vec3(quad.z + VERTEX.x * quad.x, (VERTEX.y - 0.5) * quad.y, 0.0);
			vec2 pivot = vec2(0.0, frame_half - quad.w - 0.5 * quad.y);
			VERTEX.xy = mat2(MODEL_MATRIX[0].xy, MODEL_MATRIX[1].xy) * (VERTEX.xy - pivot) + pivot + vec2(MODEL_MATRIX[1].z, 0.0);
			v_modulate = vec4(1.0 - MODEL_MATRIX[2].x, 1.0 - MODEL_MATRIX[2].y, MODEL_MATRIX[2].z, 1.0 - MODEL_MATRIX[0].z);
			float yaw = COLOR.a * TAU;
			mat4 placed = mat4(vec4(cos(yaw), 0.0, -sin(yaw), 0.0), vec4(0.0, 1.0, 0.0, 0.0), vec4(sin(yaw), 0.0, cos(yaw), 0.0), MODEL_MATRIX[3]);
			MODELVIEW_MATRIX = VIEW_MATRIX * placed;
			MODELVIEW_NORMAL_MATRIX = mat3(MODELVIEW_MATRIX);
			NORMAL = vec3(0.0, 0.0, 1.0);
			v_world = (placed * vec4(VERTEX, 1.0)).xyz;
		}
	} else {
		// A shaped piece: the mesh holds every part; this instance draws one and the rest are moved out of the view.
		float part = floor(UV2.x + 0.5);
		vec4 rect = metric(id, 0);
		v_rect = rect;
		v_tex = UV;
		if (part != want) {
			VERTEX = vec3(0.0);
		}
		// The arms of a joined window or grille and the corner posts of a border piece: a face with an arm bit is drawn only when the instance's joints mask has it.
		float arm = floor(UV2.y + 0.5);
		if (arm > 0.5 && mod(floor(INSTANCE_CUSTOM.z / arm + 0.01), 2.0) < 0.5) {
			VERTEX = vec3(0.0);
		}
		float open = COLOR.a;
		// The block faces (parts 17 to 21) and the directional window (part 1) carry no slide: a window's carry the glass's share in their alpha instead.
		// So do a firelock's slabs (parts 11 and 12, which never slide): a holofirelock's carry its see-through share (MapRenderer.HoloPane).
		if (want > 16.5 && want < 21.5 || want == 1.0 || want == 11.0 || want == 12.0) {
			v_pane = open;
			open = 0.0;
		}
		if (want == 7.0) { VERTEX.x -= open * 0.98; v_door_x = VERTEX.x; }
		else if (want == 8.0) { VERTEX.x += open * 0.98; v_door_x = VERTEX.x; }
		else if (want == 9.0) {
			// A pod door or shutters roll up into the ceiling, as tg's (D121, the owner: "better into the ceiling, as in tg"): the slab rises by its height and what goes above the
			// ceiling of its level is not drawn, so nothing shows under the station or over the roof.
			// The owner (2026-10-15): open, its bottom edge still hangs a lip under the ceiling, so the player sees there are shutters above the doorway.
			VERTEX.y += open * (wall_height - POD_DOOR_LIP);
			v_clip = MODEL_MATRIX[3].y + wall_height + 0.002;
		}
		else if (want == 10.0) { VERTEX.x += open * 1.95; }
		v_world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	}
#ifdef GLASS_PASS
	if (v_glass <= 0.0 && v_pane <= 0.0) {
		VERTEX = vec3(0.0); // no glass on this face: its triangles collapse and are never drawn
	}
#endif
}

void fragment() {
	// Every face shows ONE whole picture (or its intended share of it): uv 0..1 over the face, no wrap and no repeat. The mesh's v runs up,
	// the picture's down. The lookup stays inside the stored rectangle (a hair from its edges), so nearest filtering never reads a
	// neighbour's pixel in the atlas.
	vec2 local = vec2(v_tex.x, 1.0 - v_tex.y) * v_rect.zw;
	// A model's face whose UVs have no extent on an axis (an extruded pixel's side: the builders map it to one texel edge or corner) is read
	// a thousandth of a texel off that edge, so the interpolation's rounding does not flip its pixels between the neighbouring texels (the
	// owner, 2026-11-11: speckled faces on the gravity generator's coils, the tank holders and other models).
	if (v_mode > 5.5) {
		local += vec2(0.001);
	}
	local = clamp(local, vec2(0.01), max(v_rect.zw - vec2(0.01), vec2(0.01)));
	float page = floor(v_rect.y / atlas_size.y);
	vec2 at = v_rect.xy + local - vec2(0.0, page * atlas_size.y);
	vec4 c = texture(atlas, vec3(at / atlas_size, page)) * v_modulate;
	if (v_rect.z < 0.5 || v_world.y > v_clip) {
		discard;
	}
	float noise = fract(52.9829189 * fract(dot(FRAGCOORD.xy, vec2(0.06711056, 0.00583715))));
	// a sliding part at the floor's plane or below it is cut (an open external airlock's sunk lower leaf, whose top lies on the floor, would fight with it);
	// a part's own floor-level vertices stand above the plane (MODEL_FLOOR_LIFT, at most 4 m walls: -0.999)
	if (abs(v_door_x) > 1.0 || v_door_y < -0.9995 || v_door_y > 1.001) {
		discard;
	}
	// Glass (the owner, 2026-11-13: its screen-door stipple showed as a static grid from afar): a glass pixel is see-through by its share (a model's
	// glass insert: the picture's alpha times its own; a window's pane: tg's faint pane pixels, alpha 0.3, times the pane's share). The opaque pass
	// draws only the solid pixels (the frame, a shut airlock's fill overlay), and the glass pass (prop_glass.gdshader, this material's next pass)
	// blends the see-through ones over what is behind them. It writes no depth and casts no shadow (D288: the sun shines through the windows).
	float glass_share = -1.0; // below 0: not glass
	if (v_glass > 0.0) {
		glass_share = c.a * v_glass;
	} else if (v_pane > 0.0 && c.a < 0.99) {
		glass_share = c.a < 0.02 ? 0.0 : c.a * v_pane;
		// DEVIATION(DV55): tg's full-tile tinted window is opaque; drawn dark but see-through.
		// A tinted window's whole sprite is half transparent (tinted_window.dmi, alpha 0.53, frame included): its light frame pixels are drawn solid, its dark glass
		// denser and at half its brightness (the owner, 2026-10-16: darker).
		if (v_pane > 0.99 && c.a >= 0.02) {
			if (dot(c.rgb, vec3(0.299, 0.587, 0.114)) > 0.3) {
				glass_share = 1.0;
			} else {
				glass_share = min(c.a * 1.5, 1.0);
				c.rgb *= 0.5;
			}
		}
	}
#ifdef GLASS_PASS
	if (v_shadow_pass > 0.5 || glass_share <= 0.0 || glass_share >= GLASS_SOLID) {
		discard;
	}
#else
	if (glass_share >= 0.0) {
		if (glass_share < GLASS_SOLID) {
			discard;
		}
	} else if (c.a < (v_modulate.a < 0.999 || v_mode > 3.5 && v_mode < 4.5 ? max(noise, 0.004) : scissor)) {
		// DEVIATION(DV58): a floor decal's see-through pixels (tg's faint footprints, alpha 20 and up by the blood on the shoe; dirt) are a
		// screen-door stipple whose share of drawn pixels is their alpha, as BYOND's blending would show them; the scissor cut them away.
		discard;
	}
#endif
	// the editor's preview: a picture turned to the eye (modes 0, 3, 5) is asked where it stands and a floor decal (4) where it lies, 0.3 m under v_world
	c.rgb = anomaly_previewed(c.rgb, v_mode < 0.5 || v_mode > 2.5 && v_mode < 5.5 ? v_world - vec3(0.0, 0.3, 0.0) : v_world, 0.0);
	ALBEDO = c.rgb;
	ROUGHNESS = 1.0;
	vec3 lit = ALBEDO * anomaly_grid_light(v_world) * v_shade;
	bool piece = v_mode > 1.5 && v_mode < 2.5 || v_mode > 5.5;
	if (piece && v_part == 6.0) {
		// A lit lamp (D142): its picture carries tg's glow over the tube or bulb; the light makes it bright. A model's light layer (its Emission_Overlay: only
		// tg's lighting_overlay pixels) glows over its lit picture (anomaly_model_glow).
		float lamp = v_mode > 5.5 && v_flag > 1.5 ? 1.0 : 0.0;
		lit += ALBEDO * (v_mode > 5.5 ? mix(anomaly_model_glow, anomaly_lamp_glow, lamp) : 1.6) * (v_flag - 2.0 * lamp);
	} else if (piece && v_flag > 0.5) {
		lit += vec3(0.7, 0.05, 0.05);
	}
	EMISSION = lit;
	if (!FRONT_FACING) {
		NORMAL = -NORMAL;
	}
	vec3 wn = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	EMISSION += ALBEDO * anomaly_sky_light(v_world, wn) * v_shade;
#ifdef GLASS_PASS
	// a pane draws no shadow (shadows_disabled), so the ceilings cannot keep the sun off it: only the open sky lets it in
	anomaly_sun_share = anomaly_open_sky(v_world, wn);
#else
	anomaly_sun_share = anomaly_sun_share_at(v_world, wn, length(VERTEX));
#endif
	// a billboard, an item or a resting billboard (modes 0, 3, 5) is a picture turned to the eye
#ifdef GLASS_PASS
	// a sheen that grows toward a grazing view (Schlick's fresnel), in the light of its place, so a pane reads as glass and not as a hole
	// only on the face turned to the eye: a window block's far and side faces, seen from inside, doubled the sheen and showed the
	// reflection on every side of the frame (the owner, 2026-11-13)
	float facing = FRONT_FACING ? 1.0 : 0.0;
	float sheen = facing * pow(1.0 - clamp(abs(dot(NORMAL, VIEW)), 0.0, 1.0), 4.0);
	EMISSION += (anomaly_grid_light(v_world) + anomaly_sky_light(v_world, wn)) * vec3(0.6, 0.72, 0.9) * sheen * 0.12;
	// a window block is seen through its near and its far face: each takes the share that makes the pane's own of the two
	float layer = v_pane > 0.0 ? 1.0 - sqrt(1.0 - glass_share) : glass_share;
	ALPHA = min(layer + sheen * 0.08, 1.0);
	// The pane's reflection (the owner, 2026-11-13): the view's mirror ray is walked through the depth of the opaque view, and where it goes
	// behind something seen, that thing's picture is laid on the glass, more toward a grazing view (the screen-space reflections do not reach a
	// blended surface). Only what is on screen is reflected, so it fades at the screen's edges and is nothing head-on.
	vec3 eye_ray = normalize(VERTEX);
	// faint: glass's few per cent head-on, a quarter at a grazing view, so it grows evenly with the angle and never covers the room behind
	float mirror = facing * mix(0.04, 0.25, pow(1.0 - clamp(dot(-eye_ray, NORMAL), 0.0, 1.0), 3.0));
	// A ray turned back toward the eye (a pane seen head-on, or close up) goes to what is beside or behind the camera, which the screen does not
	// hold: its points crowd toward the screen's edges and slide across it far faster than the eye moves, so one thing was picked up again and again
	// and ran over the glass (the owner, 2026-11-14). Such rays fade out; the hit is refined between the last two steps and must lie close behind
	// what is seen there, and a far hit fades.
	vec3 way = reflect(eye_ray, NORMAL);
	// the own body in first person, which the screen does not hold (self_reflection.gdshaderinc): traced, so it shows head-on too
	vec4 self_seen = anomaly_self_reflection((INV_VIEW_MATRIX * vec4(VERTEX, 1.0)).xyz, normalize((INV_VIEW_MATRIX * vec4(way, 0.0)).xyz));
	float self_k = facing * self_seen.a * mix(0.1, 0.25, pow(1.0 - clamp(dot(-eye_ray, NORMAL), 0.0, 1.0), 3.0));
	mirror *= 1.0 - smoothstep(0.05, 0.4, way.z);
	if (mirror > 0.0) {
		float t = 0.12;
		float last = 0.0;
		for (int i = 0; i < 22; i++) {
			vec3 q = VERTEX + way * t;
			if (q.z > -0.3) {
				break;
			}
			vec4 clip = PROJECTION_MATRIX * vec4(q, 1.0);
			vec2 suv = clip.xy / clip.w * 0.5 + 0.5;
			if (suv.x < 0.0 || suv.y < 0.0 || suv.x > 1.0 || suv.y > 1.0) {
				break;
			}
			vec4 seen = INV_PROJECTION_MATRIX * vec4(suv * 2.0 - 1.0, texture(glass_depth, suv).r, 1.0);
			float behind = seen.z / seen.w - q.z;
			if (behind > 0.02) {
				// halve the last step four times: the first point behind what is seen, not a step past it
				float lo = last, hi = t;
				for (int j = 0; j < 4; j++) {
					float mid = (lo + hi) * 0.5;
					vec4 mc = PROJECTION_MATRIX * vec4(VERTEX + way * mid, 1.0);
					vec2 muv = mc.xy / mc.w * 0.5 + 0.5;
					vec4 ms = INV_PROJECTION_MATRIX * vec4(muv * 2.0 - 1.0, texture(glass_depth, muv).r, 1.0);
					if (ms.z / ms.w - (VERTEX.z + way.z * mid) > 0.02) {
						hi = mid;
						suv = muv;
						behind = ms.z / ms.w - (VERTEX.z + way.z * mid);
					} else {
						lo = mid;
					}
				}
				if (behind < 0.12 + hi * 0.04) {
					vec2 edge = smoothstep(vec2(0.0), vec2(0.08), suv) * smoothstep(vec2(0.0), vec2(0.08), 1.0 - suv);
					float k = mirror * edge.x * edge.y * (1.0 - smoothstep(4.0, 9.0, hi));
					float covered = min(ALPHA + k * (1.0 - ALPHA), 1.0);
					EMISSION = (EMISSION * ALPHA + texture(glass_screen, suv).rgb * k) / max(covered, 0.001);
					ALPHA = covered;
				}
				break;
			}
			last = t;
			t *= 1.25;
		}
	}
	if (self_k > 0.0) {
		float covered = min(ALPHA + self_k * (1.0 - ALPHA), 1.0);
		EMISSION = (EMISSION * ALPHA + self_seen.rgb * self_k) / max(covered, 0.001);
		ALPHA = covered;
	}
#endif
	anomaly_wrap = v_mode < 0.5 || v_mode > 2.5 && v_mode < 3.5 || v_mode > 4.5 && v_mode < 5.5 ? 0.5 : 0.0;
}
