// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/mob_defines.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (vars only), written by the W4.0 contracts (rules 10.2): the var block, tg's overrides of BYOND's /mob
// defaults (dm_tg_vars, rules 10.2 "Builtin types"), the object-creating defaults (dm_init_vars) and the static
// next_mob_id (a TgState field, rules 10.6). Owned by package C in wave 4. The [DmPath] of a BYOND builtin type is on
// its engine part in src/Core/Tg/Builtin/.

/**
 * The mob, usually meant to be a creature of some type
 *
 * Has a client attached that is a living person (most of the time), although I have to admit
 * sometimes it's hard to tell they're sentient
 *
 * Has a lot of the creature game world logic, such as health etc
 */
// tg: code/modules/mob/mob_defines.dm:9
public partial class mob : atom_movable
{
    // tg: code/modules/mob/mob_defines.dm:10-22  tg's overrides of BYOND's /mob defaults (rules 10.2, "Builtin types");
    // hud_possible (:14) and faction (:24) create lists: dm_init_vars
    private partial void dm_tg_vars()
    {
        density = TRUE;
        layer = MOB_LAYER;
        abstract_type = typeof(mob);
        animate_movement = SLIDE_STEPS;
        pressure_resistance = 8;
        mouse_drag_pointer = MOUSE_ACTIVE_POINTER;
        throwforce = 10;
        blocks_emissive = EMISSIVE_BLOCK_GENERIC;
        pass_flags_self = PASSMOB;
        // we never want to hide a turf because it's not lit
        // We can rely on the lighting plane to handle that for us
        see_in_dark = 1e6f;
    }

