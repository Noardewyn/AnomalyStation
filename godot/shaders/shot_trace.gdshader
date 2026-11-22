// D285: a shot's tracer (Render/ShotTrace.cs): a ribbon along the bullet's line, turned about that line to face the eye, at least
// MIN_PIXELS wide on the screen (so a far tracer does not thin to nothing), additive, a white-hot core in the shot's colour, brightest at the
// head and dimmer toward the tail. The mesh is a unit ribbon (x across, y from the tail to the head); the node's +y spans the tracer.
shader_type spatial;
render_mode blend_add, unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled, skip_vertex_transform;

const float MIN_PIXELS = 2.5;

instance uniform vec4 tint : source_color = vec4(1.0);
// How much of the tracer has dissolved, from the tail: 0 while its shot flies (the tail end is already clear, the head full), 1 when all of
// it is gone. The clear part grows smoothly up to the head, so the tracer fades from its tail instead of standing whole and then going out.
instance uniform float gone = 0.0;
const float SOFT = 0.6; // the part of the ribbon the fade from clear to full takes
instance uniform float width = 0.03; // metres (ShotLook.Trail)

void vertex() {
	vec3 axis = (MODELVIEW_MATRIX * vec4(0.0, VERTEX.y, 0.0, 1.0)).xyz;
	vec3 along = normalize((MODELVIEW_MATRIX * vec4(0.0, 1.0, 0.0, 0.0)).xyz);
	// the eye is at the view space's origin: the side is across both the line and the way to the eye
	vec3 side = cross(along, axis);
	float sl = length(side);
	side = sl > 1e-5 ? side / sl : vec3(1.0, 0.0, 0.0);
	float pixel = 2.0 / (PROJECTION_MATRIX[1][1] * VIEWPORT_SIZE.y);
	float wide = max(width, -axis.z * pixel * MIN_PIXELS);
	VERTEX = axis + side * VERTEX.x * wide;
	NORMAL = -normalize(axis);
}

void fragment() {
	float grey = dot(tint.rgb, vec3(0.299, 0.587, 0.114));
	vec3 rim = clamp(mix(vec3(grey), tint.rgb, 1.5), 0.0, 1.0);
	float across = abs(UV.x * 2.0 - 1.0);
	float core = 1.0 - smoothstep(0.0, 0.45, across);
	float edge = 1.0 - smoothstep(0.3, 1.0, across);
	float fade = smoothstep(0.0, 1.0, (UV.y - gone) / SOFT);
	vec3 colour = mix(rim, vec3(1.0), core * 0.8);
	ALBEDO = colour * (core * 1.5 + edge * 0.8) * fade;
	ALPHA = 1.0;
}
