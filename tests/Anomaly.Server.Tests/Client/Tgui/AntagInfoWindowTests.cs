using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Syndicate package A1 (D316): the client's AntagInfoGeneric window and tg's shared objective printout (common/Objectives.tsx) without
/// the engine: the antagonist's name in red, "Your current objectives:" and each objective as "#count: explanation", "None!" for none;
/// the replace button only when the antagonist may change its objectives, sending tg's change_objectives.
/// </summary>
public class AntagInfoWindowTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(TguiInterface view, string name, string title, string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, name, title, 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, view(context), acts);
    }

    [Fact]
    public void The_generic_window_names_the_antagonist_and_lists_its_objectives()
    {
        var (context, tree, _) = Render(AntagInfoGeneric.Render, "AntagInfoGeneric", "Saboteur",
            "{\"antag_name\":\"Saboteur\",\"can_change_objective\":0,\"objectives\":[{\"count\":1,\"name\":\"Objective\",\"explanation\":\"Assassinate Alice, the Assistant.\",\"complete\":0},"
            + "{\"count\":2,\"name\":\"Objective\",\"explanation\":\"Steal a fire axe.\",\"complete\":0}]}");
        Assert.Equal((620.0, 250.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Saboteur", context.WindowTitle);
        UiNode you = tree.Walk().Single(n => n.Text == "You are the Saboteur!");
        Assert.Equal(("red", "20px"), (you.Color, you.FontSize as string));
        UiNode title = tree.Walk().Single(n => n.Text == "Your current objectives:");
        Assert.True(title.Bold);
        Assert.Equal(new[] { "#1: Assassinate Alice, the Assistant.", "#2: Steal a fire axe." },
            tree.Walk().Where(n => n.Text != null && n.Text.StartsWith('#')).Select(n => n.Text));
        Assert.True(tree.Walk().Single(n => n.Key == "info").Scrollable);
        Assert.Contains("AntagInfoGeneric", TguiInterfaces.Names);
    }

    [Fact]
    public void No_objectives_print_none_and_the_replace_button_shows_only_when_allowed()
    {
        var (_, tree, _) = Render(AntagInfoGeneric.Render, "AntagInfoGeneric", "Antagonist", "{\"antag_name\":\"Antagonist\",\"can_change_objective\":0,\"objectives\":[]}");
        Assert.Contains(tree.Walk(), n => n.Text == "None!");

        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "AntagInfoGeneric", "Antagonist", 2, UiValue.FromJson("{}"), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        Assert.Null(Objectives.ReplaceObjectivesButton(context, false, "Replace", "red"));
        UiNode button = Objectives.ReplaceObjectivesButton(context, true, "Replace", "red");
        Assert.Equal(("Replace", "red", "Replace your existing objectives with a custom one. This action can only be taken once"), (button.Text, button.Color, button.Tooltip));
        button.OnClick();
        Assert.Equal("change_objectives", Assert.Single(acts).Item1);
    }
}
