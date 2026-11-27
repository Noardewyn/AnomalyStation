// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/hostile/megafauna/clockwork_knight.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

/*

Difficulty: Very Easy
Was planned to be something more but due to the content freeze it unfortunately cannot be
I'd rather there be something than the clockwork ruin be entirely empty though so here is a basic mob

*/

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/clockwork_knight.dm:9
public partial class mob_living_simple_animal_hostile_megafauna_clockwork_defender : mob_living_simple_animal_hostile_megafauna
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/clockwork_knight.dm:42
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        ADD_TRAIT(this, TRAIT_NO_FLOATING_ANIM, INNATE_TRAIT);
        return dot;
    }

    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/clockwork_knight.dm:46
    public override async DmTask OpenFire(atom A = null)
    {
        await DmTask.CompletedTask;
    }
}
