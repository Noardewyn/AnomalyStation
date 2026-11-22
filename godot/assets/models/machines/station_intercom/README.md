# Station intercom

Source: `tgstation/code/game/objects/items/devices/radio/intercom.dm` and parent `radio.dm`. Original atlas: `res://assets/tg/icons/obj/machines/wallmounts.png` (640 x 608; 32 x 32 tiles). Atlas RGBA verified identical to reference DMI. No derived material textures.

## Presets

| File stem | Base state | Separate indicators | Use |
|---|---|---|---|
| station_intercom_listening | intercom | intercom_s | Default station intercom |
| station_intercom_broadcasting | intercom | intercom_m | Broadcast only |
| station_intercom_two_way | intercom | intercom_s, intercom_m | Receive and broadcast |
| station_intercom_quiet | intercom | None | Both modes disabled; also reuse for unscrewed and wallframe appearances |
| station_intercom_off | intercom-p | None | Power off / EMP |

`update_overlays()` omits indicators when unscrewed. `set_on(FALSE)` disables listening and broadcasting; `update_icon_state()` chooses intercom-p. The wallframe uses intercom, not the unused intercom_assembly state. Do not duplicate this mesh for chapel, AI-private or free-range types with the same appearance. Command, prison, syndicate and departmental skins are outside this ordinary station-intercom request; their atlas metadata is retained for reference. No animations, collisions or gameplay integration.

## Geometry and materials

Continuous closed stepped housing with an actual one-pixel display recess; separate closed display and indicator parts. Upper sprite perspective rows 9-11 map to the roof. Rear and side faces use original housing/grille pixels, without front status lights on the back. One material slot: Intercom_Original_Atlas, opaque, nearest-neighbor, flat shading. Indicators are separate meshes with original overlay UVs. No invented hinge: the source item unscrews and detaches as a whole.

Dimensions: 0.5625 m width, 0.28125 m height; body depth 0.084375 m, indicator depth extends to 0.08690625 m. Scale 0.90/32 m per pixel. Origin at rear mounting plane, vertically centered; Blender front -Y, glTF/Godot front +Z. Rotate the same asset for each wall direction.

## Delivery and validation

78-102 triangles per preset. All five GLBs pass embedded-original-PNG hash, nearest sampler, UV transparency and degenerate triangle checks (4,380 opaque UV samples). Solid mesh boundaries checked manifold and flat. Visually reviewed front, back, right, top and three-quarter views. Godot runtime was not exercised. Global validation also reports unrelated pre-existing failures; export_validation.json contains this batch only.
