using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Traits;

/// <summary>
/// The trait signals of code/__DEFINES/traits/_traits.dm, written from the DM source: SIGNAL_ADDTRAIT is sent when a
/// trait gets its first source (ADD_TRAIT, :12 and :19), SIGNAL_REMOVETRAIT when the last source goes (REMOVE_TRAIT
/// :40, REMOVE_TRAIT_NOT_FROM :64, REMOVE_TRAITS_NOT_IN :82, REMOVE_TRAITS_IN :107), each with (target, trait), and
/// always after the source lists were updated but before an empty _status_traits is dropped.
/// </summary>
public class TraitSignalTests
{
    private const string TRAIT_A = "pilot_trait_a";
    private const string TRAIT_B = "pilot_trait_b";
    private const string TRAIT_C = "pilot_trait_c";

    private static test_traits_listener Listener(List<string> trace, string label = "listener")
    {
        test_traits_listener l = Dm.@new<test_traits_listener>();
        l.trace = trace;
        l.label = label;
        return l;
    }

    [Fact]
    public void TheSignalNamesAreTheTraitPrefixedWithAddtraitAndRemovetrait()
    {
        // #define SIGNAL_ADDTRAIT(trait_ref) ("addtrait " + trait_ref)    (_traits.dm:1)
        // #define SIGNAL_REMOVETRAIT(trait_ref) ("removetrait " + trait_ref) (_traits.dm:2)
        Assert.Equal("addtrait " + TRAIT_GODMODE, SIGNAL_ADDTRAIT(TRAIT_GODMODE));
        Assert.Equal("removetrait " + TRAIT_GODMODE, SIGNAL_REMOVETRAIT(TRAIT_GODMODE));
        Assert.Equal("addtrait godmode", SIGNAL_ADDTRAIT("godmode"));
    }

    [Fact]
    public void OnlyTheFirstSourceOfATraitSendsAddtrait()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum target = Dm.@new<datum>();
            test_traits_listener listener = Listener(trace);
            listener.Listen(target, TRAIT_A);
            listener.Listen(target, TRAIT_B);
            string id = test_traits_listener.Describe(target);

            // First trait of a datum without _status_traits (the first branch, :8-12).
            ADD_TRAIT(target, TRAIT_A, "source_1");
            // Further sources of the same trait (:15-16) send nothing, a repeated source is not added twice (|=).
            ADD_TRAIT(target, TRAIT_A, "source_2");
            ADD_TRAIT(target, TRAIT_A, "source_1");
            // A new trait of a datum that has _status_traits already (the else branch, :17-19).
            ADD_TRAIT(target, TRAIT_B, "source_1");

            Assert.Equal(new[]
            {
                "listener add pilot_trait_a source=" + id + " has=1 sources=[source_1] status=1",
                "listener add pilot_trait_b source=" + id + " has=1 sources=[source_1] status=2",
            }, trace);
            Assert.Equal("[source_1,source_2]", test_traits_listener.Sources(target, TRAIT_A));
            Assert.Equal(2f, COUNT_TRAIT_SOURCES(target, TRAIT_A));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void OnlyTheRemovalOfTheLastSourceSendsRemovetrait()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum target = Dm.@new<datum>();
            test_traits_listener listener = Listener(trace);
            listener.Listen(target, TRAIT_A);
            ADD_TRAIT(target, TRAIT_A, "source_1");
            ADD_TRAIT(target, TRAIT_A, "source_2");
            ADD_TRAIT(target, TRAIT_B, "source_1");
            trace.Clear();

            REMOVE_TRAIT(target, TRAIT_A, "source_1");
            Assert.Empty(trace);
            // A source the trait does not have, and a trait the datum does not have: nothing happens (:32).
            REMOVE_TRAIT(target, TRAIT_A, "source_9");
            REMOVE_TRAIT(target, TRAIT_C, "source_1");
            Assert.Empty(trace);
            Assert.Equal("[source_2]", test_traits_listener.Sources(target, TRAIT_A));

