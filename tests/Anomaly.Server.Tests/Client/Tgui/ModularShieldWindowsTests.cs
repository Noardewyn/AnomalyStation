using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Engineering package E10: the client's ModularShieldGen and ModularShieldConsole windows (tg's ModularShieldGen.tsx and
/// ModularShieldConsole.tsx) without the engine. The generator's window shows the strength, regeneration and radius bars with tg's
/// ranges and sends set_radius, toggle_exterior and toggle_shields; the console's lists the linked generators and sends rename and
/// toggle_shields with the generator's id.
/// </summary>
public class ModularShieldWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string name, TguiInterface render, string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, name, "shield", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private const string Ready = "{\"max_radius\":3,\"current_radius\":3,\"max_strength\":60,\"max_regeneration\":5,\"current_regeneration\":5,"
        + "\"current_strength\":60,\"active\":0,\"recovering\":0,\"exterior_only\":0,\"initiating_field\":0}";

    [Fact]
    public void TheGeneratorWindowShowsTgsBarsAndSendsItsThreeActs()
    {
        var (context, tree, acts) = Render("ModularShieldGen", ModularShieldGen.Render, Ready);
        Assert.Equal((690.0, 225.0, "Modular Shield Generator"), (context.WindowWidth, context.WindowHeight, context.WindowTitle));
        Assert.Equal(new[] { "Shield Strength", "Regeneration and Radius", "Settings" }, tree.Walk().Where(n => n.Kind == UiKind.Section && n.Title != null).Select(n => n.Title));
        UiNode strength = ByKey(tree, "strength");
        Assert.Equal((60.0, 60.0, "60/60"), (strength.Value, strength.MaxValue, strength.Text));
        Assert.Equal(new[] { ("good", 45.0, 60.0), ("average", 15.0, 45.0), ("bad", 0.0, 15.0) }, strength.Ranges);
        Assert.Equal("Regeneration 5/5", ByKey(tree, "regeneration").Text);
        Assert.Equal("Radius 3/3", ByKey(tree, "radius").Text);
        Assert.Equal("white", tree.Walk().Single(n => n.Title == "Shield Strength").Color);

        UiNode input = ByKey(tree, "set-radius");
        Assert.Equal((3.0, 3.0, 3.0, 1.0, false), (input.Value, input.MinValue, input.MaxValue, input.Step, input.Disabled));
        UiNode exterior = ByKey(tree, "exterior");
        Assert.Equal(("Internal & External", false), (exterior.Text, exterior.Disabled));
        UiNode power = ByKey(tree, "power");
        Assert.Equal(("Off", "power-off", false, false, true), (power.Text, power.Icon, power.Selected, power.Disabled, power.Bold));

        input.OnChange(4);
        exterior.OnClick();
        power.OnClick();
        Assert.Equal(new (string, string)[] { ("set_radius", "{\"new_radius\":4}"), ("toggle_exterior", null), ("toggle_shields", null) }, acts);
    }

    [Fact]
    public void AnActiveGeneratorLocksItsSettingsAndARecoveringOneItsPowerButton()
    {
        var (_, tree, _) = Render("ModularShieldGen", ModularShieldGen.Render,
            "{\"max_radius\":4,\"current_radius\":3,\"max_strength\":120,\"max_regeneration\":11,\"current_regeneration\":2.2,"
            + "\"current_strength\":80,\"active\":1,\"recovering\":0,\"exterior_only\":1,\"initiating_field\":0}");
        Assert.True(tree.Walk().Single(n => n.Kind == UiKind.NumberInput).Disabled);
        Assert.Equal(("External only", true), (ByKey(tree, "exterior").Text, ByKey(tree, "exterior").Disabled));
        Assert.Equal(("On", true, false), (ByKey(tree, "power").Text, ByKey(tree, "power").Selected, ByKey(tree, "power").Disabled));
        Assert.Equal("Regeneration 2.2/11", ByKey(tree, "regeneration").Text);

        (_, tree, _) = Render("ModularShieldGen", ModularShieldGen.Render, Ready.Replace("\"recovering\":0", "\"recovering\":1").Replace("\"current_strength\":60", "\"current_strength\":12.5"));
        Assert.True(ByKey(tree, "power").Disabled);
        Assert.Equal("red", tree.Walk().Single(n => n.Title == "Shield Strength").Color);
        Assert.Equal("12.5/60", ByKey(tree, "strength").Text);
        (_, tree, _) = Render("ModularShieldGen", ModularShieldGen.Render, Ready.Replace("\"initiating_field\":0", "\"initiating_field\":1"));
        Assert.True(ByKey(tree, "power").Disabled);
    }

    [Fact]
    public void TheConsoleListsItsGeneratorsAndSendsRenameAndToggleWithTheirId()
    {
        var (context, tree, acts) = Render("ModularShieldConsole", ModularShieldConsole.Render,
            "{\"generators\":[{\"name\":\"Shield Generator\",\"id\":1,\"max_strength\":60,\"current_strength\":60,\"generator_name\":\"Shield Generator\","
            + "\"active\":1,\"recovering\":0,\"current_regeneration\":1},{\"name\":\"Shield Gate\",\"id\":2,\"max_strength\":40,\"current_strength\":3,"
            + "\"generator_name\":\"Shield Gate\",\"active\":0,\"recovering\":1,\"current_regeneration\":0.75,\"inactive\":1}]}");
        Assert.Equal((450.0, 275.0, "Modular Shield Console"), (context.WindowWidth, context.WindowHeight, context.WindowTitle));
        Assert.Equal(new[] { "Name", "Status", "Toggle" }, ByKey(tree, "head").Children.Select(n => n.Text));
        Assert.Equal(3, tree.Walk().Count(n => n.Kind == UiKind.TableRow));
        Assert.Equal("Shield Generator", ByKey(tree, "generator:1:name").TextValue);
        Assert.Equal("60/60 + 1", ByKey(tree, "generator:1:strength").Text);
        Assert.Equal(("On", true, false), (ByKey(tree, "generator:1:power").Text, ByKey(tree, "generator:1:power").Selected, ByKey(tree, "generator:1:power").Disabled));
        Assert.Equal("3/40 + 0.75", ByKey(tree, "generator:2:strength").Text);
        Assert.Equal(("Off", true), (ByKey(tree, "generator:2:power").Text, ByKey(tree, "generator:2:power").Disabled));

        ByKey(tree, "generator:1:name").OnCommit("Bay");
        ByKey(tree, "generator:1:power").OnClick();
        Assert.Equal(new (string, string)[] { ("rename", "{\"id\":1,\"name\":\"Bay\"}"), ("toggle_shields", "{\"id\":1}") }, acts);
    }

    [Fact]
    public void AConsoleWithoutGeneratorsShowsTgsNotice()
    {
        var (_, tree, _) = Render("ModularShieldConsole", ModularShieldConsole.Render, "{\"generators\":[]}");
        Assert.Equal("No Generators Connected", tree.Walk().Single(n => n.Kind == UiKind.NoticeBox).Text);
        Assert.DoesNotContain(tree.Walk(), n => n.Kind == UiKind.Table);
    }

    [Fact]
    public void TheWindowsAreRegisteredUnderTheirInterfaceNames()
    {
        Assert.Equal((TguiInterface)ModularShieldGen.Render, TguiInterfaces.Find("ModularShieldGen"));
        Assert.Equal((TguiInterface)ModularShieldConsole.Render, TguiInterfaces.Find("ModularShieldConsole"));
    }
}
