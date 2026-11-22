namespace Anomaly.Core.DmShim;

// Owner: values (written complete by contracts). The BYOND stddef.dm constants that tg uses and does not #define
// itself (docs/ai/translation-rules.md Appendix C.1), written bare in translated code: TRUE, FALSE, the directions,
// layers, genders, animate() and appearance flags, sight flags and the rest. Values are BYOND's stddef.dm as
// reproduced by OpenDream's DMCompiler/DMStandard/Defines.dm; a constant is listed when its name occurs in
// ../tgstation/code and tg does not #define it (tg redefines TURF_LAYER as an #error, __DEFINES/layers.dm:160). Numbers are
// `const float` (DM numbers, rules 3.1), texts `const string`. Function-like stddef macros are not here
// (EXCEPTION is Dm.EXCEPTION; REGEX_QUOTE waits for the regex builtin).
public static partial class Dm
{
    // BYOND ref "stddef.dm file"; TRUE and FALSE are DM numbers (rules 3.1).
    public const float TRUE = 1;
    public const float FALSE = 0;

    // directions (dir, get_dir)
    public const float NORTH = 1;
    public const float SOUTH = 2;
    public const float EAST = 4;
    public const float WEST = 8;
    public const float UP = 16;
    public const float DOWN = 32;
    public const float NORTHEAST = 5;
    public const float SOUTHEAST = 6;
    public const float SOUTHWEST = 10;
    public const float NORTHWEST = 9;

    // atom.layer and atom.plane
    public const float FLOAT_LAYER = -1;
    public const float AREA_LAYER = 1;
    public const float OBJ_LAYER = 3;
    public const float MOB_LAYER = 4;
    public const float FLY_LAYER = 5;
    public const float EFFECTS_LAYER = 5000;
    public const float TOPDOWN_LAYER = 10000;
    public const float BACKGROUND_LAYER = 20000;
    public const float FLOAT_PLANE = -32767;

    // atom.gender
    public const string FEMALE = "female";
    public const string MALE = "male";
    public const string NEUTER = "neuter";
    public const string PLURAL = "plural";

    // animate() flags arg
    public const float ANIMATION_END_NOW = 1;
    public const float ANIMATION_LINEAR_TRANSFORM = 2;
    public const float ANIMATION_PARALLEL = 4;
    public const float ANIMATION_RELATIVE = 256;
    public const float ANIMATION_CONTINUE = 512;

    // animate() easing arg
    public const float LINEAR_EASING = 0;
    public const float SINE_EASING = 1;
    public const float CIRCULAR_EASING = 2;
    public const float CUBIC_EASING = 3;
    public const float BOUNCE_EASING = 4;
    public const float ELASTIC_EASING = 5;
    public const float BACK_EASING = 6;
    public const float QUAD_EASING = 7;
    public const float JUMP_EASING = 8;

    // flags applied to the animate() easing arg
    public const float EASE_IN = 64;
    public const float EASE_OUT = 128;

    // atom.animate_movement
    public const float NO_STEPS = 0;
    public const float FORWARD_STEPS = 1;
    public const float SLIDE_STEPS = 2;

    // world.system_type
    public const string UNIX = "UNIX";
    public const string MS_WINDOWS = "MS Windows";

    // Icon blending functions
    public const float ICON_ADD = 0;
    public const float ICON_SUBTRACT = 1;
    public const float ICON_MULTIPLY = 2;
    public const float ICON_OVERLAY = 3;
    public const float ICON_OR = 5;
    public const float ICON_UNDERLAY = 6;

    // mob.sight
    public const float SEE_INFRA = 64;
    public const float SEE_SELF = 32;
    public const float SEE_MOBS = 4;
    public const float SEEMOBS = 4;
    public const float SEE_OBJS = 8;
    public const float SEEOBJS = 8;
    public const float SEE_TURFS = 16;
    public const float SEE_PIXELS = 256;
    public const float SEE_THRU = 512;
    public const float SEE_BLACKNESS = 1024;
    public const float BLIND = 1;

