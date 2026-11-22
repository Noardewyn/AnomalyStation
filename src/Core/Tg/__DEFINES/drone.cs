// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/drone.dm (its global; the #defines are generated)
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tg;

// GLOBAL_VAR_INIT expanded inline (rules 10.6, 11.1 (b)), as preloader.cs; gaps S4b (D327): BotKeeper's drone access toggle.
public partial class datum_controller_global_vars : datum_controller
{
    /// If drones are blacklisted from certain sensitive machines
    // tg: code/__DEFINES/drone.dm:2  GLOBAL_VAR_INIT(drone_machine_blacklist_enabled, FALSE)
    public object drone_machine_blacklist_enabled;

    // tg: code/__DEFINES/drone.dm:2
    public virtual void InitGlobaldrone_machine_blacklist_enabled()
    {
        drone_machine_blacklist_enabled = Dm.FALSE;
        Dm.append(ref gvars_datum_init_order, "drone_machine_blacklist_enabled");
    }
}
