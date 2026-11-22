# Mass driver

Model-only asset, built and reviewed using interactive Blender MCP.

- `mass_driver.glb`: 1548 triangles; original atlas embedded; nearest sampling.
- `mass_driver.blend`: editable source with independent animation actions.
- `source_states.json`: all original directions, sprite frames and delays.

Footprint 1.76 x 1.80 m, height 0.633 m; fits one 2 x 2 m station tile. Blender Z up, forward -Y; glTF Y up, forward +Z. Root origin is floor-centred.

Parts: Rail_Bed, Carriage (moving parent), Pusher_Housing, Service_Hatch, Service_Circuit. Launch translates the carriage through 1.28 m and returns in 1.1 seconds. Service_Open_Close opens the rear-hinged hatch by 105 degrees and closes it. Run servicing while the carriage is parked. The source game prevents launch while the service panel is open.

Original source: `icons/obj/machines/floor.dmi`, exported unchanged as `godot/assets/tg/icons/obj/machines/floor.png`. States: mass_driver (idle, 5 frames), mass_driver1 (launch, 11 frames), mass_driver_o (maintenance), each with four directions. Studied `tgstation/code/game/machinery/mass_driver.dm` and the project's generated type. Atlas UVs distinguish horizontal rail bed, carriage roof and vertical crossbar face; back and sides receive restrained metal detailing.

The geometric motion is exported as two GLB animation clips. Source sprite colour/light cycling is documented but not implemented as a runtime shader. No integration/catalog edits or game tests performed.

Validation: all four mesh components are closed, have positive signed volume and no zero-area faces. Front, rear, both sides, open hatch and intermediate carriage pose reviewed in Blender. Service scripts live outside the repository: `../AnomalyStation3D_offrepo/tools/art/build_mass_driver.py`. The saved Blender file is the deliverable source.

Wall girder was explicitly excluded by the user and was not modelled.

Ribbed revision: raised chamfered ribs on both side rails; eight-sided corrugated central actuator connects a fixed front anchor to the carriage coupling. Its compression is included with carriage motion in the GLB Launch clip. All 34 sampled frames maintain the connection (maximum numerical mismatch below 0.000001 m). Rail guide shoes are outside the rib path. Revised through revise_mass_driver_ribs.py via Blender MCP.
