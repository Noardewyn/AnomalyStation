namespace Anomaly.Core.DmShim;

// Owner: the M2 contracts step (complete; docs/ai/specs/m2-ownership.md, docs/ai/specs/dm-conformance.md "M2 contracts").

public static partial class Dm
{
    // BYOND 516's arithmetic over the associated values of a list or alist (BYOND ref "values_sum proc" and its
    // siblings, "convenience procs for games trying to eke out high performance"). tg's gas mixtures keep their moles
    // in an alist and use them (code/modules/atmospherics/gasmixtures/gas_mixture.dm: values_sum :104, values_dot :86,
    // values_cut_under :79; M2 P2), the bodyparts values_sum (_bodyparts.dm:337).
    //
    // Order and precision: the items are visited in list order (an alist keeps insertion order, rules 6.1), each item
    // once (a key that occurs twice in a plain list counts twice, as `for(var/k, v in L)` visits it), and the running
    // total is a float, added left to right, so the result depends only on the list (D31). A value counts when it is
    // a number (TryGetNumber: a boxed int is the float of the same value, rules 3.1); text, objects, lists and null
    // do not.

    /// <summary>
    /// DM <c>values_sum(Alist)</c>: the sum of the associated values of <paramref name="Alist"/> that are numbers
    /// ("Other values, including vectors, are not summed"); 0 when it is not an associative list (or not a list).
    /// </summary>
    public static float values_sum(object Alist)
    {
        if (Alist is not DmList list || list._assoc == null) return 0f;
        float total = 0f;
        object[] items = list._items;
        for (int i = 0; i < list._count; i++)
        {
            if (list.TryGetAssociated(items[i], out object value) && TryGetNumber(value, out float n))
                total += n;
        }
        return total;
    }

    /// <summary>
    /// DM <c>values_product(Alist)</c>: the product of the associated values that are numbers, or 1 when there is none
    /// (or <paramref name="Alist"/> is not an associative list).
    /// </summary>
    public static float values_product(object Alist)
    {
        if (Alist is not DmList list || list._assoc == null) return 1f;
        float product = 1f;
        object[] items = list._items;
        for (int i = 0; i < list._count; i++)
        {
            if (list.TryGetAssociated(items[i], out object value) && TryGetNumber(value, out float n))
                product *= n;
        }
        return product;
    }

    /// <summary>
    /// DM <c>values_dot(A, B)</c>: the dot product of two associative lists: for every item of <paramref name="A"/>
    /// whose associated value is a number and that <paramref name="B"/> also associates with a number, the two values
    /// multiplied, all of it added up (in the order of A). An item missing from either side counts as 0; a non-list
    /// gives 0.
    /// </summary>
    public static float values_dot(object A, object B)
    {
        if (A is not DmList a || B is not DmList b || a._assoc == null || b._assoc == null) return 0f;
        float total = 0f;
        object[] items = a._items;
        for (int i = 0; i < a._count; i++)
        {
            object key = items[i];
            if (a.TryGetAssociated(key, out object va) && TryGetNumber(va, out float na)
                && b.TryGetAssociated(key, out object vb) && TryGetNumber(vb, out float nb))
                total += na * nb;
        }
        return total;
    }

    /// <summary>
    /// DM <c>values_cut_under(Alist, Min, inclusive=0)</c>: removes every item whose associated value is less than
    /// <paramref name="Min"/>, not a number, or NaN (and, with <paramref name="inclusive"/> true, equal to Min); returns
    /// how many were removed. Nothing happens to a list that is not associative (it has no associated values at all);
    /// in one that is, an item without a value is "not a number" and goes too.
    /// </summary>
    public static float values_cut_under(object Alist, object Min, object inclusive = null)
    {
        float min = num(Min);
        bool equalToo = truthy(inclusive);
        return CutValues(Alist, v => v < min || (equalToo && v == min));
    }

    /// <summary>
    /// DM <c>values_cut_over(Alist, Max, inclusive=0)</c>: removes every item whose associated value is greater than
    /// <paramref name="Max"/>, not a number, or NaN (and, with <paramref name="inclusive"/> true, equal to Max); returns
    /// how many were removed. Nothing happens to a list that is not associative.
    /// </summary>
    public static float values_cut_over(object Alist, object Max, object inclusive = null)
    {
        float max = num(Max);
        bool equalToo = truthy(inclusive);
        return CutValues(Alist, v => v > max || (equalToo && v == max));
    }

    // Removes, from the end, every item whose value is not a number, NaN, or matches; the kept items keep their order and
    // their values. A key that occurs twice in a plain list shares one value, so both occurrences go together.
    private static float CutValues(object Alist, Func<float, bool> cut)
    {
        if (Alist is not DmList list || list._assoc == null) return 0f;
        int removed = 0;
        for (int i = list._count - 1; i >= 0; i--)
        {
            object key = list._items[i];
            bool keep = list.TryGetAssociated(key, out object value) && TryGetNumber(value, out float n) && !float.IsNaN(n) && !cut(n);
            if (keep) continue;
            list.Cut(i + 1, i + 2);
            removed++;
            if (i > list._count) i = list._count;
        }
        return removed;
    }
}
