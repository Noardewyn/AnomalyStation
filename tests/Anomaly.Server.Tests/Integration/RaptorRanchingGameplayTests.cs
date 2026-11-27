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
/// Through the real server: a joined miner climbs onto a wild raptor by dragging himself onto it, rides out its bucking by
/// steering the way each arrow points until it calms down, and then rides it about; a joined player scans a raptor with a
/// RaptorDex and the RaptorDex window opens with the raptor's data.
/// </summary>
public class RaptorRanchingGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action f)
    {
        using (w.World.Enter())
            f();
    }

    private static ClickTarget On(atom a) => new(TargetKind.Entity, a.dm_id, 0, 0, 0, 16, 16);

    // Facing north (yaw 0), the movement keys go the world's way (first-person movement is relative to the view)
    private static MoveKeys KeyFor(string arrow) => arrow switch
    {
        "north" => MoveKeys.Forward,
        "south" => MoveKeys.Back,
        "west" => MoveKeys.Left,
        _ => MoveKeys.Right,
    };

    [Fact]
    public void A_miner_tames_a_wild_raptor_by_riding_out_its_bucking_and_rides_it()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 13281, TimeoutSteps = 12000 });
        RealHeadlessWorld.Player p = w.Join("Miner");
        var me = (mob_living_carbon_human)w.MobOf(p);
        p.Yaw = 0;
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount;

        // a wild adult raptor beside him, standing still (its wandering would make the drag a matter of luck)
        var raptor = In(w, () =>
        {
            var r = Dm.@new<mob_living_basic_raptor_green>(get_step(me, EAST));
            if (r.ai_controller is datum_ai_controller ai)
                Waiting.NoSleep(ai.set_ai_status(AI_STATUS_OFF));
            return r;
        });
        w.Run(5);
        Assert.False(In(w, () => Dm.truthy(raptor.has_ally(me))));

        // he drags himself onto it: the raptor bucks and the minigame starts
        Assert.True(p.Session.Drop(new DragRelease(On(me), On(raptor), ClickButton.Left, ClickModifiers.None, 4)));
        w.Run(Second);
        Assert.True(In(w, () => me.buckled == raptor), "the miner is on the raptor");
        var game = In(w, () => Dm.each<datum>(SSprocessing.processing).OfType<datum_riding_minigame>().FirstOrDefault());
        Assert.NotNull(game);

        // each time an arrow lights up he steers once its way
        int steps = 0;
        for (int tick = 0; tick < 60 * Second && !In(w, () => QDELETED(game)); tick++)
        {
            string up = In(w, () => Dm.each<object>(game.cached_arrows).FirstOrDefault(d => Dm.truthy((game.cached_arrows[d] as DmList)["is_active"])) as string);
            if (up == null)
            {
                p.Keys = MoveKeys.None;
                w.Run(1);
                continue;
            }
            p.Keys = KeyFor(up);
            w.Run(1);
            p.Keys = MoveKeys.None;
            w.Run(1);
            steps++;
        }
        output.WriteLine($"steered {steps} times; succeeded {In(w, () => QDELETED(game) ? -1 : game.current_succeeded)}");
        Assert.True(In(w, () => QDELETED(game)), "the minigame ended");
        Assert.True(In(w, () => Dm.truthy(raptor.has_ally(me))), "the raptor calmed down and took him as a friend");
        Assert.True(In(w, () => me.buckled == raptor), "he is still in the saddle");

        // and now he rides it: the raptor carries him north
        w.Run(Second);
        float startY = In(w, () => raptor.y);
        p.Keys = MoveKeys.Forward;
        w.Run(Second);
        p.Keys = MoveKeys.None;
        w.Run(Second / 2);
        Assert.True(In(w, () => raptor.y) > startY, "the raptor carried him north");
        Assert.True(In(w, () => me.buckled == raptor && me.loc == raptor.loc));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void A_player_scans_a_raptor_with_a_raptor_dex_and_its_window_opens()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 13282, TimeoutSteps = 8000 });
        RealHeadlessWorld.Player p = w.Join("Rancher");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        var windows = new TguiWindows
        {
            SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
            SendClose = window => p.Session.UiClose(window),
        };

        Do(w, () => DevCommands.Give(me, "/obj/item/raptor_dex"));
        var raptor = In(w, () =>
        {
            var r = Dm.@new<mob_living_basic_raptor_red>(get_step(me, EAST));
            if (r.ai_controller is datum_ai_controller ai)
                Waiting.NoSleep(ai.set_ai_status(AI_STATUS_OFF));
            return r;
        });
        w.Run(5);
        Assert.IsType<obj_item_raptor_dex>(In(w, () => me.get_active_held_item()));

        Assert.True(p.Session.Click(On(raptor), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        UiWindowState window = Assert.Single(replica.UiWindows.Tgui());
        Assert.Equal("RaptorDex", window.Interface);
        Assert.Equal(1, window.Merged.Num("raptor_scan"));
        string name = In(w, () => raptor.name);
        windows.Sync(replica.UiWindows, 1280, 720, 1);
        UiNode content = windows.Get(window.Window)?.Content;
        Assert.NotNull(content);
        output.WriteLine(content.AllText());
        Assert.Contains(name, content.AllText(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Health", content.AllText());
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    // The console's spawn makes a living mob a capsule like a player's. A gutluncher has no footstep element, so the
    // strides of its capsule must not send the stride signal to a listener it does not have.
    [Fact]
    public void A_spawned_gutluncher_and_raptor_roam_beside_a_player_without_an_error()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 13283, TimeoutSteps = 12000 });
        RealHeadlessWorld.Player p = w.Join("Rancher");
        var me = (mob_living_carbon_human)w.MobOf(p);
        w.Run(Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        (int x, int y, int z) = In(w, () => ((int)me.x, (int)me.y, (int)me.z));
        w.Host.EnqueueConsoleLine($"spawn /mob/living/basic/mining/gutlunch/milk {x} {y} {z}");
        w.Run(Second);
        w.Host.EnqueueConsoleLine($"spawn /mob/living/basic/raptor/green {x} {y} {z}");
        w.Run(90 * Second);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
