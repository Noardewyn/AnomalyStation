using System.Diagnostics;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using Anomaly.Server.Replication;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Replication.Bench;

/// <summary>
/// The bytes the server sends one player per second in protocol version 2 (M2 P5, scope 2h, the plan's "about 30 KB/s per
/// client at 150 movers"), and what the replicator costs per tick, with 50 and 150 humans running in straight lines through the
/// 48 x 48 room (a run is 1/3 tile per tick; they bounce off the walls), tg's boot, a few sessions with a health doll's worth of
/// screen objects. The messages are the ones the host sends on the State channel (the join's MapGrid and Snapshot, then one
/// Delta per tick: header, shared body, private block); LiteNetLib adds 4 to 8 bytes of header to each packet and splits a
/// message over its MTU (1024) into fragments, which these payload counts do not include. The loopback transport counts payload
/// bytes alike. Skipped unless <c>ANOMALY_BENCH=1</c>; reported in docs/ai/specs/replication.md section 9.
/// </summary>
[Trait("Category", "Bench")]
public class BandwidthBench
{
    private readonly ITestOutputHelper _output;

    public BandwidthBench(ITestOutputHelper output)
    {
        _output = output;
    }

    [BenchFact]
    public void BytesPerPlayerPerSecondAndReplicatorCostWithMovingHumans()
    {
        var report = new BenchReport(_output, "bandwidth-v2");
        foreach (int humans in new[] { 50, 150 })
        {
            foreach (bool lookingAround in new[] { false, true })
                report.Add($"humans {humans}, yaw {(lookingAround ? "changing every tick" : "steady")}", Measure(humans, lookingAround));
        }

        report.Save();
    }

    /// <summary>A small, non-bench guard of the same thing: at 150 humans a player's bytes stay under 45 KB/s (the plan expects about 30).</summary>
    [Fact]
    public void ABudgetOf45KilobytesPerPlayerPerSecondHoldsAt150MovingHumans()
    {
        Dictionary<string, object> result = Measure(150, lookingAround: true, measureTicks: 100);
        double perSecond = (double)result["bytes per player per second"];
        Assert.InRange(perSecond, 5_000, 45_000);
    }

    private static Dictionary<string, object> Measure(int humans, bool lookingAround, int measureTicks = 400)
    {
        const int Players = 3, WarmTicks = 40;
        ReplicationWorld world = ReplicationWorld.Boot(size: 48, movers: 0, seed: 3, containers: 0, notTranslatedThrows: false);
        world.ScenarioOn = false;
        var run = new V2Run(world);
        var crowd = new Crowd(world, run.Locomotion, humans, lookingAround);
        var sessions = new List<SessionView>();
        for (int i = 0; i < Players; i++)
        {
            SessionView s = run.Connect("Player" + i, crowd.Humans[i]);
            run.In(() =>
            {
                for (int k = 0; k < 9; k++) // the doll, the health indicator, fullscreens, alerts: a handful of screen objects
                {
                    var o = Dm.@new<obj>(null);
                    o.icon = Dm.resource("icons/hud/screen_gen.dmi");
                    o.icon_state = "health0";
                    o.screen_loc = "EAST-1:28,CENTER-" + k;
                    s.Client.screen.Add(o);
                }
            });
            sessions.Add(s);
        }

        for (int i = 0; i < WarmTicks; i++)
        {
            crowd.Act();
            run.Step();
        }

        var delta = new List<double>();
        var privateBlock = new List<double>();
        long bytes0 = sessions.Sum(s => s.TotalBytes);
        long body = 0;
        var step = new List<double>();
        for (int i = 0; i < measureTicks; i++)
        {
            crowd.Act();
            run.World.Step();
            long tick = run.World.World.Tick;
            var sw = Stopwatch.StartNew();
            DeltaFrame frame = run.Replicator.BuildDelta(tick);
            delta.Add(sw.Elapsed.TotalMilliseconds * 1000);
            body += frame.Body.Length;
            foreach (SessionView s in sessions)
            {
                sw.Restart();
                PrivateFrame own = run.Replicator.BuildPrivate(tick, run.Inputs(s));
                privateBlock.Add(sw.Elapsed.TotalMilliseconds * 1000);
                var buffer = new byte[Codec.MaxDeltaHeaderBytes + frame.Body.Length + own.Bytes.Length];
                int length = Codec.EncodeDelta(tick, 0, s.Avatar.dm_id, frame.Body.Span, own.Bytes.Span, buffer);
                s.Bytes.Add(buffer.AsSpan(0, length).ToArray());
                s.Replica.TryApply(buffer.AsSpan(0, length), out _, out _);
            }

            step.Add(sw.Elapsed.TotalMilliseconds);
        }

        double seconds = measureTicks / 20.0;
        long measured = sessions.Sum(s => s.TotalBytes) - bytes0;
        JoinMessages join = run.Replicator.BuildJoin(run.World.World.Tick, run.Inputs(sessions[0]));
        var result = new Dictionary<string, object>
        {
            ["bytes per player per second"] = Math.Round(measured / (double)Players / seconds),
            ["shared body bytes per tick"] = Math.Round(body / (double)measureTicks, 1),
            ["bytes per human per tick (body)"] = Math.Round(body / (double)measureTicks / humans, 2),
            ["header and private block bytes per player per tick"] = Math.Round((measured - (double)body * Players) / Players / measureTicks, 1),
            ["BuildDelta microseconds"] = Dist.Of(delta).ToString(),
            ["BuildPrivate microseconds per session"] = Dist.Of(privateBlock).ToString(),
            ["join MapGrid bytes"] = join.MapGrid.Length,
            ["join Snapshot bytes"] = join.Snapshot.Length,
            ["public appearance rows"] = run.Replicator.Appearances.Count,
        };
        return result;
    }

