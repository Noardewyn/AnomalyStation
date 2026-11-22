using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Models;

namespace Anomaly.Server.Tests.Client.Models;

/// <summary>
/// D142: the barbeque grill (grill.dm update_icon_state) shuts its lid only over what grills (grill), not while empty (grill_open, though its name ends in
/// open); the disk compartmentalizer's cover swings with its panel overlay; the jukebox and the dance machine draw their playing state.
/// </summary>
public sealed class KitchenAndBarModelTests
{
    private static ModelEntry Find(string icon, string state)
    {
        for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "godot", "assets", "models", ModelCatalog.FileName);
            if (File.Exists(candidate))
                return ModelCatalog.Parse(File.ReadAllText(candidate)).Find(icon, state);
        }
        throw new InvalidOperationException("godot/assets/models/models.json not found above the test's folder");
    }

    [Fact]
    public void TheGrillShutsItsLidOnlyOverWhatGrills()
    {
        ModelEntry grill = Find("icons/obj/machines/kitchen.dmi", "grill_open");
        Assert.Equal("barbeque_grill", grill.Id);
        Assert.False(CrateLook.Open(grill, "grill_open", []));
        Assert.False(CrateLook.Open(grill, "grill_on", []));
        Assert.True(CrateLook.Open(grill, "grill", []));
    }

    [Fact]
    public void TheDiskCompartmentalizersCoverSwingsWithItsPanel()
    {
        ModelEntry disks = Find("icons/obj/machines/smartfridge.dmi", "disktoaster");
        Assert.Equal("disk_compartmentalizer", disks.Id);
        Assert.True(CrateLook.Open(disks, "disktoaster", new[] { "disktoaster-panel" }));
        Assert.False(CrateLook.Open(disks, "disktoaster", []));
        Assert.NotEqual(0f, disks.Parts["Service cover.001"].Degrees);
    }

    [Theory]
    [InlineData("music.dmi", "jukebox-active", "jukebox")]
    [InlineData("music.dmi", "disco-active", "radiant_dance_machine")]
    [InlineData("smartfridge.dmi", "drying-rack", "drying_rack")]
    public void EachMachineHasItsModel(string dmi, string state, string id)
        => Assert.Equal(id, Find("icons/obj/machines/" + dmi, state)?.Id);
}
