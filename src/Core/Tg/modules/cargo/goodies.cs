// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/goodies.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4b (D298): the whole file; the goody packs are var blocks (generated), the beach ball's description is its one proc.

// tg: code/modules/cargo/goodies.dm:267
public partial class datum_supply_pack_goody_beach_ball : datum_supply_pack_goody
{
    // tg: code/modules/cargo/goodies.dm:273
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        desc = Dm.str(Dm.initial(typeof(obj_item_toy_beach_ball_branded), (obj_item_toy_beach_ball_branded p) => p.desc));
    }
}
