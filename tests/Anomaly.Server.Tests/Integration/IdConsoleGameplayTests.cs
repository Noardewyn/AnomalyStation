using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps S4a (D327) through the real server on MetaStation: Alice clicks an identification console, runs Plexagon Access Management
/// from NtOS's menu, inserts the captain's spare ID and Bob's ID through the NtosCard window, logs in, takes an access off Bob's
/// card and puts it back, renames his assignment and demotes him (tg's deny chirp); Plexagon HR Core then shows the job slots.
/// </summary>
public class IdConsoleGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private static bool Clear(turf t) => t is turf_open && !Dm.truthy(t.density) && !Dm.each_cast<atom>(t.contents).Any(a => Dm.truthy(a.density));

    private static turf Beside(atom thing)
    {
        foreach (float dir in new float[] { SOUTH, NORTH, EAST, WEST })
        {
            turf t = get_step(thing, dir);
            if (t != null && Clear(t))
                return t;
        }
        return null;
    }

    private static ClickTarget On(atom a) => new ClickTarget(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    [MetaServerFact]
    public void Alice_programs_Bobs_card_at_the_identification_console()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 4110, TimeoutSteps = 30000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        ReplicaWorld replica = alice.Session.Replica;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters),
            SendClose = window => alice.Session.UiClose(window),
        };
        var sounds = new List<SoundEvent>();
        replica.SoundsReceived += events => sounds.AddRange(events);
        w.Run(2 * Second);
        var errors = new List<Exception>();
        Action<Exception> sink = w.World.RuntimeErrorSink;
        w.World.RuntimeErrorSink = e => { errors.Add(e); sink?.Invoke(e); };

        var console = In(w, () => Dm.each_cast<obj_machinery_modular_computer_preset_id>(SSmachines.get_machines_by_type(typeof(obj_machinery_modular_computer_preset_id)))
            .First(m => !Dm.truthy(Dm.bitand(m.machine_stat, Dm.bitor(NOPOWER, BROKEN))) && Beside(m) != null));
        obj_item_card_id bobsCard = In(w, () =>
        {
            Waiting.NoSleep(me.forceMove(Beside(console)));
            return bobMob.wear_id as obj_item_card_id;
        });
        Assert.NotNull(bobsCard);
        w.Run(Second / 2);

        // NtOS's menu on the console, then the ID writer
        Assert.True(alice.Session.Click(On(console), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("NtosMain", window.Interface);
        UiNode content = Shown(windows, replica, window.Window);
        ByKey(ByKey(content, "program:plexagonidwriter"), "run").OnClick();
        w.Run(Second);
        Assert.Equal("NtosCard", replica.UiWindows[window.Window].Interface);
        Do(w, () => Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(Dm.@new<obj_item_card_id_advanced_gold_captains_spare>(me.loc)))))); // an empty hand opened the menu (a card in it is inserted, as tg)
        w.Run(Second / 2);

        // the spare ID into the login slot, Bob's card into the target slot
        ByKey(Shown(windows, replica, window.Window), "card-main-id").OnClick();
        w.Run(Second);
        In(w, () =>
        {
            Assert.IsType<obj_item_card_id_advanced_gold_captains_spare>(console.cpu.stored_id);
            Waiting.NoSleep(bobMob.dropItemToGround(bobsCard));
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_hands(bobsCard))));
            return 0;
        });
        w.Run(Second / 2);
        ByKey(Shown(windows, replica, window.Window), "card-alt-id").OnClick();
        w.Run(Second);
        In(w, () => { Assert.Same(bobsCard, console.cpu.alt_stored_id); return 0; });
        ByKey(Shown(windows, replica, window.Window), "card-login").OnClick();
        w.Run(Second);
        content = Shown(windows, replica, window.Window);
        Assert.Contains(content.Walk(), n => n.Kind == UiKind.NoticeBox && n.Text == "Login: captain's spare ID");

        // the access list: maintenance off and back on; the assignment renamed; then demoted
        In(w, () => { Assert.Contains(ACCESS_MAINT_TUNNELS, Dm.each<object>(bobsCard.access)); return 0; });
        Assert.True(alice.Session.UiAct(window.Window, "PRG_access", UiValue.ObjectOf(("access_target", UiValue.Of(ACCESS_MAINT_TUNNELS)))));
        w.Run(Second);
        In(w, () => { Assert.DoesNotContain(ACCESS_MAINT_TUNNELS, Dm.each<object>(bobsCard.access)); return 0; });
        Assert.True(alice.Session.UiAct(window.Window, "PRG_access", UiValue.ObjectOf(("access_target", UiValue.Of(ACCESS_MAINT_TUNNELS)))));
        w.Run(Second);
        In(w, () => { Assert.Contains(ACCESS_MAINT_TUNNELS, Dm.each<object>(bobsCard.access)); return 0; });
        ByKey(Shown(windows, replica, window.Window), "card-rank").OnCommit("Barista");
        w.Run(Second);
        In(w, () => { Assert.Equal("Barista", bobsCard.assignment); return 0; });
        Assert.Contains("Barista", ByKey(Shown(windows, replica, window.Window), "card-alt-id").Text);
        Assert.True(alice.Session.UiAct(window.Window, "PRG_terminate", null));
        w.Run(Second);
        In(w, () =>
        {
            Assert.Equal("Demoted", bobsCard.assignment);
            Assert.Null(bobsCard.trim);
            return 0;
        });
        Assert.Contains(sounds, e => e.File == "sound/machines/terminal/terminal_prompt_deny.ogg");

        // Plexagon HR Core: the job slots, the captain's spare authenticating it
        Assert.True(alice.Session.UiAct(window.Window, "PC_exit", null));
        w.Run(Second);
        content = Shown(windows, replica, window.Window);
        ByKey(ByKey(content, "program:plexagoncore"), "run").OnClick();
        w.Run(Second);
        Assert.Equal("NtosJobManager", replica.UiWindows[window.Window].Interface);
        content = Shown(windows, replica, window.Window);
        Assert.Contains(content.Walk(), n => n.Key == "slot:Scientist");

        output.WriteLine(string.Join("\n", errors.Select(e => e.Message)));
        Assert.DoesNotContain(errors, e => e.StackTrace != null && (e.StackTrace.Contains("card_mod", StringComparison.Ordinal) || e.StackTrace.Contains("job_management", StringComparison.Ordinal)));
    }
}
