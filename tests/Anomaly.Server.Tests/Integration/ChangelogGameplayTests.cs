using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M10x slice H (D281, DV200): the lobby's Changelog button through the real server on MetaStation. A new player is told of the unread
/// updates (tg's lastchangelog against the changelog's hash); the button opens tg's Changelog window with the port's versions, newest
/// first, and the newest one's changes; get_month picks another; the hash is remembered in the preferences.
/// </summary>
public class ChangelogGameplayTests
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    [MetaServerFact]
    public void The_lobbys_Changelog_button_opens_tgs_window_with_the_ports_versions()
    {
        ServerConfig meta = MapJoinTests.MapConfig("metastation", MetaServer.Folder());
        var config = new ServerConfig
        {
            Seed = meta.Seed, Console = false, StatusEveryTicks = 100000, Map = meta.Map, MapsFolder = meta.MapsFolder, MoveSpeedScale = 1f, Lobby = true,
        };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 47, TimeoutSteps = 8000 }, config, strict: false);
        for (int i = 0; i < 10 * Second && In(w, () => GlobalProcs.SSticker.current_state) < Defines.GAME_STATE_PREGAME; i++)
            w.Run(1);
        var texts = new List<string>();
        LoopbackClientTransport transport = w.Network.CreateClient();
        var client = new GameClient(transport, "127.0.0.1", w.Server.Port, "Alice");
        client.ServerTextReceived += t => texts.Add(t.Html);
        Assert.Equal(FeedPhase.Playing, w.Connect(client));
        RealHeadlessWorld.Player alice = w.Adopt("Alice", client);
        w.Run(2 * Second);
        int bootRuntimes = w.World.RuntimeErrorCount;
        Assert.Contains(texts, t => t.Contains("You have unread updates in the changelog.", StringComparison.Ordinal));

        UiWindowState lobby = Assert.Single(alice.Session.Replica.UiWindows.Values, v => v.Interface == Limits.LobbyUiInterface);
        Assert.True(alice.Session.UiAct(lobby.Window, "changelog", null));
        w.Run(Second);
        UiWindowState window = Assert.Single(alice.Session.Replica.UiWindows.Values, v => v.Interface == "Changelog");
        List<string> dates = window.Merged.Get("dates").Items.Select(d => d.Text).ToList();
        Assert.Contains("1.0.0-pre-alpha", dates);
        Assert.Equal(dates[0], window.Merged.Str("selected"));
        Assert.NotEmpty(window.Merged.Get("changes").Items);

        Assert.True(alice.Session.UiAct(window.Window, "get_month", UiValue.ObjectOf(new[] { new KeyValuePair<string, UiValue>("date", UiValue.Of("1.0.0-pre-alpha")) })));
        w.Run(Second);
        window = Assert.Single(alice.Session.Replica.UiWindows.Values, v => v.Interface == "Changelog");
        Assert.Equal("1.0.0-pre-alpha", window.Merged.Str("selected"));
        Assert.Equal("2026-10-27", window.Merged.Str("date"));
        Assert.True(In(w, () => Dm.eq(FindClient("Alice").prefs.lastchangelog, GlobalProcs.GLOB.changelog_hash))); // tg's changelog verb remembers it
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }

    private static client FindClient(string key)
    {
        foreach (client c in Dm.each<client>(GlobalProcs.GLOB.clients))
        {
            if (c.key as string == key)
                return c;
        }
        return null;
    }
}
