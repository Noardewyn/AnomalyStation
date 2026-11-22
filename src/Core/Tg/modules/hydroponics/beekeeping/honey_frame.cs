// Translated from tgstation (snapshot 1d99bd7f): code/modules/hydroponics/beekeeping/honey_frame.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P2 (D298): the whole file.

// tg: code/modules/hydroponics/beekeeping/honey_frame.dm:1
public partial class obj_item_honey_frame : obj_item
{
    // tg: code/modules/hydroponics/beekeeping/honey_frame.dm:11
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        pixel_x = base_pixel_x + rand(8, -8);
        pixel_y = base_pixel_y + rand(8, -8);
        return dot;
    }
}
