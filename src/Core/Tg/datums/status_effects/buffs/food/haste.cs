// Translated from tgstation (snapshot 1d99bd7f): code/datums/status_effects/buffs/food/haste.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps S1 (D291): the whole file (the buff every crafted-food complexity picks, GLOB.food_buffs). The var blocks are generated.

///Haste makes the eater move and act faster
// tg: code/datums/status_effects/buffs/food/haste.dm:2
public partial class datum_status_effect_food_haste : datum_status_effect_food
{
    // tg: code/datums/status_effects/buffs/food/haste.dm:4
    public override async DmTask<object> on_apply(params object[] __extra)
    {
        datum_movespeed_modifier_food_haste speed_mod = Dm.@new<datum_movespeed_modifier_food_haste>();
        speed_mod.multiplicative_slowdown = -0.04f * Dm.num(strength);
        await owner.add_movespeed_modifier(speed_mod, update: TRUE);
        datum_actionspeed_modifier_status_effect_food_haste action_mod = Dm.@new<datum_actionspeed_modifier_status_effect_food_haste>();
        action_mod.multiplicative_slowdown = -0.06f * Dm.num(strength);
        await owner.add_actionspeed_modifier(action_mod, update: TRUE);
        return await base.on_apply(__extra);
    }

    // tg: code/datums/status_effects/buffs/food/haste.dm:13
    public override async DmTask be_replaced()
    {
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_food_haste));
        owner.remove_actionspeed_modifier(typeof(datum_actionspeed_modifier_status_effect_food_haste));
        await base.be_replaced();
    }

    // tg: code/datums/status_effects/buffs/food/haste.dm:18
    public override async DmTask<object> on_remove()
    {
        owner.remove_movespeed_modifier(typeof(datum_movespeed_modifier_food_haste), update: TRUE);
        owner.remove_actionspeed_modifier(typeof(datum_actionspeed_modifier_status_effect_food_haste), update: TRUE);
        return await base.on_remove();
    }
}
