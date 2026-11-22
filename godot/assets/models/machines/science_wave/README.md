# Science and security

11 base models. Each has a GLB and editable Blender file.

Use the GLB in Godot. Keep original texture sampling at nearest. Inspect named child meshes and anchors before wiring animations to gameplay; mutually exclusive state clips must not play together. Dynamic contents are deliberately separate.

Source atlas metadata: `source_states.json`. Source audit and specific limitations are recorded below. Atlas verification checks RGBA identity and glTF sampler settings; geometry checks sample exported animation poses against floor and ceiling. They do not certify gameplay completeness.

| Model | Triangles including hidden state meshes | Coverage |
|---|---:|---|
| [netpod.glb](netpod.glb) | 232 | Hollow capsule with rotating opaque door and separate session console. Source unpowered/open/occupied activity screen variants. Patient supplied at anchor, never baked into glass. |
| [quantum_server.glb](quantum_server.glb) | 60 | Source unloaded/ready/not-ready states qserver/on/off, independent service flap. Virtual domain loading and cooldown handled by gameplay. |
| [byteforge.glb](byteforge.glb) | 92 | Octagonal printing platform with raised side prongs and original grid UV. Product materialisation remains a runtime effect; maintenance hatch separate. |
| [destructive_scanner.glb](destructive_scanner.glb) | 192 | 32x64 source. Hollow tube telescopes into base during 1.2s source closing cycle; source tube_on is a collapsed state. Specimen stays separate; destruction/scan effects are gameplay. |
| [destructive_analyzer.glb](destructive_analyzer.glb) | 126 | Hollow analysis bay, retracting red shield and source processing display. Source loading item remains a runtime attachment. |
| [experimentor.glb](experimentor.glb) | 66 | Original 64x32 atlas and two-cell width, tall lab section and lower side output. Source work-loop window and service hatch; unpredictable gameplay experiments not baked into model. |
| [anomaly_refinery.glb](anomaly_refinery.glb) | 156 | Small console and raised core cradle, operational/service states. Core is an attachment, not a permanently embedded item. |
| [skillsoft_station.glb](skillsoft_station.glb) | 268 | Glass patient capsule with opening shell, side controller, chip socket and original wiring silhouette. Occupied state uses actual character, no source human painted on glass. |
| [ai_defense_turret.glb](ai_defense_turret.glb) | 212 | Separate rotating/retracting head, two sliding covers, off/stun/lethal/broken display states. Muzzle and mode panels follow the head. Default retracted pose; combat logic not implemented. |
| [beer_nuclear_device.glb](beer_nuclear_device.glb) | 156 | Original fictional game device atlas: base/safety-off/countdown/final appearance clips and independent service panel. Beer variant uses standard nuke artwork and a rear beverage tap anchor; no explosive core. |
| [station_selfdestruct_terminal.glb](station_selfdestruct_terminal.glb) | 72 | Original fictional game device atlas: base/safety-off/countdown/final appearance clips and independent service panel. Station terminal uses a distinct upright source silhouette. |
