// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/simple_animal/hostile/megafauna/_megafauna.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M5x slice 5): only the megafauna's singularity_act (its health set to 0 before the living mob's). The megafauna are M10+.

// tg: code/modules/mob/living/simple_animal/hostile/megafauna/_megafauna.dm:111
public partial class mob_living_simple_animal_hostile_megafauna : mob_living_simple_animal_hostile
{
    // tg: code/modules/mob/living/simple_animal/hostile/megafauna/_megafauna.dm:111
    public override async DmTask<object> singularity_act()
    {
        set_health(0);
        return await base.singularity_act();
    }
}
