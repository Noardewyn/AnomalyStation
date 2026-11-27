using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// The client's windows of science's anomaly refinery and pads without the engine: the AnomalyRefinery's implosion control (the core
/// with its radius, the bomb's tanks, the implode and swap buttons) and its simulator tab; the LaunchpadConsole's pad tabs and the
/// selected pad's control; the MechpadConsole's pads and the selected pad's name, launch and the missing pad notice.
/// </summary>
public class AnomalyWindowsTests
{
    private static (UiNode Tree, List<(string Action, UiValue Params)> Acts, TguiContext Context) Render(TguiInterface window, string name, string json, TguiLocalState local = null)
    {
        var acts = new List<(string, UiValue)>();
        var c = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p)), local ?? new TguiLocalState());
        UiNode tree = window(c);
        return (tree, acts, c);
    }

    private const string Refinery = "{\"gasList\":[{\"name\":\"oxygen tank\",\"total_moles\":240,\"temperature\":293.15,\"pressure\":8000,\"gases\":[]},"
        + "{\"name\":\"plasma tank\",\"total_moles\":120,\"temperature\":1500,\"pressure\":20000,\"gases\":[]},"
        + "{\"name\":\"Combined Gasmix\",\"total_moles\":0,\"temperature\":0,\"pressure\":0,\"gases\":[]}],"
        + "\"valvePresent\":1,\"valveReady\":1,\"reactionIncrement\":0,\"core\":\"raw flux core\",\"requiredRadius\":4,\"active\":0}";

    [Fact]
    public void TheAnomalyRefineryShowsItsCoreAndItsBomb()
    {
        var local = new TguiLocalState();
        var (tree, acts, c) = Render(AnomalyRefinery.Render, "AnomalyRefinery", Refinery, local);
        Assert.Equal((550.0, 350.0, "Anomaly Refinery"), (c.WindowWidth, c.WindowHeight, c.WindowTitle));
        Assert.Contains(tree.Walk(), n => n.Text == "raw flux core");
        Assert.Contains(tree.Walk(), n => n.Text == "4 tiles");
        Assert.Equal("Giver Tank (plasma tank)", tree.Walk().Single(n => n.Key == "giver-name").Text);
        Assert.Equal("Target Tank (oxygen tank)", tree.Walk().Single(n => n.Key == "target-name").Text);
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "no-core" || n.Key == "no-bomb");
        UiNode implode = tree.Walk().Single(n => n.Key == "start_implosion");
        Assert.False(implode.Disabled);
        implode.OnClick();
        tree.Walk().Single(n => n.Key == "swap").OnClick();
        tree.Walk().Single(n => n.Key == "eject_core").OnClick();
        Assert.Equal(new[] { "start_implosion", "swap", "eject_core" }, acts.Select(a => a.Action));

        // the simulator tab: the merged mix and each tank
        tree.Walk().Single(n => n.Key == "tab").OnClick();
        (tree, acts, _) = Render(AnomalyRefinery.Render, "AnomalyRefinery", Refinery, local);
        Assert.Equal("Implosion Control", tree.Walk().Single(n => n.Key == "tab").Text);
        UiNode react = tree.Walk().Single(n => n.Key == "react");
        Assert.Equal("Open Valve", react.Text);
        Assert.Contains(tree.Walk(), n => n.Key == "no-gas");
        Assert.Equal("plasma tank", tree.Walk().Single(n => n.Key == "tank1").Title);
        react.OnClick();
        Assert.Equal("react", acts.Single().Action);

        // empty and running
        (tree, _, _) = Render(AnomalyRefinery.Render, "AnomalyRefinery", "{\"gasList\":[{},{},{}],\"valvePresent\":0,\"valveReady\":0,\"core\":0,\"requiredRadius\":null,\"active\":0}");
        Assert.Equal("No Core Inserted!", tree.Walk().Single(n => n.Key == "no-core").Text);
        Assert.Equal("No Bomb Inserted!", tree.Walk().Single(n => n.Key == "no-bomb").Text);
        Assert.Contains(tree.Walk(), n => n.Text == "Implosion not possible.");
        Assert.True(tree.Walk().Single(n => n.Key == "start_implosion").Disabled);
        Assert.True(tree.Walk().Single(n => n.Key == "eject_bomb").Disabled);
        (tree, _, _) = Render(AnomalyRefinery.Render, "AnomalyRefinery", Refinery.Replace("\"active\":0", "\"active\":1"));
        Assert.True(tree.Walk().Single(n => n.Key == "start_implosion").Disabled);
        Assert.True(tree.Walk().Single(n => n.Key == "eject_core").Disabled);
    }

    private const string Launchpads = "{\"launchpads\":[{\"name\":\"Launchpad\",\"id\":1},{\"name\":\"Range\",\"id\":2,\"inactive\":1}],\"selected_id\":2,"
        + "\"x\":2,\"y\":-1,\"pad_name\":\"Range\",\"range\":10,\"pad_active\":1}";

    [Fact]
    public void TheLaunchpadConsoleShowsItsPadsAndTheSelectedPadsControl()
    {
        var (tree, acts, c) = Render(LaunchpadConsole.Render, "LaunchpadConsole", Launchpads);
        Assert.Equal((475.0, 275.0), (c.WindowWidth, c.WindowHeight));
        Assert.False(tree.Walk().Single(n => n.Key == "pad-1").Selected);
        Assert.True(tree.Walk().Single(n => n.Key == "pad-2").Selected);
        Assert.Equal("Range", tree.Walk().Single(n => n.Key == "pad_name").TextValue);
        Assert.Equal(2, tree.Walk().Single(n => n.Key == "pos-x").Value);
        Assert.Equal(-1, tree.Walk().Single(n => n.Key == "pos-y").Value);
        tree.Walk().Single(n => n.Key == "pad-1").OnClick();
        tree.Walk().Single(n => n.Key == "launch").OnClick();
        tree.Walk().Single(n => n.Key == "pull").OnClick();
        Assert.Equal(new[] { "select_pad", "launch", "pull" }, acts.Select(a => a.Action));
        Assert.Equal(1, acts[0].Params.Num("id"));

        (tree, _, _) = Render(LaunchpadConsole.Render, "LaunchpadConsole", "{\"launchpads\":[{\"name\":\"Launchpad\",\"id\":1}],\"selected_id\":null}");
        Assert.Equal("Please select a pad", tree.Walk().Single(n => n.Key == "select").Text);
        (tree, _, _) = Render(LaunchpadConsole.Render, "LaunchpadConsole", "{\"launchpads\":[],\"selected_id\":null}");
        Assert.Equal("No Pads Connected", tree.Walk().Single(n => n.Key == "none").Text);
    }

    [Fact]
    public void TheMechpadConsoleShowsItsPadsAndLaunchesTheSelectedOne()
    {
        string json = "{\"mechpads\":[{\"name\":\"Orbital Pad\",\"id\":1},{\"name\":\"Mining\",\"id\":2}],\"selected_id\":2,\"connected_mechpad\":1,"
            + "\"pad_name\":\"Mining\",\"pad_active\":1,\"mechonly\":0}";
        var (tree, acts, c) = Render(MechpadConsole.Render, "MechpadConsole", json);
        Assert.Equal((475.0, 130.0), (c.WindowWidth, c.WindowHeight));
        Assert.True(tree.Walk().Single(n => n.Key == "pad-Mining").Selected);
        Assert.Equal("Mining", tree.Walk().Single(n => n.Key == "pad_name").TextValue);
        UiNode launch = tree.Walk().Single(n => n.Key == "launch");
        Assert.Equal(("Launch", "good"), (launch.Text, launch.Color));
        launch.OnClick();
        tree.Walk().Single(n => n.Key == "pad_name").OnCommit("Base");
        tree.Walk().Single(n => n.Key == "pad-Orbital Pad").OnClick();
        Assert.Equal(new[] { "launch", "rename", "select_pad" }, acts.Select(a => a.Action));
        Assert.Equal("Base", acts[1].Params.Str("name"));

        (tree, _, _) = Render(MechpadConsole.Render, "MechpadConsole", json.Replace("\"mechonly\":0", "\"mechonly\":1"));
        Assert.Equal("Launch (Mech Only)", tree.Walk().Single(n => n.Key == "launch").Text);
        (tree, _, _) = Render(MechpadConsole.Render, "MechpadConsole", json.Replace("\"connected_mechpad\":1", "\"connected_mechpad\":0"));
        Assert.Equal("No Launch Pad Connected.", tree.Walk().Single(n => n.Key == "no-pad").Text);
        (tree, _, _) = Render(MechpadConsole.Render, "MechpadConsole", "{\"mechpads\":[],\"selected_id\":null}");
        Assert.Equal("No Pads Connected", tree.Walk().Single(n => n.Key == "none").Text);
    }
}
