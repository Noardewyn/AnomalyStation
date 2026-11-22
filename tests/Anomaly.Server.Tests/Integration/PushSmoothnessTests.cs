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
/// DV535 and DV533 (the owner's play test, 2026-11-19): a pushed crate moved in jerks (smoothly one way, not the other), a pulled one jerked
/// when the pull started from the puller's own tile (a crate just spawned there) and when the puller turned back into it. A headless client
/// pushes a crate along the test room's hall in each direction, pulls one spawned on its own tile, and walks back into a pulled one; the
/// crate's drawn pose, sampled every frame, keeps an even speed, is never drawn inside the own body and settles on its tile.
/// </summary>
public class PushSmoothnessTests
{
    private const int Fps = 60;
    private readonly ITestOutputHelper _output;

    public PushSmoothnessTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static RealHeadlessWorld NewWorld(int jitterFrames) =>
        new(new LoopbackOptions { Seed = 7, TimeoutSteps = 4000, MinLatencySteps = jitterFrames > 0 ? 1 : 0, MaxLatencySteps = jitterFrames },
            new ServerConfig { Seed = 7, RoomWidth = 48, RoomHeight = 24, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 0.5f }, framesPerTick: Fps / 20);

    // A crate spawned on (x, y) of the own mob's level, with the own mob put on (mx, my).
    private static obj_structure_closet_crate Setup(RealHeadlessWorld w, RealHeadlessWorld.Player p, int mx, int my, int x, int y)
    {
        var me = w.MobOf(p);
        w.Run(20);
        int z = In(w, () => (int)Dm.num(me.z));
        using (w.World.Enter())
            Waiting.NoSleep(me.forceMove(Builtins.locate(mx, my, z)));
        w.Run(10);
        w.Host.EnqueueConsoleLine($"spawn /obj/structure/closet/crate {x} {y} {z}");
        w.Run(10);
        var crate = In(w, () => Dm.each_cast<atom>(((turf)Builtins.locate(x, y, z)).contents).OfType<obj_structure_closet_crate>().FirstOrDefault());
        Assert.NotNull(crate);
        return crate;
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(-1, 0, 0)]
    [InlineData(0, 1, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(-1, 0, 3)]
    public void A_pushed_crate_moves_evenly_ahead_of_the_body_in_every_direction(int ux, int uy, int jitterFrames)
    {
        using RealHeadlessWorld w = NewWorld(jitterFrames);
        RealHeadlessWorld.Player p = w.Join("Pusher");
        int cx = 25, cy = ux != 0 ? 15 : (uy > 0 ? 14 : 17); // the hall: x 5 to 48, y 13 to 18
        var crate = Setup(w, p, cx - ux, cy - uy, cx, cy);
        int length = ux != 0 ? 8 : 3;
        Trace trace = Walk(w, p, crate.dm_id, (cx + ux * (length + 0.5), cy + uy * (length + 0.5)));
        trace.Steady(_output, $"pushed ({ux}, {uy})", out float min, out float max, out float mean);
        Assert.True(min > 0.6f * mean, $"the pushed crate slowed to {min:0.00} of a mean {mean:0.00}");
        Assert.True(max < 1.5f * mean, $"the pushed crate rushed at {max:0.00} of a mean {mean:0.00}");
        Assert.True(In(w, () => Math.Abs(Dm.num(crate.x) - cx) + Math.Abs(Dm.num(crate.y) - cy)) >= length, "the crate was pushed along");
        float ahead = trace.Ahead(ux, uy).Min();
        _output.WriteLine($"ahead of the own body's centre at least {ahead:0.00} tiles");
        Assert.True(ahead > PoseSource.PushContact - 0.02f, $"the crate was drawn {ahead:0.00} tiles ahead: inside the body");
        trace.AssertNoRestart("the pushed crate");
        trace.AssertBodyNeverSlows("pushing");
        trace.AssertSettled(p, crate.dm_id);
    }

    [Fact]
    public void A_crate_pulled_from_the_own_tile_starts_without_a_surge()
    {
        using RealHeadlessWorld w = NewWorld(3);
        RealHeadlessWorld.Player p = w.Join("Puller");
        var crate = Setup(w, p, 20, 14, 20, 14); // spawned where the player stands, as the spawn menu does
        Assert.True(p.Session.Click(crate.dm_id, ClickButton.Left, ClickModifiers.Ctrl));
        w.Run(5);
        Assert.Same(crate, In(w, () => w.MobOf(p).pulling));
        Trace trace = Walk(w, p, crate.dm_id, (27, 14));
        float walk = trace.OwnSpeeds.Max();
        float fastest = trace.Speeds.Max();
        _output.WriteLine($"the crate at most {fastest:0.00} tiles a second, the body {walk:0.00}");
        Assert.True(fastest < 1.3f * walk, $"the crate surged at {fastest:0.00} tiles a second, the body walks {walk:0.00}");
        trace.Steady(_output, "pulled", out float min, out _, out float mean);
        Assert.True(min > 0.6f * mean, $"the pulled crate slowed to {min:0.00} of a mean {mean:0.00}");
    }

    [Fact]
    public void Walking_back_into_a_pulled_crate_pushes_it_evenly()
    {
        using RealHeadlessWorld w = NewWorld(3);
        RealHeadlessWorld.Player p = w.Join("Puller");
        var crate = Setup(w, p, 20, 14, 19, 14);
        Assert.True(p.Session.Click(crate.dm_id, ClickButton.Left, ClickModifiers.Ctrl));
        w.Run(5);
        Walk(w, p, crate.dm_id, (25, 14));
        Trace back = Walk(w, p, crate.dm_id, (16, 14));
        Assert.Null(In(w, () => w.MobOf(p).pulling)); // tg's PushAM stops pulling what it pushes
        Assert.Equal(0L, p.Session.Poses.PullTarget);
        // the first touch costs the body no time (DV535: its step is taken again through the freed tile), so the steady push starts at once
        int pushed = back.Poses.FindIndex(q => q.X < back.Poses[0].X - 0.05f);
        back.Steady(_output, "pushed back", out float min, out float max, out float mean, pushed);
        Assert.True(min > 0.6f * mean, $"the crate slowed to {min:0.00} of a mean {mean:0.00}");
        Assert.True(max < 1.5f * mean, $"the crate rushed at {max:0.00} of a mean {mean:0.00}");
        float ahead = back.Ahead(-1, 0).Skip(Fps / 2).Min();
        _output.WriteLine($"ahead of the own body's centre at least {ahead:0.00} tiles");
        Assert.True(ahead > PoseSource.PushContact - 0.02f, $"the crate was drawn {ahead:0.00} tiles ahead: inside the body");
        back.AssertNoRestart("the crate");
        back.AssertBodyNeverSlows("walking back into it");
        back.AssertSettled(p, crate.dm_id);
    }

    private sealed class Trace
    {
        public readonly List<float> Speeds = new(), OwnSpeeds = new();
        public readonly List<(float X, float Y, float OwnX, float OwnY)> Poses = new();

        // the crate's drawn position ahead of the own body's centre along (ux, uy)
        public IEnumerable<float> Ahead(int ux, int uy) => Poses.Select(q => (q.X - q.OwnX) * ux + (q.Y - q.OwnY) * uy);

        // the speeds of the steady walk: from half a second after the body starts walking (or from `from`) to a third of a second before it stops
        public void Steady(ITestOutputHelper output, string what, out float min, out float max, out float mean, int from = -1)
        {
            int first = OwnSpeeds.FindIndex(v => v > 1f), last = OwnSpeeds.FindLastIndex(v => v > 1f);
            first = from >= 0 ? from : first + Fps / 2;
            List<float> steady = Speeds.GetRange(first, last - first + 1).SkipLast(Fps / 3).ToList();
            (min, max, mean) = (steady.Min(), steady.Max(), steady.Average());
            output.WriteLine($"{what}: drawn {min:0.00} to {max:0.00} tiles a second (mean {mean:0.00})");
        }

        // after the body stops it slides on to its tile without stopping and starting again
        public void AssertNoRestart(string what)
        {
            int stop = OwnSpeeds.FindLastIndex(v => v > 1f);
            List<float> after = Speeds.Skip(stop).ToList();
            for (int i = 1; i < after.Count; i++)
                Assert.True(after[i] <= after[i - 1] + 0.3f, $"{what} sped up again after the body stopped: {after[i - 1]:0.00} then {after[i]:0.00} tiles a second");
        }

        // DV535: the own body's drawn speed only grows (its ramp) or holds while the keys are held: a press that stood it for a tick shows as a dip
        public void AssertBodyNeverSlows(string what)
        {
            int first = OwnSpeeds.FindIndex(v => v > 1f), last = OwnSpeeds.FindLastIndex(v => v > 1f) - Fps / 3;
            float top = OwnSpeeds.Max();
            for (int i = first + 1; i <= last; i++)
                Assert.True(OwnSpeeds[i] >= OwnSpeeds[i - 1] - 0.05f * top, $"{what}, the body slowed from {OwnSpeeds[i - 1]:0.00} to {OwnSpeeds[i]:0.00} tiles a second");
        }

        public void AssertSettled(RealHeadlessWorld.Player p, long id)
        {
            ReplicaEntity e = p.Session.Replica.Entities.Single(x => x.Id == id);
            (float x, float y, _, _) = Poses[^1];
            Assert.Equal(e.State.X, x, 2);
            Assert.Equal(e.State.Y, y, 2);
        }
    }

    // Steers the player to `to` from its own predicted position, as a player would, then stands for a second; the crate's pose is drawn every frame.
    private static Trace Walk(RealHeadlessWorld w, RealHeadlessWorld.Player p, long id, (double X, double Y) to)
    {
        ReplicaWorld replica = p.Session.Replica;
        var trace = new Trace();
        w.AfterFrame = () =>
        {
            if (!replica.TryGet(id, out ReplicaEntity e) || !p.Session.Poses.TryGetPose(e, out RenderPose pose)
                || !replica.TryGet(replica.OwnEntity, out ReplicaEntity ownEntity) || !p.Session.Poses.TryGetPose(ownEntity, out RenderPose own))
                return;
            if (trace.Poses.Count > 0)
            {
                var last = trace.Poses[^1];
                trace.Speeds.Add(MathF.Sqrt((pose.X - last.X) * (pose.X - last.X) + (pose.Y - last.Y) * (pose.Y - last.Y)) * Fps);
                trace.OwnSpeeds.Add(MathF.Sqrt((own.X - last.OwnX) * (own.X - last.OwnX) + (own.Y - last.OwnY) * (own.Y - last.OwnY)) * Fps);
            }
            trace.Poses.Add((pose.X, pose.Y, own.X, own.Y));
        };
        for (int t = 0; t < 600; t++)
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
        w.Run(Fps);
        w.AfterFrame = null;
        return trace;
    }
}
