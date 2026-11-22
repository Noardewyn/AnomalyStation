using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Dcs;

/// <summary>
/// /datum/proc/AddElementTrait (code/datums/elements/_element.dm:91-104) and the element's
/// _detach_on_trait_removed (:106-109): an element managed by the sources of a trait. The trait is removed with tg's
/// REMOVE_TRAIT (<see cref="GlobalProcs.REMOVE_TRAIT"/>, __DEFINES/traits/_traits.dm:23), which sends SIGNAL_REMOVETRAIT when the last source goes.
/// </summary>
public class ElementTraitTests
{
    private const string TRAIT = "test_dcs_trait";

    [Fact]
    public void TheFirstSourceAttachesTheElementAndLaterSourcesOnlyAddToTheTrait()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");

            Waiting.NoSleep(host.AddElementTrait(TRAIT, "source_a", typeof(test_dcs_simple), 5));
            Waiting.NoSleep(host.AddElementTrait(TRAIT, "source_b", typeof(test_dcs_simple), 6));

            // :94-96: ADD_TRAIT first; HAS_TRAIT_NOT_FROM is true from the second source on, so only the first one attaches,
            // with the arguments after the fixed three (:97-102).
            Assert.Equal(new[] { "H Attach test_dcs_simple args=5" }, t.Trace);
            Assert.Equal(new object[] { "source_a", "source_b" }, Dm.each(host._status_traits[TRAIT]).ToArray());
            // :103-104: the element listens for the trait's removal.
            var ele = (datum_element)SSdcs.elements_by_type[typeof(test_dcs_simple)];
            Assert.Same(ele, host._listen_lookup[SIGNAL_REMOVETRAIT(TRAIT)]);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TheElementIsDetachedWhenTheLastSourceOfTheTraitIsRemoved()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            Waiting.NoSleep(host.AddElementTrait(TRAIT, "source_a", typeof(test_dcs_simple)));
            Waiting.NoSleep(host.AddElementTrait(TRAIT, "source_b", typeof(test_dcs_simple)));
            t.Watch(host);
            t.Trace.Clear();

            REMOVE_TRAIT(host, TRAIT, "source_a");
            Assert.Empty(t.Trace);

            REMOVE_TRAIT(host, TRAIT, "source_b");
            Assert.Equal(new[]
            {
                "H Detach test_dcs_simple",
                "H COMSIG_ELEMENT_DETACH test_dcs_simple",
            }, t.Trace);
            // :109: the removal signal is unregistered too.
            Assert.Null(host._listen_lookup[SIGNAL_REMOVETRAIT(TRAIT)]);
            Assert.Null(host._status_traits);
            t.Trace.Clear();

            // Adding the trait again attaches again.
            Waiting.NoSleep(host.AddElementTrait(TRAIT, "source_c", typeof(test_dcs_simple)));
            Assert.Equal(new[] { "H COMSIG_ELEMENT_ATTACH test_dcs_simple", "H Attach test_dcs_simple" }, t.Trace);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ABespokeElementTraitIsTheElementOfItsArguments()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");

            Waiting.NoSleep(host.AddElementTrait(TRAIT, "source_a", typeof(test_dcs_bespoke), 1, "x"));

            // :102-103: _AddElement gets a copy of list(eletype, 1, "x") (it writes src into it), GetElement the original.
            object ele = SSdcs.elements_by_type["%2fdatum%2felement%2ftest_dcs_bespoke&1&x"];
            Assert.IsType<test_dcs_bespoke>(ele);
            Assert.Same(ele, host._listen_lookup[SIGNAL_REMOVETRAIT(TRAIT)]);
            Assert.Equal(new[] { "H Attach test_dcs_bespoke args=1,x" }, t.Trace);

            REMOVE_TRAIT(host, TRAIT, "source_a");
            Assert.Equal("H Detach test_dcs_bespoke", t.Trace[^1]);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AddElementTraitCrashesForATypeThatIsNoElement()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");

            // :92-93, before the trait is added
            var crash = Assert.Throws<DmCrashException>(() => Waiting.NoSleep(host.AddElementTrait(TRAIT, "source_a", typeof(datum))));
            Assert.Equal("AddElementTrait called, but /datum is not of a /datum/element path", crash.Message);
            Assert.Null(host._status_traits);
        }
    }
}
