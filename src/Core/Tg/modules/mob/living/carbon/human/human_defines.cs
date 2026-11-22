// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/human_defines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (vars only), written by the M2 contracts step (rules 10.2): the var overrides (constructor), the
// object-creating default hud_possible (dm_init_vars) and the var block. Owner in M2: P2.

/// Any humanoid (non-Xeno) mob, such as humans, plasmamen, lizards.
// tg: code/modules/mob/living/carbon/human/human_defines.dm:2
// [DmPath("/mob/living/carbon/human")] is on the part in code/__HELPERS/pronouns.dm, the earliest .dme file that
// declares /mob/living/carbon/human (rules 2.3).
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:3-17  the var overrides (rules 10.2); hud_possible
    // (:8) creates a list: dm_init_vars
    public mob_living_carbon_human()
    {
        name = "Unknown";
        real_name = "Unknown";
        icon = Dm.resource("icons/mob/human/human.dmi");
        icon_state = "human_basic";
        appearance_flags = Dm.bitor(Dm.bitor(Dm.bitor(KEEP_TOGETHER, TILE_BOUND), PIXEL_SCALE), LONG_GLIDE);
        hud_type = typeof(datum_hud_human);
        pressure_resistance = 25;
        buckle_lying = 0;
        mob_biotypes = NONE; // The current mob biotypes of human mobs are dependent on the human species (and the organ set bonus)
        can_be_shoved_into = TRUE;
        initial_language_holder = typeof(datum_language_holder_empty); // We get stuff from our species
        flags_1 = PREVENT_CONTENTS_EXPLOSION_1;
        max_grab = GRAB_KILL;
        examine_thats = "This is";
    }

    // tg: code/modules/mob/living/carbon/human/human_defines.dm:8  the object-creating default (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        hud_possible = Dm.list(HEALTH_HUD, STATUS_HUD, BLOOD_HUD, ID_HUD, WANTED_HUD, IMPLOYAL_HUD, IMPSEC_FIRST_HUD, IMPSEC_SECOND_HUD, ANTAG_HUD, GLAND_HUD, FAN_HUD);
    }

    //Hair colour and style
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:20
    public string hair_color = COLOR_BLACK;
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:21
    public string hairstyle = "Bald";

    ///Colours used for hair and facial hair gradients.
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:24
    public DmList grad_color;
    ///Styles used for hair and facial hair gradients.
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:26
    public DmList grad_style;

    //Facial hair colour and style
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:29
    public string facial_hair_color = COLOR_BLACK;
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:30
    public string facial_hairstyle = "Shaved";

    // Base "natural" eye color
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:33
    public string eye_color_left = COLOR_BLACK;
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:34
    public string eye_color_right = COLOR_BLACK;
    /// Var used to keep track of a human mob having a heterochromatic right eye. To ensure prefs don't overwrite shit
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:36
    public float eye_color_heterochromatic = FALSE;
    // Eye color overrides assoc lists - priority key to hex color
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:38
    public DmList eye_color_left_overrides;
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:39
    public DmList eye_color_right_overrides;

    // tg: code/modules/mob/living/carbon/human/human_defines.dm:41
    public string skin_tone = "caucasian1"; //Skin tone

    // tg: code/modules/mob/living/carbon/human/human_defines.dm:43
    public object lip_style = null; //no lipstick by default- arguably misleading, as it could be used for general makeup
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:44
    public string lip_color = COLOR_WHITE;

    // tg: code/modules/mob/living/carbon/human/human_defines.dm:46
    public float? age = 30; //Player's age

    /// Which body type to use
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:49
    public string physique = MALE;

    //consider updating /mob/living/carbon/human/copy_clothing_prefs() if adding more of these
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:52
    public string underwear = "Nude"; //Which underwear the player wants
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:53
    public string underwear_color = COLOR_BLACK;
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:54
    public string undershirt = "Nude"; //Which undershirt the player wants
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:55
    public string socks = "Nude"; //Which socks the player wants
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:56
    public string backpack = DBACKPACK; //Which backpack type the player has chosen.
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:57
    public string jumpsuit_style = PREF_SUIT; //suit/skirt

    //Equipment slots
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:60
    public obj_item back = null;
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:61
    public obj_item head = null;
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:62
    public obj_item gloves = null;
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:63
    public obj_item ears = null;
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:64
    public obj_item glasses = null;
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:65
    public obj_item shoes = null;
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:66
    public obj_item wear_neck = null;
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:67
    public obj_item wear_mask = null;
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:68
    public obj_item wear_suit = null;
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:69
    public obj_item w_uniform = null;
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:70
    public obj_item belt = null;
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:71
    public obj_item wear_id = null;
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:72
    public obj_item r_store = null;
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:73
    public obj_item l_store = null;
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:74
    public obj_item s_store = null;

    /// Allows for special overrides of voice
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:77
    public string override_voice = "";

    // tg: code/modules/mob/living/carbon/human/human_defines.dm:79
    public object account_id;

    // tg: code/modules/mob/living/carbon/human/human_defines.dm:81
    public float hardcore_survival_score = 0;

    /// How many "units of blood" we have on our hands
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:84
    public float blood_in_hands = 0;

    /// The core temperature of the human compaired to the skin temp of the body
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:87
    public float coretemperature = BODYTEMP_NORMAL;

    ///Exposure to damaging heat levels increases stacks, stacks clean over time when temperatures are lower. Stack is consumed to add a wound.
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:90
    public float heat_exposure_stacks = 0;

    /// When an braindead player has their equipment fiddled with, we log that info here for when they come back so they know who took their ID while they were DC'd for 30 seconds
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:93
    public DmList afk_thefts;

    /// Base height of the mob, modified by stuff like dwarfism or species
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:96  VAR_PRIVATE/base_mob_height = HUMAN_HEIGHT_MEDIUM
    [DmPrivate]
    public float base_mob_height = HUMAN_HEIGHT_MEDIUM;
    /// Actual height of the mob. Don't touch this one, it is set via update_mob_height()
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:98  VAR_FINAL/mob_height = HUMAN_HEIGHT_MEDIUM
    [DmFinal]
    public float mob_height = HUMAN_HEIGHT_MEDIUM;

    /// Tracks how long in seconds we've been in a low pressure environment
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:101  VAR_FINAL/seconds_in_low_pressure = 0
    [DmFinal]
    public float seconds_in_low_pressure = 0;

    /// Combined width of our body sprite
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:104  VAR_PRIVATE/cached_body_width = ICON_SIZE_X
    [DmPrivate]
    public float cached_body_width = ICON_SIZE_X;
    /// Combined height of our body sprite
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:106  VAR_PRIVATE/cached_body_height = ICON_SIZE_Y
    [DmPrivate]
    public float cached_body_height = ICON_SIZE_Y;
    /// Leftmost offset of our overlays
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:108
    public float cached_body_min_x_offset = 0;
    /// Rightmost offset of our overlays
    // tg: code/modules/mob/living/carbon/human/human_defines.dm:110
    public float cached_body_min_y_offset = 0;
}
