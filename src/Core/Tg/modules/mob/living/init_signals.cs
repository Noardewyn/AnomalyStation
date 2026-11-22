// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/init_signals.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/mob/living/init_signals.dm:2
public partial class mob_living : mob
{
    /// Called on [/mob/living/Initialize(mapload)], for the mob to register to relevant signals.
    // tg: code/modules/mob/living/init_signals.dm:2
    public virtual async DmTask register_init_signals()
    {
        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_KNOCKEDOUT), nameof(on_knockedout_trait_gain));
        RegisterSignal(this, SIGNAL_REMOVETRAIT(TRAIT_KNOCKEDOUT), nameof(on_knockedout_trait_loss));

        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_DEATHCOMA), nameof(on_deathcoma_trait_gain));
        RegisterSignal(this, SIGNAL_REMOVETRAIT(TRAIT_DEATHCOMA), nameof(on_deathcoma_trait_loss));

        RegisterSignals(this, Dm.list(
            SIGNAL_ADDTRAIT(TRAIT_FAKEDEATH),
            SIGNAL_REMOVETRAIT(TRAIT_FAKEDEATH),

            SIGNAL_ADDTRAIT(TRAIT_DEFIB_BLACKLISTED),
            SIGNAL_REMOVETRAIT(TRAIT_DEFIB_BLACKLISTED)
        ), nameof(update_medhud_on_signal));

        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_IMMOBILIZED), nameof(on_immobilized_trait_gain));
        RegisterSignal(this, SIGNAL_REMOVETRAIT(TRAIT_IMMOBILIZED), nameof(on_immobilized_trait_loss));

        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_FLOORED), nameof(on_floored_trait_gain));
        RegisterSignal(this, SIGNAL_REMOVETRAIT(TRAIT_FLOORED), nameof(on_floored_trait_loss));

        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_FORCED_STANDING), nameof(on_forced_standing_trait_gain));
        RegisterSignal(this, SIGNAL_REMOVETRAIT(TRAIT_FORCED_STANDING), nameof(on_forced_standing_trait_loss));

        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_HANDS_BLOCKED), nameof(on_handsblocked_trait_gain));
        RegisterSignal(this, SIGNAL_REMOVETRAIT(TRAIT_HANDS_BLOCKED), nameof(on_handsblocked_trait_loss));

        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_UI_BLOCKED), nameof(on_ui_blocked_trait_gain));
        RegisterSignal(this, SIGNAL_REMOVETRAIT(TRAIT_UI_BLOCKED), nameof(on_ui_blocked_trait_loss));

        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_PULL_BLOCKED), nameof(on_pull_blocked_trait_gain));
        RegisterSignal(this, SIGNAL_REMOVETRAIT(TRAIT_PULL_BLOCKED), nameof(on_pull_blocked_trait_loss));

        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_INCAPACITATED), nameof(on_incapacitated_trait_gain));
        RegisterSignal(this, SIGNAL_REMOVETRAIT(TRAIT_INCAPACITATED), nameof(on_incapacitated_trait_loss));

        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_RESTRAINED), nameof(on_restrained_trait_gain));
        RegisterSignal(this, SIGNAL_REMOVETRAIT(TRAIT_RESTRAINED), nameof(on_restrained_trait_loss));

        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_DEAF), nameof(on_hearing_loss));
        RegisterSignal(this, SIGNAL_REMOVETRAIT(TRAIT_DEAF), nameof(on_hearing_regain));
        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_STASIS), nameof(on_stasis_trait_gain));
        RegisterSignal(this, SIGNAL_REMOVETRAIT(TRAIT_STASIS), nameof(on_stasis_trait_loss));

        RegisterSignals(this, Dm.list(SIGNAL_ADDTRAIT(TRAIT_NODEATH), SIGNAL_REMOVETRAIT(TRAIT_NODEATH)), nameof(on_nodeath));

        RegisterSignal(this, COMSIG_MOVETYPE_FLAG_ENABLED, nameof(on_movement_type_flag_enabled));
        RegisterSignal(this, COMSIG_MOVETYPE_FLAG_DISABLED, nameof(on_movement_type_flag_disabled));

        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_SKITTISH), nameof(on_skittish_trait_gain));
        RegisterSignal(this, SIGNAL_REMOVETRAIT(TRAIT_SKITTISH), nameof(on_skittish_trait_loss));

        RegisterSignals(this, Dm.list(SIGNAL_ADDTRAIT(TRAIT_UNDENSE), SIGNAL_REMOVETRAIT(TRAIT_UNDENSE)), nameof(undense_changed));
        RegisterSignals(this, Dm.list(SIGNAL_ADDTRAIT(TRAIT_NEGATES_GRAVITY), SIGNAL_REMOVETRAIT(TRAIT_NEGATES_GRAVITY)), nameof(on_negate_gravity));
        RegisterSignals(this, Dm.list(SIGNAL_ADDTRAIT(TRAIT_IGNORING_GRAVITY), SIGNAL_REMOVETRAIT(TRAIT_IGNORING_GRAVITY)), nameof(on_ignore_gravity));
        RegisterSignals(this, Dm.list(SIGNAL_ADDTRAIT(TRAIT_FORCED_GRAVITY), SIGNAL_REMOVETRAIT(TRAIT_FORCED_GRAVITY)), nameof(on_force_gravity));
        // We hook for forced grav changes from our turf and ourselves
        // tg: :59-62  var/static/list/loc_connections = list(...): register_init_signals__loc_connections (rules 10.6)
        await AddElement(typeof(datum_element_connect_loc), register_init_signals__loc_connections);

        RegisterSignal(this, COMSIG_MOVABLE_EDIT_UNIQUE_IMMERSE_OVERLAY, nameof(edit_immerse_overlay));

        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_BASIC_HEALTH_HUD_VISIBLE), nameof(add_to_basic_health_hud));
        RegisterSignal(this, SIGNAL_REMOVETRAIT(TRAIT_BASIC_HEALTH_HUD_VISIBLE), nameof(remove_from_basic_health_hud));

        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_APATHETIC), nameof(on_apathetic_trait_gain));
        RegisterSignal(this, SIGNAL_REMOVETRAIT(TRAIT_APATHETIC), nameof(on_apathetic_trait_loss));

        RegisterSignal(this, SIGNAL_ADDTRAIT(TRAIT_MIND_TEMPORARILY_GONE), nameof(on_mind_temporarily_gone_trait_gain));
        RegisterSignal(this, SIGNAL_REMOVETRAIT(TRAIT_MIND_TEMPORARILY_GONE), nameof(on_mind_temporarily_gone_trait_loss));

        RegisterSignals(this, Dm.list(
            SIGNAL_ADDTRAIT(TRAIT_NIGHT_VISION),
            SIGNAL_REMOVETRAIT(TRAIT_NIGHT_VISION),
            SIGNAL_ADDTRAIT(TRAIT_MESON_VISION),
            SIGNAL_REMOVETRAIT(TRAIT_MESON_VISION),
            SIGNAL_ADDTRAIT(TRAIT_TRUE_NIGHT_VISION),
            SIGNAL_REMOVETRAIT(TRAIT_TRUE_NIGHT_VISION),
            SIGNAL_ADDTRAIT(TRAIT_THERMAL_VISION),
            SIGNAL_REMOVETRAIT(TRAIT_THERMAL_VISION),
            SIGNAL_ADDTRAIT(TRAIT_XRAY_VISION),
            SIGNAL_REMOVETRAIT(TRAIT_XRAY_VISION),
            SIGNAL_ADDTRAIT(TRAIT_ECHOLOCATOR),
            SIGNAL_REMOVETRAIT(TRAIT_ECHOLOCATOR)
        ), nameof(update_sight));

        RegisterSignals(this, Dm.list(SIGNAL_ADDTRAIT(TRAIT_PASSTABLE), SIGNAL_REMOVETRAIT(TRAIT_PASSTABLE)), nameof(on_passtable_trait_toggled));
        RegisterSignals(this, Dm.list(SIGNAL_ADDTRAIT(TRAIT_PASSWINDOW), SIGNAL_REMOVETRAIT(TRAIT_PASSWINDOW)), nameof(on_passwindow_trait_toggled));
    }

    // tg: code/modules/mob/living/init_signals.dm:59  /mob/living/proc/register_init_signals: var/static/list/loc_connections (rules 10.6)
    private static ref DmList register_init_signals__loc_connections => ref TgState.Current.mob_living__register_init_signals__loc_connections;

    /// Called when [TRAIT_KNOCKEDOUT] is added to the mob.
    // tg: code/modules/mob/living/init_signals.dm:95
    [SignalHandler]
    public virtual void on_knockedout_trait_gain(datum source = null)
    {
        Waiting.NoSleep(apply_status_effect(typeof(datum_status_effect_knocked_out)));

        if (stat <= SOFT_CRIT) // going into hard crit gives a similar log
            log_combat(this, this, "lost consciousness");
    }

    /// Called when [TRAIT_KNOCKEDOUT] is removed from the mob.
    // tg: code/modules/mob/living/init_signals.dm:104
    [SignalHandler]
    public virtual void on_knockedout_trait_loss(datum source = null)
    {
        remove_status_effect(typeof(datum_status_effect_knocked_out));

        if (stat <= SOFT_CRIT) // leaving hard crit gives a similar log
            log_combat(this, this, "regained consciousness");
    }

    /// Called when [TRAIT_DEATHCOMA] is added to the mob.
    // tg: code/modules/mob/living/init_signals.dm:113
    [SignalHandler]
    public virtual void on_deathcoma_trait_gain(datum source = null)
    {
        ADD_TRAIT(this, TRAIT_KNOCKEDOUT, TRAIT_DEATHCOMA);
    }

    /// Called when [TRAIT_DEATHCOMA] is removed from the mob.
    // tg: code/modules/mob/living/init_signals.dm:118
    [SignalHandler]
    public virtual void on_deathcoma_trait_loss(datum source = null)
    {
        REMOVE_TRAIT(this, TRAIT_KNOCKEDOUT, TRAIT_DEATHCOMA);
    }

    /// Updates medhud when receiving relevant signals.
    // tg: code/modules/mob/living/init_signals.dm:123
    [SignalHandler]
    public virtual void update_medhud_on_signal(datum source = null)
    {
        med_hud_set_health();
        med_hud_set_status();
    }

    /// Called when [TRAIT_IMMOBILIZED] is added to the mob.
    // tg: code/modules/mob/living/init_signals.dm:129
    [SignalHandler]
    public virtual void on_immobilized_trait_gain(datum source = null)
    {
        Dm.mask(ref mobility_flags, Dm.bitnot(MOBILITY_MOVE));
        if (Dm.truthy(Dm.bitand(living_flags, MOVES_ON_ITS_OWN)))
        {
            GLOB.move_manager.stop_looping(this); //stop mid walk //This is also really dumb
        }
    }

    /// Called when [TRAIT_IMMOBILIZED] is removed from the mob.
    // tg: code/modules/mob/living/init_signals.dm:136
    [SignalHandler]
    public virtual void on_immobilized_trait_loss(datum source = null)
    {
        Dm.combine(ref mobility_flags, MOBILITY_MOVE);
    }


    /// Called when [TRAIT_FLOORED] is added to the mob.
    // tg: code/modules/mob/living/init_signals.dm:142
    [SignalHandler]
    public virtual void on_floored_trait_gain(datum source = null)
    {
        if (Dm.truthy(buckled) && !Dm.eq(buckled.buckle_lying, NO_BUCKLE_LYING))
            return; // Handled by the buckle.
        if (HAS_TRAIT(this, TRAIT_FORCED_STANDING))
            return; // Don't go horizontal if mob has forced standing trait.
        Dm.mask(ref mobility_flags, Dm.bitnot(MOBILITY_STAND));
        Waiting.NoSleep(on_floored_start());
    }


    /// Called when [TRAIT_FLOORED] is removed from the mob.
    // tg: code/modules/mob/living/init_signals.dm:153
    [SignalHandler]
    public virtual void on_floored_trait_loss(datum source = null)
    {
        Dm.combine(ref mobility_flags, MOBILITY_STAND);
        on_floored_end();
    }

    /// Called when [TRAIT_FORCED_STANDING] is added to the mob.
    // tg: code/modules/mob/living/init_signals.dm:159
    [SignalHandler]
    public virtual void on_forced_standing_trait_gain(datum source = null)
    {

        Waiting.NoSleep(set_body_position(STANDING_UP));
        Waiting.NoSleep(set_lying_angle(0));
    }

    /// Called when [TRAIT_FORCED_STANDING] is removed from the mob.
    // tg: code/modules/mob/living/init_signals.dm:166
    [SignalHandler]
    public virtual void on_forced_standing_trait_loss(datum source = null)
    {

        if (HAS_TRAIT(this, TRAIT_FLOORED))
        {
            Waiting.NoSleep(on_fall());
            Waiting.NoSleep(set_lying_down());
        }
        else if (Dm.truthy(resting))
            Waiting.NoSleep(set_lying_down());
    }

    /// Called when [TRAIT_HANDS_BLOCKED] is added to the mob.
    // tg: code/modules/mob/living/init_signals.dm:176
    [SignalHandler]
    public virtual void on_handsblocked_trait_gain(datum source = null)
    {
        Dm.mask(ref mobility_flags, Dm.bitnot(Dm.bitor(Dm.bitor(MOBILITY_USE, MOBILITY_PICKUP), MOBILITY_STORAGE)));
        Waiting.NoSleep(on_handsblocked_start());
    }

    /// Called when [TRAIT_HANDS_BLOCKED] is removed from the mob.
    // tg: code/modules/mob/living/init_signals.dm:182
    [SignalHandler]
    public virtual void on_handsblocked_trait_loss(datum source = null)
    {
        Dm.combine(ref mobility_flags, Dm.bitor(Dm.bitor(MOBILITY_USE, MOBILITY_PICKUP), MOBILITY_STORAGE));
        on_handsblocked_end();
    }


    /// Called when [TRAIT_UI_BLOCKED] is added to the mob.
    // tg: code/modules/mob/living/init_signals.dm:189
    [SignalHandler]
    public virtual void on_ui_blocked_trait_gain(datum source = null)
    {
        Dm.mask(ref mobility_flags, Dm.bitnot(MOBILITY_UI));
        Waiting.NoSleep(update_mob_action_buttons());
    }

    /// Called when [TRAIT_UI_BLOCKED] is removed from the mob.
    // tg: code/modules/mob/living/init_signals.dm:195
    [SignalHandler]
    public virtual void on_ui_blocked_trait_loss(datum source = null)
    {
        Dm.combine(ref mobility_flags, MOBILITY_UI);
        Waiting.NoSleep(update_mob_action_buttons());
    }


    /// Called when [TRAIT_PULL_BLOCKED] is added to the mob.
    // tg: code/modules/mob/living/init_signals.dm:202
    [SignalHandler]
    public virtual void on_pull_blocked_trait_gain(datum source = null)
    {
        Dm.mask(ref mobility_flags, Dm.bitnot(MOBILITY_PULL));
        if (Dm.truthy(pulling))
            Waiting.NoSleep(stop_pulling());
        pull_force_change();
    }

    /// Called when [TRAIT_PULL_BLOCKED] is removed from the mob.
    // tg: code/modules/mob/living/init_signals.dm:210
    [SignalHandler]
    public virtual void on_pull_blocked_trait_loss(datum source = null)
    {
        Dm.combine(ref mobility_flags, MOBILITY_PULL);
        pull_force_change();
    }


    /// Called when [TRAIT_INCAPACITATED] is added to the mob.
    // tg: code/modules/mob/living/init_signals.dm:217
    [SignalHandler]
    public virtual void on_incapacitated_trait_gain(datum source = null)
    {
        add_traits(Dm.list(TRAIT_UI_BLOCKED, TRAIT_PULL_BLOCKED), TRAIT_INCAPACITATED);
        update_appearance();
        update_incapacitated();
    }

    /// Called when [TRAIT_INCAPACITATED] is removed from the mob.
    // tg: code/modules/mob/living/init_signals.dm:224
    [SignalHandler]
    public virtual void on_incapacitated_trait_loss(datum source = null)
    {
        remove_traits(Dm.list(TRAIT_UI_BLOCKED, TRAIT_PULL_BLOCKED), TRAIT_INCAPACITATED);
        update_appearance();
        update_incapacitated();
    }

    /// Called when [TRAIT_RESTRAINED] is added to the mob.
    // tg: code/modules/mob/living/init_signals.dm:231
    [SignalHandler]
    public virtual void on_restrained_trait_gain(datum source = null)
    {
        ADD_TRAIT(this, TRAIT_HANDS_BLOCKED, TRAIT_RESTRAINED);
        update_incapacitated();
    }

    /// Called when [TRAIT_RESTRAINED] is removed from the mob.
    // tg: code/modules/mob/living/init_signals.dm:237
    [SignalHandler]
    public virtual void on_restrained_trait_loss(datum source = null)
    {
        REMOVE_TRAIT(this, TRAIT_HANDS_BLOCKED, TRAIT_RESTRAINED);
        update_incapacitated();
    }

    /// Called when [TRAIT_STASIS] is added to the mob
    // tg: code/modules/mob/living/init_signals.dm:243
    [SignalHandler]
    public virtual void on_stasis_trait_gain(datum source = null)
    {
        ADD_TRAIT(this, TRAIT_TUMOR_SUPPRESSED, TRAIT_STASIS);
        update_incapacitated();
    }

    /// Called when [TRAIT_STASIS] is removed from the mob
    // tg: code/modules/mob/living/init_signals.dm:249
    [SignalHandler]
    public virtual void on_stasis_trait_loss(datum source = null)
    {
        REMOVE_TRAIT(this, TRAIT_TUMOR_SUPPRESSED, TRAIT_STASIS);
        update_incapacitated();
    }

    /// Called when [TRAIT_NODEATH] is added or removed from the mob
    // tg: code/modules/mob/living/init_signals.dm:255
    [SignalHandler]
    public virtual void on_nodeath()
    {
        update_succumb_action();
        update_stat();
    }

    /**
     * Called when traits that alter succumbing are added/removed.
     *
     * Will show or hide the succumb alert prompt.
     */
    // tg: code/modules/mob/living/init_signals.dm:265
    public virtual void update_succumb_action()
    {
        if (CAN_SUCCUMB(this) || HAS_TRAIT(this, TRAIT_SUCCUMB_OVERRIDE))
            throw_alert(ALERT_SUCCUMB, typeof(atom_movable_screen_alert_succumb));
        else
            clear_alert(ALERT_SUCCUMB);
    }

    ///From [element/movetype_handler/on_movement_type_trait_gain()]
    // tg: code/modules/mob/living/init_signals.dm:272
    [SignalHandler]
    public virtual void on_movement_type_flag_enabled(datum source = null, object flag = null, object old_movement_type = null)
    {
        // DM passes FALSE; /mob/proc/update_movespeed declares no parameter (modules/movespeed/_movespeed_modifier.dm:175),
        // so the extra argument is dropped (rules 7.3).
        update_movespeed();
    }

    ///From [element/movetype_handler/on_movement_type_trait_loss()]
    // tg: code/modules/mob/living/init_signals.dm:277
    [SignalHandler]
    public virtual void on_movement_type_flag_disabled(datum source = null, object flag = null, object old_movement_type = null)
    {
        // DM passes FALSE, dropped as above (rules 7.3).
        update_movespeed();
    }


    /// Called when [TRAIT_SKITTISH] is added to the mob.
    // tg: code/modules/mob/living/init_signals.dm:283
    [SignalHandler]
    public virtual void on_skittish_trait_gain(datum source = null)
    {
        // tg: :285  AddElement(/datum/element/skittish)
        // GAP(lead): /datum/element/skittish (datums/elements/skittish.dm) is not declared (a quirk and some species, M9).
        Dm.NotTranslated("code/modules/mob/living/init_signals.dm:285", "on_skittish_trait_gain: AddElement(/datum/element/skittish) (the skittish element, M10+)");
    }

    /// Called when [TRAIT_SKITTISH] is removed from the mob.
    // tg: code/modules/mob/living/init_signals.dm:288
    [SignalHandler]
    public virtual void on_skittish_trait_loss(datum source = null)
    {
        // tg: :290  RemoveElement(/datum/element/skittish)
        // GAP(lead): as above (M9).
        Dm.NotTranslated("code/modules/mob/living/init_signals.dm:290", "on_skittish_trait_loss: RemoveElement(/datum/element/skittish) (the skittish element, M10+)");
    }

    /// Called when [TRAIT_NEGATES_GRAVITY] is gained or lost
    // tg: code/modules/mob/living/init_signals.dm:293
    [SignalHandler]
    public virtual void on_negate_gravity(datum source = null)
    {
        if (!isgroundlessturf(loc))
        {
            if (HAS_TRAIT(this, TRAIT_NEGATES_GRAVITY))
                ADD_TRAIT(this, TRAIT_IGNORING_GRAVITY, IGNORING_GRAVITY_NEGATION);
            else
                REMOVE_TRAIT(this, TRAIT_IGNORING_GRAVITY, IGNORING_GRAVITY_NEGATION);
        }
    }

    /// Called when [TRAIT_IGNORING_GRAVITY] is gained or lost
    // tg: code/modules/mob/living/init_signals.dm:302
    [SignalHandler]
    public virtual void on_ignore_gravity(datum source = null)
    {
        Waiting.NoSleep(refresh_gravity());
    }

    /// Called when [TRAIT_FORCED_GRAVITY] is gained or lost
    // tg: code/modules/mob/living/init_signals.dm:307
    [SignalHandler]
    public virtual void on_force_gravity(datum source = null)
    {
        Waiting.NoSleep(refresh_gravity());
    }

    /// Called when our loc's [TRAIT_FORCED_GRAVITY] is gained or lost
    // tg: code/modules/mob/living/init_signals.dm:312
    [SignalHandler]
    public virtual void on_loc_force_gravity(datum source = null)
    {
        Waiting.NoSleep(refresh_gravity());
    }

    // tg: code/modules/mob/living/init_signals.dm:316
    [SignalHandler]
    public virtual void edit_immerse_overlay(datum source = null, atom_movable_immerse_mask effect_relay = null)
    {
        effect_relay.transform = ((matrix)effect_relay.transform).Scale(1 / current_size);
        effect_relay.transform = ((matrix)effect_relay.transform).Turn(-lying_angle);
    }

    /// Called when [TRAIT_UNDENSE] is gained or lost
    // tg: code/modules/mob/living/init_signals.dm:322
    [SignalHandler]
    public virtual void undense_changed(datum source = null)
    {
        update_density();
    }

    ///Called when [TRAIT_DEAF] is added to the mob.
    // tg: code/modules/mob/living/init_signals.dm:327
    [SignalHandler]
    public virtual void on_hearing_loss(params object[] __extra)
    {
        refresh_looping_ambience(); // tg: code/modules/mob/living/init_signals.dm:329
        stop_sound_channel(CHANNEL_AMBIENCE); // tg: code/modules/mob/living/init_signals.dm:330
    }

    ///Called when [TRAIT_DEAF] is added to the mob.
    // tg: code/modules/mob/living/init_signals.dm:333
    [SignalHandler]
    public virtual void on_hearing_regain()
    {
        refresh_looping_ambience(); // tg: code/modules/mob/living/init_signals.dm:335
    }

    /// When gaining [TRAIT_BASIC_HEALTH_HUD_VISIBLE], add to the basic health hud
    // tg: code/modules/mob/living/init_signals.dm:338
    [SignalHandler]
    public virtual void add_to_basic_health_hud(datum source = null)
    {
        datum_atom_hud_data_human_medical_basic hud = GLOB.huds[DATA_HUD_MEDICAL_BASIC] as datum_atom_hud_data_human_medical_basic;
        hud.add_atom_to_hud(this);
    }

    /// When losing [TRAIT_BASIC_HEALTH_HUD_VISIBLE], remove from the basic health hud
    // tg: code/modules/mob/living/init_signals.dm:344
    [SignalHandler]
    public virtual void remove_from_basic_health_hud(datum source = null)
    {
        datum_atom_hud_data_human_medical_basic hud = GLOB.huds[DATA_HUD_MEDICAL_BASIC] as datum_atom_hud_data_human_medical_basic;
        hud.remove_atom_from_hud(this);
    }

    /// Called when [TRAIT_APATHETIC] is added to the mob.
    // tg: code/modules/mob/living/init_signals.dm:350
    [SignalHandler]
    public virtual void on_apathetic_trait_gain(datum source = null)
    {
        mob_mood?.update_mood();
        if (mob_mood != null)
            Waiting.NoSleep(mob_mood.set_sanity(SANITY_NEUTRAL));
    }

    /// Called when [TRAIT_APATHETIC] is removed from the mob.
    // tg: code/modules/mob/living/init_signals.dm:356
    [SignalHandler]
    public virtual void on_apathetic_trait_loss(datum source = null)
    {
        mob_mood?.update_mood();
    }

    /// Called when [TRAIT_MIND_TEMPORARILY_GONE] is added to the mob.
    // tg: code/modules/mob/living/init_signals.dm:361
    [SignalHandler]
    public virtual void on_mind_temporarily_gone_trait_gain(datum source = null)
    {
        med_hud_set_status();
    }

    /// Called when [TRAIT_MIND_TEMPORARILY_GONE] is removed from the mob.
    // tg: code/modules/mob/living/init_signals.dm:366
    [SignalHandler]
    public virtual void on_mind_temporarily_gone_trait_loss(datum source = null)
    {
        med_hud_set_status();
    }

    /// Called when [TRAIT_PASSTABLE] is added/removed to/from the mob.
    // tg: code/modules/mob/living/init_signals.dm:371
    [SignalHandler]
    public virtual void on_passtable_trait_toggled(datum source = null)
    {
        if (HAS_TRAIT(this, TRAIT_PASSTABLE))
            Dm.combine(ref pass_flags, PASSTABLE);
        else
            Dm.mask(ref pass_flags, Dm.bitnot(PASSTABLE));
    }

    /// Called when [TRAIT_PASSWINDOW] is added/removed to/from the mob.
    // tg: code/modules/mob/living/init_signals.dm:379
    [SignalHandler]
    public virtual void on_passwindow_trait_toggled(datum source = null)
    {
        if (HAS_TRAIT(this, TRAIT_PASSWINDOW))
            Dm.combine(ref pass_flags, PASSWINDOW);
        else
            Dm.mask(ref pass_flags, Dm.bitnot(PASSWINDOW));
    }
}

public sealed partial class TgState
{
    // tg: code/modules/mob/living/init_signals.dm:59  /mob/living/proc/register_init_signals: var/static/list/loc_connections = list(...)
    internal DmList mob_living__register_init_signals__loc_connections = Dm.list(
        Dm.kv(SIGNAL_ADDTRAIT(TRAIT_FORCED_GRAVITY), nameof(mob_living.on_loc_force_gravity)),
        Dm.kv(SIGNAL_REMOVETRAIT(TRAIT_FORCED_GRAVITY), nameof(mob_living.on_loc_force_gravity))
    );
}
