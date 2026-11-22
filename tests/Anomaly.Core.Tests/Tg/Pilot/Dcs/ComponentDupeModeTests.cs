using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Dcs;

/// <summary>
/// What /datum/proc/_AddComponent (code/datums/components/_component.dm:313-405) does for each COMPONENT_DUPE_* mode
/// (__DEFINES/dcs/declarations.dm:41-56) when the parent already has a component of the type, in tg's order of events.
/// The sources mode is in <see cref="ComponentSourceTests"/>.
/// </summary>
public class ComponentDupeModeTests
{
    [Fact]
    public void TheDupeModeIsReadFromTheTypeAndHighlanderIsTheDefault()
    {
        // var/dupe_mode = initial(component_type.dupe_mode) (:331); /datum/component/var/dupe_mode = COMPONENT_DUPE_HIGHLANDER (:18)
        Assert.Equal(COMPONENT_DUPE_HIGHLANDER, Dm.initial(typeof(datum_component), (datum_component p) => p.dupe_mode));
        Assert.Equal(COMPONENT_DUPE_HIGHLANDER, Dm.initial(typeof(test_dcs_highlander), (datum_component p) => p.dupe_mode));
        Assert.Equal(COMPONENT_DUPE_ALLOWED, Dm.initial(typeof(test_dcs_allowed_sub), (datum_component p) => p.dupe_mode));
    }

