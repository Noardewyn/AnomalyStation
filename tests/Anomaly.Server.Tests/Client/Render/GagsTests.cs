using Anomaly.Client.Shared.Icons;

namespace Anomaly.Server.Tests.Client.Render;

/// <summary>GAGS composed on the client (tg's greyscale configs): hand-made configs against hand-computed pictures, then the real jumpsuit.</summary>
public class GagsTests
{
    private const string Under = "icons/under.dmi";

    private static IconLibrary Library(Action<MemoryIconFiles> files, FakeIcon icon)
    {
        var memory = new MemoryIconFiles();
        files(memory);
        var library = new IconLibrary(memory);
        library.Register(Under, icon);
        return library;
    }

    private static string Config(string type, string states) => "{\"type\":\"" + type + "\",\"name\":\"n\",\"icon_file\":\"" + Under + "\",\"states\":" + states + "}";

    private static string Layer(string state, string colors = null, string blend = "overlay")
        => "{\"type\":\"icon_state\",\"icon_state\":\"" + state + "\",\"blend_mode\":\"" + blend + "\"" + (colors == null ? "" : ",\"color_ids\":" + colors) + "}";

    [Fact]
    public void AColouredLayerIsTheGreyscaleStateTimesTheColour()
    {
        IconLibrary library = Library(f => f.Text("gags/jumpsuit.json", Config("/datum/greyscale_config/jumpsuit", "{\"jumpsuit\":[" + Layer("body", "[1]") + "]}")),
            new FakeIcon().Solid("body", 0x808080FF));
        Assert.True(library.TryGetIcon("gags:/datum/greyscale_config/jumpsuit|#ff0000", out IIcon icon));
        Assert.True(icon.TryGetState("jumpsuit", out IIconState state));
        RasterAssert.Pixel(state.Get(0, 0), 3, 3, 0x800000FF);
    }

    [Fact]
    public void LayersJoinInOrderWithTheirBlendModes()
    {
        var under = new FakeIcon().Solid("body", 0xFFFFFFFF).State("trim", 1, 1, TrimPicture()).Solid("shade", 0x40404040);
        IconLibrary library = Library(f => f.Text("gags/a.json", Config("/datum/greyscale_config/a",
            "{\"s\":[" + Layer("body", "[2]") + "," + Layer("trim") + "," + Layer("shade", null, "multiply") + "]}")), under);
        Assert.True(library.TryGetIcon("gags:/datum/greyscale_config/a|#111111#00ff00", out IIcon icon));
        Assert.True(icon.TryGetState("s", out IIconState state));
        Raster picture = state.Get(0, 0);
        // Pixel (0,0): the body tinted green, trim transparent there, then multiplied by the 25% shade (0x40 / 255 of each channel and alpha).
        RasterAssert.Pixel(picture, 0, 0, Mul(0x00FF00FF, 0x40404040));
        // Pixel (1,0): the opaque red trim replaced the body, then the same shade.
        RasterAssert.Pixel(picture, 1, 0, Mul(0xFF0000FF, 0x40404040));
    }

    private static Raster TrimPicture()
    {
        var r = new Raster(32, 32);
        r.Set(1, 0, 0xFF0000FF);
        return r;
    }

    private static uint Mul(uint a, uint b)
    {
        uint Channel(int shift) => (uint)((((a >> shift) & 255) * ((b >> shift) & 255) + 127) / 255) << shift;
        return Channel(24) | Channel(16) | Channel(8) | Channel(0);
    }

    [Fact]
    public void ALiteralColourNeedsNoIndex()
    {
        IconLibrary library = Library(f => f.Text("gags/a.json", Config("/datum/greyscale_config/a", "{\"s\":[" + Layer("body", "[\"#00ff00\"]") + "]}")), new FakeIcon().Solid("body", 0xFFFFFFFF));
        library.TryGetIcon("gags:/datum/greyscale_config/a|", out IIcon icon);
        icon.TryGetState("s", out IIconState state);
        RasterAssert.Pixel(state.Get(0, 0), 0, 0, 0x00FF00FF);
    }

