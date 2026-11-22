using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// ROADMAP M4a's demo check through the real server on MetaStation, end to end: a headless Godot client (<see cref="PlaySession"/>)
/// clicks as a player does, on world entities, its own HUD and turfs, over the loopback. It picks up a mechanical toolbox, opens it
/// with the other hand, takes the welder out of the storage window, lights it, welds a public airlock shut with tg's right click and
/// 4 second do_after (and the welded door does not open to a hand), throws the toolbox in throw mode, and opens a security airlock
/// with a swiped ID card that the assistant's own card does not open. Each step is checked in the world and in what the client sees.
/// </summary>
public class M4aScenarioTests
{
    private const int Second = 20; // ticks per second

    private readonly ITestOutputHelper _output;

    public M4aScenarioTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Put(RealHeadlessWorld w, mob m, int x, int y)
    {
        using (w.World.Enter())
            Waiting.NoSleep(m.forceMove(Builtins.locate(x, y, m.z)));
        w.Run(Second);
    }

    private static T Find<T>(RealHeadlessWorld w, int x, int y, int z) where T : class
        => In(w, () => Dm.each_cast<atom>(((turf)Builtins.locate(x, y, z)).contents).OfType<T>().FirstOrDefault());

    private static ScreenEntry? OnScreen(ReplicaWorld replica, long id)
        => replica.Screen.Values.Where(e => e.Id == id).Select(e => (ScreenEntry?)e).FirstOrDefault();

