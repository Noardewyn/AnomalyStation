using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// The chapel's windows. The ReligiousTool (tg's ReligiousTool.jsx): the sects to pick from before a sect is chosen, then the sect's
/// page and its rites with the invoke button a lack of favour disables; the SparringContract (SparringContract.tsx): the terms picked
/// in the window and signed with them; the AntagInfoShade (AntagInfoShade.jsx): the shade's master. Each is registered under its name.
/// </summary>
public class ChapelWindowTests
{
    private const string SelectJson = "{\"sects\":[" +
        "{\"name\":\"Mechanical God\",\"desc\":\"Bring the machines.\",\"alignment\":\"neutral\",\"quote\":\"May you find peace in a metal shell.\",\"icon\":\"robot\",\"path\":\"/datum/religion_sect/mechanical\"}," +
        "{\"name\":\"Nanotrasen Defense God\",\"desc\":\"Honor.\",\"alignment\":\"good\",\"quote\":\"Honor.\",\"icon\":\"scroll\",\"path\":\"/datum/religion_sect/honorbound\"}]," +
        "\"alignment\":\"neutral\",\"toolname\":\"altar of the Gods\"}";

    private const string SectJson = "{\"sects\":null,\"name\":\"Maintenance God\",\"desc\":\"Yuck.\",\"quote\":\"Yes.\",\"alignment\":\"evil\",\"icon\":\"eye\"," +
        "\"favordesc\":\"You have 50 favor with Space Jesus.\",\"favor\":50,\"deity\":\"Space Jesus\",\"wanted\":null,\"toolname\":\"altar of the Gods\"," +
        "\"rites\":[{\"name\":\"Ritual Totem\",\"desc\":\"A totem.\",\"favor\":75,\"can_cast\":1,\"path\":\"/datum/religion_rites/ritual_totem\"}," +
        "{\"name\":\"Adapted Eyes\",\"desc\":\"Eyes.\",\"favor\":25,\"can_cast\":0,\"path\":\"/datum/religion_rites/adapted_eyes\"}]}";

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    [Fact]
    public void TheReligiousToolOffersTheSectsAndSelectsOne()
    {
        Assert.NotNull(TguiInterfaces.Find("ReligiousTool"));
        var acts = new List<(string Action, string Path)>();
        var context = new TguiContext(1, "ReligiousTool", "w", 2, UiValue.FromJson(SelectJson), (action, args) => acts.Add((action, args?.Get("path")?.Text)), new TguiLocalState());
        UiNode tree = ReligiousTool.Render(context);
        Assert.Equal((560.0, 500.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("altar of the Gods", context.WindowTitle);
        Assert.Contains("Sect Select", tree.AllText());
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "tab:rites");
        UiNode pick = ByKey(ByKey(tree, "sect:/datum/religion_sect/honorbound"), "select");
        Assert.Equal("Select Nanotrasen Defense God", pick.Text);
        pick.OnClick();
        Assert.Equal(("sect_select", "/datum/religion_sect/honorbound"), Assert.Single(acts));
    }

    [Fact]
    public void TheReligiousToolShowsTheSectAndItsRites()
    {
        var acts = new List<(string Action, string Path)>();
        var local = new TguiLocalState();
        var context = new TguiContext(1, "ReligiousTool", "w", 2, UiValue.FromJson(SectJson), (action, args) => acts.Add((action, args?.Get("path")?.Text)), local);
        UiNode tree = ReligiousTool.Render(context);
        Assert.Contains("Maintenance God", tree.AllText());
        Assert.Contains("You have 50 favor with Space Jesus.", tree.AllText());
        Assert.Contains("Space Jesus doesn't want any sacrifices.", tree.AllText());
        ByKey(tree, "tab:rites").OnClick();
        UiNode rites = ReligiousTool.Render(new TguiContext(1, "ReligiousTool", "w", 2, UiValue.FromJson(SectJson), (action, args) => acts.Add((action, args?.Get("path")?.Text)), local));
        UiNode totem = ByKey(rites, "rite:/datum/religion_rites/ritual_totem").Walk().First(n => n.Kind == UiKind.Section);
        UiNode eyes = ByKey(rites, "rite:/datum/religion_rites/adapted_eyes").Walk().First(n => n.Kind == UiKind.Section);
        Assert.Equal("Ritual Totem", totem.Title);
        Assert.True(totem.Buttons.Disabled, "75 favor is more than the sect has");
        Assert.False(eyes.Buttons.Disabled);
        Assert.Contains("Costs 25 favor.", rites.AllText());
        eyes.Buttons.OnClick();
        Assert.Equal(("perform_rite", "/datum/religion_rites/adapted_eyes"), Assert.Single(acts));
    }

    [Fact]
    public void TheSparringContractSignsTheTermsPickedInTheWindow()
    {
        Assert.NotNull(TguiInterfaces.Find("SparringContract"));
        const string json = "{\"set_weapon\":3,\"set_area\":\"Chapel\",\"set_stakes\":1,\"possible_areas\":[\"Chapel\",\"Chapel Office\"],\"stakes_holy_match\":2," +
            "\"left_sign\":\"none\",\"right_sign\":\"Alice\",\"in_area\":0,\"no_chaplains\":1}";
        var acts = new List<(string Action, UiValue Args)>();
        var local = new TguiLocalState();
        TguiContext Context() => new(1, "SparringContract", "w", 2, UiValue.FromJson(json), (action, args) => acts.Add((action, args)), local);
        UiNode tree = SparringContract.Render(Context());
        Assert.Equal("Melee Only", ByKey(tree, "pick:weapon").TextValue);
        Assert.Contains("Ranged weapons are a violation.", tree.AllText());
        Assert.True(ByKey(tree, "fight").Disabled, "not in the arena");
        Assert.Contains("Alice", tree.AllText());
        ByKey(tree, "pick:weapon").OnSelected("Fist Fight");
        ByKey(tree, "pick:stakes").OnSelected("Holy Match");
        tree = SparringContract.Render(Context());
        Assert.Contains("fists only", tree.AllText());
        ByKey(ByKey(tree, "left"), "sign").OnClick();
        (string action, UiValue args) = Assert.Single(acts);
        Assert.Equal("sign", action);
        Assert.Equal(1, args.Num("weapon"));
        Assert.Equal(2, args.Num("stakes"));
        Assert.Equal("Chapel", args.Str("area"));
        Assert.Equal("left", args.Str("sign_position"));
    }

    [Fact]
    public void TheShadeIsToldWhoItsMasterIs()
    {
        Assert.NotNull(TguiInterfaces.Find("AntagInfoShade"));
        var context = new TguiContext(1, "AntagInfoShade", "w", 2, UiValue.FromJson("{\"master_name\":\"Bob\"}"), (_, _) => { }, new TguiLocalState());
        UiNode tree = AntagInfoShade.Render(context);
        Assert.Equal((400.0, 400.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("abductor", context.WindowTheme);
        Assert.Contains("You are bound to the will of Bob!", tree.AllText());
    }
}
