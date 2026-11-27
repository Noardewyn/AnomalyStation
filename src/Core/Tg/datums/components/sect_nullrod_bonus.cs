// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/sect_nullrod_bonus.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: a sect's secret rites, unlocked by the null rod form the chaplain picked (the pyre sect's blazing star with the
// divine bow). The var block is generated (D111).

/**
 * sect nullrod bonus component; for sekret rite combos
 *
 * Good example is the bow and pyre sect. pick the bow, get a special rite in the pyre sect.
 */
// tg: code/datums/components/sect_nullrod_bonus.dm:6
public partial class datum_component_sect_nullrod_bonus : datum_component
{
    // tg: code/datums/components/sect_nullrod_bonus.dm:12
    [DmParams("bonus_rites")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        DmList bonus_rites = Dm.arg<DmList>(__extra, 0);
        if (!(parent is datum_religion_sect))
            return COMPONENT_INCOMPATIBLE;
        this.bonus_rites = bonus_rites;
        check_bonus_rites();
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/datums/components/sect_nullrod_bonus.dm:18
    public override async DmTask RegisterWithParent()
    {
        RegisterSignals(SSdcs, Dm.list(COMSIG_RELIGIOUS_SECT_CHANGED, COMSIG_GLOB_NULLROD_PICKED), nameof(on_nullrod_picked));
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/sect_nullrod_bonus.dm:21
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(SSdcs, Dm.list(COMSIG_RELIGIOUS_SECT_CHANGED, COMSIG_GLOB_NULLROD_PICKED));
        await DmTask.CompletedTask;
    }

    // tg: code/datums/components/sect_nullrod_bonus.dm:24
    [SignalHandler]
    public virtual void on_nullrod_picked(datum source = null)
    {
        check_bonus_rites();
    }

    // tg: code/datums/components/sect_nullrod_bonus.dm:28
    public virtual void check_bonus_rites()
    {
        if (Dm.truthy(bonus_applied) || GLOB.holy_weapon_type == null)
            return;
        object unlocked_rites = bonus_rites[GLOB.holy_weapon_type];
        if (!Dm.truthy(unlocked_rites) || GLOB.religious_sect == null)
            return;
        GLOB.religious_sect.rites_list.Add(unlocked_rites);
        bonus_applied = TRUE;
    }
}
