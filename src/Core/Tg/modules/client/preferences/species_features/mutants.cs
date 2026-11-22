// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/species_features/mutants.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps SP1 (D338): the whole file: the mutant colour (the lizard's scales, its limbs' and organs' colour).

// tg: code/modules/client/preferences/species_features/mutants.dm:1
public partial class datum_preference_color_mutant_color : datum_preference_color
{
    // tg: code/modules/client/preferences/species_features/mutants.dm:7
    public override object is_accessible(datum_preferences preferences = null)
    {
        if (!Dm.truthy(base.is_accessible(preferences)))
            return FALSE;

        object species_type = preferences.read_preference(typeof(datum_preference_choiced_species));
        datum_species species = GLOB.species_prototypes[species_type] as datum_species;
        return !Dm.@in(TRAIT_FIXED_MUTANT_COLORS, species.inherent_traits) ? TRUE : FALSE;
    }

    // tg: code/modules/client/preferences/species_features/mutants.dm:15
    public override object create_default_value()
    {
        return sanitize_hexcolor(Dm.text("[][][]", pick("7F", "FF"), pick("7F", "FF"), pick("7F", "FF")));
    }

    // tg: code/modules/client/preferences/species_features/mutants.dm:18
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        object species_type = preferences.read_preference(typeof(datum_preference_choiced_species));
        datum_species species = GLOB.species_prototypes[species_type] as datum_species;
        if (!Dm.@in(TRAIT_FIXED_MUTANT_COLORS, species.inherent_traits))
            target.dna.features[FEATURE_MUTANT_COLOR] = value;
    }

    // tg: code/modules/client/preferences/species_features/mutants.dm:24
    public override object is_valid(object value = null, datum_preferences preferences = null)
    {
        if (!Dm.truthy(base.is_valid(value, preferences)))
            return FALSE;

        if (Dm.truthy(is_color_dark(value, 15)))
            return FALSE;

        return TRUE;
    }
}
