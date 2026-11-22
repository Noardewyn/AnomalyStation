// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/offered_when_pulled.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326: the janitor cyborg's vacuum handle). The var block is generated (D111).

/**For items automatically offered to a player when they pull a borg.*/
// tg: code/datums/components/offered_when_pulled.dm:3
public partial class datum_component_borg_item_offered_when_pulled : datum_component
{
    // tg: code/datums/components/offered_when_pulled.dm:6
    [DmParams("borg")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object borg = Dm.arg(__extra, 0);
        object dot = await base.Initialize(__extra);
        if (!isitem(parent))
            return COMPONENT_INCOMPATIBLE;
        if (borg is not mob_living_silicon_robot borg__typed)
        {
            stack_trace(Dm.text("Component assigned to [], but was not assigned a ref for type /mob/living/silicon/robot to attach COMSIG_LIVING_GET_PULLED", parent), "code/datums/components/offered_when_pulled.dm", 11);
            return COMPONENT_INCOMPATIBLE;
        }
        RegisterSignal(borg__typed, COMSIG_LIVING_GET_PULLED, nameof(on_pulled));
        ADD_TRAIT(parent, TRAIT_OFFERED_WHEN_PULLED, TRAIT_OFFERED_WHEN_PULLED);
        robot = borg__typed;
        return dot;
    }

    // tg: code/datums/components/offered_when_pulled.dm:17
    public override object Destroy(float? force = null)
    {
        UnregisterSignal(robot, COMSIG_LIVING_GET_PULLED);
        robot = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/offered_when_pulled.dm:22
    [SignalHandler]
    public virtual void on_pulled(mob_living holder = null, mob_living puller = null)
    {
        Waiting.NoSleep(holder.give(puller, parent as obj_item));
    }
}
