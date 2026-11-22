// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/bounties/slime.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4c (D298): the whole file. The var blocks are generated (D111).

// tg: code/modules/cargo/bounties/slime.dm:1
public partial class datum_bounty_item_slime : datum_bounty_item
{
    // tg: code/modules/cargo/bounties/slime.dm:4
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        description = I18n.T("Nanotrasen's science lead is hunting for the rare and exotic []. A bounty has been offered for finding it.", name);
        reward = Dm.num(reward) + rand(0, 4) * 500;
    }
}
