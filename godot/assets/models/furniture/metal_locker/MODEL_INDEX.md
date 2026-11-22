# Base locker and box models

One GLB per geometry, with no duplicate colour variants and no demo scene objects. Each file holds one closed object with the base texture from the original atlas. Doors, lids and inner shelves are kept; moving parts have separate meshes and pivot points. No animation clips are recorded.

| File | Type |
|---|---|
| `locker_wardrobe.glb` | Metal wardrobe locker, top shelf |
| `locker_shelves.glb` | Metal locker with shelves |
| `locker_rack.glb` | Metal locker with racks |
| `locker_freezer.glb` | Tall freezer locker |
| `wood_cabinet.glb` | Wooden two-door cabinet |
| `gun_cabinet.glb` | Gun cabinet with mesh doors |
| `abductor_locker.glb` | Alien locker |
| `mini_fridge.glb` | Mini fridge |
| `magic_locker.glb` | Green special locker |
| `old_locker.glb` | Old locker |
| `cardboard_box.glb` | Cardboard box with four flaps |
| `metal_box.glb` | Metal box with two lids |

The texture is embedded in each GLB, nearest filtering. Looks are changed through material UV offsets, listed in `variants.json` and `remaining_variants.json`. All base models are directly in this folder, with no subfolders and no duplicate colour variants.