    // client.perspective
    public const float MOB_PERSPECTIVE = 0;
    public const float EYE_PERSPECTIVE = 1;

    // atom.blend_mode
    public const float BLEND_DEFAULT = 0;
    public const float BLEND_OVERLAY = 1;
    public const float BLEND_ADD = 2;
    public const float BLEND_SUBTRACT = 3;
    public const float BLEND_MULTIPLY = 4;
    public const float BLEND_INSET_OVERLAY = 5;

    // sound.status
    public const float SOUND_MUTE = 1;
    public const float SOUND_STREAM = 4;
    public const float SOUND_UPDATE = 16;

    // Color spaces
    public const float COLORSPACE_RGB = 0;
    public const float COLORSPACE_HSV = 1;
    public const float COLORSPACE_HSL = 2;
    public const float COLORSPACE_HCY = 3;

    // See color matrix filter: filter(type="color", ...)
    public const float FILTER_COLOR_RGB = 0;
    public const float FILTER_COLOR_HSV = 1;
    public const float FILTER_COLOR_HSL = 2;
    public const float FILTER_COLOR_HCY = 3;

    // atom.appearance_flags
    public const float LONG_GLIDE = 1;
    public const float RESET_COLOR = 2;
    public const float RESET_ALPHA = 4;
    public const float RESET_TRANSFORM = 8;
    public const float NO_CLIENT_COLOR = 16;
    public const float KEEP_TOGETHER = 32;
    public const float KEEP_APART = 64;
    public const float PLANE_MASTER = 128;
    public const float TILE_BOUND = 256;
    public const float PIXEL_SCALE = 512;
    public const float PASS_MOUSE = 1024;
    public const float TILE_MOVER = 2048;

    // matrix() operation flags (BYOND 515)

    public const float MATRIX_ROTATE = 5;
    public const float MATRIX_SCALE = 6;
    public const float MATRIX_TRANSLATE = 7;

    // world/Profile() arg
    public const float PROFILE_STOP = 1;
    public const float PROFILE_CLEAR = 2;
    public const float PROFILE_START = 0;
    public const float PROFILE_REFRESH = 0;
    public const float PROFILE_RESTART = 2;

    // filter(type="alpha", ...) flags arg
    public const float MASK_INVERSE = 1;
    public const float MASK_SWAP = 2;

    // filter(type="layer", ...) flags arg
    public const float FILTER_OVERLAY = 0;
    public const float FILTER_UNDERLAY = 1;

    // filter(type="outline", ...) flags arg
    public const float OUTLINE_SHARP = 1;
    public const float OUTLINE_SQUARE = 2;

    // filter(type="wave", ...) flags arg
    public const float WAVE_BOUNDED = 1;
    public const float WAVE_SIDEWAYS = 2;

    // see mouse handling
    public const float MOUSE_INACTIVE_POINTER = 0;
    public const float MOUSE_ACTIVE_POINTER = 1;
    public const float MOUSE_HAND_POINTER = 7;

    // client.control_freak
    public const float CONTROL_FREAK_SKIN = 2;
    public const float CONTROL_FREAK_MACROS = 4;

    // atom.vis_flags
    public const float VIS_INHERIT_ICON = 1;
    public const float VIS_INHERIT_ICON_STATE = 2;
    public const float VIS_INHERIT_DIR = 4;
    public const float VIS_INHERIT_LAYER = 8;
    public const float VIS_INHERIT_PLANE = 16;
    public const float VIS_INHERIT_ID = 32;
    public const float VIS_UNDERLAY = 64;
    public const float VIS_HIDE = 128;

    // world.map_format
    public const float SIDE_MAP = 2;

    // generator() distributions
    public const float UNIFORM_RAND = 0;
    public const float NORMAL_RAND = 1;
    public const float LINEAR_RAND = 2;
    public const float SQUARE_RAND = 3;

    // v515 json_encode() pretty print flag
    public const float JSON_PRETTY_PRINT = 1;
}
