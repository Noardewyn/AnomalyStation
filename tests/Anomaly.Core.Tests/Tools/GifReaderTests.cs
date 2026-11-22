using System.IO;
using Anomaly.Core.Tests.Tg;
using Anomaly.Tools.Icons;

namespace Anomaly.Core.Tests.Tools;

/// <summary>
/// M9 slice E: the icons tool's GIF reader on tg's NtOS header pictures (icons/ui/programs): the downloader's eight frames with their
/// delays, a still battery icon, and the PNG that tg names mafia.gif.
/// </summary>
public class GifReaderTests
{
    private static string Programs => TgClone.Root == null ? null : Path.Combine(TgClone.Root, "icons", "ui", "programs");

    [Fact]
    public void Tgs_header_gifs_become_frame_strips()
    {
        if (Programs == null)
            return; // no tgstation clone next to the repository

        GifImage running = GifReader.Read(File.ReadAllBytes(Path.Combine(Programs, "downloader_running.gif")));
        Assert.Equal((22, 22, 8), (running.Width, running.Height, running.Frames.Count));
        Assert.Equal(new[] { 1.0, 1, 1, 1, 1, 1, 1, 50 }, running.Delays);
        Assert.All(running.Frames, f => Assert.Contains(f, c => c >> 24 != 0)); // every frame has opaque pixels

        (byte[] png, DmiFile meta) = GifReader.ToSheet(File.ReadAllBytes(Path.Combine(Programs, "batt_100.gif")));
        Assert.Equal((40, 22, 40), (meta.Width, meta.Height, meta.SheetWidth));
        Assert.Null(meta.States.Single().Delays); // still
        Assert.Equal(0x89, png[0]);

        (byte[] same, DmiFile mafia) = GifReader.ToSheet(File.ReadAllBytes(Path.Combine(Programs, "mafia.gif")));
        Assert.Equal((22, 22), (mafia.Width, mafia.Height));
        Assert.Equal(File.ReadAllBytes(Path.Combine(Programs, "mafia.gif")), same);
    }
}
