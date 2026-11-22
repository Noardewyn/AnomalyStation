using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.TurfArea;

/// <summary>
/// /atom/update_appearance and its family (game/atom/atom_appearance.dm) and the overlay procs of SSoverlays
/// (controllers/subsystem/overlays.dm), which package B owns as shared callees: the order of the update procs, the
/// signals that mask them, the overlay diffing of update_icon, and the overlay list edits. Overlays made of texts, icons
/// or appearances need BYOND's appearance var, which is a SHIM-GAP (M3), so those cases are not tested here.
/// </summary>
public class AtomAppearanceTests
{
    private static List<string> Lines(TurfAreaWorld t) => TurfAreaTrace.Of(t.World).Lines;

    private static obj_test_turfarea_appearance Thing(TurfAreaWorld t)
    {
        var thing = Dm.@new<obj_test_turfarea_appearance>();
        thing.name = "thing";
        return thing;
    }

    private static datum_test_turfarea_listener Listener(string label, object returns = null)
    {
        var l = Dm.@new<datum_test_turfarea_listener>();
        l.label = label;
        if (returns != null)
            l.returns = returns;
        return l;
    }

    // ---- update_appearance ----

    [Fact]
    public void UpdateAppearanceDefaultsToALLAndRunsNameDescIconStateAndOverlaysInOrder()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var thing = Thing(t);
            object result = thing.update_appearance();
            string all = Dm.str(ALL);
            Assert.Equal(new[]
            {
                "update_name " + all, "update_desc " + all, "update_icon_state", "update_overlays",
            }, Lines(t));
            // `. = NONE`, then |= each part's return: name and desc answer NONE with no listener, icon answers its two flags
            Assert.True(Dm.eq(result, UPDATE_ICON));
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void UpdateAppearanceWithFlagsRunsOnlyTheRequestedParts()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var thing = Thing(t);
            object desc = thing.update_appearance(UPDATE_DESC);
            Assert.Equal(new[] { "update_desc " + Dm.str(UPDATE_DESC) }, Lines(t));
            Assert.True(Dm.eq(desc, NONE));

            Lines(t).Clear();
            object state = thing.update_appearance(UPDATE_ICON_STATE);
            Assert.Equal(new[] { "update_icon_state" }, Lines(t));   // no overlays: UPDATE_OVERLAYS was not asked for
            Assert.True(Dm.eq(state, UPDATE_ICON_STATE));

