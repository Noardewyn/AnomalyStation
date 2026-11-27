// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/alien/sentinel.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the alien sentinel, which spits damaging neurotoxin. The var block is generated.

// tg: code/modules/mob/living/basic/alien/sentinel.dm:1
public partial class mob_living_basic_alien_sentinel : mob_living_basic_alien
{
    // tg: code/modules/mob/living/basic/alien/sentinel.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ranged_attacks), Dm.named("projectile_type", projectiletype), Dm.named("projectile_sound", projectilesound), Dm.named("cooldown_time", 1 * SECONDS)));
        return dot;
    }
}
