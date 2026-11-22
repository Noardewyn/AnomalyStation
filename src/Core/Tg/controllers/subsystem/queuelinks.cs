// Translated from tgstation (snapshot 1d99bd7f): code/controllers/subsystem/queuelinks.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1b, D316: the syndicate base's keycard doors are puzzle doors, whose Initialize queues the linked ones).
// The var blocks are generated (D111).
//
// GAP(types): the queue link's `var/id` (:47) holds the link's text id; the generator types it `Type`, so it is declared here as the
// DM value it is.

public partial class atom : datum
{
    // tg: code/controllers/subsystem/queuelinks.dm:1
    public virtual void MatchedLinks(object id = null, DmList partners = null)
    {
    }
}

// tg: code/controllers/subsystem/queuelinks.dm:3  SUBSYSTEM_DEF(queuelinks)
public partial class datum_controller_subsystem_queuelinks : datum_controller_subsystem
{
    // The New() that SUBSYSTEM_DEF(queuelinks) defines (__DEFINES/MC.dm), with NEW_SS_GLOBAL(SSqueuelinks)
    // (__DEFINES/MC.dm:29) expanded inline (it reads src).
    // tg: code/controllers/subsystem/queuelinks.dm:3
    public override async DmTask New(params object[] __extra)
    {
        if (SSqueuelinks != this)
        {
            if (SSqueuelinks is datum_controller_subsystem_queuelinks)
            {
                Recover();
                qdel(SSqueuelinks);
            }
            SSqueuelinks = this;
        }
        PreInit();
    }

    ///Creates or adds to a queue with the id supplied, if the queue is now or above the size of the queue, calls MatchedLinks and clears queue.
    /// queues with a size of 0 wait never pop until something is added with an actual queue_max
    // tg: code/controllers/subsystem/queuelinks.dm:11
    public virtual void add_to_queue(atom what = null, object id = null, float? queue_max = null)
    {
        queue_max ??= 0;
        if (!isatom(what))
            throw Dm.CRASH(Dm.text("Attempted to add a non-atom to queue; []!", what), "code/controllers/subsystem/queuelinks.dm", 13);
        if (id == null)
            throw Dm.CRASH(Dm.text("Attempted to add to queue with no ID; []", what), "code/controllers/subsystem/queuelinks.dm", 15);

        var link = queues[id] as datum_queue_link;
        if (link == null)
        {
            link = Dm.@new<datum_queue_link>(id);
            queues[id] = link;
        }

        if (link.add(what, queue_max))
            Dm.remove(ref queues, id);
    }

    /**
     * Pop a queue link without waiting for it to reach its max size.
     * This is useful for those links that do not have a fixed size and thus may not pop.
     */
    // tg: code/controllers/subsystem/queuelinks.dm:29
    public virtual void pop_link(object id = null)
    {
        if (id == null)
            throw Dm.CRASH("Attempted to pop a queue with no ID", "code/controllers/subsystem/queuelinks.dm", 31);

        var link = queues[id] as datum_queue_link;
        if (queues[id] == null)
            throw Dm.CRASH(Dm.text("Attempted to pop a non-existant queue: []", id), "code/controllers/subsystem/queuelinks.dm", 35);

        link.pop();
        Dm.remove(ref queues, id);
    }
}

// tg: code/controllers/subsystem/queuelinks.dm:41
public partial class datum_queue_link : datum
{
    /// id
    // tg: code/controllers/subsystem/queuelinks.dm:47  (hand-written: see the GAP(types) above)
    public object id;

    // tg: code/controllers/subsystem/queuelinks.dm:49
    [DmParams("new_id")]
    public override async DmTask New(params object[] __extra)
    {
        id = Dm.arg(__extra, 0);
        await base.New(__extra);
    }

    ///adds an atom to the queue, if we are popping this returns TRUE
    // tg: code/controllers/subsystem/queuelinks.dm:54
    public virtual bool add(atom what = null, float? max = null)
    {
        max ??= 0;
        if (Dm.@in(what, partners))
            return false;
        Dm.append(ref partners, what);
        RegisterSignal(what, COMSIG_QDELETING, nameof(link_object_deleted));

        if (queue_max != 0 && max != 0 && max != queue_max)
            throw Dm.CRASH(Dm.text("Tried to change queue size to [] from []!", max, queue_max), "code/controllers/subsystem/queuelinks.dm", 63);
        else if (queue_max == 0)
            queue_max = max.Value;

        if (queue_max == 0 || length(partners) < queue_max)
            return false;

        pop();
        return true;
    }

    // tg: code/controllers/subsystem/queuelinks.dm:72
    public virtual void pop()
    {
        foreach (atom item in Dm.each<atom>(partners.Copy()))
            item.MatchedLinks(id, Dm.sub(partners, item) as DmList);
        qdel(this);
    }

    // because CI and stuff
    // tg: code/controllers/subsystem/queuelinks.dm:77
    [SignalHandler]
    public virtual void link_object_deleted(datum source = null)
    {
        Dm.remove(ref partners, source);
    }

    // tg: code/controllers/subsystem/queuelinks.dm:81
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        partners = null;
        return dot;
    }
}

public sealed partial class TgState
{
    // tg: code/controllers/subsystem/queuelinks.dm:3  SUBSYSTEM_DEF(queuelinks): GLOBAL_REAL(SSqueuelinks, /datum/controller/subsystem/queuelinks)
    internal datum_controller_subsystem_queuelinks SSqueuelinks;
}

public static partial class GlobalProcs
{
    public static ref datum_controller_subsystem_queuelinks SSqueuelinks => ref TgState.Current.SSqueuelinks;
}
