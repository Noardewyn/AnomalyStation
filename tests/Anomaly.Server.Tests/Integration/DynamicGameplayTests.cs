using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Syndicate package A3 (D316) through the real server: MetaStation with the lobby and a config folder whose dynamic.toml (tg's file,
/// edited as a server operator would) leaves Low Chaos the only tier and lets the roundstart traitors start from any population, two at
/// most. Alice and Bob turn the traitor on in the lobby's Antagonists page (tg's set_antags; closing the menu saves it to the player
/// saves), Carol does not; the host opens the Dynamic panel from the console (dynamic-panel) and sets the tier there (tg's list prompt);
/// at the round's start Alice and Bob are the traitors, with tg's greeting and its stinger sound, and Carol is not; the panel then shows
/// the tier and the roundstart traitors with their players.
/// </summary>
public sealed class DynamicGameplayTests : IDisposable
{
    private const int Second = 20;
    private const float GameStatePregame = Defines.GAME_STATE_PREGAME, GameStatePlaying = Defines.GAME_STATE_PLAYING;

    private readonly ITestOutputHelper _output;
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "anomaly-dynamic-" + Guid.NewGuid().ToString("N"));

    public DynamicGameplayTests(ITestOutputHelper output)
    {
        _output = output;
        Directory.CreateDirectory(Path.Combine(_folder, "saves"));
        File.WriteAllText(Path.Combine(_folder, "dynamic.toml"),
            "[\"Greenshift\"]\nweight = 0\n\n[\"Low Chaos\"]\nweight = 1\nruleset_type_settings.roundstart.low = 1\nruleset_type_settings.roundstart.high = 1\n\n"
            + "[\"Low-Medium Chaos\"]\nweight = 0\n\n[\"Medium-High Chaos\"]\nweight = 0\n\n[\"High Chaos\"]\nweight = 0\n\n"
            + "[\"Roundstart Traitor\"]\nmin_pop = 0\nmax_antag_cap = 2\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static mob MobOf(RealHeadlessWorld w, RealHeadlessWorld.Player p)
        => w.World.Objects.TryGet(w.Host.AvatarOf(p.Name), out object o) ? o as mob : null;

    private static TguiWindows WindowsOf(RealHeadlessWorld.Player p) => new()
    {
        SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
        SendClose = window => p.Session.UiClose(window),
    };

    private static TguiWindow Window(TguiWindows windows, RealHeadlessWorld.Player p, string @interface)
    {
        windows.Sync(p.Session.Replica.UiWindows, 1280, 720, 1);
        return windows.Open.FirstOrDefault(x => x.Interface == @interface);
    }

    // The lobby's character setup, its Antagonists page, a click on the traitor's picture, then the menu closed (tg's ui_close saves)
    private static void PreferTraitor(RealHeadlessWorld w, RealHeadlessWorld.Player p)
    {
        TguiWindows windows = WindowsOf(p);
        Window(windows, p, Limits.LobbyUiInterface).Context.Act("character_setup");
        w.Run(Second / 2);
        TguiWindow prefs = Window(windows, p, "PreferencesMenu");
        Assert.NotNull(prefs);
        prefs.Content.Walk().Single(n => n.Key == "page:Antagonists").Children[0].OnClick();
        prefs = Window(windows, p, "PreferencesMenu");
        UiNode traitor = prefs.Content.Walk().Single(n => n.Key == "antag:traitor").Walk().Single(n => n.Key == "icon");
        Assert.Equal("red", traitor.Color); // tg's default for a new player: nothing on
        traitor.OnClick();
        w.Run(Second / 2);
        prefs = Window(windows, p, "PreferencesMenu");
        Assert.Equal("green", prefs.Content.Walk().Single(n => n.Key == "antag:traitor").Walk().Single(n => n.Key == "icon").Color);
        p.Session.UiClose(prefs.Index);
        w.Run(Second / 2);
    }

    [MetaServerFact]
    public void Two_players_who_prefer_the_traitor_are_Dynamics_roundstart_traitors()
    {
        ServerConfig meta = MapJoinTests.MapConfig("metastation", MetaServer.Folder());
        var config = new ServerConfig
        {
            Seed = meta.Seed, Console = false, StatusEveryTicks = 100000, Map = meta.Map, MapsFolder = meta.MapsFolder, MoveSpeedScale = 1f, Lobby = true,
            ConfigFile = Path.Combine(_folder, "server.txt"), PlayerSavesFolder = Path.Combine(_folder, "saves"),
        };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 47, TimeoutSteps = 8000 }, config, strict: false);
        int bootRuntimes = w.World.RuntimeErrorCount;
        for (int i = 0; i < 10 * Second && In(w, () => GlobalProcs.SSticker.current_state) < GameStatePregame; i++)
            w.Run(1);
        Assert.Equal(GameStatePregame, In(w, () => GlobalProcs.SSticker.current_state));

        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        RealHeadlessWorld.Player carol = w.Join("Carol");
        var texts = new Dictionary<string, List<string>>();
        var heard = new List<SoundEvent>();
        foreach (RealHeadlessWorld.Player p in new[] { alice, bob, carol })
        {
            var mine = texts[p.Name] = new List<string>();
            p.Client.ServerTextReceived += t => mine.Add(t.Html);
        }
        alice.Session.Replica.SoundsReceived += events => heard.AddRange(events);

        PreferTraitor(w, alice);
        PreferTraitor(w, bob);
        // tg's savefile has it: preferences.json's be_special
        string saved = File.ReadAllText(Path.Combine(_folder, "saves", "a", "alice", "preferences.json"));
        Assert.Contains("\"be_special\"", saved, StringComparison.Ordinal);
        Assert.Contains("\"Traitor\"", saved, StringComparison.Ordinal);

        // the host's Dynamic panel: no tier yet; tg's Set tier prompt
        TguiWindows aliceWindows = WindowsOf(alice);
        alice.Session.DevCommand("dynamic-panel");
        w.Run(Second / 2);
        TguiWindow panel = Window(aliceWindows, alice, "DynamicAdmin");
        Assert.NotNull(panel);
        Assert.Equal(6, panel.Context.Data.Obj("full_config").Count); // the six tables of the test's file
        Assert.Contains(panel.Context.Data.Obj("all_rulesets"), e => e.Key == "roundstart" && e.Value.Items.Any(r => r.Str("id") == "Roundstart Traitor"));
        panel.Content.Walk().Single(n => n.Text == "(Click to set)").OnClick();
        w.Run(Second / 2);
        TguiWindow tiers = Window(aliceWindows, alice, "ListInputWindow");
        Assert.NotNull(tiers);
        Assert.True(alice.Session.UiAct(tiers.Index, "submit", UiValue.ObjectOf(("entry", UiValue.Of("Low Chaos")))));
        w.Run(Second / 2);
        Assert.Equal("Low Chaos", In(w, () => GlobalProcs.SSdynamic.current_tier?.config_tag as string));

        foreach (RealHeadlessWorld.Player p in new[] { alice, bob, carol })
            Window(WindowsOf(p), p, Limits.LobbyUiInterface).Context.Act("ready_toggle");
        w.Run(Second / 2);
        alice.Session.DevCommand("startnow");
        w.Run(5 * Second);
        Assert.Equal(GameStatePlaying, In(w, () => GlobalProcs.SSticker.current_state));

        foreach (RealHeadlessWorld.Player p in new[] { alice, bob })
        {
            var body = Assert.IsAssignableFrom<mob_living_carbon_human>(MobOf(w, p));
            Assert.True(In(w, () => Dm.truthy(body.mind.has_antag_datum(typeof(datum_antagonist_traitor)))), p.Name + " is no traitor");
            Assert.True(texts[p.Name].Any(t => t.Contains("You are the Traitor.", StringComparison.Ordinal)), string.Join(" | ", texts[p.Name].Where(t => t.Contains("You are", StringComparison.Ordinal) || t.Contains("raitor", StringComparison.Ordinal))));
        }
        var carolBody = Assert.IsAssignableFrom<mob_living_carbon_human>(MobOf(w, carol));
        Assert.False(In(w, () => Dm.truthy(carolBody.mind.has_antag_datum(typeof(datum_antagonist_traitor)))));
        Assert.DoesNotContain(texts[carol.Name], t => t.Contains("You are the Traitor.", StringComparison.Ordinal));
        Assert.True(heard.Any(h => (h.File ?? "").Contains("tatoralert", StringComparison.Ordinal)), string.Join(", ", heard.Select(h => h.Kind + ":" + h.File))); // the traitor's stinger

        // the panel after the start: the tier and the executed roundstart traitors with their players
        alice.Session.DevCommand("dynamic-panel");
        w.Run(Second / 2);
        panel = Window(aliceWindows, alice, "DynamicAdmin");
        Assert.Equal("Low Chaos", panel.Context.Data.Get("current_tier").Str("name"));
        panel.Content.Walk().Single(n => n.Kind == UiKind.Tab && n.Text == "Rulesets").OnClick();
        panel = Window(aliceWindows, alice, "DynamicAdmin");
        string selected = panel.Content.Walk().Single(n => n.Key == "selected").Text;
        Assert.True(selected is "Selected: Alice and Bob" or "Selected: Bob and Alice", selected);
        _output.WriteLine(w.Err.ToString());
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
