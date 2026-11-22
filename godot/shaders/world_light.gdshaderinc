// D288: the lighting function of every lit world material (after light_grid.gdshaderinc). The lamps and flashlights are Godot lights,
// lit as plain Lambert diffuse with their shadows; the one directional light, the sun, reaches only where anomaly_sun_share lets it (set in
// fragment(): anomaly_sun_share = anomaly_sun_share_at(world_pos, world_normal, length(VERTEX))), so it never lights the inside of the station
// past the reach of its shadow.

varying float anomaly_sun_share;
#ifdef ANOMALY_WRAP_VARYING
varying float anomaly_wrap; // set in fragment(): 0.5 for a picture turned to the eye, 0 for a solid (prop_atlas, whose MultiMesh holds both)
#endif

void light() {
	float share = LIGHT_IS_DIRECTIONAL ? anomaly_sun_share : 1.0;
	// a flat picture turned to the eye stands for a whole body: a light behind it still lights it at half (ANOMALY_PICTURE_LIGHT, the billboards)
#ifdef ANOMALY_PICTURE_LIGHT
	float wrap = 0.5;
#else
#ifdef ANOMALY_WRAP_VARYING
	float wrap = anomaly_wrap;
#else
	float wrap = 0.0;
#endif
#endif
	float facing = wrap + (1.0 - wrap) * clamp(dot(NORMAL, LIGHT), 0.0, 1.0);
	DIFFUSE_LIGHT += facing * ATTENUATION * LIGHT_COLOR / PI * share;
}
