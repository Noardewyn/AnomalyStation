// Translated from tgstation (snapshot 1d99bd7f): code/modules/food_and_drinks/food/vegetables.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (syndicate N2b, D316: the Donk pinata drops it): the tator tot; the other vegetables keep the base. The var blocks are
// generated (D111).

// Potatoes
// tg: code/modules/food_and_drinks/food/vegetables.dm:44
public partial class obj_item_food_tatortot : obj_item_food
{
    // tg: code/modules/food_and_drinks/food/vegetables.dm:55
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_dunkable), 10));
        return dot;
    }
}
