// A map's own sky of two colours (protocol 87, docs/ai/specs/map-editor.md 5.10): a planet's sky, one colour overhead and one at the horizon,
// the ground far below a darker shade of the horizon's, and the sun's disc with the glow about it, drawn here as the starfield draws its own
// (so it is there whatever lights the view has, and in the map editor's unshaded look).
shader_type sky;

uniform vec3 top_color : source_color = vec3(0.15, 0.32, 0.56);
uniform vec3 horizon_color : source_color = vec3(0.66, 0.77, 0.87);
uniform vec3 ground_color : source_color = vec3(0.3, 0.35, 0.4);
uniform vec3 sun_dir = vec3(0.0, 0.57, -0.82);
uniform float sun_size = 0.035; // its disc's radius, radians
uniform bool sun_on = true;
uniform vec3 sun_glow = vec3(1.0, 0.92, 0.78);
uniform vec3 sun_disc = vec3(4.0, 3.7, 3.2);

void sky() {
	vec3 dir = normalize(EYEDIR);
	vec3 col = dir.y >= 0.0 ? mix(horizon_color, top_color, pow(dir.y, 0.5)) : mix(horizon_color, ground_color, pow(-dir.y, 0.4));
	if (sun_on) {
		float a = acos(clamp(dot(dir, sun_dir), -1.0, 1.0));
		col += sun_glow * (exp(-a * 9.0) * 0.35 + exp(-a * 40.0) * 0.6);
		col = mix(col, sun_disc, smoothstep(sun_size, sun_size * 0.85, a));
	}
	COLOR = col;
}
