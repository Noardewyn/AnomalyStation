// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/bodyparts/bodypart_effects/nyxosynthesis.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP3b (D349): the whole file: the shadowperson's limbs heal in the dark and burn in the light. The var block is generated (D111).

/// This limb heals in darkness and dies in light!
/// Only BODYTYPE_SHADOW limbs will heal, but limbs without the flag (and with the effect) still contribute to healing of the other limbs
// tg: code/modules/surgery/bodyparts/bodypart_effects/nyxosynthesis.dm:3
public partial class datum_status_effect_grouped_bodypart_effect_nyxosynthesis : datum_status_effect_grouped_bodypart_effect
{
    // tg: code/modules/surgery/bodyparts/bodypart_effects/nyxosynthesis.dm:7
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        turf owner_turf = owner.loc as turf;
        if (owner_turf == null)
            return null;

        float bodypart_coefficient = get_bodypart_coefficient();
        if (!Dm.truthy(owner_turf.check_lumcount_below(SHADOW_SPECIES_LIGHT_THRESHOLD)))
        {
            await owner.take_overall_damage(brute: 1 * bodypart_coefficient, burn: 1 * bodypart_coefficient, required_bodytype: BODYTYPE_SHADOW);
            return null;
        }

        // Heal in the dark
        await owner.heal_overall_damage(brute: 0.5f * bodypart_coefficient, burn: 0.5f * bodypart_coefficient, required_bodytype: BODYTYPE_SHADOW);

        mob_living_carbon carbon_owner = (mob_living_carbon)owner;
        if (!Dm.eq(((datum_species)carbon_owner.dna.species).id, SPECIES_NIGHTMARE)) // Somewhat awkward, but let's not duplicate the alerts
        {
            // This only appears when in shadows, don't move to bodypart effect so people with nightvision can still tell if they're in light or not
            await owner.apply_status_effect(typeof(datum_status_effect_shadow));
        }
        return null;
    }
}
