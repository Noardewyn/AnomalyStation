using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Datum;

/// <summary>
/// The component and element wrappers of code/__DEFINES/dcs/helpers.dm:14-26, written from the DM source: each is
/// <c>_X(list(##arguments))</c>, so the callee gets one list with the positional arguments in order and each named
/// argument as a text-keyed entry. The callees belong to the dcs package; the host below records what it receives.
/// </summary>
public class DcsHelperTests
{
    [Fact]
    public void AddComponentWrapsItsArgumentsInOneListWithNamedArgumentsAsTextKeys()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            test_datum_dcs_host host = Dm.@new<test_datum_dcs_host>();
            DmList inner = Dm.list(1, 2);

            // AddComponent(/datum/component/x, inner, is_directional = TRUE) is _AddComponent(list(/datum/component/x, inner, "is_directional" = TRUE))
            object result = Waiting.NoSleep(host.AddComponent(typeof(test_datum_component), inner, Dm.named("is_directional", TRUE)));

            Assert.Equal("added", result);
            Assert.Equal(new[] { "_AddComponent source=null" }, host.calls);
            DmList raw = host.last_list;
            Assert.Equal(3f, raw.len);
            Assert.Equal(typeof(test_datum_component), raw[1]);
            Assert.Same(inner, raw[2]); // list(##arguments) keeps a list argument as one item
            Assert.Equal("is_directional", raw[3]);
            Assert.True(Dm.eq(raw["is_directional"], TRUE));
        }
    }

    [Fact]
    public void AddComponentFromPassesTheSourceAfterTheList()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            test_datum_dcs_host host = Dm.@new<test_datum_dcs_host>();
            datum source = Dm.@new<datum>();

            // AddComponentFrom(source, arguments...) is _AddComponent(list(##arguments), source)
            Waiting.NoSleep(host.AddComponentFrom(source, typeof(test_datum_component), 5));

            Assert.Equal(new[] { "_AddComponent source=#" + source.dm_id }, host.calls);
            Assert.Equal(new object[] { typeof(test_datum_component), 5 }, Dm.each(host.last_list).ToArray());
        }
    }

    [Fact]
    public void TheElementWrappersAndLoadComponentPassTheirListsAsWell()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            test_datum_dcs_host host = Dm.@new<test_datum_dcs_host>();

            Waiting.NoSleep(host.AddElement(typeof(datum), "a"));
            Assert.Equal(new object[] { typeof(datum), "a" }, Dm.each(host.last_list).ToArray());
            host.RemoveElement(typeof(datum), Dm.named("flag", 2));
            Assert.Equal(new object[] { typeof(datum), "flag" }, Dm.each(host.last_list).ToArray());
            Assert.True(Dm.eq(host.last_list["flag"], 2));
            Assert.Equal("loaded", Waiting.NoSleep(host.LoadComponent(typeof(test_datum_component))));
            Assert.Equal(new object[] { typeof(test_datum_component) }, Dm.each(host.last_list).ToArray());

            // No arguments: list() is an empty list.
            host.RemoveElement();
            Assert.Equal(0f, host.last_list.len);

            Assert.Equal(new[] { "_AddElement", "_RemoveElement", "_LoadComponent", "_RemoveElement" }, host.calls);
            Assert.Empty(t.Runtimes);
        }
    }
}

/// <summary>A datum whose dcs entry points record the list the wrappers build.</summary>
[DmPath("/datum/test_datum_dcs_host")]
public class test_datum_dcs_host : datum
{
    [NotHashed]
    public List<string> calls = new();

    [NotHashed]
    public DmList last_list;

    public override async DmTask<object> _AddComponent(DmList raw_args = null, object source = null)
    {
        calls.Add("_AddComponent source=" + test_datum_listener.Describe(source));
        last_list = raw_args;
        return "added";
    }

    public override async DmTask<object> _LoadComponent(DmList arguments = null)
    {
        calls.Add("_LoadComponent");
        last_list = arguments;
        return "loaded";
    }

    public override async DmTask _AddElement(DmList arguments = null, params object[] __extra)
    {
        calls.Add("_AddElement");
        last_list = arguments;
    }

    public override void _RemoveElement(DmList arguments = null)
    {
        calls.Add("_RemoveElement");
        last_list = arguments;
    }
}
