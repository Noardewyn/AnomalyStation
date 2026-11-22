using Anomaly.Core.DmShim;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tests.Dynamic;

/// <summary>
/// The argument plumbing of variadic slots: <c>Dm.arg</c>, <c>Dm.arg&lt;T&gt;</c>, <c>Dm.forward</c>, <c>Dm.args</c>
/// (rules 7.2, 7.5, 7.6) and the arguments the pre-New hook sees.
/// </summary>
public class ArgsTests
{
    [Fact]
    public void ArgReadsAnExtraOrNull()
    {
        // Rules 7.2: object a = Dm.arg(__extra, 0); missing extras are null (BYOND ref "arguments (proc)").
        object[] extra = { "a", 2 };
        Assert.Equal("a", arg(extra, 0));
        Assert.Equal(2, arg(extra, 1));
        Assert.Null(arg(extra, 2));
        Assert.Null(arg(null, 0));                                     // a null __extra is one null
        Assert.Null(arg(Array.Empty<object>(), 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => arg(extra, -1));
    }

    [Fact]
    public void ArgOfTConvertsByRules35()
    {
        // Rules 7.2: Dm.arg<obj_item>(__extra, 1) has `as` semantics; Dm.arg<float?>(__extra, 2) is Dm.num_or_null.
        var item = new fx_item();
        object[] extra = { 3, item, null, "text", 1.5 };
        Assert.Equal(3f, arg<float?>(extra, 0));
        Assert.Equal(3f, arg<float>(extra, 0));
        Assert.Same(item, arg<fx_item>(extra, 1));
        Assert.Null(arg<fx_stack>(extra, 1));                          // another class: null
        Assert.Null(arg<float?>(extra, 2));
        Assert.Equal(0f, arg<float>(extra, 2));
        Assert.Equal("text", arg<string>(extra, 3));
        Assert.Null(arg<DmList>(extra, 3));
        Assert.Equal(1.5f, arg<float?>(extra, 4));
        Assert.Null(arg<string>(extra, 7));
        Assert.Throws<DmRuntimeException>(() => arg<float?>(extra, 3));    // text is not a number
        Assert.Throws<DmRuntimeException>(() => arg<string>(extra, 0));    // a number is not text
    }

    [Fact]
    public void ForwardReplacesTheUnpackedEntriesOfACopy()
    {
        // Rules 7.5: ..() passes the current values of every argument; stack.use's ..() is
        // base.use(used, Dm.forward(__extra, transfer, check)).
        object[] extra = { "t0", "c0", "more" };
        object[] forwarded = forward(extra, "t1", "c1");
        Assert.Equal(new object[] { "t1", "c1", "more" }, forwarded);
        Assert.Equal(new object[] { "t0", "c0", "more" }, extra);      // a copy
        Assert.Equal(new object[] { "a", "b" }, forward(Array.Empty<object>(), "a", "b"));
        Assert.Equal(new object[] { "a", null }, forward(new object[] { null, null }, "a"));
        Assert.Equal(new object[] { "a" }, forward(null, "a"));          // a null __extra is one null, replaced
        Assert.Equal(new object[] { null, "x" }, forward(new object[] { "y", "x" }, null));
    }

    [Fact]
    public void ArgsListsTheParametersThenTheExtras()
    {
        // Rules 7.6: DmList args = Dm.args(p1, p2, __extra); /atom/New's args is Dm.args(Dm.forward(__extra, loc)).
        Assert.Equal(new object[] { 1, 2, "x", "y" }, Items(args(1, 2, new object[] { "x", "y" })));
        Assert.Equal(new object[] { 1, 2 }, Items(args(1, 2, Array.Empty<object>())));
        Assert.Equal(new object[] { "loc", "extra" }, Items(args(forward(new object[] { null, "extra" }, "loc"))));
        Assert.Equal(new object[] { 1, null }, Items(args(1, null)));    // a null __extra: one null extra
        Assert.Equal(new object[] { null }, Items(args(null)));
        Assert.Equal(0, args().Count);
        Assert.Equal(new object[] { 7 }, Items(args(new object[] { 7 })));
    }

    [Fact]
    public void ThePreNewHookSeesPositionalAndNamedArgumentsAndTheInitialLocation()
    {
        // BYOND ref "New proc (atom)": the first positional argument, or the one named loc when there is none.
        var positional = new DmArgs(new object[] { "here", 2 }, new[] { new KeyValuePair<string, object>("loc", "there") });
        Assert.Equal("here", positional.InitialLocation);
        var byName = new DmArgs(null, new[] { new KeyValuePair<string, object>("loc", "there") });
        Assert.Equal("there", byName.InitialLocation);
        Assert.Null(DmArgs.Empty.InitialLocation);
        var twice = new DmArgs(null, new[] { new KeyValuePair<string, object>("a", 1), new KeyValuePair<string, object>("a", 2) });
        Assert.True(twice.TryGetNamed("a", out object value));
        Assert.Equal(2, value);
        Assert.False(twice.TryGetNamed("A", out _));
    }

    private static object[] Items(DmList list)
    {
        var items = new object[list.Count];
        for (int i = 0; i < items.Length; i++) items[i] = list[(float)(i + 1)];
        return items;
    }
}
