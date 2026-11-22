using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Engineering package E15: the client's plumbing windows (tg's ChemAcclimator, ChemAutomaticBuffer, ChemDisposer, ChemFilter,
/// ChemMixingChamber, ChemPress, ChemReactionChamber, ChemSplitter, ChemSynthesizer and RapidPlumbingDevice TSX) without the engine. They
/// read tg's ui_data the way the TSX does (sizes, labels, fields and their ranges, the selected buttons, the local amount and tab state)
/// and send tg's acts; the "plumbing-tgui" and "chemmaster" sheet classes resolve to tg's .dmi files and states as the asset datums insert
/// them.
/// </summary>
public class PlumbingWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string iface, Func<TguiContext, UiNode> render, string json, TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, iface, "machine", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private static string[] Labels(UiNode tree) => tree.Walk().Where(n => n.Kind == UiKind.LabeledItem).Select(n => n.Label).ToArray();

    [Fact]
    public void TheAcclimatorBufferDisposerAndSplitterShowTheirFieldsAndSendTheirActs()
    {
        var (context, tree, acts) = Render("ChemAcclimator", ChemAcclimator.Render, "{\"chem_temp\":300.5,\"target_temperature\":350,\"max_volume\":200,\"acclimate_state\":1}");
        Assert.Equal((320.0, 130.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "Current Temperature", "Target Temperature", "Buffer", "Current State" }, Labels(tree));
        Assert.Equal("300.5 K", tree.Walk().Single(n => n.Label == "Current Temperature").Text);
        Assert.Equal("Heating", tree.Walk().Single(n => n.Label == "Current State").Text);
        UiNode target = ByKey(tree, "target");
        Assert.Equal((350.0, "K", 0.0, 1000.0, 5.0), (target.Value, target.Unit, target.MinValue, target.MaxValue, target.Step));
        target.OnChange(400);
        ByKey(tree, "buffer").OnChange(50);
        Assert.Equal(new[] { ("set_target_temperature", "{\"temperature\":400}"), ("change_volume", "{\"volume\":50}") }, acts);

        (context, tree, acts) = Render("ChemAutomaticBuffer", ChemAutomaticBuffer.Render, "{\"threshold\":100,\"connections\":0}");
        Assert.Equal((250.0, 120.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("N/A", tree.Walk().Single(n => n.Label == "Connections").Text);
        ByKey(tree, "sync").OnClick();
        ByKey(tree, "threshold").OnChange(60);
        Assert.Equal(new[] { ("sync", (string)null), ("set_threshold", "{\"threshold\":60}") }, acts);

        (context, tree, acts) = Render("ChemDisposer", ChemDisposer.Render, "{\"enabled\":1,\"max_volume\":25,\"disposal_rate\":5}");
        Assert.Equal((320.0, 105.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(("On", true), (ByKey(tree, "power").Text, ByKey(tree, "power").Selected));
        Assert.Equal((0.1, 25.0, "u"), (ByKey(tree, "volume").MinValue, ByKey(tree, "volume").MaxValue, ByKey(tree, "volume").Unit));
        ByKey(tree, "power").OnClick();
        Assert.Equal(new[] { ("toggle_power", (string)null) }, acts);

        (context, tree, acts) = Render("ChemSplitter", ChemSplitter.Render, "{\"straight\":5,\"left\":3,\"right\":1,\"max_transfer\":9}");
        Assert.Equal(new[] { "Straight", "Left", "Right" }, Labels(tree));
        UiNode left = ByKey(tree, "left");
        Assert.Equal((3.0, 9.0, 0.05, "3.00"), (left.Value, left.MaxValue, left.Step, left.Format(3)));
        left.OnChange(4);
        Assert.Equal(new[] { ("set_amount", "{\"target\":\"left\",\"amount\":4}") }, acts);
    }

    [Fact]
    public void TheSynthesizerSelectsItsAmountAndReagentAndTheFilterListsItsSides()
    {
        var (context, tree, acts) = Render("ChemSynthesizer", ChemSynthesizer.Render,
            "{\"amount\":5,\"current_reagent\":\"Water\",\"possible_amounts\":[0,1,2,3,4,5],"
            + "\"chemicals\":[{\"title\":\"Water\",\"id\":\"Water\"},{\"title\":\"Silicon\",\"id\":\"Silicon\"}]}");
        Assert.Equal((300.0, 375.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "0", "1", "2", "3", "4", "5" }, tree.Walk().Where(n => n.Key?.StartsWith("amount-") == true).Select(n => n.Text));
        Assert.True(ByKey(tree, "amount-5").Selected);
        Assert.True(ByKey(tree, "chem-Water").Selected);
        Assert.False(ByKey(tree, "chem-Silicon").Selected);
        ByKey(tree, "amount-2").OnClick();
        ByKey(tree, "chem-Silicon").OnClick();
        Assert.Equal(new[] { ("amount", "{\"target\":2}"), ("select", "{\"reagent\":\"Silicon\"}") }, acts);

        (context, tree, acts) = Render("ChemFilter", ChemFilter.Render, "{\"left\":[\"Water\"],\"right\":[]}");
        Assert.Equal((500.0, 300.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "Right", "Left" }, tree.Walk().Where(n => n.Kind == UiKind.Section).Select(n => n.Title));
        Assert.Equal(("red", "yellow"), (ByKey(tree, "add-right").Color, ByKey(tree, "add-left").Color));
        ByKey(tree, "add-right").OnClick();
        ByKey(tree, "left-Water").OnClick();
        Assert.Equal(new[] { ("add", "{\"which\":\"right\"}"), ("remove", "{\"which\":\"left\",\"reagent\":\"Water\"}") }, acts);
    }

    [Fact]
    public void TheChambersShowTheirStateTheirReagentsAndAddWithTheLocalAmount()
    {
        const string mixing = "{\"reagents\":[{\"name\":\"Water\",\"volume\":20}],\"catalysts\":[],\"emptying\":0,\"temperature\":300.4,\"targetTemp\":300,\"isReacting\":0}";
        var local = new TguiLocalState();
        var (context, tree, acts) = Render("ChemMixingChamber", ChemMixingChamber.Render, mixing, local);
        Assert.Equal((290.0, 400.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "Conditions", "Settings" }, tree.Walk().Where(n => n.Kind == UiKind.Section).Select(n => n.Title));
        Assert.Equal(("Filling", "good"), (ByKey(tree, "state").Text, ByKey(tree, "state").Color));
        Assert.Equal("300 K", ByKey(tree, "temperature").Text);
        Assert.Equal("20", ByKey(tree, "reagent-volume-Water").Text);
        ByKey(tree, "quantity").OnChange(15);
        (_, tree, acts) = Render("ChemMixingChamber", ChemMixingChamber.Render, mixing, local);
        ByKey(tree, "add").OnClick();
        ByKey(tree, "remove-Water").OnClick();
        ByKey(tree, "target").OnChange(350);
        Assert.Equal(new[] { ("add", "{\"amount\":15}"), ("remove", "{\"chem\":\"Water\"}"), ("temperature", "{\"target\":350}") }, acts);

        (context, tree, acts) = Render("ChemReactionChamber", ChemReactionChamber.Render,
            "{\"reagents\":[{\"name\":\"Water\",\"volume\":20}],\"catalysts\":[{\"name\":\"Silicon\",\"volume\":5}],\"emptying\":1,\"temperature\":300,"
            + "\"targetTemp\":300,\"isReacting\":0,\"ph\":7,\"reagentAcidic\":5,\"reagentAlkaline\":9}");
        Assert.Equal((290.0, 570.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "Conditions", "Settings", "Catalysts" }, tree.Walk().Where(n => n.Kind == UiKind.Section).Select(n => n.Title));
        Assert.Equal(("Emptying", "bad"), (ByKey(tree, "state").Text, ByKey(tree, "state").Color));
        Assert.Equal(new[] { "Acidic pH limit", "Alkaline pH limit" }, Labels(tree));
        Assert.Equal((7.0, "7"), (ByKey(tree, "ph-gauge").Value, ByKey(tree, "ph").Text));
        ByKey(tree, "catalyst-Water").OnClick();
        ByKey(tree, "catremove-Silicon").OnClick();
        ByKey(tree, "acidic").OnChange(4);
        Assert.Equal(new[] { ("catalyst", "{\"chem\":\"Water\"}"), ("catremove", "{\"chem\":\"Silicon\"}"), ("acidic", "{\"target\":4}") }, acts);
    }

    [Fact]
    public void ThePressShowsItsCategoriesAndStylesFromTheChemmasterSheet()
    {
        const string pill = "objitemreagentcontainersapplicatorpillstyle";
        string json = "{\"min_volume\":5,\"current_volume\":10,\"pill_duration\":3,\"max_volume\":50,\"max_duration\":60,\"product_name\":\"factory\","
            + "\"packaging_type\":\"[0x9000001]\",\"packaging_category\":\"pills\",\"packaging_types\":["
            + "{\"cat_name\":\"pills\",\"products\":[{\"class_name\":\"chemmaster32x32 " + pill + "\",\"ref\":\"[0x9000001]\"}]},"
            + "{\"cat_name\":\"Bottles\",\"products\":[{\"class_name\":\"chemmaster32x32 objitemreagentcontainerscupbottle\",\"ref\":\"[0x9000002]\"}]}]}";
        var local = new TguiLocalState();
        var (context, tree, acts) = Render("ChemPress", ChemPress.Render, json, local);
        Assert.Equal((450.0, 340.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "Product", "Volume", "Duration", "Name", "Styles" }, Labels(tree));
        Assert.Equal((true, "Pills"), (ByKey(tree, "category-pills").Checkbox == true, ByKey(tree, "category-pills").Text));
        UiNode style = ByKey(tree, "style-[0x9000001]");
        Assert.Equal((TguiSpritesheets.Chemmaster32, pill, true), (style.DmIcon, style.DmIconState, style.Selected));
        Assert.True(TguiSpritesheets.TryResolve(style.DmIcon, style.DmIconState, out SpritesheetIcon icon));
        Assert.Equal(("icons/obj/medical/chemical.dmi", "pill0"), (icon.Files[0], icon.State));
        ByKey(tree, "name").OnCommit("cure");
        style.OnClick();
        Assert.Equal(new[] { ("change_product_name", "{\"name\":\"cure\"}"), ("change_product", "{\"ref\":\"[0x9000001]\"}") }, acts);
        // the Bottles tab: no duration field
        ByKey(tree, "category-Bottles").OnClick();
        (_, tree, _) = Render("ChemPress", ChemPress.Render, json, local);
        Assert.DoesNotContain("Duration", Labels(tree));
        Assert.Single(tree.Walk(), n => n.Key == "style-[0x9000002]");
    }

    [Fact]
    public void TheConstructorShowsItsColoursLayersMatterAndDesignsFromThePlumbingSheet()
    {
        string json = "{\"matterLeft\":200,\"silo_upgraded\":0,\"silo_enabled\":0,\"paint_colors\":{\"omni\":\"#FFFFFF\",\"red\":\"#FF0000\"},"
            + "\"piping_layer\":3,\"selected_color\":\"omni\",\"layer_icon\":\"plumbing_layer4\",\"selected_category\":\"Synthesizers\","
            + "\"selected_recipe\":\"chemical synthesizer\",\"categories\":["
            + "{\"cat_name\":\"Synthesizers\",\"recipes\":[{\"icon\":\"synthesizer\",\"name\":\"chemical synthesizer\"}]},"
            + "{\"cat_name\":\"Distributors\",\"recipes\":[{\"icon\":\"nduct\",\"name\":\"fluid duct\"},{\"icon\":\"manifold\",\"name\":\"layer manifold\"}]}]}";
        var local = new TguiLocalState();
        var (context, tree, acts) = Render("RapidPlumbingDevice", RapidPlumbingDevice.Render, json, local);
        Assert.Equal((480.0, 575.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "Color", "Layer", "Units Left" }, Labels(tree));
        Assert.True(ByKey(tree, "layer-3").Checkbox == true);
        Assert.True(ByKey(tree, "recipe-chemical synthesizer").Selected);
        Assert.Equal((TguiSpritesheets.Plumbing32, "plumbing_layer4"), (ByKey(tree, "layer-icon").DmIcon, ByKey(tree, "layer-icon").DmIconState));
        Assert.True(TguiSpritesheets.TryResolve(TguiSpritesheets.Plumbing32, "plumbing_layer4", out SpritesheetIcon layer));
        Assert.Equal("icons/hud/radial.dmi", layer.Files[0]);
        Assert.True(TguiSpritesheets.TryResolve(TguiSpritesheets.Plumbing32, "synthesizer", out SpritesheetIcon synth));
        Assert.Equal(("icons/obj/pipes_n_cables/hydrochem/plumbers.dmi", "synthesizer"), (synth.Files[0], synth.State));
        Assert.True(TguiSpritesheets.TryResolve(TguiSpritesheets.Plumbing32, "plumb", out SpritesheetIcon iv));
        Assert.Equal("icons/obj/medical/iv_drip.dmi", iv.Files[0]);
        ByKey(tree, "layer-2").OnClick();
        ByKey(tree, "color-red").OnClick();
        ByKey(tree, "tab-Distributors").OnClick();
        Assert.Equal(new[] { ("piping_layer", "{\"piping_layer\":2}"), ("color", "{\"paint_color\":\"red\"}") }, acts);
        (_, tree, acts) = Render("RapidPlumbingDevice", RapidPlumbingDevice.Render, json, local);
        Assert.Equal("Fluid Duct", ByKey(tree, "recipe-fluid duct").Children.Last().Text);
        ByKey(tree, "recipe-layer manifold").OnClick();
        Assert.Equal(new[] { ("recipe", "{\"category\":\"Distributors\",\"id\":1}") }, acts);
    }
}
