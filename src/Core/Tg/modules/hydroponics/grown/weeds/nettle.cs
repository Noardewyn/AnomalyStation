// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/grown/weeds/nettle.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the file's procs (the juicing and grinding values, the plants' own uses) The cooking procs (slicing, baking, microwaving, drying) are live-partial rows for the kitchen.

// tg: code/modules/hydroponics/grown/weeds/nettle.dm:36
public partial class obj_item_food_grown_nettle : obj_item_food_grown
{
    // tg: code/modules/hydroponics/grown/weeds/nettle.dm:54
    public override async DmTask<object> suicide_act(mob_living user = null)
    {
        user.visible_message(span_suicide(I18n.T("[] is eating some of []! It looks like [] trying to commit suicide!", user, this, user.p_theyre())));
        return Dm.bitor(BRUTELOSS, TOXLOSS);
    }
}
