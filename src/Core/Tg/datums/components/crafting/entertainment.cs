// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/crafting/entertainment.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 8 (crafting): the file's only proc. The recipes themselves are var blocks (generated, D111).

// tg: code/datums/components/crafting/entertainment.dm:182
public partial class datum_crafting_recipe_toiletbong : datum_crafting_recipe
{
    // tg: code/datums/components/crafting/entertainment.dm:195
    public override object check_requirements(mob user = null, DmList collected_requirements = null)
    {
        obj_item_flamethrower flamethrower = (collected_requirements[typeof(obj_item_flamethrower)] as DmList)[1] as obj_item_flamethrower;
        if (!Dm.truthy(flamethrower.ptank))
            return FALSE;
        return base.check_requirements(user, collected_requirements);
    }
}
