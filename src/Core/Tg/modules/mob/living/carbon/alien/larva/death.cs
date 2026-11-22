// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/larva/death.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file.

// tg: code/modules/mob/living/carbon/alien/larva/death.dm:1
public partial class mob_living_carbon_alien_larva : mob_living_carbon_alien
{
    // tg: code/modules/mob/living/carbon/alien/larva/death.dm:1
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (stat == DEAD)
            return null;

        object dot = await base.death(gibbed, __extra);

        await update_icons();
        return dot;
    }

    // tg: code/modules/mob/living/carbon/alien/larva/death.dm:9
    public override async DmTask spawn_gibs(float? drop_bitflags = null)
    {
        drop_bitflags ??= NONE;
        if (Dm.truthy(Dm.bitand(drop_bitflags, DROP_BODYPARTS)))
            Dm.@new<obj_effect_gibspawner_larva>(drop_location(), this);
        else
            Dm.@new<obj_effect_gibspawner_larva_bodypartless>(drop_location(), this);
    }

    // tg: code/modules/mob/living/carbon/alien/larva/death.dm:15
    public override void gib_animation()
    {
        Dm.@new<obj_effect_temp_visual_gib_animation>(loc, "gibbed-l");
    }

    // tg: code/modules/mob/living/carbon/alien/larva/death.dm:18
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
