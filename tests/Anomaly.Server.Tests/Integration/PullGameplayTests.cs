using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Pulling (2026-10-15) through the real server on MetaStation: a headless Godot client (<see cref="PlaySession"/>) ctrl-clicks a
/// crate west of the arrivals spawn (tg's CtrlClickOn: the living base_click_ctrl's pulled verb and start_pulling, with tg's
/// thudswoosh) and walks east along the arrivals corridor; the crate follows into the tiles it leaves, in the world and on the
/// client (gliding at the puller's glide size), and the HUD's pull button shows tg's lit "pull" state. tg's stop_pulling key (H)
/// lets go ("pull0"). Then the player pulls a second player, standing, along the corridor and north, lies them down and drags
/// them on (tg's PULL_PRONE_SLOWDOWN), and lets go with the HUD button. The puller's prediction is corrected no more than walking.
/// </summary>
public class PullGameplayTests
{
    private const int Second = 20; // ticks per second
    private const string Thud = "sound/items/weapons/thudswoosh.ogg";

    private readonly ITestOutputHelper _output;

    public PullGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static (int X, int Y) Tile(RealHeadlessWorld w, atom a) => In(w, () => ((int)Dm.num(a.x), (int)Dm.num(a.y)));

    private static List<string> States(ReplicaWorld replica, long id)
        => CrewOutfitTests.Icons(replica, replica.Screen.Values.Single(e => e.Id == id).Appearance).Select(i => i.State).ToList();

    // Steers the player to (x, y) from its own predicted position, as a player would (MetaWalkTests); returns the ticks it took.
    private static int WalkTo(RealHeadlessWorld w, RealHeadlessWorld.Player p, double x, double y, int maxTicks = 20 * 20)
    {
        int ticks = 0;
        while (ticks < maxTicks)
        {
            MotionState predicted = p.Session.Prediction.Predicted;
            double dx = x - predicted.X / 256.0, dy = y - predicted.Y / 256.0;
            if (Math.Sqrt(dx * dx + dy * dy) < 0.15)
                break;
            p.Keys = MoveKeys.Forward;
            p.Yaw = Math.Atan2(-dx, dy); // Godot's yaw: 0 faces north, +90 degrees west
            w.Run(1);
            ticks++;
        }
        p.Keys = MoveKeys.None;
        w.Run(10);
        return ticks;
    }

    [MetaServerFact]
    public void A_player_pulls_a_crate_along_the_arrivals_corridor_then_another_player_and_lets_go()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 41, TimeoutSteps = 4000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Puller");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        int bootRuntimes = w.World.RuntimeErrorCount; // Meta's boot reaches code of later milestones (strict: false)
        // the spawn is a chair of the docked arrivals shuttle (M10 slice G3): the test starts in the arrivals corridor
        using (w.World.Enter())
            Waiting.NoSleep(me.forceMove(Builtins.locate(40, 122, 1)));
        w.Run(2);
        (int sx, int sy) = Tile(w, me);
        int z = In(w, () => (int)me.z);
        Assert.Equal((40, 122), (sx, sy)); // the arrivals corridor
        w.Run(Second);

