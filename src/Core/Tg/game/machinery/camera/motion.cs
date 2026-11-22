// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/camera/motion.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI4 (D326): the motion-sensitive cameras, but the thunderdome's (CentCom's arena: its entertainment telescreens' notify is
// not ported).

// tg: code/game/machinery/camera/motion.dm:1
public partial class obj_machinery_camera : obj_machinery
{
    // tg: code/game/machinery/camera/motion.dm:1
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        // motion camera event loop
        if (!Dm.truthy(isMotion()))
            return PROCESS_KILL; // FIXME: This is never undone if the camera gets upgraded to a motion camera
        if (Dm.truthy(Dm.bitand(machine_stat, EMPED)))
            return null;
        if (detectTime > 0)
        {
            float elapsed = world.time - detectTime;
            if (elapsed > alarm_delay)
                triggerAlarm();
        }
        else if (detectTime == -1)
        {
            foreach (datum_weakref targetref in Dm.each<datum_weakref>(getTargetList().Copy()))
            {
                mob target = targetref.resolve() as mob;
                if (QDELETED(target) || Dm.eq(target.stat, DEAD) || (area_motion == null && !Dm.truthy(in_range(this, target))))
                {
                    //If not part of a monitored area and the camera is not in range or the target is dead
                    lost_target(target);
                }
            }
        }
        return null;
    }

    // tg: code/game/machinery/camera/motion.dm:19
    public virtual DmList getTargetList()
    {
        if (area_motion != null)
            return area_motion.motion_targets;
        return localMotionTargets;
    }

    // tg: code/game/machinery/camera/motion.dm:24
    public virtual bool new_target(mob target = null)
    {
        if (isAI(target))
            return false;
        if (detectTime == 0)
            detectTime = world.time; // start the clock
        DmList targets = getTargetList();
        object target_ref = WEAKREF(target); // targets |= WEAKREF(target)
        if (!Dm.@in(target_ref, targets))
            targets.Add(target_ref);
        return true;
    }

    // tg: code/game/machinery/camera/motion.dm:33
    public virtual void lost_target(mob target = null)
    {
        DmList targets = getTargetList();
        Dm.remove(ref targets, WEAKREF(target));
        if (!Dm.truthy(length(targets)))
            cancelAlarm();
    }

    // tg: code/game/machinery/camera/motion.dm:39
    public override object Destroy(float? force = null)
    {
        localMotionTargets = null;
        cancelAlarm();
        return Destroy__d0(force);
    }

    // tg: code/game/machinery/camera/motion.dm:44
    public virtual bool cancelAlarm()
    {
        if (detectTime == -1 && Dm.truthy(camera_enabled))
            alarm_manager.clear_alarm(ALARM_MOTION);
        detectTime = 0;
        return true;
    }

    // tg: code/game/machinery/camera/motion.dm:50
    public virtual bool triggerAlarm()
    {
        if (!Dm.truthy(detectTime))
            return false;
        if (Dm.truthy(camera_enabled))
        {
            if (Dm.truthy(alarm_manager.send_alarm(ALARM_MOTION, this, this)))
                visible_message(span_warning(I18n.T("A red light flashes on []!", this)));
        }
        detectTime = -1;
        return true;
    }

    // tg: code/game/machinery/camera/motion.dm:59
    public override async DmTask<object> HasProximity(atom_movable proximity_check_mob = null)
    {
        // Motion cameras outside of an "ai monitored" area will use this to detect stuff.
        if (area_motion == null)
        {
            if (isliving(proximity_check_mob))
                new_target(proximity_check_mob as mob);
        }
        return null;
    }
}
