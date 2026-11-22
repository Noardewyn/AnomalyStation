// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/stock_parts.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only /obj/item/stock_parts's Initialize and get_part_rating translated (M4c slice 2: the power cell is a stock
// part). The rapid part exchange devices and the other parts' procs come with machine construction (M5). The var blocks
// are generated.

// tg: code/modules/research/stock_parts.dm:6
public partial class obj_item_stock_parts : obj_item
{
    // tg: code/modules/research/stock_parts.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = null;
        dot = base.Initialize(mapload, __extra);
        pixel_x = base_pixel_x + rand(-5, 5);
        pixel_y = base_pixel_y + rand(-5, 5);
        return dot;
    }

    // tg: code/modules/research/stock_parts.dm:25
    public override object get_part_rating()
    {
        return rating;
    }
}
