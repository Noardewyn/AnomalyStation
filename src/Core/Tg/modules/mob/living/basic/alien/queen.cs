// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/alien/queen.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the alien queen and the large empress, which spit damaging neurotoxin. The var blocks are generated.

// tg: code/modules/mob/living/basic/alien/queen.dm:1
public partial class mob_living_basic_alien_queen : mob_living_basic_alien
{
    // tg: code/modules/mob/living/basic/alien/queen.dm:20
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ranged_attacks), Dm.named("projectile_type", projectiletype), Dm.named("projectile_sound", projectilesound), Dm.named("cooldown_time", 1 * SECONDS)));
        return dot;
    }
}
