using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Package E12a: the client's Crystallizer and Electrolyzer windows (tg's Crystallizer.tsx and Electrolyzer.tsx) without the engine.
/// The crystallizer's shows the power button, one button per recipe with the selected one lit, the gas input field, the progress
/// and the log-scaled temperature bars, the requirements text and a bar per gas in tg's gas colours, and sends tg's acts; the
/// electrolyzer's shows the cell's charge or a red None, its eject button only with the panel open and a cell, and its power button
/// only with a cell or anchored. Both are registered under their interface names.
/// </summary>
public class CrystallizerElectrolyzerWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(TguiInterface render, string name, string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, name, "device", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private const string CrystallizerData = "{\"on\":1,\"selected\":\"metal_h\",\"gas_input\":2.5,\"progress_bar\":40,\"internal_temperature\":700,"
        + "\"requirements\":\"To create Metallic Hydrogen you will need:\\n-300 moles of Hydrogen\",\"selected_recipes\":["
        + "{\"name\":\"Nothing\",\"id\":\"\"},{\"name\":\"\",\"id\":\"\"},{\"name\":\"Metallic Hydrogen\",\"id\":\"metal_h\"},{\"name\":\"Diamond\",\"id\":\"diamond\"}],"
        + "\"internal_gas_data\":[{\"name\":\"Hydrogen\",\"id\":\"hydrogen\",\"amount\":310.25},{\"name\":\"BZ\",\"id\":\"bz\",\"amount\":12}]}";

    [Fact]
    public void TheCrystallizerWindowShowsItsControlsProgressRequirementsAndGasesWithTgsActs()
    {
        var (context, tree, acts) = Render(Crystallizer.Render, "Crystallizer", CrystallizerData);
        Assert.Equal((500.0, 600.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "Controls", "Requirements and progress", "Gases" }, tree.Walk().Where(n => n.Kind == UiKind.Section).Select(n => n.Title));
        Assert.Equal(new[] { "Power", "Recipe", "Gas Input", "Progress", "Recipe", "Temperature", "Hydrogen", "BZ" },
            tree.Walk().Where(n => n.Kind == UiKind.LabeledItem).Select(n => n.Label));

        UiNode power = ByKey(tree, "power");
        Assert.Equal(("On", "power-off", true), (power.Text, power.Icon, power.Selected));
        // the recipes, the blank base recipe of tg's list among them; the selected one lit
        List<UiNode> recipes = tree.Walk().Where(n => n.Kind == UiKind.Button && n.Key.StartsWith("recipe")).ToList();
        Assert.Equal(new[] { "Nothing", "", "Metallic Hydrogen", "Diamond" }, recipes.Select(b => b.Text));
        Assert.Equal(new[] { false, false, true, false }, recipes.Select(b => b.Selected));
        UiNode input = ByKey(tree, "gas_input");
        Assert.Equal((UiKind.NumberInput, 2.5, "moles/s", 0.0, 250.0, 0.1), (input.Kind, input.Value, input.Unit, input.MinValue, input.MaxValue, input.Step));
        Assert.Equal("63px", input.Width);

        UiNode progress = ByKey(tree, "progress");
        Assert.Equal(0.4, progress.Value, 6);
        Assert.Equal("average", progress.Ranges.First(r => progress.Value >= r.From && progress.Value <= r.To).Color);
        Assert.Equal("To create Metallic Hydrogen you will need:\n-300 moles of Hydrogen", ByKey(tree, "requirements").Text);
        UiNode temperature = ByKey(tree, "temperature");
        Assert.Equal(("700 K", Math.Log2(716) - 4, Math.Log2(10016) - 4), (temperature.Text, temperature.Value, temperature.MaxValue));
        Assert.Equal("average", temperature.Ranges.First(r => temperature.Value >= r.From && temperature.Value < r.To).Color); // 600-5000 K

        UiNode h2 = ByKey(tree, "gas:hydrogen");
        Assert.Equal(("white", 310.25, 1000.0, "310.25 moles"), (h2.Color, h2.Value, h2.MaxValue, h2.Text));
        Assert.Equal(("mediumpurple", "12.00 moles"), (ByKey(tree, "gas:bz").Color, ByKey(tree, "gas:bz").Text));

        power.OnClick();
        recipes[3].OnClick();
        recipes[0].OnClick();
        input.OnChange(7.5);
        Assert.Equal(new (string, string)[]
        {
            ("power", null), ("recipe", "{\"mode\":\"diamond\"}"), ("recipe", "{\"mode\":\"\"}"), ("gas_input", "{\"gas_input\":7.5}"),
        }, acts);

        // off, nothing selected: Nothing (and the blank recipe of the same id) lit
        var (_, off, _) = Render(Crystallizer.Render, "Crystallizer",
            "{\"on\":0,\"selected\":\"\",\"gas_input\":0,\"progress_bar\":0,\"internal_temperature\":0,\"requirements\":\"Select a recipe to see the requirements\","
            + "\"selected_recipes\":[{\"name\":\"Nothing\",\"id\":\"\"}],\"internal_gas_data\":[]}");
        Assert.Equal(("Off", "times", false), (ByKey(off, "power").Text, ByKey(off, "power").Icon, ByKey(off, "power").Selected));
        Assert.True(ByKey(off, "recipe0").Selected);
        Assert.Equal("0 K", ByKey(off, "temperature").Text);
    }

    [Fact]
    public void TheElectrolyzerWindowShowsTheCellAndItsTwoButtonsWithTgsActs()
    {
        var (context, tree, acts) = Render(Electrolyzer.Render, "Electrolyzer",
            "{\"open\":1,\"on\":1,\"hasPowercell\":1,\"anchored\":0,\"powerLevel\":45}");
        Assert.Equal((400.0, 305.0), (context.WindowWidth, context.WindowHeight));
        UiNode section = tree.Walk().Single(n => n.Kind == UiKind.Section);
        Assert.Equal("Power", section.Title);
        UiNode eject = ByKey(section.Buttons, "eject");
        UiNode power = ByKey(section.Buttons, "power");
        Assert.Equal(("Eject Cell", "eject", false), (eject.Text, eject.Icon, eject.Disabled));
        Assert.Equal(("On", "power-off", true, false), (power.Text, power.Icon, power.Selected, power.Disabled));
        UiNode cell = ByKey(tree, "cell");
        Assert.Equal(0.45, cell.Value, 6);
        Assert.Equal("average", cell.Ranges.First(r => cell.Value >= r.From && cell.Value < r.To).Color);
        eject.OnClick();
        power.OnClick();
        Assert.Equal(new (string, string)[] { ("eject", null), ("power", null) }, acts);

        // no cell, unanchored, panel closed: both buttons disabled, the cell row says None in red
        var (_, none, _) = Render(Electrolyzer.Render, "Electrolyzer", "{\"open\":0,\"on\":0,\"hasPowercell\":0,\"anchored\":0}");
        UiNode noneSection = none.Walk().Single(n => n.Kind == UiKind.Section);
        Assert.True(ByKey(noneSection.Buttons, "eject").Disabled);
        Assert.Equal(("Off", "times", true), (ByKey(noneSection.Buttons, "power").Text, ByKey(noneSection.Buttons, "power").Icon, ByKey(noneSection.Buttons, "power").Disabled));
        UiNode row = none.Walk().Single(n => n.Kind == UiKind.LabeledItem);
        Assert.Equal(("Cell", "None", "bad"), (row.Label, row.Text, row.Color));
        // anchored: the power button works without a cell
        var (_, anchored, _) = Render(Electrolyzer.Render, "Electrolyzer", "{\"open\":0,\"on\":0,\"hasPowercell\":0,\"anchored\":1}");
        Assert.False(ByKey(anchored.Walk().Single(n => n.Kind == UiKind.Section).Buttons, "power").Disabled);
    }

    [Fact]
    public void BothWindowsAreRegisteredUnderTheirInterfaceNames()
    {
        Assert.Equal((TguiInterface)Crystallizer.Render, TguiInterfaces.Find("Crystallizer"));
        Assert.Equal((TguiInterface)Electrolyzer.Render, TguiInterfaces.Find("Electrolyzer"));
    }
}
