// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/designs/wiremod/_core_designs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI3 (D326): this file's design procs (SSresearch makes every design); the designs are var blocks (generated, D111).

// tg: code/modules/research/designs/wiremod/_core_designs.dm:35
public partial class datum_design_component : datum_design
{
    // tg: code/modules/research/designs/wiremod/_core_designs.dm:45
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (build_path != null)
        {
            Type component_path = build_path;
            desc = Dm.str(Dm.initial(component_path, (obj_item_circuit_component c) => c.desc));
        }
    }
}
