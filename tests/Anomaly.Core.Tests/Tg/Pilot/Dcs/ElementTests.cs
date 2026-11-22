using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Pilot.Datum;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Dcs;

/// <summary>
/// Elements, written from code/datums/elements/_element.dm (Attach :29, OnTargetDelete :37, Detach :42, Destroy :49,
/// _AddElement :58, _RemoveElement :75) and SSdcs.GetElement (controllers/subsystem/dcs.dm:35). Bespoke ids are in
/// <see cref="BespokeElementTests"/>, AddElementTrait in <see cref="ElementTraitTests"/>.
/// </summary>
public class ElementTests
{
    [Fact]
    public void OneElementInstancePerTypeServesEveryTarget()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            test_dcs_host h2 = t.Host("H2");
            t.Watch(h1, h2);

            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_simple), 1));
            Waiting.NoSleep(h2.AddElement(typeof(test_dcs_simple), 2));

            // "Only one element of each type is instanced" (:4): GetElement creates it with no arguments on first use
            // (dcs.dm:45-48) and _AddElement calls Attach(src, ...) (:67-68), whose parent call sends COMSIG_ELEMENT_ATTACH.
            Assert.Equal(1f, SSdcs.elements_by_type.len);
            var ele = Assert.IsType<test_dcs_simple>(SSdcs.elements_by_type[typeof(test_dcs_simple)]);
            Assert.Same(ele, SSdcs.GetElement(Dm.list(typeof(test_dcs_simple))));
            Assert.Equal(new[]
            {
                "H1 COMSIG_ELEMENT_ATTACH test_dcs_simple",
                "H1 Attach test_dcs_simple args=1",
                "H2 COMSIG_ELEMENT_ATTACH test_dcs_simple",
                "H2 Attach test_dcs_simple args=2",
            }, t.Trace);
            // An element keeps nothing on its targets by itself.
            Assert.Null(h1._datum_components);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void RemoveElementDetachesTheTargetAndKeepsTheElement()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_simple)));
            t.Watch(h1);
            t.Trace.Clear();

            h1.RemoveElement(typeof(test_dcs_simple), "ignored");

            // :82-83: Detach(src) only, the other arguments are for bespoke lookups; Detach's parent call sends
            // COMSIG_ELEMENT_DETACH (:46).
            Assert.Equal(new[]
            {
                "H1 Detach test_dcs_simple",
                "H1 COMSIG_ELEMENT_DETACH test_dcs_simple",
            }, t.Trace);
            Assert.NotNull(SSdcs.elements_by_type[typeof(test_dcs_simple)]);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void RemoveElementOfAnElementThatWasNeverMadeDoesNotMakeIt()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");

            // GetElement(arguments, FALSE) (:76; dcs.dm:46-47)
            h1.RemoveElement(typeof(test_dcs_simple));

            Assert.Equal(0f, SSdcs.elements_by_type.len);
            Assert.Null(SSdcs.GetElement(Dm.list(typeof(test_dcs_simple)), FALSE));
            Assert.Empty(t.Trace);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AnElementFlaggedToDetachOnHostDestroyIsDetachedByTheHostsQdel()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_host_bound)));
            var ele = (datum_element)SSdcs.elements_by_type[typeof(test_dcs_host_bound)];
            // :34-35: RegisterSignal(target, COMSIG_QDELETING, PROC_REF(OnTargetDelete), override = TRUE)
            Assert.Same(ele, h1._listen_lookup[COMSIG_QDELETING]);
            t.Watch(h1);
            t.Trace.Clear();

            qdel(h1);

            // COMSIG_QDELETING comes before the host's Destroy clears its listeners, so the watcher still hears the
            // detach; Detach unregisters the element's COMSIG_QDELETING (:47).
            Assert.Equal(new[]
            {
                "H1 Detach test_dcs_host_bound",
                "H1 COMSIG_ELEMENT_DETACH test_dcs_host_bound",
            }, t.Trace);
            Assert.Null(h1._listen_lookup);
            Assert.Equal(0f, ele._signal_procs.len); // UnregisterSignal drops the target's entry, not the list (signals.dm)
            Assert.False(QDELETED(ele));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AnElementWithoutTheFlagIsNotDetachedByTheHostsQdel()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_simple)));
            t.Trace.Clear();

            qdel(h1);

            Assert.Empty(t.Trace);
            Assert.Null(h1._listen_lookup);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AttachingTheSameElementTwiceAttachesTwiceAndOverridesTheQdeletingRegistrationQuietly()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_host_bound), 1));
            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_host_bound), 2));

            // _AddElement has no duplicate check; override = TRUE (:35) keeps RegisterSignal from reporting the
            // overridden registration (signals.dm:60-62).
            Assert.Equal(new[] { "H1 Attach test_dcs_host_bound args=1", "H1 Attach test_dcs_host_bound args=2" }, t.Trace);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AComplexDetachElementGetsTheRemoveElementArguments()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_complex_detach), 1, "x"));
            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_bespoke), 1, "x"));
            t.Trace.Clear();

            // :79-81: arguments[1] = src; ele.Detach(arglist(arguments))
            h1.RemoveElement(typeof(test_dcs_complex_detach), 1, "x");
            // :82-83 without the flag
            h1.RemoveElement(typeof(test_dcs_bespoke), 1, "x");

            Assert.Equal(new[]
            {
                "H1 Detach test_dcs_complex_detach args=1,x",
                "H1 Detach test_dcs_bespoke",
            }, t.Trace);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AnIncompatibleElementCrashesAddElement()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");

            // :68-69
            var crash = Assert.Throws<DmCrashException>(() => Waiting.NoSleep(h1.AddElement(typeof(test_dcs_incompatible), 5)));
            Assert.Equal("Incompatible element /datum/element/test_dcs_incompatible was assigned to a /datum/test_dcs_host! args: [[\"/datum/test_dcs_host\",5]]", crash.Message);
            Assert.Equal(69, crash.DmLine);

            // The base type refuses itself (:31-32); GetElement made one anyway.
            var root = Assert.Throws<DmCrashException>(() => Waiting.NoSleep(h1.AddElement(typeof(datum_element))));
            Assert.StartsWith("Incompatible element /datum/element was assigned to a /datum/test_dcs_host!", root.Message);
            Assert.IsType<datum_element>(SSdcs.elements_by_type[typeof(datum_element)]);
        }
    }

    [Fact]
    public void AddElementOfATypeThatIsNoElementCrashesInGetElement()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");

            // dcs.dm:39-40. DEVIATION(DV1): in DM the CRASH ends GetElement, which returns null, and _AddElement returns
            // quietly ("the crash message has already been sent", :66); in the port it unwinds through _AddElement.
            var crash = Assert.Throws<DmCrashException>(() => Waiting.NoSleep(h1.AddElement(typeof(datum))));
            Assert.Equal("Attempted to instantiate /datum as a /datum/element", crash.Message);
            Assert.Equal("code/controllers/subsystem/dcs.dm", crash.DmFile);
            Assert.Equal(0f, SSdcs.elements_by_type.len);
        }
    }

    [Fact]
    public void AddElementToAQdeletedDatumIsReportedAndDoesNothing()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            qdel(h1);

            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_simple)));

            // :59-62: before GetElement, so no element is made.
            Assert.Equal(new[]
            {
                "DmCrashException: " + DatumTestMacros.StackTraceText("We just tried to add the element /datum/element/test_dcs_simple to a qdeleted datum, something is fucked", "code/datums/elements/_element.dm", 61),
            }, t.Messages);
            Assert.Equal(0f, SSdcs.elements_by_type.len);
            Assert.Empty(t.Trace);
        }
    }

    [Fact]
    public void AnElementLivesThroughAQdelWithoutForceAndLeavesTheRegistryWithForce()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_simple)));
            var ele = (datum_element)SSdcs.elements_by_type[typeof(test_dcs_simple)];

            // :50-51: QDEL_HINT_LETMELIVE, and qdel clears gc_destroyed (garbage.dm:381-384).
            qdel(ele);
            Assert.False(QDELETED(ele));
            Assert.Same(ele, SSdcs.elements_by_type[typeof(test_dcs_simple)]);

            // :52-53
            qdel(ele, TRUE);
            Assert.True(QDELETED(ele));
            Assert.Null(SSdcs.elements_by_type[typeof(test_dcs_simple)]);
            Assert.Equal(0f, SSdcs.elements_by_type.len);

            // The next AddElement makes a new one.
            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_simple)));
            Assert.NotSame(ele, SSdcs.elements_by_type[typeof(test_dcs_simple)]);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TgQuirkAForcedQdelOfABespokeElementLeavesItsIdInTheRegistry()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            Waiting.NoSleep(h1.AddElement(typeof(test_dcs_bespoke), 1));
            object id = SSdcs.GetIdFromArguments(Dm.list(typeof(test_dcs_bespoke), 1));
            var ele = (datum_element)SSdcs.elements_by_type[id];

            // :52: SSdcs.elements_by_type -= type, but a bespoke element is stored under its id (dcs.dm:42-48).
            qdel(ele, TRUE);

            Assert.True(QDELETED(ele));
            Assert.Same(ele, SSdcs.elements_by_type[id]);
            // GetElement hands out the deleted element again.
            Assert.Same(ele, SSdcs.GetElement(Dm.list(typeof(test_dcs_bespoke), 1)));
            Assert.Empty(t.Runtimes);
        }
    }
}
