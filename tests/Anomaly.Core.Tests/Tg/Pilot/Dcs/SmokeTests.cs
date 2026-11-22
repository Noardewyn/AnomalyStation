using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Dcs;

/// <summary>
/// Compile-and-run smoke tests of the dcs package (wave 2 pilot): components, elements and SSdcs run end to end in a
/// world. The behaviour tests written from the DM code and the ported tg unit tests belong to the test-writer.
/// </summary>
public class SmokeTests
{
    private static World NewWorld(out List<Exception> runtimes)
    {
        var errors = new List<Exception>();
        runtimes = errors;
        var w = new World(7) { RuntimeErrorSink = errors.Add };
        using (w.Enter())
        {
            Dm.@new<datum_controller_subsystem_garbage>();
            Dm.@new<datum_controller_subsystem_processing_dcs>();
        }
        return w;
    }

    [Fact]
    public void AddComponentInitializesJoinsTheParentAndSendsComponentAdded()
    {
        World w = NewWorld(out List<Exception> runtimes);
        using (w.Enter())
        {
            var log = new List<string>();
            datum parent = Dm.@new<datum>();
            test_dcs_smoke_watcher watcher = Dm.@new<test_dcs_smoke_watcher>();
            watcher.log = log;
            watcher.RegisterSignal(parent, COMSIG_COMPONENT_ADDED, nameof(test_dcs_smoke_watcher.on_added));

            var c = (test_dcs_smoke_sub)Waiting.NoSleep(parent.AddComponent(typeof(test_dcs_smoke_sub), "a", Dm.named("b", 2)));

            Assert.Same(parent, c.parent);
            Assert.Equal("a", c.first_arg);
            Assert.True(Dm.eq(c.named_b, 2));
            // _GetInverseTypeList: the component is registered under its type and every parent type below /datum/component.
            Assert.Same(c, parent._datum_components[typeof(test_dcs_smoke_sub)]);
            Assert.Same(c, parent._datum_components[typeof(test_dcs_smoke)]);
            Assert.Equal(2f, parent._datum_components.len);
            Assert.Same(c, parent.GetComponent(typeof(test_dcs_smoke)));
            Assert.Same(c, parent.GetExactComponent(typeof(test_dcs_smoke_sub)));
            Assert.Null(parent.GetExactComponent(typeof(test_dcs_smoke)));
            // RegisterWithParent (_component.dm:122) runs before COMSIG_COMPONENT_ADDED is sent (:402).
            Assert.Equal(new[] { "added #" + c.dm_id + " registered=1" }, log);
            Assert.Empty(runtimes);
        }
    }

    [Fact]
    public void AHighlanderComponentReplacesTheOldOneAndQdelOfTheParentDestroysTheNewOne()
    {
        World w = NewWorld(out List<Exception> runtimes);
        using (w.Enter())
        {
            datum parent = Dm.@new<datum>();
            var first = (test_dcs_smoke)Waiting.NoSleep(parent.AddComponent(typeof(test_dcs_smoke)));
            var second = (test_dcs_smoke)Waiting.NoSleep(parent.AddComponent(typeof(test_dcs_smoke)));

            Assert.NotSame(first, second);
            Assert.True(QDELETED(first));
            Assert.Null(first.parent);
            Assert.Equal("inherit #" + first.dm_id + " 0", second.inherited);
            Assert.Same(second, parent.GetComponent(typeof(test_dcs_smoke)));

            qdel(parent);
            Assert.True(QDELETED(second));
            Assert.Null(second.parent);
            Assert.Null(parent._datum_components);
            Assert.Equal(1f, second.unregistered);
            Assert.Empty(runtimes);
        }
    }

    [Fact]
    public void AnIncompatibleComponentIsDeletedReportedAndNotAdded()
    {
        World w = NewWorld(out List<Exception> runtimes);
        using (w.Enter())
        {
            datum parent = Dm.@new<datum>();
            object result = Waiting.NoSleep(parent.AddComponent(typeof(test_dcs_smoke), "incompatible"));

            Assert.Null(result);
            Assert.Null(parent._datum_components);
            Assert.Single(runtimes); // the stack_trace of _component.dm:50
            Assert.Contains("Incompatible /datum/component/test_dcs_smoke assigned to a /datum!", runtimes[0].Message);
        }
    }

    [Fact]
    public void AnElementIsOneSingletonPerTypeAttachedAndDetachedWithItsHost()
    {
        World w = NewWorld(out List<Exception> runtimes);
        using (w.Enter())
        {
            datum a = Dm.@new<datum>();
            datum b = Dm.@new<datum>();
            Waiting.NoSleep(a.AddElement(typeof(test_dcs_smoke_element), 1));
            Waiting.NoSleep(b.AddElement(typeof(test_dcs_smoke_element), 2));

            var ele = (test_dcs_smoke_element)SSdcs.elements_by_type[typeof(test_dcs_smoke_element)];
            Assert.Equal(1f, SSdcs.elements_by_type.len);
            Assert.Equal(new[] { "attach #" + a.dm_id + " 1", "attach #" + b.dm_id + " 2" }, ele.log);

            b.RemoveElement(typeof(test_dcs_smoke_element));
            // ELEMENT_DETACH_ON_HOST_DESTROY: qdel of the host detaches through COMSIG_QDELETING.
            qdel(a);
            Assert.Equal(new[] { "detach #" + b.dm_id, "detach #" + a.dm_id }, ele.log.Skip(2));
            Assert.Null(a._listen_lookup);
            Assert.Empty(runtimes);
        }
    }

