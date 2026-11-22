// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/body_type.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/client/preferences/body_type.dm:3
[DmPath("/datum/preference/choiced/body_type")]
public partial class datum_preference_choiced_body_type : datum_preference_choiced
{
    // tg: code/modules/client/preferences/body_type.dm:1  #define USE_GENDER "Use gender" (file-local, #undef at :34)
    private const string USE_GENDER = "Use gender";

    // tg: code/modules/client/preferences/body_type.dm:10
    public override DmList init_possible_values()
    {
        return Dm.list(USE_GENDER, MALE, FEMALE);
    }

    // tg: code/modules/client/preferences/body_type.dm:13
    public override object create_informed_default_value(datum_preferences preferences = null)
    {
        return Dm.truthy(gender_has_physique(preferences.read_preference(typeof(datum_preference_choiced_gender)))) ? USE_GENDER : FEMALE;
    }

    // tg: code/modules/client/preferences/body_type.dm:16
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        if (Dm.eq(value, USE_GENDER))
        {
            value = target.gender;
            if (!Dm.truthy(gender_has_physique(value)))
                value = FEMALE; // non-binary physique does not work for several reasons, big refactor for whoever bites, female is the most common in this scenario
        }

        target.physique = Dm.str(value);
    }

    // tg: code/modules/client/preferences/body_type.dm:24
    public override object is_accessible(datum_preferences preferences = null)
    {
        if (!Dm.truthy(base.is_accessible(preferences)))
            return FALSE;

        Type species = preferences.read_preference(typeof(datum_preference_choiced_species)) as Type;
        return Dm.initial(species, (datum_species p) => p.sexes);
    }
}

public static partial class GlobalProcs
{
    // tg: code/modules/client/preferences/body_type.dm:31
    public static object gender_has_physique(object gender = null)
    {
        return Dm.eq(gender, MALE) || Dm.eq(gender, FEMALE) ? TRUE : FALSE;
    }
}