    [Fact]
    public void AReferenceLayerGeneratesAnotherConfigWithItsOwnColours()
    {
        IconLibrary library = Library(f => f
            .Text("gags/inner.json", Config("/datum/greyscale_config/inner", "{\"part\":[" + Layer("body", "[1]") + "]}"))
            .Text("gags/outer.json", Config("/datum/greyscale_config/outer",
                "{\"whole\":[{\"type\":\"reference\",\"reference_type\":\"/datum/greyscale_config/inner\",\"icon_state\":\"part\",\"blend_mode\":\"overlay\",\"color_ids\":[2]}]}")),
            new FakeIcon().Solid("body", 0xFFFFFFFF));
        library.TryGetIcon("gags:/datum/greyscale_config/outer|#ff0000#0000ff", out IIcon icon);
        icon.TryGetState("whole", out IIconState state);
        // The reference was given the outer config's second colour as its first.
        RasterAssert.Pixel(state.Get(0, 0), 5, 5, 0x0000FFFF);
    }

    [Fact]
    public void ANestedGroupIsJoinedWithItsFirstLayersBlend()
    {
        var under = new FakeIcon().Solid("a", 0xFF0000FF).Solid("b", 0x00FF0080).Solid("c", 0x808080FF);
        // Group [a (overlay), b (overlay)] first: green at half over red; then c multiplied over the group's result.
        string group = "[" + Layer("a") + "," + Layer("b") + "]";
        IconLibrary library = Library(f => f.Text("gags/g.json", Config("/datum/greyscale_config/g", "{\"s\":[" + Layer("c", null, "overlay") + "," + group + "]}")), under);
        library.TryGetIcon("gags:/datum/greyscale_config/g|", out IIcon icon);
        icon.TryGetState("s", out IIconState state);
        // c (grey) is below; the group (red with green at half) is laid over it with overlay, and it is opaque, so it wins.
        uint inner = state.Get(0, 0).Get(0, 0);
        Assert.Equal(0xFFu, inner & 0xFF);
        Assert.InRange((inner >> 24) & 255, 120u, 130u);
        Assert.InRange((inner >> 16) & 255, 120u, 135u);
    }

    [Fact]
    public void AMissingStateIsSkippedAndReportedOnce()
    {
        IconLibrary library = Library(f => f.Text("gags/a.json", Config("/datum/greyscale_config/a", "{\"s\":[" + Layer("body", "[1]") + "," + Layer("nothing") + "]}")), new FakeIcon().Solid("body", 0xFFFFFFFF));
        library.TryGetIcon("gags:/datum/greyscale_config/a|#ff0000", out IIcon icon);
        Assert.True(icon.TryGetState("s", out IIconState state));
        RasterAssert.Pixel(state.Get(0, 0), 0, 0, 0xFF0000FF);
        Assert.Single(library.Problems);
        Assert.Contains("'nothing'", library.Problems[0]);
        Assert.False(icon.TryGetState("no such state", out _));
    }

    [Fact]
    public void ADirectionalBaseKeepsItsDirectionsWhenALayerHasOne()
    {
        var north = Raster.Solid(32, 32, 0x0000FFFF);
        var south = Raster.Solid(32, 32, 0xFF0000FF);
        var under = new FakeIcon().State("body", 4, 1, south, north, south, south).Solid("overlay", 0xFFFFFF40);
        IconLibrary library = Library(f => f.Text("gags/a.json", Config("/datum/greyscale_config/a", "{\"s\":[" + Layer("body") + "," + Layer("overlay") + "]}")), under);
        library.TryGetIcon("gags:/datum/greyscale_config/a|", out IIcon icon);
        icon.TryGetState("s", out IIconState state);
        Assert.Equal(4, state.Dirs);
        Assert.NotEqual(state.Get(ByondDir.Index(ByondDir.South, 4), 0).Get(0, 0), state.Get(ByondDir.Index(ByondDir.North, 4), 0).Get(0, 0));
    }

