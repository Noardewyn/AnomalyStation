// Translated from tgstation (snapshot 1d99bd7f): code/datums/ruins.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The vars but cost are generated.

// tg: code/datums/ruins.dm:1
[DmPath("/datum/map_template/ruin")]
public partial class datum_map_template_ruin : datum_map_template
{
    // tg: code/datums/ruins.dm:16
    public float cost = 0;

    // tg: code/datums/ruins.dm:36
    public override async DmTask New(params object[] __extra)
    {
        if (!Dm.truthy(name) && Dm.truthy(id))
            name = Dm.str(id);

        mappath = Dm.add(prefix, suffix);
        await base.New(mappath);
    }
}
