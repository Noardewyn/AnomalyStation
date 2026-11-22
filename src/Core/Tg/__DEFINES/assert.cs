// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/assert.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/__DEFINES/assert.dm:1 #undef ASSERT: tg's macro below replaces BYOND's ASSERT (rules 1.6).

public static partial class GlobalProcs
{
    /// Override BYOND's native ASSERT to optionally specify a message
    // tg: code/__DEFINES/assert.dm:4  #define ASSERT(condition, message...) if (!(condition)) { CRASH(assertion_message(__FILE__, __LINE__, #condition, ##message)) }
    // ASSERT has no member: it is expanded inline at each use (rules 11.1, lazy and inline macros), so that the message
    // is built only when the assertion fails, as in DM. ASSERT(cond, msg) at code/x.dm:123 is
    //     if (!(cond))
    //         throw Dm.CRASH(assertion_message("code/x.dm", 123, "<cond as written in DM>", msg), "code/x.dm", 123);
    // (without a message, assertion_message gets three arguments: ##message drops the comma).

    // tg: code/__DEFINES/assert.dm:9
    public static object assertion_message(object file = null, object line = null, object condition = null, object message = null)
    {
        if (message != null)
            message = Dm.text(" - []", message);

        return Dm.text("[]:[]:Assertion failed: [][]", file, line, condition, message);
    }
}
