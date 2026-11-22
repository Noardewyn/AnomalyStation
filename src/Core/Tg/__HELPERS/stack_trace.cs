// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/stack_trace.dm
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
    /// gives us the stack trace from CRASH() without ending the current proc.
    /// Do not call directly, use the [stack_trace] macro instead.
    // tg: code/__HELPERS/stack_trace.dm:3
    // DM: the CRASH ends only this proc ("without ending the current proc"), so it has the proc-level catch of rules 9.3,
    // which reports and returns. The stack_trace macro does not come here: its member reports at the use site directly
    // (__DEFINES/stack_trace.cs); this is for a direct call, which tg forbids.
    public static void _stack_trace(object message = null, object file = null, object line = null)
    {
        try
        {
            throw Dm.CRASH(Dm.text("[][][][]", message, WORKAROUND_IDENTIFIER, json_encode(Dm.list(file, line)), WORKAROUND_IDENTIFIER), "code/__HELPERS/stack_trace.dm", 4);
        }
        catch (Exception e) { World.Current.ReportRuntime(e); } // DM: a runtime ends only this proc (rules 9.3)
    }

    // tg: code/__HELPERS/stack_trace.dm:6  #define STACK_DEPTH_SEARCH_LIMIT 2000 (file-local, #undef at :34): only the
    // default of dump_stack, which is not translated.

    /// Returns an ordered list of all our parent procs, highest to deepest.
    ///
    /// introspection like this is mostly useful for debugging, if you have another use I suspect you are just creating god's strongest footgun and should rethink things
    // tg: code/__HELPERS/stack_trace.dm:10
    public static object dump_stack(float? max_depth = null)
    {
        Dm.NotTranslated("code/__HELPERS/stack_trace.dm:10", "dump_stack: walks BYOND's /callee stack (caller, proc), which the port does not model (debugging only)");
        return default;
    }
}
