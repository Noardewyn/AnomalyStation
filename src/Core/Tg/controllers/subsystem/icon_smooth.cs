// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/icon_smooth.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M3 P4: icon smoothing, scope S1). Its four list vars are in the generated complement
// (Generated/Types/datum.controller.g.cs); their list() initializers are here, in the hand-written dm_init_vars.

// tg: code/controllers/subsystem/icon_smooth.dm:1 SUBSYSTEM_DEF(icon_smooth)
[DmPath("/datum/controller/subsystem/icon_smooth")]
public partial class datum_controller_subsystem_icon_smooth : datum_controller_subsystem
{
    // tg: code/controllers/subsystem/icon_smooth.dm:2-8
    public datum_controller_subsystem_icon_smooth()
    {
        name = "Icon Smoothing";
        wait = 1;
        priority = FIRE_PRIORITY_SMOOTHING;
        ss_flags = SS_TICKER;
    }

    // tg: code/controllers/subsystem/icon_smooth.dm:3-14  (the list initializers, rules 10.2)
    protected internal override void dm_init_vars()
    {
        base.dm_init_vars();
        dependencies = Dm.list(
            typeof(datum_controller_subsystem_atoms));
        ///Blueprints assemble an image of what pipes/manifolds/wires look like on initialization, and thus should be taken after everything's been smoothed
        blueprint_queue = Dm.list();
        smooth_queue = Dm.list();
        deferred = Dm.list();
        deferred_by_source = Dm.list();
    }

    // The New() that SUBSYSTEM_DEF(icon_smooth) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSicon_smooth) (__DEFINES/MC.dm:29)
    // expanded inline (it reads src).
    // tg: code/controllers/subsystem/icon_smooth.dm:1
    public override async DmTask New(params object[] __extra)
    {
        if (SSicon_smooth != this)
        {
            if (SSicon_smooth is datum_controller_subsystem_icon_smooth)
            {
                Recover();
                qdel(SSicon_smooth);
            }
            SSicon_smooth = this;
        }
        PreInit();
    }

    // tg: code/controllers/subsystem/icon_smooth.dm:16
    public override async DmTask fire(float? resumed = null, params object[] __extra)
    {
        resumed ??= FALSE;
        // We do not want to smooth icons of atoms whose neighbors are not initialized yet,
        // this causes runtimes.
        // Icon smoothing SS runs after atoms, so this only happens for something like shuttles.
        // This kind of map loading shouldn't take too long, so the delay is not a problem.
        if (Dm.truthy(SSatoms.initializing_something()))
            return;

        DmList smooth_queue_cache = smooth_queue;
        while (Dm.truthy(length(smooth_queue_cache)))
        {
            atom smoothing_atom = smooth_queue_cache[length(smooth_queue_cache)] as atom;
            smooth_queue_cache.len--;
            if (QDELETED(smoothing_atom) || !Dm.truthy(Dm.bitand(smoothing_atom.smoothing_flags, SMOOTH_QUEUED)))
                continue;
            if (Dm.truthy(Dm.bitand(smoothing_atom.flags_1, INITIALIZED_1)))
                smoothing_atom.smooth_icon__proc();
            else
                Dm.append(ref deferred, smoothing_atom);
            if (Dm.truthy((world.tick_usage > datum_controller_master.current_ticklimit || !Dm.eq(this.state, SS_RUNNING)) ? pause() : 0)) // MC_TICK_CHECK (__DEFINES/MC.dm:6), inline (rules 11.3)
                return;
        }

        if (!Dm.truthy(length(smooth_queue_cache)))
        {
            if (Dm.truthy(deferred.len))
            {
                smooth_queue = deferred;
                deferred = smooth_queue_cache;
            }
            else
                can_fire = FALSE;
        }
    }

    // tg: code/controllers/subsystem/icon_smooth.dm:45
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        DmList queue = smooth_queue;
        smooth_queue = Dm.list();

        while (Dm.truthy(length(queue)))
        {
            atom smoothing_atom = queue[length(queue)] as atom;
            queue.len--;
            if (QDELETED(smoothing_atom) || !Dm.truthy(Dm.bitand(smoothing_atom.smoothing_flags, SMOOTH_QUEUED)) || !Dm.truthy(smoothing_atom.z))
                continue;
            smoothing_atom.smooth_icon__proc();
            CHECK_TICK();
        }

        queue = blueprint_queue;
        blueprint_queue = null;

        foreach (atom_movable movable_item in Dm.each_cast<atom_movable>(queue))
        {
            if (!isturf(movable_item.loc))
                continue;
            turf item_loc = movable_item.loc as turf;
            item_loc.add_blueprints(movable_item);
        }

        return SS_INIT_SUCCESS;
    }

    /// Releases a pool of delayed smooth attempts from a particular source
    // tg: code/controllers/subsystem/icon_smooth.dm:68
    public virtual void free_deferred(object source_to_free = null)
    {
        Dm.append(ref smooth_queue, deferred_by_source[source_to_free]);
        Dm.remove(ref deferred_by_source, source_to_free);
        if (!Dm.truthy(can_fire))
            can_fire = TRUE;
    }

    // tg: code/controllers/subsystem/icon_smooth.dm:74
    public virtual void add_to_queue(atom thing = null)
    {
        if (Dm.truthy(Dm.bitand(thing.smoothing_flags, SMOOTH_QUEUED)))
            return;
        Dm.combine(ref thing.smoothing_flags, SMOOTH_QUEUED);
        // If we're currently locked into mapload BY something
        // Then put us in a deferred list that we release when this mapload run is finished
        if (Dm.truthy(initialized) && Dm.truthy(length(SSatoms.initialized_state)) && SSatoms.initialized == INITIALIZATION_INNEW_MAPLOAD)
        {
            object source = SSatoms.get_initialized_source();
            // LAZYADD(deferred_by_source[source], thing) (__HELPERS/_lists.dm:62), expanded inline: a list element takes no ref
            // (rules 4.3), so `L ||= list(); L += I` is written on the element (the ||= statement form of rules 4.5)
            if (!Dm.truthy(deferred_by_source[source]))
                deferred_by_source[source] = Dm.list();
            Dm.append_at(deferred_by_source, source, thing);
            return;
        }
        Dm.append(ref smooth_queue, thing);
        if (!Dm.truthy(can_fire))
            can_fire = TRUE;
    }

    // tg: code/controllers/subsystem/icon_smooth.dm:88
    public virtual void remove_from_queues(atom thing = null)
    {
        // Lack of removal from deferred_by_source is safe because the lack of SMOOTH_QUEUED will just free it anyway
        // Hopefully this'll never cause a harddel (dies)
        Dm.mask(ref thing.smoothing_flags, Dm.bitnot(SMOOTH_QUEUED));
        Dm.remove(ref smooth_queue, thing);
        if (Dm.truthy(blueprint_queue))
            Dm.remove(ref blueprint_queue, thing);
        Dm.remove(ref deferred, thing);
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/icon_smooth.dm:1  SUBSYSTEM_DEF(icon_smooth): GLOBAL_REAL(SSicon_smooth, /datum/controller/subsystem/icon_smooth)
    internal datum_controller_subsystem_icon_smooth SSicon_smooth;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_icon_smooth SSicon_smooth => ref TgState.Current.SSicon_smooth;
}
