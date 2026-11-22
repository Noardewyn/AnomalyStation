# Stasis capsule v3

Blender MCP asset, +Y points toward the head, Z is up. One Blender unit is one meter. Patient is not included.

- stasis_capsule_v3.blend: editable asset and review studio. Run the included Stasis_State_Controller text with STATE set to normal, powered, maintenance, or broken.
- stasis_v3_normal.glb: intact, unpowered.
- stasis_v3_maintenance.glb: open service hatches with actual openings and colored wiring.
- stasis_v3_broken.glb: bent rails, damaged mattress, crooked open hatches, no mattress emission. Damage morph target defaults are enabled in this export.

Mesh topology counts: 592 vertices / 1052 triangles for normal, powered and maintenance; 616 vertices / 1070 triangles for broken. GPU vertex counts after glTF normal/material splitting may be higher. All variants preserve the same root and patient anchors.

The powered and broken custom properties drive mattress emission in Blender; the broken property also drives damage shape keys. Use the controller to update the service hatches and tear visibility together. Blender drivers are not game runtime scripts. GLBs are separate visual state variants; game-side state selection is not integrated yet.

Maintenance represents the original stasis_maintenance state, not a pile of fully dismantled construction parts. Broken represents stasis_broken, not a deleted machine or rubble pile.

Rear side rails, fascia, and deck now extend to the rear pedestals. The housing overlaps pedestal tops internally while exposed planes remain offset to avoid z-fighting. The front rim is one watertight ring.

Preview: stasis_v3_states.png. Joint close-up: stasis_v3_joint.png.

Build scripts: `tools/art/stasis/` (run through Blender MCP; they write into this folder).

Order: build_stasis (v1), revise_stasis_v2, upgrade_stasis_v3, fix_rear_joint_v3. In the game the catalog draws normal, maintenance and broken by tg's icon state (entry `stasis`); the powered variant waits for the mattress glow (a vis_contents overlay).
