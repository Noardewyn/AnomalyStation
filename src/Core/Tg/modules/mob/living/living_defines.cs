// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/living_defines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (it holds only the /mob/living var block), translated by the M2 contracts step (rules 10.2): the var
// overrides of the type block (constructor), the object-creating defaults (dm_init_vars) and the var declarations, in DM
// order. Owner in M2: P1 (living core).

// tg: code/modules/mob/living/living_defines.dm:1
// [DmPath("/mob/living")] is on the part in code/__HELPERS/hallucinations.dm, the earliest .dme file that declares
// /mob/living (rules 2.3).
public partial class mob_living : mob
{
    // tg: code/modules/mob/living/living_defines.dm:2-8  the var overrides of the type block (rules 10.2), in DM order;
    // hud_possible (:4) creates a list: dm_init_vars below. see_invisible is a builtin /mob var (object-model.md 2).
    public mob_living()
    {
        see_invisible = SEE_INVISIBLE_LIVING;
        abstract_type = typeof(mob_living);
        pressure_resistance = 10;
        hud_type = typeof(datum_hud_living);
        interaction_flags_click = ALLOW_RESTING;
        interaction_flags_mouse_drop = ALLOW_RESTING;
    }

    // tg: code/modules/mob/living/living_defines.dm:4, :142, :208  the object-creating defaults (rules 10.2), in DM order
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        hud_possible = Dm.list(HEALTH_HUD, STATUS_HUD, BLOOD_HUD, ANTAG_HUD);
        pipes_shown = Dm.list();
        // List of changes to body temperature, used by desease symtoms like fever
        body_temp_changes = Dm.list();
    }

    ///Tracks the scale of the mob transformation matrix in relation to its identity. Use update_transform(resize) to change it.
    // tg: code/modules/mob/living/living_defines.dm:11
    public float current_size = RESIZE_DEFAULT_SIZE;
    ///How the mob transformation matrix is scaled on init.
    // tg: code/modules/mob/living/living_defines.dm:13
    public float initial_size = RESIZE_DEFAULT_SIZE;

    // tg: code/modules/mob/living/living_defines.dm:15
    public object lastattacker = null;
    // tg: code/modules/mob/living/living_defines.dm:16
    public object lastattackerckey = null;

    //Health and life related vars
    /// Maximum health that should be possible.
    // tg: code/modules/mob/living/living_defines.dm:20
    public float maxHealth = MAX_LIVING_HEALTH;
    /// The mob's current health.
    // tg: code/modules/mob/living/living_defines.dm:22
    public float health = MAX_LIVING_HEALTH;

    /// The max amount of stamina damage we can have at once (Does NOT effect stamcrit thresholds. See crit_threshold)
    // tg: code/modules/mob/living/living_defines.dm:25
    public float max_stamina = 120;
    ///Stamina damage, or exhaustion. You recover it slowly naturally, and are knocked down if it gets too high. Holodeck and hallucinations deal this.
    // tg: code/modules/mob/living/living_defines.dm:27
    public float staminaloss = 0;

    //Damage related vars, NOTE: THESE SHOULD ONLY BE MODIFIED BY PROCS
    ///Brutal damage caused by brute force (punching, being clubbed by a toolbox ect... this also accounts for pressure damage)
    // tg: code/modules/mob/living/living_defines.dm:31
    public float bruteloss = 0;
    ///Oxygen depravation damage (no air in lungs)
    // tg: code/modules/mob/living/living_defines.dm:33
    public float oxyloss = 0;
    ///Toxic damage caused by being poisoned or radiated
    // tg: code/modules/mob/living/living_defines.dm:35
    public float toxloss = 0;
    ///Burn damage caused by being way too hot, too cold or burnt.
    // tg: code/modules/mob/living/living_defines.dm:37
    public float fireloss = 0;

    ///Lazy list of multipliers for related to mobs, like burn/brute/oxy/tox/stamina damage, hunger, bleeding, electrical conductivity
    // tg: code/modules/mob/living/living_defines.dm:40
    public DmList physiology;
    ///Contains an inner armor datum separated from worn armor (I could use the default 'armor' var but it might cause some confusion)
    // tg: code/modules/mob/living/living_defines.dm:42  VAR_PROTECTED/datum/armor/inner_armor
    [DmProtected]
    public datum_armor inner_armor;
    ///Used on apply_damage(). Less than 0 means you take extra damage. While over 100 is where no damage is taken at all.
    // tg: code/modules/mob/living/living_defines.dm:44
    public float damage_resistance = 0;

    /// The movement intent of the mob (run/wal)
    // tg: code/modules/mob/living/living_defines.dm:47
    public string move_intent = MOVE_INTENT_RUN;

    /// Rate at which fire stacks should decay from this mob
    // tg: code/modules/mob/living/living_defines.dm:50
    public float fire_stack_decay_rate = -0.05f;

    /// when the mob goes from "normal" to crit
    // tg: code/modules/mob/living/living_defines.dm:53
    public float crit_threshold = HEALTH_THRESHOLD_CRIT;
    ///When the mob enters hard critical state and is fully incapacitated.
    // tg: code/modules/mob/living/living_defines.dm:55
    public float hardcrit_threshold = HEALTH_THRESHOLD_FULLCRIT;
    ///Amount of damage needed to be fully dead
    // tg: code/modules/mob/living/living_defines.dm:57
    public float dead_threshold = HEALTH_THRESHOLD_DEAD;

    //Damage dealing vars! These are meaningless outside of specific instances where it's checked and defined.
    /// Lower bound of damage done by unarmed melee attacks. Mob code is a mess, only works where this is checked for.
    // tg: code/modules/mob/living/living_defines.dm:61
    public float melee_damage_lower = 0;
    /// Upper bound of damage done by unarmed melee attacks. Please ensure you check the xyz_defenses.dm for the mobs in question to see if it uses this or hardcoded values.
    // tg: code/modules/mob/living/living_defines.dm:63
    public float melee_damage_upper = 0;

    /// Generic bitflags for boolean conditions at the [/mob/living] level. Keep this for inherent traits of living types, instead of runtime-changeable ones.
    // tg: code/modules/mob/living/living_defines.dm:66
    public float living_flags = NONE;

    /// Flags that determine the potential of a mob to perform certain actions. Do not change this directly.
    // tg: code/modules/mob/living/living_defines.dm:69
    public float mobility_flags = MOBILITY_FLAGS_DEFAULT;

    // tg: code/modules/mob/living/living_defines.dm:71
    public float resting = FALSE;

    /// Variable to track the body position of a mob, regardgless of the actual angle of rotation (usually matching it, but not necessarily).
    // tg: code/modules/mob/living/living_defines.dm:74
    public float body_position = STANDING_UP;
    /// Number of degrees of rotation of a mob. 0 means no rotation, up-side facing NORTH. 90 means up-side rotated to face EAST, and so on.
    // tg: code/modules/mob/living/living_defines.dm:76  VAR_PROTECTED/lying_angle = 0
    [DmProtected]
    public float lying_angle = 0;
    /// Value of lying lying_angle before last change. TODO: Remove the need for this.
    // tg: code/modules/mob/living/living_defines.dm:78
    public float lying_prev = 0;
    /// Does the mob rotate when lying
    // tg: code/modules/mob/living/living_defines.dm:80
    public float rotate_on_lying = FALSE;
    ///Used by the resist verb, likely used to prevent players from bypassing next_move by logging in/out.
    // tg: code/modules/mob/living/living_defines.dm:82
    public float last_special = 0;

    ///A message sent when the mob dies, with the *deathgasp emote
    // tg: code/modules/mob/living/living_defines.dm:85
    public string death_message = "";
    ///A sound sent when the mob dies, with the *deathgasp emote
    // tg: code/modules/mob/living/living_defines.dm:87
    public object death_sound;

    /// Helper vars for quick access to firestacks, these should be updated every time firestacks are adjusted
    // tg: code/modules/mob/living/living_defines.dm:90
    public float on_fire = FALSE;
    // tg: code/modules/mob/living/living_defines.dm:91
    public float fire_stacks = 0;

    /**
      * Allows mobs to move through dense areas without restriction. For instance, in space or out of holder objects.
      *
      * FALSE is off, [INCORPOREAL_MOVE_BASIC] is normal, [INCORPOREAL_MOVE_SHADOW] is for ninjas
      * and [INCORPOREAL_MOVE_JAUNT] is blocked by holy water/salt
      */
    // tg: code/modules/mob/living/living_defines.dm:99
    public float incorporeal_move = FALSE;

    /// Lazylist of all quirks the mob has. These are not singletons
    // tg: code/modules/mob/living/living_defines.dm:102
    public DmList quirks;
    /// Lazylist of all typepaths of personalities the mob has.
    // tg: code/modules/mob/living/living_defines.dm:104
    public DmList personalities;

    /// Lazylist of surgery speed modifiers - id to number - 2 = 2x faster, 0.5x = 0.5x slower
    // tg: code/modules/mob/living/living_defines.dm:107
    public DmList mob_surgery_speed_mods;

    /// Used by [living/Bump()][/mob/living/proc/Bump] and [living/PushAM()][/mob/living/proc/PushAM] to prevent potential infinite loop.
    // tg: code/modules/mob/living/living_defines.dm:110
    public object now_pushing = null;

    ///The mob's latest time-of-death
    // tg: code/modules/mob/living/living_defines.dm:113
    public float timeofdeath = 0;
    ///The mob's latest time-of-death, as a station timestamp instead of world.time
    // tg: code/modules/mob/living/living_defines.dm:115
    public object station_timestamp_timeofdeath;

    /// Sets AI behavior that allows mobs to target and dismember limbs with their basic attack.
    // tg: code/modules/mob/living/living_defines.dm:118
    public float limb_destroyer = 0;

    // tg: code/modules/mob/living/living_defines.dm:120
    // (float?: the null-use grep matches, __HELPERS/paths/path.dm:295)
    public float? mob_size = MOB_SIZE_HUMAN;
    /// List of biotypes the mob belongs to. Used by diseases and reagents mainly.
    // tg: code/modules/mob/living/living_defines.dm:122
    public float mob_biotypes = MOB_ORGANIC;
    ///more or less efficiency to metabolize helpful/harmful reagents and regulate body temperature..
    // tg: code/modules/mob/living/living_defines.dm:124
    public float metabolism_efficiency = 1;
    ///does the mob have distinct limbs?(arms,legs, chest,head)
    // tg: code/modules/mob/living/living_defines.dm:126
    public float has_limbs = FALSE;

    ///How many legs does this mob have by default. This shouldn't change at runtime.
    // tg: code/modules/mob/living/living_defines.dm:129
    public float default_num_legs = 2;
    ///How many legs does this mob currently have. Should only be changed through set_num_legs()
    // tg: code/modules/mob/living/living_defines.dm:131
    public float num_legs = 2;
    ///How many usable legs this mob currently has. Should only be changed through set_usable_legs()
    // tg: code/modules/mob/living/living_defines.dm:133
    public float usable_legs = 2;

    ///How many hands does this mob have by default. This shouldn't change at runtime.
    // tg: code/modules/mob/living/living_defines.dm:136
    public float default_num_hands = 2;
    ///How many hands hands does this mob currently have. Should only be changed through set_num_hands()
    // tg: code/modules/mob/living/living_defines.dm:138
    public float num_hands = 2;
    ///How many usable hands does this mob currently have. Should only be changed through set_usable_hands()
    // tg: code/modules/mob/living/living_defines.dm:140
    public float usable_hands = 2;

    // tg: code/modules/mob/living/living_defines.dm:142 (list(): dm_init_vars above)
    public DmList pipes_shown;
    // tg: code/modules/mob/living/living_defines.dm:143
    public float last_played_vent = 0;
    /// The last direction we moved in a vent. Used to make holding two directions feel nice
    // tg: code/modules/mob/living/living_defines.dm:145
    public float last_vent_dir = 0;
    /// Cell tracker datum we use to manage the pipes around us, for faster ventcrawling
    /// Should only exist if you're in a pipe
    // tg: code/modules/mob/living/living_defines.dm:148
    public datum_cell_tracker pipetracker;
    /// Cooldown for welded vent movement messages to prevent spam
    // tg: code/modules/mob/living/living_defines.dm:150  COOLDOWN_DECLARE(welded_vent_message_cd)
    // (__DEFINES/cooldowns.dm: var/welded_vent_message_cd = 0)
    public float welded_vent_message_cd = 0;

    // tg: code/modules/mob/living/living_defines.dm:152
    public float smoke_delay = 0; ///used to prevent spam with smoke reagent reaction on mob.

    ///what icon the mob uses for speechbubbles
    // tg: code/modules/mob/living/living_defines.dm:155
    public string bubble_icon = "default";
    ///if this exists AND the normal sprite is bigger than 32x32, this is the replacement icon state (because health doll size limitations). the icon will always be screen_gen.dmi
    // tg: code/modules/mob/living/living_defines.dm:157
    public object health_doll_icon;

    // tg: code/modules/mob/living/living_defines.dm:159
    public float last_bumped = 0;
    ///if a mob's name should be appended with an id when created e.g. Mob (666)
    // tg: code/modules/mob/living/living_defines.dm:161
    public float unique_name = FALSE;
    ///the id a mob gets when it's created
    // tg: code/modules/mob/living/living_defines.dm:163
    public float identifier = 0;

    ///these will be yielded from butchering with a probability chance equal to the butcher item's effectiveness
    // tg: code/modules/mob/living/living_defines.dm:166
    public DmList butcher_results = null;
    ///these will always be yielded from butchering
    // tg: code/modules/mob/living/living_defines.dm:168
    public DmList guaranteed_butcher_results = null;
    ///effectiveness prob. is modified negatively by this amount; positive numbers make it more difficult, negative ones make it easier
    // tg: code/modules/mob/living/living_defines.dm:170
    public float butcher_difficulty = 0;

    /// How much blood the mob currently has.
    /// Don't read directly, use get_blood_volume() and get_blood_volume(apply_modifiers = TRUE).
    /// Don't write directly either, use set_blood_volume() and adjust_blood_volume().
    /// Also don't initialize this. Initialize default_blood_volume instead.
    // tg: code/modules/mob/living/living_defines.dm:176
    public float blood_volume = 0;
    /// The default blood volume of the mob. Used primarily for healing bloodloss.
    // tg: code/modules/mob/living/living_defines.dm:178
    public float default_blood_volume = 0;
    /// Lazylist of blood volume modifiers. These multiply blood volume when get_blood_volume(apply_modifiers = TRUE) is used.
    /// Use set_blood_volume_modifier(multiplier, source) and remove_blood_volume_modifier(source) to modify this.
    // tg: code/modules/mob/living/living_defines.dm:181
    public DmList blood_volume_modifiers = null;

    ///a list of all status effects the mob has
    // tg: code/modules/mob/living/living_defines.dm:184
    public DmList status_effects;
    // tg: code/modules/mob/living/living_defines.dm:185
    public DmList implants = null;

    ///used for database logging
    // tg: code/modules/mob/living/living_defines.dm:188
    public object last_words;

    /// The w_class of the holder when held.
    // tg: code/modules/mob/living/living_defines.dm:191
    public float held_w_class = WEIGHT_CLASS_NORMAL;
    ///if it can be held, can it be equipped to any slots? (think pAI's on head)
    // tg: code/modules/mob/living/living_defines.dm:193
    public float worn_slot_flags = NONE;

    // tg: code/modules/mob/living/living_defines.dm:195
    public float ventcrawl_layer = PIPING_LAYER_DEFAULT;
    // tg: code/modules/mob/living/living_defines.dm:196
    public float losebreath = 0;

    //List of active diseases
    /// list of all diseases in a mob
    // tg: code/modules/mob/living/living_defines.dm:200
    public DmList diseases;
    // tg: code/modules/mob/living/living_defines.dm:201
    public DmList disease_resistances;
    // tg: code/modules/mob/living/living_defines.dm:202
    public DmList symptom_resistances;

    ///Whether the mob is slowed down when dragging another prone mob
    // tg: code/modules/mob/living/living_defines.dm:205
    public float slowed_by_drag = TRUE;

    /// List of changes to body temperature, used by desease symtoms like fever
    // tg: code/modules/mob/living/living_defines.dm:208 (list(): dm_init_vars above)
    public DmList body_temp_changes;

    //this stuff is here to make it simple for admins to mess with custom held sprites
    ///left hand icon for holding mobs
    // tg: code/modules/mob/living/living_defines.dm:212  var/icon/held_lh (object: it holds a resource, rules 3.7)
    public object held_lh = Dm.resource("icons/mob/inhands/pets_held_lh.dmi");
    ///right hand icon for holding mobs
    // tg: code/modules/mob/living/living_defines.dm:214  var/icon/held_rh (object: it holds a resource, rules 3.7)
    public object held_rh = Dm.resource("icons/mob/inhands/pets_held_rh.dmi");
    ///what it looks like when the mob is held on your head
    // tg: code/modules/mob/living/living_defines.dm:216  var/icon/head_icon (object: it holds a resource, rules 3.7)
    public object head_icon = Dm.resource("icons/mob/clothing/head/pets_head.dmi");
    /// icon_state for holding mobs.
    // tg: code/modules/mob/living/living_defines.dm:218
    public string held_state = "";
    /// Typepath of the holder created when we're picked up
    // tg: code/modules/mob/living/living_defines.dm:220
    public Type inhand_holder_type = typeof(obj_item_mob_holder);

    ///If combat mode is on or not
    // tg: code/modules/mob/living/living_defines.dm:223
    // (float?: the null-use grep matches, datums/ai/_ai_controller.dm:614)
    public float? combat_mode = FALSE;

    /// Is this mob allowed to be buckled/unbuckled to/from things?
    // tg: code/modules/mob/living/living_defines.dm:226
    public float can_buckle_to = TRUE;

    ///The height offset of a mob's maptext due to their current size.
    // tg: code/modules/mob/living/living_defines.dm:229
    public float body_maptext_height_offset = 0;

    /// FOV view that is applied from either nativeness or traits
    // tg: code/modules/mob/living/living_defines.dm:232
    public object fov_view;
    /// Lazy list of FOV traits that will apply a FOV view when handled.
    // tg: code/modules/mob/living/living_defines.dm:234
    public DmList fov_traits;
    ///what multiplicative slowdown we get from turfs currently.
    // tg: code/modules/mob/living/living_defines.dm:236
    public float current_turf_slowdown = 0;

    /// Direction that this mob is looking at, used for the look_up and look_down procs
    // tg: code/modules/mob/living/living_defines.dm:239
    public float looking_vertically = NONE;
    ///looking holder we use for look_up and look_down. we use this over resetting to the turf because we want to glide
    // tg: code/modules/mob/living/living_defines.dm:241
    public atom_movable_looking_holder looking_holder;

    /// Living mob's mood datum
    // tg: code/modules/mob/living/living_defines.dm:244
    public datum_mood mob_mood;

    // Multiple imaginary friends!
    /// Contains the owner and all imaginary friend mobs if they exist, otherwise null
    // tg: code/modules/mob/living/living_defines.dm:248
    public DmList imaginary_group = null;

    /// What our current gravity state is. Used to avoid duplicate animates and such
    // tg: code/modules/mob/living/living_defines.dm:251
    public object gravity_state = null;

    /// How long it takes to return to 0 stam
    // tg: code/modules/mob/living/living_defines.dm:254
    public float stamina_regen_time = 10 * SECONDS;

    /// Lazylists of pixel offsets this mob is currently using
    /// Modify this via add_offsets and remove_offsets,
    /// NOT directly (and definitely avoid modifying offsets directly)
    // tg: code/modules/mob/living/living_defines.dm:259  VAR_PRIVATE/list/offsets
    [DmPrivate]
    public DmList offsets;

    /// Lazylist of martial arts this mob knows
    /// First element is the current martial art - any other elements are "saved" for if they unlearn the first one
    /// Reference handling is done by the martial arts themselves
    // tg: code/modules/mob/living/living_defines.dm:264
    public DmList martial_arts;

    /// how many tiles can this mob reach with their hands? 1 tile is adjacent.
    // tg: code/modules/mob/living/living_defines.dm:267
    public float reach_length = 1;

    /// Lazy assoc list of currently applied fishing difficulty modifiers keyed to their source
    // tg: code/modules/mob/living/living_defines.dm:270
    public DmList fishing_difficulty_mods_by_source;

    /// When less than or equal to  this distance (but not adjacent), this mob can hear parts of distant whispers, but not the entire message.
    /// When greater than this distance, this mob cannot hear anything of a whisper.
    // tg: code/modules/mob/living/living_defines.dm:274
    public float eavesdrop_range = EAVESDROP_RANGE;

    /// Reference to the unconscious appearance image that appears in place of the mob to other knocked out mobs
    // tg: code/modules/mob/living/living_defines.dm:277  VAR_FINAL/image/unconscious_appearance
    [DmFinal]
    public image unconscious_appearance;

    /// Reduces the effects of EMPs, does NOT negate them even at very high numbers
    // tg: code/modules/mob/living/living_defines.dm:280
    public float emp_protection = EMP_PROTECTION_NONE;
}
