using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Traits;

/// <summary>
/// The trait helpers of code/__HELPERS/traits.dm, written from the DM source: TRAIT_CALLBACK_ADD/REMOVE (:1-2), the
/// global procs they call, ___TraitAdd and ___TraitRemove (:5-32), and /datum/proc/add_traits and remove_traits (:36-45).
/// </summary>
public class TraitHelperTests
{
    private const string TRAIT_A = "pilot_trait_a";
    private const string TRAIT_B = "pilot_trait_b";

    [Fact]
    public void TraitCallbacksAreGlobalProcCallbacksWithTheTargetTheTraitAndTheSource()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            datum target = Dm.@new<datum>();

            // CALLBACK(GLOBAL_PROC, GLOBAL_PROC_REF(___TraitAdd), ##target, ##trait, ##source)   (:1)
            datum_callback add = TRAIT_CALLBACK_ADD(target, TRAIT_A, "source_cb");
            Assert.Equal(GLOBAL_PROC, add.@object);
            Assert.Equal("/proc/___TraitAdd", add.@delegate);
            Assert.Equal(new object[] { target, TRAIT_A, "source_cb" }, Dm.each(add.arguments).ToArray());
            datum_callback remove = TRAIT_CALLBACK_REMOVE(target, TRAIT_A, "source_cb");
            Assert.Equal("/proc/___TraitRemove", remove.@delegate);
            Assert.Equal(new object[] { target, TRAIT_A, "source_cb" }, Dm.each(remove.arguments).ToArray());
            // Creating them changes nothing yet.
            Assert.Null(target._status_traits);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void InvokingTheTraitCallbacksAddsAndRemovesTheTrait()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            var trace = new List<string>();
            datum target = Dm.@new<datum>();
            test_traits_listener listener = Dm.@new<test_traits_listener>();
            listener.trace = trace;
            listener.Listen(target, TRAIT_A);
            ADD_TRAIT(target, TRAIT_A, "source_other");
            trace.Clear();

            datum_callback add = TRAIT_CALLBACK_ADD(target, TRAIT_A, "source_cb");
            Assert.Null(Waiting.NoSleep(add.Invoke()));
            Assert.Equal("[source_other,source_cb]", test_traits_listener.Sources(target, TRAIT_A));
            // A callback is reusable; adding the same source again changes nothing.
            Waiting.NoSleep(add.Invoke());
            Assert.Equal(2f, COUNT_TRAIT_SOURCES(target, TRAIT_A));

            // Timers call their callbacks with InvokeAsync (timer.dm:125, :172), which has set waitfor = FALSE.
            datum_callback remove = TRAIT_CALLBACK_REMOVE(target, TRAIT_A, "source_cb");
            Assert.Null(remove.InvokeAsync());
            Assert.True(HAS_TRAIT_FROM_ONLY(target, TRAIT_A, "source_other"));
            Assert.Empty(trace);

            Waiting.NoSleep(TRAIT_CALLBACK_REMOVE(target, TRAIT_A, "source_other").Invoke());
            Assert.False(HAS_TRAIT(target, TRAIT_A));
            Assert.Equal(new[] { "listener remove" }, trace.Select(line => string.Join(' ', line.Split(' ').Take(2))));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ATraitCallbackMadeDuringAnActionRemovesTheTraitAsItsUser()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            // tg: addtimer(TRAIT_CALLBACK_REMOVE(target, TRAIT_IWASBATONED, user_ref), cooldown) (baton.dm:305). The
            // callback keeps WEAKREF(usr) (callback.dm:68-69); invoked later without a usr it runs the trait proc
            // through world.push_usr (callback.dm:96-101, mobs.dm:709), so the signal handlers see that user.
            var trace = new List<string>();
            var seenUsr = new List<object>();
            datum target = Dm.@new<datum>();
            mob user = Dm.@new<mob>();
            test_traits_listener listener = Dm.@new<test_traits_listener>();
            listener.trace = trace;
            listener.seen_usr = seenUsr;
            listener.RegisterSignal(target, SIGNAL_REMOVETRAIT(TRAIT_A), nameof(test_traits_listener.on_removetrait_usr));
            ADD_TRAIT(target, TRAIT_A, REF(user));

            usr = user;
            datum_callback remove = TRAIT_CALLBACK_REMOVE(target, TRAIT_A, REF(user));
            usr = null;
            Assert.Same(user, remove.user.resolve());

            Waiting.NoSleep(remove.Invoke());
            Assert.False(HAS_TRAIT(target, TRAIT_A));
            Assert.Equal(new object[] { user }, seenUsr);
            Assert.Null(usr);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TheTraitProcsDoNothingWithoutATargetATraitOrASource()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            datum target = Dm.@new<datum>();

