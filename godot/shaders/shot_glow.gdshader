// D176: the client's own 3D shots (Render/ShotVisual.cs): a glowing body added to the view, its core white where it faces the eye and its rim
// in the shot's colour (ShotLook.Colour). One material for every shot: the colour, the strength and the look are per instance.
// look 0: a bolt or a streak (a stretched capsule); 1: the electrode's spark (its vertices jitter); 2: the ion bolt (it pulses and crackles);
// 3: a plain glowing ball (the temperature bolt); 4: the soft halo around a body (a larger copy of its mesh, brightest where it faces the eye).
shader_type spatial;
render_mode blend_add, unshaded, cull_back, depth_draw_never, shadows_disabled, fog_disabled;

instance uniform vec4 tint : source_color = vec4(1.0);
instance uniform float strength = 1.0;
instance uniform float look = 0.0;

varying vec3 v_local;

float hash13(vec3 p) {
	return fract(sin(dot(p, vec3(12.9898, 78.233, 37.719))) * 43758.5453);
}

void vertex() {
	v_local = VERTEX;
	if (look > 0.5 && look < 1.5) {
		// the spark: each vertex out and in by a hash of its place and the frame (30 a second)
		float frame = floor(TIME * 30.0);
		VERTEX *= 0.6 + 0.8 * hash13(floor(VERTEX * 8.0) + frame);
	} else if (look > 1.5 && look < 2.5) {
		VERTEX *= 1.0 + 0.15 * sin(TIME * 25.0);
	}
}

void fragment() {
	float facing = abs(dot(normalize(NORMAL), normalize(VIEW)));
	// tg's colours are often pale (a laser's light is COLOR_SOFT_RED): the rim takes the hue a little more saturated
	float grey = dot(tint.rgb, vec3(0.299, 0.587, 0.114));
	vec3 rim = clamp(mix(vec3(grey), tint.rgb, 1.5), 0.0, 1.0);
	// (no early return: the shader's code is inlined into Godot's own fragment function, which writes the outputs after it)
	float core = smoothstep(0.7, 1.0, facing) * 0.9;
	vec3 colour = mix(rim, vec3(1.0), core);
	float glow = mix(0.6, 1.0, facing) * 1.6;
	if (look > 1.5 && look < 2.5) {
		// the ion bolt's crackle: cells that flash on and off
		float cell = hash13(floor(v_local * 40.0) + floor(TIME * 24.0));
		glow *= 0.5 + step(0.55, cell);
	}
	if (look > 3.5 && look < 4.5) {
		colour = rim;
		glow = pow(facing, 3.0) * 0.45;
	}
	ALBEDO = colour * glow * strength;
	ALPHA = 1.0;
}
