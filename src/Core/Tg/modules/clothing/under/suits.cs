// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/under/suits.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps CL1, D333): the tuxedo and the carpskin suit (with their fishing difficulty); the rest of the file is
// var-only. The var blocks are generated (D111).

// tg: code/modules/clothing/under/suits.dm:99
public partial class obj_item_clothing_under_suit_tuxedo : obj_item_clothing_under_suit
{
    // tg: code/modules/clothing/under/suits.dm:105
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), 4)); //You aren't going to fish with this are you?
        return dot;
    }
}

// tg: code/modules/clothing/under/suits.dm:109
public partial class obj_item_clothing_under_suit_carpskin : obj_item_clothing_under_suit
{
    // tg: code/modules/clothing/under/suits.dm:116
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), -4));
        return dot;
    }
}
