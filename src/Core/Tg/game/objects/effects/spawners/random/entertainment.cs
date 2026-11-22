// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/spawners/random/entertainment.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate G4a, D316: the toy spawner, a mail goodie, is its only proc). The var blocks are generated (D111).

// tg: code/game/objects/effects/spawners/random/entertainment.dm:253
public partial class obj_effect_spawner_random_entertainment_toy : obj_effect_spawner_random_entertainment
{
    // tg: code/game/objects/effects/spawners/random/entertainment.dm:258
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Dm.append(ref loot, GLOB.arcade_prize_pool);
        return base.Initialize(mapload, __extra);
    }
}
