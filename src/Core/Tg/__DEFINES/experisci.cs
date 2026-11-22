// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/experisci.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps RD3 (D333, the experiments): the function-like macros of experisci.dm, the progress stages an experiment shows in its window
// (rules 11.1); the constants are generated into Defines.

public static partial class GlobalProcs
{
    /// Macro for defining a progress stage
    // tg: code/__DEFINES/experisci.dm:17  #define EXPERIMENT_PROGRESS(type, desc, values...) list(list(type, desc, values))
    public static DmList EXPERIMENT_PROGRESS(object type, object desc, params object[] values)
    {
        var items = new object[2 + values.Length];
        items[0] = type;
        items[1] = desc;
        Array.Copy(values, 0, items, 2, values.Length);
        return Dm.list(Dm.list(items)); // the variadic values are the inner list's own items, as DM's expansion places them
    }

    /// Macro for boolean stages
    // tg: code/__DEFINES/experisci.dm:19  #define EXPERIMENT_PROG_BOOL(desc, complete) EXPERIMENT_PROGRESS(EXPERIMENT_BOOL_STAGE, desc, complete)
    public static DmList EXPERIMENT_PROG_BOOL(object desc, object complete) => EXPERIMENT_PROGRESS(EXPERIMENT_BOOL_STAGE, desc, complete);

    /// Macro for integer stages
    // tg: code/__DEFINES/experisci.dm:21  #define EXPERIMENT_PROG_INT(desc, complete, total) EXPERIMENT_PROGRESS(EXPERIMENT_INT_STAGE, desc, complete, total)
    public static DmList EXPERIMENT_PROG_INT(object desc, object complete, object total) => EXPERIMENT_PROGRESS(EXPERIMENT_INT_STAGE, desc, complete, total);

    /// Macro for float stages
    // tg: code/__DEFINES/experisci.dm:23  #define EXPERIMENT_PROG_FLOAT(desc, complete) EXPERIMENT_PROGRESS(EXPERIMENT_FLOAT_STAGE, desc, complete)
    public static DmList EXPERIMENT_PROG_FLOAT(object desc, object complete) => EXPERIMENT_PROGRESS(EXPERIMENT_FLOAT_STAGE, desc, complete);

    /// Macro for non-valued stages, details for exp stages
    // tg: code/__DEFINES/experisci.dm:25  #define EXPERIMENT_PROG_DETAIL(desc, complete) EXPERIMENT_PROGRESS(EXPERIMENT_DETAIL_STAGE, desc, complete)
    public static DmList EXPERIMENT_PROG_DETAIL(object desc, object complete) => EXPERIMENT_PROGRESS(EXPERIMENT_DETAIL_STAGE, desc, complete);
}