            REMOVE_TRAIT(target, TRAIT_A, "source_2");
            // The signal comes after _L -= trait (:39-40); TRAIT_B keeps the list alive.
            Assert.Equal(new[] { "listener remove pilot_trait_a source=" + test_traits_listener.Describe(target) + " has=0 sources=none status=1" }, trace);

            // Removing it again finds no trait and sends nothing.
            REMOVE_TRAIT(target, TRAIT_A, "source_2");
            Assert.Single(trace);
            Assert.True(HAS_TRAIT(target, TRAIT_B));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void RemovetraitOfTheLastTraitSeesTheEmptyListThatIsDroppedAfterIt()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum target = Dm.@new<datum>();
            Listener(trace).Listen(target, TRAIT_A);
            ADD_TRAIT(target, TRAIT_A, "source_1");
            DmList status = target._status_traits;

            REMOVE_TRAIT(target, TRAIT_A, "source_1");

            // _L -= trait; SEND_SIGNAL(...) (:39-40), then if(!length(_L)) target._status_traits = null (:42-43): the
            // handler sees the emptied list, the macro drops it afterwards.
            Assert.Equal("listener remove pilot_trait_a source=" + test_traits_listener.Describe(target) + " has=0 sources=none status=0", trace.Last());
            Assert.Null(target._status_traits);
            Assert.Equal(0f, status.len);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ATraitAddedByTheRemovetraitHandlerKeepsTheTraitList()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum target = Dm.@new<datum>();
            test_traits_listener listener = Listener(trace);
            listener.react_trait = TRAIT_B;
            listener.react_source = "from_handler";
            listener.RegisterSignal(target, SIGNAL_REMOVETRAIT(TRAIT_A), nameof(test_traits_listener.on_removetrait_add_other));
            listener.RegisterSignal(target, SIGNAL_ADDTRAIT(TRAIT_B), nameof(test_traits_listener.on_addtrait));
            ADD_TRAIT(target, TRAIT_A, "source_1");
            DmList status = target._status_traits;

            REMOVE_TRAIT(target, TRAIT_A, "source_1");

            // The handler runs while _status_traits is the emptied (but present) list, so its ADD_TRAIT takes the else
            // branch (:13-19) into that same list; REMOVE_TRAIT then finds it non-empty and keeps it (:42).
            string id = test_traits_listener.Describe(target);
            Assert.Equal(new[]
            {
                "listener remove pilot_trait_a source=" + id + " has=0 sources=none status=0",
                "listener add pilot_trait_b source=" + id + " has=1 sources=[from_handler] status=1",
            }, trace);
            Assert.Same(status, target._status_traits);
            Assert.True(HAS_TRAIT(target, TRAIT_B));
            Assert.False(HAS_TRAIT(target, TRAIT_A));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AnAddtraitHandlerThatAddsTheSameTraitOnlyAddsASource()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum target = Dm.@new<datum>();
            test_traits_listener listener = Listener(trace);
            listener.react_source = "from_handler";
            listener.RegisterSignal(target, SIGNAL_ADDTRAIT(TRAIT_A), nameof(test_traits_listener.on_addtrait_add_again));

            ADD_TRAIT(target, TRAIT_A, "source_1");

            // The signal is ADD_TRAIT's last statement (:12): the handler finds the trait present and only adds its
            // source (:15-16), which sends nothing.
            Assert.Single(trace);
            Assert.Equal("[source_1,from_handler]", test_traits_listener.Sources(target, TRAIT_A));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void EveryListenerGetsTheSignalAndOtherTraitsSignalsAreNotSent()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum target = Dm.@new<datum>();
            Listener(trace, "first").Listen(target, TRAIT_A);
            Listener(trace, "second").Listen(target, TRAIT_A);
            Listener(trace, "other").Listen(target, TRAIT_B);

            ADD_TRAIT(target, TRAIT_A, "source_1");
            REMOVE_TRAIT(target, TRAIT_A, "source_1");

