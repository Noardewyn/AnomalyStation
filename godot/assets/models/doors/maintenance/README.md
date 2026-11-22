# Maintenance airlock

Two independent sliding mesh objects, `Door_Left` and `Door_Right`, derived from
the top-left 32 x 32 tile of the project's tg `maintenance.png` atlas.

- `maintenance_airlock.blend`: editable Blender asset scene, packed original atlas.
- `maintenance_airlock.glb`: two mesh nodes, embedded texture, ready for import.
- Closed dimensions: 2 m wide, 2 m tall, up to 0.20 m deep at the indicator panel.
- 560 triangles total: left 252, right 308. Flat normals; no subdivision.
- The shallow perimeter step is present on the front and back of both leaves;
  the body is 0.16 m thick and the outer rim is 0.136 m thick. The indicator panel
  retains its small raised profile.
- Front and back map to the same physical sprite pixels. The reverse side mirrors
  the front, retaining aligned windows and the indicator panel on the same leaf.
- Transparent sprite regions become volumetric translucent inserts, opacity 0.35,
  centered at Blender Y=0 with surfaces at Y=-0.0266667 and +0.0266667 m. The
  front recess, glass thickness, and back recess are equal: each is 0.16/3 m
  (about 5.33 cm). Overall depth is 20% below the earlier version. The inserts
  are part of the leaf meshes, not separate objects.
- Each glass insert is a closed volume with side faces connecting its front and
  back contours. The metal window tunnel also has a continuous opaque lining,
  so looking through the glass cannot expose the hollow inside of the leaf.
- The stepped seam follows the sprite; both leaves slide along X. Closed object
  positions are X=-0.5 and X=0.5. Add offsets -1.125 and +1.125 respectively for
  full clearance. No animation or game logic is included.
- Both meeting edges reuse the exact dark-steel material of the exterior sides,
  with the existing yellow edge material continuing across the full thickness.
  The source atlas is unchanged; the GLB embeds the byte-identical original PNG.
  No additional texture is used. Stripe boundaries add 32 triangles in total.
- Blender: Z up, front -Y. GLB: Y up, front +Z (standard Blender glTF conversion).
- Atlas filtering: Closest in Blender; NEAREST for both minification and
  magnification in GLB. No mipmaps in the GLB sampler. Preserve Nearest filtering
  when replacing materials in Godot.

Rebuild with `tools/art/build_maintenance_airlock.py` in Blender's Python context.
The script preserves the existing unrelated scene and replaces only its own asset
scene. `tools/art/preview_maintenance_airlock.py` renders the separate preview at
`docs/ai/art/maintenance_airlock_preview.png` without adding staging to the asset.
`tools/art/maintenance_seam.py` supplies the reusable meeting-edge finish.
`tools/art/close_maintenance_glazing.py` closes the glass and metal window tunnels.
`tools/art/optimize_maintenance_airlock.py` retains optional thin-glass and flat-rim
experiments; neither is enabled in the current build.

Validation: closed manifold meshes, equal recess/glass/recess thirds, retained rim
relief, two exported GLB mesh nodes, 560 exported triangles,
NEAREST samplers and BLEND insert material. Closed, reverse, and separated poses
were visually inspected in Blender.

## In the game

The catalog entry `station_airlock` in `godot/assets/models/models.json` draws every door whose icon is in
`icons/obj/doors/airlocks/station/` with this model (all 22 share the closed silhouette; the freezer has no
windows); see docs/ai/specs/models.md. Other doors keep the box panels.

- Texture `sprite`: the model's own uv and flat-colour materials are not used; every face takes the pixel of the
  door's composed picture that it stands on in the front view, so tg's overlays (`fill_closed` or `glass_closed`,
  bolt and emergency lights, panel, weld, notes) land on the model as on the sprite, and side faces continue the
  sprite's edge colours.
- The glass insert faces (the BLEND material) are drawn with an ordered stipple by the picture's alpha.
- Open, the leaves do not use `open_offset_m` (1.125 m, fully into the walls): each stops with one sprite pixel of
  its seam edge in the doorway (three where the seam steps out): the catalog's `slide` is -0.9375 m and 0.8125 m.
- The client reads the `.glb` itself (the folder is `.gdignore`d); rebuilding the model needs no code change while
  the node names, the 2 x 2 m frame and the 1 px = 1/16 m scale stay the same.
