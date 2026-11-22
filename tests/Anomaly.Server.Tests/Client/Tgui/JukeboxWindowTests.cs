using Anomaly.Client.Shared.Tgui;
using Anomaly.Client.Shared.Tgui.Interfaces;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Tgui;

/// <summary>
/// Gaps H3 (the bar): the client's Jukebox window (tg's Jukebox.tsx) of the jukebox and the radiant dance machine. It reads tg's
/// ui_data the way the TSX does (the Play/Stop button, the Repeat checkbox locked while playing, the track list sorted by name with
/// the selected track's length and beat, the volume knob red from 25 %) and sends tg's acts; it is registered under its name.
/// </summary>
public class JukeboxWindowTests
{
    private static (TguiContext Context, UiNode Tree, List<(string Action, string Params)> Acts) Render(string json)
    {
        var acts = new List<(string, string)>();
        var context = new TguiContext(1, "Jukebox", "device", 2, UiValue.FromJson(json), (a, p) => acts.Add((a, p?.ToJson())), new TguiLocalState());
        return (context, Jukebox.Render(context), acts);
    }

    private static UiNode ByKey(UiNode tree, string key) => tree.Walk().Single(n => n.Key == key);

    private const string Idle = "{\"active\":0,\"looping\":0,\"volume\":50,\"track_selected\":\"Tintin on the Moon\",\"songs\":["
        + "{\"name\":\"Tintin on the Moon\",\"length\":\"3 minutes and 52 seconds\",\"beat\":10},"
        + "{\"name\":\"A Space Oddity\",\"length\":\"5 minutes\",\"beat\":1}]}";

    [Fact]
    public void TheJukeboxWindowShowsTheSortedTracksTheSelectedTrackAndTheVolumeWithTgsActs()
    {
        var (context, tree, acts) = Render(Idle);
        Assert.Equal((370.0, 313.0), (context.WindowWidth, context.WindowHeight));
        Assert.Equal(new[] { "Track Selected", "Track Length", "Track Beat" }, tree.Walk().Where(n => n.Kind == UiKind.LabeledItem).Select(n => n.Label));
        Assert.Equal(("Play", "play", false), (ByKey(tree, "toggle").Text, ByKey(tree, "toggle").Icon, ByKey(tree, "toggle").Selected));
        Assert.False(ByKey(tree, "loop").Disabled);

        UiNode tracks = ByKey(tree, "track");
        Assert.Equal(new[] { "A Space Oddity", "Tintin on the Moon" }, tracks.Options);
        Assert.Equal("Tintin on the Moon", tracks.TextValue);
        Assert.Contains("3 minutes and 52 seconds", tree.AllText());
        Assert.Contains("10 beats", tree.AllText());

        UiNode knob = ByKey(tree, "volume");
        Assert.Equal((UiKind.Knob, 50.0, "%", 0.0, 50.0, "red"), (knob.Kind, knob.Value, knob.Unit, knob.MinValue, knob.MaxValue, knob.Color));

        ByKey(tree, "toggle").OnClick();
        ByKey(tree, "loop").OnClick();
        tracks.OnSelected("A Space Oddity");
        knob.OnChange(20);
        ByKey(tree, "min").OnClick();
        ByKey(tree, "max").OnClick();
        ByKey(tree, "reset").OnClick();
        Assert.Equal(new (string, string)[]
        {
            ("toggle", null), ("loop", "{\"looping\":true}"), ("select_track", "{\"track\":\"A Space Oddity\"}"),
            ("set_volume", "{\"volume\":20}"), ("set_volume", "{\"volume\":\"min\"}"), ("set_volume", "{\"volume\":\"max\"}"),
            ("set_volume", "{\"volume\":\"reset\"}"),
        }, acts);
    }

    [Fact]
    public void WhilePlayingTheTrackAndRepeatAreLockedAndAQuietVolumeIsGreen()
    {
        var (_, tree, _) = Render("{\"active\":1,\"looping\":1,\"volume\":10,\"track_selected\":\"A Space Oddity\",\"songs\":["
            + "{\"name\":\"A Space Oddity\",\"length\":\"5 minutes\",\"beat\":1}]}");
        Assert.Equal(("Stop", "pause", true), (ByKey(tree, "toggle").Text, ByKey(tree, "toggle").Icon, ByKey(tree, "toggle").Selected));
        Assert.True(ByKey(tree, "loop").Disabled);
        Assert.True(ByKey(tree, "loop").Checkbox);
        Assert.True(ByKey(tree, "track").Disabled);
        Assert.Contains("1 beat", tree.AllText());
        Assert.DoesNotContain("1 beats", tree.AllText());
        Assert.Equal("green", ByKey(tree, "volume").Color);

        // nothing selected yet
        var (_, none, _) = Render("{\"active\":0,\"looping\":0,\"volume\":50,\"track_selected\":null,\"songs\":[]}");
        Assert.Equal("Select a Track", ByKey(none, "track").TextValue);
        Assert.Contains("No Track Selected", none.AllText());
    }

    [Fact]
    public void TheJukeboxWindowIsRegisteredUnderItsName()
    {
        Assert.Equal((TguiInterface)Jukebox.Render, TguiInterfaces.Find("Jukebox"));
    }
}
