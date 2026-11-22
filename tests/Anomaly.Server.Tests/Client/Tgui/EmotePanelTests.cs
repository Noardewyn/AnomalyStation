using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// M9 slice B (D256): the client's EmotePanel window (tg's EmotePanel.tsx) without the engine; the payload follows emote_panel.dm:4-31.
/// The window sorts by name, filters, searches, and sends tg's play_emote and preview_sound.
/// </summary>
public class EmotePanelTests
{
    private const string Emotes = "{\"emotes\":[" +
        "{\"key\":\"wave\",\"name\":\"wave\",\"hands\":0,\"visible\":1,\"audible\":0,\"sound\":0,\"use_params\":\"\"}," +
        "{\"key\":\"scream\",\"name\":\"scream\",\"hands\":0,\"visible\":1,\"audible\":2,\"sound\":1,\"use_params\":\"\"}," +
        "{\"key\":\"nod\",\"name\":\"nod\",\"hands\":0,\"visible\":1,\"audible\":0,\"sound\":0,\"use_params\":\"nods at %t.\"}," +
        "{\"key\":\"twitch_s\",\"name\":\"twitch (Slight)\",\"hands\":0,\"visible\":1,\"audible\":0,\"sound\":0,\"use_params\":\"\"}]}";

    private static (TguiContext Context, List<(string Action, string Params)> Acts) Context(TguiLocalState local = null)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "EmotePanel", "Emote Panel", 2, UiValue.FromJson(Emotes), (a, p) => acts.Add((a, p?.ToJson())), local ?? new TguiLocalState());
        return (context, acts);
    }

    private static List<string> Buttons(UiNode tree) => tree.Walk().Where(n => n.Key != null && n.Key.StartsWith("play:", StringComparison.Ordinal)).Select(n => n.Text).ToList();

    [Fact]
    public void ThePanelListsTheEmotesByNameAndPlaysOne()
    {
        var (context, acts) = Context();
        UiNode tree = EmotePanel.Render(context);
        Assert.Equal((630.0, 500.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "Nod", "Scream", "Twitch (slight)", "Wave" }, Buttons(tree));
        Assert.Single(tree.Walk(), n => n.Key == "preview:scream"); // only an emote with a sound has the preview button
        tree.Walk().Single(n => n.Key == "play:nod").OnClick();
        tree.Walk().Single(n => n.Key == "preview:scream").OnClick();
        Assert.Equal(("play_emote", "{\"emote_key\":\"nod\",\"use_params\":false}"), acts[0]);
        Assert.Equal(("preview_sound", "{\"emote_key\":\"scream\"}"), acts[1]);
    }

    [Fact]
    public void TheFiltersAndTheSearchNarrowTheList()
    {
        var local = new TguiLocalState();
        var (context, _) = Context(local);
        EmotePanel.Render(context).Walk().Single(n => n.Key == "filterSound").OnClick();
        Assert.Equal(new[] { "Scream" }, Buttons(EmotePanel.Render(Context(local).Context)));
        local.Set("filterSound", false);
        local.Set("searchText", "TWI");
        UiNode tree = EmotePanel.Render(Context(local).Context);
        Assert.Equal(new[] { "Twitch (slight)" }, Buttons(tree));
        Assert.Contains(tree.Walk(), n => n.Title == "Search results of \"TWI\"");
        local.Set("searchText", "");
        local.Set("showNames", false);
        Assert.Equal(new[] { "nod", "scream", "twitch_s", "wave" }, Buttons(EmotePanel.Render(Context(local).Context)));
    }
}
