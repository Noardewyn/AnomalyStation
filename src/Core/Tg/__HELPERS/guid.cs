// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/guid.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public static partial class GlobalProcs
{
    /**
     * returns a GUID like identifier (using a mostly made up record format)
     * guids are not on their own suitable for access or security tokens, as most of their bits are predictable.
     * (But may make a nice salt to one)
    **/
    // tg: code/__HELPERS/guid.dm:6
    // SHIM-GAP: GUID hashes md5 (Appendix C: planned) over BYOND's world.hub, hub_password, internet_address, address,
    // status and port, which the DmWorld facade does not have yet, and formats with tg's num2hex (not translated). Its only
    // M0 caller is world/Genesis (game/world.dm:91), where it names the temporary config_error log that SetupLogs replaces,
    // so it is a deferred leaf: it returns null and that log is "data/logs/config_error..log".
    public static object GUID()
    {
        Dm.Deferred("code/__HELPERS/guid.dm:6", "GUID: needs md5 and BYOND's hub/address/port world vars (shim); only names the temporary config_error log in M0");
        return null;
    }
}
