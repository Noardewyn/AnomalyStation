// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/designs/autolathe/service_designs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI3 (D326): this file's design procs (SSresearch makes every design); the designs are var blocks (generated, D111).

// tg: code/modules/research/designs/autolathe/service_designs.dm:578
public partial class datum_design_telescreen_monastery : datum_design
{
    // tg: code/modules/research/designs/autolathe/service_designs.dm:592
    public override async DmTask New(params object[] __extra)
    {
        // CHECK_MAP_JOB_CHANGE(job, change) (__DEFINES/maps.dm:243) is SSmapping.current_map.job_changes?[job]?[change]
        object has_monastery = (SSmapping?.current_map?.job_changes?[JOB_CHAPLAIN] as DmList)?["has_monastery"];
        if (!Dm.truthy(has_monastery))
            qdel(this);
        await base.New(__extra);
    }
}
