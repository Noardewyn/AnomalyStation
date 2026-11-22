using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps package V1 (D327): the client's AntagInfoRevolution window without the engine: a comrade's guidance and the heads of staff to
/// kill or exile; a leader's taller window with the heads' code words ("a, b, and c") and the conversion guideline field, which sends
/// tg's set_conversion_objective.
/// </summary>
public class AntagInfoRevolutionWindowTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "AntagInfoRevolution", "Revolutionary", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, AntagInfoRevolution.Render(context), acts);
    }

    [Fact]
    public void A_comrade_sees_the_cause_and_the_heads_to_overthrow()
    {
        var (context, tree, _) = Render("{\"leader\":0,\"heads\":[{\"name\":\"Carol Smith\",\"role\":\"Captain\"}],\"objectives\":[]}");
        Assert.Equal((400.0, 400.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains(tree.Walk(), n => n.Text == "Viva la Revolution!" && n.Color == "red");
        Assert.Contains(tree.Walk(), n => n.Text == "- Help your cause. Do not harm your fellow freedom fighters.");
        Assert.Contains(tree.Walk(), n => n.Text == "You must kill or exile the heads of staff:");
        Assert.Contains(tree.Walk(), n => n.Text == "- Carol Smith, the Captain");
        Assert.DoesNotContain(tree.Walk(), n => n.Kind == UiKind.Input);
        Assert.Contains("AntagInfoRevolution", TguiInterfaces.Names);
    }

    [Fact]
    public void A_leader_sees_the_code_words_and_sets_the_conversion_guideline()
    {
        var (context, tree, acts) = Render("{\"leader\":1,\"lone_wolf\":1,\"heads\":[],\"code_phrases\":[\"apple\",\"pear\",\"plum\"],\"code_responses\":[\"red\",\"blue\"],"
            + "\"conversion_objective\":\"\",\"conversion_objective_max_length\":140,\"objectives\":[]}");
        Assert.Equal(500.0, context.WindowHeight);
        Assert.Contains(tree.Walk(), n => n.Text != null && n.Text.StartsWith("- You are a lone leader of the revolution."));
        Assert.Contains(tree.Walk(), n => n.Text == "Phrases: apple, pear, and plum" && n.Color == "blue");
        Assert.Contains(tree.Walk(), n => n.Text == "Responses: red and blue" && n.Color == "red");
        UiNode input = tree.Walk().Single(n => n.Kind == UiKind.Input);
        Assert.Equal(140, input.MaxLength);
        Assert.StartsWith("Ex: ", input.Tooltip);
        input.OnCommit("Seize the bridge.");
        var act = Assert.Single(acts);
        Assert.Equal("set_conversion_objective", act.Action);
        Assert.Contains("Seize the bridge.", act.Params);
    }
}
