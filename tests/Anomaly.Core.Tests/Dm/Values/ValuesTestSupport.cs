using Anomaly.Core.DmShim;
using Anomaly.Core.Tests.Contracts;

// The namespace must not contain a segment named Dm: it would shadow the class Dm (the wave-0 bug).
namespace Anomaly.Core.Tests.DmValues;

/// <summary>Assertions shared by the values conformance tests (docs/ai/specs/dm-conformance.md, "Values").</summary>
internal static class V
{
    /// <summary>The items of <paramref name="L"/>, compared with DM equality, in order.</summary>
    public static void Items(DmList L, params object[] expected)
    {
        Assert.NotNull(L);
        var actual = new object[L.Count];
        for (int i = 0; i < L.Count; i++) actual[i] = L[i + 1];
        Assert.True(expected.Length == actual.Length && expected.Zip(actual).All(p => Dm.eq(p.First, p.Second)),
            "expected (" + Show(expected) + ") but got (" + Show(actual) + ")");
    }

    /// <summary>A DM runtime error.</summary>
    public static DmRuntimeException Error(Action action) => Assert.ThrowsAny<DmRuntimeException>(action);

    /// <summary>A DM runtime error from a value-returning call.</summary>
    public static DmRuntimeException Error(Func<object> action) => Assert.ThrowsAny<DmRuntimeException>(() => action());

    /// <summary>DM equality of a result (a boxed number or text) with the expected value.</summary>
    public static void Eq(object expected, object actual)
        => Assert.True(Dm.eq(expected, actual), "expected " + Show(new[] { expected }) + " but got " + Show(new[] { actual }));

    private static string Show(object[] values) => string.Join(", ", values.Select(v => v switch
    {
        null => "null",
        string s => "\"" + s + "\"",
        DmList l => "list(" + l.Count + ")",
        _ => v.ToString(),
    }));
}

/// <summary>A datum with DM's name and gender vars (a /datum subtype that declares them, as tg datums do).</summary>
internal sealed class NamedDatum : TestDatum
{
    public string name;
    public string gender;

    public NamedDatum(string name, string gender = null)
    {
        this.name = name;
        this.gender = gender;
    }
}

/// <summary>A datum with a DM path and no name var: renders as its type path.</summary>
[DmPath("/datum/values_test_plain")]
internal sealed class PlainDatum : TestDatum
{
}

/// <summary>A datum with an <c>operator""</c> overload (rules 7.10; tg's /datum/timedevent has one).</summary>
internal sealed class TextOverloadDatum : TestDatum
{
    public object operator__text() => "overloaded text";
}

/// <summary>A datum with an <c>operator+</c> overload.</summary>
internal sealed class AddOverloadDatum : TestDatum
{
    public object operator__add(object other) => "added " + Dm.str(other);
}

[DmPath("/mob/values_test_mob")]
internal sealed class KindMob : TestAtom
{
    public KindMob() : base("mob")
    {
    }
}

[DmPath("/obj/values_test_obj")]
internal sealed class KindObj : TestAtom
{
    public KindObj() : base("obj")
    {
    }
}

[DmPath("/turf/values_test_turf")]
internal sealed class KindTurf : TestAtom
{
    public KindTurf() : base("turf")
    {
    }
}

[DmPath("/area/values_test_area")]
internal sealed class KindArea : TestAtom
{
    public KindArea() : base("area")
    {
    }
}