            // if(!target || !trait || !source) return   (:6-7, :21-22): null, "" and 0 are all false.
            foreach (object empty in new object[] { null, "", 0f })
            {
                ___TraitAdd(empty, TRAIT_A, "s1");
                ___TraitAdd(target, empty, "s1");
                ___TraitAdd(target, TRAIT_A, empty);
            }
            Assert.Null(target._status_traits);

            ADD_TRAIT(target, TRAIT_A, "s1");
            foreach (object empty in new object[] { null, "", 0f })
            {
                ___TraitRemove(empty, TRAIT_A, "s1");
                ___TraitRemove(target, empty, "s1");
                ___TraitRemove(target, TRAIT_A, empty);
            }
            // ___TraitRemove with no source returns before REMOVE_TRAIT could remove every source (it never passes null).
            Assert.True(HAS_TRAIT_FROM_ONLY(target, TRAIT_A, "s1"));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void OnAListTheTraitProcsApplyToEachDatumInIt()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            datum first = Dm.@new<datum>();
            datum second = Dm.@new<datum>();
            datum outside = Dm.@new<datum>();

            // for(var/datum/listed_target in target) ADD_TRAIT(listed_target, trait, source)   (:9-12): the typed loop
            // skips what is not a datum.
            DmList targets = Dm.list(first, "text", null, 5, second, Dm.list(outside));
            Waiting.NoSleep(TRAIT_CALLBACK_ADD(targets, TRAIT_A, "source_cb").Invoke());
            Assert.True(HAS_TRAIT(first, TRAIT_A));
            Assert.True(HAS_TRAIT(second, TRAIT_A));
            Assert.False(HAS_TRAIT(outside, TRAIT_A));

            ADD_TRAIT(second, TRAIT_A, "source_other");
            Waiting.NoSleep(TRAIT_CALLBACK_REMOVE(targets, TRAIT_A, "source_cb").Invoke());
            Assert.False(HAS_TRAIT(first, TRAIT_A));
            Assert.True(HAS_TRAIT_FROM_ONLY(second, TRAIT_A, "source_other"));

