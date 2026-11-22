// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/effect_system/effects_trail.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M4c slice 6: the jetpack's ion trail). The steam, ion and smoke trails and their particles are var-only;
// the steam particle itself (effects_water.dm) waits for its own users. The var blocks are generated (D111).

/////////////////////////////////////////////
//////// Attach a trail to any object, that spawns when it moves (like for the jetpack)
/// just pass in the object to attach it to in set_up
/// Then do start() to start it and stop() to stop it, obviously
/// and don't call start() in a loop that will be repeated otherwise it'll get spammed!
/////////////////////////////////////////////

// tg: code/game/objects/effects/effect_system/effects_trail.dm:9
public partial class datum_effect_system_trail_follow : datum_effect_system
{
    // tg: code/game/objects/effects/effect_system/effects_trail.dm:29
    [DmParams("location")]
    public override async DmTask New(params object[] __extra)
    {
        object location = Dm.arg<object>(__extra, 0);
        await base.New(__extra);
        attach(location as atom);
        // oldposition = location: tg stores the argument as given, and the jetpack passes its user (jetpack.dm:94), a mob in a
        // var typed /turf; the field here is a turf, so a mob reads as null. Nothing reads it before start() sets it to the
        // holder's turf (start is what every caller does next).
        oldposition = location as turf;
    }

    // tg: code/game/objects/effects/effect_system/effects_trail.dm:34
    public override object Destroy(float? force = null)
    {
        oldposition = null;
        stop();
        return base.Destroy(force);
    }

    // tg: code/game/objects/effects/effect_system/effects_trail.dm:39
    public virtual object stop()
    {
        oldposition = null;
        STOP_PROCESSING(SSfastprocess, this);
        active = FALSE;
        return TRUE;
    }

    // tg: code/game/objects/effects/effect_system/effects_trail.dm:45
    public override async DmTask<object> start(params object[] __extra)
    {
        oldposition = get_turf(holder);
        if (!Dm.truthy(check_conditions()))
            return FALSE;
        if (Dm.truthy(auto_process))
            START_PROCESSING(SSfastprocess, this);
        active = TRUE;
        return TRUE;
    }

    // tg: code/game/objects/effects/effect_system/effects_trail.dm:54
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        generate_effect();
        return null;
    }

    // tg: code/game/objects/effects/effect_system/effects_trail.dm:57
    public virtual object generate_effect()
    {
        if (!Dm.truthy(check_conditions()))
            return stop();

        if (!Dm.truthy(oldposition) || oldposition == get_turf(holder))
        {
            oldposition = get_turf(holder);
            return null;
        }

        if (Dm.truthy(nograv_required) && Dm.truthy(oldposition.has_gravity()))
        {
            oldposition = get_turf(holder);
            return null;
        }

        obj_effect particle = Dm.@new(effect_type, oldposition) as obj_effect;
        set_dir(particle);
        if (Dm.truthy(fade))
        {
            flick(fadetype, particle);
            particle.icon_state = "";
        }

        if (Dm.truthy(qdel_in_time))
        {
            // QDEL_IN(particle, qdel_in_time) (__DEFINES/qdel.dm:59), expanded inline (rules 11.1 (f))
            _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), (qdel_in_time > GC_FILTER_QUEUE ? (object)WEAKREF(particle) : particle)), qdel_in_time, file: "code/game/objects/effects/effect_system/effects_trail.dm", line: 76);
        }
        return null;
    }

    // tg: code/game/objects/effects/effect_system/effects_trail.dm:78
    public virtual object check_conditions()
    {
        if (!Dm.truthy(get_turf(holder)))
            return FALSE;
        return TRUE;
    }

    // tg: code/game/objects/effects/effect_system/effects_trail.dm:83
    public virtual void set_dir(obj_effect effect = null)
    {
        // DEVIATION(DV15): tg's mob faces the way it steps (client/Move turns it), so holder.dir is the flight's direction. A capsule
        // mob's crossing keeps the view's dir (the locomotion bridge's Move, update_dir FALSE; a weightless one's too since DV69): its
        // particle takes the step's direction instead, from the turf it left (pre_move_react's oldposition) to the one it is on, as
        // tg's turned mob would give it. Every other holder keeps tg's line.
        if (holder is mob flier && LocomotionSeam.IsRegistered(flier) && oldposition != null)
        {
            float stepped = get_dir(oldposition, get_turf(holder));
            if (stepped != 0)
            {
                Waiting.NoSleep(effect.setDir(stepped));
                return;
            }
        }
        Waiting.NoSleep(effect.setDir(holder.dir));
    }
}
