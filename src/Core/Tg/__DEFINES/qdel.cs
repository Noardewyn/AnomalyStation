// Translated from tgstation (snapshot 1d99bd7f): code/__DEFINES/qdel.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The constants of this file (QDEL_HINT_*, GC_QUEUE_*, GC_*_QUEUE, QDEL_ITEM_*, GC_CURRENTLY_BEING_QDELETED) are
// generated into Defines (src/Core/Tg/Generated/Defines/__DEFINES/qdel.g.cs); its function-like macros follow.

public static partial class GlobalProcs
{
    // tg: code/__DEFINES/qdel.dm:53
    // DEVIATION(DV2): tg tests the truthiness of gc_destroyed; the port tests it against null, so a datum qdel'd at
    // world time 0 (gc_destroyed 0) counts as deleted (translation-rules.md 10.5).
    public static bool QDELING(object X) => ((datum)X).gc_destroyed != null;

    // tg: code/__DEFINES/qdel.dm:54
    public static bool QDELETED(object X) => X == null || QDELING(X);

    // tg: code/__DEFINES/qdel.dm:55
    public static bool QDESTROYING(object X) => !Dm.truthy(X) || Dm.eq(((datum)X).gc_destroyed, GC_CURRENTLY_BEING_QDELETED);

    // This is a bit hacky, we do it to avoid people relying on a return value for the macro
    // If you need that you should use QDEL_IN_STOPPABLE instead
    // tg: code/__DEFINES/qdel.dm:59
    // QDEL_IN(item, time), QDEL_IN_STOPPABLE(item, time) (:61), QDEL_IN_CLIENT_TIME(item, time) (:62) and
    // QDEL_LIST_IN(L, time) (:65) expand addtimer, whose __FILE__ and __LINE__ belong to the use site, so they are
    // expanded inline at every use (rules 11.1 (f)), for example QDEL_IN(item, time) as
    //     _addtimer(Dm.@new<datum_callback>(GLOBAL_PROC, "/proc/" + nameof(GlobalProcs.qdel), time > GC_FILTER_QUEUE ? WEAKREF(item) : item), time, file: "code/x.dm", line: 123);
    // with item and time bound to temps unless they are plain reads (rules 4.12).

    // tg: code/__DEFINES/qdel.dm:63
    public static void QDEL_NULL<T>(ref T item) where T : class
    {
        qdel(item);
        item = null;
    }

    // tg: code/__DEFINES/qdel.dm:64
    public static void QDEL_LIST(object L)
    {
        if (Dm.truthy(L))
        {
            foreach (object I in Dm.each(L))
                qdel(I);
            ((DmList)L).Cut();
        }
    }

    // tg: code/__DEFINES/qdel.dm:66
    public static void QDEL_LIST_ASSOC(object L)
    {
        if (Dm.truthy(L))
        {
            foreach (object I in Dm.each(L))
            {
                qdel(((DmList)L)[I]);
                qdel(I);
            }
            ((DmList)L).Cut();
        }
    }

    // tg: code/__DEFINES/qdel.dm:67
    public static void QDEL_LIST_ASSOC_VAL(object L)
    {
        if (Dm.truthy(L))
        {
            foreach (object I in Dm.each(L))
                qdel(((DmList)L)[I]);
            ((DmList)L).Cut();
        }
    }
}
