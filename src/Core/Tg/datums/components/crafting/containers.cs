// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/crafting/containers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5 slice 8 (crafting): the file's only proc. The recipes themselves are var blocks (generated, D111).

// tg: code/datums/components/crafting/containers.dm:24
public partial class datum_crafting_recipe_underwater_basket : datum_crafting_recipe
{
    // tg: code/datums/components/crafting/containers.dm:37
    public override object check_requirements(mob user = null, DmList collected_requirements = null)
    {
        object dot = base.check_requirements(user, collected_requirements);
        turf T = get_turf(user);
        if (istype(T, typeof(turf_open_water)))
            return TRUE;
        obj_machinery_shower S = Dm.locate<obj_machinery_shower>(T);
        if (Dm.truthy(S?.actually_on))
            return TRUE;
        return dot;
    }
}
