using Anomaly.Core.DmShim;

namespace Anomaly.Core.Tests.DmValues;

/// <summary>
/// Numeric builtins (docs/ai/translation-rules.md 4.6): BYOND ref <c>/proc/round</c>, <c>floor</c>, <c>ceil</c>,
/// <c>trunc</c>, <c>fract</c>, <c>sign</c>, <c>abs</c>, <c>sqrt</c>, <c>min</c>, <c>max</c>, <c>clamp</c>, <c>log</c>,
/// <c>lerp</c> and the trigonometric procs, with their examples; tg's round(A, B) tie rule; OpenDream's BYOND-checked
/// tests (Math/*.dm, Builtins/Clamp, Max, lerp, log_*, sign).
/// </summary>
public class MathTests
{
    [Fact]
    public void RoundOneArgumentIsFloor()
    {
        // BYOND ref /proc/round: round(1.45) outputs 1, round(-1.45) outputs -2.
        Assert.Equal(1f, Dm.round(1.45f));
        Assert.Equal(-2f, Dm.round(-1.45f));
    }

    [Fact]
    public void RoundToAMultipleRoundsTiesUp()
    {
        // tg __DEFINES/atmospherics/atmos_core.dm:41-42 ("When it has a second arg it will round up. So for instance
        // round(0.5, 1) == 1"); rules 4.6; BYOND ref example round(1.45, 1.5) outputs 1.5.
        Assert.Equal(1f, Dm.round(0.5f, 1));
        Assert.Equal(3f, Dm.round(2.5f, 1));
        Assert.Equal(0f, Dm.round(-0.5f, 1));
        Assert.Equal(1.5f, Dm.round(1.45f, 1.5f));
        Assert.Equal(-2f, Dm.round(-2.5f, 1));
        Assert.Equal(0.25f, Dm.round(0.3f, 0.25f));
    }

    [Fact]
    public void FloorCeilTruncFract()
    {
        // BYOND ref examples; OpenDream tests floor, ceil, trunc, fract.
        Assert.Equal(1f, Dm.floor(1.45f));
        Assert.Equal(-2f, Dm.floor(-1.45f));
        Assert.Equal(7f, Dm.floor(7.2f));
        Assert.Equal(-4f, Dm.floor(-3.21f));
        Assert.Equal(2f, Dm.ceil(1.45f));
        Assert.Equal(-1f, Dm.ceil(-1.45f));
        Assert.Equal(8f, Dm.ceil(7.2f));
        Assert.Equal(-3f, Dm.ceil(-3.21f));
        Assert.Equal(5f, Dm.trunc(5.5f));
        Assert.Equal(99f, Dm.trunc(99.762f));
        Assert.True(float.IsPositiveInfinity(Dm.trunc(float.PositiveInfinity)));
        Assert.True(float.IsNaN(Dm.trunc(float.NaN)));
        Assert.Equal(0f, Dm.fract(6f));
        Assert.Equal(0.5f, Dm.fract(1.5f));
        Assert.Equal(-0.5f, Dm.fract(-1.5f));
        Assert.Equal(0.5f, Dm.fract(99.5f));
        Assert.Equal(0f, Dm.fract(float.PositiveInfinity));
        Assert.True(float.IsNaN(Dm.fract(float.NaN)));
    }

    [Fact]
    public void SignAbsSqrt()
    {
        // BYOND ref sign examples (20 -> 1, 0 -> 0, -3 -> -1), abs, sqrt(2) outputs 1.41421; OpenDream test sign.
        Assert.Equal(1f, Dm.sign(20f));
        Assert.Equal(0f, Dm.sign(0f));
        Assert.Equal(-1f, Dm.sign(-3f));
        Assert.Equal(1f, Dm.sign(5.2f));
        Assert.Equal(-1f, Dm.sign(-5.2f));
        Assert.Equal(1f, Dm.abs(-1f));
        Assert.Equal(1f, Dm.abs(1f));
        Assert.Equal("1.41421", Dm.str(Dm.sqrt(2f)));
        Assert.True(float.IsNaN(Dm.sqrt(-1f)));
    }

