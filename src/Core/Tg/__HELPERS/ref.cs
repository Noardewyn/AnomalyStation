// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/ref.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

public static partial class GlobalProcs
{
    /**
     * \ref behaviour got changed in 512 so this is necesary to replicate old behaviour.
     * If it ever becomes necesary to get a more performant REF(), this lies here in wait
     * #define REF(thing) (thing && isdatum(thing) && (thing:datum_flags & DF_USE_TAG) && thing:tag ? "[thing:tag]" : text_ref(thing))
    **/
    // tg: code/__HELPERS/ref.dm:6
    public static object REF(object input = null)
    {
        if (isdatum(input))
        {
            datum thing = input as datum;
            if (Dm.truthy(Dm.bitand(thing.datum_flags, DF_USE_TAG)))
            {
                if (!Dm.truthy(thing.tag))
                {
                    stack_trace(Dm.text("A ref was requested of an object with DF_USE_TAG set but no tag: []", thing), "code/__HELPERS/ref.dm", 11);
                    Dm.mask(ref thing.datum_flags, Dm.bitnot(DF_USE_TAG));
                }
                else
                    return Dm.text("\\[[]\\]", url_encode(thing.tag));
            }
        }
        return text_ref(input);
    }
}
