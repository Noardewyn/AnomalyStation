using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Traits;

/// <summary>
/// Compile-and-run smoke tests of the traits package (wave 2 pilot): the translated trait macros and helpers run end
/// to end in a world. The behaviour tests written from the DM code belong to the test-writer.
/// </summary>
public class SmokeTests
{
    private static World NewWorld(out List<Exception> runtimes)
    {
        var errors = new List<Exception>();
        runtimes = errors;
        var world = new World(5) { RuntimeErrorSink = errors.Add };
        AtomsStandIn.Install(world); // GLOB and SSatoms, which /atom/New reads (wave 3)
        return world;
    }

    [Fact]
    public void TheFirstSourceSignalsAdditionAndTheLastRemovalSignalsRemoval()
    {
        World w = NewWorld(out List<Exception> runtimes);
        using (w.Enter())
        {
            datum target = Dm.@new<datum>();
            test_traits_smoke_listener listener = Dm.@new<test_traits_smoke_listener>();
            listener.RegisterSignal(target, SIGNAL_ADDTRAIT(TRAIT_GODMODE), nameof(test_traits_smoke_listener.on_trait));
            listener.RegisterSignal(target, SIGNAL_REMOVETRAIT(TRAIT_GODMODE), nameof(test_traits_smoke_listener.on_trait));
            Assert.Equal("addtrait godmode", SIGNAL_ADDTRAIT(TRAIT_GODMODE));

            ADD_TRAIT(target, TRAIT_GODMODE, "source_a");
            ADD_TRAIT(target, TRAIT_GODMODE, "source_b");
            ADD_TRAIT(target, TRAIT_GODMODE, "source_b");
            Assert.True(HAS_TRAIT(target, TRAIT_GODMODE));
            Assert.True(HAS_TRAIT_FROM(target, TRAIT_GODMODE, "source_b"));
            Assert.False(HAS_TRAIT_FROM_ONLY(target, TRAIT_GODMODE, "source_a"));
            Assert.True(HAS_TRAIT_NOT_FROM(target, TRAIT_GODMODE, "source_a"));
            Assert.Equal(2f, COUNT_TRAIT_SOURCES(target, TRAIT_GODMODE));

            REMOVE_TRAIT(target, TRAIT_GODMODE, "source_a");
            Assert.True(HAS_TRAIT_FROM_ONLY(target, TRAIT_GODMODE, "source_b"));
            REMOVE_TRAIT(target, TRAIT_GODMODE, Dm.list("source_b"));
            Assert.False(HAS_TRAIT(target, TRAIT_GODMODE));
            Assert.Null(target._status_traits);
            Assert.Equal(new[] { "godmode", "godmode" }, listener.seen);
            Assert.Equal(0f, COUNT_TRAIT_SOURCES(target, TRAIT_GODMODE));
            Assert.False(HAS_TRAIT(null, TRAIT_GODMODE));
            Assert.Empty(runtimes);
        }
    }

    [Fact]
    public void RemovalWithoutSourcesKeepsRoundstartAndTheSourceListMacrosFilter()
    {
        World w = NewWorld(out _);
        using (w.Enter())
        {
            datum target = Dm.@new<datum>();
            ADD_TRAIT(target, TRAIT_GODMODE, ROUNDSTART_TRAIT);
            ADD_TRAIT(target, TRAIT_GODMODE, "source_a");
            REMOVE_TRAIT(target, TRAIT_GODMODE, null);
            Assert.True(HAS_TRAIT_FROM_ONLY(target, TRAIT_GODMODE, ROUNDSTART_TRAIT));

            ADD_TRAIT(target, TRAIT_GODMODE, "source_a");
            REMOVE_TRAIT_NOT_FROM(target, TRAIT_GODMODE, "source_a");
            Assert.True(HAS_TRAIT_FROM_ONLY(target, TRAIT_GODMODE, "source_a"));

            ADD_TRAIT(target, TRAIT_NOBREATH, "source_main");
            ADD_TRAIT(target, TRAIT_NOBREATH, "source_alt");
            ADD_TRAIT(target, TRAIT_RESISTCOLD, "source_alt");
            REMOVE_TRAITS_NOT_IN(target, "source_main");
            Assert.True(HAS_TRAIT_FROM_ONLY(target, TRAIT_NOBREATH, "source_main"));
            Assert.False(HAS_TRAIT(target, TRAIT_GODMODE));
            Assert.False(HAS_TRAIT(target, TRAIT_RESISTCOLD));

            REMOVE_TRAITS_IN(target, "source_main");
            Assert.Null(target._status_traits);
        }
    }

