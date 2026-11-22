// Translated from tgstation (snapshot 1d99bd7f): code/datums/keybinding/movement.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only the level keys' procs (zlevel_upwards and zlevel_downwards, :33-61; DV422 binds them to Space and Ctrl). The four direction
// bindings stay unported: a capsule mob moves by the bridge's frames (DV15), not by tg's keyLoop. The var blocks are generated.

// tg: code/datums/keybinding/movement.dm:40
public partial class datum_keybinding_movement_zlevel_upwards : datum_keybinding_movement
{
    // tg: code/datums/keybinding/movement.dm:40
    public override async DmTask<object> down(client user = null, turf target = null, object mousepos_x = null, object mousepos_y = null)
    {
        object dot = await base.down(user, target, mousepos_x, mousepos_y);
        if (Dm.truthy(dot))
            return dot;
        await user.mob.up();
        return TRUE;
    }
}

// tg: code/datums/keybinding/movement.dm:54
public partial class datum_keybinding_movement_zlevel_downwards : datum_keybinding_movement
{
    // tg: code/datums/keybinding/movement.dm:54
    public override async DmTask<object> down(client user = null, turf target = null, object mousepos_x = null, object mousepos_y = null)
    {
        object dot = await base.down(user, target, mousepos_x, mousepos_y);
        if (Dm.truthy(dot))
            return dot;
        await user.mob.down();
        return TRUE;
    }
}
