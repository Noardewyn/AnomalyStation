extends Node3D
## Attach to an inherited console GLB scene or a parent of the console instance.
## The body is untouched; each instance gets independent overlay materials.

@export var screen_state: String = "generic"
@export var keyboard_state: String = "generic_key"
const ATLAS = preload("res://assets/tg/icons/obj/machines/computer.png")
const OVERLAY_SHADER = preload("res://assets/models/machines/consoles/console_overlay.gdshader")
const METADATA_PATH = "res://assets/tg/icons/obj/machines/computer.json"
var _states: Dictionary = {}

func _ready() -> void:
    var parsed = JSON.parse_string(FileAccess.get_file_as_string(METADATA_PATH))
    if not parsed is Dictionary:
        push_error("Cannot read computer atlas metadata")
        return
    for state in parsed.get("states", []):
        _states[state["name"]] = state
    set_screen(screen_state)
    set_keyboard(keyboard_state)

func set_screen(state: String, frame: int = 0) -> void:
    _set_layer("Screen_Overlay", state, frame)

func set_keyboard(state: String, frame: int = 0) -> void:
    _set_layer("Keyboard_Overlay", state, frame)

func _set_layer(node_name: String, state_name: String, frame: int) -> void:
    var layer := find_child(node_name, true, false) as MeshInstance3D
    if layer == null:
        push_error("Missing console layer: " + node_name)
        return
    if state_name.is_empty():
        layer.hide()
        return
    if not _states.has(state_name):
        push_error("Unknown computer atlas state: " + state_name)
        return
    var state: Dictionary = _states[state_name]
    # First direction is SOUTH/front. Rotate the 3D console, never its UI texture.
    var rect: Array = state["rects"][posmod(frame, int(state["frames"]))]
    var material := layer.material_override as ShaderMaterial
    if material == null:
        material = ShaderMaterial.new()
        material.shader = OVERLAY_SHADER
        material.set_shader_parameter("atlas", ATLAS)
        layer.material_override = material
    material.set_shader_parameter("atlas_rect", Vector4(rect[0], rect[1], rect[2], rect[3]))
    layer.show()
