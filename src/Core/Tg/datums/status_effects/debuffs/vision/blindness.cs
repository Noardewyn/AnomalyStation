// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/debuffs/vision/blindness.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;


/// Blindness
// tg: code/datums/status_effects/debuffs/vision/blindness.dm:6
[DmPath("/datum/status_effect/grouped/blindness")]
public partial class datum_status_effect_grouped_blindness : datum_status_effect_grouped
{
    // tg: code/datums/status_effects/debuffs/vision/blindness.dm:9-11  the var overrides of the type block (rules 10.2)
    public datum_status_effect_grouped_blindness()
    {
        // This is not "remove on fullheal" as in practice,
        // fullheal should instead remove all the sources and in turn cure this
        id = "blindness";
        tick_interval = STATUS_EFFECT_NO_TICK;
        alert_type = typeof(atom_movable_screen_alert_status_effect_blind);
    }

    // tg: code/datums/status_effects/debuffs/vision/blindness.dm:12  var/static/list/update_signals = list(...)  (rules 10.6: a TgState field, below)
    public static ref DmList update_signals => ref TgState.Current.datum_status_effect_grouped_blindness__update_signals;
    /// List of sources which prevent SIGHT_BYPASS from working
    // tg: code/datums/status_effects/debuffs/vision/blindness.dm:17  var/static/list/blocking_sources = list(...)  (rules 10.6: a TgState field, below)
    public static ref DmList blocking_sources => ref TgState.Current.datum_status_effect_grouped_blindness__blocking_sources;

    /// Helper macro, for ease of expanding checks for mobs which cannot be blinded
    /// There are no reason why these cannot be blinded, it is simply for "design reasons" (these things shouldn't be blinded)
    // tg: code/datums/status_effects/debuffs/vision/blindness.dm:3  #define CAN_BE_BLIND(mob) (!isanimal_or_basicmob(mob) && !isbrain(mob) && !isrevenant(mob))
    // (file-local, #undef at :133; temporary_blindness uses it too, so an internal member of the file's main type, rules 11.4)
    internal static bool CAN_BE_BLIND(object mob) => (!isanimal_or_basicmob(mob) && !isbrain(mob) && !isrevenant(mob));

    // tg: code/datums/status_effects/debuffs/vision/blindness.dm:22
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (!CAN_BE_BLIND(owner))
            return FALSE;

        RegisterSignals(owner, update_signals, nameof(update_blindness));
        return await base.on_apply(__extra);
    }

    // tg: code/datums/status_effects/debuffs/vision/blindness.dm:29
    public override void source_added(object source = null, params object[] __extra)
    {
        update_blindness();
    }

    // tg: code/datums/status_effects/debuffs/vision/blindness.dm:32
    public override void source_removed(object source = null, object removing = null)
    {
        if (!Dm.truthy(removing))
            update_blindness();
    }
}

// tg: code/datums/status_effects/debuffs/vision/blindness.dm:36
public partial class datum_status_effect_grouped_blindness : datum_status_effect_grouped
{
    // tg: code/datums/status_effects/debuffs/vision/blindness.dm:36
    public virtual void update_blindness()
    {
        if (!CAN_BE_BLIND(owner)) // future proofing
        {
            qdel(this);
            return;
        }

        if (!HAS_TRAIT(owner, TRAIT_SIGHT_BYPASS))
        {
            make_blind();
            return;
        }

        foreach (object blocker in Dm.each(blocking_sources))
        {
            if (Dm.truthy(owner.is_blind_from(blocker)))
            {
                make_blind();
                return;
            }
        }

        make_unblind();
    }

    // tg: code/datums/status_effects/debuffs/vision/blindness.dm:52
    public virtual void make_blind()
    {
        if (!Dm.truthy(GET_CLIENT(owner)))
        {
            RegisterSignal(owner, COMSIG_MOB_LOGIN, nameof(make_blind_on_login), @override: TRUE);
            return;
        }

        // have some extra logic to determine what overlay to use
        // by default we use the noflicker overlay
        // but if our one and only source is from "temp blindness", use flicker overlay
        object overlay_to_use = typeof(atom_movable_screen_fullscreen_blind_noflicker);
        if (Dm.eq(sources[1], Dm.initial(typeof(datum_status_effect_temporary_blindness), (datum_status_effect_temporary_blindness p) => p.id)) && length(sources) == 1)
            overlay_to_use = typeof(atom_movable_screen_fullscreen_blind);

        owner.overlay_fullscreen(id, overlay_to_use);
        // You are blind - at most, able to make out shapes near you
        owner.add_client_colour(typeof(datum_client_colour_blindness), id);
    }

