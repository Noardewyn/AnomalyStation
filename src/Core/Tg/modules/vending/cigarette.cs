// Translated from tgstation (snapshot 1d99bd7f): code/modules/vending/cigarette.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps package S2: the file's one proc (the vendor's lighter thrown lit, a deferred leaf while the lighter is inert). The var blocks are generated.

// tg: code/modules/vending/cigarette.dm:2
public partial class obj_machinery_vending_cigarette : obj_machinery_vending
{
    // tg: code/modules/vending/cigarette.dm:91
    public override async DmTask pre_throw(obj_item thrown_item = null)
    {
        if (istype(thrown_item, typeof(obj_item_lighter)))
        {
            obj_item_lighter thrown_lighter = (obj_item_lighter)thrown_item;
            thrown_lighter.set_lit(TRUE);
        }
    }
}