            Lines(t).Clear();
            Assert.True(Dm.eq(thing.update_appearance(NONE), NONE));
            Assert.Empty(Lines(t));
        }
    }

    [Fact]
    public void ACOMSIG_ATOM_UPDATE_APPEARANCEHandlerCanMaskPartsOfTheUpdate()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var thing = Thing(t);
            var mask = Listener("appearance", Dm.bitor(UPDATE_NAME, UPDATE_DESC));
            mask.RegisterSignal(thing, COMSIG_ATOM_UPDATE_APPEARANCE, nameof(datum_test_turfarea_listener.on_any));

            thing.update_appearance();

            // `updates &= ~SEND_SIGNAL(...)` (atom_appearance.dm:25): the masked parts do not run; the handler saw ALL
            Assert.Equal(new[]
            {
                "appearance thing(" + Dm.str(ALL) + ",null,null,null)",
                "update_icon_state",
                "update_overlays",
            }, Lines(t));
        }
    }

    [Fact]
    public void TheReturnValueCombinesWhatTheUpdateProcsAndTheirSignalsAnswer()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var thing = Thing(t);
            Listener("name", 1 << 10).RegisterSignal(thing, COMSIG_ATOM_UPDATE_NAME, nameof(datum_test_turfarea_listener.on_any));
            Listener("desc", 1 << 11).RegisterSignal(thing, COMSIG_ATOM_UPDATE_DESC, nameof(datum_test_turfarea_listener.on_any));
            Listener("updated", 1 << 12).RegisterSignal(thing, COMSIG_ATOM_UPDATED_ICON, nameof(datum_test_turfarea_listener.on_any));

            object result = thing.update_appearance();

            Assert.True(Dm.eq(result, Dm.bitor(Dm.bitor(Dm.bitor(1 << 10, 1 << 11), 1 << 12), UPDATE_ICON)));
        }
    }

    // ---- update_name, update_desc, update_icon_state, update_overlays ----

    [Fact]
    public void TheSingleUpdateProcsSendTheirSignalAndAnswerWhatItAnswers()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var thing = Thing(t);
            Assert.True(Dm.eq(thing.update_name(), NONE));     // no listener: NONE, the signal is not even sent
            Assert.True(Dm.eq(thing.update_desc(), NONE));
            Assert.True(Dm.eq(Waiting.NoSleep(thing.update_icon_state()), NONE));
            Lines(t).Clear();

            Listener("name", 7).RegisterSignal(thing, COMSIG_ATOM_UPDATE_NAME, nameof(datum_test_turfarea_listener.on_any));
            Listener("desc", 9).RegisterSignal(thing, COMSIG_ATOM_UPDATE_DESC, nameof(datum_test_turfarea_listener.on_any));
            Listener("state", 11).RegisterSignal(thing, COMSIG_ATOM_UPDATE_ICON_STATE, nameof(datum_test_turfarea_listener.on_any));
            Assert.True(Dm.eq(thing.update_name(UPDATE_NAME), 7));
            Assert.True(Dm.eq(thing.update_desc(UPDATE_DESC), 9));
            Assert.True(Dm.eq(Waiting.NoSleep(thing.update_icon_state()), 11));
            Assert.Contains("name thing(" + Dm.str(UPDATE_NAME) + ",null,null,null)", Lines(t));   // `updates` is the argument
            Assert.Contains("desc thing(" + Dm.str(UPDATE_DESC) + ",null,null,null)", Lines(t));
        }
    }

    [Fact]
    public void BaseUpdateOverlaysAnswersAFreshEmptyListAndSendsItToHandlersSoTheyCanAddToIt()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var plain = Dm.@new<obj_test_turfarea_sitter>();
            plain.name = "plain";
            var first = (DmList)Waiting.NoSleep(plain.update_overlays());
            var second = (DmList)Waiting.NoSleep(plain.update_overlays());
            Assert.Equal(0, first.len);
            Assert.NotSame(first, second);

            var watch = Listener("update_overlays");
            watch.RegisterSignal(plain, COMSIG_ATOM_UPDATE_OVERLAYS, nameof(datum_test_turfarea_listener.on_any));
            var list = (DmList)Waiting.NoSleep(plain.update_overlays());
            // the handler is given the very list the proc returns (`SEND_SIGNAL(src, COMSIG_ATOM_UPDATE_OVERLAYS, .)`)
            Assert.Equal("update_overlays plain(list(0),null,null,null)", Assert.Single(Lines(t)));
            Assert.Equal(0, list.len);
        }
    }

    // ---- update_icon ----

    [Fact]
    public void ACOMSIG_ATOM_UPDATE_ICONHandlerMasksTheIconPartsAndTheUpdatedSignalSeesTheMaskedArguments()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var thing = Thing(t);
            Listener("icon", UPDATE_OVERLAYS).RegisterSignal(thing, COMSIG_ATOM_UPDATE_ICON, nameof(datum_test_turfarea_listener.on_any));
            Listener("updated").RegisterSignal(thing, COMSIG_ATOM_UPDATED_ICON, nameof(datum_test_turfarea_listener.on_any));

            object result = Waiting.NoSleep(thing.update_icon());

            Assert.Equal(new[]
            {
                "icon thing(" + Dm.str(ALL) + ",null,null,null)",
                "update_icon_state",
                // COMSIG_ATOM_UPDATED_ICON(updates after the mask, the flags done so far) (atom_appearance.dm:100)
                "updated thing(" + Dm.str(Dm.bitand(ALL, Dm.bitnot(UPDATE_OVERLAYS))) + "," + Dm.str(UPDATE_ICON_STATE) + ",null,null)",
            }, Lines(t));
            Assert.True(Dm.eq(result, UPDATE_ICON_STATE));
        }
    }

    [Fact]
    public void UpdateIconWithNothingToShowLeavesTheOverlayListsAlone()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var thing = Thing(t);
            Waiting.NoSleep(thing.update_icon());
            Assert.Null(thing.managed_overlays);
            Assert.Equal(0, thing.overlays.len);

            thing.overlay_values.Add(null);   // nulls are counted and cut from the list (atom_appearance.dm:49-59)
            thing.overlay_values.Add(null);
            Waiting.NoSleep(thing.update_icon());
            Assert.Null(thing.managed_overlays);
            Assert.Equal(0, thing.overlays.len);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void UpdateIconWithTheSameSingleOverlayAsManagedChangesNothing()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var thing = Thing(t);
            thing.managed_overlays = "glow";           // "Single items are stored on their own, not in a list"
            thing.overlays.Add("glow");
            thing.overlay_values.Add("glow");

            Waiting.NoSleep(thing.update_icon());

            Assert.Equal("glow", thing.managed_overlays);
            Assert.Equal(new object[] { "glow" }, Dm.each(thing.overlays).Cast<object>().ToArray());   // identical: no cut, no add
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void UpdateIconWithTheSameListOfOverlaysAsManagedChangesNothing()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var thing = Thing(t);
            var managed = Dm.list("a", "b");
            thing.managed_overlays = managed;
            thing.overlays.Add("a", "b");
            thing.overlay_values.Add("a");
            thing.overlay_values.Add("b");
            Waiting.NoSleep(thing.update_icon());
            Assert.Same(managed, thing.managed_overlays);
            Assert.Equal(2, thing.overlays.len);
            Assert.Empty(t.Runtimes);
        }
    }

    [Fact]
    public void WhenTheManagedOverlaysAreAllTheOverlaysAndNoneAreWantedTheListIsReplacedByNull()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var thing = Thing(t);
            thing.managed_overlays = Dm.list("a", "b");
            thing.overlays.Add("a", "b");   // length(overlays) == length(managed_overlays): full control (atom_appearance.dm:73)

            Waiting.NoSleep(thing.update_icon());

            Assert.Equal(0, thing.overlays.len);
            Assert.Null(thing.managed_overlays);
        }
    }

    [Fact]
    public void ASingleManagedOverlayThatIsTheOnlyOverlayIsAlsoFullControl()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var thing = Thing(t);
            thing.managed_overlays = "glow";
            thing.overlays.Add("glow");
            Waiting.NoSleep(thing.update_icon());
            Assert.Equal(0, thing.overlays.len);
            Assert.Null(thing.managed_overlays);
        }
    }

    [Fact]
    public void DV5AnUpdateIconThatSleepsInsideUpdateAppearanceIsReportedAndFinishesDetached()
    {
        // update_appearance is SHOULD_NOT_SLEEP and calls the coloured update_icon through Waiting.NoSleep (waiting-model.md
        // 5.3; DV5): when an update_icon_state override sleeps anyway, the caller carries on with a default value, a runtime
        // is reported, and the update runs on to its end
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var sleepy = Dm.@new<obj_test_turfarea_sleepy_icon>();
            sleepy.name = "sleepy";

            object result = sleepy.update_appearance(UPDATE_ICON);

            Assert.True(Dm.eq(result, NONE));
            Assert.Single(t.Runtimes);
            Assert.Equal("", sleepy.icon_state ?? "");
            t.World.Step(2);
            Assert.Equal("slept", sleepy.icon_state);
            Assert.Contains("update_icon_state done", Lines(t));
        }
    }

    // ---- the overlay procs ----

    [Fact]
    public void BuildAppearanceListWrapsASingleValueAndDropsFalsyOnes()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var thing = Thing(t);
            Assert.Equal(0, ((DmList)thing.build_appearance_list(null)).len);
            var wrapped = (DmList)thing.build_appearance_list(5);
            Assert.Equal(new object[] { 5 }, Dm.each(wrapped).Cast<object>().ToArray());

            var mixed = Dm.list(5, null, 0, 7, null);
            object built = thing.build_appearance_list(mixed);
            Assert.Same(mixed, built);   // the list is edited in place and returned (overlays.dm:36-38)
            Assert.Equal(new object[] { 5, 7 }, Dm.each(mixed).Cast<object>().ToArray());
        }
    }

    [Fact]
    public void AddOverlayAppendsToTheOverlaysEvenWhenTheyWereEmpty()
    {
        // overlays.dm:78-79 reads `if(!overlays) return`: BYOND's overlays list is never null, so the guard does not trip
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var thing = Thing(t);
            Assert.Equal(0, thing.overlays.len);
            thing.add_overlay(5);
            thing.add_overlay(Dm.list(6, 7));
            Assert.Equal(new object[] { 5, 6, 7 }, Dm.each(thing.overlays).Cast<object>().ToArray());
            thing.add_overlay(null);   // nothing to add
            Assert.Equal(3, thing.overlays.len);
            Assert.Contains(DmWorldState.Current.DeferredSites, s => s.Contains("overlays.dm:86"));
        }
    }

    [Fact]
    public void CutOverlayRemovesTheNamedOverlaysAndCutOverlaysClearsThem()
    {
        var t = new TurfAreaWorld(1, 1, 1);
        using (t.World.Enter())
        {
            var thing = Thing(t);
            thing.add_overlay(Dm.list(5, 6, 7, 6));
            thing.cut_overlay(6);                 // list minus value takes the last occurrence
            Assert.Equal(new object[] { 5, 6, 7 }, Dm.each(thing.overlays).Cast<object>().ToArray());
            thing.cut_overlay(Dm.list(5, 7));
            Assert.Equal(new object[] { 6 }, Dm.each(thing.overlays).Cast<object>().ToArray());
            thing.cut_overlay(99);                // not there: nothing happens
            Assert.Equal(1, thing.overlays.len);

            thing.cut_overlays();
            Assert.Equal(0, thing.overlays.len);
            Assert.Contains(DmWorldState.Current.DeferredSites, s => s.Contains("overlays.dm:67"));
            Assert.Contains(DmWorldState.Current.DeferredSites, s => s.Contains("overlays.dm:76"));
            Assert.Empty(t.Runtimes);
        }
    }
}

