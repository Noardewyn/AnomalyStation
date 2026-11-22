using System.IO;
using Anomaly.Client.Shared.Models;

namespace Anomaly.Server.Tests.Client.Models;

/// <summary>
/// Gaps H4: the janitorial cart shows what it carries, tg's overlays of the cart (janitor.dm:359): its mop and push broom in their
/// clips, its trash bag (blue when bluespace), its spray bottle and light replacer, and one to four wet floor signs, each a
/// node of janitorial_cart_tools.glb at the cart model's own anchor (tools/models/make_cart_tools.py).
/// </summary>
public sealed class JanitorModelTests
{
    private static string Root => Path.GetDirectoryName(ModelCatalog.Locate()!)!;
    private static ModelCatalog Catalog => ModelCatalog.Parse(File.ReadAllText(Path.Combine(Root, ModelCatalog.FileName)));

    [Theory]
    [InlineData("Mop", "cart_mop")]
    [InlineData("Broom", "cart_broom")]
    [InlineData("Trash_Bag", "cart_garbage")]
    [InlineData("Trash_Bag_Bluespace", "cart_bluespace_garbage")]
    [InlineData("Spray", "cart_spray")]
    [InlineData("Light_Replacer", "cart_replacer")]
    public void EachThingOnTheCartShowsWithItsOverlay(string part, string overlay)
    {
        ModelEntry cart = Catalog.Find("icons/obj/service/janitor.dmi", "cart")!;
        Assert.Equal("janitorial_cart", cart.Id);
        Assert.True(cart.Parts[part].DrawnWith(new[] { overlay }));
        Assert.False(cart.Parts[part].DrawnWith(new[] { "cart_water" }));
        GlbFile file = GlbFile.Read(File.ReadAllBytes(Path.Combine(Root, cart.Parts[part].File)));
        Assert.Contains(file.MeshNodes, n => n.Name == part);
    }

    [Theory]
    [InlineData("cart_sign1", 1)]
    [InlineData("cart_sign2", 2)]
    [InlineData("cart_sign3", 3)]
    [InlineData("cart_sign4", 4)]
    public void TheSignRackShowsAsManySignsAsTgsOverlayCounts(string overlay, int signs)
    {
        ModelEntry cart = Catalog.Find("icons/obj/service/janitor.dmi", "cart")!;
        for (int i = 1; i <= 4; i++)
            Assert.Equal(i <= signs, cart.Parts["Sign_" + i].DrawnWith(new[] { overlay }));
    }
}
