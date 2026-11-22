using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// DV529 (the owner's play test, 2026-11-18): a pulled item, object or mob moved in jerks behind the puller and lagged far behind it. tg steps the
/// pulled thing into each tile the puller leaves, so a capsule walking at an angle made it step along a staircase at uneven times; its glide (a
/// crate) or the server's slides (a mob) stopped and rushed, a step behind that tile. A headless client pulls a crate, then a second player, along
/// the test room's hall at an angle; their drawn poses, sampled every frame, keep an even speed about a tile behind the own body, and a crate let go
/// settles on its tile.
/// </summary>
public class PullSmoothnessTests
{
    private const int Fps = 60;
    private readonly ITestOutputHelper _output;

    public PullSmoothnessTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    [Theory]
    [InlineData(1f, 0.0, 0)]
    [InlineData(1f, 0.15, 0)]
    [InlineData(0.5f, 0.15, 0)]
    [InlineData(0.5f, 0.5, 0)]
    [InlineData(0.5f, 0.15, 3)]
    public void A_pulled_crate_and_player_follow_a_tile_behind_at_an_even_speed(float speedScale, double slope, int jitterFrames)
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7, TimeoutSteps = 4000, MinLatencySteps = jitterFrames > 0 ? 1 : 0, MaxLatencySteps = jitterFrames },
            new ServerConfig { Seed = 7, RoomWidth = 48, RoomHeight = 24, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = speedScale }, framesPerTick: Fps / 20);
        RealHeadlessWorld.Player p = w.Join("Puller");
        var me = w.MobOf(p);
        w.Run(20);
        int z = In(w, () => (int)Dm.num(me.z));
        // the test room's hall: x 5 to 48, y 13 to 18
        int sx = 6, sy = 13;
        double length = slope > 0.3 ? 9 : 30;
        (double X, double Y) to = (sx + length, sy + length * slope);
        using (w.World.Enter())
            Waiting.NoSleep(me.forceMove(Builtins.locate(sx, sy, z)));
        w.Run(10);
        w.Host.EnqueueConsoleLine($"spawn /obj/structure/closet/crate {sx - 1} {sy} {z}");
        w.Run(5);
        var crate = In(w, () => Dm.each_cast<atom>(((turf)Builtins.locate(sx - 1, sy, z)).contents).OfType<obj_structure_closet_crate>().FirstOrDefault());
        Assert.NotNull(crate);
        Assert.True(p.Session.Click(crate.dm_id, ClickButton.Left, ClickModifiers.Ctrl));
        w.Run(5);
        Assert.Same(crate, In(w, () => me.pulling));
        Follow("the crate", w, p, crate.dm_id, to);

        // let go and walk on: the crate is drawn on its tile again
        p.Session.Keybinding(KeybindingCode.StopPulling);
        w.Run(5);
        Assert.Null(In(w, () => me.pulling));
        ReplicaEntity onClient = p.Session.Replica.Entities.Single(e => e.Id == crate.dm_id);
        w.AfterFrame = () => p.Session.Poses.TryGetPose(onClient, out _); // drawn every frame, as the client does
        Walk(w, p, (to.X, sy + 3));
        w.Run(20);
        w.AfterFrame = null;
        Assert.True(p.Session.Poses.TryGetPose(onClient, out RenderPose rest));
        Assert.Equal(onClient.State.X, rest.X, 2);
        Assert.Equal(onClient.State.Y, rest.Y, 2);

        RealHeadlessWorld.Player other = w.Join("Pulled");
        var them = w.MobOf(other);
        using (w.World.Enter())
        {
            Waiting.NoSleep(crate.forceMove(Builtins.locate(sx, 20, z)));
            Waiting.NoSleep(me.forceMove(Builtins.locate(sx, sy, z)));
            Waiting.NoSleep(them.forceMove(Builtins.locate(sx - 1, sy, z)));
        }
        w.Run(20);
        Assert.True(p.Session.Click(them.dm_id, ClickButton.Left, ClickModifiers.Ctrl));
        w.Run(5);
        Assert.Same(them, In(w, () => me.pulling));
        Follow("the player", w, p, them.dm_id, to);
    }

    // Walks the puller to `to` and checks the drawn pose of `id`, every frame, against the own drawn body.
    private void Follow(string what, RealHeadlessWorld w, RealHeadlessWorld.Player p, long id, (double X, double Y) to)
    {
        ReplicaWorld replica = p.Session.Replica;
        (float X, float Y) last = (float.NaN, float.NaN);
        var speeds = new List<float>();
        var gaps = new List<float>();
        w.AfterFrame = () =>
        {
            if (!replica.TryGet(id, out ReplicaEntity e) || !p.Session.Poses.TryGetPose(e, out RenderPose pose))
                return;
            if (!float.IsNaN(last.X))
                speeds.Add(MathF.Sqrt((pose.X - last.X) * (pose.X - last.X) + (pose.Y - last.Y) * (pose.Y - last.Y)) * Fps);
            last = (pose.X, pose.Y);
            if (replica.TryGet(replica.OwnEntity, out ReplicaEntity ownEntity) && p.Session.Poses.TryGetPose(ownEntity, out RenderPose own))
                gaps.Add(MathF.Sqrt((pose.X - own.X) * (pose.X - own.X) + (pose.Y - own.Y) * (pose.Y - own.Y)));
        };
        Walk(w, p, to);
        w.AfterFrame = null;
        // the steady walk: from half a second after it starts moving to a third of a second before it stops
        int first = speeds.FindIndex(v => v > 1f), lastMoving = speeds.FindLastIndex(v => v > 1f);
        List<float> steady = speeds.GetRange(first, lastMoving - first + 1).Skip(Fps / 2).SkipLast(Fps / 3).ToList();
        List<float> near = gaps.Skip(first + Fps / 2).Take(steady.Count).ToList();
        float mean = steady.Average();
        _output.WriteLine($"{what}: drawn {steady.Min():0.00} to {steady.Max():0.00} tiles a second (mean {mean:0.00}); from the own body {near.Min():0.00} to {near.Max():0.00} tiles");
        Assert.True(steady.Min() > 0.6f * mean, $"{what} slowed to {steady.Min():0.00} of a mean {mean:0.00}");
        Assert.True(steady.Max() < 1.5f * mean, $"{what} rushed at {steady.Max():0.00} of a mean {mean:0.00}");
        Assert.InRange(near.Min(), 0.85f, 1.05f); // tg's pulled thing is in the next tile: drawn a tile behind, not two
        Assert.InRange(near.Max(), 0.85f, 1.05f);
    }

    // Steers the player to `to` from its own predicted position, as a player would; then stands for a second.
    private static void Walk(RealHeadlessWorld w, RealHeadlessWorld.Player p, (double X, double Y) to)
    {
        for (int t = 0; t < 400; t++)
        {
            MotionState predicted = p.Session.Prediction.Predicted;
            double dx = to.X - predicted.X / 256.0, dy = to.Y - predicted.Y / 256.0;
            if (Math.Sqrt(dx * dx + dy * dy) < 0.15)
                break;
            p.Keys = MoveKeys.Forward;
            p.Yaw = Math.Atan2(-dx, dy); // Godot's yaw: 0 faces north, +90 degrees west
            w.Run(1);
        }
        p.Keys = MoveKeys.None;
        w.Run(20);
    }
}
