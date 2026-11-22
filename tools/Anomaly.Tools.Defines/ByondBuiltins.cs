namespace Anomaly.Tools.Defines;

/// <summary>
/// The object-like constants of BYOND's built-in <c>stddef.dm</c> that tg's defines build on (<c>TRUE</c>,
/// <c>NORTH</c>, <c>TOPDOWN_LAYER</c>, <c>SEE_MOBS</c>, ...). They are predefined macros of the DM compiler, so a
/// tg define such as <c>ALL_CARDINALS (NORTH|SOUTH|EAST|WEST)</c> folds to a number. The converter never emits
/// them: they belong to the shim (translation-rules.md 1.6, Appendix C.1).
/// Values: OpenDream <c>DMCompiler/DMStandard/Defines.dm</c> (OpenDream's copy of stddef.dm), checked against the
/// BYOND reference where it gives a value (for example "dir var (atom)": NORTH 1, SOUTH 2, EAST 4, WEST 8, UP 16,
/// DOWN 32). OpenDream's own <c>OPENDREAM</c> and <c>OPENDREAM_TOPIC_PORT_EXISTS</c> are left out (a BYOND build
/// does not define them). The function-like ones (<see cref="FunctionMacros"/>) are there so that tg's
/// <c>#undef ASSERT</c> (<c>assert.dm:1</c>) ends a definition, as it does in BYOND, whose ASSERT lives in stddef.dm
/// (BYOND ref "ASSERT proc", see also "stddef.dm file").
/// </summary>
public static class ByondBuiltins
{
    public static readonly IReadOnlyList<(string Name, string Body)> Constants = new (string, string)[]
    {
        ("TRUE", "1"), ("FALSE", "0"),
        ("NORTH", "1"), ("SOUTH", "2"), ("EAST", "4"), ("WEST", "8"), ("UP", "16"), ("DOWN", "32"),
        ("NORTHEAST", "5"), ("SOUTHEAST", "6"), ("SOUTHWEST", "10"), ("NORTHWEST", "9"),
        ("FLOAT_LAYER", "-1"), ("AREA_LAYER", "1"), ("TURF_LAYER", "2"), ("OBJ_LAYER", "3"), ("MOB_LAYER", "4"),
        ("FLY_LAYER", "5"), ("EFFECTS_LAYER", "5000"), ("TOPDOWN_LAYER", "10000"), ("BACKGROUND_LAYER", "20000"),
        ("FLOAT_PLANE", "-32767"),
        ("FEMALE", "\"female\""), ("MALE", "\"male\""), ("NEUTER", "\"neuter\""), ("PLURAL", "\"plural\""),
        ("ANIMATION_END_NOW", "1"), ("ANIMATION_LINEAR_TRANSFORM", "2"), ("ANIMATION_PARALLEL", "4"),
        ("ANIMATION_SLICE", "8"), ("ANIMATION_END_LOOP", "16"), ("ANIMATION_RELATIVE", "256"), ("ANIMATION_CONTINUE", "512"),
        ("LINEAR_EASING", "0"), ("SINE_EASING", "1"), ("CIRCULAR_EASING", "2"), ("CUBIC_EASING", "3"), ("BOUNCE_EASING", "4"),
        ("ELASTIC_EASING", "5"), ("BACK_EASING", "6"), ("QUAD_EASING", "7"), ("JUMP_EASING", "8"),
        ("EASE_IN", "64"), ("EASE_OUT", "128"),
        ("NO_STEPS", "0"), ("FORWARD_STEPS", "1"), ("SLIDE_STEPS", "2"), ("SYNC_STEPS", "3"),
        ("UNIX", "\"UNIX\""), ("MS_WINDOWS", "\"MS Windows\""),
        ("ICON_ADD", "0"), ("ICON_SUBTRACT", "1"), ("ICON_MULTIPLY", "2"), ("ICON_OVERLAY", "3"), ("ICON_AND", "4"),
        ("ICON_OR", "5"), ("ICON_UNDERLAY", "6"),
        ("SEE_INFRA", "(1<<6)"), ("SEE_SELF", "(1<<5)"), ("SEE_MOBS", "(1<<2)"), ("SEEMOBS", "(1<<2)"), ("SEE_OBJS", "(1<<3)"),
        ("SEEOBJS", "(1<<3)"), ("SEE_TURFS", "(1<<4)"), ("SEETURFS", "(1<<4)"), ("SEE_PIXELS", "(1<<8)"), ("SEE_THRU", "(1<<9)"),
        ("SEE_BLACKNESS", "(1<<10)"), ("BLIND", "(1<<0)"),
        ("MOB_PERSPECTIVE", "0"), ("EYE_PERSPECTIVE", "1"), ("EDGE_PERSPECTIVE", "2"),
        ("BLEND_DEFAULT", "0"), ("BLEND_OVERLAY", "1"), ("BLEND_ADD", "2"), ("BLEND_SUBTRACT", "3"), ("BLEND_MULTIPLY", "4"),
        ("BLEND_INSET_OVERLAY", "5"),
        ("SOUND_MUTE", "(1<<0)"), ("SOUND_PAUSED", "(1<<1)"), ("SOUND_STREAM", "(1<<2)"), ("SOUND_UPDATE", "(1<<4)"),
        ("COLORSPACE_RGB", "0"), ("COLORSPACE_HSV", "1"), ("COLORSPACE_HSL", "2"), ("COLORSPACE_HCY", "3"),
        ("FILTER_COLOR_RGB", "0"), ("FILTER_COLOR_HSV", "1"), ("FILTER_COLOR_HSL", "2"), ("FILTER_COLOR_HCY", "3"),
        ("LONG_GLIDE", "(1<<0)"), ("RESET_COLOR", "(1<<1)"), ("RESET_ALPHA", "(1<<2)"), ("RESET_TRANSFORM", "(1<<3)"),
        ("NO_CLIENT_COLOR", "(1<<4)"), ("KEEP_TOGETHER", "(1<<5)"), ("KEEP_APART", "(1<<6)"), ("PLANE_MASTER", "(1<<7)"),
        ("TILE_BOUND", "(1<<8)"), ("PIXEL_SCALE", "(1<<9)"), ("PASS_MOUSE", "(1<<10)"), ("TILE_MOVER", "(1<<11)"),
        ("MATRIX_COPY", "0"), ("MATRIX_MULTIPLY", "1"), ("MATRIX_ADD", "2"), ("MATRIX_SUBTRACT", "3"), ("MATRIX_INVERT", "4"),
        ("MATRIX_INTERPOLATE", "8"), ("MATRIX_ROTATE", "5"), ("MATRIX_SCALE", "6"), ("MATRIX_TRANSLATE", "7"),
        ("MATRIX_MODIFY", "128"),
        ("PROFILE_STOP", "1"), ("PROFILE_CLEAR", "2"), ("PROFILE_AVERAGE", "4"), ("PROFILE_START", "0"), ("PROFILE_REFRESH", "0"),
        ("PROFILE_RESTART", "PROFILE_CLEAR"),
        ("MASK_INVERSE", "(1<<0)"), ("MASK_SWAP", "(1<<1)"),
        ("FILTER_OVERLAY", "0"), ("FILTER_UNDERLAY", "1"),
        ("OUTLINE_SHARP", "(1<<0)"), ("OUTLINE_SQUARE", "(1<<1)"),
        ("WAVE_BOUNDED", "(1<<0)"), ("WAVE_SIDEWAYS", "(1<<1)"),
        ("MOUSE_INACTIVE_POINTER", "0"), ("MOUSE_ACTIVE_POINTER", "1"), ("MOUSE_DRAG_POINTER", "3"), ("MOUSE_DROP_POINTER", "4"),
        ("MOUSE_ARROW_POINTER", "5"), ("MOUSE_CROSSHAIRS_POINTER", "6"), ("MOUSE_HAND_POINTER", "7"),
        ("CONTROL_FREAK_ALL", "(1<<0)"), ("CONTROL_FREAK_SKIN", "(1<<1)"), ("CONTROL_FREAK_MACROS", "(1<<2)"),
        ("VIS_INHERIT_ICON", "1"), ("VIS_INHERIT_ICON_STATE", "2"), ("VIS_INHERIT_DIR", "4"), ("VIS_INHERIT_LAYER", "8"),
        ("VIS_INHERIT_PLANE", "16"), ("VIS_INHERIT_ID", "32"), ("VIS_UNDERLAY", "64"), ("VIS_HIDE", "128"),
        ("TOPDOWN_MAP", "0"), ("ISOMETRIC_MAP", "1"), ("SIDE_MAP", "2"), ("TILED_ICON_MAP", "32768"),
        ("LEGACY_MOVEMENT_MODE", "0"), ("TILE_MOVEMENT_MODE", "1"), ("PIXEL_MOVEMENT_MODE", "2"),
        ("UNIFORM_RAND", "0"), ("NORMAL_RAND", "1"), ("LINEAR_RAND", "2"), ("SQUARE_RAND", "3"),
        ("JSON_PRETTY_PRINT", "1"),
    };

    /// <summary>OpenDream's function-like stddef macros: name, parameters, body.</summary>
    public static readonly IReadOnlyList<(string Name, string[] Parameters, string Body)> FunctionMacros = new (string, string[], string)[]
    {
        ("REGEX_QUOTE", new[] { "a" }, "regex((a), 1)"),
        ("REGEX_QUOTE_REPLACEMENT", new[] { "a" }, "regex((a), 2)"),
        ("ASSERT", new[] { "expr" }, "((expr) ? null : CRASH(\"Assertion Failed: \" + #expr))"),
        ("EXCEPTION", new[] { "value" }, "new/exception(value, __FILE__, __LINE__)"),
    };
}
