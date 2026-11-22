using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Pilot.Datum;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Dcs;

/// <summary>
/// A component's life, written from code/datums/components/_component.dm: /datum/component/New (:43) and Initialize
/// (:68), _JoinParent (:89), Destroy (:77) and _RemoveFromParent (:127), the parent's Destroy (datums/datum.dm:146-157),
/// the CRASHes of _AddComponent (:313), and _LoadComponent (:426).
/// </summary>
public class ComponentLifecycleTests
{
    [Fact]
    public void NewGivesInitializeEveryArgumentButTheParentWithNamedArgumentsBoundByName()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");

            // AddComponent(/datum/component/x, initialize_result = null, label = "N") is _AddComponent(list(x, "initialize_result" = null, "label" = "N"));
            // New gets list(src, ...) and calls Initialize(arglist(raw_args.Copy(2))) (:43-47).
            var c = (dcs_recording_component)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_highlander), Dm.named("initialize_result", null), Dm.named("label", "N")));

            Assert.Equal("N", c.label);
            Assert.Same(host, c.parent);
            Assert.Equal(new[] { "N Initialize parent=H", "N RegisterWithParent parent=H" }, t.Trace);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AnIncompatibleComponentIsReportedAndDeletedWithForceBeforeItJoins()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            t.Watch(host);

            object result = Waiting.NoSleep(host.AddComponent(typeof(test_dcs_highlander), "A", COMPONENT_INCOMPATIBLE));

            // :49-52: stack_trace, qdel(src, TRUE, TRUE): Destroy with force skips _RemoveFromParent (it never joined) but
            // still sends COMSIG_COMPONENT_REMOVING to the parent it was meant for (:82). _AddComponent returns null (:405).
            Assert.Null(result);
            Assert.Equal(new[]
            {
                "A Initialize parent=H returns 1",
                "A Destroy force=1 parent=H",
                "H COMSIG_COMPONENT_REMOVING A parent=H",
            }, t.Trace);
            Assert.Null(host._datum_components);
            Assert.Equal(new[]
            {
                "DmCrashException: " + DatumTestMacros.StackTraceText("Incompatible /datum/component/test_dcs_highlander assigned to a /datum/test_dcs_host! args: [\"A\",1]", "code/datums/components/_component.dm", 50),
            }, t.Messages);
        }
    }

    [Fact]
    public void ARedundantComponentIsDeletedTheSameWayWithoutAReport()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            t.Watch(host);

            object result = Waiting.NoSleep(host.AddComponent(typeof(test_dcs_highlander), "A", COMPONENT_REDUNDANT));

            Assert.Null(result);
            Assert.Equal(new[]
            {
                "A Initialize parent=H returns 3",
                "A Destroy force=1 parent=H",
                "H COMSIG_COMPONENT_REMOVING A parent=H",
            }, t.Trace);
            Assert.Null(host._datum_components);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void QdelOfAComponentRemovesItFromEveryTypeKeyUnregistersAndSignals()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            var c = (test_dcs_highlander_sub)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_highlander_sub), "S"));
            t.Watch(host);
            t.Trace.Clear();

            qdel(c);

            Assert.Equal(new[]
            {
                "S Destroy force=0 parent=H",
                "S UnregisterFromParent parent=H",
                "H COMSIG_COMPONENT_REMOVING S parent=H",
            }, t.Trace);
            // :144-145: the last component gone, the parent's list is dropped.
            Assert.Null(host._datum_components);
            Assert.Null(c.parent);
            Assert.True(QDELETED(c));
            Assert.Null(host.GetComponent(typeof(test_dcs_highlander)));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void QdelOfAComponentWithForceLeavesItInItsParentsList()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            var c = (test_dcs_highlander)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_highlander), "A"));
            t.Watch(host);
            t.Trace.Clear();

            // :75: "force - makes it not check for and remove the component from the parent"
            qdel(c, TRUE);

            Assert.Equal(new[]
            {
                "A Destroy force=1 parent=H",
                "H COMSIG_COMPONENT_REMOVING A parent=H",
            }, t.Trace);
            Assert.Null(c.parent);
            Assert.Same(c, host.GetComponent(typeof(test_dcs_highlander)));
            Assert.True(QDELETED((datum)host.GetComponent(typeof(test_dcs_highlander))));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void QdelOfTheParentDeletesEachComponentOnceInKeyOrderWhileItsListenersStillHear()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            var s = (test_dcs_highlander_sub)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_highlander_sub), "S"));
            var a1 = (test_dcs_allowed)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_allowed), "A1"));
            var a2 = (test_dcs_allowed)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_allowed), "A2"));
            t.Watch(host);
            t.Trace.Clear();
            DmList dc = host._datum_components;
            Assert.Equal(new object[] { typeof(test_dcs_highlander_sub), typeof(test_dcs_highlander), typeof(test_dcs_allowed) }, Dm.each(dc).ToArray());

            qdel(host);

            // datum.dm:146-157: qdel(component, FALSE) for each key's component(s), in key order; S is under two keys
            // but the second read finds it removed (null) already. The listeners of the parent are cleared only after
            // (_clear_signal_refs, :159), so COMSIG_COMPONENT_REMOVING still reaches them.
            Assert.Equal(new[]
            {
                "S Destroy force=0 parent=H",
                "S UnregisterFromParent parent=H",
                "H COMSIG_COMPONENT_REMOVING S parent=H",
                "A1 Destroy force=0 parent=H",
                "A1 UnregisterFromParent parent=H",
                "H COMSIG_COMPONENT_REMOVING A1 parent=H",
                "A2 Destroy force=0 parent=H",
                "A2 UnregisterFromParent parent=H",
                "H COMSIG_COMPONENT_REMOVING A2 parent=H",
            }, t.Trace);
            Assert.All(new datum_component[] { s, a1, a2 }, c => Assert.True(QDELETED(c)));
            Assert.All(new datum_component[] { s, a1, a2 }, c => Assert.Null(c.parent));
            Assert.Null(host._datum_components);
            Assert.Equal(0f, dc.len); // dc.Cut()
            Assert.Null(host._listen_lookup);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AddComponentCrashesForATypeThatIsNoComponentForTheBaseTypeAndOnAQdeletedParent()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");

            // :322-324
            var notComponent = Assert.Throws<DmCrashException>(() => Waiting.NoSleep(host.AddComponent(typeof(datum))));
            Assert.Equal("Attempted to instantiate [/datum] as a component added to parent of type [/datum/test_dcs_host]!", notComponent.Message);
            Assert.Equal("code/datums/components/_component.dm", notComponent.DmFile);
            Assert.Equal(324, notComponent.DmLine);
            var text = Assert.Throws<DmCrashException>(() => Waiting.NoSleep(host.AddComponent("thing")));
            Assert.Equal("Attempted to instantiate [thing] as a component added to parent of type [/datum/test_dcs_host]!", text.Message);

            // :328-329
            var root = Assert.Throws<DmCrashException>(() => Waiting.NoSleep(host.AddComponent(typeof(datum_component))));
            Assert.Equal("/datum/component attempted instantiation!", root.Message);

            // :317-318: QDELING is gc_destroyed != null (DV2), so a qdel'd parent counts too.
            qdel(host);
            var deleted = Assert.Throws<DmCrashException>(() => Waiting.NoSleep(host.AddComponent(typeof(test_dcs_highlander), "A")));
            Assert.Equal("Attempted to add a new component of type [/datum/component/test_dcs_highlander] to a qdeleting parent of type [/datum/test_dcs_host]!", deleted.Message);
            Assert.Empty(t.Trace);
            Assert.Null(host._datum_components);
        }
    }

    [Fact]
    public void AComponentWhoseParentIsDeletedDuringInitializeCrashesNewAndDoesNotJoin()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            t.Watch(host);

            // :58-59. DEVIATION(DV1): in DM the CRASH ends only New, new returns the component and _AddComponent goes on
            // to announce it on the deleted parent and return it (:398-403); in the port the runtime unwinds out of
            // AddComponent.
            var crash = Assert.Throws<DmCrashException>(() => Waiting.NoSleep(host.AddComponent(typeof(test_dcs_parent_killer), "K")));

            Assert.Equal("Component /datum/component/test_dcs_parent_killer was created with a deleted parent or was deleted itself before it could be added to a parent", crash.Message);
            Assert.Equal(59, crash.DmLine);
            Assert.True(QDELETED(host));
            Assert.Equal(new[] { "K Initialize parent=H" }, t.Trace);
            Assert.Null(host._datum_components);
        }
    }

    [Fact]
    public void LoadComponentReturnsTheExistingComponentOrAddsOne()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");

            var made = (test_dcs_highlander)Waiting.NoSleep(host.LoadComponent(typeof(test_dcs_highlander), "A"));
            object again = Waiting.NoSleep(host.LoadComponent(typeof(test_dcs_highlander), "B"));

            Assert.Same(made, again);
            Assert.Equal(new[] { "A Initialize parent=H", "A RegisterWithParent parent=H" }, t.Trace);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ComponentsLiveOnAnyDatumAndTheirUiHostIsTheParent()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            datum plain = Dm.@new<datum>();
            var c = (test_dcs_highlander)Waiting.NoSleep(plain.AddComponent(typeof(test_dcs_highlander), "A"));

            Assert.Same(plain, c.parent);
            Assert.Same(c, plain.GetComponent(typeof(test_dcs_highlander)));
            // :498-499
            Assert.Same(plain, c.ui_host());
            Assert.Empty(t.Runtimes);
        }
    }
}
