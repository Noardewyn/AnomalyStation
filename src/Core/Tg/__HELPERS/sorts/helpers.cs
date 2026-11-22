// Translated from tgstation (snapshot 1d99bd7f): code/__HELPERS/sorts/helpers.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// tg: code/__HELPERS/sorts/helpers.dm:15  #define CREATE_SORT_INSTANCE(to_sort, cmp, associative, fromIndex, toIndex)
// Helper for the sorting procs. Prevents some code duplication. Creates /datum/sort_instance/sorter
// It returns from the caller and declares `sorter` for the code after it (rules 11.1 (c)): expanded inline in
// sortTim, sortMerge and sortInsert, its only uses (#undef at :95).

public static partial class GlobalProcs
{
    // tg: code/__HELPERS/sorts/helpers.dm:2
    /// Sorts the list in place with timSort, default settings.
    // Note (tg): it calls timSort(1, 0) without the normalization of CREATE_SORT_INSTANCE, so the range is empty and
    // nothing is sorted; tg has no use of SORT_TIM at the snapshot.
    public static void SORT_TIM(object to_sort, float associative)
    {
        if (length(to_sort) >= 2)
        {
            datum_sort_instance sorter = GLOB.sortInstance;
            if (sorter == null)
                sorter = Dm.@new<datum_sort_instance>();
            sorter.L = to_sort as DmList;
            sorter.cmp = "/proc/" + nameof(GlobalProcs.cmp_numeric_asc);
            sorter.associative = associative;
            sorter.timSort(1, 0);
        }
    }

    /**
     * ## Tim Sort
     * Hybrid sorting algorithm derived from merge sort and insertion sort.
     *
     * **Sorts in place**.
     * You might not need to get the return value.
     *
     * @see
     * https://en.wikipedia.org/wiki/Timsort
     *
     * @param {list} to_sort - The list to sort.
     *
     * @param {proc} cmp - The comparison proc to use. Default: Numeric ascending.
     *
     * @param {boolean} associative - Whether the list is associative. Default: FALSE.
     *
     * @param {int} fromIndex - The index to start sorting from. Default: 1.
     *
     * @param {int} toIndex - The index to stop sorting at. Default: 0.
     */
    // tg: code/__HELPERS/sorts/helpers.dm:56  (`as /list`: a return type hint, dropped like RETURN_TYPE, rules 7.9)
    public static object sortTim(DmList to_sort = null, string cmp = null, float? associative = null, float? fromIndex = null, float? toIndex = null)
    {
        cmp ??= "/proc/" + nameof(GlobalProcs.cmp_numeric_asc);
        associative ??= FALSE;
        fromIndex ??= 1;
        toIndex ??= 0;
        // CREATE_SORT_INSTANCE(to_sort, cmp, associative, fromIndex, toIndex) (helpers.dm:15)
        if (length(to_sort) < 2)
            return to_sort;
        fromIndex = Dm.mod(Dm.num(fromIndex), length(to_sort));
        toIndex = Dm.mod(Dm.num(toIndex), length(to_sort) + 1);
        if (Dm.num(fromIndex) <= 0)
            Dm.append(ref fromIndex, length(to_sort));
        if (Dm.num(toIndex) <= 0)
            Dm.append(ref toIndex, length(to_sort) + 1);
        datum_sort_instance sorter = GLOB.sortInstance;
        if (sorter == null)
            sorter = Dm.@new<datum_sort_instance>();
        sorter.L = to_sort;
        sorter.cmp = cmp;
        sorter.associative = Dm.num(associative);

        sorter.timSort(fromIndex, toIndex);

        return to_sort;
    }

    /**
     * ## Merge Sort
     * Divide and conquer sorting algorithm.
     *
     * @see
     * - https://en.wikipedia.org/wiki/Merge_sort
     */
    // tg: code/__HELPERS/sorts/helpers.dm:71
    public static object sortMerge(DmList to_sort = null, string cmp = null, float? associative = null, float? fromIndex = null, float? toIndex = null)
    {
        cmp ??= "/proc/" + nameof(GlobalProcs.cmp_numeric_asc);
        associative ??= FALSE;
        fromIndex ??= 1;
        toIndex ??= 0;
        // CREATE_SORT_INSTANCE(to_sort, cmp, associative, fromIndex, toIndex) (helpers.dm:15)
        if (length(to_sort) < 2)
            return to_sort;
        fromIndex = Dm.mod(Dm.num(fromIndex), length(to_sort));
        toIndex = Dm.mod(Dm.num(toIndex), length(to_sort) + 1);
        if (Dm.num(fromIndex) <= 0)
            Dm.append(ref fromIndex, length(to_sort));
        if (Dm.num(toIndex) <= 0)
            Dm.append(ref toIndex, length(to_sort) + 1);
        datum_sort_instance sorter = GLOB.sortInstance;
        if (sorter == null)
            sorter = Dm.@new<datum_sort_instance>();
        sorter.L = to_sort;
        sorter.cmp = cmp;
        sorter.associative = Dm.num(associative);

        sorter.mergeSort(fromIndex, toIndex);

        return to_sort;
    }

    /**
     * ## Insertion Sort
     * Simple sorting algorithm that builds the final sorted list one item at a time.
     *

     * @see
     * - https://en.wikipedia.org/wiki/Insertion_sort
     */
    // tg: code/__HELPERS/sorts/helpers.dm:87
    public static object sortInsert(DmList to_sort = null, string cmp = null, float? associative = null, float? fromIndex = null, float? toIndex = null)
    {
        cmp ??= "/proc/" + nameof(GlobalProcs.cmp_numeric_asc);
        associative ??= FALSE;
        fromIndex ??= 1;
        toIndex ??= 0;
        // CREATE_SORT_INSTANCE(to_sort, cmp, associative, fromIndex, toIndex) (helpers.dm:15)
        if (length(to_sort) < 2)
            return to_sort;
        fromIndex = Dm.mod(Dm.num(fromIndex), length(to_sort));
        toIndex = Dm.mod(Dm.num(toIndex), length(to_sort) + 1);
        if (Dm.num(fromIndex) <= 0)
            Dm.append(ref fromIndex, length(to_sort));
        if (Dm.num(toIndex) <= 0)
            Dm.append(ref toIndex, length(to_sort) + 1);
        datum_sort_instance sorter = GLOB.sortInstance;
        if (sorter == null)
            sorter = Dm.@new<datum_sort_instance>();
        sorter.L = to_sort;
        sorter.cmp = cmp;
        sorter.associative = Dm.num(associative);

        sorter.binarySort(fromIndex, toIndex);

        return to_sort;
    }
}
