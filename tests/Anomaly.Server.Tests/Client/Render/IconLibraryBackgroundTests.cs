using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Render;

/// <summary>
/// The icon library's background loading (the explosion hitch, 2026-10-18): a sheet first drawn mid-game is decoded on a worker thread,
/// the compositor answers "not yet" and caches nothing meanwhile, and a blocking reader waits for the load in flight.
/// </summary>
public class IconLibraryBackgroundTests
{
    private const uint Red = 0xFF0000FF;

    // One 4 x 4 red state "boom"; reading its picture waits until the test opens the gate.
    private sealed class GatedFiles : IIconFiles
    {
        public readonly ManualResetEventSlim Gate = new(false);
        public int PictureReads;
        private readonly MemoryIconFiles _files = new();

        public GatedFiles()
        {
            _files.Text("fx.json", "{\"version\":\"4.0\",\"width\":4,\"height\":4,\"sheetWidth\":4,\"sheetHeight\":4,\"states\":[{\"name\":\"boom\",\"dirs\":1,\"frames\":1,"
                + "\"delays\":null,\"loop\":0,\"rewind\":false,\"movement\":false,\"hotspot\":null,\"first\":0,\"rects\":[[0,0,4,4]]}]}");
            _files.Files["fx.png"] = PngEncoder.Encode(Raster.Solid(4, 4, Red), 6, 8, 0, false);
        }

        public bool TryRead(string relativePath, out byte[] bytes)
        {
            if (relativePath.EndsWith(".png", StringComparison.Ordinal))
            {
                Interlocked.Increment(ref PictureReads);
                Gate.Wait(TimeSpan.FromSeconds(10));
            }
            return _files.TryRead(relativePath, out bytes);
        }
    }

    // tg's blank_title.png, the one atom icon that is a plain PNG: tools/Anomaly.Tools.Icons gives it a .json like a .dmi's.
    [Fact]
    public void APlainPngIconIsReadByItsTgPath()
    {
        var files = new MemoryIconFiles();
        files.Text("icons/blanks/blank_title.json", "{\"version\":\"4.0\",\"width\":4,\"height\":4,\"sheetWidth\":4,\"sheetHeight\":4,\"states\":[{\"name\":\"\",\"dirs\":1,\"frames\":1,"
            + "\"delays\":null,\"loop\":0,\"rewind\":false,\"movement\":false,\"hotspot\":null,\"first\":0,\"rects\":[[0,0,4,4]]}]}");
        files.Files["icons/blanks/blank_title.png"] = PngEncoder.Encode(Raster.Solid(4, 4, Red), 6, 8, 0, false);
        var library = new IconLibrary(files);
        var problems = new List<string>();
        library.ProblemFound += problems.Add;
        Assert.NotNull(library.GetSheet("icons/blanks/blank_title.png"));
        Assert.Empty(problems);
    }

    private static void WaitLoaded(IconLibrary library)
    {
        for (int i = 0; i < 1000 && library.PendingCount > 0; i++)
        {
            library.Pump();
            Thread.Sleep(5);
        }
        Assert.Equal(0, library.PendingCount);
    }

    [Fact]
    public void ASheetLoadsOnAWorkerAndTheCompositorDrawsItWhenItIsIn()
    {
        var files = new GatedFiles();
        var library = new IconLibrary(files) { Background = true };
        var tables = new TestTables();
        TableRef boom = tables.Add("fx.dmi", "boom");
        var compositor = new AppearanceCompositor(library);

        Assert.False(compositor.TryCompose(tables, boom, ByondDir.South, 0, out _), "still loading");
        Assert.Equal(1, library.PendingCount);
        Assert.True(compositor.IsBlank(tables, boom, ByondDir.South), "nothing drawn while loading, not a placeholder");
        Assert.Equal(1, compositor.Directions(tables, boom));

        files.Gate.Set();
        WaitLoaded(library);
        Assert.True(compositor.TryCompose(tables, boom, ByondDir.South, 0, out ComposedRaster composed), "nothing of the miss was cached");
        Assert.Equal(Red, composed.Image.Get(1, 1));
        Assert.Equal(1, library.CompletedLoads);
    }

    [Fact]
    public void ABlockingReaderWaitsForTheLoadInFlight()
    {
        var files = new GatedFiles();
        var library = new IconLibrary(files) { Background = true };
        library.Prefetch("fx.dmi");
        Assert.Equal(1, library.PendingCount);
        files.Gate.Set();
        Assert.True(library.TryGetIcon("fx.dmi", out IIcon icon));
        Assert.True(icon.TryGetState("boom", out _));
        Assert.Equal(0, library.PendingCount);
    }

    // A reader that comes before the worker starts reads the sheet itself (a long pool queue froze the frame for seconds); the worker then has nothing to do.
    [Fact]
    public void ASheetIsReadOnceWhoeverComesFirst()
    {
        var files = new GatedFiles();
        files.Gate.Set();
        var library = new IconLibrary(files) { Background = true };
        library.Prefetch("fx.dmi");
        Assert.True(library.TryGetIcon("fx.dmi", out _));
        Thread.Sleep(100); // the worker, if it had not started yet
        Assert.Equal(1, files.PictureReads);
    }

    [Fact]
    public void WithoutBackgroundEveryLoadBlocksAsBefore()
    {
        var files = new GatedFiles();
        files.Gate.Set();
        var library = new IconLibrary(files);
        Assert.True(library.TryGetIconNow("fx.dmi", out IIcon icon, out bool pending));
        Assert.False(pending);
        Assert.NotNull(icon);
        library.Prefetch("fx.dmi"); // off: nothing started
        Assert.Equal(0, library.PendingCount);
    }

    [Fact]
    public void AMissingSheetIsReportedOnceItsLoadLands()
    {
        var library = new IconLibrary(new MemoryIconFiles()) { Background = true };
        Assert.False(library.TryGetIconNow("nothing.dmi", out _, out _));
        WaitLoaded(library);
        Assert.False(library.TryGetIconNow("nothing.dmi", out _, out bool pending));
        Assert.False(pending);
        Assert.Contains(library.Problems, p => p.Contains("nothing.dmi", StringComparison.Ordinal));
    }
}
