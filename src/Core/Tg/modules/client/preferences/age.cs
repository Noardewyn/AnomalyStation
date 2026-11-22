// Translated from tgstation (snapshot 1d99bd7f): code/modules/client/preferences/age.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/client/preferences/age.dm:1
[DmPath("/datum/preference/numeric/age")]
public partial class datum_preference_numeric_age : datum_preference_numeric
{
    // tg: code/modules/client/preferences/age.dm:9
    public override void apply_to_human(mob_living_carbon_human target = null, object value = null, datum_preferences preferences = null)
    {
        target.age = Dm.num(value);
    }

    // tg: code/modules/client/preferences/age.dm:12
    public override object create_informed_default_value(datum_preferences preferences = null)
    {
        return rand(Dm.max(Dm.num(minimum), 21), Dm.min(Dm.num(maximum), 50));
    }
}
