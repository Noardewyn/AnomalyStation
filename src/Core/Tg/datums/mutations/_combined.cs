// Translated from tgstation (snapshot 1d99bd7f): code/datums/mutations/_combined.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): the DNA console's mutation combining; the recipes are the generated var blocks (D111).

// tg: code/datums/mutations/_combined.dm:6 (global procs; interleaved types, rules 1.9)
public static partial class GlobalProcs
{
    // tg: code/datums/mutations/_combined.dm:6
    public static object get_mixed_mutation(object mutation1 = null, object mutation2 = null)
    {
        if (!Dm.truthy(mutation1) || !Dm.truthy(mutation2))
            return FALSE;
        if (Dm.eq(mutation1, mutation2)) //this could otherwise be bad
            return FALSE;
        foreach (object GR in Dm.each(subtypesof(typeof(datum_generecipe))))
        {
            Type input_one = Dm.initial(GR, (datum_generecipe g) => g.input_one);
            Type input_two = Dm.initial(GR, (datum_generecipe g) => g.input_two);
            if ((Dm.eq(input_one, mutation1) && Dm.eq(input_two, mutation2)) || (Dm.eq(input_one, mutation2) && Dm.eq(input_two, mutation1)))
                return Dm.initial(GR, (datum_generecipe g) => g.result);
        }
        return null;
    }
}
