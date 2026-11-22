// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/jobless_role.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/client/preferences/jobless_role.dm:1
[DmPath("/datum/preference/choiced/jobless_role")]
public partial class datum_preference_choiced_jobless_role : datum_preference_choiced
{
    // tg: code/modules/client/preferences/jobless_role.dm:5
    public override object create_default_value()
    {
        return BEOVERFLOW;
    }

    // tg: code/modules/client/preferences/jobless_role.dm:8
    public override DmList init_possible_values()
    {
        return Dm.list(BEOVERFLOW, BERANDOMJOB, RETURNTOLOBBY);
    }

    // tg: code/modules/client/preferences/jobless_role.dm:11
    public override object should_show_on_page(object preference_tab = null)
    {
        return Dm.eq(preference_tab, PREFERENCE_TAB_CHARACTER_PREFERENCES) ? TRUE : FALSE;
    }
}
