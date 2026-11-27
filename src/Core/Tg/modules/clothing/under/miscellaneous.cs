// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/under/miscellaneous.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps CL1, D333): the psychedelic jumpsuit's general colour and the administrative cybernetic jumpsuit (with its fishing
// difficulty); the rest of the file is var-only. The var blocks are generated (D111).

// tg: code/modules/clothing/under/miscellaneous.dm:33
public partial class obj_item_clothing_under_misc_psyche : obj_item_clothing_under_misc
{
    // tg: code/modules/clothing/under/miscellaneous.dm:39
    public override object get_general_color(icon base_icon = null)
    {
        return "#3f3f3f";
    }
}

// tg: code/modules/clothing/under/miscellaneous.dm:49
public partial class obj_item_clothing_under_misc_adminsuit : obj_item_clothing_under_misc
{
    // tg: code/modules/clothing/under/miscellaneous.dm:65
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_adjust_fishing_difficulty), -25));
        return dot;
    }
}
