# Wall-mounted flash

Reference: tgstation code/game/machinery/flasher.dm. Ordinary mounted flash only; the portable floor-standing flasher is excluded by the current asset preference.

## Variants

| File suffix (mounted_flash_) | Source state | Zero-based frame |
|---|---|---|
| ready | mflash1 | 0 |
| ready_alternate | mflash1 | 1 |
| flash_start | mflash_flash | 0 |
| flash_yellow | mflash_flash | 1 |
| flash_white | mflash_flash | 2 |
| off | mflash1-p | 0 |
| frame | mflash_frame | 0 |

Source ready delays [2,2], flash delays [1,1,3]; metadata retained, no animations. flash() uses flick to replace the sprite temporarily, not an additional painted overlay. Off applies to a burnt-out bulb or missing power. Source also shows mflash1-p when the bulb is absent; its pixels equal mflash_frame. Use frame geometry with the module absent in that case. Wallframe construction uses mflash_frame. Wirecutters remove the bulb; a wrench removes the frame only after the bulb is removed. These functions are documented, not integrated.

## Parts and texture slots

Continuous_Wall_Housing: shared closed stepped shell with lined bulb recess and closed rear. Replaceable_Flashbulb: separate closed stepped lamp, pivot at module insertion point (Blender 0,-0.02953125,0.0421875 m), removal along local -Y. Closed_Glass_Lens: child of the lamp, closed 0.0039375 m thick, original texture tint with alpha 0.22. Status_Indicator: separate strip; original frames retained. No invented hinge. Bare frame uses the original grey socket art.

Only res://assets/tg/icons/obj/wallmounts.png, not the different machines/wallmounts.png. Original atlas RGBA verified against DMI and embedded PNG bytes verified. All faces have atlas UVs; nearest sampling and flat shading. Material slots: Mounted_Flash_Original_Atlas (housing/off), Mounted_Flash_Lamp_Original (ready lamp/indicator emission), Mounted_Flash_Burst_Original (flash emission), Mounted_Flash_Glass_Original (alpha-blended closed lens). No derived textures. Identical meshes share datablocks in the variants library.

Width 0.28125 m, height 0.225 m, maximum depth 0.06609375 m. Scale 0.90/32 m per pixel. Rear mounting plane Y=0; Blender front -Y, Godot front +Z. Upper sprite border maps to the roof. Rotate the same asset for wall directions.

## Delivery and validation

76 triangles for empty frame, 176 for installed variants. All solid parts including glass are manifold. 8,680 opaque UV samples, no transparent hits or degenerate triangles, nearest samplers and embedded PNG hashes pass. Front/back/right/top/three-quarter views inspected. Batch checks in export_validation.json; global validation has unrelated pre-existing failures. Godot runtime not exercised. No animations, collisions or gameplay integration.
