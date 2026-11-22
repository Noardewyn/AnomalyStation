// The draw order of the camera-facing pictures on one tile (Shared/World/DepthRank.cs): a picture's view-space position is scaled about the eye,
// which leaves its image on the screen as it was (the same pixels, the same perspective) and moves only its depth: to its tile's centre's (snap: a machine or
// another thing tg draws on its tile) or its own (a mob, which walks between tiles; an item, which lies where it was put, D329: pushed back to the
// centre's depth an item nearer the eye than the centre went into the floor), then rank metres nearer. So the things of
// one tile keep tg's layer order seen from every side and never fight. An orthographic view (the editor's top view) is not scaled (that would resize
// the picture) but moved along its axis, which leaves its image as it was too: an item that faces the eye lies in the floor's plane there, and sank
// into the floor unmoved.
mat4 depth_ordered(mat4 modelview, mat4 view, mat4 projection, vec3 origin, bool snap, float rank) {
	float own = -(view * vec4(origin, 1.0)).z;
	// TileSpace: a tile is 2 m, its centre at odd metres on X and Z
	vec3 anchor = snap ? vec3(floor(origin.x * 0.5) * 2.0 + 1.0, origin.y, floor(origin.z * 0.5) * 2.0 + 1.0) : origin;
	if (projection[3][3] != 0.0) {
		float nearer = own - (-(view * vec4(anchor, 1.0)).z - rank);
		return mat4(vec4(1.0, 0.0, 0.0, 0.0), vec4(0.0, 1.0, 0.0, 0.0), vec4(0.0, 0.0, 1.0, 0.0), vec4(0.0, 0.0, nearer, 1.0)) * modelview;
	}
	// close to the eye (its own tile's centre may be behind it) a picture is not moved nearer than it is, nor in front of the near plane
	float target = max(-(view * vec4(anchor, 1.0)).z - rank, min(own, 0.5));
	if (own < 0.01) {
		return modelview;
	}
	float k = target / own;
	return mat4(vec4(k, 0.0, 0.0, 0.0), vec4(0.0, k, 0.0, 0.0), vec4(0.0, 0.0, k, 0.0), vec4(0.0, 0.0, 0.0, 1.0)) * modelview;
}
