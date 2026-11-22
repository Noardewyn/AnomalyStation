using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Pilot.Datum;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Dcs;

/// <summary>
/// COMPONENT_DUPE_SOURCES (__DEFINES/dcs/declarations.dm:52), written from code/datums/components/_component.dm:
/// AddComponentFrom (__DEFINES/dcs/helpers.dm:23) into _AddComponent (:313, :332-336, :369-378, :398-401),
/// on_source_add (:174), on_source_remove (:184), RemoveComponentSource (:410) and allow_source_update (:502).
/// </summary>
public class ComponentSourceTests
{
    [Fact]
    public void TheFirstSourceCreatesTheComponentAndLaterSourcesAreAddedToIt()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            test_dcs_host s1 = t.Host("s1");
            test_dcs_host s2 = t.Host("s2");
            t.Watch(host);

            var c = (test_dcs_sources)Waiting.NoSleep(host.AddComponentFrom(s1, typeof(test_dcs_sources), "C"));
            // :398-403: a new component gets its first source after it joined, then COMSIG_COMPONENT_ADDED.
            Assert.Equal(new[]
            {
                "C Initialize parent=H",
                "C RegisterWithParent parent=H",
                "C on_source_add s1 args=C sources=s1",
                "H COMSIG_COMPONENT_ADDED C parent=H",
            }, t.Trace);
            t.Trace.Clear();

            // :369-378: an existing component gets on_source_add(arglist(list(source) + raw_args.Copy(2))) and is
            // returned; nothing is created or announced.
            object again = Waiting.NoSleep(host.AddComponentFrom(s2, typeof(test_dcs_sources), "X"));
            Assert.Same(c, again);
            Assert.Equal(new[] { "C on_source_add s2 args=X sources=s1,s2" }, t.Trace);
            t.Trace.Clear();

            // :370-371: a source that is already registered is not added again ("no work to do").
            Assert.Same(c, Waiting.NoSleep(host.AddComponentFrom(s1, typeof(test_dcs_sources), "Y")));
            Assert.Empty(t.Trace);

            // Unless allow_source_update says so; LAZYOR keeps the source once (:178).
            c.allow_update = TRUE;
            Assert.Same(c, Waiting.NoSleep(host.AddComponentFrom(s1, typeof(test_dcs_sources), "Z")));
            Assert.Equal(new[] { "C on_source_add s1 args=Z sources=s1,s2" }, t.Trace);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void RemovingTheLastSourceDeletesTheComponent()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            test_dcs_host s1 = t.Host("s1");
            test_dcs_host s2 = t.Host("s2");
            var c = (test_dcs_sources)Waiting.NoSleep(host.AddComponentFrom(s1, typeof(test_dcs_sources), "C"));
            Waiting.NoSleep(host.AddComponentFrom(s2, typeof(test_dcs_sources), "C"));
            t.Watch(host);
            t.Trace.Clear();

            // RemoveComponentSource(source, component_type) with a typepath looks the exact component up (:411-412).
            host.RemoveComponentSource(s1, typeof(test_dcs_sources));
            Assert.Equal(new[] { "C on_source_remove s1" }, t.Trace);
            Assert.Equal("s2", c.Sources());
            Assert.False(QDELETED(c));
            t.Trace.Clear();

            // With the component itself; the last source gone, on_source_remove qdels it (:189-190).
            host.RemoveComponentSource(s2, c);
            Assert.Equal(new[]
            {
                "C on_source_remove s2",
                "C Destroy force=0 parent=H",
                "C UnregisterFromParent parent=H",
                "H COMSIG_COMPONENT_REMOVING C parent=H",
            }, t.Trace);
            Assert.True(QDELETED(c));
            Assert.Null(c.sources);
            Assert.Null(host._datum_components);
            t.Trace.Clear();

            // :413-414: nothing to remove from.
            host.RemoveComponentSource(s2, typeof(test_dcs_sources));
            host.RemoveComponentSource(s2, null);
            Assert.Empty(t.Trace);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AnIncompatibleOrRedundantSourceForAnExistingComponentGivesNull()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            test_dcs_host s1 = t.Host("s1");
            test_dcs_host s2 = t.Host("s2");
            var c = (test_dcs_sources)Waiting.NoSleep(host.AddComponentFrom(s1, typeof(test_dcs_sources), "C"));

