using System.IO;
using Anomaly.Client.Shared.Models;

namespace Anomaly.Server.Tests.Client.Models;

/// <summary>
/// Gaps RD1: the fabricators' pictures that tg's code sets are drawn by their models: the autolathe's, the protolathe's (and the
/// techfab's, which shares its sprite) and the circuit imprinter's idle, printing (tg's production_animation) and open-panel states, the
/// open panel swinging the service hatch, and a department's stripe overlay (protolathe_stripe, _t with the panel open) showing the
/// stripe part.
/// </summary>
public sealed class FabricatorModelTests
{
    private static string Root => Path.GetDirectoryName(ModelCatalog.Locate()!)!;
    private static ModelCatalog Catalog => ModelCatalog.Parse(File.ReadAllText(Path.Combine(Root, ModelCatalog.FileName)));

    [Theory]
    [InlineData("icons/obj/machines/lathes.dmi", "autolathe", "autolathe")]
    [InlineData("icons/obj/machines/lathes.dmi", "autolathe_n", "autolathe")]
    [InlineData("icons/obj/machines/lathes.dmi", "autolathe_t", "autolathe")]
    [InlineData("icons/obj/machines/research.dmi", "protolathe", "protolathe")]
    [InlineData("icons/obj/machines/research.dmi", "protolathe_n", "protolathe")]
    [InlineData("icons/obj/machines/research.dmi", "protolathe_t", "protolathe")]
    [InlineData("icons/obj/machines/research.dmi", "circuit_imprinter", "circuit_imprinter")]
    [InlineData("icons/obj/machines/research.dmi", "circuit_imprinter_ani", "circuit_imprinter")]
    [InlineData("icons/obj/machines/research.dmi", "circuit_imprinter_t", "circuit_imprinter")]
    public void EachPictureOfTheFabricatorsIsDrawnByItsModel(string icon, string state, string id)
    {
        ModelEntry entry = Catalog.Find(icon, state)!;
        Assert.Equal(id, entry.Id);
        Assert.Equal(state.EndsWith("_t", StringComparison.Ordinal), entry.OpenStates.Contains(state));
    }

    [Fact]
    public void TheDepartmentStripeShowsWithItsOverlay()
    {
        ModelEntry lathe = Catalog.Find("icons/obj/machines/research.dmi", "protolathe")!;
        ModelPart stripe = lathe.Parts["Department_Stripe_Frame_0"];
        Assert.True(stripe.DrawnWith(new[] { "protolathe_stripe" }));
        Assert.True(stripe.DrawnWith(new[] { "protolathe_stripe_t" }));
        Assert.False(stripe.DrawnWith(Array.Empty<string>()));
    }
}
