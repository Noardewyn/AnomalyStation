using System.IO;
using Anomaly.Client.Shared.Models;

namespace Anomaly.Server.Tests.Client.Models;

/// <summary>
/// The Emergency NanoMed (tg's wall-mounted vending/wallmed) is a wall model in every state tg gives it, so its wall decal draws the model, not tg's picture
/// (a wall decal is drawn by a wall model only); its cabinet and front are nodes of its glb.
/// </summary>
public sealed class WallmedModelTests
{
    private static string Root => Path.GetDirectoryName(ModelCatalog.Locate()!)!;
    private static ModelCatalog Catalog => ModelCatalog.Parse(File.ReadAllText(Path.Combine(Root, ModelCatalog.FileName)));

    [Theory]
    [InlineData("wallmed")]
    [InlineData("wallmed-off")]
    [InlineData("wallmed-broken")]
    public void TheNanoMedIsAWallModelInEveryState(string state)
    {
        ModelEntry wallmed = Catalog.Find("icons/obj/machines/vending.dmi", state)!;
        Assert.Equal("vending_wallmed", wallmed.Id);
        Assert.Equal(ModelRule.Wall, wallmed.Rule);
        GlbFile file = GlbFile.Read(File.ReadAllBytes(Path.Combine(Root, wallmed.Parts["model"].File)));
        foreach (ModelPart part in wallmed.Parts.Values)
            Assert.Contains(file.MeshNodes, n => n.Name == part.Node);
    }
}
