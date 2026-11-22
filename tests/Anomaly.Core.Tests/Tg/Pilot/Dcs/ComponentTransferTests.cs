using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Pilot.Dcs;

/// <summary>
/// Moving components between parents, written from code/datums/components/_component.dm: ClearFromParent (:435),
/// TakeComponent (:452), TransferComponents (:480), PreTransfer (:221) and PostTransfer (:231).
/// </summary>
public class ComponentTransferTests
{
    [Fact]
    public void TakeComponentClearsTheOldParentAnnouncesTheComponentAndOnlyThenJoinsTheNewOne()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            test_dcs_host h2 = t.Host("H2");
            var c = (test_dcs_transferable)Waiting.NoSleep(h1.AddComponent(typeof(test_dcs_transferable), "T"));
            t.Watch(h1, h2);
            t.Trace.Clear();

            Waiting.NoSleep(h2.TakeComponent(c));

            // :455-457, :439-442, then AddComponent(target) (:467) announces the component with its parent still null,
            // and :469-470 join it.
            Assert.Equal(new[]
            {
                "T PreTransfer new_parent=H2 parent=H1",
                "T UnregisterFromParent parent=H1",
                "H1 COMSIG_COMPONENT_REMOVING T parent=null",
                "T PostTransfer new_parent=H2 parent=null",
                "H2 COMSIG_COMPONENT_ADDED T parent=null",
                "T RegisterWithParent parent=H2",
            }, t.Trace);
            Assert.Same(h2, c.parent);
            Assert.Null(h1._datum_components);
            Assert.Same(c, h2.GetComponent(typeof(test_dcs_transferable)));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TakeComponentDoesNothingForNoComponentOrOneThatIsAlreadyItsOwn()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            var c = (test_dcs_transferable)Waiting.NoSleep(h1.AddComponent(typeof(test_dcs_transferable), "T"));
            t.Trace.Clear();

            Waiting.NoSleep(h1.TakeComponent(c));
            Waiting.NoSleep(h1.TakeComponent(null));

            Assert.Empty(t.Trace);
            Assert.Same(h1, c.parent);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AComponentThatRefusesTheTransferIsDeleted()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            test_dcs_host h2 = t.Host("H2");
            var c = (test_dcs_transferable)Waiting.NoSleep(h1.AddComponent(typeof(test_dcs_transferable), "T"));
            c.post_transfer_result = COMPONENT_NOTRANSFER;
            t.Watch(h1, h2);
            t.Trace.Clear();

            Waiting.NoSleep(h2.TakeComponent(c));

            // :463-465; the component has no parent any more, so its Destroy just calls the parent (:78-79).
            Assert.Equal(new[]
            {
                "T PreTransfer new_parent=H2 parent=H1",
                "T UnregisterFromParent parent=H1",
                "H1 COMSIG_COMPONENT_REMOVING T parent=null",
                "T PostTransfer new_parent=H2 parent=null",
                "T Destroy force=0 parent=null",
            }, t.Trace);
            Assert.True(QDELETED(c));
            Assert.Null(h1._datum_components);
            Assert.Null(h2._datum_components);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AnIncompatibleTransferDeletesTheComponentAndCrashes()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            test_dcs_host h2 = t.Host("H2");
            var c = (test_dcs_transferable)Waiting.NoSleep(h1.AddComponent(typeof(test_dcs_transferable), "T"));
            c.post_transfer_result = COMPONENT_INCOMPATIBLE;
            t.Trace.Clear();

            var crash = Assert.Throws<DmCrashException>(() => Waiting.NoSleep(h2.TakeComponent(c)));

