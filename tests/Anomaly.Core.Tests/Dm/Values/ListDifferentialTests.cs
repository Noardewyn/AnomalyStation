using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tests.DmValues;

/// <summary>
/// A randomized differential test (fixed seeds) of every <see cref="DmList"/> mutation path against a naive reference
/// model of DM list semantics. The model is written from the BYOND reference (<c>/list</c>, "list associations", the
/// list procs, "list operators", <c>/proc/for/list</c>, <c>/proc/alist</c>) and the pinned conformance rows
/// (docs/ai/specs/dm-conformance.md), never from the port: items in order, DM equality (numbers by value whatever their
/// box, NaN equal to nothing, text ordinal, everything else by reference), and an association per key that lives
/// while the key occurs. After every step it compares the items, every key's value, membership, <c>Find</c>, the
/// associations kept and the occurrence index; loop snapshots (<c>Dm.each</c>, <c>Dm.pairs</c>) started between steps
/// must keep the items they started with.
/// </summary>
public class ListDifferentialTests
{
    [Fact]
    public void EveryListMutationPathMatchesTheReferenceModel()
    {
        Run(alist: false, seed: 0x5EED1234u, steps: 6000);
        Run(alist: false, seed: 0x00C0FFEEu, steps: 3000);
    }

    [Fact]
    public void EveryAlistMutationPathMatchesTheReferenceModel()
    {
        Run(alist: true, seed: 0x0A11570Fu, steps: 6000);
        Run(alist: true, seed: 0x00BADA55u, steps: 3000);
    }

    // ---- the reference model ----

    private static bool Num(object x, out float f)
    {
        switch (x)
        {
            case float v: f = v; return true;
            case int i: f = i; return true;
            case double d: f = (float)d; return true;
            default: f = 0f; return false;
        }
    }

    // DM equality (BYOND ref "==", rules 4.2), independent of Dm.eq.
    private static bool Same(object a, object b)
    {
        if (a is null || b is null) return a is null && b is null;
        if (a is string sa) return b is string sb && string.Equals(sa, sb, StringComparison.Ordinal);
        if (Num(a, out float x)) return Num(b, out float y) && x == y;
        return ReferenceEquals(a, b);
    }

    // The same stored item: DM equality, except that a NaN item is the NaN that was stored (positions, loops).
    private static bool SameItem(object a, object b)
        => Num(a, out float x) && Num(b, out float y) && float.IsNaN(x) && float.IsNaN(y) || Same(a, b);

    private static string Show(object x) => x switch
    {
        null => "null",
        string s => "\"" + s + "\"",
        DmList => "a list",
        Type t => t.Name,
        float or int or double => x.GetType().Name + " " + x,
        _ => "object#" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(x),
    };

    private sealed class Model
    {
        public readonly bool Alist;
        public readonly List<object> Items = new();
        public readonly List<(object Key, object Value)> Assoc = new();

        public Model(bool alist) => Alist = alist;

        public Model Clone(bool? alist = null)
        {
            var m = new Model(alist ?? Alist);
            m.Items.AddRange(Items);
            m.Assoc.AddRange(Assoc);
            return m;
        }

        public int IndexOf(object x) => Items.FindIndex(i => Same(i, x));

        public bool Has(object x) => IndexOf(x) >= 0;

        public int Occurrences(object x) => Items.Count(i => Same(i, x));

        public bool TryAssoc(object key, out object value)
        {
            int at = Assoc.FindIndex(e => Same(e.Key, key));
            value = at >= 0 ? Assoc[at].Value : null;
            return at >= 0;
        }

        public object Read(object key) => TryAssoc(key, out object v) ? v : null;

        public void SetAssoc(object key, object value)
        {
            int at = Assoc.FindIndex(e => Same(e.Key, key));
            if (at >= 0) Assoc[at] = (Assoc[at].Key, value);
            else Assoc.Add((key, value));
        }