            // An empty list is a list (true): nothing to do, and no assertion.
            ___TraitAdd(Dm.list(), TRAIT_A, "source_cb");
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AnAssertBuildsItsMessageOnlyWhenItFails()
    {
        // ASSERT is a macro (__DEFINES/assert.dm:4): `if (!(condition)) { CRASH(assertion_message(..., ##message)) }`, so
        // its message ("... Invoked on [src] ...", traits.dm:37) is rendered only when the condition is false.
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            test_traits_text_counter target = Dm.@new<test_traits_text_counter>();
            target.add_traits(Dm.list(TRAIT_A), "s1");
            target.remove_traits(Dm.list(TRAIT_A), "s1");
            Assert.Equal(0, target.renders);

            var crash = Assert.Throws<DmCrashException>(() => target.add_traits(null, "s1"));
            Assert.Equal(1, target.renders);
            Assert.Equal("code/__HELPERS/traits.dm:37:Assertion failed: islist(list_of_traits) - Invalid arguments passed to add_traits! Invoked on counter with , source being s1.", crash.Message);
            Assert.Equal("code/__HELPERS/traits.dm", crash.DmFile);
            Assert.Equal(37, crash.DmLine);
        }
    }

    [Fact]
    public void ATraitCallbackOnANonDatumFailsItsAssertion()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            // ASSERT(isdatum(target), "Invalid target used in TRAIT_CALLBACK_ADD! ... got [target] instead.")   (:14)
            var crash = Assert.Throws<DmCrashException>(() => ___TraitAdd("not a datum", TRAIT_A, "s1"));
            Assert.Equal("code/__HELPERS/traits.dm:14:Assertion failed: isdatum(target) - Invalid target used in TRAIT_CALLBACK_ADD! Expected a datum reference, got not a datum instead.", crash.Message);
            Assert.Equal("code/__HELPERS/traits.dm", crash.DmFile);
            Assert.Equal(14, crash.DmLine);

            crash = Assert.Throws<DmCrashException>(() => ___TraitRemove(7f, TRAIT_A, "s1"));
            Assert.Equal("code/__HELPERS/traits.dm:29:Assertion failed: isdatum(target) - Invalid target used in TRAIT_CALLBACK_REMOVE! Expected a datum reference, got 7 instead.", crash.Message);
            Assert.Equal(29, crash.DmLine);

            // Through the callback the runtime ends at Invoke's isolation boundary (rules 9.2): Invoke returns null and
            // the runtime is reported.
            Assert.Null(Waiting.NoSleep(TRAIT_CALLBACK_ADD("not a datum", TRAIT_A, "s1").Invoke()));
            Assert.Equal(new[] { "DmCrashException: code/__HELPERS/traits.dm:14:Assertion failed: isdatum(target) - Invalid target used in TRAIT_CALLBACK_ADD! Expected a datum reference, got not a datum instead." }, t.Messages);
        }
    }

    [Fact]
    public void AddTraitsAndRemoveTraitsApplyOneSourceToEveryTraitOfTheList()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            datum target = Dm.@new<datum>();
            ADD_TRAIT(target, TRAIT_B, "source_other");

            target.add_traits(Dm.list(TRAIT_A, TRAIT_B, TRAIT_A), "source_main");
            Assert.Equal("[source_main]", test_traits_listener.Sources(target, TRAIT_A));
            Assert.Equal("[source_other,source_main]", test_traits_listener.Sources(target, TRAIT_B));

            // remove_traits with another source leaves them; with the source it removes only that source.
            target.remove_traits(Dm.list(TRAIT_A, TRAIT_B), "source_unknown");
            Assert.Equal(2f, target._status_traits.len);
            target.remove_traits(Dm.list(TRAIT_A, TRAIT_B), "source_main");
            Assert.False(HAS_TRAIT(target, TRAIT_A));
            Assert.Equal("[source_other]", test_traits_listener.Sources(target, TRAIT_B));

            // REMOVE_TRAIT(src, trait, source) with a null source removes every source but roundstart (traits.dm:45).
            ADD_TRAIT(target, TRAIT_B, ROUNDSTART_TRAIT);
            target.remove_traits(Dm.list(TRAIT_B), null);
            Assert.Equal("[roundstart]", test_traits_listener.Sources(target, TRAIT_B));

            // An empty list does nothing.
            target.add_traits(Dm.list(), "source_main");
            Assert.Equal(1f, target._status_traits.len);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AddTraitsAndRemoveTraitsAssertThatTheyGotAList()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            datum target = Dm.@new<datum>();

            // ASSERT(islist(list_of_traits), "... Invoked on [src] with [list_of_traits], source being [source].")
            // (traits.dm:37, :43); [src] of a datum is its type path, null renders as nothing.
            var crash = Assert.Throws<DmCrashException>(() => target.add_traits(null, "source_main"));
            Assert.Equal("code/__HELPERS/traits.dm:37:Assertion failed: islist(list_of_traits) - Invalid arguments passed to add_traits! Invoked on /datum with , source being source_main.", crash.Message);
            crash = Assert.Throws<DmCrashException>(() => target.remove_traits(null, "source_main"));
            Assert.Equal("code/__HELPERS/traits.dm:43:Assertion failed: islist(list_of_traits) - Invalid arguments passed to remove_traits! Invoked on /datum with , source being source_main.", crash.Message);

            // A call by name with a single trait text instead of a list fails the assertion before any trait is added.
            // (The binder converts the text to the slot's DmList parameter, which gives null, so the message renders
            // "with ," where DM would print the text; see the slot note on add_traits in docs/ai/specs/generated/slots.tsv.)
            Assert.Throws<DmCrashException>(() => Waiting.NoSleep(Dm.Call(target, nameof(datum.add_traits), TRAIT_A, "source_main")));
            Assert.Null(target._status_traits);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TheListHelpersIterateACopyOfTheirList()
    {
        var t = new TraitTestWorld();
        using (t.World.Enter())
        {
            // for(var/trait in list_of_traits) (traits.dm:38, :44) iterates a copy (rules 6.5): a trait that a signal
            // handler appends to the list while add_traits runs is not reached, and the list keeps it.
            var trace = new List<string>();
            datum target = Dm.@new<datum>();
            test_traits_listener listener = Dm.@new<test_traits_listener>();
            listener.trace = trace;
            DmList traits = Dm.list(TRAIT_A, TRAIT_B);
            listener.react_list = traits;
            listener.react_trait = "pilot_trait_late";
            listener.RegisterSignal(target, SIGNAL_ADDTRAIT(TRAIT_A), nameof(test_traits_listener.on_addtrait_append));

            target.add_traits(traits, "source_main");

            Assert.Single(trace);
            Assert.Equal(new object[] { TRAIT_A, TRAIT_B, "pilot_trait_late" }, Dm.each(traits).ToArray());
            Assert.True(HAS_TRAIT(target, TRAIT_B));
            Assert.False(HAS_TRAIT(target, "pilot_trait_late"));
            Assert.Empty(t.Runtimes);
        }
    }
}
