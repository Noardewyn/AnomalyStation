// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/chat.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the EXAMINE_HINT macro (M7 slice 1: a power machine's examine; rules 11.1 and 11.4, a global #define of a
// file outside __DEFINES); gaps H1 moved the earlier packages' private copies to it.

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/chat.dm:83  #define EXAMINE_HINT(text) ("<b>" + text + "</b>")
    public static string EXAMINE_HINT(object text) => (string)Dm.add(Dm.add("<b>", text), "</b>");
}
