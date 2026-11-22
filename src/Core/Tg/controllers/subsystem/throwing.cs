// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/throwing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/controllers/subsystem/throwing.dm:5 SUBSYSTEM_DEF(throwing)
// The var block (name, priority, wait = 1, ss_flags = SS_NO_INIT | SS_TICKER, runlevels, currentrun, processing) is
// generated (types-converter.md 3.2, HandProcs), with the [DmPath] (rules 2.3: this part declares no var).
public partial class datum_controller_subsystem_throwing : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/throwing.dm:1
    internal const float MAX_THROWING_DIST = 1280; // 5 z-levels on default width
    // tg: code/controllers/subsystem/throwing.dm:2
    internal const float MAX_TICKS_TO_MAKE_UP = 3; //how many missed ticks will we attempt to make up for this run.

    // The New() that SUBSYSTEM_DEF(throwing) defines (__DEFINES/MC.dm:93-96), with NEW_SS_GLOBAL(SSthrowing)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/throwing.dm:5
    public override async DmTask New(params object[] __extra)
    {
        if (SSthrowing != this)
        {
            if (SSthrowing is datum_controller_subsystem_throwing)
            {
                Recover();
                qdel(SSthrowing);
            }
            SSthrowing = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/throwing.dm:14
    public override object stat_entry(object msg = null)
    {
        Dm.Deferred("code/controllers/subsystem/throwing.dm:14", "stat_entry: the MC stat panel line (stat panels, translation-rules.md 13)");
        return null;
    }

    // tg: code/controllers/subsystem/throwing.dm:18
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        resumed ??= 0;
        if (!Dm.truthy(resumed))
            this.currentrun = processing.Copy();

        //cache for sanic speed (lists are references anyways)
        DmList currentrun = this.currentrun;

        while (Dm.truthy(length(currentrun)))
        {
            atom_movable AM = currentrun[currentrun.len] as atom_movable;
            datum_thrownthing TT = currentrun[AM] as datum_thrownthing;
            currentrun.len--;
            if (QDELETED(AM) || QDELETED(TT))
            {
                Dm.remove(ref processing, AM);
                if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                    return;
                continue;
            }

            await TT.tick();

            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK
                return;
        }

        currentrun = null;
    }

}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/throwing.dm:5  SUBSYSTEM_DEF(throwing): GLOBAL_REAL(SSthrowing, /datum/controller/subsystem/throwing)
    internal datum_controller_subsystem_throwing SSthrowing;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_throwing SSthrowing => ref TgState.Current.SSthrowing;
}

// tg: code/controllers/subsystem/throwing.dm:43
// The var block (throwing.dm:44-93) is generated (types-converter.md 3.2).
[DmPath("/datum/thrownthing")]
public partial class datum_thrownthing : datum
{
    // tg: code/controllers/subsystem/throwing.dm:95
    [DmParams("thrownthing", "target", "init_dir", "maxrange", "speed", "thrower", "diagonals_first", "force", "gentle", "callback", "target_zone")]
    public override async DmTask New(params object[] __extra)
    {
        object thrownthing = Dm.arg(__extra, 0);
        object target = Dm.arg(__extra, 1);
        object init_dir = Dm.arg(__extra, 2);
        object maxrange = Dm.arg(__extra, 3);
        object speed = Dm.arg(__extra, 4);
        object thrower = Dm.arg(__extra, 5);
        object diagonals_first = Dm.arg(__extra, 6);
        object force = Dm.arg(__extra, 7);
        object gentle = Dm.arg(__extra, 8);
        object callback = Dm.arg(__extra, 9);
        object target_zone = Dm.arg(__extra, 10);
        await base.New(Dm.forward(__extra, thrownthing, target, init_dir, maxrange, speed, thrower, diagonals_first, force, gentle, callback, target_zone));
        this.thrownthing = thrownthing as atom_movable;
        RegisterSignal(this.thrownthing, COMSIG_QDELETING, nameof(on_thrownthing_qdel));
        this.starting_turf = get_turf(thrownthing);
        this.target_turf = get_turf(target);
        if (this.target_turf != target)
            this.initial_target = WEAKREF(target as datum) as datum_weakref;
        this.init_dir = init_dir;
        this.maxrange = maxrange;
        this.speed = speed;
        if (Dm.truthy(thrower))
            this.thrower = WEAKREF(thrower as datum) as datum_weakref;
        this.diagonals_first = diagonals_first;
        this.force = Dm.num(force);
        this.gentle = Dm.num(gentle);
        this.callback = callback as datum_callback;
        this.target_zone = target_zone;
    }

