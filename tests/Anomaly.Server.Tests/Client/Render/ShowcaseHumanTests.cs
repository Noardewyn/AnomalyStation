using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Client.Render;

/// <summary>A human composed from tg's real icons, the way the showcase draws one (D104): size, direction frames, damage, lying, GAGS colour.</summary>
public class ShowcaseHumanTests
{
    private readonly ITestOutputHelper _output;

    public ShowcaseHumanTests(ITestOutputHelper output) => _output = output;

    private static (int Left, int Top, int Right, int Bottom) Bounds(Raster r)
    {
        int left = r.Width, top = r.Height, right = -1, bottom = -1;
        for (int y = 0; y < r.Height; y++)
        {
            for (int x = 0; x < r.Width; x++)
            {
                if (r.Pixels[(y * r.Width + x) * 4 + 3] == 0)
                    continue;
                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }
        return (left, top, right, bottom);
    }

    private static ComposedRaster Compose(ShowcaseWorld world, TableRef appearance, byte dir, IconLibrary library, out AppearanceCompositor compositor)
    {
        compositor = new AppearanceCompositor(library);
        var tables = new SnapshotTables(world.Snapshot);
        Assert.True(compositor.TryCompose(tables, appearance, dir, 0, out ComposedRaster composed), string.Join("; ", library.Problems));
        return composed;
    }

    private static readonly HumanLook Plain = new("#e8b59b", "#3b2a1a", "#3f3f3f");

    [RealIconsFact]
    public void AStandingHumanIsAboutOneMetreEightyTallInEveryDirection()
    {
        IconLibrary library = RealIcons.NewLibrary();
        var world = new ShowcaseWorld();
        world.BuildRoom(5, 5);
        TableRef human = world.Human(Plain);
        world.Finish();
        foreach (byte dir in new[] { ByondDir.South, ByondDir.North, ByondDir.East, ByondDir.West })
        {
            ComposedRaster picture = Compose(world, human, dir, library, out _);
            Assert.Equal((32, 32, 0, 0), (picture.Image.Width, picture.Image.Height, picture.OriginX, picture.OriginY));
            (int left, int top, int right, int bottom) = Bounds(picture.Image);
            _output.WriteLine($"dir {dir}: columns {left}..{right}, rows {top}..{bottom}");
            // tg's human spans rows 3 to 31 (1.81 m at 6.25 cm per pixel), feet on the bottom row of the square.
            Assert.InRange(top, 2, 4);
            Assert.Equal(31, bottom);
            Assert.InRange(right - left + 1, 9, 22);
        }
    }

    [RealIconsFact]
    public void TheFramesOfTheFourDirectionsDiffer()
    {
        IconLibrary library = RealIcons.NewLibrary();
        var world = new ShowcaseWorld();
        world.BuildRoom(5, 5);
        TableRef human = world.Human(Plain);
        world.Finish();
        var seen = new List<byte[]>();
        foreach (byte dir in new[] { ByondDir.South, ByondDir.North, ByondDir.East, ByondDir.West })
            seen.Add(Compose(world, human, dir, library, out _).Image.Pixels);
        for (int i = 0; i < seen.Count; i++)
        {
            for (int j = i + 1; j < seen.Count; j++)
                Assert.NotEqual(seen[i], seen[j]);
        }
    }

    [RealIconsFact]
    public void TheJumpsuitTakesItsGagsColourAndTheSkinItsTone()
    {
        IconLibrary library = RealIcons.NewLibrary();
        var world = new ShowcaseWorld();
        world.BuildRoom(5, 5);
        TableRef black = world.Human(new HumanLook("#e8b59b", null, "#3f3f3f"));
        TableRef red = world.Human(new HumanLook("#e8b59b", null, "#b02020"));
        TableRef none = world.Human(new HumanLook("#e8b59b", null, null));
        world.Finish();
        Raster a = Compose(world, black, ByondDir.South, library, out _).Image, b = Compose(world, red, ByondDir.South, library, out _).Image, c = Compose(world, none, ByondDir.South, library, out _).Image;
        // The chest (middle of the torso): the jumpsuit's own pixel, dark grey or red, against the bare skin without a jumpsuit.
        uint chestBlack = a.Get(16, 17), chestRed = b.Get(16, 17), chestBare = c.Get(16, 17);
        _output.WriteLine($"chest: black {chestBlack:X8} red {chestRed:X8} bare {chestBare:X8}");
        Assert.NotEqual(chestBlack, chestRed);
        Assert.NotEqual(chestBlack, chestBare);
        Assert.True(((chestRed >> 24) & 255) > ((chestRed >> 8) & 255), "the red jumpsuit is red");
        Assert.True(((chestBlack >> 24) & 255) < 90, "the jumpsuit tinted #3f3f3f is dark");
    }

    [RealIconsFact]
    public void MoreDamageShowsMoreBloodAndBurns()
    {
        IconLibrary library = RealIcons.NewLibrary();
        var world = new ShowcaseWorld();
        world.BuildRoom(5, 5);
        int[] Level(int n) => new[] { n, n, n, n, n, n };
        TableRef[] brute = Enumerable.Range(0, 4).Select(n => world.Human(new HumanLook("#e8b59b", null, null, Level(n), null))).ToArray();
        TableRef[] burn = Enumerable.Range(0, 4).Select(n => world.Human(new HumanLook("#e8b59b", null, null, null, Level(n)))).ToArray();
        world.Finish();
        int Reddish(TableRef appearance)
        {
            Raster r = Compose(world, appearance, ByondDir.South, library, out _).Image;
            int count = 0;
            for (int i = 0; i < r.Pixels.Length; i += 4)
            {
                if (r.Pixels[i + 3] > 0 && r.Pixels[i] > 110 && r.Pixels[i + 1] < 60 && r.Pixels[i + 2] < 60)
                    count++;
            }
            return count;
        }
        int[] bloody = brute.Select(Reddish).ToArray();
        _output.WriteLine("blood pixels by brute level: " + string.Join(", ", bloody));
        Assert.True(bloody[0] < bloody[1] && bloody[1] < bloody[2] && bloody[2] < bloody[3], string.Join(", ", bloody));
        // Burns darken and redden the skin: the picture changes with every level.
        byte[][] burned = burn.Select(b => Compose(world, b, ByondDir.South, library, out _).Image.Pixels).ToArray();
        for (int i = 1; i < 4; i++)
            Assert.NotEqual(burned[0], burned[i]);
        Assert.NotEqual(burned[1], burned[2]);
    }

    [RealIconsFact]
    public void ALyingHumanIsWiderThanTallAndHasItsHeadOnTheSideTgTurnedItTo()
    {
        IconLibrary library = RealIcons.NewLibrary();
        var world = new ShowcaseWorld();
        world.BuildRoom(5, 5);
        TableRef lying90 = world.Human(Plain with { LyingAngle = 90 }), lying270 = world.Human(Plain with { LyingAngle = 270 });
        world.Finish();
        foreach (TableRef lying in new[] { lying90, lying270 })
        {
            ComposedRaster picture = Compose(world, lying, ByondDir.South, library, out _);
            (int left, int top, int right, int bottom) = Bounds(picture.Image);
            _output.WriteLine($"lying: {picture.Image.Width}x{picture.Image.Height} origin {picture.OriginX},{picture.OriginY}, columns {left}..{right}, rows {top}..{bottom}");
            Assert.True(right - left > bottom - top, "wider than tall");
        }
        // The head (the skin-coloured hair-topped end) sits at the east end for 90 degrees clockwise and the west end for 270.
        Raster east = Compose(world, lying90, ByondDir.South, library, out _).Image, west = Compose(world, lying270, ByondDir.South, library, out _).Image;
        Assert.True(HairColumn(east) > east.Width / 2, "the head of a human turned 90 degrees clockwise is at the right");
        Assert.True(HairColumn(west) < west.Width / 2, "and at the left when turned 270");
    }

    // The mean column of the brown hair pixels.
    private static double HairColumn(Raster r)
    {
        double sum = 0;
        int n = 0;
        for (int y = 0; y < r.Height; y++)
        {
            for (int x = 0; x < r.Width; x++)
            {
                uint p = r.Get(x, y);
                if ((p & 0xFF) == 255 && ((p >> 24) & 255) is > 40 and < 90 && ((p >> 16) & 255) is > 25 and < 65 && ((p >> 8) & 255) < 40)
                {
                    sum += x;
                    n++;
                }
            }
        }
        Assert.True(n > 0, "no hair pixels found");
        return sum / n;
    }

    [RealIconsFact]
    public void TheTurfPicturesComposeAndTheCompositionIsQuick()
    {
        IconLibrary library = RealIcons.NewLibrary();
        var world = new ShowcaseWorld();
        world.BuildRoom(9, 9);
        TableRef human = world.Human(new HumanLook("#e8b59b", "#3b2a1a", "#3f3f3f", new[] { 1, 2, 0, 0, 3, 1 }, new[] { 0, 0, 1, 1, 0, 0 }));
        world.Finish();
        var tables = new SnapshotTables(world.Snapshot);
        var compositor = new AppearanceCompositor(library);
        var watch = System.Diagnostics.Stopwatch.StartNew();
        Assert.True(compositor.TryCompose(tables, human, ByondDir.South, 0, out _));
        long firstMs = watch.ElapsedMilliseconds; // includes loading and decoding the icon files
        watch.Restart();
        const int Repeats = 40;
        for (int i = 0; i < Repeats; i++)
        {
            compositor.Clear();
            Assert.True(compositor.TryCompose(tables, human, ByondDir.South, 0, out _));
        }
        double perCompose = watch.Elapsed.TotalMilliseconds / Repeats;
        _output.WriteLine($"first compose {firstMs} ms with loading, then {perCompose:0.00} ms per human and direction");
        Assert.True(perCompose < 25, perCompose + " ms");
        foreach (TableRef turf in world.Snapshot.Appearances.Select((_, i) => TableRef.Public((uint)i + 1)).Take(4))
        {
            Assert.True(compositor.TryCompose(tables, turf, ByondDir.South, 0, out ComposedRaster picture));
            Assert.Equal(32, picture.Image.Width);
        }
    }

    /// <summary>A Snapshot's tables as an appearance source (what ReplicaWorld offers after applying it).</summary>
    private sealed class SnapshotTables : IAppearanceSource
    {
        private readonly Snapshot _snapshot;

        public SnapshotTables(Snapshot snapshot) => _snapshot = snapshot;

        public bool TryGetAppearance(TableRef reference, out AppearanceEntry entry)
        {
            entry = reference.IsNone || reference.Private || reference.Index > _snapshot.Appearances.Count ? null : _snapshot.Appearances[(int)reference.Index - 1];
            return entry != null;
        }

        public string ResolveString(TableRef reference) => reference.IsNone || reference.Private || reference.Index > _snapshot.Strings.Count ? null : _snapshot.Strings[(int)reference.Index - 1];

        public string ResolveResource(TableRef reference) => reference.IsNone || reference.Private || reference.Index > _snapshot.Resources.Count ? null : _snapshot.Resources[(int)reference.Index - 1];
    }
}
