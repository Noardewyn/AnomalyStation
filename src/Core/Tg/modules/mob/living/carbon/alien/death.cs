// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/death.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file.

// tg: code/modules/mob/living/carbon/alien/death.dm:1
public partial class mob_living_carbon_alien : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/alien/death.dm:1
    public override void gib_animation()
    {
        Dm.@new<obj_effect_temp_visual_gib_animation>(loc, "gibbed-a");
    }

    // tg: code/modules/mob/living/carbon/alien/death.dm:4
    public override void spawn_dust(float? just_ash = null)
    {
        if (Dm.truthy(just_ash))
        {
            base.spawn_dust(just_ash);
            return;
        }

        obj_effect_decal_remains_xeno bones = Dm.@new<obj_effect_decal_remains_xeno>(loc);
        bones.pixel_z = -6;
        bones.pixel_w = rand(-1, 1);
    }
}
