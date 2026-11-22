// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/underwear_color.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/client/preferences/underwear_color.dm:2
[DmPath("/datum/preference/color/underwear_color")]
public partial class datum_preference_color_underwear_color : datum_preference_color
{
    // tg: code/modules/client/preferences/underwear_color.dm:6
    public override object is_accessible(datum_preferences preferences = null)
    {
        if (!Dm.truthy(base.is_accessible(preferences)))
            return FALSE;

        object species_type = preferences.read_preference(typeof(datum_preference_choiced_species));
        datum_species species = GLOB.species_prototypes[species_type] as datum_species;
        return !Dm.@in(TRAIT_NO_UNDERWEAR, species.inherent_traits) ? TRUE : FALSE;
    }

    // tg: code/modules/client/preferences/underwear_color.dm:14
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        target.underwear_color = Dm.str(value);
    }
}
