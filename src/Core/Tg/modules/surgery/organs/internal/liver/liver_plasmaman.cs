// Translated from tgstation (snapshot 1d99bd7f): code/modules/surgery/organs/internal/liver/liver_plasmaman.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps OG1, D339, D341): the plasmaman's reagent processing crystal, which works in any carbon. The var block is generated
// (D111).

/**
 * Plasmaman liver
 * Makes plasma and hot ice heal wounds, also makes gunpowder a hallucinogen.
 **/
// tg: code/modules/surgery/organs/internal/liver/liver_plasmaman.dm:5
public partial class obj_item_organ_liver_bone_plasmaman : obj_item_organ_liver_bone
{
    // tg: code/modules/surgery/organs/internal/liver/liver_plasmaman.dm:13
    public override object handle_chemical(mob_living_carbon organ_owner = null, datum_reagent chem = null, object seconds_per_tick = null, object metabolization_rate = null)
    {
        object dot = base.handle_chemical(organ_owner, chem, seconds_per_tick, metabolization_rate);
        // parent returned COMSIG_MOB_STOP_REAGENT_TICK or we are failing
        if (Dm.truthy(Dm.bitand(dot, COMSIG_MOB_STOP_REAGENT_TICK)) || Dm.truthy(Dm.bitand(organ_flags, ORGAN_FAILING)))
            return dot;
        // plasmamen use plasma to reform their bones or whatever
        if (istype(chem, typeof(datum_reagent_toxin_plasma)) || istype(chem, typeof(datum_reagent_toxin_hot_ice)))
        {
            foreach (datum_wound iter_wound in Dm.each_cast<datum_wound>(organ_owner.all_wounds))
                Waiting.NoSleep(iter_wound.on_xadone(2 * Dm.num(seconds_per_tick)));
            return dot; // Do normal metabolism
        }
        // plasmamen get high on gunpowder lol
        if (istype(chem, typeof(datum_reagent_gunpowder)))
        {
            Waiting.NoSleep(organ_owner.set_timed_status_effect(15 * SECONDS * Dm.num(seconds_per_tick), typeof(datum_status_effect_drugginess)));
            if (Dm.num(organ_owner.get_timed_status_effect_duration(typeof(datum_status_effect_hallucination))) / 10 < Dm.num(chem.volume))
                Waiting.NoSleep(organ_owner.adjust_hallucinations(2.5f * SECONDS * Dm.num(seconds_per_tick)));
            return dot; // Do normal metabolism
        }
        return dot;
    }
}