    [Fact]
    public void BespokeElementsAreKeyedByTheirHashedArguments()
    {
        World w = NewWorld(out List<Exception> runtimes);
        using (w.Enter())
        {
            datum host = Dm.@new<datum>();
            Waiting.NoSleep(host.AddElement(typeof(test_dcs_smoke_bespoke), 1, "x"));
            Waiting.NoSleep(host.AddElement(typeof(test_dcs_smoke_bespoke), 1, "x"));
            Waiting.NoSleep(host.AddElement(typeof(test_dcs_smoke_bespoke), 2, "x"));

            Assert.Equal(2f, SSdcs.elements_by_type.len);
            // list2params of list(type, 1, "x"): the path is URL-encoded like every list2params value.
            Assert.Equal("%2fdatum%2felement%2ftest_dcs_smoke_bespoke&1&x", SSdcs.GetIdFromArguments(Dm.list(typeof(test_dcs_smoke_bespoke), 1, "x")));
            Assert.NotNull(SSdcs.elements_by_type["%2fdatum%2felement%2ftest_dcs_smoke_bespoke&1&x"]);
            Assert.NotNull(SSdcs.elements_by_type["%2fdatum%2felement%2ftest_dcs_smoke_bespoke&2&x"]);
            Assert.Empty(runtimes);
        }
    }

    [Fact]
    public void AddElementTraitAttachesOnceAndDetachesWhenTheTraitIsRemoved()
    {
        World w = NewWorld(out List<Exception> runtimes);
        using (w.Enter())
        {
            datum host = Dm.@new<datum>();
            Waiting.NoSleep(host.AddElementTrait("smoke_trait", "source_a", typeof(test_dcs_smoke_element), 5));
            Waiting.NoSleep(host.AddElementTrait("smoke_trait", "source_b", typeof(test_dcs_smoke_element), 6));

            var ele = (test_dcs_smoke_element)SSdcs.elements_by_type[typeof(test_dcs_smoke_element)];
            Assert.Equal(new[] { "attach #" + host.dm_id + " 5" }, ele.log);
            Assert.Equal(2f, ((DmList)host._status_traits["smoke_trait"]).len);

            // The trait's removal signal (SIGNAL_REMOVETRAIT, sent by REMOVE_TRAIT) detaches the element.
            host._SendSignal("removetrait smoke_trait", Dm.list(host, "smoke_trait"));
            Assert.Equal("detach #" + host.dm_id, ele.log[^1]);
            Assert.Empty(runtimes);
        }
    }
}

/// <summary>A component that records what tg's component procs hand it.</summary>
[DmPath("/datum/component/test_dcs_smoke")]
public class test_dcs_smoke : datum_component
{
    public object first_arg;
    public object named_b;
    public string inherited;
    public float registered;
    public float unregistered;

    [DmParams("first", "b")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        first_arg = Dm.arg(__extra, 0);
        named_b = Dm.arg(__extra, 1);
        if (Dm.eq(first_arg, "incompatible"))
            return COMPONENT_INCOMPATIBLE;
        return null;
    }

    public override async DmTask RegisterWithParent()
    {
        registered++;
    }

    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        unregistered++;
    }

    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        inherited = "inherit #" + C?.dm_id + " " + Dm.str(i_am_original);
    }
}

/// <summary>A subtype, registered under both component types.</summary>
[DmPath("/datum/component/test_dcs_smoke/sub")]
public class test_dcs_smoke_sub : test_dcs_smoke
{
}

/// <summary>A listener of COMSIG_COMPONENT_ADDED.</summary>
[DmPath("/datum/test_dcs_smoke_watcher")]
public class test_dcs_smoke_watcher : datum
{
    [NotHashed]
    public List<string> log;

    [SignalHandler]
    public virtual void on_added(datum source = null, datum_component added = null)
    {
        log.Add("added #" + added.dm_id + " registered=" + Dm.str(((test_dcs_smoke)added).registered));
    }
}

/// <summary>An element that detaches when its host is deleted and records its attachments.</summary>
[DmPath("/datum/element/test_dcs_smoke_element")]
public class test_dcs_smoke_element : datum_element
{
    [NotHashed]
    public List<string> log = new();

    public test_dcs_smoke_element()
    {
        element_flags = ELEMENT_DETACH_ON_HOST_DESTROY;
    }

    [DmParams("target", "value")]
    public override async DmTask<object> Attach(datum target = null, params object[] __extra)
    {
        object dot = await base.Attach(target, __extra);
        log.Add("attach #" + target.dm_id + " " + Dm.str(Dm.arg(__extra, 0)));
        return dot;
    }

    public override object Detach(datum source = null, params object[] __extra)
    {
        log.Add("detach #" + source.dm_id);
        return base.Detach(source, __extra);
    }
}

/// <summary>A bespoke element: one instance per distinct argument list from the second argument on.</summary>
[DmPath("/datum/element/test_dcs_smoke_bespoke")]
public class test_dcs_smoke_bespoke : datum_element
{
    public test_dcs_smoke_bespoke()
    {
        element_flags = ELEMENT_BESPOKE;
        argument_hash_start_idx = 2;
    }
}
