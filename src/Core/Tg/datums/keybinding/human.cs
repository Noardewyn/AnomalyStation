// Translated from tgstation (snapshot 1d99bd7f): code/datums/keybinding/human.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: /datum/keybinding/human's can_use and quick_equip (M4a P2); quick_equip_belt and its subtypes (the belt and
// bag bindings: smart_equip_targeted, M9) come with their mechanics. The var blocks are generated (types-converter.md 3.2).

// tg: code/datums/keybinding/human.dm:5
public partial class datum_keybinding_human : datum_keybinding
{
    // tg: code/datums/keybinding/human.dm:5
    public override object can_use(client user = null)
    {
        return Dm.num(ishuman(user.mob));
    }
}

// tg: code/datums/keybinding/human.dm:15
public partial class datum_keybinding_human_quick_equip : datum_keybinding_human
{
    // tg: code/datums/keybinding/human.dm:15
    public override async DmTask<object> down(client user = null, turf target = null, object mousepos_x = null, object mousepos_y = null)
    {
        object dot = null;
        dot = await base.down(user, target, mousepos_x, mousepos_y);
        if (Dm.truthy(dot))
            return dot;
        mob_living_carbon_human H = user.mob as mob_living_carbon_human;
        H.quick_equip();
        return TRUE;
    }
}
