# Crates from crates.png

16 base GLB models and 72 looks that share geometry within each type. All files are in this folder. The source `res://assets/tg/icons/obj/storage/crates.png` is embedded unchanged, the UVs address the source 544×576 atlas, nearest filtering.

`crates_library.blend` holds scenes of the separate models, `Crates_All_Variants` with every look in the closed/open state, and `Crates_Preview` with the base shapes. The open positions are for demonstration; the GLBs have no animation clips or collisions.

| GLB | Type | Triangles |
|---|---|---:|
| metal_crate.glb | Plain metal crate and its colour/department variants | 72 |
| ribbed_crate.glb | Crate with ribs on the lid, weapons/plasma | 108 |
| wooden_crate.glb | Wooden crate with straw at the bottom | 72 |
| freezer_crate.glb | Freezer crate | 96 |
| mining_cart.glb | Mining cart with a tapered body and wheels | 184 |
| trash_cart.glb | Trash cart with a two-section lid | 268 |
| laundry_cart.glb | Laundry cart | 184 |
| large_wooden_crate.glb | Tall wooden shipping crate | 132 |
| animal_crate.glb | Animal carrier with a front door and an oxygen tank | 108 |
| necropolis_chest.glb | Necropolis chest | 96 |
| coffin.glb | Coffin with a shaped body | 88 |
| treasure_chest.glb | Chest with metal bands | 108 |
| mail_crate.glb | Mail container with a removable lid | 72 |
| cardboard_crate.glb | Cardboard box with four flaps | 108 |
| ancient_sarcophagus.glb | Ancient sarcophagus with a removable lid | 72 |
| trash_bin.glb | Octagonal bin with a removable lid | 88 |

## Matching the game

Checked: `tgstation/code/game/objects/structures/crates_lockers/crates.dm`, the subtypes in `crates/`, `code/game/objects/items/mail.dm`, `code/modules/mining/mine_items.dm` and `code/modules/mining/lavaland/necropolis_chests.dm`. The translated logic of the main pair of states is also in `src/Core/Tg/game/objects/structures/crates_lockers/crates.cs`.

- Plain crate: `base_icon_state` is closed, `base_icon_state + "open"` is open. The inner side of the lid and the visible insides come from the open sprite.
- Carrier: the body is `crittercrate`, the door is a separate `crittercrate_door`; the door's open 2D sprite is replaced by rotating the mesh.
- Boxes: the body keeps its state, `cardboardopen` gives the opened flaps. Three looks share one geometry.
- Mail: `mailsealed` is closed, `mailopen` is open and empty, `mail` holds letters. The letters are not baked into the empty body; the lid is separate.
- In the game the large wooden crate is destroyed when pried open with a crowbar. Its detachable top suits showing the crate being taken apart; it is not a regular hinged lid.
- The trash cart's gallery repeats the sprite with the left section open. Both lid meshes are separate.
- Locks, indicators, welding, the shipping label and the contents are separate overlays/game objects, not permanent parts of the body. Graves, keys and effects were not modelled as crates.

`variants.json` maps looks to models, with the source states and where the DM code assigns them explicitly. The `atlas_only` field means no explicit assignment was found in the crate types; it does not prove there is no dynamic use. The coffin and the animated sarcophagus use the first frame/direction of the source state.

## Use in Godot

The body and the lid/door are separate meshes. The root is at floor level. A plain crate is 1.0×0.78×0.62 m. The GLB is exported closed. Meshes can be shared between instances; only materials change.

The `Crate_exterior_*` and `Crate_interior_*` materials use different regions of one atlas. `godot_uv_offsets` in variants.json gives the StandardMaterial3D `uv1_offset` values relative to the base GLB. Keep scale=1 and nearest. Duplicate/cache materials per look and assign them per surface; do not cover the whole object with a single material. The metadata already accounts for Godot's V direction.

Moving meshes have the properties `motion`, `open_axis` and `open_degrees`. The axes are in Blender terms: X → Godot X, Y → −Z, Z → Y. `hinge` means rotation, `remove`/`lift` a removable lid. These properties describe how the model is prepared; no game animation code is added.

Depth and hidden sides are reconstructed from the source sprites. Checked: closed components, mesh/triangle counts, the source PNG inside each GLB and nearest filters. All looks were viewed in Blender; the base shapes are rendered in `docs/ai/art/crates_preview.png`. No game scenario was tested in Godot.

Generator: `tools/art/build_crates.py`, run in Blender over MCP. It creates its own scenes and does not change the door, table and locker models.
