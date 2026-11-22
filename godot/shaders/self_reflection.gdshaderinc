// The own body's reflection in first person (the owner, 2026-11-18): the body is not drawn then (D288), so neither the screen-space
// reflections nor the panes' depth walk can show it. EntityLayer.PublishSelfReflection gives its picture and where it stands, and a reflecting
// surface traces its mirror ray against that picture, a card standing at the body and turned to the reflecting point (as a billboard turns to
// its eye). Needs light_grid.gdshaderinc. shortcut: nothing between the surface and the body hides it; a depth test would need the body's view.
// Its four sides (BYOND dirs: south its front, east its right, north its back, west its left) and its facing (radians clockwise from north):
// each reflecting point shows the side the body turns to it (BillboardMath.ChooseDir), so a pane beside the body shows its side.
global uniform sampler2D anomaly_self_south;
global uniform sampler2D anomaly_self_east;
global uniform sampler2D anomaly_self_north;
global uniform sampler2D anomaly_self_west;
global uniform float anomaly_self_yaw;
global uniform float anomaly_self_sway; // the walking tilt about the feet (Billboard.Sway; its lift is in anomaly_self_at)
global uniform vec4 anomaly_self_at; // the body's node (its feet) in the world; w: 1 while it is published
global uniform vec4 anomaly_self_rect; // the picture around the feet in metres: left, right, bottom, top (Billboard.Rect)

// What the mirror ray from world point o along world direction d sees of the own body: its lit colour, a = 1 where it hits a drawn pixel.
vec4 anomaly_self_reflection(vec3 o, vec3 d) {
	if (anomaly_self_at.w < 0.5) {
		return vec4(0.0);
	}
	vec3 at = anomaly_self_at.xyz;
	vec2 away = o.xz - at.xz;
	float far = length(away);
	if (far < 0.05 || far > 8.0 || abs(o.y - at.y) > 3.0) {
		return vec4(0.0);
	}
	vec3 n = vec3(away.x, 0.0, away.y) / far;
	float toward = dot(d, n);
	if (toward > -0.01) {
		return vec4(0.0);
	}
	vec3 hit = o + d * (dot(at - o, n) / toward);
	vec3 right = vec3(n.z, 0.0, -n.x); // the right of an eye at o looking at the body (its forward is -n)
	// the card point back in the picture: the billboard turns its picture by the sway about the feet (picture_billboard.gdshader)
	vec2 card = vec2(dot(hit - at, right), hit.y - at.y);
	float cs = cos(anomaly_self_sway), sn = sin(anomaly_self_sway);
	float x = cs * card.x + sn * card.y, y = -sn * card.x + cs * card.y;
	vec4 r = anomaly_self_rect;
	if (x < r.x || x > r.y || y < r.z || y > r.w) {
		return vec4(0.0);
	}
	vec2 uv = vec2((x - r.x) / (r.y - r.x), (r.w - y) / (r.w - r.z));
	// the bearing from the body to the reflecting point (compass: north is -z, east +x) against its facing, in quarter turns
	float relative = atan(n.x, -n.z) - anomaly_self_yaw;
	int side = int(floor(mod(relative + 0.25 * PI, TAU) / (0.5 * PI))) % 4;
	vec4 c;
	if (side == 0) {
		ivec2 size = textureSize(anomaly_self_south, 0);
		c = texelFetch(anomaly_self_south, clamp(ivec2(uv * vec2(size)), ivec2(0), size - 1), 0);
	} else if (side == 1) {
		ivec2 size = textureSize(anomaly_self_east, 0);
		c = texelFetch(anomaly_self_east, clamp(ivec2(uv * vec2(size)), ivec2(0), size - 1), 0);
	} else if (side == 2) {
		ivec2 size = textureSize(anomaly_self_north, 0);
		c = texelFetch(anomaly_self_north, clamp(ivec2(uv * vec2(size)), ivec2(0), size - 1), 0);
	} else {
		ivec2 size = textureSize(anomaly_self_west, 0);
		c = texelFetch(anomaly_self_west, clamp(ivec2(uv * vec2(size)), ivec2(0), size - 1), 0);
	}
	if (c.a < 0.5) {
		return vec4(0.0);
	}
	// the global sampler has no source_color hint: its sRGB texels are made linear here
	vec3 linear = mix(c.rgb / 12.92, pow((c.rgb + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), c.rgb));
	return vec4(linear * anomaly_grid_light(at + vec3(0.0, 0.3, 0.0)), 1.0);
}
