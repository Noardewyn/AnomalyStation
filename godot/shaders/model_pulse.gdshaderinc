// A model part's pulse (docs/ai/specs/models.md, "shader"): the frames of the tg icon state it plays, looped: each frame's length in seconds (tg's delay),
// its strength (a crystal's glow, a field's alpha) and its colour factor. ModelLibrary sets them from the catalog's "pulse"; none: steady (1, white).
uniform int pulse_count = 0;
uniform float pulse_seconds[8];
uniform float pulse_strength[8];
uniform vec3 pulse_scale[8];

// The frame showing at time t: tg steps from frame to frame, it does not blend them.
void anomaly_pulse(float t, out float strength, out vec3 scale) {
	strength = 1.0;
	scale = vec3(1.0);
	if (pulse_count <= 0) {
		return;
	}
	float total = 0.0;
	for (int i = 0; i < 8; i++) {
		if (i < pulse_count) {
			total += pulse_seconds[i];
		}
	}
	float at = mod(t, max(total, 0.001));
	for (int i = 0; i < 8; i++) {
		if (i >= pulse_count) {
			break;
		}
		strength = pulse_strength[i];
		scale = pulse_scale[i];
		if (at < pulse_seconds[i]) {
			break;
		}
		at -= pulse_seconds[i];
	}
}

// A stable number in 0..1 per face of a model: from its flat normal in the model's own space, so it does not change as the eye moves.
float anomaly_facet(vec3 local) {
	vec3 n = normalize(cross(dFdx(local), dFdy(local)));
	return fract(sin(dot(round(n * 40.0), vec3(12.9898, 78.233, 37.719))) * 43758.5453);
}
