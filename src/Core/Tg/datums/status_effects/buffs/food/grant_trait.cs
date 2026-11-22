// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/buffs/food/grant_trait.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333: the crafted foods' trait buffs). The var blocks are generated (D111).

/// Makes you gain a trait
// tg: code/datums/status_effects/buffs/food/grant_trait.dm:2
public partial class datum_status_effect_food_trait : datum_status_effect_food
{
    // tg: code/datums/status_effects/buffs/food/grant_trait.dm:5
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        if (!HAS_TRAIT_FROM(owner, trait, TRAIT_STATUS_EFFECT(id))) // Check if trait was already applied
            ADD_TRAIT(owner, trait, TRAIT_STATUS_EFFECT(id));
        return await base.on_apply(__extra);
    }

    // tg: code/datums/status_effects/buffs/food/grant_trait.dm:10
    public override async DmTask be_replaced()
    {
        REMOVE_TRAIT(owner, trait, TRAIT_STATUS_EFFECT(id));
        await base.be_replaced();
    }

    // tg: code/datums/status_effects/buffs/food/grant_trait.dm:14
    public override async DmTask<object> on_remove()
    {
        REMOVE_TRAIT(owner, trait, TRAIT_STATUS_EFFECT(id));
        return await base.on_remove();
    }
}

// tg: code/datums/status_effects/buffs/food/grant_trait.dm:48
public partial class datum_status_effect_food_trait_waddle : datum_status_effect_food_trait
{
    // tg: code/datums/status_effects/buffs/food/grant_trait.dm:52
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        await owner.AddElementTrait(trait, type, typeof(datum_element_waddling));
        return await base.on_apply(__extra);
    }
}
