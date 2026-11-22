using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Datum;

/// <summary>
/// The small datum helpers of the package, written from the DM source: REF (__HELPERS/ref.dm), GenerateTag
/// (datums/datum.dm:321), is_valid_src and call_async (__HELPERS/datums.dm), stack_trace (__HELPERS/stack_trace.dm).
/// </summary>
public class DatumHelperTests
{
    [Fact]
    public void RefOfADatumIsItsTextRef()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            datum d = Dm.@new<datum>();
            Assert.Equal("[0x" + d.dm_id.ToString("x") + "]", REF(d));
            Assert.Equal(text_ref(d), REF(d));
            Assert.Equal("[0x0]", REF(null));
            Assert.Same(d, Dm.locate_ref(REF(d)));
        }
    }

    [Fact]
    public void RefOfATagUsingDatumWithoutATagStackTracesAndDropsTheFlag()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            datum d = Dm.@new<datum>();
            d.GenerateTag();
            Assert.Equal((float)DF_USE_TAG, d.datum_flags); // datum_flags |= DF_USE_TAG (datum.dm:322)

            Assert.Equal(text_ref(d), REF(d));
            // ref.dm:9-12
            Assert.Equal(new[] { "DmCrashException: " + DatumTestMacros.StackTraceText("A ref was requested of an object with DF_USE_TAG set but no tag: /datum", "code/__HELPERS/ref.dm", 11) }, t.Messages);
            Assert.Equal(0f, d.datum_flags);
        }
    }

    [Fact]
    public void IsValidSrcIsTrueForALiveDatumOnly()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            t.World.Step(2);
            datum d = Dm.@new<datum>();
            Assert.True(Dm.eq(is_valid_src(d), TRUE));
            Assert.True(Dm.eq(is_valid_src(null), FALSE));
            qdel(d);
            Assert.True(Dm.eq(is_valid_src(d), FALSE));
        }
    }

    [Fact]
    public void CallAsyncReturnsTheValueOnlyWhenTheCalleeDoesNotSleep()
    {
        var t = new DatumTestWorld();
        var trace = new List<string>();
        using (t.World.Enter())
        {
            test_datum_listener l = Dm.@new<test_datum_listener>();
            l.trace = trace;
            l.label = "l";
            l.returns = "value";
            // set waitfor = FALSE; return call(source, proc_type)(arglist(arguments)) (datums.dm:7-9)
            Assert.Equal("value", call_async(l, nameof(test_datum_listener.record_call), Dm.list("a")));
            Assert.Null(call_async(l, nameof(test_datum_listener.sleep_then_return), Dm.list(1, "late")));
        }
        t.World.Step(3);
        Assert.Equal(new[] { "l called a null null", "l sleeps", "l woke" }, trace);
        Assert.Empty(t.Runtimes);
    }

    [Fact]
    public void StackTraceReportsWithTheUseSiteAndLetsTheCallerCarryOn()
    {
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            stack_trace("pilot message", "code/pilot.dm", 12);
            stack_trace(5, "code/pilot.dm", 13);
            // _stack_trace's CRASH("[message][WORKAROUND_IDENTIFIER][json_encode(list(file, line))][WORKAROUND_IDENTIFIER]")
            // (stack_trace.dm:4) as world/Error rewrites it (error_handler.dm:62-66): the message without the use site, and
            // the use site as the runtime's file and line (rules 9.5); reported and returned from ("without ending the
            // current proc").
            Assert.Equal(new[] { "DmCrashException: pilot message", "DmCrashException: 5" }, t.Messages);
            Assert.Equal(new[] { "code/pilot.dm", "code/pilot.dm" }, t.Runtimes.Select(e => ((DmCrashException)e).DmFile));
            Assert.Equal(new[] { 12, 13 }, t.Runtimes.Select(e => ((DmCrashException)e).DmLine));
        }
    }

    [Fact]
    public void ADirectCallOfUnderscoreStackTraceReportsTgsTextAndReturns()
    {
        // tg forbids it ("Do not call directly, use the [stack_trace] macro instead"); without world/Error's rewrite the
        // runtime keeps tg's text and _stack_trace's own location. The CRASH ends only _stack_trace (rules 9.3).
        var t = new DatumTestWorld();
        using (t.World.Enter())
        {
            _stack_trace("pilot message", "code/pilot.dm", 12);
            Assert.Equal(new[] { "DmCrashException: pilot message%//%[\"code/pilot.dm\",12]%//%" }, t.Messages);
            var crash = (DmCrashException)t.Runtimes[0];
            Assert.Equal("code/__HELPERS/stack_trace.dm", crash.DmFile);
            Assert.Equal(4, crash.DmLine);
        }
    }
}
