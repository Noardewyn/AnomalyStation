// Translated from tgstation (snapshot 1d99bd7f): code/modules/mapping/space_management/space_level.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M3 P2: the whole file (the var block is generated, Generated/Types/datum.g.cs).

// tg: code/modules/mapping/space_management/space_level.dm:1
public partial class datum_space_level : datum
{
    // tg: code/modules/mapping/space_management/space_level.dm:10
    [DmParams("new_z", "new_name", "new_traits")]
    public override DmTask New(params object[] __extra)
    {
        object new_z = Dm.arg(__extra, 0);
        object new_name = Dm.arg(__extra, 1);
        object new_traits = __extra.Length > 2 ? Dm.arg(__extra, 2) : Dm.list();
        z_value = Dm.num(new_z);
        name = (string)new_name;
        traits = new_traits as DmList;

        if (islist(new_traits))
        {
            foreach (object trait in Dm.each(new_traits))
                Dm.append_at(SSmapping.z_trait_levels, trait, Dm.list(new_z));
        }
        else // in case a single trait is passed in
        {
            Dm.append_at(SSmapping.z_trait_levels, new_traits, Dm.list(new_z));
        }

        set_linkage(((DmList)new_traits)[ZTRAIT_LINKAGE]);
        return DmTask.CompletedTask;
    }
}
