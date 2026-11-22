// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/liver/liver_golem.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1, D339, D341): the golem's porous rock, which works in any carbon. The var block is generated (D111).

/**
 * Golem liver
 * Basically only exists to remove the nutriment factor from consumables,
 * so golems can only consume minerals even when injecting reagents.
 *
 * Actually consuming golem food is handled by /obj/item/organ/stomach/golem!
 **/
// tg: code/modules/surgery/organs/internal/liver/liver_golem.dm:8
public partial class obj_item_organ_liver_golem : obj_item_organ_liver
{
    // tg: code/modules/surgery/organs/internal/liver/liver_golem.dm:15
    public override object handle_chemical(mob_living_carbon organ_owner = null, datum_reagent chem = null, object seconds_per_tick = null, object metabolization_rate = null)
    {
        object dot = base.handle_chemical(organ_owner, chem, seconds_per_tick, metabolization_rate);
        // parent returned COMSIG_MOB_STOP_REAGENT_TICK or we are failing
        if (Dm.truthy(Dm.bitand(dot, COMSIG_MOB_STOP_REAGENT_TICK)) || Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
            return dot;
        // golems can only eat minerals
        if (istype(chem, typeof(datum_reagent_consumable)) && !istype(chem, typeof(datum_reagent_consumable_nutriment_mineral)))
        {
            datum_reagent_consumable yummy_chem = chem as datum_reagent_consumable;
            yummy_chem.nutriment_factor = 0;
            return dot; // Do normal metabolism
        }
        return dot;
    }
}
