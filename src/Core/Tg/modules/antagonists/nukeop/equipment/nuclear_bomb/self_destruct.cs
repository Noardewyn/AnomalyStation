// Translated from tgstation (snapshot 1d99bd7f): code/modules/antagonists/nukeop/equipment/nuclear_bomb/self_destruct.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (syndicate N1a, D316: Meta's self-destruct terminal in the vault). The var block is generated (D111).

// tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/self_destruct.dm:1
public partial class obj_machinery_nuclearbomb_selfdestruct : obj_machinery_nuclearbomb
{
    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/self_destruct.dm:8
    public override DmTask set_anchor(mob anchorer = null)
    {
        return DmTask.CompletedTask;
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/self_destruct.dm:11
    public override void toggle_nuke_safety()
    {
        base.toggle_nuke_safety();
        if (Dm.truthy(timing))
            SSmapping.add_nuke_threat(this);
        else
            SSmapping.remove_nuke_threat(this);
    }

    // tg: code/modules/antagonists/nukeop/equipment/nuclear_bomb/self_destruct.dm:18
    public override void toggle_nuke_armed()
    {
        base.toggle_nuke_armed();
        if (Dm.truthy(timing))
            SSmapping.add_nuke_threat(this);
        else
            SSmapping.remove_nuke_threat(this);
    }
}
