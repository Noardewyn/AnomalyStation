using System.Reflection;
using System.Runtime.CompilerServices;
using Anomaly.Core.Motion;
using static Anomaly.Core.Tests.Motion.MotionTest;

namespace Anomaly.Core.Tests.Motion;

/// <summary>
/// Properties of the controller over many random paths (M2 scope 2l, docs/ai/specs/locomotion.md section 2): never inside a
/// solid tile or a standing capsule, the crossing events chain from the start tile to the end tile, the state stays on the
/// 1/256 grid, and one recorded run gives the same bits in every build (the test project runs in Debug and Release; the
/// motion core is always optimized).
/// </summary>
public class MotionPropertyTests
{
    private const int Radius = 45; // 0.175 tile

    [Fact]
    public void TenThousandRandomPathsNeverEnterASolidTileOrAStandingCapsule()
    {
        var rng = new Random(20261007);
        int crossings = 0, contacts = 0, presses = 0;
        for (int path = 0; path < 10_000; path++)
        {
            GridView view = RandomMap(rng, out MotionState s);
            MotionState before = s;
            for (int step = 0; step < 40; step++)
            {
                MotionParams p = RandomParams(rng);
                MotionInput frame = RandomFrame(rng, (uint)step);
                List<MotionEvent> events = Step(ref s, frame, p, view);
                Assert.False(view.BoxInSolid(s.X, s.Y, Radius), $"path {path} step {step}: inside a solid tile at {s.X},{s.Y}");
                Assert.False(view.InCapsule(s.X, s.Y, Radius), $"path {path} step {step}: inside a capsule at {s.X},{s.Y}");
                Assert.False(view.IsSolid(s.TileX, s.TileY, 1), $"path {path} step {step}: centre on a solid tile");
                // The crossings chain the old tile to the new one, one cardinal step each.
                int cx = before.TileX, cy = before.TileY;
                foreach (MotionEvent e in events)
                {
                    if (e.Kind == MotionEventKind.Crossed)
                    {
                        crossings++;
                        Assert.Equal((cx, cy), (e.FromX, e.FromY));
                        Assert.Equal(1, Math.Abs(e.ToX - e.FromX) + Math.Abs(e.ToY - e.FromY));
                        Assert.False(view.IsSolid(e.ToX, e.ToY, 1), $"path {path} step {step}: a crossing into a solid tile");
                        (cx, cy) = (e.ToX, e.ToY);
                    }
                    else if (e.Kind == MotionEventKind.Contact) contacts++;
                    else presses++;
                }
                Assert.Equal((s.TileX, s.TileY), (cx, cy));
                before = s;
            }
        }
        Assert.True(crossings > 10_000 && contacts > 100 && presses > 1_000, $"the paths exercised little: {crossings} crossings, {contacts} contacts, {presses} presses");
    }

    [Fact]
    public void QuantizationIsStable()
    {
        var rng = new Random(77);
        GridView view = RandomMap(rng, out MotionState s);
        for (int i = 0; i < 2_000; i++)
        {
            Step(ref s, RandomFrame(rng, (uint)i), RandomParams(rng), view);
            // The state is whole subunits: back and forth through tiles gives the same subunit, and a replay from a copy of
            // any state goes on bit for bit.
            Assert.Equal(s.X, MotionMath.Quantize(MotionMath.ToTiles(s.X)));
            Assert.Equal(s.Y, MotionMath.Quantize(MotionMath.ToTiles(s.Y)));
        }
    }

    /// <summary>
    /// A recorded run: 20,000 random steps over a random map with capsules and slides. The hash is pinned, so the same
    /// assertion passing in the Debug and the Release run of this test project proves both give the same bits (and a
    /// change of the controller's behaviour shows here first; update the constant only for an intended change).
    /// </summary>
    [Fact]
    public void ARecordedRunGivesTheSameBitsInEveryBuild()
    {
        Assert.Equal(PinnedRunHash, RecordedRunHash());
    }

    internal const ulong PinnedRunHash = 0xb8306c9577ed07acUL; // moved by M2 T2's fix (steps as long as the touch distance are cut into pieces, locomotion.md section 2.7), then by the M2 integration's (a press only into a face that blocks the final tile, PressTargetTests)

