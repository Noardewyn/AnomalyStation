using System.IO;
using Anomaly.Client.Shared.Models;

namespace Anomaly.Server.Tests.Client.Models;

/// <summary>
/// A wall lamp is drawn by one model per state (light.dm update_icon_state and its glow overlay): every state of one fixture kind
/// is placed and sized alike, so the lamp does not move or grow when a fire alarm turns it to its emergency state.
/// </summary>
public sealed class LampModelTests
{
    private static ModelCatalog Catalog
    {
        get
        {
            for (DirectoryInfo dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "godot", "assets", "models", ModelCatalog.FileName);
                if (File.Exists(candidate))
                    return ModelCatalog.Parse(File.ReadAllText(candidate));
            }
            throw new InvalidOperationException("godot/assets/models/models.json not found above the test's folder");
        }
    }

    [Theory]
    [InlineData("light_tube_")]
    [InlineData("light_bulb_")]
    [InlineData("light_floor_")]
    public void EveryStateOfALampIsPlacedAndSizedAlike(string kind)
    {
        List<ModelEntry> lamps = Catalog.Entries.Where(e => e.Id.StartsWith(kind, StringComparison.Ordinal)).ToList();
        Assert.True(lamps.Count >= 7, $"{lamps.Count} {kind} entries");
        ModelEntry lit = lamps.Single(e => e.Id == kind + "on");
        Assert.All(lamps, e =>
        {
            Assert.Equal((lit.FitWidth, lit.FitHeight), (e.FitWidth, e.FitHeight));
            Assert.Equal(lit.Offset, e.Offset);
        });
    }
}
