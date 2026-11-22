using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Dcs;

/// <summary>
/// Determinism of the dcs package (translation-rules.md 15: the same scenario twice gives the same
/// <c>World.ComputeStateHash()</c>): components in every lookup shape, sources, a transfer, elements of each kind and
/// an element trait, and the hash sees the dcs state (a parent's components, SSdcs's elements, the signals they hold).
/// </summary>
public class DcsStateHashTests
{
    // keep holds the scenario's objects until the hash is taken (the registry's entries are weak, object-model.md 6).
    private static ulong Scenario(Action<test_dcs_host> change = null, ulong seed = 11)
    {
        var keep = new List<object>();
        var t = new DcsTestWorld(seed);
        using (t.World.Enter())
        {
            t.World.Step(2);
            test_dcs_host h1 = t.Host("H1");
            test_dcs_host h2 = t.Host("H2");
            test_dcs_host source = t.Host("source");
            Waiting.NoSleep(h1.AddComponent(typeof(test_dcs_highlander_sub), "S"));
            Waiting.NoSleep(h1.AddComponent(typeof(test_dcs_allowed), "A1"));
            Waiting.NoSleep(h1.AddComponent(typeof(test_dcs_allowed_sub), "A2"));
            Waiting.NoSleep(h1.AddComponentFrom(source, typeof(test_dcs_sources), "C"));
            var moving = (datum_component)Waiting.NoSleep(h1.AddComponent(typeof(test_dcs_transferable), "T"));
            Waiting.NoSleep(h2.TakeComponent(moving));
            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_host_bound)));
            Waiting.NoSleep(h2.AddElement(typeof(test_dcs_bespoke), 1, Dm.named("a", "x")));
            Waiting.NoSleep(h1.AddElementTrait("test_dcs_trait", "trait_source", typeof(test_dcs_simple)));
            test_dcs_host gone = t.Host("gone");
            Waiting.NoSleep(gone.AddComponent(typeof(test_dcs_unique), "U"));
            qdel(gone);

            change?.Invoke(h1);
            keep.AddRange(new object[] { h1, h2, source, gone });
        }
        ulong hash = t.World.ComputeStateHash();
        GC.KeepAlive(keep);
        return hash;
    }

    [Fact]
    public void TheSameScenarioTwiceGivesTheSameHash()
    {
        ulong first = Scenario();
        Assert.Equal(first, Scenario());
        Assert.NotEqual(first, Scenario(seed: 12));
    }

    public static IEnumerable<object[]> Changes() => new[]
    {
        new object[] { "a component var", (Action<test_dcs_host>)(h => ((datum_component)h.GetComponent(typeof(test_dcs_highlander))).can_transfer = TRUE) },
        new object[] { "another component", (Action<test_dcs_host>)(h => Waiting.NoSleep(h.AddComponent(typeof(test_dcs_unique), "U2"))) },
        new object[] { "a deleted component", (Action<test_dcs_host>)(h => qdel(h.GetComponent(typeof(test_dcs_highlander)))) },
        new object[] { "another source", (Action<test_dcs_host>)(h => Waiting.NoSleep(h.AddComponentFrom(h, typeof(test_dcs_sources), "C"))) },
        new object[] { "a detached element", (Action<test_dcs_host>)(h => h.RemoveElement(typeof(test_dcs_host_bound))) },
        new object[] { "another bespoke element", (Action<test_dcs_host>)(h => Waiting.NoSleep(h.AddElement(typeof(test_dcs_bespoke), 2))) },
    };

    [Theory]
    [MemberData(nameof(Changes))]
    public void AChangedDcsStateChangesTheHash(string what, Action<test_dcs_host> change)
    {
        Assert.NotEqual(Scenario(), Scenario(change));
        Assert.Equal(Scenario(change), Scenario(change)); // and the changed scenario is deterministic too ({what})
        Assert.NotNull(what);
    }
}
