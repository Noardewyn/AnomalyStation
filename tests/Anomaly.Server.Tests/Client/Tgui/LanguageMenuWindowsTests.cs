using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps L2 (D327): the client's LanguageMenu window without the engine: the spoken languages first, then the understood ones by name,
/// the unknown ones hidden but for an admin, the speak, understand and partial marks, the key, the default checkbox and the admin's
/// grant, removal and omnitongue.
/// </summary>
public class LanguageMenuWindowsTests
{
    private static (UiNode Tree, TguiContext Context, List<(string Action, string Params)> Acts) Render(string json)
    {
        var acts = new List<(string, string)>();
        var c = new TguiContext(1, "LanguageMenu", "LanguageMenu", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (LanguageMenu.Render(c), c, acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().First(n => n.Key == key);

    private const string Json = "{\"is_living\":1,\"admin_mode\":0,\"omnitongue\":0,\"languages\":["
        + "{\"name\":\"Uncommon\",\"desc\":\"Old.\",\"key\":\"!\",\"is_default\":0,\"can_speak\":0,\"could_speak\":1,\"can_understand\":1,\"partial_understanding\":0,\"icon\":\"icons/ui/chat/language.dmi\",\"icon_state\":\"galuncom\"},"
        + "{\"name\":\"Galactic Common\",\"desc\":\"Common.\",\"key\":\"0\",\"is_default\":1,\"can_speak\":1,\"could_speak\":1,\"can_understand\":1,\"partial_understanding\":0,\"icon\":\"icons/ui/chat/language.dmi\",\"icon_state\":\"galcom\"},"
        + "{\"name\":\"Draconic\",\"desc\":\"Hiss.\",\"key\":\"o\",\"is_default\":0,\"can_speak\":0,\"could_speak\":0,\"can_understand\":0,\"partial_understanding\":20,\"icon\":\"\",\"icon_state\":\"\"},"
        + "{\"name\":\"Moffic\",\"desc\":\"Flutter.\",\"key\":\"m\",\"is_default\":0,\"can_speak\":0,\"could_speak\":0,\"can_understand\":0,\"partial_understanding\":0,\"icon\":\"\",\"icon_state\":\"\"}]}";

    [Fact]
    public void TheMenuListsWhatTheMobSpeaksAndUnderstands()
    {
        var (tree, context, acts) = Render(Json);
        Assert.Equal((500.0, 145.0, "Language Menu"), (context.WindowWidth, context.WindowHeight, context.WindowTitle));
        var rows = tree.Walk().Where(n => n.Kind == UiKind.TableRow && n.Key.StartsWith("language:", StringComparison.Ordinal)).Select(n => n.Key).ToArray();
        Assert.Equal(new[] { "language:Galactic Common", "language:Draconic", "language:Uncommon" }, rows); // spoken first; Moffic hidden
        UiNode common = ByKey(tree, "language:Galactic Common");
        Assert.Equal(("good", ",0"), (ByKey(common, "speakIcon").Color, ByKey(common, "key").Text));
        Assert.True(ByKey(common, "default").Checkbox);
        Assert.Equal("20%", ByKey(ByKey(tree, "language:Draconic"), "partial").Text);
        Assert.Equal("grey", ByKey(ByKey(tree, "language:Draconic"), "speakIcon").Color);
        Assert.Equal("bad", ByKey(ByKey(tree, "language:Uncommon"), "speakIcon").Color); // could speak, cannot
        ByKey(ByKey(tree, "language:Uncommon"), "default").OnClick();
        Assert.Equal(("select_default", "{\"language_name\":\"Uncommon\"}"), Assert.Single(acts));
    }

    [Fact]
    public void AnAdminSeesEveryLanguageAndGrants()
    {
        var (tree, context, acts) = Render(Json.Replace("\"admin_mode\":0", "\"admin_mode\":1"));
        Assert.Equal(700.0, context.WindowWidth);
        Assert.Equal("Omnitongue Disabled", ByKey(tree, "omnitongue").Text);
        UiNode moffic = ByKey(tree, "language:Moffic");
        Assert.True(ByKey(moffic, "remove").Disabled);
        ByKey(moffic, "grant").OnClick();
        ByKey(tree, "omnitongue").OnClick();
        Assert.Equal(new (string, string)[] { ("grant_language", "{\"language_name\":\"Moffic\"}"), ("toggle_omnitongue", null) }, acts);
    }
}