    [Fact]
    public void MinAndMaxOfNumbers()
    {
        Assert.Equal(1f, Dm.min(1f, 2f, 3f));          // BYOND ref min example
        Assert.Equal(3f, Dm.max(1f, 2f, 3f));          // BYOND ref max example
        Assert.Equal(4f, Dm.max(4f, 3f, 2f, 1f));
        Assert.Equal(4f, Dm.max(1f, 3f, 4f, 2f));
        Assert.Equal(-1f, Dm.min(5f, -1f));
    }

    [Fact]
    public void MinAndMaxWithNullTextAndLists()
    {
        // OpenDream tests Math/minmax and Builtins/Max: null counts as 0 and the later of equal values wins for
        // min; text compares as text; a single list argument supplies the values.
        Assert.Null(Dm.min(null, 1f));
        V.Eq(-1f, Dm.min(null, -1f));
        Assert.Null(Dm.min(1f, null));
        V.Eq(-1f, Dm.min(-1f, null));
        Assert.Null(Dm.min(0f, null));
        V.Eq(0f, Dm.min(null, 0f));
        V.Eq("a", Dm.min("a", "b", "c"));
        V.Eq("a", Dm.min("b", "a", "c"));
        V.Eq(1f, Dm.max(null, 1f));
        Assert.Null(Dm.max(null, -1f));
        V.Eq(1f, Dm.max(1f, null));
        Assert.Null(Dm.max(-1f, null));
        Assert.Null(Dm.max(0f, null));
        V.Eq(0f, Dm.max(null, 0f));
        V.Eq("c", Dm.max("a", "b", "c"));
        V.Eq("c", Dm.max("b", "a", "c"));
        V.Eq(1f, Dm.max((object)1f));                  // "Single arg returns that arg"
        Assert.Null(Dm.max(null, null, null));
        V.Eq("", Dm.max(null, ""));
        V.Eq("", Dm.max("", null));
        V.Eq("str", Dm.max("", "str", null));
        V.Eq(5f, Dm.max(5f, null));
        V.Eq(3f, Dm.max(Dm.list(1f, 3f, 2f)));       // BYOND ref: a single list argument
        V.Eq(1f, Dm.min(Dm.arglist(Dm.list(1f, 3f, 2f))));   // rules 7.3: min(arglist(L))
        Assert.Null(Dm.max(Dm.list()));
        V.Error(() => Dm.max(1f, Dm.list()));
    }

    [Fact]
    public void ClampNumbersAndLists()
    {
        // BYOND ref clamp examples (5 -> 5, -1 -> 0, 20 -> 10 within 0..10; Low and High in either order; a list is
        // clamped in place); OpenDream test Clamp.
        Assert.Equal(5f, Dm.clamp(5f, 0f, 10f));
        Assert.Equal(0f, Dm.clamp(-1f, 0f, 10f));
        Assert.Equal(10f, Dm.clamp(20f, 0f, 10f));
        Assert.Equal(5f, Dm.clamp(10f, 1f, 5f));
        Assert.Equal(1f, Dm.clamp(-10f, 1f, 5f));
        Assert.Equal(10f, Dm.clamp(20f, 10f, 0f));
        V.Eq(5f, Dm.clamp((object)10f, 1f, 5f));
        V.Eq(1f, Dm.clamp((object)null, 1f, 5f));    // null is 0
        DmList L = Dm.list(-10f, 5f, 40f, -40f);
        Assert.Same(L, Dm.clamp(L, 1f, 10f));
        V.Items(L, 1f, 5f, 10f, 1f);
        V.Error(() => Dm.clamp((object)"text", 1f, 5f));
        V.Error(() => Dm.clamp((object)1f, null, 5f));
    }

    [Fact]
    public void Logarithms()
    {
        // BYOND ref /proc/log: "The logarithm (base X) of Y"; OpenDream tests log_zero, log_negative, log_one_base,
        // log_zero_base (runtime errors).
        Assert.Equal(0f, Dm.log(1f));
        Assert.Equal(1f, Dm.log(MathF.E), 5);
        Assert.Equal(2f, Dm.log(10f, 100f), 5);
        Assert.Equal(3f, Dm.log(2f, 8f), 5);
        V.Error(() => Dm.log(0f));
        V.Error(() => Dm.log(-1f));
        V.Error(() => Dm.log(1f, 8f));
        V.Error(() => Dm.log(0f, 8f));
    }

