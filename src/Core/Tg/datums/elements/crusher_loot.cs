// Translated from tgstation (snapshot 1d99bd7f): code/datums/elements/crusher_loot.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/**
 * Crusher Loot; which makes the attached mob drop a crusher trophy of some type if the majority damage was from a crusher!
 *
 * Used for all the mobs droppin' crusher trophies
 */
// tg: code/datums/elements/crusher_loot.dm:6
public partial class datum_element_crusher_loot : datum_element
{
    // tg: code/datums/elements/crusher_loot.dm:22
    [DmParams("target", "trophy_type", "drop_mod", "drop_immediately", "guaranteed_drop", "replace_all")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object trophy_type = Dm.arg(__extra, 0);
        float? drop_mod = Dm.arg<float?>(__extra, 1);
        float? drop_immediately = Dm.arg<float?>(__extra, 2);
        object guaranteed_drop = Dm.arg(__extra, 3);
        float? replace_all = Dm.arg<float?>(__extra, 4);
        drop_mod ??= 25;
        drop_immediately ??= FALSE;
        replace_all ??= FALSE;
        object dot = await base.Attach(target, Dm.forward(__extra, trophy_type, drop_mod, drop_immediately, guaranteed_drop, replace_all));
        if (!isliving(target))
            return ELEMENT_INCOMPATIBLE;

        RegisterSignal(target, COMSIG_LIVING_DEATH, nameof(on_death));

        this.trophy_type = trophy_type;
        this.drop_mod = drop_mod;
        this.guaranteed_drop = guaranteed_drop;
        this.drop_immediately = drop_immediately;
        this.replace_all = replace_all;
        if (Dm.truthy(replace_all))
            RegisterSignal(target, COMSIG_LIVING_DROP_LOOT, nameof(on_loot_drop));
        return dot;
    }

    // tg: code/datums/elements/crusher_loot.dm:37
    public override object Detach(datum target = null, params object[] __extra)
    {
        UnregisterSignal(target, Dm.list(COMSIG_LIVING_DEATH, COMSIG_LIVING_DROP_LOOT));
        return base.Detach(target, __extra);
    }

    // tg: code/datums/elements/crusher_loot.dm:41
    [SignalHandler]
    public virtual object on_loot_drop(mob_living target = null, DmList spawn_loot = null, object gibbed = null)
    {
        // Prevent normal loot from being dropped if we have replace_all enabled
        return COMPONENT_NO_LOOT_DROP;
    }

    // tg: code/datums/elements/crusher_loot.dm:46
    [SignalHandler]
    public virtual void on_death(mob_living target = null, object gibbed = null)
    {
        datum_status_effect_crusher_damage damage = target.has_status_effect(typeof(datum_status_effect_crusher_damage)) as datum_status_effect_crusher_damage;
        if (damage == null)
            return;

        if (Dm.truthy(guaranteed_drop))
        {
            if (damage.total_damage / Dm.num(target.maxHealth) < Dm.num(guaranteed_drop))
                return;
        }
        else if (!prob((damage.total_damage / Dm.num(target.maxHealth)) * Dm.num(drop_mod))) // On average, you'll need to kill 4 creatures before getting the item. by default.
            return;

        if (Dm.truthy(replace_all) && isanimal(target))
        {
            mob_living_simple_animal expiring_code = target as mob_living_simple_animal;
            if (!islist(trophy_type))
            {
                expiring_code.loot = Dm.list(trophy_type);
                return;
            }

            DmList trophies = trophy_type as DmList;
            expiring_code.loot = trophies.Copy();
            return;
        }

        if (!islist(trophy_type))
        {
            make_path(target, trophy_type);
            return;
        }

        if (Dm.truthy(replace_all))
        {
            target.butcher_results?.Cut();
            target.guaranteed_butcher_results?.Cut();
        }

        foreach (object trophypath in Dm.each(trophy_type))
            make_path(target, trophypath);
    }

    // tg: code/datums/elements/crusher_loot.dm:80
    public virtual void make_path(mob_living target = null, object path = null)
    {
        if (Dm.truthy(drop_immediately))
        {
            Dm.@new((Type)path, get_turf(target));
            return;
        }

        if (target.guaranteed_butcher_results == null)
            target.guaranteed_butcher_results = Dm.list();

        target.guaranteed_butcher_results[path] = 1;
    }
}
