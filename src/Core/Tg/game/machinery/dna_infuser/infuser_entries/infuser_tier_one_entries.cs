// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/dna_infuser/infuser_entries/infuser_tier_one_entries.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3b, D327): the tier one entries (goliath, carp, rat, roach, fish, ink, TTX healing, amphibious, stoat); the
// fish's and the stoat's organ choice. A fish's traits that point at another entry (`GLOB.fish_traits`, fishing) wait for fishing
// (not-translated section 166). The var blocks are generated (D111).

// tg: code/game/machinery/dna_infuser/infuser_entries/infuser_tier_one_entries.dm:111
public partial class datum_infuser_entry_fish : datum_infuser_entry
{
    // tg: code/game/machinery/dna_infuser/infuser_entries/infuser_tier_one_entries.dm:139
    public override DmList get_output_organs(mob_living_carbon_human target = null, atom_movable infused_from__arg = null)
    {
        if (infused_from__arg is not obj_item_fish infused_from)
            return base.get_output_organs(target, infused_from__arg);

        ///Get a list of possible alternatives to the standard fish infusion. We prioritize special infusions over it.
        if (Dm.truthy(length(infused_from.fish_traits)))
            Dm.Deferred("code/game/machinery/dna_infuser/infuser_entries/infuser_tier_one_entries.dm:145", "fish get_output_organs: a fish trait's own infusion entry (GLOB.fish_traits, fishing)");

        DmList organs = base.get_output_organs(target, infused_from);
        if (Dm.eq(infused_from.required_fluid_type, AQUARIUM_FLUID_AIR) || HAS_TRAIT(infused_from, TRAIT_FISH_AMPHIBIOUS))
            Dm.remove(ref organs, typeof(obj_item_organ_lungs_fish));
        return organs;
    }
}

// tg: code/game/machinery/dna_infuser/infuser_entries/infuser_tier_one_entries.dm:221
public partial class datum_infuser_entry_stoat : datum_infuser_entry
{
    // tg: code/game/machinery/dna_infuser/infuser_entries/infuser_tier_one_entries.dm:248
    public override DmList get_output_organs(mob_living_carbon_human target = null, atom_movable infused_from = null)
    {
        // eyes and ears are low priority, infuse them last
        datum_status_effect_organ_set_bonus_stoat organ_tracker = target.has_status_effect(typeof(datum_status_effect_organ_set_bonus_stoat)) as datum_status_effect_organ_set_bonus_stoat;
        if (organ_tracker != null && organ_tracker.organs < 3)
            return Dm.list(
                typeof(obj_item_organ_heart_stoat),
                typeof(obj_item_organ_tongue_stoat),
                typeof(obj_item_organ_snout_stoat));

        return base.get_output_organs(target, infused_from);
    }
}
