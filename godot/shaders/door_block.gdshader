// D288 (the owner's play tests, 2026-10-29): a shut door blocks the lamps' light, as its opaque turf does in tg, whatever seams and thinness its
// model has. A box over the door's cell drawn only into the shadows (LightLayer.PlaceDoorBlocks), its local z across the door's plane. From each
// light (the shadow pass's eye: a lamp's cube face or paraboloid, a flashlight's spot) it keeps the half beyond the plane, its face toward the
// light by the most that light's shadow bias allows (a lamp's LightLayer.LampShadowBias, a flashlight's BeamBlockFace, each its own instance):
// the light's side of the door stays lit, and everything past the plane is that bias or more behind the face, in the shadow.
shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled;

uniform float toward_light = 0.1; // metres, set per instance (LightLayer.NewDoorBlocks)

void vertex() {
	vec3 across = MODEL_MATRIX[2].xyz;
	float side = dot(INV_VIEW_MATRIX[3].xyz - MODEL_MATRIX[3].xyz, across) >= 0.0 ? 1.0 : -1.0;
	if (VERTEX.z * side > 0.0) {
		VERTEX.z = side * toward_light / length(across);
	}
}
