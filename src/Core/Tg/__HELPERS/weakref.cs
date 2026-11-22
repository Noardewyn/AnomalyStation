// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/weakref.dm
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
    /// Checks if potential_weakref is a weakref of thing.
    // tg: code/__HELPERS/weakref.dm:2
    public static bool IS_WEAKREF_OF(object thing, object potential_weakref) => isdatum(thing) && potential_weakref != null && Dm.eq(((datum)thing).weak_reference, potential_weakref);

    //For these two procs refs MUST be ref = TRUE format like typecaches!
    // tg: code/__HELPERS/weakref.dm:5
    public static object weakref_filter_list(DmList things = null, DmList refs = null)
    {
        DmList dot = null;
        if (!islist(things) || !islist(refs))
            return dot;
        if (!Dm.truthy(refs.len))
            return things;
        if (things.len > refs.len)
        {
            DmList f = Dm.list();
            foreach (object i in Dm.each(refs))
            {
                datum_weakref r = i as datum_weakref;
                datum d = r.resolve() as datum;
                if (Dm.truthy(d))
                    Dm.combine(ref f, d);
            }
            return Dm.bitand(things, f);
        }

        else
        {
            dot = Dm.list();
            foreach (object i in Dm.each(things))
            {
                if (!Dm.truthy(refs[WEAKREF(i as datum)]))
                    continue;
                Dm.combine(ref dot, i);
            }
        }
        return dot;
    }

    // tg: code/__HELPERS/weakref.dm:26
    public static object weakref_filter_list_reverse(DmList things = null, DmList refs = null)
    {
        DmList dot = null;
        if (!islist(things) || !islist(refs))
            return dot;
        if (!Dm.truthy(refs.len))
            return things;
        if (things.len > refs.len)
        {
            DmList f = Dm.list();
            foreach (object i in Dm.each(refs))
            {
                datum_weakref r = i as datum_weakref;
                datum d = r.resolve() as datum;
                if (Dm.truthy(d))
                    Dm.combine(ref f, d);
            }

            return Dm.sub(things, f);
        }
        else
        {
            dot = Dm.list();
            foreach (object i in Dm.each(things))
            {
                if (Dm.truthy(refs[WEAKREF(i as datum)]))
                    continue;
                Dm.combine(ref dot, i);
            }
        }
        return dot;
    }
}
