// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/bodyparts/bodypart_effects/photosynthesis.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Species package SP2b (D349): the photosynthesis of the podperson's limbs. The var blocks are generated (D111).

// tg: code/modules/surgery/bodyparts/bodypart_effects/photosynthesis.dm:2
public partial class datum_status_effect_grouped_bodypart_effect_photosynthesis : datum_status_effect_grouped_bodypart_effect
{
    // tg: code/modules/surgery/bodyparts/bodypart_effects/photosynthesis.dm:7
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        float light_amount = 0; // How much light there is in the place, affects receiving nutrition and healing
        float bodypart_coefficient = get_bodypart_coefficient();

        if (!isturf(owner.loc)) // There's considered to be no light inside of objects
        {
            if (Dm.num(owner.nutrition) < NUTRITION_LEVEL_STARVING + 50)
                await owner.take_overall_damage(brute: 1 * bodypart_coefficient, required_bodytype: BODYTYPE_PLANT);
            return null;
        }

        turf turf_loc = (turf)owner.loc;
        light_amount = Math.Min(1f, Dm.num(turf_loc.get_lumcount())) - 0.5f;

        if (Dm.num(owner.nutrition) < NUTRITION_LEVEL_ALMOST_FULL)
            await owner.adjust_nutrition(5 * light_amount * bodypart_coefficient);

        if (light_amount > 0.2f) // If there's enough light, heal
        {
            bool need_mob_update = false;
            need_mob_update |= Dm.truthy(await owner.heal_overall_damage(brute: 0.5f * bodypart_coefficient,
                burn: 0.5f * bodypart_coefficient, updating_health: FALSE, required_bodytype: BODYTYPE_PLANT));
            need_mob_update |= Dm.truthy(await owner.adjust_tox_loss(-0.5f * bodypart_coefficient, updating_health: FALSE));
            need_mob_update |= Dm.truthy(await owner.adjust_oxy_loss(-0.5f * bodypart_coefficient, updating_health: FALSE));
            if (need_mob_update)
                await owner.updatehealth();
        }

        if (Dm.num(owner.nutrition) < NUTRITION_LEVEL_STARVING + 50)
            await owner.take_overall_damage(brute: 1 * bodypart_coefficient, required_bodytype: BODYTYPE_PLANT);
        return null;
    }
}
