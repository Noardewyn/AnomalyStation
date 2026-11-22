// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/suits/wetfloor.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps H4 (D333, the janitor): the whole file: the wet floor sign, worn as a suit or stood on the floor (the floor placeable
// element). The var blocks are generated (D111).

// tg: code/modules/clothing/suits/wetfloor.dm:1
public partial class obj_item_clothing_suit_caution : obj_item_clothing_suit
{
    // tg: code/modules/clothing/suits/wetfloor.dm:33
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_floor_placeable)));
        return dot;
    }
}
