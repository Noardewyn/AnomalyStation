// Translated from tgstation (snapshot 1d99bd7f): code/game/turfs/open/ashplanet.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The ash planet's ash and wet rock (Lavaland's crash site). The var blocks are generated.

// tg: code/game/turfs/open/ashplanet.dm:1
public partial class turf_open_misc_ashplanet : turf_open_misc
{
    // tg: code/game/turfs/open/ashplanet.dm:21
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(Dm.bitand(smoothing_flags, SMOOTH_BITMASK)))
        {
            var M = Dm.@new<matrix>();
            M.Translate(-4, -4);
            transform = M;
            icon = smooth_icon;
            icon_state = Dm.text("[]-[]", icon_state, smoothing_junction);
        }
        return dot;
    }

    // tg: code/game/turfs/open/ashplanet.dm:30
    public override object break_tile() => null;

    // tg: code/game/turfs/open/ashplanet.dm:33
    public override object burn_tile() => null;
}

// tg: code/game/turfs/open/ashplanet.dm:58
public partial class turf_open_misc_ashplanet_wateryrock : turf_open_misc_ashplanet
{
    // tg: code/game/turfs/open/ashplanet.dm:69
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        icon_state = Dm.text("[][]", icon_state, Dm.rand(1, 9));
        return base.Initialize(mapload, __extra);
    }
}
