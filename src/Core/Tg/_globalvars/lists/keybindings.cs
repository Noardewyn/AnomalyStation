// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/keybindings.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only init_keybindings, as a deferred leaf (rules 1.4): make_datum_reference_lists calls it at world start (boot
// package, wave 3); the keybinding datums and their lists are client input preferences, which nothing in M0 reads (M9).

public static partial class GlobalProcs
{
    /// Creates and sorts all the keybinding datums
    // tg: code/_globalvars/lists/keybindings.dm:2
    public static void init_keybindings()
    {
        Dm.Deferred("code/_globalvars/lists/keybindings.dm:2", "init_keybindings: the keybinding datums (client input preferences, M10+)");
    }
}
