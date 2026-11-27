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
// fish's and the stoat's organ choice, a fish's trait that points at another entry choosing that entry's organs. The var blocks
// are generated (D111).

// tg: code/game/machinery/dna_infuser/infuser_entries/infuser_tier_one_entries.dm:111
public partial class datum_infuser_entry_fish : datum_infuser_entry
{
    // tg: code/game/machinery/dna_infuser/infuser_entries/infuser_tier_one_entries.dm:139
    public override DmList get_output_organs(mob_living_carbon_human target = null, atom_movable infused_from__arg = null)
    {
        if (infused_from__arg is not obj_item_fish infused_from)
            return base.get_output_organs(target, infused_from__arg);

        ///Get a list of possible alternatives to the standard fish infusion. We prioritize special infusions over it.
        DmList possible_alt_infusions = Dm.list();
        foreach (object type in Dm.each(infused_from.fish_traits))
        {
            datum_fish_trait trait = GLOB.fish_traits[type] as datum_fish_trait;
            if (trait.infusion_entry == null)
                continue;
            datum_infuser_entry entry = GLOB.infuser_entries[trait.infusion_entry] as datum_infuser_entry;
            foreach (object organ in Dm.each(entry.output_organs))
            {
                if (!Dm.truthy(target.get_organ_by_type(organ)))
                {
                    Dm.combine(possible_alt_infusions, entry);
                    break;
                }
            }
        }

        if (Dm.truthy(length(possible_alt_infusions)))
        {
            datum_infuser_entry chosen = pick(possible_alt_infusions) as datum_infuser_entry;
            return chosen.get_output_organs(target, infused_from);
        }

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