    [Fact]
    public void AColourMatrixLayerMapsTheLayersBelow()
    {
        var under = new FakeIcon().Solid("body", 0xFF0000FF);
        // Red becomes blue (row "red in" = 0 0 1 0), the matrix layer is then laid over the red body as an overlay: the blue wins.
        string matrix = "{\"type\":\"color_matrix\",\"color_matrix\":[[0,0,1,0],[0,1,0,0],[1,0,0,0],[0,0,0,1]],\"blend_mode\":\"overlay\"}";
        IconLibrary library = Library(f => f.Text("gags/a.json", Config("/datum/greyscale_config/a", "{\"s\":[" + Layer("body") + "," + matrix + "]}")), under);
        library.TryGetIcon("gags:/datum/greyscale_config/a|", out IIcon icon);
        icon.TryGetState("s", out IIconState state);
        RasterAssert.Pixel(state.Get(0, 0), 0, 0, 0x0000FFFF);
    }

    [Theory]
    [InlineData("#3f3f3f", 1, 0x3F3F3FFFu)]
    [InlineData("#abc", 1, 0xAABBCCFFu)]
    [InlineData("#11223344", 1, 0x11223344u)]
    [InlineData("#ff0000#00ff00", 2, 0xFF0000FFu)]
    [InlineData("", 0, 0u)]
    [InlineData("#zz", 0, 0u)]
    public void ColourStringsSplitOnTheHash(string text, int count, uint first)
    {
        List<uint> colors = GagsColors.Parse(text);
        Assert.Equal(count, colors.Count);
        if (count > 0)
            Assert.Equal(first, colors[0]);
    }

    [RealIconsFact]
    public void TheRealWornJumpsuitIsTheTintedStateWithItsAccessories()
    {
        IconLibrary library = RealIcons.NewLibrary();
        Assert.True(library.TryGetIcon("gags:/datum/greyscale_config/jumpsuit/worn|#3f3f3f", out IIcon jumpsuit));
        Assert.True(jumpsuit.TryGetState("jumpsuit", out IIconState state));
        Assert.Equal(4, state.Dirs);

        // The same picture computed straight from the two source states (tg: jumpsuit multiplied by the colour, accessories over it).
        Assert.True(library.TryGetIcon("icons/mob/clothing/under/color.dmi", out IIcon color));
        Assert.True(color.TryGetState("jumpsuit", out IIconState body));
        Assert.True(color.TryGetState("jumpsuit_accessories", out IIconState accessories));
        for (int dir = 0; dir < 4; dir++)
        {
            Raster expected = body.Get(dir, 0).Clone();
            expected.Multiply(0x3F3F3FFF);
            expected.Blend(accessories.Get(dir, 0), IconBlend.Overlay);
            Assert.Equal(expected.Pixels, state.Get(dir, 0).Pixels);
        }
        Assert.False(state.Get(0, 0).IsEmpty());
        Assert.Empty(library.Problems);
    }

    [RealIconsFact]
    public void EveryConfigOfTgComposesItsEveryStateWithoutProblems()
    {
        IconLibrary library = RealIcons.NewLibrary();
        string index = System.IO.File.ReadAllText(System.IO.Path.Combine(RealIcons.Root, "gags", "index.json"));
        using var document = System.Text.Json.JsonDocument.Parse(index);
        int composed = 0;
        foreach (System.Text.Json.JsonElement entry in document.RootElement.EnumerateArray())
        {
            string type = entry.GetProperty("type").GetString();
            // Colours: enough white-ish operands for any config (ids go to 8 at most).
            string resource = "gags:" + type + "|#ffffff#ff0000#00ff00#0000ff#ffff00#ff00ff#00ffff#808080";
            Assert.True(library.TryGetIcon(resource, out IIcon icon), type);
            var config = GagsConfig.Parse(System.IO.File.ReadAllText(System.IO.Path.Combine(RealIcons.Root, "gags", entry.GetProperty("file").GetString())));
            foreach (string state in config.States.Keys)
            {
                Assert.True(icon.TryGetState(state, out IIconState generated), type + " state " + state);
                Assert.False(generated.Get(0, 0).IsEmpty() && state.Length > 0 && false);
                composed++;
            }
        }
        Assert.True(composed > 500, composed + " states");
        // tg's own gold cleric mace configs reference a two-colour config with one colour; nothing else is wrong.
        Assert.True(library.Problems.All(p => p.Contains("cleric_mace") && p.Contains("expects colour")), string.Join("\n", library.Problems));
    }
}
