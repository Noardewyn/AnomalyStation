// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/swarming.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

// tg: code/datums/components/swarming.dm:1
public partial class datum_component_swarming : datum_component
{
    private static ref DmList swarming_loc_connections => ref TgState.Current.datum_component_swarming__swarming_loc_connections;

    // tg: code/datums/components/swarming.dm:13
    [DmParams("max_x", "max_y")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object max_x = Dm.arg(__extra, 0) ?? 24f;
        object max_y = Dm.arg(__extra, 1) ?? 24f;
        if (!ismovable(parent))
            return COMPONENT_INCOMPATIBLE;
        offset_x = rand(-Dm.num(max_x), Dm.num(max_x));
        offset_y = rand(-Dm.num(max_y), Dm.num(max_y));

        await AddComponent(typeof(datum_component_connect_loc_behalf), parent, swarming_loc_connections);
        return null;
    }

    // tg: code/datums/components/swarming.dm:21
    public override object Destroy(float? force = null)
    {
        foreach (object other in Dm.each(swarm_members))
        {
            datum_component_swarming other_swarm = other as datum_component_swarming;
            Dm.remove(ref other_swarm.swarm_members, this);
            if (Dm.length(other_swarm.swarm_members) == 0)
                other_swarm.unswarm();
        }
        swarm_members = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/swarming.dm:30
    [SignalHandler]
    public virtual void join_swarm(datum source = null, atom_movable arrived = null)
    {
        datum_component_swarming other_swarm = arrived.GetComponent(typeof(datum_component_swarming)) as datum_component_swarming;
        if (other_swarm == null)
            return;
        swarm();
        Dm.combine(ref swarm_members, other_swarm);
        other_swarm.swarm();
        Dm.combine(ref other_swarm.swarm_members, this);
    }

    // tg: code/datums/components/swarming.dm:41
    [SignalHandler]
    public virtual void leave_swarm(datum source = null, atom_movable gone = null, object direction = null)
    {
        datum_component_swarming other_swarm = gone.GetComponent(typeof(datum_component_swarming)) as datum_component_swarming;
        if (other_swarm == null || !Dm.@in(other_swarm, swarm_members))
            return;
        Dm.remove(ref swarm_members, other_swarm);
        if (Dm.length(swarm_members) == 0)
            unswarm();
        Dm.remove(ref other_swarm.swarm_members, this);
        if (Dm.length(other_swarm.swarm_members) == 0)
            other_swarm.unswarm();
    }

    // tg: code/datums/components/swarming.dm:54
    public virtual void swarm()
    {
        atom_movable owner = parent as atom_movable;
        if (!Dm.truthy(is_swarming))
        {
            is_swarming = TRUE;
            animate(owner, Dm.named("pixel_x", owner.pixel_x + offset_x), Dm.named("pixel_y", owner.pixel_y + offset_y), Dm.named("time", 2));
        }
    }

    // tg: code/datums/components/swarming.dm:60
    public virtual void unswarm()
    {
        atom_movable owner = parent as atom_movable;
        if (Dm.truthy(is_swarming))
        {
            animate(owner, Dm.named("pixel_x", owner.pixel_x - offset_x), Dm.named("pixel_y", owner.pixel_y - offset_y), Dm.named("time", 2));
            is_swarming = FALSE;
        }
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/swarming.dm:6
    internal DmList datum_component_swarming__swarming_loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_EXITED, nameof(datum_component_swarming.leave_swarm)),
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(datum_component_swarming.join_swarm)),
        Dm.kv(COMSIG_ATOM_AFTER_SUCCESSFUL_INITIALIZED_ON, nameof(datum_component_swarming.join_swarm)));
}
