using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps OG1: the client's HighLuminosityEyesMenu window (tg's HighLuminosityEyesMenu.tsx) shows the light's colour and range and,
/// with an owner, the eye colour matching or the two eyes' own colours, each with tg's pick, dice and typed colour; the
/// TacVisorEyesMenu window (TacVisorEyesMenu.tsx) shows the IFF factions, the visor image and the threat flags. Both are registered
/// under their names.
/// </summary>
public class EyeAugmentWindowsTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string iface, Func<TguiContext, UiNode> render, string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, iface, "w", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    [Fact]
    public void TheHighLuminosityEyesMenuShowsTheLightAndTheEyeColours()
    {
        Assert.NotNull(TguiInterfaces.Find("HighLuminosityEyesMenu"));
        // loose eyes: the light only, tg's small window
        var (loose, looseTree, _) = Render("HighLuminosityEyesMenu", HighLuminosityEyesMenu.Render,
            "{\"eyeColor\":{\"mode\":0,\"hasOwner\":0,\"left\":\"#000000\",\"right\":\"#000000\"},\"lightColor\":\"#ffffff\",\"range\":3}");
        Assert.Equal((225.0, 135.0), (loose.WindowWidth, loose.WindowHeight));
        Assert.Equal("High Luminosity Eyes", loose.WindowTitle);
        Assert.DoesNotContain(looseTree.Walk(), n => n.Key == "match");

        // in a head, custom colours: both eyes' rows
        var (context, tree, acts) = Render("HighLuminosityEyesMenu", HighLuminosityEyesMenu.Render,
            "{\"eyeColor\":{\"mode\":0,\"hasOwner\":1,\"left\":\"#ff0000\",\"right\":\"#00ff00\"},\"lightColor\":\"#ffffff\",\"range\":3}");
        Assert.Equal((262.0, 220.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("#ffffff", ByKey(tree, "light_box").Color);
        Assert.Equal("#ff0000", ByKey(tree, "left_input").TextValue);
        Assert.Equal("#00ff00", ByKey(tree, "right_box").Color);
        Assert.Equal(3.0, ByKey(tree, "range_input").Value);
        Assert.Equal(5.0, ByKey(tree, "range_input").MaxValue);
        Assert.False(ByKey(tree, "match_toggle").Checkbox);
        ByKey(tree, "light_pick").OnClick();
        ByKey(tree, "left_random").OnClick();
        ByKey(tree, "right_input").OnCommit("#123456");
        ByKey(tree, "range_plus").OnClick();
        ByKey(tree, "range_minus").OnClick();
        ByKey(tree, "match_toggle").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("pick_color", "{\"to_update\":0}"), ("random_color", "{\"to_update\":1}"), ("enter_color", "{\"new_color\":\"#123456\",\"to_update\":2}"),
            ("set_range", "{\"new_range\":4}"), ("set_range", "{\"new_range\":2}"), ("toggle_eye_color", null),
        }, acts);

        // matching the light: no eye rows, tg's shorter window
        var (matching, matchTree, _) = Render("HighLuminosityEyesMenu", HighLuminosityEyesMenu.Render,
            "{\"eyeColor\":{\"mode\":1,\"hasOwner\":1,\"left\":\"#ff0000\",\"right\":\"#00ff00\"},\"lightColor\":\"#ffffff\",\"range\":3}");
        Assert.Equal(170.0, matching.WindowHeight);
        Assert.True(ByKey(matchTree, "match_toggle").Checkbox);
        Assert.DoesNotContain(matchTree.Walk(), n => n.Key == "left");
    }

    [Fact]
    public void TheTacVisorEyesMenuShowsTheFactionsTheVisorAndTheThreatFlags()
    {
        Assert.NotNull(TguiInterfaces.Find("TacVisorEyesMenu"));
        var (context, tree, acts) = Render("TacVisorEyesMenu", TacVisorEyesMenu.Render,
            "{\"friendlyFaction\":\"Nanotrasen\",\"hostileFaction\":\"Syndicate\",\"visorDisplay\":\"Cross\",\"threatFlags\":5,"
            + "\"validFriendlyFactions\":[\"None\",\"Nanotrasen\"],\"validHostileFactions\":[\"None\",\"Syndicate\"],"
            + "\"visorOptions\":[\"Cross\",\"Eyes\",\"Line\"],\"threatOptions\":{\"Valid ID\":1,\"Weapon Permit\":2,\"Security Status\":4}}");
        Assert.Equal((320.0, 264.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Tactical IFF Visor", context.WindowTitle);
        Assert.Equal("Nanotrasen", ByKey(tree, "friendly_dropdown").TextValue);
        Assert.Equal(new[] { "Cross", "Eyes", "Line" }, ByKey(tree, "display_dropdown").Options);
        Assert.True(ByKey(tree, "threat:Valid ID").Checkbox);
        Assert.False(ByKey(tree, "threat:Weapon Permit").Checkbox);
        Assert.True(ByKey(tree, "threat:Security Status").Checkbox);
        ByKey(tree, "hostile_dropdown").OnSelected("None");
        ByKey(tree, "display_dropdown").OnSelected("Line");
        ByKey(tree, "threat:Weapon Permit").OnClick();
        ByKey(tree, "threat:Valid ID").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("set_hostile", "{\"faction\":\"None\"}"), ("set_display", "{\"display\":\"Line\"}"),
            ("set_threat_flags", "{\"threat_flags\":7}"), ("set_threat_flags", "{\"threat_flags\":4}"),
        }, acts);
    }
}