    [Fact]
    public void ListHelpersAndTraitCallbacksAddAndRemove()
    {
        World w = NewWorld(out List<Exception> runtimes);
        using (w.Enter())
        {
            datum target = Dm.@new<datum>();
            datum other = Dm.@new<datum>();
            target.add_traits(Dm.list(TRAIT_GODMODE, TRAIT_NOBREATH), "source_main");
            Assert.Equal(2f, target._status_traits.len);
            target.remove_traits(Dm.list(TRAIT_GODMODE, TRAIT_NOBREATH), "source_main");
            Assert.Null(target._status_traits);

            datum_callback add = TRAIT_CALLBACK_ADD(Dm.list(target, other), TRAIT_GODMODE, "source_cb");
            Waiting.NoSleep(add.Invoke());
            Assert.True(HAS_TRAIT(target, TRAIT_GODMODE));
            Assert.True(HAS_TRAIT(other, TRAIT_GODMODE));
            datum_callback remove = TRAIT_CALLBACK_REMOVE(target, TRAIT_GODMODE, "source_cb");
            Waiting.NoSleep(remove.Invoke());
            Assert.False(HAS_TRAIT(target, TRAIT_GODMODE));
            Assert.True(HAS_TRAIT(other, TRAIT_GODMODE));
            Assert.Empty(runtimes);

            var crash = Assert.Throws<DmCrashException>(() => target.add_traits(null, "source_main"));
            Assert.StartsWith("code/__HELPERS/traits.dm:37:Assertion failed: islist(list_of_traits) - Invalid arguments passed to add_traits!", crash.Message);
        }
    }

    [Fact]
    public void ParametrizedTraitNamesAndMindTraitsRender()
    {
        World w = NewWorld(out _);
        using (w.Enter())
        {
            Assert.Equal("distortion_in_use_#2", TRAIT_DISTORTION_IN_USE(2));
            Assert.Equal("parallax_displayed__map", TRAIT_PARALLAX_DISPLAYED(""));
            Assert.Equal("metabolize:/datum", METABOLIZATION_TRAIT(typeof(datum)));
            Assert.Equal("element_trait_/datum/mind", ELEMENT_TRAIT(typeof(datum_mind)));
            Assert.Equal("5-trait", TRAIT_STATUS_EFFECT(5));
            Assert.Equal("turf_has_elevated_obj_3", TRAIT_TURF_HAS_ELEVATED_OBJ(3));

            mob M = Dm.@new<mob>();
            Assert.False(HAS_MIND_TRAIT(M, TRAIT_GODMODE));
            M.mind = Dm.@new<datum_mind_test_bare_mind>(); // a bare world has no SSjob for a mind's New (TraitFixtures)
            ADD_TRAIT(M.mind, TRAIT_GODMODE, "source_mind");
            Assert.True(HAS_MIND_TRAIT(M, TRAIT_GODMODE));
            Assert.False(HAS_TRAIT(M, TRAIT_GODMODE));
        }
    }
}

/// <summary>A listener for the trait signals, for the smoke tests.</summary>
[DmPath("/datum/test_traits_smoke_listener")]
public class test_traits_smoke_listener : datum
{
    [NotHashed]
    public List<string> seen = new List<string>();

    [SignalHandler]
    public virtual void on_trait(datum source = null, object trait = null)
    {
        seen.Add(Dm.str(trait));
    }
}
