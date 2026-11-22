using System.IO;
using System.Globalization;
using System.Numerics;
using System.Text.Json;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.Models;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Client.Render;

namespace Anomaly.Server.Tests.Client.Models;

public sealed class TankAndSmesModelTests
{
    private static string Root => Path.GetDirectoryName(ModelCatalog.Locate()!)!;
    private static ModelCatalog Catalog => ModelCatalog.Parse(File.ReadAllText(Path.Combine(Root, ModelCatalog.FileName)));
    private static GlbFile Read(string path) => GlbFile.Read(File.ReadAllBytes(Path.Combine(Root, path)));
    private static ModelMeshData Build(ModelEntry entry, ModelPart part) => ModelMesh.Build(Read(part.File), entry, part, 2, 2, .75f);

    [Theory]
    [InlineData("water", "Blue")]
    [InlineData("fuel", "Orange")]
    [InlineData("water_high", "Green")]
    [InlineData("fuel_high", "Orange")]
    [InlineData("foam", "White")]
    [InlineData("water_stationary", "Blue")]
    [InlineData("fuel_stationary", "Orange")]
    [InlineData("tank_stationary", "Ivory")]
    public void TankTypesKeepTheirOwnGeometryAndColourBelowTheCeiling(string state, string color)
    {
        ModelEntry entry = Catalog.Find("icons/obj/medical/chemical_tanks.dmi", state)!;
        ModelPart body = entry.Parts["model"];
        Assert.Equal("station_props/chemical_tanks/" + state + ".glb", body.File);
        GlbFile file = Read(body.File);
        var vessel = file.MeshNodes.Single(n => n.Name.StartsWith("Main_Vessel", StringComparison.Ordinal));
        Assert.Contains(color, file.Materials[vessel.Primitives[0].Material].Name);
        Vector3[] positions = Build(entry, body).Positions;
        Assert.InRange(positions.Min(p => p.Y), -.001f, .001f);
        Assert.InRange(positions.Max(p => p.Y), 1.599f, 1.601f);
        Assert.All(positions, p => Assert.InRange(Math.Max(Math.Abs(p.X), Math.Abs(p.Z)), 0, .99f));
    }

    [Fact]
    public void StorageMixtureFollowsItsOwnOverlayColourAndKeepsItsAuthoredOpacity()
    {
        ModelEntry entry = Catalog.Find("icons/obj/medical/chemical_tanks.dmi", "tank_stationary")!;
        ModelPart mixture = entry.Parts["mixture"];
        Assert.False(mixture.DrawnWith(Array.Empty<string>()));
        Assert.True(mixture.DrawnWith(new[] { "tank_chem_overlay" }));
        var source = new TestTables();
        TableRef red = source.Add("icons/obj/medical/chemical_tanks.dmi", "tank_chem_overlay", c => c.Rgba = 0xE02040FF);
        TableRef blue = source.Add("icons/obj/medical/chemical_tanks.dmi", "tank_chem_overlay", c => c.Rgba = 0x2040E0FF);
        var first = new AppearanceEntry(); first.Overlays.Add(red);
        var second = new AppearanceEntry(); second.Overlays.Add(blue);
        Assert.Equal(0xE02040FFu, ModelOverlays.Tint(source, first, mixture.TintOverlay));
        Assert.Equal(0x2040E0FFu, ModelOverlays.Tint(source, second, mixture.TintOverlay));
        GlbMaterial material = Assert.Single(Read(mixture.File).Materials);
        Assert.InRange(material.BaseColor.W, .619f, .621f);
        Assert.Equal(0xE02040FFu, ModelColors.Swatch(material.Name, material.BaseColor, null, ModelOverlays.Tint(source, first, mixture.TintOverlay)));
    }

