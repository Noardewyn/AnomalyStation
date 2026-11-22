using Anomaly.Client.Shared;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// DV67 (the owner's request, 2026-10-19): tg's singularity steps a tile once a second, and the client draws it drifting smoothly, as the own body
/// in zero gravity glides (<see cref="SingularityDrift"/>): measured per rendered frame without an engine over a roam of a step a second with a turn,
/// it never stands still between steps, moves about a tile a second, never jumps, and stays near the server's tile.
/// </summary>
public class SingularityDriftTests
{
    private readonly ITestOutputHelper _output;

    public SingularityDriftTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void A_singularity_stepping_a_tile_a_second_drifts_without_stopping()
    {
        var glide = SingularityDrift.NewGlide();
        const int fps = 60;
        const double frame = 1.0 / fps;
        // Twelve steps a second apart (a little off the second, as the subsystem's ticks fall): east, then north-east, then north.
        var tiles = new List<(double At, int X, int Y)>();
        int x = 50, y = 50;
        double at = 0.5;
        for (int i = 0; i < 12; i++)
        {
            if (i < 5) x++;
            else if (i < 8) { x++; y++; }
            else y++;
            at += i % 3 == 0 ? 1.05 : 1.0;
            tiles.Add((at, x, y));
        }
        (int X, int Y) server = (50, 50);
        int next = 0;
        var drawn = new List<(double T, float X, float Y)>();
        for (double t = 0; t < at + 1.5; t += frame)
        {
            while (next < tiles.Count && tiles[next].At <= t)
            {
                server = (tiles[next].X, tiles[next].Y);
                next++;
            }
            (float dx, float dy, _) = SingularityDrift.Step(glide, server.X, server.Y, 1, frame);
            drawn.Add((t, dx, dy));
            Assert.True(Math.Max(Math.Abs(dx - server.X), Math.Abs(dy - server.Y)) < 2.6f, $"drawn {dx},{dy} far from the server's {server} at {t:F2}");
        }

        // From the third step to the last: every frame moves, at about a tile a second (a diagonal tile is longer), never rushing a tile.
        int from = drawn.FindIndex(d => d.T >= tiles[2].At), to = drawn.FindIndex(d => d.T >= tiles[^1].At);
        double slowest = double.MaxValue, fastest = 0;
        for (int i = from + 1; i <= to; i++)
        {
            double speed = Math.Sqrt(Math.Pow(drawn[i].X - drawn[i - 1].X, 2) + Math.Pow(drawn[i].Y - drawn[i - 1].Y, 2)) / frame;
            slowest = Math.Min(slowest, speed);
            fastest = Math.Max(fastest, speed);
        }
        _output.WriteLine($"speed {slowest:F2} to {fastest:F2} tiles a second");
        Assert.True(slowest > 0.5, $"it stood still between steps (slowest {slowest:F2} tiles a second)");
        Assert.True(fastest < 2.0, $"it rushed a tile (fastest {fastest:F2} tiles a second)");
        // It ends on the last tile once the steps stop.
        Assert.Equal(server.X, drawn[^1].X, 2);
        Assert.Equal(server.Y, drawn[^1].Y, 2);
    }

    // The frames' drawn points of a thing whose server tile is `tileAt(t)` (frames at 60 a second, tg's glide size `glideSize`).
    private static List<(double T, float X, float Y)> Draw(Func<double, (int X, int Y)> tileAt, double seconds, float glideSize)
    {
        var glide = TileDrift.NewGlide("/obj/item/crowbar");
        const double frame = 1.0 / 60;
        var drawn = new List<(double, float, float)>();
        for (double t = 0; t < seconds; t += frame)
        {
            (int x, int y) = tileAt(t);
            (float dx, float dy, _) = TileDrift.Step(glide, x, y, 1, frame, glideSize);
            drawn.Add((t, dx, dy));
        }
        return drawn;
    }

    private static (double Slowest, double Fastest) Speeds(List<(double T, float X, float Y)> drawn, double from, double to)
    {
        double slowest = double.MaxValue, fastest = 0;
        for (int i = 1; i < drawn.Count; i++)
        {
            if (drawn[i - 1].T < from || drawn[i].T > to)
                continue;
            double speed = Math.Sqrt(Math.Pow(drawn[i].X - drawn[i - 1].X, 2) + Math.Pow(drawn[i].Y - drawn[i - 1].Y, 2)) / (drawn[i].T - drawn[i - 1].T);
            slowest = Math.Min(slowest, speed);
            fastest = Math.Max(fastest, speed);
        }
        return (slowest, fastest);
    }

    [Fact]
    public void An_item_the_singularity_pulls_a_tile_a_second_glides_between_its_steps_after_the_first_few()
    {
        // tg's step_towards once a second (the component's eat), the item's glide size BYOND's default (8 pixels a tick: a fifth of a second a tile)
        List<(double T, float X, float Y)> drawn = Draw(t => (10 + Math.Min(10, (int)Math.Floor(t)), 10), 12, 8f);
        (double slowest, double fastest) = Speeds(drawn, 4.0, 10.0);
        _output.WriteLine($"pulled: speed {slowest:F2} to {fastest:F2} tiles a second");
        Assert.True(slowest > 0.5, $"it stood still between steps (slowest {slowest:F2})");
        Assert.True(fastest < 2.0, $"it rushed a tile (fastest {fastest:F2})");
        Assert.Equal(20f, drawn[^1].X, 2);
    }

    [Fact]
    public void A_thing_an_explosion_throws_one_and_two_tiles_a_tick_flies_at_an_even_speed()
    {
        // tg's thrownthing at EXPLOSION_THROW_SPEED 4: 2 tiles a decisecond, the server's ticks of a twentieth of a second move it 1 or 2 tiles (the
        // ceiling of its progress); its glide size from 1 / speed is more than a tile a tick.
        int TileAt(double t)
        {
            int tick = (int)Math.Floor(t * 20);
            if (tick < 10)
                return 10;
            return 10 + Math.Min(20, (int)Math.Ceiling((tick - 10) * 1.5));
        }
        List<(double T, float X, float Y)> drawn = Draw(t => (TileAt(t), 10), 3, 128f);
        (double slowest, double fastest) = Speeds(drawn, 0.75, 1.1);
        _output.WriteLine($"thrown: speed {slowest:F2} to {fastest:F2} tiles a second");
        Assert.True(fastest < 1.5 * slowest, $"its speed changed from tick to tick ({slowest:F2} to {fastest:F2} tiles a second)");
        Assert.Equal(30f, drawn[^1].X, 2);
    }
}
