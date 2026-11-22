using System.IO;
using Anomaly.Client.Shared.Models;

namespace Anomaly.Server.Tests.Client.Models;

/// <summary>
/// D325 (the owner, 2026-11-12): a multi-deck pipe adapter or cable hub draws its column on the deck its link goes up from and tg's hub sprite flat on the floor of the deck
/// above, where the link goes down (tg draws both the same; ModelPart.Deck). A lone hub keeps its column; one linked both ways (a middle deck) draws the column.
/// </summary>
public sealed class MultiDeckHubModelTests
{
    private static ModelEntry Entry(string id)
    {
        string root = Path.GetDirectoryName(ModelCatalog.Locate()!)!;
        return ModelCatalog.Parse(File.ReadAllText(Path.Combine(root, ModelCatalog.FileName))).Entries.Single(e => e.Id == id);
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    public void TheColumnRisesWhereTheLinkGoesUpAndTheFittingLiesWhereItGoesOnlyDown(bool above, bool below, bool column)
    {
        foreach ((string id, string housing, string fitting) in new[] { ("atmos_multideck_adapter", "hub_1_4", "fitting_1_4"), ("atmos_multideck_adapter", "model", "fitting"), ("cable_hub_multiz", "model", "fitting") })
        {
            ModelEntry entry = Entry(id);
            Assert.Equal(column, entry.Parts[housing].ShownOnDeck(above, below));
            Assert.Equal(!column, entry.Parts[fitting].ShownOnDeck(above, below));
            // the fitting lies where the housing would stand: the same layer's shift and port
            Assert.Equal(entry.Parts[housing].Offset, entry.Parts[fitting].Offset);
            Assert.Equal(entry.Parts[housing].Dirs, entry.Parts[fitting].Dirs);
            Assert.Equal(entry.Parts[housing].Overlays, entry.Parts[fitting].Overlays);
        }
    }

    [Fact]
    public void AnUnknownDeckIsRefused()
    {
        const string text = """{ "models": [ { "id": "x", "icons": ["a.dmi"], "rule": "single", "texture": "model", "source": { "width": 2.0, "height": 2.0 }, "fit": { "width": "tile", "height": "wall" }, "parts": { "model": { "file": "a.glb", "deck": "sideways" } } } ] }""";
        Assert.Throws<InvalidDataException>(() => ModelCatalog.Parse(text));
    }
}
