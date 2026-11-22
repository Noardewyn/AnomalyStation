using System.IO;
using Anomaly.Client.Shared.Models;

namespace Anomaly.Server.Tests.Client.Models;

/// <summary>
/// Gaps H2 (D333): the kitchen machines' models follow the icon states and overlays the translated machines show: the microwave's
/// back_off, back_open, back_on (door_charge while charging), mwbloody and mwb; the oven's and the range's door with tg's lid overlay
/// and their heat lamp while on; the griddle's knob per variant and state; the deep fryer's baskets down while frying; the gibber's
/// lights and jaws by its overlays; the processor's hatch open with its panel; the meat spike's spikes once built.
/// </summary>
public sealed class KitchenModelTests
{
    private static string Root => Path.GetDirectoryName(ModelCatalog.Locate()!)!;
    private static ModelCatalog Catalog => ModelCatalog.Parse(File.ReadAllText(Path.Combine(Root, ModelCatalog.FileName)));

    [Theory]
    [InlineData("back_off", null, "microwave_idle")]
    [InlineData("back_open", null, "microwave_open")]
    [InlineData("back_on", null, "microwave_cooking")]
    [InlineData("back_on", "door_charge", "microwave_charging")]
    [InlineData("mwbloody", null, "microwave_dirty")]
    [InlineData("mwb", null, "microwave_broken")]
    [InlineData("mw-o", null, "microwave_service")]
    public void TheMicrowaveShowsTgsState(string state, string overlay, string model)
    {
        ModelEntry entry = Catalog.Find("icons/obj/machines/microwave.dmi", state, overlay == null ? null : new[] { overlay })!;
        Assert.Equal(model, entry.Id);
    }

    [Theory]
    [InlineData("oven", "kitchen_oven")]
    [InlineData("range", "kitchen_range")]
    public void TheOvensDoorOpensWithTgsLidOverlayAndItsLampLightsWhileBaking(string baseState, string model)
    {
        ModelCatalog catalog = Catalog;
        ModelEntry off = catalog.Find("icons/obj/machines/kitchen.dmi", baseState + "_off")!;
        Assert.Equal(model, off.Id);
        Assert.Same(off, catalog.Find("icons/obj/machines/kitchen.dmi", baseState + "_on"));
        Assert.True(CrateLook.Open(off, baseState + "_off", new[] { baseState + "_lid_open" })); // open: oven.dm update_overlays
        Assert.False(CrateLook.Open(off, baseState + "_off", new[] { baseState + "_lid_closed" }));
        Assert.True(off.Parts["Heat_Lamp_" + baseState + "_on"].ShownIn(baseState + "_on"));
        Assert.False(off.Parts["Heat_Lamp_" + baseState + "_on"].ShownIn(baseState + "_off"));
    }

    [Theory]
    [InlineData("griddle1_off")]
    [InlineData("griddle2_on")]
    [InlineData("griddle3_on")]
    public void TheGriddleShowsItsVariantsKnobByState(string state)
    {
        ModelEntry griddle = Catalog.Find("icons/obj/machines/kitchen.dmi", state)!;
        Assert.Equal("griddle", griddle.Id);
        Assert.True(griddle.Parts["Control_" + state].ShownIn(state));
        foreach (var (name, part) in griddle.Parts)
        {
            if (name.StartsWith("Control_", System.StringComparison.Ordinal) && name != "Control_" + state)
                Assert.False(part.ShownIn(state), name);
        }
    }

    [Fact]
    public void TheDeepFryersBasketsGoDownWhileItFries()
    {
        ModelEntry fryer = Catalog.Find("icons/obj/machines/kitchen.dmi", "fryer_off")!;
        Assert.Equal("deep_fryer", fryer.Id);
        Assert.True(CrateLook.Open(fryer, "fryer_on", System.Array.Empty<string>())); // deep_fryer.dm update_icon_state: frying
        Assert.False(CrateLook.Open(fryer, "fryer_off", System.Array.Empty<string>()));
        Assert.True(fryer.Parts["Power_fryer_on"].ShownIn("fryer_on"));
        Assert.False(fryer.Parts["Power_fryer_on"].ShownIn("fryer_off"));
    }

    [Fact]
    public void TheGibbersLightsAndJawsFollowItsOverlays()
    {
        ModelEntry gibber = Catalog.Find("icons/obj/machines/kitchen.dmi", "grinder")!;
        Assert.Equal("gibber", gibber.Id);
        Assert.True(gibber.Parts["grinder_empty_Frame_0"].DrawnWith(new[] { "grinder_empty" }));
        Assert.True(gibber.Parts["grinder_loaded_Frame_0"].DrawnWith(new[] { "grinder_loaded" }));
        Assert.True(gibber.Parts["grinder_active_Frame_0"].DrawnWith(new[] { "grinder_active", "grinder_jaws_active" }));
        Assert.True(gibber.Parts["Grinding_Jaw_0"].DrawnWith(new[] { "grinder_active", "grinder_jaws_active" })); // the spinning jaws
        Assert.False(gibber.Parts["Grinding_Jaw_0"].DrawnWith(new[] { "grinder_loaded" }));
        Assert.True(gibber.Parts["Grinding_Jaw_0_still"].DrawnWith(new[] { "grinder_loaded" }));
    }

    [Fact]
    public void TheProcessorsHatchOpensWithItsPanel()
    {
        ModelEntry processor = Catalog.Find("icons/obj/machines/kitchen.dmi", "processor")!;
        Assert.Equal("food_processor", processor.Id);
        Assert.True(CrateLook.Open(processor, "processor_open", System.Array.Empty<string>())); // processor.dm update_icon_state: panel_open
        Assert.False(CrateLook.Open(processor, "processor", System.Array.Empty<string>()));
    }

    [Fact]
    public void TheMeatSpikeShowsItsSpikesOnceBuilt()
    {
        ModelEntry spike = Catalog.Find("icons/obj/service/kitchen.dmi", "spike")!;
        Assert.Equal("meat_spike", spike.Id);
        Assert.True(spike.Parts["Installed_Spikes"].ShownIn("spike"));
        Assert.False(spike.Parts["Installed_Spikes"].ShownIn("spikeframe"));
    }
}
