using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The owner (2026-11-14): mispredictions while running diagonally into a wall and pressing it. A walker runs at a wall at several
/// angles of the view, with the keys pressed diagonally and straight, along it, past a doorway and into a corner, at tg's speed and the
/// server's, at two frame rates: at a steady latency it is never corrected; at a varying one (which mispredicted with or without walls:
/// the server repeated the last frame for a tick none arrived in) the repeat stands in for the next seq (D331) and only a key change
/// that arrives late is one step off.
/// </summary>
public class WallSlideGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    public static IEnumerable<object[]> Cases()
    {
        foreach (float speed in new[] { 1f, 0.5f }) // tg's speed and the server's own (DV70)
        foreach (int latency in new[] { 0, 2, -3, -6 }) // negative: a latency varying from none to that many network steps (D331)
        foreach (int frames in new[] { 3, 4 })
        foreach (int degrees in new[] { 10, 45, 65 })
            yield return new object[] { latency, frames, degrees, speed };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void RunningDiagonallyIntoAWallIsNeverMispredicted(int latency, int framesPerTick, int degrees, float speed)
    {
        var config = new ServerConfig { Seed = 7, RoomWidth = 24, RoomHeight = 24, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = speed };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 5, TimeoutSteps = 4000, MinLatencySteps = Math.Max(0, latency), MaxLatencySteps = Math.Abs(latency) },
            config, framesPerTick: framesPerTick, strict: false);
        var p = w.Join("Bob", inputBufferTarget: latency != 0 ? 2 : 1);
        var me = w.MobOf(p);
        using (w.World.Enter())
        {
            ADD_TRAIT(me, TRAIT_GODMODE, "test");
            // a wall to the east with a doorway gap and a corner, as the station's rooms have
            for (int y = 8; y <= 16; y++)
                if (y != 13)
                    Waiting.NoSleep(locate(10, y, 1).ChangeTurf(typeof(turf_closed_wall)));
            for (int x = 4; x <= 10; x++)
                Waiting.NoSleep(locate(x, 16, 1).ChangeTurf(typeof(turf_closed_wall)));
            Waiting.NoSleep(me.forceMove(locate(6, 10, 1)));
        }
        w.Run(Second);
        // the network's latency draws come in send order, so the join's messages (they grow with every window and type the port adds)
        // would move which frames arrive late: the schedule is pinned from here
        w.Network.Reseed(5);
        var before = p.Counters;
        var trace = new List<string>();
        double east = -Math.PI / 2;
        // the view turned from east toward north by the angle; W runs into the wall at that angle, then W+A and W+D press it diagonally
        (MoveKeys Keys, double Yaw, int Ticks)[] legs =
        {
            (MoveKeys.Forward, east + degrees * Math.PI / 180, Second * 3),
            (MoveKeys.Forward | MoveKeys.Left, east, Second * 2),
            (MoveKeys.Forward | MoveKeys.Right, east, Second * 2),
            (MoveKeys.Forward, east + Math.PI / 2 - degrees * Math.PI / 180, Second * 3), // the corner in the north-east
            (MoveKeys.Forward | MoveKeys.Right, east + Math.PI / 4, Second * 2),
        };
        foreach ((MoveKeys keys, double yaw, int ticks) in legs)
        {
            p.Keys = keys;
            p.Yaw = yaw;
            for (int t = 0; t < ticks; t++)
            {
                w.Run(1);
                MotionState s = w.ServerState(p);
                MotionState c = p.Session.Prediction.Predicted;
                trace.Add($"{keys}/{yaw:0.00} s{s.X},{s.Y} c{c.X},{c.Y} m{p.Counters.Corrections - before.Corrections + p.Counters.Mismatches - before.Mismatches + p.Counters.Snaps - before.Snaps}");
            }
        }
        p.Keys = MoveKeys.None;
        w.Run(Second);
        var after = p.Counters;
        output.WriteLine($"corrections {after.Corrections - before.Corrections}, mismatches {after.Mismatches - before.Mismatches}, snaps {after.Snaps - before.Snaps}");
        var jitter = w.Host.Sessions.Single().Jitter;
        output.WriteLine($"stand-ins {jitter.StandIns}, repeats {jitter.Repeats}, dropped {jitter.Dropped}");
        int first = Math.Max(0, trace.FindIndex(l => !l.EndsWith(" m0", StringComparison.Ordinal)) - 12);
        string Where() => string.Join(" | ", trace.Skip(first).Take(24));
        if (latency >= 0)
            Assert.True(after.Corrections == before.Corrections && after.Mismatches == before.Mismatches && after.Snaps == before.Snaps, Where());
        else
        {
            // D331: a late frame's tick stands in with the frame before, so only a leg's first frame arriving late is a step with the old keys (35 corrections before)
            Assert.Equal(0, jitter.Dropped);
            Assert.Equal(before.Snaps, after.Snaps);
            Assert.True(after.Corrections - before.Corrections <= legs.Length, Where());
        }
    }
}
