// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/bounties/botany.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4c (D298): the whole file. The var blocks are generated (D111).

// tg: code/modules/cargo/bounties/botany.dm:1
public partial class datum_bounty_item_botany : datum_bounty_item
{
    // tg: code/modules/cargo/bounties/botany.dm:7
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        description = I18n.T("Central Command's head chef is looking to prepare a fine [] with []. []", foodtype, name, bonus_desc);
        reward = Dm.num(reward) + multiplier * (CARGO_CRATE_VALUE * 2);
        required_count = rand(5, 10);
    }
}

// tg: code/modules/cargo/bounties/botany.dm:214
public partial class datum_bounty_item_botany_bonfire : datum_bounty_item_botany
{
    // tg: code/modules/cargo/bounties/botany.dm:219
    public override object applies_to(obj O = null)
    {
        if (!Dm.truthy(base.applies_to(O)))
            return FALSE;
        var B = (obj_structure_bonfire)O;
        return Dm.truthy(B.burning) ? TRUE : FALSE;
    }
}
