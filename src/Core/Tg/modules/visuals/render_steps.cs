// Translated from tgstation (snapshot 1d99bd7f): code/modules/visuals/render_steps.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// SKELETON: holds the [DmPath] of a type that the wave-4 var blocks and signatures name (rules 2.3: the earliest .dme file that declares it), written by the W4.0 contracts; only the type is declared, and the file is translated with its module.

// tg: code/modules/visuals/render_steps.dm:5
[DmPath("/atom/movable/render_step")]
public partial class atom_movable_render_step : atom_movable
{
}

// tg: code/modules/visuals/render_steps.dm:71
[DmPath("/atom/movable/render_step/emissive_blocker")]
public partial class atom_movable_render_step_emissive_blocker : atom_movable_render_step
{
}

// tg: code/modules/visuals/render_steps.dm:29 (interleaved types, rules 1.9; M5x slice 5)
public partial class atom_movable_render_step : atom_movable
{
    // tg: code/modules/visuals/render_steps.dm:29
    public override async DmTask<object> singularity_act()
    {
        return null;
    }

    // tg: code/modules/visuals/render_steps.dm:32
    public override async DmTask<object> singularity_pull(atom singularity = null, object current_size = null)
    {
        return null;
    }
}
