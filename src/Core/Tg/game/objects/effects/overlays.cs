// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/effects/overlays.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: holds the [DmPath] of a type that the wave-4 var blocks and signatures name (rules 2.3: the earliest .dme file that declares it), written by the W4.0 contracts; only the type is declared, and the file is translated with its module.

// tg: code/game/objects/effects/overlays.dm:1
[DmPath("/obj/effect/overlay")]
public partial class obj_effect_overlay : obj_effect
{
}

// tg: code/game/objects/effects/overlays.dm:37
[DmPath("/obj/effect/overlay/vis")]
public partial class obj_effect_overlay_vis : obj_effect_overlay
{
}

// tg: code/game/objects/effects/overlays.dm:4 (interleaved types, rules 1.9; M5x slice 5)
public partial class obj_effect_overlay : obj_effect
{
    // tg: code/game/objects/effects/overlays.dm:4
    public override async DmTask<object> singularity_act()
    {
        return null;
    }

    // tg: code/game/objects/effects/overlays.dm:7
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        return null;
    }
}