    // Humans running in straight lines at a run's pace (about 85 subunits a tick), bouncing off the room's edge.
    private sealed class Crowd
    {
        private readonly ReplicationWorld _world;
        private readonly FakeLocomotion _locomotion;
        private readonly bool _lookingAround;
        private readonly Random _random = new(11);
        private readonly List<(int Dx, int Dy)> _heading = new();

        public List<mob_living_carbon_human> Humans { get; } = new();

        public Crowd(ReplicationWorld world, FakeLocomotion locomotion, int count, bool lookingAround)
        {
            _world = world;
            _locomotion = locomotion;
            _lookingAround = lookingAround;
            using (world.World.Enter())
            {
                for (int i = 0; i < count; i++)
                {
                    int x = _random.Next(4, world.Size - 3), y = _random.Next(4, world.Size - 3);
                    var h = Dm.@new<mob_living_carbon_human>(Builtins.locate(x, y, 1));
                    _locomotion.Capsules[h.dm_id] = MotionState.AtTile(x, y, 1, (ushort)_random.Next(65536));
                    Humans.Add(h);
                    _heading.Add(((_random.Next(2) == 0 ? -1 : 1) * _random.Next(30, 61), (_random.Next(2) == 0 ? -1 : 1) * _random.Next(30, 61))); // |v| about 42 to 85
                }
            }
        }

        public void Act()
        {
            using (_world.World.Enter())
            {
                int lo = 256 * 3, hi = 256 * (_world.Size - 3);
                for (int i = 0; i < Humans.Count; i++)
                {
                    mob_living_carbon_human h = Humans[i];
                    MotionState s = _locomotion.Capsules[h.dm_id];
                    (int dx, int dy) = _heading[i];
                    int x = s.X + dx, y = s.Y + dy;
                    if (x < lo || x > hi) { dx = -dx; x = s.X + dx; }
                    if (y < lo || y > hi) { dy = -dy; y = s.Y + dy; }
                    _heading[i] = (dx, dy);
                    ushort yaw = _lookingAround ? (ushort)(s.Yaw + _random.Next(-600, 601)) : s.Yaw;
                    var next = s with { X = x, Y = y, Yaw = yaw };
                    if (next.TileX != s.TileX || next.TileY != s.TileY)
                        Waiting.NoSleep(h.forceMove(Builtins.locate(next.TileX, next.TileY, 1)));
                    _locomotion.Capsules[h.dm_id] = next;
                }
            }
        }
    }
}