    internal static ulong RecordedRunHash()
    {
        var rng = new Random(424242);
        ulong h = FnvOffset;
        for (int path = 0; path < 200; path++)
        {
            GridView view = RandomMap(rng, out MotionState s);
            for (int i = 0; i < 100; i++)
            {
                if (rng.Next(50) == 0)
                    MotionStep.Displace(ref s, s.TileX + rng.Next(-1, 2), s.TileY + rng.Next(-1, 2), 1, (ushort)rng.Next(0, 6));
                List<MotionEvent> events = Step(ref s, RandomFrame(rng, (uint)i), RandomParams(rng), view);
                h = Hash(h, s);
                foreach (MotionEvent e in events)
                {
                    h = Mix(h, (uint)e.Kind);
                    h = Mix(h, (uint)(e.ToX * 1000 + e.ToY));
                    h = Mix(h, e.Dir);
                    h = Mix(h, (uint)e.OtherId);
                }
            }
        }
        return h;
    }

    [Fact]
    public void TheMotionCoreHasNoMutableStaticState()
    {
        var offenders = new List<string>();
        foreach (Type type in typeof(MotionStep).Assembly.GetTypes())
        {
            if (type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)) continue;
            foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                if (field.IsLiteral) continue;
                if (!field.IsInitOnly || field.FieldType.IsArray)
                    offenders.Add(type.FullName + "." + field.Name);
            }
        }
        Assert.Empty(offenders);
    }

    // ---- random worlds ----

    // A 12x12 map with about 15% solid tiles, a few standing capsules on open tiles (clear of each other), and a start on an
    // open tile's centre clear of the capsules.
    internal static GridView RandomMap(Random rng, out MotionState start)
    {
        var view = new GridView(12, 12);
        var open = new List<(int X, int Y)>();
        for (int x = 1; x <= 12; x++)
        {
            for (int y = 1; y <= 12; y++)
            {
                if (rng.Next(100) < 15) view.Solid(x, y);
                else open.Add((x, y));
            }
        }
        if (open.Count < 2)
        {
            view = new GridView(12, 12);
            open.Add((6, 6));
            open.Add((7, 7));
        }
        (int sx, int sy) = open[rng.Next(open.Count)];
        start = MotionState.AtTile(sx, sy, 1);
        int capsules = rng.Next(0, 7);
        for (int i = 0; i < capsules; i++)
        {
            (int cx, int cy) = open[rng.Next(open.Count)];
            // Somewhere inside the tile, kept off its faces by the radius so it does not reach into a wall.
            int px = cx * 256 + rng.Next(-80, 81);
            int py = cy * 256 + rng.Next(-80, 81);
            var c = new CapsuleObstacle(10 + i, px, py, 0.175f);
            bool clear = (px - start.X) * (long)(px - start.X) + (py - start.Y) * (long)(py - start.Y) >= 90L * 90L
                && view.Capsules.All(o => (o.X - px) * (long)(o.X - px) + (o.Y - py) * (long)(o.Y - py) >= 90L * 90L);
            if (clear) view.Capsules.Add(c);
        }
        return view;
    }

    internal static MotionParams RandomParams(Random rng)
    {
        float tilesPerTick = rng.Next(4) switch
        {
            0 => MotionMath.TilesPerTickFor(1.5f, 1f),
            1 => MotionMath.TilesPerTickFor(4f, 1f),
            2 => MotionMath.TilesPerTickFor(1.5f, 4f), // the largest speed scale: 1.33 tiles per tick
            _ => (float)rng.NextDouble(),
        };
        return new MotionParams(tilesPerTick, MotionMath.DefaultRadius, rng.Next(10) != 0, (short)rng.Next(1, 9));
    }

    internal static MotionInput RandomFrame(Random rng, uint seq)
    {
        sbyte x, y;
        switch (rng.Next(5))
        {
            case 0: x = 0; y = 0; break;
            case 1: x = (sbyte)(rng.Next(2) == 0 ? 127 : -127); y = 0; break;
            case 2: x = 0; y = (sbyte)(rng.Next(2) == 0 ? 127 : -127); break;
            default: x = (sbyte)rng.Next(-128, 128); y = (sbyte)rng.Next(-128, 128); break;
        }
        return new MotionInput(seq, x, y, (ushort)rng.Next(65536), MotionInputFlags.None);
    }
}
