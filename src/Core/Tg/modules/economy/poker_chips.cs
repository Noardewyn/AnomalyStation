// Translated from tgstation (snapshot 1d99bd7f): code/modules/economy/poker_chips.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4a (D327): the whole file: the poker chips, worth their value in an ID card or a payment. The var blocks are generated.

// tg: code/modules/economy/poker_chips.dm:1
public partial class obj_item_poker_chip : obj_item
{
    // tg: code/modules/economy/poker_chips.dm:11
    public override async DmTask<object> grind_results()
    {
        return Dm.list(Dm.kv(typeof(datum_reagent_plastic_polymers), COIN_MATERIAL_AMOUNT * 0.2f));
    }

    // tg: code/modules/economy/poker_chips.dm:14
    public override object get_item_credit_value()
    {
        return redeem_value;
    }
}
