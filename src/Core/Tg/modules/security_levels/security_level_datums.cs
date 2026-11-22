// Translated from tgstation (snapshot 1d99bd7f): code/modules/security_levels/security_level_datums.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M10 slice G4 (D272): the whole file (the levels' vars are generated).
/**
 * Security levels
 *
 * These are used by the security level subsystem. Each one of these represents a security level that a player can set.
 *
 * Base type is abstract
 */
// tg: code/modules/security_levels/security_level_datums.dm:9
[DmPath("/datum/security_level")]
public partial class datum_security_level : datum
{
    // tg: code/modules/security_levels/security_level_datums.dm:44
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (lowering_to_configuration_key != null) // I'm not sure about you, but isn't there an easier way to do this?
            lowering_to_announcement = config.Get(lowering_to_configuration_key);
        if (elevating_to_configuration_key != null)
            elevating_to_announcement = config.Get(elevating_to_configuration_key);
    }
}
