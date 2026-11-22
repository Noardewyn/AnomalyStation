// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/keybinding.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only DEACTIVATE_KEYBIND translated (M2 P1: /datum/keybinding/proc/up); the constants of this file are generated
// (Defines).

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/keybinding.dm:9  #define DEACTIVATE_KEYBIND(A) "[A]_DEACTIVATED"
    public static string DEACTIVATE_KEYBIND(object A) => Dm.text("[]_DEACTIVATED", A);
}
