// Translated from tgstation (snapshot 1d99bd7f): code/datums/ruins/lavaland.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the ruin templates' var blocks are generated; the random blood-drunk miner picks its map.

// tg: code/datums/ruins/lavaland.dm:171
public partial class datum_map_template_ruin_lavaland_blood_drunk_miner_random : datum_map_template_ruin_lavaland_blood_drunk_miner
{
    // tg: code/datums/ruins/lavaland.dm:176
    public override async DmTask New(params object[] __extra)
    {
        suffix = pick("lavaland_surface_blooddrunk1.dmm", "lavaland_surface_blooddrunk2.dmm", "lavaland_surface_blooddrunk3.dmm");
        await base.New(__extra);
    }
}
