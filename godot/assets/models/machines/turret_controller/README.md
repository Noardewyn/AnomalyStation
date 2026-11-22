# Wall turret controller

Source /obj/machinery/turretid in code/game/machinery/porta_turret/turret_control.dm.
Initialization mounts the controller on a wall; wallframe uses pixel_shift 30.
Three MetaStation definition records were found. No matching 3D model existed.

Nine static presets: standby, stun and kill with both source screen frames,
plus the three corresponding unpowered configurations. Enabled adds button_left;
lethal also adds button_right. Power loss removes screen overlays/emission but
preserves enabled/lethal button artwork. Locked and AI-lock do not change artwork.
Source emissive_screen/emissive_button_* are lighting masks, not white colored
overlays; original screen/button colors provide emission instead. The three
screen sequences have delays [10,10], rewind true. No animations added.
control_frame is the unmounted construction item and remains metadata only.

Original turret_control.png: 128 x 128, 32 x 32 tiles. PNG pixels match DMI.
GLBs embed unchanged original PNG bytes; BLEND references the original atlas.
Nearest filtering, flat shading. No derived textures. All six housing surfaces
use original atlas pixels; top perspective rows map onto roof. Rear and depth
are 3D interpretations from existing casing strips.

One closed continuous housing and two separate closed buttons with rear-center
pivots. Local +Y presses into the housing. Screen is a separate surface using
original overlay artwork. Housing shared across all scenes, buttons shared by
matching state/position, screen mesh shared across all six powered presets.
Screen UV offsets use KHR_texture_transform. No extra glass, collision,
animations or gameplay integration. Rotate one asset for wall directions.

Material slot 0: Turret_Controller_Housing_Original,
Turret_Controller_<preset>_Button_<1|2>, or
Turret_Controller_<preset>_Screen_Overlay. Button emission is only enabled for
active buttons with power. Screen emission strength 1; button strength 0.4.

Scale 0.90 / 32 m per pixel: housing 0.50625 m wide, 0.309375 m high,
0.05625 m deep. Buttons protrude 0.00421875 m. Rear-center origin;
front Blender -Y / Godot +Z. 36 triangles unpowered, 38 powered.
All nine presets pass opaque UV, original PNG, nearest sampler and degeneracy
checks. Housing/buttons are manifold; screen is intentionally an open sheet.
Front, above, rear and side visually reviewed. Godot import/runtime not tested.
Global validator retains unrelated legacy failures; this family passes.

Built through Blender Lab MCP using tools/art/build_turret_controller.py and
export_turret_controller.py. Adjacent JSON files document source and validation.
