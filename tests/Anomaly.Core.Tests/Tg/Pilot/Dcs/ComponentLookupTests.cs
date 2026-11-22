using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Pilot.Datum;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Dcs;

/// <summary>
/// How a parent's <c>_datum_components</c> is keyed over the component type hierarchy, written from
/// code/datums/components/_component.dm: _JoinParent (:89-122), _GetInverseTypeList (:237-244), GetComponent (:255),
/// GetExactComponent (:275) and GetComponents (:296).
/// </summary>
public class ComponentLookupTests
{
    [Fact]
    public void TheInverseTypeListGoesFromTheTypeUpToButNotIncludingDatumComponent()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            var d = (test_dcs_allowed_sub_deeper)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_allowed_sub_deeper), "D"));
            var a = (test_dcs_allowed)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_allowed), "A"));

            Assert.Equal(new object[] { typeof(test_dcs_allowed_sub_deeper), typeof(test_dcs_allowed_sub), typeof(test_dcs_allowed) }, Dm.each(d._GetInverseTypeList()).ToArray());
            // "most components are root level + 1, this won't even have to run" (:241)
            Assert.Equal(new object[] { typeof(test_dcs_allowed) }, Dm.each(a._GetInverseTypeList()).ToArray());
            // The walk starts at src's parent_type (:240), whatever our_type is: an explicit our_type only replaces
            // the first entry (tg always passes the component's own type).
            Assert.Equal(new object[] { typeof(test_dcs_highlander), typeof(test_dcs_allowed_sub), typeof(test_dcs_allowed) }, Dm.each(d._GetInverseTypeList(typeof(test_dcs_highlander))).ToArray());
        }
    }

    [Fact]
    public void ASubtypeIsFoundUnderEveryParentTypeButOnlyItsOwnByExactLookup()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            var s = (test_dcs_highlander_sub)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_highlander_sub), "S"));

            Assert.Equal(new object[] { typeof(test_dcs_highlander_sub), typeof(test_dcs_highlander) }, Dm.each(host._datum_components).ToArray());
            Assert.Same(s, host.GetComponent(typeof(test_dcs_highlander_sub)));
            Assert.Same(s, host.GetComponent(typeof(test_dcs_highlander)));
            Assert.Same(s, host.GetExactComponent(typeof(test_dcs_highlander_sub)));
            Assert.Null(host.GetExactComponent(typeof(test_dcs_highlander)));
            Assert.Equal(new object[] { s }, Dm.each(host.GetComponents(typeof(test_dcs_highlander))).ToArray());
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void LookupsOfAbsentComponentsGiveNullOrAnEmptyList()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            Assert.Null(host.GetComponent(typeof(test_dcs_highlander)));
            Assert.Null(host.GetExactComponent(typeof(test_dcs_highlander)));
            var none = Assert.IsType<DmList>(host.GetComponents(typeof(test_dcs_highlander)));
            Assert.Equal(0f, none.len);

            Waiting.NoSleep(host.AddComponent(typeof(test_dcs_highlander), "A"));
            Assert.Null(host.GetComponent(typeof(test_dcs_unique)));
            Assert.Equal(0f, ((DmList)host.GetComponents(typeof(test_dcs_allowed))).len);
            Assert.Empty(t.Runtimes);

            // The report for a dupe-allowed type comes before the lookup (:257-258), so it is made with no such component too.
            Assert.Null(host.GetComponent(typeof(test_dcs_allowed)));
            Assert.Single(t.Runtimes);
        }
    }

    [Fact]
    public void GetComponentOfATypeThatAllowsDuplicatesReportsAndReturnsTheFirst()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            var a = Waiting.NoSleep(host.AddComponent(typeof(test_dcs_allowed), "A"));
            Waiting.NoSleep(host.AddComponent(typeof(test_dcs_allowed), "B"));

            Assert.Same(a, host.GetComponent(typeof(test_dcs_allowed)));
            Assert.Null(host.GetExactComponent(typeof(test_dcs_allowed))); // a list is stored there, whose type is not the component's (:286)
            string message = "GetComponent was called to get a component of which multiple copies could be on an object. This can easily break and should be changed. Type: [/datum/component/test_dcs_allowed]";
            Assert.Equal(new[]
            {
                "DmCrashException: " + DatumTestMacros.StackTraceText(message, "code/datums/components/_component.dm", 258),
                "DmCrashException: " + DatumTestMacros.StackTraceText(message, "code/datums/components/_component.dm", 279),
            }, t.Messages);
        }
    }

    [Fact]
    public void GetComponentsReturnsTheParentsOwnListOrANewListAroundASingleComponent()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            var single = Waiting.NoSleep(host.AddComponent(typeof(test_dcs_highlander), "A"));
            Waiting.NoSleep(host.AddComponent(typeof(test_dcs_allowed), "B"));
            Waiting.NoSleep(host.AddComponent(typeof(test_dcs_allowed), "C"));

            // :300: return islist(components) ? components : list(components)
            var wrapped = (DmList)host.GetComponents(typeof(test_dcs_highlander));
            Assert.Equal(new object[] { single }, Dm.each(wrapped).ToArray());
            Assert.NotSame(wrapped, host.GetComponents(typeof(test_dcs_highlander)));
            Assert.Same(host._datum_components[typeof(test_dcs_allowed)], host.GetComponents(typeof(test_dcs_allowed)));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ExactMatchesAreKeptInOrderAndAnIndirectMatchGoesToTheBackOfTheLine()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            var p1 = Waiting.NoSleep(host.AddComponent(typeof(test_dcs_allowed), "P1"));
            var p2 = Waiting.NoSleep(host.AddComponent(typeof(test_dcs_allowed), "P2"));
            // :117-118: "indirect match, back of the line with ya"
            var s1 = Waiting.NoSleep(host.AddComponent(typeof(test_dcs_allowed_sub), "S1"));

            Assert.Equal(new[] { p1, p2, s1 }, Dm.each(host._datum_components[typeof(test_dcs_allowed)]).ToArray());
            Assert.Same(s1, host._datum_components[typeof(test_dcs_allowed_sub)]);
            Assert.Empty(t.Runtimes);
        }
    }

    /// <summary>
    /// _component.dm:112 (DV8, docs/ai/DEVIATIONS.md). An exact match that joins a type key whose list holds a component of
    /// a subtype ("exact match, take priority ... but not over other exact matches") is meant to be inserted before that
    /// subtype component, but tg writes <c>components_of_type.Insert(J, I)</c>: it inserts I, the type key, so the
    /// typepath lands in the parent's list, the new component is not in it at all, the next exact match reads
    /// <c>C.type</c> of that typepath (:111) and, once the subtype component is gone, GetComponent returns the bare
    /// typepath. The port inserts src (a registered deviation); this test pins the order that gives.
    /// </summary>
    [Fact]
    public void AnExactMatchJoiningAKeyWithASubtypeComponentGoesBeforeIt()
    {
        // DV8 (docs/ai/DEVIATIONS.md): _component.dm:112 inserts I, the type key, where the joining component belongs (a
        // tg bug that leaves a typepath in the list); the port inserts src, so an exact match takes priority over the
        // subtype component "but not over other exact matches" (:106-116), as the surrounding code intends.
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            var s1 = (test_dcs_allowed)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_allowed_sub), "S1"));
            var p1 = (test_dcs_allowed)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_allowed), "P1"));

            Assert.Same(host, p1.parent);
            Assert.Contains("P1 RegisterWithParent parent=H", t.Trace);
            Assert.Equal(new object[] { p1, s1 }, Dm.each(host._datum_components[typeof(test_dcs_allowed)]).ToArray());
            Assert.Same(s1, host._datum_components[typeof(test_dcs_allowed_sub)]);
            Assert.Equal(new object[] { p1, s1 }, Dm.each(host.GetComponents(typeof(test_dcs_allowed))).ToArray());

            // The next exact match goes after P1 (another exact match) and before S1.
            var p2 = (test_dcs_allowed)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_allowed), "P2"));
            Assert.Contains("P2 RegisterWithParent parent=H", t.Trace);
            Assert.Equal(new object[] { p1, p2, s1 }, Dm.each(host._datum_components[typeof(test_dcs_allowed)]).ToArray());

            // Removing the subtype component drops its own key and leaves the two exact matches (:131-139).
            qdel(s1);
            Assert.Equal(new object[] { typeof(test_dcs_allowed) }, Dm.each(host._datum_components).ToArray());
            Assert.Equal(new object[] { p1, p2 }, Dm.each(host._datum_components[typeof(test_dcs_allowed)]).ToArray());

            // One left: the key holds it alone ("make him special", :136-137); then the last one drops the list (:141-142).
            qdel(p1);
            Assert.Same(p2, host._datum_components[typeof(test_dcs_allowed)]);
            qdel(p2);
            Assert.Null(host._datum_components);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void WithAHighlanderParentTypeTheNewComponentReplacesTheSubtypeOne()
    {
        // DV8, as above: the new component is inserted itself, so after the highlander path qdels the subtype
        // component the new one is the parent's component of that type and is deleted with the parent.
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            var s = (test_dcs_highlander)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_highlander_sub), "S"));

            // GetComponent(/datum/component/test_dcs_highlander) finds S, so the highlander path runs (:353-358): the new
            // component joins before S, inherits from S, and S is qdel'd.
            var p = (test_dcs_highlander)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_highlander), "P"));

            Assert.True(QDELETED(s));
            Assert.Same(host, p.parent);
            Assert.Contains("P InheritComponent C=S i_am_original=0", t.Trace);
            Assert.Equal(new object[] { typeof(test_dcs_highlander) }, Dm.each(host._datum_components).ToArray());
            Assert.Same(p, host._datum_components[typeof(test_dcs_highlander)]);
            Assert.Same(p, host.GetComponent(typeof(test_dcs_highlander)));
            Assert.Same(p, host.GetExactComponent(typeof(test_dcs_highlander)));
            Assert.Empty(t.Runtimes);

            // The parent's Destroy (datum.dm:154-156) qdels its components: P goes with it.
            qdel(host);
            Assert.True(QDELETED(p));
            Assert.Null(p.parent);
            Assert.Empty(t.Runtimes);
        }
    }
}