    // tg: code/controllers/subsystem/throwing.dm:115
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref SSthrowing.processing, thrownthing);
        Dm.remove(ref SSthrowing.currentrun, thrownthing);
        thrownthing.throwing = null;
        thrownthing = null;
        thrower = null;
        initial_target = null;
        callback = null;
        return base.Destroy(force);
    }

    ///Defines the datum behavior on the thrownthing's qdeletion event.
    // tg: code/controllers/subsystem/throwing.dm:125
    [SignalHandler]
    public virtual void on_thrownthing_qdel(atom_movable source = null, object force = null)
    {
        qdel(this);
    }

    /// Returns the thrower, or null
    // tg: code/controllers/subsystem/throwing.dm:131
    public virtual object get_thrower()
    {
        object dot = null;
        dot = thrower?.resolve();
        if (dot == null)
            thrower = null;
        return dot;
    }

    // tg: code/controllers/subsystem/throwing.dm:136
    public virtual async DmTask tick()
    {
        atom_movable AM = thrownthing;
        if (!isturf(AM.loc) || !Dm.truthy(AM.throwing))
        {
            finalize();
            return;
        }

        if (Dm.truthy(paused))
        {
            delayed_time += world.time - Dm.num(last_move);
            return;
        }

        // DEVIATION(DV416): a thing that is no mob flies a ballistic arc; the seam makes tg's moves and checks along it
        if (FlightSeam.Flies(AM))
        {
            await FlightSeam.ThrowTick(this);
            return;
        }

        atom_movable actual_target = initial_target?.resolve() as atom_movable;
        atom thrower = get_thrower() as atom;

        if (Dm.truthy(dist_travelled)) //to catch sneaky things moving on our tile while we slept
        {
            foreach (atom_movable obstacle in Dm.each_cast<atom_movable>(get_turf(thrownthing)))
            {
                if (obstacle == thrownthing || (obstacle == thrower && !ismob(thrownthing)))
                    continue;
                if (ismob(obstacle) && Dm.truthy(Dm.bitand(thrownthing.pass_flags, PASSMOB)) && (obstacle != actual_target))
                    continue;
                if (Dm.truthy(Dm.bitand(obstacle.pass_flags_self, LETPASSTHROW)))
                    continue;
                if (obstacle == actual_target || (Dm.truthy(obstacle.density) && !Dm.truthy(Dm.bitand(obstacle.flags_1, ON_BORDER_1)) && !Dm.@in(obstacle, AM.buckled_mobs) && !Dm.@in(AM, obstacle.buckled_mobs)))
                {
                    finalize(TRUE, obstacle);
                    return;
                }
            }
        }

        atom step = null;

        last_move = world.time;

        //calculate how many tiles to move, making up for any missed ticks.
        float tilestomove = CEILING(min(((((world.time + world.tick_lag) - Dm.num(start_time) + delayed_time) * Dm.num(speed)) - (Dm.truthy(dist_travelled) ? dist_travelled : -1)), Dm.num(speed) * datum_controller_subsystem_throwing.MAX_TICKS_TO_MAKE_UP) * (world.tick_lag * SSthrowing.wait), 1);
        while (tilestomove-- > 0)
        {
            if ((dist_travelled >= Dm.num(maxrange) || AM.loc == target_turf) && Dm.truthy(AM.has_gravity(AM.loc as turf)))
            {
                finalize();
                return;
            }

            if (dist_travelled <= max(Dm.num(dist_x), Dm.num(dist_y))) //if we haven't reached the target yet we home in on it, otherwise we use the initial direction
                step = get_step(AM, get_dir(AM, target_turf));
            else
                step = get_step(AM, Dm.num(init_dir));

            if (!Dm.truthy(pure_diagonal) && !Dm.truthy(diagonals_first)) // not a purely diagonal trajectory and we don't want all diagonal moves to be done first
            {
                if (Dm.num(diagonal_error) >= 0 && max(Dm.num(dist_x), Dm.num(dist_y)) - dist_travelled != 1) //we do a step forward unless we're right before the target
                    step = get_step(AM, Dm.num(dx));
                diagonal_error = Dm.num(diagonal_error) + ((Dm.num(diagonal_error) < 0) ? Dm.num(dist_x) / 2 : -Dm.num(dist_y));
            }

            if (!Dm.truthy(step)) // going off the edge of the map makes get_step return null, don't let things go off the edge
            {
                finalize();
                return;
            }

            if (!Dm.truthy(await AM.Move(step, get_dir(AM, step), DELAY_TO_GLIDE_SIZE(1 / Dm.num(speed))))) // we hit something during our move...
            {
                if (Dm.truthy(AM.throwing)) // ...but finalize() wasn't called on Bump() because of a higher level definition that doesn't always call parent.
                    finalize();
                return;
            }

            dist_travelled++;

            if (Dm.truthy(actual_target) && !Dm.truthy(Dm.bitand(actual_target.pass_flags_self, LETPASSTHROW)) && actual_target.loc == AM.loc) // we crossed a movable with no density (e.g. a mouse or APC) we intend to hit anyway.
            {
                finalize(TRUE, actual_target);
                return;
            }

            if (dist_travelled > datum_controller_subsystem_throwing.MAX_THROWING_DIST)
            {
                finalize();
                return;
            }
        }
    }

    // tg: code/controllers/subsystem/throwing.dm:201  (set waitfor = FALSE: the wrapper of waiting-model.md 5.4)
    public virtual void finalize(float? hit = null, object target = null)
    {
        Waiting.WaitforFalse(finalize__body(hit, target));
    }

    private async DmTask finalize__body(float? hit, object target)
    {
        hit ??= FALSE;
        //done throwing, either because it hit something or it finished moving
        if (!Dm.truthy(thrownthing))
            return;
        thrownthing.throwing = null;
        float drift_force = Dm.num(speed);
        if (isitem(thrownthing))
        {
            obj_item thrownitem = thrownthing as obj_item;
            // WEIGHT_TO_NEWTONS(w_class) (__HELPERS/movement.dm:2): 0.2 NEWTONS + w_class * 0.4 NEWTONS, inline
            drift_force *= 0.2f * NEWTONS + thrownitem.w_class * 0.4f * NEWTONS;
        }
        if (!Dm.truthy(hit))
        {
            foreach (atom_movable obstacle in Dm.each_cast<atom_movable>(get_turf(thrownthing))) //looking for our target on the turf we land on.
            {
                if (obstacle == target)
                {
                    hit = TRUE;
                    thrownthing.throw_impact(obstacle, this);
                    if (QDELETED(thrownthing)) //throw_impact can delete things, such as glasses smashing
                        return; //deletion should already be handled by on_thrownthing_qdel()
                    break;
                }
            }
            if (!Dm.truthy(hit))
            {
                thrownthing.throw_impact(get_turf(thrownthing), this);  // we haven't hit something yet and we still must, let's hit the ground.
                if (QDELETED(thrownthing)) //throw_impact can delete things, such as glasses smashing
                    return; //deletion should already be handled by on_thrownthing_qdel()
                await thrownthing.newtonian_move(delta_to_angle__local(dist_x, dist_y), drift_force: drift_force);
            }
        }
        else
            await thrownthing.newtonian_move(delta_to_angle__local(dist_x, dist_y), drift_force: drift_force);

        if (Dm.truthy(target))
        {
            thrownthing.throw_impact(target as atom, this);
            if (QDELETED(thrownthing)) //throw_impact can delete things, such as glasses smashing
                return; //deletion should already be handled by on_thrownthing_qdel()
        }

        if (Dm.truthy(callback))
        {
            await callback.Invoke();
            // Same can happen in the callback
            if (QDELETED(thrownthing))
                return;
        }

        if (!Dm.truthy(thrownthing.currently_z_moving)) // I don't think you can zfall while thrown but hey, just in case.
        {
            turf T = get_turf(thrownthing);
            if (T != null)
                await T.zFall(thrownthing);
        }

        if (Dm.truthy(thrownthing))
        {
            if (Dm.truthy(thrownthing._listen_lookup?[COMSIG_MOVABLE_THROW_LANDED]))
                thrownthing._SendSignal(COMSIG_MOVABLE_THROW_LANDED, Dm.list(thrownthing, this));
            turf landed_turf = get_turf(thrownthing);
            if (Dm.truthy(landed_turf))
            {
                if (Dm.truthy(landed_turf._listen_lookup?[COMSIG_TURF_MOVABLE_THROW_LANDED]))
                    landed_turf._SendSignal(COMSIG_TURF_MOVABLE_THROW_LANDED, Dm.list(landed_turf, thrownthing));
            }
        }

        qdel(this);
    }

    // GAP(lead): tg's global proc delta_to_angle (__HELPERS/maths.dm:10) has no GlobalProcs member yet; its body, a local
    // copy for finalize (the I1 GlobalProcs cleanup moves it).
    // tg: code/__HELPERS/maths.dm:10
    private static float delta_to_angle__local(object x, object y)
    {
        float dot = 0;
        if (!Dm.truthy(y))
            return (Dm.num(x) >= 0) ? 90 : 270;
        dot = arctan(Dm.num(x) / Dm.num(y));
        if (Dm.num(y) < 0)
            dot += 180;
        else if (Dm.num(x) < 0)
            dot += 360;
        return dot;
    }
}