    [Fact]
    public void Lerp()
    {
        // BYOND ref lerp(1, 5, 0.25) outputs 2; OpenDream test lerp.
        Assert.Equal(2f, Dm.lerp(1, 5, 0.25f));
        Assert.Equal(1f, Dm.lerp(1, 5, 0));
        Assert.Equal(5f, Dm.lerp(1, 5, 1));
        Assert.Equal(0.75f, Dm.lerp(1, 0, 0.25f));
        Assert.Equal(1f, Dm.lerp(1, 1, 0.25f));
        Assert.Equal(-0.5f, Dm.lerp(1, -5, 0.25f));
        Assert.Equal(-3f, Dm.lerp(1, 5, -1));
        Assert.Equal(9f, Dm.lerp(1, 5, 2));
    }

    [Fact]
    public void TrigonometryInDegrees()
    {
        // BYOND ref sin, arcsin, arccos, arctan examples; OpenDream test Math/trig.
        Assert.Equal(0f, Dm.sin(0));
        Assert.Equal(1f, Dm.sin(90));
        Assert.Equal("0.707107", Dm.str(Dm.sin(45)));
        Assert.Equal(1f, Dm.cos(0));
        Assert.Equal(0f, Dm.arcsin(0));
        Assert.Equal("45", Dm.str(Dm.arcsin(0.707107f)));
        Assert.Equal(90f, Dm.arcsin(1));
        Assert.Equal(90f, Dm.arccos(0));
        Assert.Equal("45", Dm.str(Dm.arccos(0.707107f)));
        Assert.Equal(0f, Dm.arccos(1));
        Assert.Equal(0f, Dm.arctan(0));
        Assert.Equal(45f, Dm.arctan(1));
        Assert.Equal(60f, Dm.arctan(Dm.sqrt(3)));
        Assert.Equal("53.1301", Dm.str(Dm.arctan(3, 4)));
        Assert.Equal(135f, Dm.arctan(-1, 1));
        Assert.Equal(-90f, Dm.arctan(0, -5));
        Assert.Equal(0f, Dm.arctan(1, 0));
        Assert.Equal(90f, Dm.arctan(0, 1));
        Assert.Equal(180f, Dm.arctan(-1, 0));
        Assert.Equal(0f, Dm.arctan(0, 0));             // "At point 0,0 ... arctan will return 0"
        Assert.Equal(1f, Dm.tan(45), 5);
    }

    [Fact]
    public void ClampOfAnAlistIsARuntimeErrorThatLeavesItAlone()
    {
        // clamp(List, ...) changes the list's items by position ("The original List, whose contents have all been
        // clamped", BYOND ref /proc/clamp); an alist has no positions to write (a number there is a key, BYOND ref
        // /proc/alist; positional changes of an alist are runtime errors, OpenDream tests AListInsert, AListSplice,
        // AListSwap), so the port refuses it instead of reading and writing the values of number keys 1..len.
        DmList AL = Dm.alist(Dm.kv("a", 5f), Dm.kv(2f, 9f));
        V.Error(() => Dm.clamp(AL, 0f, 1f));
        Assert.Equal(2f, AL.len);
        V.Eq(5f, AL["a"]);
        V.Eq(9f, AL[2]);
        Assert.False(Dm.@in(1f, AL));
    }

    [Fact]
    public void RoundToAMultipleKeepsAValueThatIsAlreadyAMultiple()
    {
        // BYOND ref /proc/round: the second format "rounds A to the nearest multiple of B", so a value that is a multiple
        // of B is its own result at every magnitude a float holds exactly; A / B + 0.5 must not be rounded to float
        // first (from 2^23 up, float has no halves: 8388609.5 would round to 8388610).
        Assert.Equal(8388609f, Dm.round(8388609f, 1));
        Assert.Equal(16777215f, Dm.round(16777215f, 1));
        Assert.Equal(-8388609f, Dm.round(-8388609f, 1));
        Assert.Equal(4194305.5f, Dm.round(4194305.5f, 0.5f));
        Assert.Equal(12582915f, Dm.round(12582915f, 5));
        Assert.Equal(1f, Dm.round(0.5f, 1));                 // ties still round up (rules 4.6)
        Assert.Equal(3f, Dm.round(2.5f, 1));
        Assert.Equal(0f, Dm.round(-0.5f, 1));
        Assert.Equal(-2f, Dm.round(-2.5f, 1));
        Assert.Equal(1.5f, Dm.round(1.45f, 1.5f));
    }
}
