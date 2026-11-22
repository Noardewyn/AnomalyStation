// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/color.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Only the carp colours (gaps package C4).

public partial class datum_controller_global_vars
{
    /// The possible colours of a space carp
    // tg: code/_globalvars/lists/color.dm:11
    public DmList carp_colors;
    public virtual void InitGlobalcarp_colors()
    {
        carp_colors = Dm.list(
            Dm.kv(COLOR_CARP_PURPLE, 7f),
            Dm.kv(COLOR_CARP_PINK, 7f),
            Dm.kv(COLOR_CARP_GREEN, 7f),
            Dm.kv(COLOR_CARP_GRAPE, 7f),
            Dm.kv(COLOR_CARP_SWAMP, 7f),
            Dm.kv(COLOR_CARP_TURQUOISE, 7f),
            Dm.kv(COLOR_CARP_BROWN, 7f),
            Dm.kv(COLOR_CARP_TEAL, 7f),
            Dm.kv(COLOR_CARP_LIGHT_BLUE, 7f),
            Dm.kv(COLOR_CARP_RUSTY, 7f),
            Dm.kv(COLOR_CARP_RED, 7f),
            Dm.kv(COLOR_CARP_YELLOW, 7f),
            Dm.kv(COLOR_CARP_BLUE, 7f),
            Dm.kv(COLOR_CARP_PALE_GREEN, 7f),
            Dm.kv(COLOR_CARP_SILVER, 1f)); // The rare silver carp
        Dm.append(ref gvars_datum_init_order, "carp_colors");
    }
}
