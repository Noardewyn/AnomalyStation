// Translated from tgstation (snapshot 1d99bd7f): code/datums/mutations/adaptation.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): The temperature, pressure and thermal adaptations. The var blocks are generated (D111).

// tg: code/datums/mutations/adaptation.dm:1
public partial class datum_mutation_adaptation : datum_mutation
{
    // tg: code/datums/mutations/adaptation.dm:13
    [DmParams("copymut")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        conflicts = typesof(typeof(datum_mutation_adaptation)) as DmList;
    }
}