            // :373-378
            Assert.Null(Waiting.NoSleep(host.AddComponentFrom(s2, typeof(test_dcs_sources), "C", null, COMPONENT_INCOMPATIBLE)));
            Assert.Equal(new[]
            {
                "DmCrashException: " + DatumTestMacros.StackTraceText("incompatible source added to a /datum/component/test_dcs_sources. Args: [\"/datum/test_dcs_host\",\"C\",null,1]", "code/datums/components/_component.dm", 375),
            }, t.Messages);
            test_dcs_host s3 = t.Host("s3");
            Assert.Null(Waiting.NoSleep(host.AddComponentFrom(s3, typeof(test_dcs_sources), "C", null, COMPONENT_REDUNDANT)));
            Assert.Single(t.Runtimes);

            // The fixture's on_source_add called the parent first (SHOULD_CALL_PARENT), so the sources were recorded
            // anyway: tg leaves undoing it to the override that rejects them. A rejected source that was recorded is
            // then "already registered" (:370-371).
            Assert.Equal("s1,s2,s3", c.Sources());
            Assert.Same(c, Waiting.NoSleep(host.AddComponentFrom(s2, typeof(test_dcs_sources), "C", null, COMPONENT_INCOMPATIBLE)));
            Assert.Single(t.Runtimes);
            Assert.Same(c, host.GetComponent(typeof(test_dcs_sources)));
        }
    }

    [Fact]
    public void ANewComponentWhoseFirstSourceIsIncompatibleStaysAttachedButIsNotAnnouncedOrReturned()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            test_dcs_host s1 = t.Host("s1");
            t.Watch(host);

            // :399-401: the stack_trace and return null come after the component joined its parent, and nothing undoes
            // that; COMSIG_COMPONENT_ADDED is not sent.
            object result = Waiting.NoSleep(host.AddComponentFrom(s1, typeof(test_dcs_sources), "C", null, COMPONENT_INCOMPATIBLE));

            Assert.Null(result);
            Assert.Equal(new[]
            {
                "C Initialize parent=H",
                "C RegisterWithParent parent=H",
                "C on_source_add s1 args=C,null,1 sources=s1",
            }, t.Trace);
            var c = (test_dcs_sources)host.GetComponent(typeof(test_dcs_sources));
            Assert.NotNull(c);
            Assert.False(QDELETED(c));
            Assert.Equal(new[]
            {
                "DmCrashException: " + DatumTestMacros.StackTraceText("incompatible source added to a /datum/component/test_dcs_sources. Args: [\"/datum/test_dcs_host\",\"C\",null,1]", "code/datums/components/_component.dm", 400),
            }, t.Messages);
        }
    }

    [Fact]
    public void SourcesAreRequiredForASourcesComponentAndRefusedForAnyOther()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            test_dcs_host s1 = t.Host("s1");

            // :333-336
            var noSource = Assert.Throws<DmCrashException>(() => Waiting.NoSleep(host.AddComponent(typeof(test_dcs_sources), "C")));
            Assert.Equal("Attempted to add a sourced component of type '/datum/component/test_dcs_sources' to '/datum/test_dcs_host' without a source!", noSource.Message);
            var withSource = Assert.Throws<DmCrashException>(() => Waiting.NoSleep(host.AddComponentFrom(s1, typeof(test_dcs_highlander), "A")));
            Assert.Equal("Attempted to add a normal component of type '/datum/component/test_dcs_highlander' to '/datum/test_dcs_host' with a source!", withSource.Message);
            Assert.Null(host._datum_components);

            // :186-187: on_source_remove of a component that does not use sources.
            var a = (test_dcs_highlander)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_highlander), "A"));
            var remove = Assert.Throws<DmCrashException>(() => host.RemoveComponentSource(s1, a));
            Assert.Equal("Component '/datum/component/test_dcs_highlander' does not use sources but is trying to remove a source", remove.Message);
            Assert.False(QDELETED(a));

            // :176-177: on_source_add of a component that does not use sources returns COMPONENT_INCOMPATIBLE.
            Assert.Equal(COMPONENT_INCOMPATIBLE, Waiting.NoSleep(a.on_source_add(s1)));
            Assert.Null(a.sources);
            Assert.Empty(t.Runtimes);
        }
    }
}
