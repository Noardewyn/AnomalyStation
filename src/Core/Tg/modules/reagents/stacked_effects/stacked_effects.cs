// Translated from tgstation (snapshot 1d99bd7f): code/modules/reagents/stacked_effects/stacked_effects.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (M8 slice A: metabolize applies the stacked effects). The var block is generated (D111).

///Side effects from metabolizing an reagent or a combination of them
// tg: code/modules/reagents/stacked_effects/stacked_effects.dm:2
[DmPath("/datum/stacked_metabolization_effect")]
public partial class datum_stacked_metabolization_effect : datum
{
    /**
     * Returns the average metabolzation ratio from an list
     * Arguments
     *
     * * list/reagents_metabolized - the list of reagents metabolized
    */
    // tg: code/modules/reagents/stacked_effects/stacked_effects.dm:13
    public virtual object average(DmList reagents_metabolized = null)
    {
        // SHOULD_BE_PURE(TRUE), SHOULD_NOT_OVERRIDE(TRUE)

        float average = 0;
        foreach (object test in Dm.each(reagents_metabolized))
            average += Dm.num(reagents_metabolized[test]);
        return average / length(reagents_metabolized);
    }

    /**
     * Checks if this side effect can be applied on the mob
     * Arguments
     *
     * * list/reagents_metabolized - a map of reagent type path -> metabolization_ratio of all reagents
     * * mob/living/carbon/owner - the mob to apply the side effects to
     * * seconds_per_tick - passed from /datum/reagents/proc/metabolize_reagent()
    */
    // tg: code/modules/reagents/stacked_effects/stacked_effects.dm:30
    public virtual object check_and_apply(DmList reagents_metabolized = null, mob_living_carbon owner = null, object seconds_per_tick = null)
    {
        // SHOULD_NOT_OVERRIDE(TRUE)
        object dot = 0;

        DmList requirements_needed = requirements;
        foreach (object test in Dm.each(reagents_metabolized))
        {
            foreach (object requirement in Dm.each(requirements_needed))
            {
                if (ispath(test, requirement as Type))
                {
                    if (requirements_needed == requirements)
                        requirements_needed = requirements.Copy();
                    requirements_needed[requirement] = Dm.num(requirements_needed[requirement]) - 1;
                    if (!Dm.truthy(requirements_needed[requirement]))
                        requirements_needed.Remove(requirement);
                    break;
                }
            }
        }

        if (!Dm.truthy(length(requirements_needed)))
            return apply(reagents_metabolized, owner, seconds_per_tick);
        return dot;
    }

    /**
     * Apply a list of side effects to an mob once they have metabolized the requirments
     * Arguments
     *
     * * list/reagents_metabolized - a map of reagent -> metabolization_ratio of all reagents
     * * mob/living/carbon/owner - the mob to apply the side effects to
     * * seconds_per_tick - passed from /datum/reagents/proc/metabolize_reagent()
     * Returns a positive value if the mobs health needs to be updated
    */
    // tg: code/modules/reagents/stacked_effects/stacked_effects.dm:57
    public virtual object apply(DmList reagents_metabolized = null, mob_living_carbon owner = null, object seconds_per_tick = null)
    {
        // PROTECTED_PROC(TRUE), SHOULD_NOT_SLEEP(TRUE)

        return 0;
    }
}
