"""Exports godot/assets/models/held/held_swing.glb from held_swing.blend, and renders the clips' frames: the first-person swings of held things (D285).

The .blend is the source: its clips are keyframed in Blender by hand. A clip moves the hands, not a model: the game makes a held thing's solid from
its tg picture, so a clip animates "Grip", the point the hands hold, and the game carries whatever thing is in the hand with it
(godot/scripts/Shared/Play/HeldClip.cs).

  * "Grip" (a child of "Rest") is keyed: its location is the hands' move from rest, its rotation the thing's turn about the hands, both in the
    camera's space (Blender's +Y is the game's forward, +Z up; the glTF export turns it into the game's x right, y up, -z forward). At the first
    and last frame it is at rest. The left hand plays the mirror image.
  * Each action is one clip, named as the game asks for it (HeldClip.Names), in its own muted NLA track on Grip so the export writes them all.
    An action's custom properties set its preview: "rest", where the hands hold a thing at rest in the game's camera space (HeldPose.Rest plus
    held_items.json's "_all" shift), and "prop", the placeholder thing on Grip to show.
  * "Rest", the placeholders ("Blade", "Axe", "Driver", "Spear") and "Camera" (the first-person view, the view model's 62 degrees) are only for
    the preview; Rest is exported unturned, so Grip's keys are the camera's moves and turns. A placeholder's mesh is in the game's picture space
    (x the picture's right, y its up, the grip at the origin) and its turn is ViewModel.Place's for a long thing in the right hand, converted to
    Blender's axes. "Axe" is the client's own extruded fireaxe1 (SpriteExtrusion, the picture packed in the .blend), checked against a client
    frame of the wielded fire axe at rest; the others are boxes.

The game plays a clip from the click: tg deals a melee hit on the click, so the wind-up is short and the blow lands 0.12 to 0.2 s in; a clip is
at most 0.8 s long (tg's CLICK_CD_MELEE).

Run (Blender 5.2), or the same calls from Blender's own Python console:
  blender --background godot/assets/models/held/held_swing.blend --python tools/models/make_held_swing.py               export
  blender --background godot/assets/models/held/held_swing.blend --python tools/models/make_held_swing.py -- --preview DIR
                                                                                     also render each clip's frames to DIR
"""
import os
import sys

import bpy
from mathutils import Vector

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
GLB = os.path.join(REPO, "godot", "assets", "models", "held", "held_swing.glb")
CLIPS = ("melee_swing", "wield_chop", "stab", "wield_stab")


def export():
    grip = bpy.data.objects["Grip"]
    grip.animation_data.action = None  # every clip is in an NLA track; an active one would be written twice
    for o in bpy.context.scene.objects:
        o.select_set(o.name in ("Rest", "Grip"))
    bpy.ops.export_scene.gltf(filepath=GLB, export_format="GLB", use_selection=True, export_animations=True,
                              export_animation_mode="ACTIONS", export_force_sampling=True, export_apply=False,
                              export_cameras=False, export_lights=False)


def preview(folder, every=3):
    """Renders every clip's every third frame (60 fps) to DIR/<clip>_<frame>.png, with its own placeholder at its own rest."""
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.render.resolution_x, scene.render.resolution_y = 480, 270
    os.makedirs(folder, exist_ok=True)
    grip = bpy.data.objects["Grip"]
    for name in CLIPS:
        action = bpy.data.actions[name]
        grip.animation_data.action = action
        x, y, z = action["rest"]
        bpy.data.objects["Rest"].location = Vector((x, -z, y))
        for o in grip.children:
            o.hide_render = o.name != action["prop"]
        for frame in range(0, int(action.frame_range[1]) + 1, every):
            scene.frame_set(frame)
            scene.render.filepath = os.path.join(folder, "%s_%02d.png" % (name, frame))
            bpy.ops.render.render(write_still=True)
    grip.animation_data.action = None


args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
export()
if "--preview" in args:
    preview(args[args.index("--preview") + 1])
