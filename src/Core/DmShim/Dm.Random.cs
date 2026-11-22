using Anomaly.Core.Kernel;

namespace Anomaly.Core.DmShim;

// Owner: values. Randomness from the world RNG only (World.Current.Rng, reseeded every tick, D31;
// docs/ai/translation-rules.md 4.13). Arguments are evaluated before the draw, left to right, as in DM. prob() draws one
// value whatever P is (prob(0) and prob(100) draw too), so the stream of a tick does not depend on probability values.
public static partial class Dm
{
    /// <summary>DM <c>prob(P)</c> (BYOND ref <c>/proc/prob</c>): true with probability P percent (never for P &lt;= 0, always for P &gt;= 100).</summary>
    public static bool prob(float P) => World.Current.Rng.NextFloat() * 100f < P;

    /// <summary>
    /// DM <c>rand_seed(Seed)</c> (BYOND ref <c>/proc/rand_seed</c>): does nothing. DEVIATION(DV11): the world RNG is
    /// seeded by the host and reseeded every tick (D31, rules 4.13), so tg's <c>rand_seed(random_seed)</c> in
    /// <c>/datum/controller/master/New</c> (<c>controllers/master.dm:98</c>) cannot reseed it; the argument is evaluated by
    /// the caller as in DM.
    /// </summary>
    public static void rand_seed(object Seed = null)
    {
    }

    /// <summary>DM <c>rand()</c> (BYOND ref <c>/proc/rand</c>): a random number from 0 up to 1 (1 excluded).</summary>
    public static float rand() => World.Current.Rng.NextFloat();

    /// <summary>DM <c>rand(H)</c>: <c>rand(L=0, H)</c>, a random integer from 0 to H inclusive (OpenDream DreamProcNativeRoot.NativeProc_rand).</summary>
    public static float rand(float H) => rand(0f, H);

    /// <summary>DM <c>rand(L, H)</c>: a random integer between L and H inclusive (both truncated; their order does not matter).</summary>
    public static float rand(float L, float H) => World.Current.Rng.NextInt(RandBound(L), RandBound(H));

    /// <summary>
    /// DM <c>pick(Val1, Val2, ...)</c> and <c>pick(List)</c> (BYOND ref <c>/proc/pick</c>): one of the arguments, or,
    /// when the only argument is a list (or <c>Dm.arglist(L)</c>), one of its items, chosen uniformly. Picking from an
    /// empty list is a runtime error (OpenDream test <c>pick_list_empty</c>); a single non-list argument is returned
    /// without a draw.
    /// </summary>
    public static object pick(params object[] Vals)
    {
        Vals ??= OneNull();
        if (Vals.Length == 0) return null;
        if (Vals.Length == 1)
        {
            object only = Vals[0] is DmArgList arglist ? arglist.List : Vals[0];
            if (only is not DmList list) return only;
            if (list.Count == 0) throw new DmRuntimeException("pick() from an empty list");
            // By position, also in an alist (its items are its keys; its indexer takes a number as a key).
            return list._items[World.Current.Rng.NextInt(1, list.Count) - 1];
        }
        return Vals[World.Current.Rng.NextInt(0, Vals.Length - 1)];
    }

    /// <summary>
    /// DM weighted <c>pick(P1; Val1, P2; Val2, Val3)</c> (BYOND ref <c>/proc/pick</c>; rules 4.13): each pair is
    /// (weight, value), an unweighted value weighs 100 ("A value for P of 200 makes it twice as likely as the norm");
    /// one draw over the cumulative weights, as OpenDream's <c>PickWeighted</c> does. A weight of 0 or less is never
    /// chosen; when no weight is positive the result is null.
    /// </summary>
    public static object pick_weighted(params (float weight, object value)[] items)
    {
        float draw = World.Current.Rng.NextFloat();
        if (items == null || items.Length == 0) return null;
        float total = 0f;
        foreach ((float weight, object _) in items)
        {
            if (weight > 0f) total += weight;
        }
        if (!(total > 0f)) return null;
        float target = draw * total;
        float cumulative = 0f;
        foreach ((float weight, object value) in items)
        {
            if (!(weight > 0f)) continue;
            cumulative += weight;
            if (target < cumulative) return value;
        }
        for (int i = items.Length - 1; i >= 0; i--)
        {
            if (items[i].weight > 0f) return items[i].value;
        }
        return null;
    }

    /// <summary>
    /// DM <c>roll(dice)</c> (BYOND ref <c>/proc/roll</c>): dice text such as "3d6" or "3d4+5" (an offset, also
    /// negative: "2d6-1"; "d6" is one die) gives the sum of the rolled dice plus the offset; a number is the sides of one
    /// die; null gives 1 (OpenDream <c>NativeProc_roll</c>). Text that is not dice is a runtime error.
    /// </summary>
    public static float roll(object dice)
    {
        switch (dice)
        {
            case null:
                return 1f;
            case string text:
                return RollText(text);
            default:
                if (TryGetNumber(dice, out float sides)) return roll(1f, sides);
                throw new DmRuntimeException("roll(): invalid dice " + KindOf(dice));
        }
    }

    /// <summary>DM <c>roll(ndice, sides)</c>: the sum of <paramref name="ndice"/> dice with <paramref name="sides"/> sides, each numbered 1 to sides (truncated; fewer than one die or side gives 0).</summary>
    public static float roll(float ndice, float sides)
    {
        int n = RandBound(ndice);
        int s = RandBound(sides);
        if (n < 1 || s < 1) return 0f;
        float total = 0f;
        Rng rng = World.Current.Rng;
        for (int i = 0; i < n; i++) total += rng.NextInt(1, s);
        return total;
    }

    private static float RollText(string text)
    {
        string t = text.Trim();
        int d = t.IndexOfAny(new[] { 'd', 'D' });
        if (d < 0)
        {
            if (int.TryParse(t, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int onlySides)) return roll(1f, onlySides);
            throw new DmRuntimeException("roll(): invalid dice \"" + text + "\"");
        }
        string count = t.Substring(0, d);
        string rest = t.Substring(d + 1);
        int dice = 1;
        if (count.Length > 0 && !int.TryParse(count, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out dice))
            throw new DmRuntimeException("roll(): invalid dice \"" + text + "\"");
        int offset = 0;
        int sign = rest.IndexOfAny(new[] { '+', '-' });
        string sidesText = sign < 0 ? rest : rest.Substring(0, sign);
        if (sign >= 0 && !int.TryParse(rest.Substring(sign), System.Globalization.NumberStyles.AllowLeadingSign, System.Globalization.CultureInfo.InvariantCulture, out offset))
            throw new DmRuntimeException("roll(): invalid dice \"" + text + "\"");
        if (!int.TryParse(sidesText, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int sides))
            throw new DmRuntimeException("roll(): invalid dice \"" + text + "\"");
        return roll(dice, sides) + offset;
    }

    // A rand()/roll() bound: truncated to an integer, clamped to the int range (NaN is 0).
    private static int RandBound(float value)
    {
        if (float.IsNaN(value)) return 0;
        if (value >= int.MaxValue) return int.MaxValue;
        if (value <= int.MinValue) return int.MinValue;
        return (int)value;
    }
}
