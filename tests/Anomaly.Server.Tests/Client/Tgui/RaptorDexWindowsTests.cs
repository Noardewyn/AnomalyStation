using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>The client's RaptorDex window and its NtOS program's (tg's RaptorDex.tsx and NtosRaptorDex.tsx) without the engine.</summary>
public class RaptorDexWindowsTests
{
    private const string Scan = "{\"raptor_scan\":1,\"raptor_icon\":\"icons/mob/simple/lavaland/raptor_big.dmi\",\"raptor_icon_state\":\"raptor_red\","
        + "\"raptor_attack\":12,\"raptor_health\":200,\"raptor_max_health\":270,\"raptor_speed\":2,\"raptor_color\":\"red raptor\",\"raptor_gender\":\"female\","
        + "\"raptor_growth\":0,\"can_grow\":0,\"raptor_description\":\"A resilient breed of raptors.\",\"raptor_happiness\":[\"full_heart\",\"full_heart\",\"empty_heart\",\"empty_heart\"],"
        + "\"inherited_attack\":2,\"inherited_attack_max\":5,\"inherited_health\":-10,\"inherited_health_max\":30,\"inherited_speed\":0.1,\"inherited_speed_max\":0.5,"
        + "\"inherited_ability\":0.05,\"inherited_ability_max\":0.2,\"inherited_growth\":0,\"inherited_growth_max\":0.2,\"inherited_traits\":[\"Playful\",\"Brave\"]}";

    private static (TguiContext Context, UiNode Tree) Render(string name, TguiInterface render, string json)
    {
        var context = new TguiContext(1, name, name, 2, UiValue.FromJson(json), (a, p) => { }, new TguiLocalState());
        return (context, render(context));
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    [Fact]
    public void TheRaptorDexShowsTheScannedRaptor()
    {
        var (context, tree) = Render("RaptorDex", RaptorDex.Render, Scan);
        Assert.Equal((770.0, 370.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("hackerman", context.WindowTheme);
        Assert.Equal("Red Raptor", ByKey(tree, "picture").Title);
        UiNode picture = ByKey(tree, "raptor");
        Assert.Equal(("icons/mob/simple/lavaland/raptor_big.dmi", "raptor_red"), (picture.DmIcon, picture.DmIconState));
        Assert.Equal("200 / 270", tree.Walk().First(n => n.Key == "health" && n.Kind == UiKind.LabeledItem && n.Text != null).Text);
        Assert.Equal("5", ByKey(tree, "speed").Text); // Math.round(10 / speed)
        Assert.Equal("Female", ByKey(tree, "gender").Text);
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "growth");
        Assert.Equal(new[] { "full_heart", "full_heart", "empty_heart", "empty_heart" }, ByKey(tree, "hearts").Children.Select(h => h.Children.Single().DmIconState));
        Assert.Equal("A resilient breed of raptors.", ByKey(tree, "description").Children.Single().Text);
        Assert.Equal(new[] { "Playful", "Brave" }, tree.Walk().Where(n => n.Key?.StartsWith("trait:") == true).Select(n => n.Text));
        UiNode attackBar = tree.Walk().First(n => n.Key == "attack" && n.Kind == UiKind.ProgressBar);
        Assert.Equal((2.0, 5.0), (attackBar.Value, attackBar.MaxValue));
    }

    [Fact]
    public void ARaptorThatCanGrowShowsItsGrowth()
    {
        var (_, tree) = Render("RaptorDex", RaptorDex.Render, Scan.Replace("\"can_grow\":0", "\"can_grow\":1").Replace("\"raptor_growth\":0", "\"raptor_growth\":40"));
        UiNode growth = tree.Walk().First(n => n.Key == "growth" && n.Kind == UiKind.ProgressBar);
        Assert.Equal((40.0, 100.0), (growth.Value, growth.MaxValue));
    }

    [Fact]
    public void AnEmptyRaptorDexSaysItHasNoScan()
    {
        var (_, tree) = Render("RaptorDex", RaptorDex.Render, "{\"raptor_scan\":0}");
        Assert.Equal("No scan data present.", ByKey(tree, "empty").Text);
    }

    [Fact]
    public void TheTabletProgramShowsTheSameScan()
    {
        var (context, tree) = Render("NtosRaptorDex", NtosRaptorDex.Render, Scan);
        Assert.Equal((770.0, 370.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Red Raptor", ByKey(tree, "picture").Title);
        Assert.Contains("Playful", tree.AllText());
    }
}