    [Fact]
    public void AHighlanderComponentJoinsInheritsFromTheOldOneAndReplacesIt()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            t.Watch(host);
            var a = (test_dcs_highlander)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_highlander), "A"));
            Assert.Equal(new[]
            {
                "A Initialize parent=H",
                "A RegisterWithParent parent=H",
                "H COMSIG_COMPONENT_ADDED A parent=H",
            }, t.Trace);
            t.Trace.Clear();

            var b = (test_dcs_highlander)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_highlander), "B"));

            // :353-358: the new component is created (New joins it to the parent), inherits from the old one, then the
            // old one is qdel'd (QDEL_NULL nulls old_component, so :398 sends COMSIG_COMPONENT_ADDED for the new one).
            Assert.Equal(new[]
            {
                "B Initialize parent=H",
                "B RegisterWithParent parent=H",
                "B InheritComponent C=A i_am_original=0",
                "A Destroy force=0 parent=H",
                "A UnregisterFromParent parent=H",
                "H COMSIG_COMPONENT_REMOVING A parent=H",
                "H COMSIG_COMPONENT_ADDED B parent=H",
            }, t.Trace);
            Assert.True(QDELETED(a));
            Assert.Null(a.parent);
            Assert.Same(host, b.parent);
            Assert.Same(b, host._datum_components[typeof(test_dcs_highlander)]);
            Assert.Equal(1f, host._datum_components.len);
            Assert.Same(b, host.GetComponent(typeof(test_dcs_highlander)));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AHighlanderComponentThatIsIncompatibleLeavesTheOldOneInPlace()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            var a = (test_dcs_highlander)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_highlander), "A"));
            t.Watch(host);
            t.Trace.Clear();

            object result = Waiting.NoSleep(host.AddComponent(typeof(test_dcs_highlander), "B", COMPONENT_INCOMPATIBLE));

            // :356: QDELETED(new_component), so nothing is inherited and the old component is returned (:405).
            Assert.Same(a, result);
            Assert.Equal(new[]
            {
                "B Initialize parent=H returns 1",
                "B Destroy force=1 parent=H",
                "H COMSIG_COMPONENT_REMOVING B parent=H",
            }, t.Trace);
            Assert.Same(a, host.GetComponent(typeof(test_dcs_highlander)));
            Assert.False(QDELETED(a));
            Assert.Single(t.Runtimes); // the stack_trace of :50
        }
    }

    [Fact]
    public void AUniqueComponentKeepsTheOldOneWhichInheritsFromTheNewOneBeforeItIsDeleted()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            var a = (test_dcs_unique)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_unique), "A"));
            t.Watch(host);
            t.Trace.Clear();

            object result = Waiting.NoSleep(host.AddComponent(typeof(test_dcs_unique), "B"));

            // :346-351: the new component is still created and joins the parent before the old one inherits from it and
            // it is qdel'd; the old component is returned and COMSIG_COMPONENT_ADDED is not sent.
            Assert.Same(a, result);
            Assert.Equal(new[]
            {
                "B Initialize parent=H",
                "B RegisterWithParent parent=H",
                "A InheritComponent C=B i_am_original=1",
                "B Destroy force=0 parent=H",
                "B UnregisterFromParent parent=H",
                "H COMSIG_COMPONENT_REMOVING B parent=H",
            }, t.Trace);
            Assert.Same(a, host._datum_components[typeof(test_dcs_unique)]);
            Assert.Equal(1f, host._datum_components.len);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AUniquePassargsComponentHandsTheArgumentsToTheOldOneWithoutCreatingANewOne()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            var a = (test_dcs_unique_passargs)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_unique_passargs), "A"));
            t.Watch(host);
            t.Trace.Clear();

            // :360-364: arguments = raw_args.Copy(2); arguments.Insert(1, null, TRUE); old_component.InheritComponent(arglist(arguments)).
            // The named argument stays named through the list and arglist.
            object result = Waiting.NoSleep(host.AddComponent(typeof(test_dcs_unique_passargs), "B", Dm.named("initialize_result", 7)));

            Assert.Same(a, result);
            // No "B Initialize": no component was created.
            Assert.Equal(new[] { "A InheritComponent C=null i_am_original=1 args=B,7" }, t.Trace);
            Assert.Same(a, host._datum_components[typeof(test_dcs_unique_passargs)]);
            Assert.Equal(1f, host._datum_components.len);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AnAllowedComponentIsAddedNextToTheOldOneAndBothAreKeptInOrder()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            t.Watch(host);
            var a = (test_dcs_allowed)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_allowed), "A"));
            var b = (test_dcs_allowed)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_allowed), "B"));

            // :395-396: "Dupes are allowed, act like normal"; _JoinParent turns the single entry into list(A, B) (:102-116).
            Assert.Equal(new[]
            {
                "A Initialize parent=H",
                "A RegisterWithParent parent=H",
                "H COMSIG_COMPONENT_ADDED A parent=H",
                "B Initialize parent=H",
                "B RegisterWithParent parent=H",
                "H COMSIG_COMPONENT_ADDED B parent=H",
            }, t.Trace);
            var entry = (DmList)host._datum_components[typeof(test_dcs_allowed)];
            Assert.Equal(new object[] { a, b }, Dm.each(entry).ToArray());
            Assert.Same(entry, host.GetComponents(typeof(test_dcs_allowed)));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void ASelectiveComponentAsksEachExistingOneInOrderAndIsDroppedWhenOneAbsorbsIt()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host host = t.Host("H");
            t.Watch(host);
            var x = (test_dcs_selective)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_selective), "x"));
            t.Trace.Clear();

            // :383-393: arguments = raw_args.Copy() with arguments[1] = new_component (null: the type was given).
            var y = (test_dcs_selective)Waiting.NoSleep(host.AddComponent(typeof(test_dcs_selective), "y"));
            Assert.Equal(new[]
            {
                "x CheckDupeComponent C=null args=y",
                "y Initialize parent=H",
                "y RegisterWithParent parent=H",
                "H COMSIG_COMPONENT_ADDED y parent=H",
            }, t.Trace);
            t.Trace.Clear();

            // The second existing component absorbs it: the loop breaks there and no component is created.
            object absorbed = Waiting.NoSleep(host.AddComponent(typeof(test_dcs_selective), "y"));
            Assert.Equal(new[]
            {
                "x CheckDupeComponent C=null args=y",
                "y CheckDupeComponent C=null args=y",
            }, t.Trace);
            // old_component is never set in this mode and new_component stays null, so :405 returns null.
            Assert.Null(absorbed);
            Assert.Equal(new object[] { x, y }, Dm.each(host.GetComponents(typeof(test_dcs_selective))).ToArray());
            t.Trace.Clear();

            // The first one absorbs it: the second is not asked.
            Assert.Null(Waiting.NoSleep(host.AddComponent(typeof(test_dcs_selective), "x")));
            Assert.Equal(new[] { "x CheckDupeComponent C=null args=x" }, t.Trace);
            Assert.Empty(t.Runtimes);
        }
    }
}
