// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/slime/death.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

public partial class mob_living_basic_slime
{
    // tg: code/modules/mob/living/basic/slime/death.dm:1
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (stat == DEAD)
            return null;
        if (!Dm.truthy(gibbed) && Dm.eq(life_stage, SLIME_LIFE_STAGE_ADULT))
        {
            mob_living_basic_slime new_slime = Dm.@new<mob_living_basic_slime>(drop_location(), slime_type.GetType());

            (new_slime.ai_controller as datum_ai_controller)?.set_blackboard_key(BB_SLIME_RABID, TRUE);
            await new_slime.regenerate_icons();

            set_life_stage(SLIME_LIFE_STAGE_BABY);
            await revive(HEAL_ALL);
            await regenerate_icons();
            update_name();
            return null;
        }

        if (buckled != null)
            stop_feeding(TRUE); //releases ourselves from the mob we fed on.

        cut_overlays();

        return await base.death(gibbed, __extra);
    }

    // tg: code/modules/mob/living/basic/slime/death.dm:23
    public override async DmTask gib(float? drop_bitflags = null)
    {
        await death(TRUE);
        qdel(this);
    }
}
