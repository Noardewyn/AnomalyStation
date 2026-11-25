// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/species_features/cerulean.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP2c (D349): the whole file: the cerulean's Character Setup features, the fish tail's colour and the frills toggle. The var blocks
// are generated (D111).

/// The color given to people with a fish tail, the selection is exclusive to Ceruleans
// tg: code/modules/client/preferences/species_features/cerulean.dm:2
public partial class datum_preference_color_fish_tail_color : datum_preference_color
{
    // tg: code/modules/client/preferences/species_features/cerulean.dm:8
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        target.dna.features[FEATURE_TAIL_FISH_COLOR] = value;
    }

    // tg: code/modules/client/preferences/species_features/cerulean.dm:11
    public override object create_default_value()
    {
        return pick(Dm.sub(GLOB.carp_colors, COLOR_CARP_SILVER) as DmList);
    }
}

/// cerulean frills. not a choice like lizard frills. just a toggle for yes or no, the accessory is aquatic
// tg: code/modules/client/preferences/species_features/cerulean.dm:16
public partial class datum_preference_toggle_cerulean_frills : datum_preference_toggle
{
    // tg: code/modules/client/preferences/species_features/cerulean.dm:23
    public override object has_relevant_feature(datum_preferences preferences = null)
    {
        return current_species_has_savekey(preferences);
    }

    // tg: code/modules/client/preferences/species_features/cerulean.dm:26
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        if (Dm.truthy(is_species(target, typeof(datum_species_human_cerulean))) && Dm.truthy(value))
        {
            string frills_name = Dm.initial(typeof(datum_sprite_accessory_frills_aquatic), (datum_sprite_accessory p) => p.name) as string;
            target.dna.features[FEATURE_FRILLS] = frills_name;
            (target.dna.species as datum_species).mutant_organs[typeof(obj_item_organ_frills)] = frills_name;
        }
        datum_species species = target.dna.species as datum_species;
        Waiting.NoSleep(species.regenerate_organs(target, GLOB.species_prototypes[species.type] as datum_species, visual_only: FALSE));
    }
}
