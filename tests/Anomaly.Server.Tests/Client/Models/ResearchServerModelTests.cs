using System.IO;
using Anomaly.Client.Shared.Models;

namespace Anomaly.Server.Tests.Client.Models;

/// <summary>
/// Gaps RD2: the master R&amp;D server shows tg's overlays (server.dm:126, :198, :258): the hazard stripes of the theft objective's
/// server and the front panel hanging open once its screws are out, each a node of research_server_master_overlays.glb on the
/// server's model (tools/models/make_server_master_overlays.py); the server's status lights follow its working, halted and
/// unpowered pictures.
/// </summary>
public sealed class ResearchServerModelTests
{
    private static string Root => Path.GetDirectoryName(ModelCatalog.Locate()!)!;
    private static ModelCatalog Catalog => ModelCatalog.Parse(File.ReadAllText(Path.Combine(Root, ModelCatalog.FileName)));

    [Theory]
    [InlineData("Objective_Stripes", "RD-server-objective-stripes")]
    [InlineData("HDD_Panel_Open", "RD-server-hdd-panel-open")]
    public void TheMasterServersOverlaysShowOnItsModel(string part, string overlay)
    {
        ModelEntry server = Catalog.Find("icons/obj/machines/research.dmi", "RD-server-on")!;
        Assert.Equal("research_server", server.Id);
        Assert.True(server.Parts[part].DrawnWith(new[] { overlay }));
        Assert.False(server.Parts[part].DrawnWith(new[] { "RD-server-on" }));
        GlbFile file = GlbFile.Read(File.ReadAllBytes(Path.Combine(Root, server.Parts[part].File)));
        Assert.Contains(file.MeshNodes, n => n.Name == part);
    }

    [Theory]
    [InlineData("RD-server-on")]
    [InlineData("RD-server-halt")]
    [InlineData("RD-server-off")]
    [InlineData("RD-server-on_t")]
    public void EachServerPictureIsTheServerModel(string state)
    {
        Assert.Equal("research_server", Catalog.Find("icons/obj/machines/research.dmi", state)!.Id);
    }
}
