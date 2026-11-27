// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/magnet.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Attracts items of a certain typepath
// tg: code/datums/components/magnet.dm:2
public partial class datum_component_magnet : datum_component
{
    // tg: code/datums/components/magnet.dm:14
    [DmParams("pull_range", "attracted_typecache", "on_pulled", "on_contact")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object pull_range = Dm.arg(__extra, 0);
        object attracted_typecache = Dm.arg(__extra, 1);
        datum_callback on_pulled = Dm.arg<datum_callback>(__extra, 2);
        datum_callback on_contact = Dm.arg<datum_callback>(__extra, 3);
        pull_range ??= 5;
        attracted_typecache ??= Dm.list(typeof(obj_item_kitchen_spoon), typeof(obj_item_kitchen_fork), typeof(obj_item_knife));
        object dot = await base.Initialize(Dm.forward(__extra, pull_range, attracted_typecache, on_pulled, on_contact));
        if (!Dm.truthy(Dm.length(attracted_typecache)))
            throw Dm.CRASH(Dm.text("Attempted to instantiate a [] on [] which does not do anything.", this, parent), "code/datums/components/magnet.dm", 22);
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;

        this.pull_range = pull_range;
        this.attracted_typecache = typecacheof(attracted_typecache) as DmList;
        this.on_pulled = on_pulled;
        this.on_contact = on_contact;
        return dot;
    }

    // tg: code/datums/components/magnet.dm:31
    public override async DmTask RegisterWithParent()
    {
        await base.RegisterWithParent();
        START_PROCESSING(SSdcs, this);
        if (!isliving(parent))
            return;
        RegisterSignal(parent, COMSIG_MOB_STATCHANGE, nameof(toggle_on_stat_change));
    }

    // tg: code/datums/components/magnet.dm:38
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        await base.UnregisterFromParent(__extra);
        STOP_PROCESSING(SSdcs, this);
        UnregisterSignal(parent, COMSIG_MOB_STATCHANGE);
    }

    // tg: code/datums/components/magnet.dm:43
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSdcs, this);
        on_pulled = null;
        on_contact = null;
        return base.Destroy(force);
    }

    /// If a mob dies we stop attracting stuff
    // tg: code/datums/components/magnet.dm:50
    [SignalHandler]
    public virtual void toggle_on_stat_change(mob_living source = null)
    {
        if (source.stat == DEAD)
            STOP_PROCESSING(SSdcs, this);
        else
            START_PROCESSING(SSdcs, this);
    }

    // tg: code/datums/components/magnet.dm:57
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        foreach (atom_movable thing in Dm.each<atom_movable>(orange(pull_range, parent)))
        {
            if (!Dm.truthy(is_type_in_typecache(thing, attracted_typecache)))
                continue;
            float range = get_dist(thing, parent);
            if (range == 0)
                continue;
            if (range == 1 && on_contact != null)
            {
                Waiting.NoSleep(on_contact.Invoke(thing));
                continue;
            }
            object moved = Waiting.NoSleep(thing.Move(get_step_towards(thing, parent)));
            if (Dm.truthy(moved) && on_pulled != null)
                Waiting.NoSleep(on_pulled.Invoke(thing));
            CHECK_TICK();
        }
        return null;
    }
}
