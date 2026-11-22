// BYOND's "outline" filter on a thing a 3D model draws (docs/ai/specs/models.md, "Filters"): the supermatter's outline as it heads for a singularity
// (common_delams.dm:37, size 1 in SUPERMATTER_SINGULARITY_LIGHT_COLOUR, a dark purple). An inverted hull: the crystal's mesh with smooth normals
// (ModelLibrary.HullAt: the flat faces' split corners joined, so the hull stays closed) pushed out along them by the line's width and drawn from its back faces
// only, so only a rim round the silhouette shows; lit up (brightness), so tg's dark colour still reads in a lit room. It is drawn in the transparent pass before
// the crystal (render priority -1): the crystal reads the opaque pass behind it and would show the hull through every facet.
shader_type spatial;
render_mode blend_mix, unshaded, cull_front, depth_draw_always, shadows_disabled, fog_disabled;

uniform vec4 colour : source_color = vec4(0.0, 0.0, 0.0, 1.0);
uniform float width = 0.0625; // metres
uniform float brightness = 3.0;

void vertex() {
	VERTEX += NORMAL * width;
}

void fragment() {
	ALBEDO = colour.rgb * brightness;
	ALPHA = 1.0; // the transparent pass (see above)
}
