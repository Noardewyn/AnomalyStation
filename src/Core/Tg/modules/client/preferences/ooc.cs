// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/ooc.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

/// The color admins will speak in for OOC.
// tg: code/modules/client/preferences/ooc.dm:2
[DmPath("/datum/preference/color/ooc_color")]
public partial class datum_preference_color_ooc_color : datum_preference_color
{
    // tg: code/modules/client/preferences/ooc.dm:7
    public override object create_default_value()
    {
        return "#c43b23";
    }

    // tg: code/modules/client/preferences/ooc.dm:10
    public override object is_accessible(datum_preferences preferences = null)
    {
        if (!Dm.truthy(base.is_accessible(preferences)))
            return FALSE;

        return Dm.truthy(is_admin(preferences.parent)) || Dm.truthy(preferences.unlock_content) ? TRUE : FALSE;
    }
}
