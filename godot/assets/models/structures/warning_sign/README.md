# Wall warning signs

24 original artwork variants from icons/obj/signs.png (512x512), 32x32 tiles. PNG RGBA verified against TG signs.dmi; original atlas embedded byte-for-byte in GLB. Nearest filtering, opaque materials, flat shading.

Source: code/game/objects/structures/signs/signs_warning.dm; mounting inherited from _signs.dm (find_and_mount_on_atom). Wall signs, not freestanding signboards. Static states, no overlays. Directional helpers reuse meshes by rotation. Secure area, docking and generic warnings share securearea artwork; vacuum and external airlock share space; radioactive area shares radiation. No duplicate aliases.

Scale 0.90/32 m per pixel; thickness 0.00984375 m. Blender front -Y; mounting plane Y=0; origin at tile centre. Closed plates follow pixel boundaries, including stepped triangles and circles. each has one object material slot Warning_Sign_<state>_Original and an atlas UV offset. Standalone GLBs carry their own geometry.

Front uses original artwork. Edges use actual boundary pixels. Rear uses an opaque border strip from the same tile: an interpretation because no reverse view exists in the source. No derived images. Highlights and fasteners remain source texture details.

All 24 exports pass exact PNG bytes, nearest filtering, triangle area and sampled opaque UV alpha checks. Physical meshes are manifold with flat shading. Preview inspected from front, oblique, rear and edge views. Godot import/runtime not tested. Repository-wide validator reports unrelated pre-existing failures; this family's results are in export_validation.json.

## Variants and MetaStation frequency

| State / filename suffix | Placements | Triangles |
|---|---:|---:|
| securearea | 57 | 156 |
| space | 36 | 44 |
| shock | 27 | 236 |
| bio | 11 | 44 |
| pods | 9 | 44 |
| radiation | 7 | 172 |
| nosmoking2 | 3 | 76 |
| cold | 3 | 44 |
| fire | 2 | 220 |
| testchamber | 2 | 44 |
| deathsposal | 1 | 44 |
| bodysposal | 1 | 44 |
| heat | 1 | 44 |
| gasmask | 1 | 44 |
| chemdiamond | 1 | 428 |
| nosmoking | 0 | 172 |
| yessmoking | 0 | 172 |
| xeno_warning | 0 | 44 |
| safety | 0 | 44 |
| explosives | 0 | 236 |
| explosives2 | 0 | 76 |
| firingrange | 0 | 44 |
| doors | 0 | 44 |
| radshelter | 0 | 44 |

162 map placements resolve to these variants. Type aliases and shared mesh groups: source_audit.json. Each variant includes GLB and editable BLEND. Shared library and preview scene are supplied.