        // An association lives while its key occurs (BYOND ref "list associations"; conformance rows).
        public void Prune() => Assoc.RemoveAll(e => !Has(e.Key));

        public (object Key, bool Has, object Value)[] Entries()
            => Items.Select(i => (i, TryAssoc(i, out object v), v)).ToArray();

        // In an alist a number is stored as a number key (BYOND ref /proc/alist).
        private void Put(object x) => Items.Add(Alist && Num(x, out float f) ? f : x);

        private void PutAt(int position, object x) => Items.Insert(position, Alist && Num(x, out float f) ? f : x);

        // One item of Add / +=: an alist adds only keys it does not have yet.
        public void AddOne(object x)
        {
            if (Alist && Has(x)) return;
            Put(x);
        }

        // A list's items with their values (Add, +=, +): BYOND ref "list associations", OpenDream AssocListAdd.
        public void AddFrom((object Key, bool Has, object Value)[] entries)
        {
            foreach ((object k, bool has, object v) in entries)
            {
                if (Alist && Has(k)) continue;
                Put(k);
                if (has) SetAssoc(k, v);
            }
        }

        public bool RemoveLast(object x)
        {
            int at = Items.FindLastIndex(i => Same(i, x));
            if (at < 0) return false;
            Items.RemoveAt(at);
            Prune();
            return true;
        }

        public int RemoveEvery(object x)
        {
            int n = Items.RemoveAll(i => Same(i, x));
            Prune();
            return n;
        }

        public void Combine((object Key, bool Has, object Value)[] entries)
        {
            foreach ((object k, bool has, object v) in entries)
            {
                if (Has(k)) continue;
                Put(k);
                if (has) SetAssoc(k, v);
            }
        }

        public void CombineOne(object x)
        {
            if (!Has(x)) Put(x);
        }

        // &: keep min(count) copies in this list's order (OpenDream list_set_pair_preservation).
        public void Mask(object[] other)
        {
            var remaining = new List<object>(other);
            var kept = new List<object>();
            foreach (object i in Items)
            {
                int at = remaining.FindIndex(r => Same(r, i));
                if (at < 0) continue;
                remaining.RemoveAt(at);
                kept.Add(i);
            }
            Items.Clear();
            Items.AddRange(kept);
            Prune();
        }

        public void MaskOne(object x)
        {
            int first = IndexOf(x);
            object keep = first >= 0 ? Items[first] : null;
            Items.Clear();
            if (first >= 0) Items.Add(keep);
            Prune();
        }

        // A ^ B for a list B: the items only in A (A's values), then the items only in B (B's values).
        public Model Xor((object Key, bool Has, object Value)[] other)
        {
            var otherModel = new Model(false);
            foreach ((object k, bool has, object v) in other)
            {
                otherModel.Items.Add(k);
                if (has) otherModel.SetAssoc(k, v);
            }
            var result = new Model(Alist);
            foreach ((object k, bool has, object v) in Entries())
            {
                if (otherModel.Has(k)) continue;
                result.Put(k);
                if (has) result.SetAssoc(k, v);
            }
            foreach ((object k, bool has, object v) in other)
            {
                if (Has(k)) continue;
                if (result.Alist && result.Has(k)) continue;
                result.Put(k);
                if (has) result.SetAssoc(k, v);
            }
            return result;
        }

        // A ^ x for a non-list x: A without x, plus x when A had none (OpenDream BitXorValues).
        public Model XorOne(object x)
        {
            var result = new Model(Alist);
            foreach ((object k, bool has, object v) in Entries())
            {
                if (Same(k, x)) continue;
                result.Put(k);
                if (has) result.SetAssoc(k, v);
            }
            if (!Has(x)) result.Put(x);
            return result;
        }

        // A ^= x for a non-list x toggles one occurrence: the first copy goes, or x is appended.
        public void XorAssignOne(object x)
        {
            int first = IndexOf(x);
            if (first >= 0)
            {
                Items.RemoveAt(first);
                Prune();
            }
            else
            {
                Put(x);
            }
        }

