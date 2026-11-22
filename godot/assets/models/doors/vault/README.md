# Vault airlock

`vault_airlock.glb` is the closed door, two meshes `Vault_Left` and `Vault_Right`, 240 triangles. `vault_airlock.blend` is the editable model with the source atlas packed.

Size 2×2 m, both leaves 0.22 m thick, up to 0.31 m at the protruding central lock. The lock belongs to the right leaf, as in the opening frames. To open, move the left leaf along X by −1.5 m and the right by +1.5 m from the starting positions. The coordinates and the triangle count are in model_info.json.

The UVs use the first 32×32 tile of the original vault/vault.png. The PNG is unchanged and embedded in the GLB byte for byte, NEAREST filtering. At the back the texture is bound to the same physical points so the asymmetric seam matches. The leaves are closed, without smoothed normals. No animations, collisions or game code are added.

Generator: tools/art/build_vault_hatch.py. A preview of both door types is in the neighbouring folder, hatch/vault_hatch_preview.blend. The atlas use was checked in tgstation/code/game/machinery/doors/airlock.dm, the movement against the source opening frames.
