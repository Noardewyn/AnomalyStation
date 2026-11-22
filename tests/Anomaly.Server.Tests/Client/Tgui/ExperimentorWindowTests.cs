using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps RD4: the client's Experimentor window (tg's Experimentor.tsx) asks for a server link, then for an item; with an item it shows
/// its title-cased name, its picture from its icon and state (DV496), the nodes it can reveal as Locked or Unlocked, one icon button
/// per experiment (disabled on cooldown or when the item cannot take it) and the Discover! button, live only for a relic. It is
/// registered under its name.
/// </summary>
public class ExperimentorWindowTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "Experimentor", "w", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, Experimentor.Render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private const string Experiments = "\"availableExperiments\":["
        + "{\"id\":\"Poke\",\"name\":\"Poke\",\"fa_icon\":\"hand\",\"isAvailable\":1,\"isDiscover\":0},"
        + "{\"id\":\"Irradiate\",\"name\":\"Irradiate\",\"fa_icon\":\"radiation\",\"isAvailable\":1,\"isDiscover\":0},"
        + "{\"id\":\"Discover\",\"name\":\"Discover\",\"fa_icon\":\"question\",\"isAvailable\":0,\"isDiscover\":1}]";

    private const string Loaded = "{\"hasItem\":1,\"isOnCooldown\":0,\"isServerConnected\":1," + Experiments + ","
        + "\"loadedItem\":{\"name\":\"alien cautery\",\"icon\":{\"icon\":\"icons/obj/antags/abductor.dmi\",\"icon_state\":\"cautery\"},\"isRelic\":0,"
        + "\"associatedNodes\":[{\"name\":\"Alien Technology\",\"isUnlocked\":0}]}}";

    [Fact]
    public void TheExperimentorShowsItsItemNodesAndExperimentsAndSendsTgsActs()
    {
        Assert.NotNull(TguiInterfaces.Find("Experimentor"));

        var (context, unlinked, _) = Render("{\"hasItem\":0,\"isOnCooldown\":0,\"isServerConnected\":0,\"availableExperiments\":[]}");
        Assert.Equal((450.0, 350.0), (context.WindowWidth, context.WindowHeight));
        Assert.Contains("Not connected to a server.", unlinked.AllText());
        var (_, empty, _) = Render("{\"hasItem\":0,\"isOnCooldown\":0,\"isServerConnected\":1,\"availableExperiments\":[]}");
        Assert.Contains("No item present in experimentation chamber.", empty.AllText());

        var (_, tree, acts) = Render(Loaded);
        string text = tree.AllText();
        Assert.Contains("Alien Cautery", text);
        Assert.Equal("Locked", ByKey(tree, "node:0").Text);
        Assert.Equal("bad", ByKey(tree, "node:0").Color);
        UiNode picture = ByKey(tree, "icon");
        Assert.Equal(("icons/obj/antags/abductor.dmi", "cautery"), (picture.DmIcon, picture.DmIconState));
        Assert.Equal("radiation", ByKey(tree, "exp:Irradiate").Icon);
        Assert.False(ByKey(tree, "exp:Poke").Disabled);
        Assert.True(ByKey(tree, "discover").Children.Single().Disabled); // not a relic

        ByKey(tree, "exp:Poke").OnClick();
        ByKey(tree, "eject").OnClick();
        Assert.Equal(new (string, string)[] { ("experiment", "{\"id\":\"Poke\"}"), ("eject", null) }, acts);

        // on cooldown every button is off; a relic's Discover! is live
        var (_, cooling, _) = Render(Loaded.Replace("\"isOnCooldown\":0", "\"isOnCooldown\":1"));
        Assert.True(ByKey(cooling, "exp:Poke").Disabled);
        string relic = Loaded.Replace("\"isRelic\":0", "\"isRelic\":1").Replace("\"isAvailable\":1", "\"isAvailable\":0").Replace("\"isAvailable\":0,\"isDiscover\":1", "\"isAvailable\":1,\"isDiscover\":1");
        var (_, relicTree, relicActs) = Render(relic);
        UiNode discover = ByKey(relicTree, "discover").Children.Single();
        Assert.False(discover.Disabled);
        Assert.Equal("Discover!", discover.Text);
        Assert.True(ByKey(relicTree, "exp:Poke").Disabled);
        discover.OnClick();
        Assert.Equal(new (string, string)[] { ("experiment", "{\"id\":\"Discover\"}") }, relicActs);
    }
}