    // tg: code/datums/status_effects/debuffs/vision/blindness.dm:68
    [SignalHandler]
    public virtual void make_blind_on_login(mob_living source = null)
    {

        UnregisterSignal(owner, COMSIG_MOB_LOGIN);
        make_blind();
    }

    // tg: code/datums/status_effects/debuffs/vision/blindness.dm:74
    public virtual void make_unblind()
    {
        UnregisterSignal(owner, COMSIG_MOB_LOGIN);
        owner.clear_fullscreen(id);
        owner.remove_client_colour(id);
    }

    // tg: code/datums/status_effects/debuffs/vision/blindness.dm:79
    public override async DmTask<object> on_remove()
    {
        make_unblind();
        UnregisterSignal(owner, update_signals);
        UnregisterSignal(owner, COMSIG_MOB_LOGIN);
        return await base.on_remove();
    }
}

/// This status effect handles applying a temporary blind to the mob.
// tg: code/datums/status_effects/debuffs/vision/blindness.dm:92
[DmPath("/datum/status_effect/temporary_blindness")]
public partial class datum_status_effect_temporary_blindness : datum_status_effect
{
    // tg: code/datums/status_effects/debuffs/vision/blindness.dm:93-96  the var overrides of the type block (rules 10.2)
    public datum_status_effect_temporary_blindness()
    {
        id = "temporary_blindness";
        tick_interval = 2 * SECONDS;
        alert_type = null;
        remove_on_fullheal = TRUE;
    }

    // tg: code/datums/status_effects/debuffs/vision/blindness.dm:98
    [DmParams("new_owner", "duration")]
    public override async DmTask<object> on_creation(mob_living new_owner = null, params object[] __extra)
    {
        object duration = Dm.arg(__extra, 0);
        duration ??= 10 * SECONDS;
        this.duration = Dm.num_or_null(duration);
        return await base.on_creation(new_owner, Dm.forward(__extra, duration));
    }

    // tg: code/datums/status_effects/debuffs/vision/blindness.dm:102
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (!datum_status_effect_grouped_blindness.CAN_BE_BLIND(owner))
            return FALSE;

        await owner.become_blind(id);
        return TRUE;
    }

    // tg: code/datums/status_effects/debuffs/vision/blindness.dm:109
    public override async DmTask<object> on_remove()
    {
        owner.cure_blind(id);
        return null;
    }

    // tg: code/datums/status_effects/debuffs/vision/blindness.dm:112
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (owner.stat == DEAD)
            return null;

        // Temp. blindness heals faster if our eyes are covered
        if (!Dm.truthy(owner.is_blind_from(EYES_COVERED)))
            return null;

        // Knocks 2 seconds off of our duration
        // If we should be deleted, give a message letting them know
        mob_living stored_owner = owner;
        if (Dm.truthy(remove_duration(2 * SECONDS)))
        {
            to_chat(stored_owner, span_green(I18n.T("Your eyes start to feel better!")));
            return null;
        }

        // Otherwise add a chance to let them know that it's working
        else if (SPT_PROB(5, seconds_between_ticks))
        {
            obj_item thing_covering_eyes = owner.is_eyes_covered() as obj_item;
            // "Your blindfold soothes your eyes", for example
            to_chat(owner, span_green(I18n.T("Your [] soothes your eyes.", (thing_covering_eyes?.name is var or__1 && Dm.truthy(or__1) ? or__1 : I18n.T("eye covering")))));
        }
        return null;
    }
}

// tg: code/datums/status_effects/debuffs/vision/blindness.dm:85
[DmPath("/atom/movable/screen/alert/status_effect/blind")]
public partial class atom_movable_screen_alert_status_effect_blind : atom_movable_screen_alert_status_effect
{
}

public sealed partial class TgState
{
    // tg: code/datums/status_effects/debuffs/vision/blindness.dm:12-15  /datum/status_effect/grouped/blindness/var/static/list/update_signals
    internal DmList datum_status_effect_grouped_blindness__update_signals = Dm.list(
        SIGNAL_REMOVETRAIT(TRAIT_SIGHT_BYPASS),
        SIGNAL_ADDTRAIT(TRAIT_SIGHT_BYPASS));
    // tg: code/datums/status_effects/debuffs/vision/blindness.dm:17-20  /datum/status_effect/grouped/blindness/var/static/list/blocking_sources
    internal DmList datum_status_effect_grouped_blindness__blocking_sources = Dm.list(
        QUIRK_TRAIT, // Meant to be completely immutable
        TRAIT_STATUS_EFFECT(Dm.initial(typeof(datum_status_effect_knocked_out), (datum_status_effect_knocked_out p) => p.id))); // Duh
}
