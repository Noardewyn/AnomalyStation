// Translated from tgstation (snapshot 1d99bd7f): code/controllers/controller.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/controllers/controller.dm:1
[DmPath("/datum/controller")]
public partial class datum_controller : datum
{
    // tg: code/controllers/controller.dm:2
    public object name;
    // The object used for the clickable stat() button.
    // tg: code/controllers/controller.dm:4  var/obj/effect/statclick/statclick: not declared, the stat panel is not
    // translated (rules 13) and /obj/effect/statclick has no class yet.

    // tg: code/controllers/controller.dm:6
    public virtual async DmTask<object> Initialize(params object[] __extra)
    {
        return null;
    }

    //cleanup actions
    // tg: code/controllers/controller.dm:9
    public virtual async DmTask Shutdown()
    {
    }

    //when we enter dmm_suite.load_map
    // tg: code/controllers/controller.dm:12
    public virtual async DmTask StartLoadingMap()
    {
    }

    //when we exit dmm_suite.load_map
    // tg: code/controllers/controller.dm:15
    public virtual void StopLoadingMap(params object[] __extra)
    {
    }

    // tg: code/controllers/controller.dm:17
    public virtual void Recover()
    {
    }

    // tg: code/controllers/controller.dm:19
    public virtual object stat_entry(object msg = null)
    {
        return null;
    }
}
