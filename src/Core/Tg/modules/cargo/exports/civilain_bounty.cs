// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/exports/civilain_bounty.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4b (D327): the whole file: a completed bounty cube sells for its bounty and its speed bonus. The var blocks are generated.

// tg: code/modules/cargo/exports/civilain_bounty.dm:1
public partial class datum_export_bounty_box : datum_export
{
    // tg: code/modules/cargo/exports/civilain_bounty.dm:7
    public override object get_base_cost(obj exported_item = null)
    {
        var cube = (obj_item_bounty_cube)exported_item;
        return cube.bounty_value + (cube.bounty_value * cube.speed_bonus);
    }
}
