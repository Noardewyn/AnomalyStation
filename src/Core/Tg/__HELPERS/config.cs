// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/config.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL (M10 slice F: a job's spawn message): get_policy. The config's policy texts are not loaded (LoadPolicy is deferred), so it
// reads null.

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/config.dm:1
    public static object get_policy(object keyword = null)
    {
        return (config.policy as DmList)?[keyword];
    }
}
