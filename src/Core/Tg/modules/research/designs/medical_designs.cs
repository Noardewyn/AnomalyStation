// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/designs/medical_designs.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Silicons SI3 (D326): this file's design procs (SSresearch makes every design); the designs are var blocks (generated, D111).

// tg: code/modules/research/designs/medical_designs.dm:1194
public partial class datum_design_surgery : datum_design
{
    // tg: code/modules/research/designs/medical_designs.dm:1203
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (name == null)
        {
            object rnd_name = Dm.initial(surgery, (datum_surgery_operation s) => s.rnd_name);
            name = Dm.str(Dm.truthy(rnd_name) ? rnd_name : capitalize(Dm.initial(surgery, (datum_surgery_operation s) => s.name)));
        }
        if (desc == null)
        {
            object rnd_desc = Dm.initial(surgery, (datum_surgery_operation s) => s.rnd_desc);
            desc = Dm.str(Dm.truthy(rnd_desc) ? rnd_desc : Dm.initial(surgery, (datum_surgery_operation s) => s.desc));
        }
    }
}