        public void ReplaceWith(Model other)
        {
            Items.Clear();
            Assoc.Clear();
            Items.AddRange(other.Items);
            Assoc.AddRange(other.Assoc);
        }

        // Insert's and Splice's items: list arguments give their items, carrying their values.
        public int InsertFlattened(int position, List<(object Item, bool Has, object Value, bool Carried)> flat)
        {
            for (int i = 0; i < flat.Count; i++) PutAt(position + i, flat[i].Item);
            foreach ((object item, bool has, object value, bool carried) in flat)
            {
                if (carried && has) SetAssoc(item, value);
            }
            return flat.Count;
        }
    }

    // An operand of a list proc or operator: a value, or a list whose entries are read when the operation reads it.
    private sealed class Operand
    {
        public object Actual;
        public Func<(object Key, bool Has, object Value)[]> Entries; // null for a non-list
    }

    private sealed class TestRng
    {
        private ulong _state;

        public TestRng(ulong seed) => _state = seed * 0x9E3779B97F4A7C15UL + 1;

        public int Next(int n)
        {
            _state ^= _state << 13;
            _state ^= _state >> 7;
            _state ^= _state << 17;
            return (int)(_state % (ulong)n);
        }

        public int Range(int lo, int hi) => lo + Next(hi - lo + 1);
    }

    private sealed class OpenLoop
    {
        public IEnumerator<object> Enumerator;
        public object[] Expected;
        public int Position;
    }

