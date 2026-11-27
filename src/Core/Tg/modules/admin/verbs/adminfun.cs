// Translated from tgstation (snapshot 1d99bd7f): code/modules/admin/verbs/adminfun.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only objectify, which the mossglob and the babbelfish use to swallow a mob; the admin verbs of the file come with the
// admin tools.

public static partial class GlobalProcs
{
    /// "Turns" people into objects. Really, we just add them to the contents of the item.
    // tg: code/modules/admin/verbs/adminfun.dm:276
    public static async DmTask objectify(atom_movable target = null, object path_or_instance = null)
    {
        atom tomb;
        if (ispath(path_or_instance))
            tomb = await Dm.new_async((Type)path_or_instance, get_turf(target)) as atom;
        else
            tomb = path_or_instance as atom;
        await target.forceMove(tomb);
        await target.AddComponent(typeof(datum_component_itembound), tomb);
    }
}
