// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/datums.dm
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
    ///Check if a datum has not been deleted and is a valid source
    // tg: code/__HELPERS/datums.dm:2
    public static object is_valid_src(datum source_datum = null)
    {
        if (source_datum is datum)
            return Dm.num(!QDELETED(source_datum));
        return FALSE;
    }

    // tg: code/__HELPERS/datums.dm:7
    public static object call_async(datum source = null, object proc_type = null, DmList arguments = null)
    {
        return Waiting.WaitforFalse(call_async__body(source, proc_type, arguments, null), null);
    }

    // The body of the set waitfor = FALSE wrapper above (waiting-model.md 5.4); it never writes '.' before it may
    // sleep, so it has no box.
    private static async DmTask<object> call_async__body(datum source, object proc_type, DmList arguments, DotBox<object> dot)
    {
        return await Dm.Call(source, (string)proc_type, Dm.arglist(arguments));
    }
}
