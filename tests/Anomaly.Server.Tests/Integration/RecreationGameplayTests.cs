using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The recreation through the real server with a headless client: a joined player opens the holodeck console's window beside a
/// holodeck built in the room (tg's converted programs mounted where SSmapping finds them) and loads the basketball court, whose
/// hoops and ball appear in her replica; she plays The Orion Trail from its window, starting a game and travelling a turn; and she
/// puts a holochip in a slot machine and pulls its lever from its window, the reels stop and the machine settles the spin. Her
/// client draws each window from the server's data.
/// </summary>
public class RecreationGameplayTests(ITestOutputHelper output)
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

    private static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    private static void Hold(RealHeadlessWorld w, mob_living_carbon_human me, obj_item item) => Do(w, () =>
    {
        foreach (obj_item held in Dm.each<obj_item>(me.held_items).ToList())
            Waiting.NoSleep(me.dropItemToGround(held));
        if (item != null)
            Assert.True(Dm.truthy(Waiting.NoSleep(me.put_in_active_hand(item))));
    });

    private static UiNode Shown(TguiWindows windows, ReplicaWorld replica, int index)
    {
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        return windows.Get(index)?.Content;
    }

    private static string RepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (dir != null && !File.Exists(Path.Combine(dir, "AnomalyStation.sln")))
            dir = Path.GetDirectoryName(dir);
        Assert.NotNull(dir);
        return dir;
    }

    [Fact]
    public void A_player_loads_a_holodeck_program_plays_Orion_Trail_and_spins_the_slots()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 14340, TimeoutSteps = 8000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        ReplicaWorld replica = alice.Session.Replica;
        var heard = new List<string>();
        alice.Client.ServerTextReceived += t => heard.Add(t.Html ?? "");
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters),
            SendClose = window => alice.Session.UiClose(window),
        };
        w.Run(Second);

        // a 9x10 holodeck west of her, its console east of her: the programs at tg's paths, as the host mounts maps/templates
        string templates = Path.Combine(RepoRoot(), "maps", "templates");
        Do(w, () => Waiting.NoSleep(me.forceMove(locate(15, 10, me.z))));
        w.Run(Second / 2);
        (int x, int y) = In(w, () => ((int)me.x, (int)me.y));
        obj_machinery_computer_holodeck console = In(w, () =>
        {
            IDmFileSystem files = DmWorldState.Of(w.World).Files;
            foreach (string converted in Directory.GetFiles(templates, "holodeck_*" + MapText.Extension))
                files.WriteText("_maps/templates/" + Path.GetFileNameWithoutExtension(converted) + ".dmm", File.ReadAllText(converted));
            SSmapping.preloadHolodeckTemplates();
            var holodeck = Dm.@new<area_station_holodeck_rec_center>();
            foreach (turf t in Dm.each<turf>(block(x - 11, y - 5, (int)me.z, x - 3, y + 4, (int)me.z)))
            {
                t.change_area((area)t.loc, holodeck);
                Waiting.NoSleep(t.ChangeTurf(typeof(turf_open_floor_holofloor_plating)));
            }
            ((area)get_area(me)).requires_power = FALSE;
            return Dm.@new<obj_machinery_computer_holodeck>(get_step(me, EAST));
        });
        w.Run(3 * Second); // its post-init links the holodeck and projects the offline program; then its cooldown
        Assert.NotNull(In(w, () => console.linked));
        Assert.Equal("holodeck_offline", In(w, () => console.program));

        Hold(w, me, null);
        Assert.True(alice.Session.Click(On(console), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("Holodeck", window.Interface);
        UiNode content = Shown(windows, replica, window.Window);
        content.Walk().Single(n => n.Key == "program:holodeck_basketball" && n.Kind == UiKind.Button).OnClick();
        w.Run(Second);
        Assert.Equal("holodeck_basketball", In(w, () => console.program));
        List<obj_structure_hoop> hoops = In(w, () => Dm.each(console.spawned).OfType<obj_structure_hoop>().ToList());
        Assert.Equal(2, hoops.Count);
        Assert.All(hoops, h => Assert.True(replica.TryGet(In(w, () => h.dm_id), out _), "a hoop is in her replica"));
        content = Shown(windows, replica, window.Window);
        Assert.True(content.Walk().Single(n => n.Key == "program:holodeck_basketball").Selected, "the loaded program is lit");
        Assert.True(alice.Session.UiClose(window.Window));
        w.Run(4);

        // The Orion Trail: Begin Game, then Continue
        obj_machinery_computer_arcade_orion_trail orion = In(w, () => Dm.@new<obj_machinery_computer_arcade_orion_trail>(get_step(me, NORTH)));
        w.Run(Second);
        Assert.True(alice.Session.Click(On(orion), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("OrionGame", window.Interface);
        content = Shown(windows, replica, window.Window);
        content.Walk().Single(n => n.Key == "start_game" && n.Kind == UiKind.Button).OnClick();
        w.Run(Second / 2);
        Assert.Equal(2f, In(w, () => orion.gameStatus));
        content = Shown(windows, replica, window.Window);
        string settler = In(w, () => Dm.str(first_name(me.name))); // her character's first name is one of the crew
        Assert.Contains(content.Walk(), n => n.Key == "settler:" + settler);
        Do(w, () => orion.@event = null);
        content = Shown(windows, replica, window.Window);
        w.Run(Second / 2);
        content = Shown(windows, replica, window.Window);
        content.Walk().Single(n => n.Key == "continue" && n.Kind == UiKind.Button).OnClick();
        w.Run(Second / 2);
        Assert.True(In(w, () => orion.turns) >= 2, "a turn of the journey");
        Assert.True(alice.Session.UiClose(window.Window));
        w.Run(4);

        // the slot machine: a holochip in, then the lever from its window
        obj_machinery_computer_slot_machine slots = In(w, () => Dm.@new<obj_machinery_computer_slot_machine>(get_step(me, SOUTH)));
        w.Run(Second);
        obj_item_holochip chip = In(w, () => Dm.@new<obj_item_holochip>(me.loc, 50));
        Hold(w, me, chip);
        Assert.True(alice.Session.Click(On(slots), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        Assert.Equal(50f, In(w, () => slots.balance));
        Hold(w, me, null);
        Assert.True(alice.Session.Click(On(slots), ClickButton.Left, ClickModifiers.None));
        w.Run(Second / 2);
        window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("SlotMachine", window.Interface);
        content = Shown(windows, replica, window.Window);
        Assert.Equal("50 cr", content.Walk().Single(n => n.Key == "balance-value").Text);
        float plays = In(w, () => slots.plays); // the machine starts with 75 to 200 plays of false hope (slotmachine.dm:83)
        content.Walk().Single(n => n.Key == "spin" && n.Kind == UiKind.Button).OnClick();
        w.Run(Second / 2);
        Assert.Equal(1f, In(w, () => slots.working));
        string name = In(w, () => Dm.str(me.name));
        Assert.Contains(heard, h => h.Contains(name + " pulls the lever and the slot machine starts spinning!", StringComparison.Ordinal));
        w.Run(5 * Second);
        Assert.Equal(0f, In(w, () => slots.working));
        Assert.Equal(plays + 1, In(w, () => slots.plays));
        float balance = In(w, () => slots.balance);
        float paid = In(w, () => Dm.each<obj_item_holochip>(slots.loc).Sum(h => Dm.num(h.credits)));
        output.WriteLine($"after the spin: balance {balance}, paid {paid}, winning {In(w, () => slots.winning)}");
        Assert.True(balance + paid >= 45, "the spin cost five credits");
        content = Shown(windows, replica, window.Window);
        Assert.NotNull(content);
        output.WriteLine("server errors:\n" + string.Join("\n", w.Err.ToString().Split('\n').TakeLast(20)));
        // the slot machine's first Initialize makes one of every coin to learn their values (slotmachine.dm:99-102), the mythril coin
        // among them, whose material reaches the fantasy affixes, a deferred leaf (basemats.dm:541): no runtime
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}
