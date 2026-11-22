// Translated from tgstation (snapshot 1d99bd7f): code/modules/bitrunning/components/netpod_healing.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps BR1 (D339, D341): the whole file, the netpod's stasis (the embryonic status effect and its alert) that heals the bitrunner's
// body while connected. The var blocks are generated (D111).

// tg: code/modules/bitrunning/components/netpod_healing.dm:3
public partial class datum_component_netpod_healing : datum_component
{
    // tg: code/modules/bitrunning/components/netpod_healing.dm:1  (#undef at :69)
    private const float BASE_HEAL = 4;

    // tg: code/modules/bitrunning/components/netpod_healing.dm:5
    [DmParams("pod")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        obj_machinery_netpod pod = Dm.arg(__extra, 0) as obj_machinery_netpod;
        if (!iscarbon(parent))
            return COMPONENT_INCOMPATIBLE;

        RegisterSignals(
            pod,
            Dm.list(COMSIG_MACHINERY_BROKEN, COMSIG_QDELETING, COMSIG_BITRUNNER_NETPOD_OPENED),
            nameof(on_remove));

        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(on_remove));

        mob_living_carbon player = parent as mob_living_carbon;
        await player.apply_status_effect(typeof(datum_status_effect_embryonic), STASIS_NETPOD_EFFECT);

        START_PROCESSING(SSmachines, this);
        return null;
    }

    // tg: code/modules/bitrunning/components/netpod_healing.dm:22
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSmachines, this);

        mob_living_carbon player = parent as mob_living_carbon;
        player.remove_status_effect(typeof(datum_status_effect_embryonic));

        return base.Destroy(force);
    }

    // tg: code/modules/bitrunning/components/netpod_healing.dm:30
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        mob_living_carbon owner = parent as mob_living_carbon;
        if (owner == null)
        {
            qdel(this);
            return null;
        }

        float spt = Dm.num(seconds_per_tick);
        float need_mob_update = FALSE;
        need_mob_update += Dm.num(Waiting.NoSleep(owner.adjust_brute_loss(-BASE_HEAL * spt, updating_health: FALSE)));
        need_mob_update += Dm.num(Waiting.NoSleep(owner.adjust_fire_loss(-BASE_HEAL * spt, updating_health: FALSE)));
        need_mob_update += Dm.num(Waiting.NoSleep(owner.adjust_tox_loss(-BASE_HEAL * spt, updating_health: FALSE, forced: TRUE)));

        owner.adjust_blood_volume(BASE_HEAL * spt, maximum: BLOOD_VOLUME_NORMAL);

        if (Dm.truthy(need_mob_update))
            Waiting.NoSleep(owner.updatehealth());
        return null;
    }

    /// Deletes itself when the machine was opened
    // tg: code/modules/bitrunning/components/netpod_healing.dm:47
    [SignalHandler]
    public virtual void on_remove()
    {
        qdel(this);
    }
}

// tg: code/modules/bitrunning/components/netpod_healing.dm:53
public partial class datum_status_effect_embryonic : datum_status_effect
{
    // tg: code/modules/bitrunning/components/netpod_healing.dm:56
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        await DmTask.CompletedTask;
        ADD_TRAIT(owner, TRAIT_STASIS, TRAIT_STATUS_EFFECT(id));
        return TRUE;
    }

    // tg: code/modules/bitrunning/components/netpod_healing.dm:60
    public override async DmTask<object> on_remove()
    {
        await DmTask.CompletedTask;
        REMOVE_TRAIT(owner, TRAIT_STASIS, TRAIT_STATUS_EFFECT(id));
        return null;
    }
}