/// <summary>An obj whose update procs record themselves and whose update_overlays adds <see cref="overlay_values"/>.</summary>
[DmPath("/obj/test_turfarea_appearance")]
public class obj_test_turfarea_appearance : obj
{
    [NotHashed] public readonly List<object> overlay_values = new();

    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;

    public override object update_name(float? updates = null)
    {
        TurfAreaTrace.Add("update_name " + Dm.str(updates));
        return base.update_name(updates);
    }

    public override object update_desc(float? updates = null)
    {
        TurfAreaTrace.Add("update_desc " + Dm.str(updates));
        return base.update_desc(updates);
    }

    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        TurfAreaTrace.Add("update_icon_state");
        return await base.update_icon_state(__extra);
    }

    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        TurfAreaTrace.Add("update_overlays");
        var list = (DmList)await base.update_overlays(__extra);
        foreach (object value in overlay_values)
            list.Add(value);
        return list;
    }
}

/// <summary>An obj whose update_icon_state sleeps a tick (tg forbids it in an update_appearance chain: DV5).</summary>
[DmPath("/obj/test_turfarea_sleepy_icon")]
public class obj_test_turfarea_sleepy_icon : obj
{
    public override DmTask New(params object[] __extra) => DmTask.CompletedTask;

    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        await sleep(1);
        icon_state = "slept";
        TurfAreaTrace.Add("update_icon_state done");
        return await base.update_icon_state(__extra);
    }
}
