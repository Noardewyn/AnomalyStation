# Wall airlock sensor

Source: /obj/machinery/airlock_sensor in code/game/machinery/airlock_control.dm.
MetaStation contains two definition records with wall offsets. Incinerator
subtypes share this appearance. No existing matching model was found.
The sensor alerts below 80% of one atmosphere; interaction cycles its master
airlock controller. Off is selected when on is false, not explicitly NOPOWER.

Seven static presets: standby, off, cycle_0/1 and alert_0/1/2.
Cycle delays [5,5], alert delays [3,3,3]; both source sequences rewind.
No animation, collisions or gameplay integration added.

Original wallmounts.png is 640 x 608 with 32 x 32 tiles. PNG pixels match
source DMI. GLB embeds unchanged original bytes; BLEND references the atlas.
Nearest filtering, flat shading, no derived textures. The three perspective
roof rows become the top surface. Rear and sides use original casing pixels;
their depth and UV arrangement are a 3D interpretation.

Closed continuous housing plus two separate press buttons and a status indicator.
Four physical meshes are shared by all presets in the variants library.
Each uses material slot 0: Airlock_Sensor_<state>_Original. Atlas state offsets
use KHR_texture_transform. Buttons have rear-center origins and press along
local +Y. The red flash is baked into source frames; external pixels are
separated into Halo_Overlay with Airlock_Sensor_<state>_Halo_Overlay slot 0,
alpha blending and original texture alpha. It is an intentional open effect
sheet, not glass or a physical hole. No invented separate source overlays.

0.90 / 32 m per pixel. Housing width 0.28125 m, height 0.140625 m,
depth 0.05625 m; buttons protrude another 0.00421875 m. Rear-center origin;
front Blender -Y / Godot +Z. Rotate the same asset for different walls.

48 triangles for physical geometry; 48-108 including optional halo pixels.
All seven GLBs pass opaque UV, degeneracy, nearest sampler and original PNG
checks. All physical meshes are manifold. Front, roof, rear and side reviewed
in the preview. Godot runtime/import not tested. Global validation still has
unrelated legacy failures; this family passes.

Built through Blender Lab MCP with tools/art/build_airlock_sensor.py and
export_airlock_sensor.py. See source_audit.json, mesh_integrity.json,
model_info.json and export_validation.json for detailed checks.
