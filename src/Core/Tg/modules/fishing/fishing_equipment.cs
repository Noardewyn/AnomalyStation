// Translated from tgstation (snapshot 1d99bd7f): code/modules/fishing/fishing_equipment.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps S3, D298): only the fishing tip, a paper slip; the fishing equipment comes with the fishing.

///Used to give the average player info about fishing stuff that's unknown to many.
// tg: code/modules/fishing/fishing_equipment.dm:560
public partial class obj_item_paper_paperslip_fishing_tip : obj_item_paper_paperslip
{
    // tg: code/modules/fishing/fishing_equipment.dm:564
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        default_raw_text = pick(GLOB.fishing_tips);
        return base.Initialize(mapload, __extra);
    }
}