    [Fact]
    public void SmesChargeInputAndOutputRemainIndependent()
    {
        ModelCatalog catalog = Catalog;
        ModelEntry entry = catalog.Find("icons/obj/machines/engine/other.dmi", "smes")!;
        Assert.Same(entry, catalog.Find("icons/obj/machines/engine/other.dmi", "smes-o"));
        for (int charge = 0; charge <= 5; charge++)
        foreach (bool input in new[] { false, true })
        foreach (bool output in new[] { false, true })
        {
            var overlays = new List<string> { "smes-oc" + (input ? 1 : 0), "smes-op" + (output ? 1 : 0) };
            if (charge > 0) overlays.Add("smes-og" + charge);
            ModelPart[] drawn = entry.Parts.Values.Where(p => p.DrawnWith(overlays)).ToArray();
            Assert.Equal(charge, drawn.Count(p => p.Node.StartsWith("Charge_Level_", StringComparison.Ordinal) && p.Glow > 0f));
            Assert.Single(drawn, p => p.Node.EndsWith("_Output_Highlight", StringComparison.Ordinal));
            Assert.Equal(input ? 1 : 2, drawn.Count(p => p.Node.EndsWith("_Input_Pulse", StringComparison.Ordinal)));
        }
        // The source sends no display overlays for an open panel, broken machine, or disabled display.
        Assert.DoesNotContain(entry.Parts.Values.Where(p => p.DrawnWith(Array.Empty<string>())), p => p.Light != null);
        Assert.True(CrateLook.Open(entry, "smes-o", Array.Empty<string>()));
        Assert.False(CrateLook.Open(entry, "smes", Array.Empty<string>()));
        ModelPart low = entry.Parts["chargeLow"], pulse = entry.Parts["Lower_Red_Input_Pulse"];
        Assert.True(low.LitAt(.99)); Assert.False(low.LitAt(1)); Assert.True(low.LitAt(2));
        Assert.True(pulse.LitAt(.49)); Assert.False(pulse.LitAt(.5)); Assert.True(pulse.LitAt(1));
    }

    [Fact]
    public void SmesLensesAreDarkWithoutSignalAndFollowSteadyOrBlinkingSignals()
    {
        ModelEntry entry = Catalog.Find("icons/obj/machines/engine/other.dmi", "smes")!;
        foreach (bool input in new[] { false, true })
        foreach (bool output in new[] { false, true })
        foreach (double seconds in new[] { .1, .6 })
        foreach (string lens in new[] { "Upper_Green_Lens", "Lower_Red_Lens" })
        {
            string[] overlays = { "smes-oc" + (input ? 1 : 0), "smes-op" + (output ? 1 : 0) };
            ModelPart face = Assert.Single(entry.Parts.Values, p => p.Node == lens && p.DrawnWith(overlays));
            bool upper = lens.StartsWith("Upper", StringComparison.Ordinal);
            bool steady = upper ? output : !output;
            bool pulse = upper ? !input : true;
            bool on = steady || pulse && seconds < .5;
            Vector3? color = face.LitAt(seconds) ? face.Color : face.OffColor;
            Assert.NotNull(color);
            uint swatch = ModelColors.Swatch(null, Vector4.One, null, color: color);
            Assert.Equal(on ? upper ? 0x33CC33FFu : 0xCC3333FFu : upper ? 0x005200FFu : 0x520000FFu, swatch);
            Assert.Equal(on, face.LitAt(seconds) && face.Glow > 0f);
        }
        ModelPart[] off = entry.Parts.Values.Where(p => p.Node.EndsWith("_Lens", StringComparison.Ordinal) && p.DrawnWith(Array.Empty<string>())).ToArray();
        Assert.Equal(2, off.Length);
        Assert.All(off, p =>
        {
            Assert.Equal(0f, p.Glow);
            Assert.Equal(p.Node.StartsWith("Upper", StringComparison.Ordinal) ? 0x005200FFu : 0x520000FFu,
                ModelColors.Swatch(null, Vector4.One, null, color: p.Color));
        });
    }

    [Fact]
    public void SmesLightColoursMatchTheirIndicatorsAndBlinkOffColoursEmitNothing()
    {
        ModelEntry entry = Catalog.Find("icons/obj/machines/engine/other.dmi", "smes")!;
        foreach (ModelPart part in entry.Parts.Values.Where(p => p.Light != null))
        {
            Assert.NotNull(part.Color);
            Assert.Equal(part.Color, part.Light.Color);
            Assert.True(part.Glow > 0f);
            if (part.Node.StartsWith("Charge_Level_", StringComparison.Ordinal))
                Assert.Equal(ChargePalette()[int.Parse(part.Node[^1..]) - 1].Lit, ModelColors.Swatch(null, Vector4.One, null, color: part.Color));
            else if (part.Node.StartsWith("Upper", StringComparison.Ordinal))
                Assert.Equal(0x33CC33FFu, ModelColors.Swatch(null, Vector4.One, null, color: part.Color));
            else
                Assert.Equal(0xCC3333FFu, ModelColors.Swatch(null, Vector4.One, null, color: part.Color));
            if (part.BlinkOn > 0f)
            {
                Assert.NotNull(part.OffColor);
                uint dark = part.Node.StartsWith("Charge_Level_", StringComparison.Ordinal) ? ChargePalette()[int.Parse(part.Node[^1..]) - 1].Dim
                    : part.Node.StartsWith("Upper", StringComparison.Ordinal) ? 0x005200FFu : 0x520000FFu;
                Assert.Equal(dark, ModelColors.Swatch(null, Vector4.One, null, color: part.OffColor));
            }
        }
    }