            Assert.Equal("Incompatible /datum/component/test_dcs_transferable transfer attempt to a /datum/test_dcs_host!", crash.Message);
            Assert.Equal(462, crash.DmLine);
            Assert.Equal("T Destroy force=0 parent=null", t.Trace[^1]);
            Assert.True(QDELETED(c));
            Assert.Null(h2._datum_components);
        }
    }

    [Fact]
    public void TheDefaultPostTransferIsIncompatible()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            var a = (test_dcs_highlander)Waiting.NoSleep(h1.AddComponent(typeof(test_dcs_highlander), "A"));
            Assert.Equal(COMPONENT_INCOMPATIBLE, Waiting.NoSleep(a.PostTransfer(h1)));
            Assert.Equal(FALSE, a.can_transfer);
        }
    }

    [Fact]
    public void ATransferOntoAParentWithAUniqueComponentIsAbsorbedByIt()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            test_dcs_host h2 = t.Host("H2");
            var moving = (test_dcs_transferable_unique)Waiting.NoSleep(h1.AddComponent(typeof(test_dcs_transferable_unique), "U1"));
            var staying = (test_dcs_transferable_unique)Waiting.NoSleep(h2.AddComponent(typeof(test_dcs_transferable_unique), "U2"));
            t.Trace.Clear();

            Waiting.NoSleep(h2.TakeComponent(moving));

            // _AddComponent with the instance (:322-327) in COMPONENT_DUPE_UNIQUE (:346-351): the old component inherits
            // from it and it is qdel'd, so TakeComponent does not join it (:468).
            Assert.Equal(new[]
            {
                "U1 PreTransfer new_parent=H2 parent=H1",
                "U1 UnregisterFromParent parent=H1",
                "U1 PostTransfer new_parent=H2 parent=null",
                "U2 InheritComponent C=U1 i_am_original=1",
                "U1 Destroy force=0 parent=null",
            }, t.Trace);
            Assert.True(QDELETED(moving));
            Assert.Null(moving.parent);
            Assert.Same(staying, h2.GetComponent(typeof(test_dcs_transferable_unique)));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TransferComponentsMovesOnlyTheTransferableComponents()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            test_dcs_host h2 = t.Host("H2");
            var tr = (test_dcs_transferable)Waiting.NoSleep(h1.AddComponent(typeof(test_dcs_transferable), "T"));
            var a = (test_dcs_highlander)Waiting.NoSleep(h1.AddComponent(typeof(test_dcs_highlander), "A"));
            var u = (test_dcs_transferable_unique)Waiting.NoSleep(h1.AddComponent(typeof(test_dcs_transferable_unique), "U"));
            t.Trace.Clear();

            Waiting.NoSleep(h1.TransferComponents(h2));

            Assert.Equal(new[]
            {
                "T PreTransfer new_parent=H2 parent=H1",
                "T UnregisterFromParent parent=H1",
                "T PostTransfer new_parent=H2 parent=null",
                "T RegisterWithParent parent=H2",
                "U PreTransfer new_parent=H2 parent=H1",
                "U UnregisterFromParent parent=H1",
                "U PostTransfer new_parent=H2 parent=null",
                "U RegisterWithParent parent=H2",
            }, t.Trace);
            Assert.Same(h2, tr.parent);
            Assert.Same(h2, u.parent);
            Assert.Same(h1, a.parent);
            Assert.Equal(new object[] { typeof(test_dcs_highlander) }, Dm.each(h1._datum_components).ToArray());
            Assert.Equal(new object[] { typeof(test_dcs_transferable), typeof(test_dcs_transferable_unique) }, Dm.each(h2._datum_components).ToArray());

            // A parent without components has nothing to transfer (:482-483).
            t.Trace.Clear();
            test_dcs_host empty = t.Host("E");
            Waiting.NoSleep(empty.TransferComponents(h1));
            Assert.Empty(t.Trace);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AnIncompatibleTransferEndsTransferComponentsInThePortWhereDmGoesOnWithTheNextComponent()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            test_dcs_host h2 = t.Host("H2");
            var bad = (test_dcs_transferable)Waiting.NoSleep(h1.AddComponent(typeof(test_dcs_transferable), "T"));
            bad.post_transfer_result = COMPONENT_INCOMPATIBLE;
            var u = (test_dcs_transferable_unique)Waiting.NoSleep(h1.AddComponent(typeof(test_dcs_transferable_unique), "U"));

            // DEVIATION(DV1): TakeComponent's CRASH (:462) ends only TakeComponent in DM, and TransferComponents' loop
            // (:484-493) moves U next; in the port the runtime unwinds through TransferComponents to its caller.
            Assert.Throws<DmCrashException>(() => Waiting.NoSleep(h1.TransferComponents(h2)));

            Assert.True(QDELETED(bad));
            Assert.Same(h1, u.parent);
            Assert.Null(h2._datum_components);
        }
    }

    [Fact]
    public void ATransferOntoAParentWithAUniquePassargsComponentIsInheritedAndDeleted()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            test_dcs_host h2 = t.Host("H2");
            var moving = (datum_component)Waiting.NoSleep(h1.AddComponent(typeof(test_dcs_transferable_passargs), "P1"));
            var staying = Waiting.NoSleep(h2.AddComponent(typeof(test_dcs_transferable_passargs), "P2"));
            t.Trace.Clear();

            Waiting.NoSleep(h2.TakeComponent(moving));

            // :365-367: with an instance, COMPONENT_DUPE_UNIQUE_PASSARGS acts like COMPONENT_DUPE_UNIQUE.
            Assert.Equal(new[]
            {
                "P1 PreTransfer new_parent=H2 parent=H1",
                "P1 UnregisterFromParent parent=H1",
                "P1 PostTransfer new_parent=H2 parent=null",
                "P2 InheritComponent C=P1 i_am_original=1",
                "P1 Destroy force=0 parent=null",
            }, t.Trace);
            Assert.True(QDELETED(moving));
            Assert.Same(staying, h2.GetComponent(typeof(test_dcs_transferable_passargs)));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void TgQuirkATransferredSelectiveComponentIsNeverCheckedAgainstTheNewParentsOnes()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            test_dcs_host h2 = t.Host("H2");
            var moving = (datum_component)Waiting.NoSleep(h1.AddComponent(typeof(test_dcs_transferable_selective), "M"));
            var staying = Waiting.NoSleep(h2.AddComponent(typeof(test_dcs_transferable_selective), "S"));
            t.Trace.Clear();

            Waiting.NoSleep(h2.TakeComponent(moving));

            // :387: GetComponents(original_type) with original_type the instance (raw_args[1], read before :340) looks the
            // instance itself up as a key and finds nothing, so S, which absorbs everything, is not asked.
            Assert.DoesNotContain(t.Trace, line => line.Contains("CheckDupeComponent"));
            Assert.Equal(new object[] { staying, moving }, Dm.each(h2.GetComponents(typeof(test_dcs_transferable_selective))).ToArray());
            Assert.Same(h2, moving.parent);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void AListeningComponentTakesItsSignalRegistrationsAlongToTheNewParent()
    {
        var t = new DcsTestWorld();
        using (t.World.Enter())
        {
            test_dcs_host h1 = t.Host("H1");
            test_dcs_host h2 = t.Host("H2");
            var c = (datum_component)Waiting.NoSleep(h1.AddComponent(typeof(test_dcs_listening), "L"));
            DcsSignal(h1);
            Waiting.NoSleep(h1.TransferComponents(h2));
            DcsSignal(h1);
            DcsSignal(h2);

            Assert.Equal(new[] { "L heard ping from H1", "L heard ping from H2" }, t.Trace.Where(line => line.Contains("heard")));
            Assert.Null(h1._listen_lookup);
            Assert.Same(c, h2._listen_lookup[test_dcs_listening.COMSIG_TEST_DCS_PING]);

            // The parent's qdel deletes the component, whose UnregisterFromParent drops the registration.
            qdel(h2);
            Assert.Null(c._signal_procs?[h2]);
            Assert.Empty(t.Runtimes);
        }
    }

    // SEND_SIGNAL(target, COMSIG_TEST_DCS_PING) as a statement (rules 11.1).
    private static void DcsSignal(datum target)
    {
        if (Dm.truthy(target._listen_lookup?[test_dcs_listening.COMSIG_TEST_DCS_PING]))
            target._SendSignal(test_dcs_listening.COMSIG_TEST_DCS_PING, Dm.list(target));
    }
}
