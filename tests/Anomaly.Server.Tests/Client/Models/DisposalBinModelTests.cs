using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Models;

namespace Anomaly.Server.Tests.Client.Models;

/// <summary>
/// D325, D142: the disposal bin's model shows tg's update_overlays (disposal/bin.dm:593): the handle overlay pulls its lever down, and each
/// light overlay lights its lamp (occupied blue, charging red and blinking, ready green), the lamp dark without it.
/// </summary>
public sealed class DisposalBinModelTests
{
    private static ModelEntry Bin()
    {
        for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "godot", "assets", "models", ModelCatalog.FileName);
            if (File.Exists(candidate))
                return ModelCatalog.Parse(File.ReadAllText(candidate)).Find("icons/obj/pipes_n_cables/disposal.dmi", "disposal");
        }
        throw new InvalidOperationException("godot/assets/models/models.json not found above the test's folder");
    }

    [Fact]
    public void TheHandleSwingsTheLeverAndEachLightOverlayLightsItsLamp()
    {
        ModelEntry bin = Bin();
        Assert.Equal("disposal_unit", bin.Id);
        Assert.True(CrateLook.Open(bin, "disposal", new[] { "dispover-handle" }));
        Assert.False(CrateLook.Open(bin, "disposal", new[] { "dispover-full" }));
        Assert.NotEqual(0f, bin.Parts["Flush_Lever"].Degrees);

        foreach ((string lamp, string overlay) in new[] { ("full", "dispover-full"), ("charge", "dispover-charge"), ("ready", "dispover-ready") })
        {
            Assert.True(bin.Parts[lamp].DrawnWith(new[] { overlay }));
            Assert.False(bin.Parts[lamp + "Off"].DrawnWith(new[] { overlay }));
            Assert.False(bin.Parts[lamp].DrawnWith(Array.Empty<string>()));
            Assert.True(bin.Parts[lamp + "Off"].DrawnWith(Array.Empty<string>()));
            Assert.NotNull(bin.Parts[lamp].Light);
        }
        Assert.Equal(0.2f, bin.Parts["charge"].BlinkOn); // dispover-charge's two frames of 2 ticks
        Assert.Equal(0f, bin.Parts["ready"].BlinkOn);
    }
}