    // tg: code/modules/mob/mob_defines.dm:14, :24, :100-193  the object-creating defaults (rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        hud_possible = Dm.list(ANTAG_HUD);
        // A list of factions that this mob is currently in, for hostile mob targeting, amongst other things
        faction = Dm.list(FACTION_NEUTRAL);
        logging = Dm.list();
        held_items = Dm.list();
        alerts = Dm.list();
        screens = Dm.list();
        client_colours = Dm.list();
        color_filter_store = Dm.list();
        // tg: code/modules/tgui/external.dm:153  /mob/var/list/tgui_open_uis = list() (M5 slice 3)
        tgui_open_uis = Dm.list();
    }

    /// The current client inhabiting this mob. Managed by login/logout
    /// This exists so we can do cleanup in logout for occasions where a client was transfere rather then destroyed
    /// We need to do this because the mob on logout never actually has a reference to client
    /// We also need to clear this var/do other cleanup in client/Destroy, since that happens before logout
    /// HHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHHH
    // tg: code/modules/mob/mob_defines.dm:30
    public client canon_client;
    /// It's like a client, but persists! Persistent clients will stick to a mob until the client in question is logged into a different mob.
    // tg: code/modules/mob/mob_defines.dm:32
    public datum_persistent_client persistent_client;

    // tg: code/modules/mob/mob_defines.dm:34
    public float shift_to_open_context_menu = TRUE;

    /// Percentage of how much rgb to max the lighting plane at
    /// This lets us brighten it without washing out color
    /// Scale from 0-100, reset off update_sight()
    // tg: code/modules/mob/mob_defines.dm:39
    public float? lighting_cutoff = LIGHTING_CUTOFF_VISIBLE;
    // Individual color max for red, we can use this to color darkness without tinting the light
    // tg: code/modules/mob/mob_defines.dm:41
    public float lighting_cutoff_red = 0;
    // Individual color max for green, we can use this to color darkness without tinting the light
    // tg: code/modules/mob/mob_defines.dm:43
    public float lighting_cutoff_green = 0;
    // Individual color max for blue, we can use this to color darkness without tinting the light
    // tg: code/modules/mob/mob_defines.dm:45
    public float lighting_cutoff_blue = 0;
    /// A list of red, green and blue cutoffs
    /// This is what actually gets applied to the mob, it's modified by things like glasses
    // tg: code/modules/mob/mob_defines.dm:48
    public DmList lighting_color_cutoffs;
    // tg: code/modules/mob/mob_defines.dm:49
    public datum_mind mind;
    // tg: code/modules/mob/mob_defines.dm:50  var/static/next_mob_id = 0  (rules 10.6: a TgState field, below)
    public static ref float next_mob_id => ref TgState.Current.mob__next_mob_id;

    /// List of movement speed modifiers applying to this mob
    // tg: code/modules/mob/mob_defines.dm:53
    public DmList movespeed_modification; //Lazy list, see mob_movespeed.dm
    /// List of movement speed modifiers ignored by this mob. List -> List (id) -> List (sources)
    // tg: code/modules/mob/mob_defines.dm:55
    public DmList movespeed_mod_immunities; //Lazy list, see mob_movespeed.dm
    /// The calculated mob speed slowdown based on the modifiers list
    // tg: code/modules/mob/mob_defines.dm:57
    public object cached_multiplicative_slowdown;
    /// List of action speed modifiers applying to this mob
    // tg: code/modules/mob/mob_defines.dm:59
    public DmList actionspeed_modification; //Lazy list, see mob_movespeed.dm
    /// List of action speed modifiers ignored by this mob. List -> List (id) -> List (sources)
    // tg: code/modules/mob/mob_defines.dm:61
    public DmList actionspeed_mod_immunities; //Lazy list, see mob_movespeed.dm
    /// The calculated mob action speed slowdown based on the modifiers list
    // tg: code/modules/mob/mob_defines.dm:63
    public object cached_multiplicative_actions_slowdown;
    /// List of action hud items the user has
    // tg: code/modules/mob/mob_defines.dm:65
    public DmList actions;
    ///Cursor icon used when holding shift over things
    // tg: code/modules/mob/mob_defines.dm:67
    public object examine_cursor_icon = Dm.resource("icons/effects/mouse_pointers/examine_pointer.dmi");

    /// Mob bitflags
    // tg: code/modules/mob/mob_defines.dm:70
    public float mob_flags = NONE;

    /// Whether a mob is alive or dead. TODO: Move this to living - Nodrak (2019, still here)
    // tg: code/modules/mob/mob_defines.dm:73
    public float stat = STABLE;

    /**
    * Whether and how a mob is incapacitated
    *
    * Normally being restrained, agressively grabbed, or in stasis counts as incapacitated
    * unless there is a flag being used to check if it's ignored
    *
    * * bitflags: (see code/__DEFINES/status_effects.dm)
    * * INCAPABLE_RESTRAINTS - if our mob is in a restraint (handcuffs)
    * * INCAPABLE_STASIS - if our mob is in stasis (stasis bed, etc.)
    * * INCAPABLE_GRAB - if our mob is being agressively grabbed
    *
    **/
    // tg: code/modules/mob/mob_defines.dm:87  VAR_FINAL/incapacitated = NONE
    [DmFinal]
    public float incapacitated = NONE;

    /* A bunch of this stuff really needs to go under their own defines instead of being globally attached to mob.
    A variable should only be globally attached to turfs/objects/whatever, when it is in fact needed as such.
    The current method unnecessarily clusters up the variable list, especially for humans (although rearranging won't really clean it up a lot but the difference will be noticable for other mobs).
    I'll make some notes on where certain variable defines should probably go.
    Changing this around would probably require a good look-over the pre-existing code.
    */

    /// The zone this mob is currently targeting
    // tg: code/modules/mob/mob_defines.dm:97
    public string zone_selected = BODY_ZONE_CHEST;

    // tg: code/modules/mob/mob_defines.dm:99
    public object computer_id;
    // tg: code/modules/mob/mob_defines.dm:100
    public DmList logging;

    /// Tick time the mob can next move
    // tg: code/modules/mob/mob_defines.dm:103
    public object next_move;

    /// What is the mobs real name (name is overridden for disguises etc)
    // tg: code/modules/mob/mob_defines.dm:106
    public object real_name = null;


    /// Default body temperature
    // tg: code/modules/mob/mob_defines.dm:110
    public float bodytemperature = BODYTEMP_NORMAL; //310.15K / 98.6F
    /// Our body temperatue as of the last process, prevents pointless work when handling alerts
    // tg: code/modules/mob/mob_defines.dm:112
    public float old_bodytemperature = 0;

    /// Hunger level of the mob
    // tg: code/modules/mob/mob_defines.dm:115
    public float nutrition = NUTRITION_LEVEL_START_MIN; // randomised in Initialize
    /// Satiation level of the mob
    // tg: code/modules/mob/mob_defines.dm:117
    public float satiety = 0; //Carbon

    /// How many ticks this mob has been over reating
    // tg: code/modules/mob/mob_defines.dm:120
    public float overeatduration = 0; // How long this guy is overeating //Carbon

    /// The last known IP of the client who was in this mob
    // tg: code/modules/mob/mob_defines.dm:123
    public object lastKnownIP;

    /// movable atom we are buckled to
    // tg: code/modules/mob/mob_defines.dm:126
    public atom_movable buckled; //Living

    //Hands
    ///What hand is the active hand
    // tg: code/modules/mob/mob_defines.dm:130
    public float active_hand_index = 1;
    /**
    * list of items held in hands
    *
    * len = number of hands, eg: 2 nulls is 2 empty hands, 1 item and 1 null is 1 full hand
    * and 1 empty hand.
    *
    * NB: contains nulls!
    *
    * `held_items[active_hand_index]` is the actively held item, but please use
    * [get_active_held_item()][/mob/proc/get_active_held_item] instead, because OOP
    */
    // tg: code/modules/mob/mob_defines.dm:142
    public DmList held_items;
    ///How many hands this mob has by default, which we divide by to know what y offset to give open inventories.
    // tg: code/modules/mob/mob_defines.dm:144
    public float default_hand_amount = 2;

    //HUD things

    /// Storage component (for mob inventory)
    // tg: code/modules/mob/mob_defines.dm:149
    public datum_storage active_storage;
    /// Active hud
    // tg: code/modules/mob/mob_defines.dm:151
    public datum_hud hud_used;

    /// Is the mob throw intent on
    // tg: code/modules/mob/mob_defines.dm:154
    public float throw_mode = THROW_MODE_DISABLED;

    /// What job does this mob have
    // tg: code/modules/mob/mob_defines.dm:157
    // (object: the V3 grep matches /datum/id_trim/job's own job var; mobs store job titles here, transform_procs.dm:149)
    public object job; //Living

    /// bitflags defining which status effects can be inflicted (replaces canknockdown, canstun, etc)
    // tg: code/modules/mob/mob_defines.dm:160
    public float status_flags = Dm.bitor(Dm.bitor(Dm.bitor(CANSTUN, CANKNOCKDOWN), CANUNCONSCIOUS), CANPUSH);

    ///Calls relay_move() to whatever this is set to when the mob tries to move
    // tg: code/modules/mob/mob_defines.dm:163
    public atom_movable remote_control;

    ///the current turf being examined in the stat panel
    // tg: code/modules/mob/mob_defines.dm:166
    public turf listed_turf;

    ///The list of people observing this mob.
    // tg: code/modules/mob/mob_defines.dm:169
    public DmList observers;

    ///List of progress bars this mob is currently seeing for actions
    // tg: code/modules/mob/mob_defines.dm:172
    public DmList progressbars; //for stacking do_after bars

    ///For storing what do_after's someone has, key = string, value = amount of interactions of that type happening.
    // tg: code/modules/mob/mob_defines.dm:175
    public DmList do_afters;

    ///Allows a datum to intercept all click calls this mob is the source of
    // tg: code/modules/mob/mob_defines.dm:178
    public datum click_intercept;

    ///The z level this mob is currently registered in
    // tg: code/modules/mob/mob_defines.dm:181
    public object registered_z;

    // tg: code/modules/mob/mob_defines.dm:183
    public float memory_throttle_time = 0;

    /// Contains [/atom/movable/screen/alert] only.
    ///
    /// On [/mob] so clientless mobs will throw alerts properly.
    // tg: code/modules/mob/mob_defines.dm:188
    public DmList alerts;
    // tg: code/modules/mob/mob_defines.dm:189
    public DmList screens;
    /// Assoc list of client_colour datum -> source it came from
    // tg: code/modules/mob/mob_defines.dm:191
    public DmList client_colours;
    /// List of filter names used in the past client color update for cleanup
    // tg: code/modules/mob/mob_defines.dm:193
    public DmList color_filter_store;
    // tg: code/modules/mob/mob_defines.dm:194
    public Type hud_type = typeof(datum_hud);

    // tg: code/modules/mob/mob_defines.dm:196
    public datum focus; //What receives our keyboard inputs. src by default

    /// Used for tracking last uses of emotes for cooldown purposes
    // tg: code/modules/mob/mob_defines.dm:199
    public DmList emotes_used;

    ///Whether the mob is updating glide size when movespeed updates or not
    // tg: code/modules/mob/mob_defines.dm:202
    public float updating_glide_size = TRUE;

    ///Override for sound_environments. If this is set the user will always hear a specific type of reverb (Instead of the area defined reverb)
    // tg: code/modules/mob/mob_defines.dm:205
    public float sound_environment_override = SOUND_ENVIRONMENT_NONE;

    /// A mock client, provided by tests and friends
    // tg: code/modules/mob/mob_defines.dm:208
    public datum_client_interface mock_client;

    // tg: code/modules/mob/mob_defines.dm:210
    public float interaction_range = 0; //how far a mob has to be to interact with something without caring about obsctruction, defaulted to 0 tiles

    ///the icon currently used for the typing indicator's bubble
    // tg: code/modules/mob/mob_defines.dm:213
    public object active_typing_indicator;
    ///the icon currently used for the thinking indicator's bubble
    // tg: code/modules/mob/mob_defines.dm:215
    public object active_thinking_indicator;

    /// A ref of the area we're taking our ambient loop from.
    // tg: code/modules/mob/mob_defines.dm:218
    public area ambience_tracked_area;

    /// Sound tokens currently playing for this mob.
    /// Managed by /datum/sound_token and the soundtoken subsystem
    // tg: code/modules/mob/mob_defines.dm:222
    public DmList sound_tokens;
}

public sealed partial class TgState
{
    // tg: code/modules/mob/mob_defines.dm:50  var/static/next_mob_id = 0
    internal float mob__next_mob_id = 0;
}
