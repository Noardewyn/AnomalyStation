// Translated from tgstation (snapshot 1d99bd7f): code/datums/actions/items/set_internals.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M4c slice 3: the whole file (the gas tank's button; its gold border and the active background while the tank is the
// owner's internals). The var block is generated.

// tg: code/datums/actions/items/set_internals.dm:1
public partial class datum_action_item_action_set_internals : datum_action_item_action
{
    // tg: code/datums/actions/items/set_internals.dm:6
    public override object is_action_active(atom_movable_screen_movable_action_button current_button = null)
    {
        mob_living_carbon carbon_owner = owner as mob_living_carbon;
        return Dm.num(carbon_owner is mob_living_carbon && target == carbon_owner.@internal);
    }
}
