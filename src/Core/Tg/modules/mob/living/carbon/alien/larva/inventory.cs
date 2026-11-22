// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/alien/larva/inventory.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps X5 (D333): the whole file.

// tg: code/modules/mob/living/carbon/alien/larva/inventory.dm:2
public partial class mob_living_carbon_alien_larva : mob_living_carbon_alien
{
    //can't unequip since it can't equip anything
    // tg: code/modules/mob/living/carbon/alien/larva/inventory.dm:2
    public override async DmTask<object> doUnEquip(obj_item item_dropping = null, object force = null, atom newloc = null, object no_move = null, float? invdrop = null, float? silent = null)
    {
        invdrop ??= TRUE;
        silent ??= FALSE;
        return null;
    }
}
