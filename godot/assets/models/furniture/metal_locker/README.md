# Metal lockers — original atlas library

All models use the **unmodified** `godot/assets/tg/icons/obj/storage/closet.png` (640×624). UVs address the original 32×48 sprite cells directly. The exact source PNG is embedded in every GLB, with nearest filtering and no mipmap minification. Repacked/recolored 64×64 textures are obsolete.

## Source appearance rules

Read-only reference: `../tgstation/code/game/objects/structures/crates_lockers/closets.dm`, `closet_update_overlays` and `animate_door`; corresponding translated logic is in `src/Core/Tg/game/objects/structures/crates_lockers/closets.cs`.

- Body and visible interior: `icon_state`.
- Closed door front: `(icon_door or icon_state) + "_door"`.
- Door reverse during animation past 90 degrees: `icon_state + "_back"`, **not** `icon_door + "_back"` and not the cabinet's exterior back.
- Static open-door overlay: `icon_state + "_open"`; its skewed 2D pose is replaced by the actual 3D door rotation.

For example, the blue wardrobe uses body `generic`, front `blue_door`, reverse `generic_back`. Bio variants use body `bio` and reverse `bio_back`. Laser-tag wardrobes use `rack` with red/blue fronts. Medical storage uses the `med` shelved interior, while many secure medical lockers use the hanging interior with a different body state. All resolved default pairs and source type paths are in `variants.json` and `source_type_audit.json`; arbitrary runtime painting can use the same state rules.

## Shared geometry

| GLB | Interior | Total triangles |
|---|---|---:|
| `locker_wardrobe.glb` | Upper shelf, hanging compartment with source hook artwork, floor tray | 116 |
| `locker_shelves.glb` | Two shelves plus floor tray | 130 |
| `locker_rack.glb` | Upper shelf, three raised rack supports and crossbar | 164 |
| `locker_freezer.glb` | Rounded profile and two slatted trays with real gaps | 316 |

Each has `Metal_Locker` root and separate `Locker_Body` / `Locker_Door` meshes. Dimensions: 0.90 × 0.60 × 1.80 m; floor-centered root. The left-hinge door is exported closed. In Godot, rotate its local Y from 0 to −140 degrees (freezer −123), matching the source animation angles. No collision or baked animation is included.

`metal_locker_atlas_library.blend` contains the four geometry scenes, all 75 appearance combinations and a comparison scene. All 150 displayed mesh objects share only eight mesh datablocks (body and door for each family). The library also includes atlas-only combinations, explicitly marked `atlas_only`; they are not claimed to be map defaults. Additional cabinets, the miniature refrigerator and boxes are provided in this same folder; see `REMAINING_MODELS.md` for file names and texture mappings.

## Changing appearance in Godot

Keep both mesh resources shared. The atlas texture stays the same. Switch the body, front and reverse **material UV offsets** using `godot_uv_offsets` from `variants.json`; these offsets are relative to each family GLB's base states. `rects` provides original source rectangles.

There are three material roles: `body`, `front`, `inside`. Body geometry uses the body role; the door has separate front and inside surfaces. Identify roles from the imported material names `Atlas_body_*`, `Atlas_front_*`, `Atlas_inside_*`, not assumed surface indices. Duplicate/cache materials per appearance and assign surface overrides; do not change shared imported materials in place. Use StandardMaterial3D `uv1_offset`, scale 1, and nearest filtering. Blender uses bottom-up V; the JSON offsets already use Godot's top-down UV convention.

Do not apply one whole-object material override to the door: its front and reverse must remain different atlas regions. No runtime game factory has been installed; this package supplies shared geometry, the fully assembled Blender appearance library and machine-readable source mappings.

## Reconstruction and source limitations

Actual shelf placement, rack rails and freezer slots follow their respective body sprites; depth and hidden outer surfaces are reconstructed because the source is a front/upper projection. Hooks remain pixel artwork to keep geometry small. There is no invented uniform recoloring of the full cabinet.

The reference requests `syndicate_chemical_back`, but this atlas lacks that state. Only this reverse uses the explicitly recorded fallback `syndicate_back` (`back_fallback: true`). Static open reference `syndicate_chemical_open` exists. This exception is visible in the manifest rather than silently treated as source behavior.

Build: `tools/art/build_atlas_lockers.py` (old `build_metal_locker.py` delegates here). Preview: `tools/art/preview_metal_locker.py`. `validation.json` records embedded PNG byte equality, nearest samplers, exported mesh counts and shared Blender geometry. Front, open, interior and perspective examples were visually checked; Godot runtime integration has not been exercised.


## In the game

The catalog entries `locker_wardrobe`, `locker_shelves`, `locker_rack` and `locker_freezer` in `godot/assets/models/models.json`
(rule `closet`, texture `cell`, docs/ai/specs/models.md) draw tg's closets of `icons/obj/storage/closet.dmi` by their icon state. The
door's front and back are separate parts (`material` `Atlas_front_` and `Atlas_inside_`), turned on the hinge of `Locker_Door`
(`open_degrees`, tg's 0.2 s). The material UV offsets of `variants.json` are not used: each part is mapped over its icon frame
and given the picture of the state tg's rules name. Things on the closet's tile rest on the shelves found in the body. The
remaining storage models are entries of the same rule (`cabinet_wood`, `gun_case`, `locker_abductor`, `mini_fridge`, `locker_cursed`,
`locker_old`, `box_cardboard`, `box_metal`): each door or flap is its own part on its own hinge (Blender's `open_axis` and `open_degrees`).

