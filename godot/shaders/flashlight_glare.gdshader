// D288 (the owner, 2026-10-29): another holder's flashlight seen from in front: its lit lens, faint from the side, and inside its beam a glow
// that grows and brightens the nearer the eye is to the beam's axis (the bloom spreads it), so one sees where a flashlight shines and is a
// little dazzled by one aimed at him. A quad turned to the eye, the child of the beam's spot (whose -Z is the way it shines), added to the view.
shader_type spatial;
render_mode blend_add, unshaded, cull_disabled, depth_draw_never, shadows_disabled, fog_disabled, skip_vertex_transform;

instance uniform vec4 tint : source_color = vec4(1.0);
instance uniform float strength = 1.0;
uniform float cone_cos = 0.848; // the cosine of the beam's half angle (FlashlightBeam.Angle, 32 degrees)
uniform float lens_metres = 0.25; // the quad's size seen from the side
uniform float dazzle_metres = 1.6; // and on the beam's axis
uniform float toward_eye = 0.25; // metres: drawn this much nearer the eye, so the holder's picture does not cut it

varying vec2 v_p;
varying float v_facing;

void vertex() {
	v_p = VERTEX.xy * 2.0; // -1 .. 1
	vec3 origin = MODEL_MATRIX[3].xyz;
	vec3 beam = normalize(-MODEL_MATRIX[2].xyz);
	vec3 to_eye = CAMERA_POSITION_WORLD - origin;
	vec3 eye_dir = to_eye / max(length(to_eye), 0.001);
	// 0 at the beam's edge and outside it, 1 on its axis
	v_facing = smoothstep(cone_cos - 0.05, 1.0, dot(beam, eye_dir));
	float size = mix(lens_metres, dazzle_metres, v_facing);
	vec3 world = origin + eye_dir * toward_eye + (INV_VIEW_MATRIX[0].xyz * VERTEX.x + INV_VIEW_MATRIX[1].xyz * VERTEX.y) * size;
	VERTEX = (VIEW_MATRIX * vec4(world, 1.0)).xyz;
}

void fragment() {
	float r2 = dot(v_p, v_p);
	float lens = exp(-r2 * 30.0);
	float halo = exp(-r2 * 5.0) * v_facing;
	float edge = 1.0 - smoothstep(0.6, 1.0, sqrt(r2)); // nothing at the quad's edge, which would show as a square
	float a = (lens * (0.5 + 2.5 * v_facing) + halo * 0.6) * edge * strength;
	ALBEDO = mix(tint.rgb, vec3(1.0), clamp(lens * 1.5, 0.0, 1.0)) * a;
	ALPHA = 1.0;
}
