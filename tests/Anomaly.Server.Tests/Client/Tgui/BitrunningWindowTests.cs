using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps BR1 (D339): the client's bitrunning windows. The QuantumConsole (tg's QuantumConsole.tsx): the domains of the selected
/// difficulty tab sorted by cost with their deploy button, the connected avatars and the stop; the NetpodOutfits (NetpodOutfits.tsx):
/// a collection's outfits filtered by the search and the selection's act; the AvatarHelp (AvatarHelp.tsx): the domain's help text and
/// the six boxes. Each is registered under its name.
/// </summary>
public class BitrunningWindowTests
{
    private const string ConsoleJson = "{\"connected\":1,\"ready\":1,\"points\":3,\"occupants\":0,\"randomized\":0,\"generated_domain\":null," +
        "\"retries_left\":0,\"scanner_tier\":1,\"broadcasting\":0,\"broadcasting_on_cd\":0," +
        "\"available_domains\":[" +
        "{\"id\":\"grasslands_hunt\",\"name\":\"Grasslands Hunt\",\"desc\":\"A peaceful hunt.\",\"cost\":0,\"difficulty\":0,\"reward\":1,\"is_modular\":1,\"has_secondary_objectives\":0,\"announce_ghosts\":0,\"grade_symbol\":\"\"}," +
        "{\"id\":\"gondola_asteroid\",\"name\":\"Gondola Asteroid\",\"desc\":\"Gondolas.\",\"cost\":0,\"difficulty\":0,\"reward\":1,\"is_modular\":0,\"has_secondary_objectives\":0,\"announce_ghosts\":0,\"grade_symbol\":\"\"}," +
        "{\"id\":\"pirates\",\"name\":\"???\",\"desc\":\"???\",\"cost\":5,\"difficulty\":2,\"reward\":\"???\",\"is_modular\":0,\"has_secondary_objectives\":0,\"announce_ghosts\":1,\"grade_symbol\":\"\"}]," +
        "\"avatars\":[{\"name\":\"Neo\",\"pilot\":\"Bob\",\"health\":100,\"brute\":0,\"burn\":0,\"tox\":0,\"oxy\":0}]}";

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    [Fact]
    public void TheQuantumConsoleListsTheDomainsOfATabAndDeploysOne()
    {
        Assert.NotNull(TguiInterfaces.Find("QuantumConsole"));
        var acts = new List<(string Action, string Id)>();
        var local = new TguiLocalState();
        var context = new TguiContext(1, "QuantumConsole", "w", 2, UiValue.FromJson(ConsoleJson), (action, args) => acts.Add((action, args?.Get("id")?.Text)), local);
        UiNode tree = QuantumConsole.Render(context);
        Assert.Equal((500.0, 500.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Quantum Console", context.WindowTitle);
        // the peaceful tab: both peaceful domains, the medium one not
        Assert.Contains(tree.Walk(), n => n.Key == "domain:grasslands_hunt");
        Assert.Contains(tree.Walk(), n => n.Key == "domain:gondola_asteroid");
        Assert.DoesNotContain(tree.Walk(), n => n.Key == "domain:pirates");
        Assert.Contains(tree.Walk(), n => n.Key == "modular");
        Assert.Equal("Nothing loaded", ByKey(tree, "notice:box").Text);
        Assert.Contains(tree.Walk(), n => n.Kind == UiKind.TableCell && n.Text == "Bob as \"Neo\"");
        UiNode deploy = ByKey(ByKey(tree, "domain:gondola_asteroid"), "deploy");
        Assert.Equal("Deploy", deploy.Text);
        Assert.False(deploy.Disabled);
        deploy.OnClick();
        Assert.Equal(("set_domain", "gondola_asteroid"), Assert.Single(acts));
        // the medium tab shows the redacted domain, too dear to deploy
        ByKey(tree, "tab2").OnClick();
        UiNode medium = QuantumConsole.Render(new TguiContext(1, "QuantumConsole", "w", 2, UiValue.FromJson(ConsoleJson), (_, _) => { }, local));
        Assert.True(ByKey(ByKey(medium, "domain:pirates"), "deploy").Disabled);
        Assert.Contains(medium.Walk(), n => n.Key == "reward" && n.Kind == UiKind.TableCell && n.Text == "???");
    }

    [Fact]
    public void TheQuantumConsoleSaysWhenNoServerIsConnected()
    {
        var context = new TguiContext(1, "QuantumConsole", "w", 2, UiValue.FromJson("{\"connected\":0}"), (_, _) => { }, new TguiLocalState());
        UiNode tree = QuantumConsole.Render(context);
        Assert.Equal("No server connected!", ByKey(tree, "noserver").Text);
    }

    [Fact]
    public void TheNetpodOutfitsWindowFiltersACollectionAndSelectsAnOutfit()
    {
        Assert.NotNull(TguiInterfaces.Find("NetpodOutfits"));
        const string json = "{\"netsuit\":\"/datum/outfit/job/bitrunner\",\"collections\":[" +
            "{\"name\":\"Jobs\",\"outfits\":[{\"path\":\"/datum/outfit/job/bitrunner\",\"name\":\"Bitrunner\"},{\"path\":\"/datum/outfit/job/cook\",\"name\":\"Cook\"}]}," +
            "{\"name\":\"Misc\",\"outfits\":[{\"path\":\"/datum/outfit/beachbum\",\"name\":\"Beach Bum\"}]}]}";
        var acts = new List<(string Action, string Outfit)>();
        var local = new TguiLocalState();
        var context = new TguiContext(1, "NetpodOutfits", "w", 2, UiValue.FromJson(json), (action, args) => acts.Add((action, args?.Get("outfit")?.Text)), local);
        UiNode tree = NetpodOutfits.Render(context);
        Assert.Equal((400.0, 300.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Bitrunner", ByKey(tree, "selected:box").Text);
        Assert.Equal(2, tree.Walk().Count(n => n.Key == "select" && n.Kind == UiKind.Button));
        tree.Walk().First(n => n.Key == "select" && n.Text == "Cook").OnClick();
        Assert.Equal(("select_outfit", "/datum/outfit/job/cook"), Assert.Single(acts));
        ByKey(tree, "search").OnInput("bit");
        UiNode searched = NetpodOutfits.Render(new TguiContext(1, "NetpodOutfits", "w", 2, UiValue.FromJson(json), (_, _) => { }, local));
        Assert.Equal(new[] { "Bitrunner" }, searched.Walk().Where(n => n.Key == "select" && n.Kind == UiKind.Button).Select(n => n.Text));
        ByKey(searched, "type:Misc").OnClick();
        UiNode misc = NetpodOutfits.Render(new TguiContext(1, "NetpodOutfits", "w", 2, UiValue.FromJson(json), (_, _) => { }, local));
        Assert.Equal("None", ByKey(misc, "selected:box").Text);
    }

    [Fact]
    public void TheAvatarHelpShowsTheDomainsHelpAndTheSixBoxes()
    {
        Assert.NotNull(TguiInterfaces.Find("AvatarHelp"));
        var context = new TguiContext(1, "AvatarHelp", "w", 2, UiValue.FromJson("{\"help_text\":\"Find the crate.\"}"), (_, _) => { }, new TguiLocalState());
        UiNode tree = AvatarHelp.Render(context);
        Assert.Equal((600.0, 600.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal("Domain Information", context.WindowTitle);
        Assert.Equal("Find the crate.", ByKey(ByKey(tree, "help"), "text").Text);
        Assert.Equal(6, tree.Walk().Count(n => n.Kind == UiKind.Section && n.Key == "box"));
        UiNode empty = AvatarHelp.Render(new TguiContext(1, "AvatarHelp", "w", 2, UiValue.FromJson("{}"), (_, _) => { }, new TguiLocalState()));
        Assert.Equal("No information available! Ask for assistance if needed.", ByKey(ByKey(empty, "help"), "text").Text);
    }
}
