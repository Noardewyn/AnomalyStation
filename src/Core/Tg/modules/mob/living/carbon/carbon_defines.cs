// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/carbon_defines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (vars only), written by the M2 contracts step (rules 10.2): the var overrides (constructor), the
// object-creating defaults (dm_init_vars), the var block and the static limb_icon_cache (a TgState field, rules 10.6).
// Owner in M2: P2.

// tg: code/modules/mob/living/carbon/carbon_defines.dm:1
// [DmPath("/mob/living/carbon")] is on the part in code/__HELPERS/names.dm, the earliest .dme file that declares
// /mob/living/carbon (rules 2.3).
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:2-17, :54  the var overrides (rules 10.2); hud_possible
    // (:6) and held_items (:8) create lists: dm_init_vars
    public mob_living_carbon()
    {
        abstract_type = typeof(mob_living_carbon);
        default_blood_volume = BLOOD_VOLUME_NORMAL;
        gender = MALE;
        pressure_resistance = 15;
        has_limbs = TRUE;
        num_legs = 0; //Populated on init through list/bodyparts
        usable_legs = 0; //Populated on init through list/bodyparts
        num_hands = 0; //Populated on init through list/bodyparts
        usable_hands = 0; //Populated on init through list/bodyparts
        mobility_flags = MOBILITY_FLAGS_CARBON_DEFAULT;
        blocks_emissive = EMISSIVE_BLOCK_NONE;
        mouse_drop_zone = TRUE;
        // STOP_OVERLAY_UPDATE_BODY_PARTS is removed after we call update_body_parts() during init.
        living_flags = Dm.bitor(ALWAYS_DEATHGASP, STOP_OVERLAY_UPDATE_BODY_PARTS);
        // tg: code/modules/mob/living/carbon/carbon_defines.dm:54
        rotate_on_lying = TRUE;
    }

    // tg: code/modules/mob/living/carbon/carbon_defines.dm:6, :8, :19, :21, :59, :68, :71, :74  the object-creating
    // defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        hud_possible = Dm.list(HEALTH_HUD, STATUS_HUD, ANTAG_HUD, GLAND_HUD);
        held_items = Dm.list(null, null);
        organs = Dm.list();
        organs_slot = Dm.list();
        bodyparts = Dm.list(
            typeof(obj_item_bodypart_chest),
            typeof(obj_item_bodypart_head),
            typeof(obj_item_bodypart_arm_left),
            typeof(obj_item_bodypart_arm_right),
            typeof(obj_item_bodypart_leg_right),
            typeof(obj_item_bodypart_leg_left));
        real_bodypart_cache = Dm.alist();
        hand_bodyparts = Dm.list();
        icon_render_keys = Dm.list();
    }

    ///List of [/obj/item/organ]s in the mob. They don't go in the contents for some reason I don't want to know.
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:19
    public DmList organs;
    ///Same as [above][/mob/living/carbon/var/organs], but stores "slot ID" - "organ" pairs for easy access.
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:21
    public DmList organs_slot;

    ///Whether or not the mob is handcuffed
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:24
    public obj_item handcuffed = null;
    ///Same as handcuffs but for legs. Bear traps use this.
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:26
    public obj_item legcuffed = null;

    /// Measure of how disgusted we are. See DISGUST_LEVEL_GROSS and friends
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:29
    public float disgust = 0;
    /// How disgusted we were LAST time we processed disgust. Helps prevent unneeded work
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:31
    public float old_disgust = 0;

    //inventory slots
    /// Equipped air tank. Never set this manually.
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:35
    public obj_item_tank @internal = null;
    /// "External" air tank. Never set this manually. Not required to stay directly equipped on the mob (i.e. could be a machine or MOD suit module).
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:37
    public obj_item_tank external = null;

    /// DNA is carbon-only, and ideally you should be accessing it through has_dna(), but you can access it directly if you know you're working with a carbon mob
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:40
    public datum_dna dna = null;
    ///last mind to control this mob, for blood-based cloning
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:42
    public datum_mind last_mind = null;

    ///This is used to determine if the mob failed a breath. If they did fail a breath, they will attempt to breathe each tick, otherwise just once per 4 ticks.
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:45
    public float failed_last_breath = FALSE;
    ///Sound loop for breathing when using internals
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:47
    public datum_looping_sound_breathing breathing_loop;

    /// Used in [carbon/proc/check_breath] and [lungs/proc/check_breath]]
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:50
    public object co2overloadtime = null;

    // tg: code/modules/mob/living/carbon/carbon_defines.dm:52  var/obj/item/food/meat/slab/type_of_meat (holds
    // typepaths: V3)
    public Type type_of_meat = typeof(obj_item_food_meat_slab);

    /// Gets filled up in [/datum/species/proc/replace_body].
    /// Will either contain a list of typepaths if nothing has been created yet,
    /// or a list of the body part objects.
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:59
    public DmList bodyparts;
    /// Alist of (non-stump) bodyparts by their bodyzone for quick get_bodypart access
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:68
    public DmList real_bodypart_cache;

    /// A collection of arms (or actually whatever the fug /bodyparts you monsters use to wreck my systems)
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:71
    public DmList hand_bodyparts;

    ///A cache of bodypart = icon to prevent excessive icon creation.
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:74
    public DmList icon_render_keys;
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:75  var/static/list/limb_icon_cache = list() (rules 10.6: a
    // TgState field, below)
    public static ref DmList limb_icon_cache => ref TgState.Current.mob_living_carbon__limb_icon_cache;

    /// Used to temporarily increase severity of / apply a new damage overlay (the red ring around the ui / screen).
    /// This number will translate to equivalent brute or burn damage taken. Handled in [mob/living/proc/update_damage_hud].
    /// (For example, setting damageoverlaytemp = 20 will add 20 "damage" to the overlay the next time it updates.)
    /// This number is also reset to 0 every tick of carbon Life(). Pain.
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:81
    public float damageoverlaytemp = 0;

    /// Protection (insulation) from the heat, Value 0-1 corresponding to the percentage of protection
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:84
    public float? heat_protection = 0; // No heat protection
    /// Protection (insulation) from the cold, Value 0-1 corresponding to the percentage of protection
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:86
    public float? cold_protection = 0; // No cold protection

    /// Timer id of any transformation
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:89
    public object transformation_timer;

    /// All of the wounds a carbon has afflicted throughout their limbs
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:92
    public DmList all_wounds;
    /// All of the scars a carbon has afflicted throughout their limbs
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:94
    public DmList all_scars;

    /// Assoc list of BODY_ZONE -> wounding_type. Set when a limb is dismembered, unset when one is attached. Used for determining what scar to add when it comes time to generate them.
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:97
    public DmList body_zone_dismembered_by;

    /// Simple modifier for whether this mob can handle greater or lesser skillchip complexity. See /datum/mutation/biotechcompat/ for example.
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:100
    public float skillchip_complexity_modifier = 0;

    /// Can other carbons be shoved into this one to make it fall?
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:103
    public float can_be_shoved_into = FALSE;

    /// Only load in visual organs
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:106
    public float visual_only_organs = FALSE;

    /// Stores the result of our last known top_offset generation for optimisation purposes when drawing limb icons.
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:109
    public object last_top_offset;

    /// A bitfield of "bodytypes", updated by /obj/item/bodypart/proc/synchronize_bodytypes()
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:112
    public float bodytype = BODYTYPE_ORGANIC;

    /// A bitfield of "bodyshapes", updated by /obj/item/bodypart/proc/synchronize_bodyshapes()
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:115
    public float bodyshape = BODYSHAPE_HUMANOID;

    // tg: code/modules/mob/living/carbon/carbon_defines.dm:117  COOLDOWN_DECLARE(bleeding_message_cd)
    // (__DEFINES/cooldowns.dm: var/bleeding_message_cd = 0)
    public float bleeding_message_cd = 0;

    /// Obscured hide flags (hideflags that can't be seen AND can't be interacted with)
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:120
    public float obscured_slots = NONE;
    /// Covered hide flags (hideflags that can be seen, BUT can't be interacted with)
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:122
    public float covered_slots = NONE;

    /// Lazylist of all hair masks applied to this mob's hairstyles
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:125
    public DmList hair_masks;
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/carbon/carbon_defines.dm:75  var/static/list/limb_icon_cache = list()
    internal DmList mob_living_carbon__limb_icon_cache = Dm.list();
}
