// Translated from tgstation (snapshot 1d99bd7f): code/modules/clothing/spacesuits/pirate.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (gaps CL1, D333): the designer pirate helmet, which takes no hat on top; the rest of the file is var-only. The var blocks
// are generated (D111).

// tg: code/modules/clothing/spacesuits/pirate.dm:39
public partial class obj_item_clothing_head_helmet_space_pirate_tophat : obj_item_clothing_head_helmet_space_pirate
{
    // tg: code/modules/clothing/spacesuits/pirate.dm:44
    [DmParams("loose_hat")]
    public override async DmTask add_stabilizer(float? loose_hat = null)
    {
        await DmTask.CompletedTask;
    }
}