            Assert.Equal(new[] { "first add", "second add", "first remove", "second remove" },
                trace.Select(line => string.Join(' ', line.Split(' ').Take(2))));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void RemoveTraitNotFromSendsRemovetraitWhenNoSourceIsLeft()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum target = Dm.@new<datum>();
            Listener(trace).Listen(target, TRAIT_A);
            ADD_TRAIT(target, TRAIT_A, "source_1");
            ADD_TRAIT(target, TRAIT_A, "source_2");
            trace.Clear();

            REMOVE_TRAIT_NOT_FROM(target, TRAIT_A, "source_2");
            Assert.Empty(trace);
            Assert.Equal("[source_2]", test_traits_listener.Sources(target, TRAIT_A));

            REMOVE_TRAIT_NOT_FROM(target, TRAIT_A, "source_9");
            // _traits_list -= trait; SEND_SIGNAL(...) (:63-64), then the empty list is dropped (:66-67).
            Assert.Equal(new[] { "listener remove pilot_trait_a source=" + test_traits_listener.Describe(target) + " has=0 sources=none status=0" }, trace);
            Assert.Null(target._status_traits);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TheMultiTraitRemovalsSignalEachEmptiedTraitInListOrder()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum target = Dm.@new<datum>();
            test_traits_listener listener = Listener(trace);
            listener.Listen(target, TRAIT_A);
            listener.Listen(target, TRAIT_B);
            listener.Listen(target, TRAIT_C);
            ADD_TRAIT(target, TRAIT_A, "source_main");
            ADD_TRAIT(target, TRAIT_B, "source_alt");
            ADD_TRAIT(target, TRAIT_C, "source_alt");
            ADD_TRAIT(target, TRAIT_C, "source_main");
            trace.Clear();
            string id = test_traits_listener.Describe(target);

            // REMOVE_TRAITS_NOT_IN (:71-89): every trait keeps only the given sources; B loses its only one.
            REMOVE_TRAITS_NOT_IN(target, "source_main");
            Assert.Equal(new[] { "listener remove pilot_trait_b source=" + id + " has=0 sources=none status=2" }, trace);
            Assert.Equal("[source_main]", test_traits_listener.Sources(target, TRAIT_C));

            // REMOVE_TRAITS_IN (:91-114): A and C lose source_main, in the order of _status_traits, each signalled after
            // its own removal; the empty list is dropped after the loop.
            trace.Clear();
            REMOVE_TRAITS_IN(target, "source_main");
            Assert.Equal(new[]
            {
                "listener remove pilot_trait_a source=" + id + " has=0 sources=none status=1",
                "listener remove pilot_trait_c source=" + id + " has=0 sources=none status=0",
            }, trace);
            Assert.Null(target._status_traits);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AddTraitsSignalsEachNewTraitInListOrder()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum target = Dm.@new<datum>();
            test_traits_listener listener = Listener(trace);
            listener.Listen(target, TRAIT_A);
            listener.Listen(target, TRAIT_B);
            listener.Listen(target, TRAIT_C);
            ADD_TRAIT(target, TRAIT_B, "source_other");
            trace.Clear();

            // /datum/proc/add_traits: for(var/trait in list_of_traits) ADD_TRAIT(src, trait, source) (traits.dm:38-39).
            target.add_traits(Dm.list(TRAIT_C, TRAIT_B, TRAIT_A), "source_main");
            Assert.Equal(new[] { "listener add pilot_trait_c", "listener add pilot_trait_a" },
                trace.Select(line => string.Join(' ', line.Split(' ').Take(3))));

            // /datum/proc/remove_traits (traits.dm:44-45): B keeps source_other, so only C and A are signalled.
            trace.Clear();
            target.remove_traits(Dm.list(TRAIT_C, TRAIT_B, TRAIT_A), "source_main");
            Assert.Equal(new[] { "listener remove pilot_trait_c", "listener remove pilot_trait_a" },
                trace.Select(line => string.Join(' ', line.Split(' ').Take(3))));
            Assert.Equal("[source_other]", test_traits_listener.Sources(target, TRAIT_B));
            Assert.Empty(t.Runtimes);
        }
    }
}