    private static (uint Lit, uint Dim)[] ChargePalette()
    {
        using var source = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "machines/smes/source_states.json")));
        static uint Rgba(string value) => uint.Parse(value[1..], NumberStyles.HexNumber) << 8 | 255u;
        return source.RootElement.GetProperty("charge_palette").EnumerateArray()
            .Select(p => (Rgba(p.GetProperty("color").GetString()!), Rgba(p.GetProperty("inactive_color").GetString()!))).ToArray();
    }

    [Fact]
    public void SmesGaugeMatchesTheSourcePaletteAndOnlyItsLowestLevelBlinks()
    {
        ModelEntry entry = Catalog.Find("icons/obj/machines/engine/other.dmi", "smes")!;
        var palette = ChargePalette();
        Assert.Equal(new[] { 0xCC0000FFu, 0xFF6600FFu, 0xFFCC00FFu, 0x83D316FFu, 0x00CC00FFu }, palette.Select(p => p.Lit));
        using var source = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "machines/smes/source_states.json")));
        var states = source.RootElement.GetProperty("states").EnumerateArray().ToArray();
        for (int charge = 0; charge <= 5; charge++)
        foreach (double seconds in new[] { .1, 1.1 })
        for (int level = 1; level <= 5; level++)
        {
            string[] overlays = charge == 0 ? Array.Empty<string>() : new[] { "smes-og" + charge };
            ModelPart segment = Assert.Single(entry.Parts.Values, p => p.Node == "Charge_Level_" + level && p.DrawnWith(overlays));
            bool lit = level <= charge && (charge != 1 || seconds < 1);
            Vector3? color = segment.LitAt(seconds) ? segment.Color : segment.OffColor;
            Assert.Equal(lit ? palette[level - 1].Lit : palette[level - 1].Dim, ModelColors.Swatch(null, Vector4.One, null, color: color));
            Assert.Equal(lit, segment.Glow > 0f && segment.LitAt(seconds));
            if (charge > 0)
            {
                JsonElement state = states.Single(s => s.GetProperty("name").GetString() == "smes-og" + charge);
                Assert.Equal(charge == 1 ? 2 : 1, state.GetProperty("frames").GetInt32());
                if (charge == 1 && level == 1)
                    Assert.Equal(new[] { segment.BlinkOn * 10, segment.BlinkOff * 10 }, state.GetProperty("delay_deciseconds").EnumerateArray().Select(d => d.GetSingle()));
                else
                    Assert.Equal(0f, segment.BlinkOn);
            }
        }
    }

    [Fact]
    public void SmesDoorAndItsDisplaysShareOneHingeAndMissingNormalsAreFlat()
    {
        ModelEntry entry = Catalog.Find("icons/obj/machines/engine/other.dmi", "smes")!;
        Vector3[] pivots = entry.Parts.Values.Where(p => p.Pivot).Select(p => Build(entry, p).Pivot).ToArray();
        Assert.True(pivots.Length > 20);
        Assert.All(pivots, pivot => Assert.InRange(Vector3.Distance(pivots[0], pivot), 0, .00001f));
        Assert.All(entry.Parts.Values.Where(p => p.Pivot), p => Assert.Equal(112f, p.Degrees));
        GlbFile file = Read("machines/smes/smes.glb");
        foreach (GlbPrimitive primitive in file.MeshNodes.SelectMany(n => n.Primitives))
        {
            Assert.All(primitive.Normals, normal => Assert.InRange(normal.Length(), .999f, 1.001f));
            for (int i = 0; i < primitive.Indices.Length; i += 3)
            {
                int a = primitive.Indices[i], b = primitive.Indices[i + 1], c = primitive.Indices[i + 2];
                Vector3 face = Vector3.Normalize(Vector3.Cross(primitive.Positions[b] - primitive.Positions[a], primitive.Positions[c] - primitive.Positions[a]));
                Assert.InRange(Vector3.Distance(face, primitive.Normals[a]), 0, .00001f);
            }
        }
        Assert.True(entry.FlatColors);
    }
}
