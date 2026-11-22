// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/species.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice F: the species preference; gaps SP1 (D338): compile_constant_data (the species page's descriptions, lore, perks and diet) and
// the randomization's species flag (middleware/random.dm:83).

/// Species preference
// tg: code/modules/client/preferences/species.dm:2
[DmPath("/datum/preference/choiced/species")]
public partial class datum_preference_choiced_species : datum_preference_choiced
{
    // tg: code/modules/client/preferences/species.dm:8
    public override object deserialize(object input = null, datum_preferences preferences = null)
    {
        return GLOB.species_list[sanitize_inlist(input, get_choices_serialized(), SPECIES_HUMAN)];
    }

    // tg: code/modules/client/preferences/species.dm:11
    public override object serialize(object input = null)
    {
        Type species = input as Type;
        return Dm.initial(species, (datum_species p) => p.id);
    }

    // tg: code/modules/client/preferences/species.dm:15
    public override object create_default_value()
    {
        return typeof(datum_species_human);
    }

    // tg: code/modules/client/preferences/species.dm:18
    public override object create_random_value(datum_preferences preferences = null)
    {
        return pick(get_choices());
    }

    // tg: code/modules/client/preferences/species.dm:21
    public override DmList init_possible_values()
    {
        DmList values = Dm.list();

        foreach (object species_id in Dm.each(get_selectable_species()))
            Dm.append(ref values, GLOB.species_list[species_id]);

        return values;
    }

    // tg: code/modules/client/preferences/species.dm:29
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        target.set_species(value, FALSE, TRUE);
    }
    // tg: code/modules/client/preferences/species.dm:32
    public override object compile_constant_data()
    {
        DmList data = Dm.list();

        foreach (object species_id in Dm.each(get_selectable_species()))
        {
            object species_type = GLOB.species_list[species_id];
            datum_species species = GLOB.species_prototypes[species_type] as datum_species;

            DmList entry = Dm.list();
            data[species_id] = entry;
            entry["name"] = species.name;
            entry["desc"] = species.get_species_description();
            entry["lore"] = species.get_species_lore();
            entry["icon"] = sanitize_css_class_name(species.name);
            entry["use_skintones"] = Dm.@in(TRAIT_USES_SKINTONES, species.inherent_traits) ? TRUE : FALSE;
            entry["sexes"] = species.sexes;
            entry["enabled_features"] = species.get_features();
            entry["perks"] = species.get_species_perks();
            entry["diet"] = species.get_species_diet();
        }

        return data;
    }

    // tg: code/modules/client/preferences/middleware/random.dm:83
    public override object included_in_randomization_flags(object randomize_flags = null)
    {
        return Dm.truthy(Dm.bitand(randomize_flags, RANDOMIZE_SPECIES)) ? TRUE : FALSE;
    }
}
