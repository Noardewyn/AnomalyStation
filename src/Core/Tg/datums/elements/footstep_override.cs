// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/footstep_override.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4b audio, slice 4: the whole file. The var block (clawfootstep, barefootstep, heavyfootstep, footstep, priority,
// occupied_turfs) and the element flags are generated.

///When attached, the footstep sound played by the footstep element will be replaced by this one's
// tg: code/datums/elements/footstep_override.dm:2
public partial class datum_element_footstep_override : datum_element
{
    // tg: code/datums/elements/footstep_override.dm:21
    [DmParams("target", "clawfootstep", "barefootstep", "heavyfootstep", "footstep", "priority")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object clawfootstep = Dm.arg(__extra, 0);
        object barefootstep = Dm.arg(__extra, 1);
        object heavyfootstep = Dm.arg(__extra, 2);
        object footstep = Dm.arg(__extra, 3);
        object priority = Dm.arg(__extra, 4);
        clawfootstep ??= FOOTSTEP_HARD_CLAW;
        barefootstep ??= FOOTSTEP_HARD_BAREFOOT;
        heavyfootstep ??= FOOTSTEP_GENERIC_HEAVY;
        footstep ??= FOOTSTEP_FLOOR;
        priority ??= STEP_SOUND_NO_PRIORITY;
        object dot = null;
        dot = await base.Attach(target, Dm.forward(__extra, clawfootstep, barefootstep, heavyfootstep, footstep, priority));
        if (!ismovable(target))
            return ELEMENT_INCOMPATIBLE;

        this.clawfootstep = clawfootstep;
        this.barefootstep = barefootstep;
        this.heavyfootstep = heavyfootstep;
        this.footstep = footstep;
        this.priority = priority;

        atom_movable movable = (atom_movable)target;
        RegisterSignal(movable, COMSIG_MOVABLE_MOVED, nameof(on_moved));
        if (isturf(movable.loc))
            occupy_turf(movable, (turf)movable.loc);
        return dot;
    }

    // tg: code/datums/elements/footstep_override.dm:36
    [DmParams("source")]
    public override object Detach(datum source = null, params object[] __extra)
    {
        atom_movable movable = (atom_movable)source;
        UnregisterSignal(movable, COMSIG_MOVABLE_MOVED);
        if (isturf(movable.loc))
            vacate_turf(movable, (turf)movable.loc);
        return base.Detach(source, __extra);
    }

    // tg: code/datums/elements/footstep_override.dm:42
    [SignalHandler]
    public virtual void on_moved(atom_movable source = null, atom oldloc = null)
    {
        if (isturf(oldloc))
            vacate_turf(source, (turf)oldloc);
        if (isturf(source.loc))
            occupy_turf(source, (turf)source.loc);
    }

    /**
     * Adds the movable to the list of movables with the element occupying the turf.
     * If the turf was not on the list of occupied turfs before, a signal will be registered
     * to it.
     */
    // tg: code/datums/elements/footstep_override.dm:54
    public virtual void occupy_turf(atom_movable movable = null, turf location = null)
    {
        if (Dm.truthy(occupied_turfs[location]))
        {
            Dm.combine((DmList)occupied_turfs[location], movable);
            return;
        }
        occupied_turfs[location] = Dm.list(movable);
        RegisterSignal(location, COMSIG_TURF_PREPARE_STEP_SOUND, nameof(prepare_steps));
    }

    /**
     * Removes the movable from the list of movables with the element occupying the turf.
     * If the turf is no longer occupied, it'll be removed from the list, and the signal
     * unregistered from it
     */
    // tg: code/datums/elements/footstep_override.dm:66
    public virtual void vacate_turf(atom_movable movable = null, turf location = null)
    {
        // LAZYREMOVE(occupied_turfs[location], movable): the element through a temp (rules 11.1)
        DmList t__1 = occupied_turfs[location] as DmList;
        LAZYREMOVE(ref t__1, movable);
        occupied_turfs[location] = t__1;
        if (!Dm.truthy(occupied_turfs[location]))
        {
            Dm.remove(ref occupied_turfs, location);
            UnregisterSignal(location, COMSIG_TURF_PREPARE_STEP_SOUND);
        }
    }

    ///Changes the sound types to be played if the element priority is higher than the one in the steps list.
    // tg: code/datums/elements/footstep_override.dm:73
    [SignalHandler]
    public virtual object prepare_steps(turf source = null, DmList steps = null)
    {
        if (Dm.gt(steps[STEP_SOUND_PRIORITY], priority))
            return null;
        steps[FOOTSTEP_MOB_SHOE] = footstep;
        steps[FOOTSTEP_MOB_BAREFOOT] = barefootstep;
        steps[FOOTSTEP_MOB_HEAVY] = heavyfootstep;
        steps[FOOTSTEP_MOB_CLAW] = clawfootstep;
        steps[STEP_SOUND_PRIORITY] = priority;
        return FOOTSTEP_OVERRIDEN;
    }
}
