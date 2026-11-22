// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/shrapnel.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps OD1 (D333): the stingball pellet's rebounds hit prone targets. The rest of the file is var blocks only, generated (D111).

// tg: code/game/objects/items/shrapnel.dm:74
public partial class obj_projectile_bullet_pellet_stingball : obj_projectile_bullet_pellet
{
    // tg: code/game/objects/items/shrapnel.dm:98
    public override void on_ricochet(atom target = null)
    {
        hit_prone_targets = TRUE; // ducking will save you from the first wave, but not the rebounds
        ignore_range_hit_prone_targets = TRUE;
    }
}