    private static void ClickScreen(RealHeadlessWorld.Player p, long id, ClickButton button = ClickButton.Left)
        => Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, id, 0, 0, 0, 16, 16), button, ClickModifiers.None));

    private static List<string> States(ReplicaWorld replica, atom a)
    {
        ReplicaEntity seen = replica.Entities.Single(e => e.Id == a.dm_id);
        return CrewOutfitTests.Icons(replica, seen.State.Appearance).Select(i => i.State).ToList();
    }

    [MetaServerFact]
    public void A_player_takes_a_welder_from_a_toolbox_welds_an_airlock_throws_and_opens_a_door_with_an_id()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 4000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Engineer");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        int z = In(w, () => (int)me.z);
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)

        // A mechanical toolbox on the floor a tile east of the arrivals spawn (tg's spawn panel, the dev console's spawn).
        (int sx, int sy) = In(w, () => ((int)me.x, (int)me.y));
        w.Host.EnqueueConsoleLine($"spawn /obj/item/storage/toolbox/mechanical {sx + 1} {sy} {z}");
        w.Run(Second);
        var toolbox = Find<obj_item_storage_toolbox_mechanical>(w, sx + 1, sy, z);
        Assert.NotNull(toolbox);
        var welder = In(w, () => Dm.each_cast<atom>(toolbox.contents).OfType<obj_item_weldingtool>().Single());
        Assert.Contains(replica.Entities, e => e.Id == toolbox.dm_id);

        // Pick it up: a click with the empty active hand. It leaves the floor for the hand, and the HUD shows it in the hand.
        Assert.Null(In(w, () => me.get_active_held_item()));
        Assert.True(p.Session.Click(toolbox.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        Assert.Same(toolbox, In(w, () => me.get_active_held_item()));
        Assert.DoesNotContain(replica.Entities, e => e.Id == toolbox.dm_id);

        // Open it: the other hand (X) clicks the held toolbox on the HUD; the storage window lists the welder.
        p.Session.Keybinding(KeybindingCode.SwapHands);
        w.Run(5);
        Assert.Null(In(w, () => me.get_active_held_item()));
        ClickScreen(p, toolbox.dm_id);
        w.Run(Second);
        ScreenEntry? inWindow = OnScreen(replica, welder.dm_id);
        Assert.NotNull(inWindow);
        Assert.Equal(HudGroup.Storage, inWindow.Value.Group);
        Assert.True(replica.Screen.Values.Count(e => e.Group == HudGroup.Storage) >= 6, "the toolbox's six tools and the window's own objects");

        // Take the welder out with the empty hand, and light it (Z, attack_self).
        ClickScreen(p, welder.dm_id);
        w.Run(Second);
        Assert.Same(welder, In(w, () => me.get_active_held_item()));
        Assert.True(In(w, () => welder.loc == me && toolbox.loc == me), "the welder in one hand, the toolbox in the other");
        p.Session.Keybinding(KeybindingCode.ActivateInhand);
        w.Run(Second);
        Assert.True(In(w, () => Dm.truthy(welder.welding)), "the welder is lit");

        // Weld the public airlock east of the hallway (66,130) shut: tg's right click, the 4 second do_after.
        var airlock = Find<obj_machinery_door_airlock>(w, 66, 130, z);
        Assert.NotNull(airlock);
        Assert.True(In(w, () => Dm.truthy(airlock.density)), "Meta maps the airlock closed");
        Put(w, me, 65, 130);
        Assert.True(p.Session.Click(airlock.dm_id, ClickButton.Right, ClickModifiers.None));
        w.Run(2 * Second);
        Assert.False(In(w, () => Dm.truthy(airlock.welded)), "not before the do_after ends");
        w.Run(3 * Second);
        Assert.True(In(w, () => Dm.truthy(airlock.welded)), "welded shut");
        List<string> doorStates = States(replica, airlock);
        _output.WriteLine("the welded airlock as the client sees it: " + string.Join(", ", doorStates));
        Assert.Contains("welded", doorStates);

        // Throw the toolbox west down the corridor: the toolbox's hand (X), throw mode (R), a click on a turf six tiles away.
        Put(w, me, 62, 128);
        p.Session.Keybinding(KeybindingCode.SwapHands);
        w.Run(5);
        Assert.Same(toolbox, In(w, () => me.get_active_held_item()));
        p.Session.Keybinding(KeybindingCode.ToggleThrowMode);
        w.Run(5);
        bool seenFlying = false;
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Turf, 0, 56, 128, z), ClickButton.Left, ClickModifiers.None));
        w.Run(3 * Second, _ => seenFlying |= replica.Entities.Any(e => e.Id == toolbox.dm_id && e.IsThrown));
        (int tx, int ty, bool onTurf) = In(w, () => ((int)toolbox.x, (int)toolbox.y, toolbox.loc is turf));
        _output.WriteLine($"the toolbox landed at {tx},{ty}");
        Assert.True(onTurf, "the toolbox left the hand");
        Assert.True(tx <= 58 && ty == 128, $"the toolbox flew west down the corridor (at {tx},{ty})");
        Assert.True(seenFlying, "the client saw the toolbox in flight");
        ReplicaEntity landed = replica.Entities.Single(e => e.Id == toolbox.dm_id);
        Assert.Equal((tx, ty), (landed.State.X, landed.State.Y));
        Assert.False(landed.IsThrown);

        // The welded airlock does not open to the now empty hand.
        Put(w, me, 65, 130);
        Assert.Null(In(w, () => me.get_active_held_item()));
        Assert.True(p.Session.Click(airlock.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second);
        Assert.True(In(w, () => Dm.truthy(airlock.density)), "a welded airlock stays shut");

        // The security airlock north of the hallway (57,138): the assistant's card (maintenance only) does not open it, a captain's spare does.
        var door = Find<obj_machinery_door_airlock_security>(w, 57, 138, z);
        Assert.NotNull(door);
        string need = In(w, () => string.Join(",", Dm.each_cast<object>(door.req_access ?? Dm.list()).Concat(Dm.each_cast<object>(door.req_one_access ?? Dm.list()))));
        string card = In(w, () => string.Join(",", Dm.each_cast<object>((DmList)((obj_item_card_id)me.wear_id).access)));
        _output.WriteLine($"the door at 57,138 needs {need}; the assistant's card has {card}");
        (int fx, int fy) = In(w, () => new[] { (0, -1), (0, 1), (-1, 0), (1, 0) }
            .Select(d => (X: 57 + d.Item1, Y: 138 + d.Item2))
            .First(t => Builtins.locate(t.X, t.Y, z) is turf_open floor && !Dm.truthy(floor.density) && !Dm.each_cast<atom>(floor.contents).Any(c => Dm.truthy(c.density))));
        Put(w, me, fx, fy);
        Assert.True(p.Session.Click(door.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second);
        Assert.True(In(w, () => Dm.truthy(door.density)), "the assistant's card does not open it");
        w.Host.EnqueueConsoleLine("give Engineer /obj/item/card/id/advanced/gold/captains_spare");
        w.Run(5);
        Assert.IsAssignableFrom<obj_item_card_id>(In(w, () => me.get_active_held_item()));
        Assert.True(p.Session.Click(door.dm_id, ClickButton.Left, ClickModifiers.None));
        w.Run(2 * Second);
        Assert.False(In(w, () => Dm.truthy(door.density)), "the swiped card opens it");
        List<string> openStates = States(replica, door);
        _output.WriteLine("the open door as the client sees it: " + string.Join(", ", openStates));
        Assert.Contains("open", openStates);

        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount); // nothing the player did ran into a runtime
    }
}