        // ---- 1. The HUD's pull button, unlit: tg's "pull0".
        long button = In(w, () => ((atom)me.hud_used.screen_objects[Defines.HUD_MOB_PULL]).dm_id);
        Assert.Contains("pull", States(replica, button)); // DV422: the unlit button is tg's "pull" dimmed

        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);

        // ---- 2. A crate west of the spawn, ctrl-clicked: pulled, with tg's sound; the button lights ("pull").
        w.Host.EnqueueConsoleLine($"spawn /obj/structure/closet/crate {sx - 1} {sy} {z}");
        w.Run(5);
        var crate = In(w, () => Dm.each_cast<atom>(((turf)Builtins.locate(sx - 1, sy, z)).contents).OfType<obj_structure_closet_crate>().FirstOrDefault());
        Assert.NotNull(crate);
        Assert.True(p.Session.Click(crate.dm_id, ClickButton.Left, ClickModifiers.Ctrl));
        w.Run(5);
        Assert.Same(crate, In(w, () => me.pulling));
        Assert.Contains("pull", States(replica, button));
        Assert.Contains(heard, e => e.File == Thud);

        // ---- 3. East along the arrivals corridor: the crate follows a tile behind, in the world and on the client.
        long corrections = p.Counters.Corrections, snaps = p.Counters.Snaps;
        int ticks = WalkTo(w, p, 48, 122);
        long crateCorrections = p.Counters.Corrections - corrections, crateSnaps = p.Counters.Snaps - snaps;
        (int mx, int my) = Tile(w, me);
        Assert.Equal((48, 122), (mx, my));
        Assert.Same(crate, In(w, () => me.pulling));
        Assert.Equal((47, 122), Tile(w, crate));
        EntityState onClient = replica.Entities.Single(e => e.Id == crate.dm_id).State;
        Assert.Equal((47, 122), (onClient.X, onClient.Y));
        _output.WriteLine($"the crate: {ticks} ticks to (48,122), crate at {Tile(w, crate)} (client {onClient.X},{onClient.Y}, glide size {onClient.GlideSize}); corrections {crateCorrections}, snaps {crateSnaps}");
        Assert.Equal(In(w, () => me.glide_size), onClient.GlideSize, 2); // it glides at the puller's glide size (move_from_pull)
        Assert.Equal(In(w, () => DELAY_TO_GLIDE_SIZE(Dm.num(me.cached_multiplicative_slowdown))), onClient.GlideSize, 2); // the run: 32 / 3 pixels a tick
        Assert.Equal(0, crateCorrections); // walking alone gives none on this corridor
        Assert.Equal(0, crateSnaps);

        // ---- 4. tg's stop_pulling key lets go; the button goes back to "pull0".
        p.Session.Keybinding(KeybindingCode.StopPulling);
        w.Run(5);
        Assert.Null(In(w, () => me.pulling));
        Assert.Null(In(w, () => crate.pulledby));
        Assert.Contains("pull", States(replica, button)); // DV422: the unlit button is tg's "pull" dimmed

        // ---- 5. A second player, standing west of the puller (where the crate was: it goes to the spawn), ctrl-clicked.
        RealHeadlessWorld.Player other = w.Join("Pulled");
        var them = (mob_living_carbon_human)w.MobOf(other);
        using (w.World.Enter())
        {
            Waiting.NoSleep(crate.forceMove(Builtins.locate(sx, sy, z)));
            Waiting.NoSleep(them.forceMove(Builtins.locate(mx - 1, my, z)));
        }
        w.Run(Second);
        float walk = In(w, () => Dm.num(me.cached_multiplicative_slowdown));
        Assert.True(p.Session.Click(them.dm_id, ClickButton.Left, ClickModifiers.Ctrl));
        w.Run(5);
        Assert.Same(them, In(w, () => me.pulling));
        Assert.Same(me, In(w, () => them.pulledby));
        Assert.Equal(walk, In(w, () => Dm.num(me.cached_multiplicative_slowdown))); // a standing mob is no bulky drag

        // east to the corridor north and up it: the pulled player follows, shifted toward the puller
        corrections = p.Counters.Corrections;
        snaps = p.Counters.Snaps;
        WalkTo(w, p, 51.5, 122);
        WalkTo(w, p, 51.5, 125);
        Assert.Same(them, In(w, () => me.pulling));
        (mx, my) = Tile(w, me);
        (int ox, int oy) = Tile(w, them);
        Assert.Equal(1, Math.Max(Math.Abs(mx - ox), Math.Abs(my - oy)));
        float shift = In(w, () => Dm.num(((DmList)them.offsets[Defines.PIXEL_Y_OFFSET])?[Defines.GRABBING_TRAIT]) + Dm.num(((DmList)them.offsets[Defines.PIXEL_X_OFFSET])?[Defines.GRABBING_TRAIT]));
        Assert.Equal(Defines.GRAB_PIXEL_SHIFT_PASSIVE, Math.Abs(shift));
        long standingCorrections = p.Counters.Corrections - corrections, standingSnaps = p.Counters.Snaps - snaps;
        _output.WriteLine($"the standing player: puller at ({mx},{my}), pulled at ({ox},{oy}); corrections {standingCorrections}, snaps {standingSnaps}");

        // ---- 6. Lying down: the puller is slowed by tg's PULL_PRONE_SLOWDOWN and the prediction keeps up.
        using (w.World.Enter())
            Waiting.NoSleep(them.set_resting(1, 1, 1));
        w.Run(Second);
        Assert.Equal(Defines.LYING_DOWN, In(w, () => them.body_position));
        Assert.Equal(walk + Defines.PULL_PRONE_SLOWDOWN, In(w, () => Dm.num(me.cached_multiplicative_slowdown)), 3);
        corrections = p.Counters.Corrections;
        snaps = p.Counters.Snaps;
        WalkTo(w, p, 51.5, 127.5);
        Assert.Same(them, In(w, () => me.pulling));
        long lyingCorrections = p.Counters.Corrections - corrections, lyingSnaps = p.Counters.Snaps - snaps;
        _output.WriteLine($"the lying player: puller at {Tile(w, me)}, pulled at {Tile(w, them)}; corrections {lyingCorrections}, snaps {lyingSnaps}");

        // ---- 7. The HUD's pull button lets go.
        Assert.True(p.Session.Click(new ClickTarget(TargetKind.Screen, button, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(5);
        Assert.Null(In(w, () => me.pulling));
        Assert.Null(In(w, () => them.pulledby));
        Assert.Contains("pull", States(replica, button)); // DV422: the unlit button is tg's "pull" dimmed
        Assert.Equal(walk, In(w, () => Dm.num(me.cached_multiplicative_slowdown)));

        PredictionCounters c = p.Counters;
        _output.WriteLine($"the puller in all: reconciliations {c.Reconciliations}, corrections {c.Corrections}, snaps {c.Snaps}, mismatches {c.Mismatches}, max error {c.MaxErrorTiles:0.000} tiles");
        Assert.Equal(0, standingCorrections + lyingCorrections);
        Assert.Equal(0, standingSnaps + lyingSnaps);
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
