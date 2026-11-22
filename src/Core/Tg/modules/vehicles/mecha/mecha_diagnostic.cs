// Translated from tgstation (snapshot 1d99bd7f): code/modules/vehicles/mecha/mecha_diagnostic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/modules/vehicles/mecha/mecha_diagnostic.dm:1
public partial class obj_item_mecha_diagnostic : obj_item
{
    // tg: code/modules/vehicles/mecha/mecha_diagnostic.dm:15
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(set_light_on(TRUE));
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mecha_diagnostic.dm:19
    public override object Destroy(float? force = null)
    {
        object dot = base.Destroy(force);
        mech_data = null;
        return dot;
    }

    // tg: code/modules/vehicles/mecha/mecha_diagnostic.dm:23
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (mech_data != null)
            Dm.append(ref dot, I18n.T("This holodata sheet consists of data from a <b>[]</b>.", Dm.initial(mech_data, "name")));
        return dot;
    }
}
