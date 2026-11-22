using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps J3 (D298): the client's Crayon window (tg's Crayon.tsx): the stencils with the selected one marked, the cap and colours only for a spray can, the text buffer only for a literate user; tg's acts.
/// </summary>
public class CrayonWindowTests
{
    private static (UiNode Tree, List<(string Action, string Params)> Acts) Render(string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "Crayon", "crayon", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (Crayon.Render(context), acts);
    }

    private const string Stencils = "\"drawables\":[{\"name\":\"Runes\",\"items\":[{\"item\":\"rune1\"},{\"item\":\"rune2\"}]}],\"selected_stencil\":\"rune2\",\"text_buffer\":\"hi\"";

    [Fact]
    public void ACrayonShowsItsStencilsAndABufferAndNoCap()
    {
        var (tree, acts) = Render("{" + Stencils + ",\"is_literate_user\":1,\"has_cap\":0,\"can_change_colour\":0}");
        Assert.DoesNotContain(tree.Walk(), n => n.Kind == UiKind.Section && n.Title == "Basic");
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.Section && n.Title == "Text");
        UiNode second = tree.Walk().Single(n => n.Key == "rune2");
        Assert.True(second.Selected);
        Assert.False(tree.Walk().Single(n => n.Key == "rune1").Selected);
        tree.Walk().Single(n => n.Key == "rune1").OnClick();
        Assert.Equal(("select_stencil", "{\"item\":\"rune1\"}"), acts.Single());
    }

    [Fact]
    public void ASprayCanHasItsCapAndColours()
    {
        var (tree, acts) = Render("{" + Stencils + ",\"is_literate_user\":0,\"has_cap\":1,\"is_capped\":1,\"can_change_colour\":1,\"selected_color\":\"red\",\"paint_colors\":{\"red\":\"#FF0000\"}}");
        Assert.DoesNotContain(tree.Walk(), n => n.Kind == UiKind.Section && n.Title == "Text");
        Assert.Equal(("On", "power-off", true), (tree.Walk().Single(n => n.Key == "cap").Text, tree.Walk().Single(n => n.Key == "cap").Icon, tree.Walk().Single(n => n.Key == "cap").Selected));
        tree.Walk().Single(n => n.Key == "color-red").OnClick();
        tree.Walk().Single(n => n.Key == "cap").OnClick();
        Assert.Equal(new[] { "color", "toggle_cap" }, acts.Select(a => a.Action));
    }
}
