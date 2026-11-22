# Resource processing

7 base models. Each has a GLB and editable Blender file.

Use the GLB in Godot. Keep original texture sampling at nearest. Inspect named child meshes and anchors before wiring animations to gameplay; mutually exclusive state clips must not play together. Dynamic contents are deliberately separate.

Source atlas metadata: `source_states.json`. Source audit and specific limitations are recorded below. Atlas verification checks RGBA identity and glTF sampler settings; geometry checks sample exported animation poses against floor and ceiling. They do not certify gameplay completeness.

| Model | Triangles including hidden state meshes | Coverage |
|---|---:|---|
| [ore_redemption.glb](ore_redemption.glb) | 120 | Original powered/off/panel-open console. Hinged upper maintenance cover and stepped output ramp. Source default input north/output south; anchors are adjacent tile centers, directions configurable in game. |
| [mineral_stacker_refinery.glb](mineral_stacker_refinery.glb) | 60 | Shared base geometry and original stacker atlas for stacking machine and boulder refinery. Source uses same appearance. Stacker input east/output west; refinery directions follow machine rotation. Off state also covers open panel, unanchored and power failure; no invented moving assembly. |
| [boulder_smelter.glb](boulder_smelter.glb) | 84 | Two source furnace windows and dark upper/lower lips. On/off follows anchored, panel and power state. Input/output anchors; no underfloor plumbing geometry. |
| [boulder_retrieval_matrix.glb](boulder_retrieval_matrix.glb) | 132 | Low landing platform with M-shaped rear support, off/manual/automatic indicators and original retrieval flash timing. Boulders remain runtime attachments; teleport effect is a separate alpha plane. |
| [ore_silo.glb](ore_silo.glb) | 152 | Ten-sided domed material silo with separate side controller. Original idle/active status panel; gameplay controls brief activity and remote material storage. No pipes or contents baked into mesh. |
| [plate_press.glb](plate_press.glb) | 124 | Open work bay with separate descending ram and four-second press cycle matching source interaction duration. Off/online/loaded indicators. Plate is a runtime attachment; original empty/filled plate sprites retained in atlas. |
| [recycler.glb](recycler.glb) | 196 | Overhead gantry with open intake and five low-poly rollers, original powered/safety-off/panel-open status. Conveyor and blood tint are runtime state outside base mesh; no underfloor belt geometry. |
