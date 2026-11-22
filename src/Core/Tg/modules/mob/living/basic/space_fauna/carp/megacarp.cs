// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/space_fauna/carp/megacarp.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

/**
 * # Mega Space Carp
 *
 * Carp which is bigger and stronger, and randomly tougher still.
 */
// tg: code/modules/mob/living/basic/space_fauna/carp/megacarp.dm:7
public partial class mob_living_basic_carp_mega : mob_living_basic_carp
{
    // tg: code/modules/mob/living/basic/space_fauna/carp/megacarp.dm:26
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = Dm.text("[] []", pick(GLOB.megacarp_first_names), pick(GLOB.megacarp_last_names));
        melee_damage_lower = Dm.num(melee_damage_lower) + rand(2, 10);
        melee_damage_upper = Dm.num(melee_damage_upper) + rand(10, 20);
        maxHealth = Dm.num(maxHealth) + rand(30, 60);
        health = maxHealth;
        return dot;
    }

    // tg: code/modules/mob/living/basic/space_fauna/carp/megacarp.dm:34
    public override object get_hud_x_offset()
    {
        return -5f;
    }
}
