# Servers and communications

8 base models. Each has a GLB and editable Blender file.

Use the GLB in Godot. Keep original texture sampling at nearest. Inspect named child meshes and anchors before wiring animations to gameplay; mutually exclusive state clips must not play together. Dynamic contents are deliberately separate.

Source atlas metadata: `source_states.json`. Source audit and specific limitations are recorded below. Atlas verification checks RGBA identity and glTF sampler settings; geometry checks sample exported animation poses against floor and ceiling. They do not certify gameplay completeness.

| Model | Triangles including hidden state meshes | Coverage |
|---|---:|---|
| [telecomms_dish.glb](telecomms_dish.glb) | 352 | Shared receiver/broadcaster geometry, receiver/transmitter/off states; open receiving dish and original colored signal-strut texels. broadcaster_send beam is a runtime effect at Signal_Origin, not a solid obstruction. |
| [telecomms_processor.glb](telecomms_processor.glb) | 120 | Source processor/processor_off with separate service flap and source open wiring. Raised processor screen and vented lower chassis. |
| [telecomms_bus_ntnet.glb](telecomms_bus_ntnet.glb) | 120 | Source bus/bus_off with separate service flap and source open wiring. NTNet uses bus/bus_off exactly; same geometry, no duplicate relay model. |
| [telecomms_hub.glb](telecomms_hub.glb) | 60 | Tall five-row telecomms hub with source on/off panels and side service flap. One tile; no separate per-direction models. |
| [telecomms_server.glb](telecomms_server.glb) | 72 | Shared chassis proportions, original comm_server atlas; source status variants and service wiring. Research master-server theft stages are not part of the ordinary server type; no objective stripes baked in. |
| [research_server.glb](research_server.glb) | 84 | Shared chassis proportions, original RD-server-on atlas; source status variants and service wiring. Research master-server theft stages are not part of the ordinary server type; no objective stripes baked in. |
| [announcement_system.glb](announcement_system.glb) | 126 | Separate tower, red cabling and terminal, on/off screen and original independently controlled arrival/head/error indicators. EMP and configuration dispatch remain gameplay. |
| [blackbox_recorder.glb](blackbox_recorder.glb) | 124 | Two independent rotating reels and removable blackbox block, recording/empty clips. Source removal stops tapes. Runtime item ownership/transfer is separate. |
