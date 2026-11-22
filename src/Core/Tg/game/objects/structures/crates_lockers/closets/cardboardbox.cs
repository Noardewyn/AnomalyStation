// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/structures/crates_lockers/closets/cardboardbox.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the MGS "!" animation (:94) and its bookkeeping (M7x slice D, the gunpoint component plays it over a man who is held
// up), the cardboard boxes (syndicate G3: the stealth implant's box). The metal box has no procs. The var blocks are generated (D111).

// tg: code/game/objects/structures/crates_lockers/closets/cardboardbox.dm:1
public partial class obj_structure_closet_cardboard : obj_structure_closet
{
    // tg: code/game/objects/structures/crates_lockers/closets/cardboardbox.dm:37
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_SPEED_POTION_APPLIED, nameof(on_speed_potioned));
        return dot;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/cardboardbox.dm:41
    [SignalHandler]
    public virtual void on_speed_potioned(datum source = null)
    {
        move_speed_multiplier *= 0.2f;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/cardboardbox.dm:45
    public override async DmTask<object> relaymove(mob_living user = null, object direction = null)
    {
        if (Dm.truthy(opened) || Dm.truthy(move_delay) || Dm.truthy(user.incapacitated) || !isturf(loc) || !Dm.truthy(has_gravity(loc as turf)))
            return null;
        move_delay = TRUE;
        atom oldloc = loc;
        await try_step_multiz(direction);
        await user.setDir(dir);
        if (oldloc != loc)
            _addtimer(Dm.@new<datum_callback>(this, nameof(ResetMoveDelay)), Dm.num(config.Get(typeof(datum_config_entry_number_movedelay_walk_delay))) * move_speed_multiplier, file: "code/game/objects/structures/crates_lockers/closets/cardboardbox.dm", line: 53);
        else
            move_delay = FALSE;
        return null;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/cardboardbox.dm:57
    public virtual void ResetMoveDelay()
    {
        move_delay = FALSE;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/cardboardbox.dm:60
    public override async DmTask<object> before_open(mob_living user = null, object force = null)
    {
        object dot = await base.before_open(user, force);
        if (!Dm.truthy(dot))
            return FALSE;

        LAZYINITLIST(ref alerted);
        bool do_alert = alert_cooldown <= world.time && Dm.locate<mob_living>(this) != null; // COOLDOWN_FINISHED (__DEFINES/cooldowns.dm:113), inline (rules 11.1 (a))
        if (!do_alert)
            return TRUE;

        alerted.Cut(); // just in case we runtimed and the list didn't get cleared in after_open
        // Cache the list before we open the box.
        foreach (mob_living alerted_mob in Dm.each<mob_living>(viewers(7, this)))
            Dm.append(ref alerted, alerted_mob);

        return TRUE;
    }

    // tg: code/game/objects/structures/crates_lockers/closets/cardboardbox.dm:76
    public override async DmTask after_open(mob_living user = null, float? force = null)
    {
        await base.after_open(user, force);
        if (!Dm.truthy(Dm.length(alerted)))
            return;

        alert_cooldown = world.time + time_between_alerts; // COOLDOWN_START (__DEFINES/cooldowns.dm:111), inline (rules 11.1 (a))

        foreach (mob_living alerted_mob in Dm.each<mob_living>(alerted))
        {
            if (IS_UNCONSCIOUS_OR_CRIT(alerted_mob) || Dm.truthy(alerted_mob.is_blind()))
                continue;
            if (!Dm.truthy(INCAPACITATED_IGNORING(alerted_mob, INCAPABLE_RESTRAINTS)))
                alerted_mob.face_atom(this);
            alerted_mob.do_alert_animation();
        }

        alerted.Cut();
        playsound(loc, Dm.resource("sound/machines/chime.ogg"), 50, FALSE, -5);
    }
}

public partial class atom : datum
{
    /// Does the MGS ! animation
    // tg: code/game/objects/structures/crates_lockers/closets/cardboardbox.dm:94
    public virtual void do_alert_animation(object duration = null)
    {
        duration ??= 1 * SECONDS;
        mutable_appearance alert = mutable_appearance(Dm.resource("icons/obj/storage/closet.dmi"), "cardboard_special") as mutable_appearance;
        SET_PLANE_EXPLICIT(alert, ABOVE_LIGHTING_PLANE, this);
        atom_movable_flick_visual exclamation = flick_overlay_view(alert, duration) as atom_movable_flick_visual;
        exclamation.alpha = 0;
        exclamation.pixel_x = -pixel_x;
        animate(exclamation, Dm.named("pixel_z", 32), Dm.named("alpha", 255), Dm.named("time", Dm.num(duration) * 0.5f), Dm.named("easing", ELASTIC_EASING));
        animate(Dm.named("time", Dm.num(duration) * 0.35f));
        animate(Dm.named("pixel_z", 64), Dm.named("alpha", 0), Dm.named("time", Dm.num(duration) * 0.15f), Dm.named("easing", SINE_EASING));
        // We use this list to update plane values on parent z change, which is why we need the timer too
        // I'm sorry :(
        LAZYADD(ref update_on_z, exclamation);
        // Intentionally less time then the flick so we don't get weird shit
        _addtimer(Dm.@new<datum_callback>(this, nameof(forget_alert), exclamation), max(0.05f * SECONDS, Dm.num(duration) - 0.1f * SECONDS), TIMER_CLIENT_TIME, file: "code/game/objects/structures/crates_lockers/closets/cardboardbox.dm", line: 109);
    }

    // tg: code/game/objects/structures/crates_lockers/closets/cardboardbox.dm:111
    public virtual void forget_alert(atom_movable_flick_visual exclamation = null)
    {
        LAZYREMOVE(ref update_on_z, exclamation);
    }
}
