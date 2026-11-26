// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/bodyparts/bodypart_effects/plasma_based.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP2a (D349): the whole file: a plasmaman's limbs set him alight in oxygen unless an envirosuit seals him; the var block is
// generated (D111).

/// Causes the owner to spontaneously combust when exposed to oxygen
// tg: code/modules/surgery/bodyparts/bodypart_effects/plasma_based.dm:2
public partial class datum_status_effect_grouped_bodypart_effect_plasma_based : datum_status_effect_grouped_bodypart_effect
{
    // tg: code/modules/surgery/bodyparts/bodypart_effects/plasma_based.dm:12
    public override async DmTask<object> tick(object seconds_between_ticks = null)
    {
        if (!ishuman(owner) || owner.loc == null) // No xenos, sorry
            return null;

        mob_living_carbon_human as_human = owner as mob_living_carbon_human;
        if (HAS_TRAIT(owner, TRAIT_STASIS) || Dm.truthy(as_human.is_atmos_sealed(additional_flags: PLASMAMAN_PREVENT_IGNITION, check_hands: TRUE)))
        {
            if (!Dm.truthy(owner.on_fire))
                REMOVE_TRAIT(owner, TRAIT_IGNORE_FIRE_PROTECTION, type);
            return null;
        }

        datum_gas_mixture environment = await owner.loc.return_air() as datum_gas_mixture;
        if (environment == null || !Dm.truthy(environment.total_moles()))
        {
            if (!Dm.truthy(owner.on_fire))
                REMOVE_TRAIT(owner, TRAIT_IGNORE_FIRE_PROTECTION, type);
            return null;
        }

        if (Dm.num(environment.moles[typeof(datum_gas_hypernoblium)]) >= 5)
        {
            if (Dm.truthy(owner.on_fire) && owner.fire_stacks > 0)
                await owner.adjust_fire_stacks(-fire_stacks_loss * Dm.num(seconds_between_ticks) * length(bodyparts));
            else
                REMOVE_TRAIT(owner, TRAIT_IGNORE_FIRE_PROTECTION, type);
            return null;
        }

        if (HAS_TRAIT(owner, TRAIT_NOFIRE))
        {
            REMOVE_TRAIT(owner, TRAIT_IGNORE_FIRE_PROTECTION, type);
            return null;
        }

        ADD_TRAIT(owner, TRAIT_IGNORE_FIRE_PROTECTION, type);

        if (Dm.num(environment.moles[typeof(datum_gas_oxygen)]) < 1) //Same threshhold that extinguishes fire
            return null;

        await owner.adjust_fire_stacks(fire_stacks_per_second * Dm.num(seconds_between_ticks) * length(bodyparts));
        if (Dm.truthy(owner.ignite_mob()))
            owner.visible_message(span_danger(I18n.T("[]'s body reacts with the atmosphere and bursts into flames!", owner)), span_userdanger(I18n.T("Your body reacts with the atmosphere and bursts into flame!")));
        return null;
    }
}