    private static void Run(bool alist, uint seed, int steps)
    {
        var rng = new TestRng(seed);
        var o1 = new object();
        var o2 = new object();
        DmList fixedList = Dm.list("a", 3f);   // a list used as an item and a key; never changed
        var fixedModel = new Model(false);
        fixedModel.Items.Add("a");
        fixedModel.Items.Add(3f);

        // NaN equals nothing; an alist with NaN keys has no defined meaning, so alists do not get NaN.
        object[] values = alist
            ? new object[] { "a", "b", "c", "D", "", null, 1f, 2, 2.0, 3f, 0f, -0f, o1, o2, typeof(string), fixedList }
            : new object[] { "a", "b", "c", "D", "", null, 1f, 2, 2.0, 3f, 0f, -0f, float.NaN, double.NaN, o1, o2, typeof(string), fixedList };
        object[] scalars = values.Where(v => v is not DmList).ToArray();
        object[] listKeys = values.Where(v => !Num(v, out _)).ToArray();          // keys a non-alist accepts
        object[] alistKeys = values.Where(v => !(Num(v, out float f) && float.IsNaN(f))).ToArray();
        object[] keys = alist ? alistKeys : listKeys;
        object[] assocValues = { null, 0f, 5f, "v", o2 };

        DmList L = alist ? Dm.alist() : Dm.list();
        var model = new Model(alist);
        var loops = new List<OpenLoop>();
        IEnumerator<(object Key, object Value)> pendingPairs = null;
        (object Key, object Value)[] pendingPairsExpected = null;

        object Pick(object[] from) => from[rng.Next(from.Length)];

        (DmList List, Model Model) RandomList()
        {
            bool asAlist = rng.Next(4) == 0;
            int n = rng.Next(5);
            var args = new object[n];
            var m = new Model(asAlist);
            object[] kinds = asAlist ? alistKeys : listKeys;
            for (int i = 0; i < n; i++)
            {
                if (rng.Next(3) == 0)
                {
                    object k = Pick(kinds);
                    object v = Pick(assocValues);
                    args[i] = Dm.kv(k, v);
                    if (!m.Has(k)) m.Items.Add(asAlist && Num(k, out float f) ? f : k);
                    m.SetAssoc(k, v);
                }
                else
                {
                    object x = Pick(asAlist ? alistKeys : values);
                    args[i] = x;
                    if (asAlist)
                    {
                        // alist(x): a plain item is a key with a null value (Dm.alist).
                        if (!m.Has(x))
                        {
                            m.Items.Add(Num(x, out float f) ? f : x);
                            m.SetAssoc(x, null);
                        }
                    }
                    else
                    {
                        m.Items.Add(x);   // list(L2) nests L2
                    }
                }
            }
            return (asAlist ? Dm.alist(args) : Dm.list(args), m);
        }

        Operand MakeOperand(bool allowScalar = true)
        {
            int kind = rng.Next(allowScalar ? 8 : 5);
            if (kind == 0) return new Operand { Actual = L, Entries = () => model.Entries() };
            if (kind == 1) return new Operand { Actual = fixedList, Entries = () => fixedModel.Entries() };
            if (kind <= 4)
            {
                (DmList list, Model m) = RandomList();
                return new Operand { Actual = list, Entries = () => m.Entries() };
            }
            object x = Pick(scalars);
            return new Operand { Actual = x, Entries = null };
        }

        Operand[] MakeOperands(int min, int max)
        {
            int n = rng.Range(min, max);
            var result = new Operand[n];
            for (int i = 0; i < n; i++) result[i] = MakeOperand();
            return result;
        }

        List<(object, bool, object, bool)> Flatten(Operand[] args)
        {
            var flat = new List<(object, bool, object, bool)>();
            foreach (Operand a in args)
            {
                if (a.Entries == null) flat.Add((a.Actual, false, null, false));
                else foreach ((object k, bool has, object v) in a.Entries()) flat.Add((k, has, v, true));
            }
            return flat;
        }

        void ExpectError(Action act, string what, int step)
        {
            Exception error = Record.Exception(act);
            Assert.True(error is DmRuntimeException, what + " at step " + step + " should be a DM runtime error, got " + (error?.GetType().Name ?? "no error"));
        }

        void CheckList(DmList actual, Model expected, string what, int step)
        {
            Assert.True(expected.Items.Count == actual.Count, what + ": length " + actual.Count + ", expected " + expected.Items.Count + " at step " + step);
            object[] items = actual.CopyItems();
            for (int i = 0; i < items.Length; i++)
                Assert.True(SameItem(expected.Items[i], items[i]), what + ": item " + (i + 1) + " is " + Show(items[i]) + ", expected " + Show(expected.Items[i]) + " at step " + step);
            foreach (object k in expected.Alist ? alistKeys : listKeys)
            {
                Assert.True(Same(expected.Read(k), actual[k]), what + ": " + Show(k) + " reads " + Show(actual[k]) + ", expected " + Show(expected.Read(k)) + " at step " + step);
            }
            foreach (object k in values)
            {
                Assert.True(expected.Has(k) == Dm.@in(k, actual), what + ": membership of " + Show(k) + " at step " + step);
                Assert.True(expected.IndexOf(k) + 1 == actual.Find(k), what + ": Find(" + Show(k) + ") at step " + step);
            }
            if (actual._assoc != null)
            {
                // No association outlives its key (the null key is stored under a private sentinel object).
                foreach (object key in actual._assoc.Keys)
                {
                    bool known = values.Any(v => Same(v, key));
                    Assert.True(known ? expected.Has(key) : expected.Has(null), what + ": an association of " + (known ? Show(key) : "null") + " outlived its key at step " + step);
                }
            }
            if (!expected.Alist)
            {
                expected.Prune();
                Assert.True(expected.Assoc.Count == (actual._assoc?.Count ?? 0), what + ": " + (actual._assoc?.Count ?? 0) + " associations kept, expected " + expected.Assoc.Count + " at step " + step);
            }
            if (actual._index != null)
            {
                int counted = expected.Items.Count(i => !(Num(i, out float f) && float.IsNaN(f)));
                Assert.True(counted == actual._index.Values.Sum(), what + ": the occurrence index counts " + actual._index.Values.Sum() + " items, expected " + counted + " at step " + step);
                foreach (object k in values)
                {
                    if (k == null || (Num(k, out float f) && float.IsNaN(f))) continue;
                    actual._index.TryGetValue(k, out int c);
                    Assert.True(expected.Occurrences(k) == c, what + ": the index counts " + c + " of " + Show(k) + ", expected " + expected.Occurrences(k) + " at step " + step);
                }
            }
        }

        for (int step = 0; step < steps; step++)
        {
            if (step % 400 == 399)
            {
                // A fresh list now and then, so small lists (scanned without the occurrence index) get their share.
                L = alist ? Dm.alist() : Dm.list();
                model = new Model(alist);
            }
            int n = model.Items.Count;
            int op = n > 24 ? (rng.Next(2) == 0 ? 2 : 3) : rng.Next(23);
            string what = "op " + op;
            switch (op)
            {
                case 0: // L.Add(...)
                {
                    Operand[] args = MakeOperands(1, 3);
                    foreach (Operand a in args)
                    {
                        if (a.Entries == null) model.AddOne(a.Actual);
                        else model.AddFrom(a.Entries());
                    }
                    L.Add(args.Select(a => a.Actual).ToArray());
                    break;
                }
                case 1: // L.Remove(...)
                {
                    Operand[] args = MakeOperands(1, 3);
                    bool removed = false;
                    foreach (Operand a in args)
                    {
                        if (a.Entries == null) removed |= model.RemoveLast(a.Actual);
                        else foreach ((object k, bool _, object _) in a.Entries()) removed |= model.RemoveLast(k);
                    }
                    Assert.Equal(removed ? 1f : 0f, L.Remove(args.Select(a => a.Actual).ToArray()));
                    break;
                }
                case 2: // L.RemoveAll(...)
                {
                    Operand[] args = MakeOperands(1, 2);
                    int count = 0;
                    foreach (Operand a in args)
                    {
                        if (a.Entries == null) count += model.RemoveEvery(a.Actual);
                        else foreach ((object k, bool _, object _) in a.Entries()) count += model.RemoveEvery(k);
                    }
                    Assert.Equal((float)count, L.RemoveAll(args.Select(a => a.Actual).ToArray()));
                    break;
                }
                case 3: // L.Cut(Start, End): Start 0 is 1, End 0 or past the end is the end, a negative Start is an error
                {
                    int a = rng.Range(-1, n + 2), b = rng.Range(-1, n + 3);
                    if (a < 0)
                    {
                        ExpectError(() => L.Cut(a, b), "Cut(" + a + ", " + b + ")", step);
                        break;
                    }
                    int s = a == 0 ? 1 : a;
                    int e = b == 0 || b > n + 1 ? n + 1 : b;
                    if (e > s && s <= n)
                    {
                        model.Items.RemoveRange(s - 1, e - s);
                        model.Prune();
                    }
                    L.Cut(a, b);
                    break;
                }
                case 4: // L.Copy(Start, End): a new list of Start..End-1 with their values
                {
                    int a = rng.Range(0, n + 2), b = rng.Range(0, n + 2);
                    int s = a == 0 ? 1 : a;
                    bool bad = s > n + 1 || b > n + 1;
                    int e = b == 0 ? n + 1 : b;
                    if (!bad && alist && (s != 1 || e != n + 1)) bad = true;
                    if (bad)
                    {
                        ExpectError(() => L.Copy(a, b), "Copy(" + a + ", " + b + ")", step);
                        break;
                    }
                    var expected = new Model(alist);
                    for (int i = s; i < e; i++)
                    {
                        object k = model.Items[i - 1];
                        expected.Items.Add(k);
                        if (model.TryAssoc(k, out object v)) expected.SetAssoc(k, v);
                    }
                    DmList copy = L.Copy(a, b);
                    Assert.NotSame(L, copy);
                    CheckList(copy, expected, "Copy(" + a + ", " + b + ")", step);
                    break;
                }
                case 5: // L.Find(Elem, Start, End)
                {
                    object x = Pick(values);
                    int a = rng.Range(1, n + 2), b = rng.Range(-1, n + 2);
                    int e = b == 0 || b > n ? n + 1 : b;
                    int found = 0;
                    for (int i = a; i < e; i++)
                    {
                        if (Same(model.Items[i - 1], x))
                        {
                            found = i;
                            break;
                        }
                    }
                    Assert.True(found == L.Find(x, a, b), "Find(" + Show(x) + ", " + a + ", " + b + ") at step " + step);
                    break;
                }
                case 6: // L.Insert(Index, ...): 0 or negative appends, past len+1 is an error; returns the index after the items
                {
                    int at = rng.Range(-1, n + 2);
                    Operand[] args = MakeOperands(0, 2);
                    object[] actualArgs = args.Select(x => x.Actual).ToArray();
                    if (alist || args.Length == 0 || at > n + 1)
                    {
                        ExpectError(() => L.Insert(at, actualArgs), "Insert(" + at + ")", step);
                        break;
                    }
                    int index = at <= 0 ? n + 1 : at;
                    int count = model.InsertFlattened(index - 1, Flatten(args));
                    Assert.Equal((float)(index + count), L.Insert(at, actualArgs));
                    break;
                }
                case 7: // L.Swap(i, j): both in 1..len; values stay with their keys
                {
                    int i = rng.Range(0, n + 1), j = rng.Range(0, n + 1);
                    if (alist || i < 1 || i > n || j < 1 || j > n)
                    {
                        ExpectError(() => L.Swap(i, j), "Swap(" + i + ", " + j + ")", step);
                        break;
                    }
                    (model.Items[i - 1], model.Items[j - 1]) = (model.Items[j - 1], model.Items[i - 1]);
                    L.Swap(i, j);
                    break;
                }
                case 8: // L[i] = x by position (truncated; outside 1..len is an error); an alist takes the number as a key
                {
                    if (alist) goto case 9;
                    float i = rng.Next(6) switch
                    {
                        0 => float.NaN,
                        1 => rng.Range(1, Math.Max(n, 1)) + 0.5f,
                        _ => rng.Range(0, n + 1),
                    };
                    object x = Pick(values);
                    bool numberBox = rng.Next(2) == 0;
                    if (float.IsNaN(i) || i < 1f || i >= n + 1f)
                    {
                        ExpectError(() => { if (numberBox) L[(object)i] = x; else L[i] = x; }, "L[" + i + "] =", step);
                        break;
                    }
                    model.Items[(int)i - 1] = x;
                    model.Prune();
                    if (numberBox) L[(object)i] = x;
                    else L[i] = x;
                    break;
                }
                case 9: // L[key] = value: a missing key is appended
                {
                    object k = Pick(keys);
                    object v = Pick(assocValues);
                    if (!model.Has(k)) model.Items.Add(alist && Num(k, out float f) ? f : k);
                    model.SetAssoc(k, v);
                    L[k] = v;
                    break;
                }
                case 10: // L.len = n: pads with nulls or drops the last items; an alist only clears (or keeps its length)
                {
                    float len = rng.Next(8) == 0 ? -1f : rng.Next(5) == 0 ? rng.Range(0, n + 2) + 0.5f : rng.Range(0, n + 3);
                    int t = (int)len;
                    if (len < 0f || (alist && t != 0 && t != n))
                    {
                        ExpectError(() => L.len = len, "len = " + len, step);
                        break;
                    }
                    if (t < n) model.Items.RemoveRange(t, n - t);
                    while (model.Items.Count < t) model.Items.Add(null);
                    model.Prune();
                    L.len = len;
                    break;
                }
                case 11: // L.Splice(Start, End, ...): negatives count from the end, clamped, swapped; Cut then Insert
                {
                    int a = rng.Range(-(n + 2), n + 3), b = rng.Range(-(n + 2), n + 3);
                    Operand[] args = MakeOperands(0, 2);
                    object[] actualArgs = args.Select(x => x.Actual).ToArray();
                    if (alist)
                    {
                        ExpectError(() => L.Splice(a, b, actualArgs), "Splice", step);
                        break;
                    }
                    int s = a < 0 ? a + n + 1 : a;
                    int e = b <= 0 ? b + n + 1 : b;
                    s = Math.Clamp(s, 1, n + 1);
                    e = Math.Clamp(e, 1, n + 1);
                    if (e < s) (s, e) = (e, s);
                    model.Items.RemoveRange(s - 1, e - s);
                    model.Prune();
                    model.InsertFlattened(s - 1, Flatten(args));
                    L.Splice(a, b, actualArgs);
                    what = "Splice(" + a + ", " + b + ")";
                    break;
                }
                case 12: // L += B (every overload)
                {
                    Operand B = MakeOperand();
                    if (B.Entries == null) model.AddOne(B.Actual);
                    else model.AddFrom(B.Entries());
                    switch (rng.Next(3))
                    {
                        case 0: { object o = L; Dm.append(ref o, B.Actual); Assert.Same(L, o); break; }
                        case 1: { DmList r = L; Dm.append(ref r, B.Actual); Assert.Same(L, r); break; }
                        default: Dm.append(L, B.Actual); break;
                    }
                    break;
                }
                case 13: // L -= B: the last occurrence of B or of each of B's items, B read first
                {
                    Operand B = MakeOperand();
                    if (B.Entries == null) model.RemoveLast(B.Actual);
                    else foreach ((object k, bool _, object _) in B.Entries()) model.RemoveLast(k);
                    switch (rng.Next(3))
                    {
                        case 0: { object o = L; Dm.remove(ref o, B.Actual); Assert.Same(L, o); break; }
                        case 1: { DmList r = L; Dm.remove(ref r, B.Actual); Assert.Same(L, r); break; }
                        default: Dm.remove(L, B.Actual); break;
                    }
                    break;
                }
                case 14: // L |= B
                {
                    Operand B = MakeOperand();
                    if (B.Entries == null) model.CombineOne(B.Actual);
                    else model.Combine(B.Entries());
                    switch (rng.Next(3))
                    {
                        case 0: { object o = L; Dm.combine(ref o, B.Actual); Assert.Same(L, o); break; }
                        case 1: { DmList r = L; Dm.combine(ref r, B.Actual); Assert.Same(L, r); break; }
                        default: Dm.combine(L, B.Actual); break;
                    }
                    break;
                }
                case 15: // L &= B
                {
                    Operand B = MakeOperand();
                    if (B.Entries == null) model.MaskOne(B.Actual);
                    else model.Mask(B.Entries().Select(x => x.Key).ToArray());
                    switch (rng.Next(3))
                    {
                        case 0: { object o = L; Dm.mask(ref o, B.Actual); Assert.Same(L, o); break; }
                        case 1: { DmList r = L; Dm.mask(ref r, B.Actual); Assert.Same(L, r); break; }
                        default: Dm.mask(L, B.Actual); break;
                    }
                    break;
                }
                case 16: // L ^= B
                {
                    Operand B = MakeOperand();
                    if (B.Entries == null) model.XorAssignOne(B.Actual);
                    else model.ReplaceWith(model.Xor(B.Entries()));
                    switch (rng.Next(3))
                    {
                        case 0: { object o = L; Dm.bitxor_assign(ref o, B.Actual); Assert.Same(L, o); break; }
                        case 1: { DmList r = L; Dm.bitxor_assign(ref r, B.Actual); Assert.Same(L, r); break; }
                        default: Dm.bitxor_assign(L, B.Actual); break;
                    }
                    break;
                }
                case 17: // L + B, L - B, L | B, L & B, L ^ B: new lists; L itself is unchanged
                case 18:
                case 19:
                case 20:
                case 21:
                {
                    Operand B = MakeOperand();
                    Model expected = model.Clone();
                    object result;
                    switch (op)
                    {
                        case 17:
                            if (B.Entries == null) expected.AddOne(B.Actual);
                            else expected.AddFrom(B.Entries());
                            result = Dm.add(L, B.Actual);
                            what = "L + B";
                            break;
                        case 18:
                            if (B.Entries == null) expected.RemoveLast(B.Actual);
                            else foreach ((object k, bool _, object _) in B.Entries()) expected.RemoveLast(k);
                            result = Dm.sub(L, B.Actual);
                            what = "L - B";
                            break;
                        case 19:
                            if (B.Entries == null) expected.CombineOne(B.Actual);
                            else expected.Combine(B.Entries());
                            result = Dm.bitor(L, B.Actual);
                            what = "L | B";
                            break;
                        case 20:
                            if (B.Entries == null) expected.MaskOne(B.Actual);
                            else expected.Mask(B.Entries().Select(x => x.Key).ToArray());
                            result = Dm.bitand(L, B.Actual);
                            what = "L & B";
                            break;
                        default:
                            expected = B.Entries == null ? model.XorOne(B.Actual) : model.Xor(B.Entries());
                            result = Dm.bitxor(L, B.Actual);
                            what = "L ^ B";
                            break;
                    }
                    DmList list = Assert.IsType<DmList>(result);
                    Assert.NotSame(L, list);
                    CheckList(list, expected, what, step);
                    break;
                }
                default: // a loop starts, or one of the running loops (and a pending pairs loop) takes its next items
                {
                    if (rng.Next(2) == 0 && loops.Count < 3)
                    {
                        loops.Add(new OpenLoop { Enumerator = ((IEnumerable<object>)Dm.each(L)).GetEnumerator(), Expected = model.Items.ToArray() });
                    }
                    if (pendingPairs == null && rng.Next(3) == 0)
                    {
                        pendingPairs = Dm.pairs(L).GetEnumerator();
                        pendingPairsExpected = model.Items.Select(i => (i, model.Read(i))).ToArray();
                    }
                    break;
                }
            }

            CheckList(L, model, what, step);

            // Running loops see the items they started with, whatever happened since (BYOND ref /proc/for/list).
            for (int k = loops.Count - 1; k >= 0; k--)
            {
                OpenLoop loop = loops[k];
                if (rng.Next(3) != 0) continue;
                if (rng.Next(10) == 0)
                {
                    loop.Enumerator.Dispose();   // a loop left by break
                    loops.RemoveAt(k);
                    continue;
                }
                for (int take = rng.Range(1, 2); take > 0; take--)
                {
                    bool more = loop.Enumerator.MoveNext();
                    if (loop.Position == loop.Expected.Length)
                    {
                        Assert.False(more, "a loop ran past its snapshot at step " + step);
                        loop.Enumerator.Dispose();
                        loops.RemoveAt(k);
                        break;
                    }
                    Assert.True(more, "a loop ended early at step " + step);
                    Assert.True(SameItem(loop.Expected[loop.Position], loop.Enumerator.Current), "a loop saw " + Show(loop.Enumerator.Current) + " instead of " + Show(loop.Expected[loop.Position]) + " at step " + step);
                    loop.Position++;
                }
            }
            if (pendingPairs != null && rng.Next(4) == 0)
            {
                var seen = new List<(object Key, object Value)>();
                while (pendingPairs.MoveNext()) seen.Add(pendingPairs.Current);
                pendingPairs.Dispose();
                Assert.Equal(pendingPairsExpected.Length, seen.Count);
                for (int i = 0; i < seen.Count; i++)
                {
                    Assert.True(SameItem(pendingPairsExpected[i].Key, seen[i].Key) && Same(pendingPairsExpected[i].Value, seen[i].Value), "pairs entry " + (i + 1) + " at step " + step);
                }
                pendingPairs = null;
            }
        }
        foreach (OpenLoop loop in loops) loop.Enumerator.Dispose();
    }
}
