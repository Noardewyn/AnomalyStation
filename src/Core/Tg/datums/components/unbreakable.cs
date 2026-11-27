// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/unbreakable.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the sparring sect's unbreakable boon, a surge of healing and stimulants when its holder falls into crit, once a
// minute. The var block is generated (D111).

// tg: code/datums/components/unbreakable.dm:1
public partial class datum_component_unbreakable : datum_component
{
    // tg: code/datums/components/unbreakable.dm:4
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        if (!ishuman(parent))
            return COMPONENT_INCOMPATIBLE;
        ADD_TRAIT(parent, TRAIT_UNBREAKABLE, INNATE_TRAIT);
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/datums/components/unbreakable.dm:9
    public override object Destroy(float? force = null)
    {
        REMOVE_TRAIT(parent, TRAIT_UNBREAKABLE, INNATE_TRAIT);
        return base.Destroy(force);
    }

    // tg: code/datums/components/unbreakable.dm:13
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_MOB_STATCHANGE, nameof(surge));
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/unbreakable.dm:16
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, COMSIG_MOB_STATCHANGE);
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/unbreakable.dm:19
    [SignalHandler]
    public virtual void surge(mob_living_carbon_human surged = null, object new_stat = null)
    {
        if (Dm.lt(new_stat, SOFT_CRIT) || Dm.ge(new_stat, DEAD))
            return;
        if (!(surge_cooldown <= world.time)) // COOLDOWN_FINISHED(src, surge_cooldown), inline (rules 11.1 (a))
            return;
        surge_cooldown = world.time + 1 * MINUTES; // COOLDOWN_START(src, surge_cooldown, 1 MINUTES)
        surged.balloon_alert(surged, I18n.T("you refuse to give up!"));//breaks balloon alert conventions by using a "!" for a fail message but that's okay because it's a pretty awesome moment
        Waiting.NoSleep(surged.heal_overall_damage(brute: 15, burn: 15, required_bodytype: BODYTYPE_ORGANIC));
        if (Dm.num(((datum_reagents)surged.reagents).get_reagent_amount(typeof(datum_reagent_medicine_ephedrine))) < 20)
            Waiting.NoSleep(((datum_reagents)surged.reagents).add_reagent(typeof(datum_reagent_medicine_ephedrine), 10));
        if (Dm.num(((datum_reagents)surged.reagents).get_reagent_amount(typeof(datum_reagent_medicine_epinephrine))) < 20)
            Waiting.NoSleep(((datum_reagents)surged.reagents).add_reagent(typeof(datum_reagent_medicine_epinephrine), 10));
    }
}
