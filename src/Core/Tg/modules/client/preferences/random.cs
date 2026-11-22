// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/random.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/client/preferences/random.dm:1
[DmPath("/datum/preference/choiced/random_body")]
public partial class datum_preference_choiced_random_body : datum_preference_choiced
{
    // tg: code/modules/client/preferences/random.dm:7
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        return;
    }

    // tg: code/modules/client/preferences/random.dm:10
    public override DmList init_possible_values()
    {
        return Dm.list(
            RANDOM_ANTAG_ONLY,
            RANDOM_DISABLED,
            RANDOM_ENABLED);
    }

    // tg: code/modules/client/preferences/random.dm:17
    public override object create_default_value()
    {
        return RANDOM_DISABLED;
    }
}

// tg: code/modules/client/preferences/random.dm:20
[DmPath("/datum/preference/toggle/random_hardcore")]
public partial class datum_preference_toggle_random_hardcore : datum_preference_toggle
{
    // tg: code/modules/client/preferences/random.dm:27
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        return;
    }

    // tg: code/modules/client/preferences/random.dm:30
    public override object is_accessible(datum_preferences preferences = null)
    {
        if (!Dm.truthy(base.is_accessible(preferences)))
            return FALSE;

        return preferences.parent != null && Dm.ge(preferences.parent.get_exp_living(pure_numeric: TRUE), PLAYTIME_HARDCORE_RANDOM) ? TRUE : FALSE;
    }
}

// tg: code/modules/client/preferences/random.dm:36
[DmPath("/datum/preference/choiced/random_name")]
public partial class datum_preference_choiced_random_name : datum_preference_choiced
{
    // tg: code/modules/client/preferences/random.dm:42
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        return;
    }

    // tg: code/modules/client/preferences/random.dm:45
    public override DmList init_possible_values()
    {
        return Dm.list(
            RANDOM_ANTAG_ONLY,
            RANDOM_DISABLED,
            RANDOM_ENABLED);
    }

    // tg: code/modules/client/preferences/random.dm:52
    public override object create_default_value()
    {
        return RANDOM_DISABLED;
    }
}
