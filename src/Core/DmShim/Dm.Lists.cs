using Anomaly.Core.Kernel;

namespace Anomaly.Core.DmShim;

// Owner: values. Contracts wrote list construction (list, alist, kv, list_n) and the loops each, each<T>,
// each_cast<T> (the enumeration core); values completed and hardened the rest. Rules sections 4.7, 6.1, 6.5, 6.7.
public static partial class Dm
{
    /// <summary>
    /// DM <c>list(A, B, C, ...)</c> and <c>list(A=a, B=b, ...)</c> (BYOND ref <c>/proc/list</c>; rules 6.1): items in
    /// order, a list argument becomes a nested list (not flattened), <c>Dm.kv(key, value)</c> items set
    /// <c>L[key] = value</c> (a repeated key keeps one item and the last value).
    /// </summary>
    public static DmList list(params object[] items)
    {
        items ??= OneNull();
        var result = new DmList(items.Length, alist: false);
        foreach (object item in items)
        {
            if (item is DmKeyValue kv) result[kv.Key] = kv.Value;
            else result.AppendItem(item);
        }
        return result;
    }

    /// <summary>
    /// DM <c>alist(A=a, B=b, ...)</c> (BYOND 516, BYOND ref <c>/proc/alist</c>; rules 6.1): a <see cref="DmList"/> in
    /// alist mode, where a number used as a key is a key, not an index, and keys are unique. The port keeps
    /// insertion order. A plain item is a key with a null value.
    /// </summary>
    public static DmList alist(params object[] items)
    {
        items ??= OneNull();
        var result = new DmList(items.Length, alist: true);
        foreach (object item in items)
        {
            if (item is DmKeyValue kv) result[kv.Key] = kv.Value;
            else if (!result.Contains(item)) result[item] = null;
        }
        return result;
    }

    /// <summary>An entry <c>key = value</c> of <c>list(...)</c> or <c>alist(...)</c> (rules 6.1).</summary>
    public static DmKeyValue kv(object key, object value) => new DmKeyValue(key, value);

    /// <summary>
    /// DM <c>new /list(n)</c> and <c>var/L[n]</c> (rules 6.1): a list of <paramref name="n"/> nulls (truncated); a
    /// negative or NaN size gives an empty list, as BYOND does (OpenDream test <c>DimensionalListSizes</c>:
    /// <c>new /list(-1)</c> and <c>var/L[-0.5]</c> are empty, <c>[1.5]</c> has one item).
    /// </summary>
    public static DmList list_n(float n)
    {
        var result = new DmList();
        if (n >= 1f) result.len = n;
        return result;
    }

    /// <summary>
    /// DM <c>A in L</c> (rules 4.7; BYOND ref <c>in</c>): false when L is null or not a list; for an atom it tests
    /// <c>L.contents</c>; for an associative list it tests the keys (the items); <c>A in world</c> tests whether A is
    /// an atom registered in the ambient world (<c>world.contents</c>). O(1) on large lists (the occurrence index).
    /// </summary>
    public static bool @in(object A, object L)
    {
        switch (L)
        {
            case DmList list:
                return list.Contains(A);
            case IDmAtom atom:
                return atom.contents != null && atom.contents.Contains(A);
            case DmWorld:
                return A is IDmAtom member && member.dm_id != 0
                    && World.Current.Objects.TryGet(member.dm_id, out object found) && ReferenceEquals(found, member);
            default:
                return false;
        }
    }

    /// <summary>
    /// DM <c>A in X to Y</c> outside a <c>for</c> (rules 4.7, 6.7): X &lt;= A &lt;= Y, inclusive; false for a
    /// non-number A (null included). Named <c>in_to</c> because tg #defines <c>in_range</c> (__DEFINES/is_helpers.dm:3).
    /// </summary>
    public static bool in_to(object A, float X, float Y) => TryGetNumber(A, out float a) && in_to(a, X, Y);

    /// <summary>DM <c>A in X to Y</c> for a number A.</summary>
    public static bool in_to(float A, float X, float Y) => X <= A && A <= Y;

    /// <summary>
    /// DM <c>for (var/x in L)</c> (rules 6.5): every item of a snapshot of L, nulls included (see
    /// <see cref="DmEach{T}"/> for null, atom and world sources).
    /// </summary>
    public static DmEach<object> each(object L) => new DmEach<object>(L, DmEachMode.All);

    /// <summary>
    /// DM <c>for (var/obj/item/I in L)</c> (rules 6.5): the items that are a <typeparamref name="T"/>, skipping nulls
    /// and other types (the hidden <c>istype</c> of a typed loop var, BYOND ref <c>/proc/for/list</c>).
    /// </summary>
    public static DmEach<T> each<T>(object L) where T : class => new DmEach<T>(L, DmEachMode.Filter);

    /// <summary>
    /// DM <c>for (var/datum/D)</c>, a typed loop without <c>in</c> (rules 6.5): the live registered objects of the world
    /// that are a <typeparamref name="T"/>, in id order (live, not a copy). For an atom loop type these are its atoms,
    /// the same as <c>Dm.each&lt;T&gt;(world)</c>; for <c>/datum</c> or a non-atom loop type they are the non-atom datums
    /// (BYOND keeps them apart from <c>world.contents</c>: tg <c>debug.dm:1039</c> counts datums with it after counting
    /// atoms with <c>in world</c>, and <c>SDQL_2.dm:664</c> uses it "to have it not return atoms"). An unreachable datum
    /// is found until the C# GC collects it (object-model.md section 6).
    /// </summary>
    public static DmEach<T> each<T>() where T : class => new DmEach<T>(DmWorldLoop.Registry, DmEachMode.Filter);

    /// <summary>
    /// DM <c>for (var/obj/item/I as anything in L)</c> (rules 6.5): every item, nulls included, checked as a
    /// <typeparamref name="T"/>; a wrongly typed item is a runtime error at the loop head (DM fails later, at the
    /// first member access).
    /// </summary>
    public static DmEach<T> each_cast<T>(object L) where T : class => new DmEach<T>(L, DmEachMode.Cast);

    /// <summary>
    /// DM <c>for (var/x as mob|obj in L)</c> (rules 6.5): the items of the given kinds (BYOND ref <c>/proc/for/list</c>:
    /// "Type can be ... area, turf, mob, obj ORed together"; the input types <c>text</c>, <c>num</c>, <c>null</c>,
    /// <c>sound</c>, <c>icon</c> and <c>file</c> filter the same way). <c>DmAs.anything</c> keeps every item. The
    /// kind of an object is the first DM path on its class chain (<c>[DmPath]</c>), so <c>/mob/living</c> is a mob.
    /// </summary>
    public static DmEach<object> each_as(object L, DmAs types) => new DmEach<object>(L, DmEachMode.As, types);

    /// <summary>
    /// DM <c>for (var/k, v in L)</c> (BYOND 516; rules 6.5): each item (key) with its associated value (null when it
    /// has none), both copied when the loop starts. An atom iterates its contents; null and non-lists iterate nothing.
    /// </summary>
    public static DmPairs<object> pairs(object L) => new DmPairs<object>(L, typed: false);

    /// <summary>
    /// DM <c>for (var/mob/M, v in L)</c> (rules 6.5): the keys that are a <typeparamref name="T"/> (nulls and other
    /// types skipped, as a typed loop var does), with their values.
    /// </summary>
    public static DmPairs<T> pairs<T>(object L) where T : class => new DmPairs<T>(L, typed: true);
}
