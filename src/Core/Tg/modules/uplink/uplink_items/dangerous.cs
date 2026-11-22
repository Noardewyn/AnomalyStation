// Translated from tgstation (snapshot 1d99bd7f): code/modules/uplink/uplink_items/dangerous.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Syndicate package A2 (D316): the entries' procs (the entries and their categories are the generated var blocks).

// tg: code/modules/uplink/uplink_items/dangerous.dm:71
public partial class datum_uplink_item_dangerous_doublesword : datum_uplink_item_dangerous
{
    // tg: code/modules/uplink/uplink_items/dangerous.dm:80
    public override object get_discount_value(object discount_type = null)
    {
        switch (discount_type)
        {
            case TRAITOR_DISCOUNT_BIG:
                return 0.5f;
            case TRAITOR_DISCOUNT_AVERAGE:
                return 0.35f;
            default:
                return 0.2f;
        }
    }
}
