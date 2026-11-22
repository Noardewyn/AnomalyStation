// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/stack_trace.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/stack_trace.dm:2  #define stack_trace(message) _stack_trace(message, __FILE__, __LINE__)
    /// gives us the stack trace from CRASH() without ending the current proc.
    // Rules 9.5 (v1.2): called with the DM location of the use, stack_trace(msg, "code/x.dm", 123), and the reported
    // location is the caller's. In tg, _stack_trace's CRASH (__HELPERS/stack_trace.dm:4) carries the use site inside its
    // message, json_encode(list(file, line)) between two WORKAROUND_IDENTIFIERs, and /world/Error moves it back into the
    // runtime's file and line and strips it from the message:
    // tg: code/modules/error_handler/error_handler.dm:62-66  if(stack_workaround.Find(E.name)) { E.file = data[1]; E.line = data[2]; E.name = stack_workaround.Replace(E.name, "") }
    // The member does that rewrite itself (world/Error is not translated): it reports the runtime CRASH("[message]") at
    // the use site, and returns, so the caller carries on as in DM, where the CRASH ends only _stack_trace. The runtime is
    // never thrown, so it is given the caller's stack here (tg's runtime log prints the proc stack of a CRASH).
    [StackTraceHidden]
    public static void stack_trace(object message, string file, int line)
        => World.Current.ReportRuntime(ExceptionDispatchInfo.SetCurrentStackTrace(Dm.CRASH(message, file, line)));
}
