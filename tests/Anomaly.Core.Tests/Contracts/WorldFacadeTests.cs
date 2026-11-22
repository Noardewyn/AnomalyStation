using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;

namespace Anomaly.Core.Tests.Contracts;

/// <summary>
/// The <c>world</c> facade (docs/ai/translation-rules.md 10.8; DV3, DV6) and the shim's per-world state.
/// </summary>
public class WorldFacadeTests
{
    [Fact]
    public void TimeAndTheTickConstants()
    {
        var w = new World();
        w.Step(7);
        using (w.Enter())
        {
            Assert.Equal(3.5f, Dm.world.time);  // 7 ticks of 0.5 ds
            Assert.Equal(0.5f, Dm.world.tick_lag);
            Assert.Equal(20f, Dm.world.fps);
            Assert.Equal(0f, Dm.world.tick_usage); // virtual tick usage (DV3)
            Assert.Equal(0f, Dm.world.cpu);
            Assert.Same(Dm.world, Dm.world);
        }
    }

    [Fact]
    public void RealtimeAndTimeofdayAdvanceWithWorldTimeFromTheHostStart()
    {
        var w = new World();
        DmWorldState.Of(w).RealtimeStart = 864000 * 3 + 36000; // three days and one hour after 2000-01-01
        w.Step(20);                                               // 10 ds
        using (w.Enter())
        {
            Assert.Equal(36010f, Dm.world.timeofday);
            Assert.Equal((float)(864000 * 3 + 36010), Dm.world.realtime);
        }
    }

    [Fact]
    public void RealtimeAndTimeofdayAreWholeDeciseconds()
    {
        // BYOND's clocks tick in whole deciseconds; tg's client-time timers rely on it (timer.dm:621). A 0.5 ds tick
        // (20 TPS) does not show until the next whole decisecond.
        var w = new World();
        DmWorldState.Of(w).RealtimeStart = 36000;
        w.Step(1); // 0.5 ds
        using (w.Enter())
        {
            Assert.Equal(36000f, Dm.world.timeofday);
            Assert.Equal(36000f, Dm.world.realtime);
        }
        w.Step(1); // 1 ds
        using (w.Enter())
            Assert.Equal(36001f, Dm.world.timeofday);
    }

    [Fact]
    public void MapSizeIsPerWorldAndPartOfTheStateHash()
    {
        var w1 = new World(5);
        var w2 = new World(5);
        using (w1.Enter())
        {
            Dm.world.maxx = 255;
            Dm.world.maxz++;
            Assert.Equal(255f, Dm.world.maxx);
            Assert.Equal(1f, Dm.world.maxz);
        }
        using (w2.Enter())
        {
            Assert.Equal(0f, Dm.world.maxx);
            Dm.world.maxx = 255;
        }
        Assert.NotEqual(w1.ComputeStateHash(), w2.ComputeStateHash());
        using (w2.Enter())
            Dm.world.maxz = 1;
        Assert.Equal(w1.ComputeStateHash(), w2.ComputeStateHash());
    }

    [Fact]
    public void ReadingTheWorldOrInstallingSinksDoesNotChangeTheStateHash()
    {
        // The shim's per-world state is created on first use; a host that installs sinks or reads world vars must
        // not make its world hash differently from one that does not (object-model.md section 8: sinks and shim
        // caches are not hashed).
        var touched = new World(4);
        var untouched = new World(4);
        RecordingSinks.Install(touched);
        DmWorldState.Of(touched).WorldLog = _ => { };
        using (touched.Enter())
        {
            Assert.Equal(0f, Dm.world.realtime);
            Assert.Equal(0f, Dm.world.maxz);
            Dm.output_to(Dm.world.log, "hello");
        }
        Assert.Equal(untouched.ComputeStateHash(), touched.ComputeStateHash());
        using (touched.Enter())
            Dm.world.maxx = 3;
        Assert.NotEqual(untouched.ComputeStateHash(), touched.ComputeStateHash());
        using (touched.Enter())
            Dm.world.maxx = 0;
        Assert.Equal(untouched.ComputeStateHash(), touched.ComputeStateHash());
    }

    [Fact]
    public void ContentsListsTheLiveAtomsInIdOrder()
    {
        var w = new World();
        var datum = new TestDatum().Register<TestDatum>(w);
        var a = new TestAtom("a").Register<TestAtom>(w);
        var b = new TestAtom("b").Register<TestAtom>(w);
        using (w.Enter())
        {
            DmList contents = Dm.world.contents;
            Assert.Equal(2f, contents.len);
            Assert.Same(a, contents[1]);
            Assert.Same(b, contents[2]);
        }
        GC.KeepAlive(datum);
    }
}
