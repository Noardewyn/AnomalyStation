// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/security_department.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// Which department to put security officers in, when the config is enabled
// tg: code/modules/client/preferences/security_department.dm:2
public partial class datum_preference_choiced_security_department : datum_preference_choiced
{
    // This is what that #warn wants you to remove :)
    // tg: code/modules/client/preferences/security_department.dm:10
    public override object deserialize(object input = null, datum_preferences preferences = null)
    {
        if (!Dm.@in(input, GLOB.security_depts_prefs))
            return SEC_DEPT_NONE;
        return base.deserialize(input, preferences);
    }

    // tg: code/modules/client/preferences/security_department.dm:15
    public override DmList init_possible_values()
    {
        return GLOB.security_depts_prefs;
    }

    // tg: code/modules/client/preferences/security_department.dm:18
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        return;
    }

    // tg: code/modules/client/preferences/security_department.dm:21
    public override object create_default_value()
    {
        return SEC_DEPT_NONE;
    }

    // tg: code/modules/client/preferences/security_department.dm:24
    public override object is_accessible(datum_preferences preferences = null)
    {
        if (!Dm.truthy(base.is_accessible(preferences)))
            return FALSE;

        return !Dm.truthy(config.Get(typeof(datum_config_entry_flag_sec_start_brig))) ? TRUE : FALSE;
    }
}
