using System.IO;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps R2 (D327): the station traits through the real server (the test room, tg's lobby, station traits on). An admin of the Host
/// rank opens Modify Station Traits from the console (station-traits), plans the Great Skub Contention for the next round in its Edit
/// tab and reboots the world; the next round rolls only that trait (the host carried tg's future traits file over the reboot), and
/// the lobby shows its sign-up button, which cycles the admin's stance with tg's feedback line. A player who is no admin gets no
/// window. On MetaStation (the holidays' monkey day path: SSstation.setup_trait during the pregame), Pun Pun is a Crewmember: the bar's
/// Pun Pun gives his place to the job's start, the lobby shows the trait's button, and the player who signs up and readies starts
/// the round as Pun Pun at the bar, a crew monkey in his waiter's suit.
/// </summary>
public sealed class StationTraitsGameplayTests : IDisposable
{
    private const int Second = 20;
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "anomaly-traits-" + Guid.NewGuid().ToString("N"));

    public StationTraitsGameplayTests()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "admin_ranks.txt"), """
            Name = Host
            Include = EVERYTHING
            Exclude =
            Edit = EVERYTHING
            """.Replace("\r\n", "\n"));
        File.WriteAllText(Path.Combine(_folder, "admins.txt"), "Boss = Host\n");
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); }
        catch (IOException) { }
    }

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

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

    private static RealHeadlessWorld.Player JoinFrom(RealHeadlessWorld w, string name, string address)
    {
        LoopbackClientTransport transport = w.Network.CreateClient();
        transport.Address = address;
        var client = new GameClient(transport, "127.0.0.1", w.Server.Port, name, default);
        Assert.Equal(FeedPhase.Playing, w.Connect(client));
        return w.Adopt(name, client);
    }

    private static void RunUntil(RealHeadlessWorld w, Func<bool> done, int ticks, string what)
    {
        for (int i = 0; i < ticks && !done(); i++)
            w.Run(1);
        Assert.True(done(), what);
    }

    [Fact]
    public void An_admin_plans_the_skub_contention_and_the_next_rounds_lobby_shows_its_button()
    {
        var config = new ServerConfig
        {
            Seed = 327, RoomWidth = 24, RoomHeight = 24, Console = false, StatusEveryTicks = 100000, MoveSpeedScale = 1f, DevCommands = true,
            Lobby = true, StationTraits = true, ConfigFile = Path.Combine(_folder, "server.txt"),
        };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 327, TimeoutSteps = 4000 }, config, strict: false);
        RealHeadlessWorld.Player boss = w.Join("Boss");
        RealHeadlessWorld.Player pleb = JoinFrom(w, "Pleb", "10.1.2.4"); // not localhost: no admin (ENABLE_LOCALHOST_RANK)
        w.Run(Second);

        TguiWindows plebWindows = WindowsOf(pleb);
        pleb.Session.DevCommand("station-traits");
        w.Run(Second / 2);
        Assert.Null(Window(plebWindows, pleb, "StationTraitsPanel"));

        // the panel: the Edit tab, the skub contention picked in the dropdown and added
        TguiWindows windows = WindowsOf(boss);
        boss.Session.DevCommand("station-traits");
        w.Run(Second / 2);
        TguiWindow panel = Window(windows, boss, "StationTraitsPanel");
        Assert.NotNull(panel);
        panel.Content.Walk().Single(n => n.Key == "edit").OnClick();
        panel = Window(windows, boss, "StationTraitsPanel");
        panel.Content.Walk().Single(n => n.Key == "select_trait").OnSelected("The Great Skub Contention");
        panel = Window(windows, boss, "StationTraitsPanel");
        panel.Content.Walk().Single(n => n.Key == "add").OnClick();
        w.Run(Second / 2);
        panel = Window(windows, boss, "StationTraitsPanel");
        Assert.Contains(panel.Content.Walk(), n => n.Text == "The Great Skub Contention" && n.Kind == UiKind.StackItem);
        Assert.Equal("[\"/datum/station_trait/skub\"]", In(w, () => Dm.file2text(Defines.FUTURE_STATION_TRAITS_FILE)));

        // the next round: only the planned trait, its button in the lobby
        boss.Session.DevCommand("reboot now");
        RunUntil(w, () => w.Host.Restarts == 1, 20 * Second, "the world reboots");
        RunUntil(w, () => In(w, () => GlobalProcs.SSstation?.station_traits?.len > 0), 30 * Second, "the next round sets its traits up");
        Assert.Equal(new[] { typeof(datum_station_trait_skub) }, In(w, () => Dm.each<datum_station_trait>(GlobalProcs.SSstation.station_traits).Select(t => t.GetType()).ToArray()));

        windows = WindowsOf(boss);
        RunUntil(w, () => Window(windows, boss, Limits.LobbyUiInterface)?.Context.Data.Arr("stationTraits").Count > 0, 30 * Second, "the lobby shows the trait's button");
        TguiWindow lobby = Window(windows, boss, Limits.LobbyUiInterface);
        UiValue button = lobby.Context.Data.Arr("stationTraits").Single();
        Assert.Equal("The Great Skub Contention", button.Str("name"));
        Assert.Equal("signup_neutral", button.Str("iconState"));
        Assert.Equal(new[] { "random_skub" }, button.Arr("overlays").Select(o => o.Text));
        Assert.Equal("Are you pro-skub or anti-skub? Click to cycle through pro-skub, anti-skub, random and neutral.", button.Str("description"));

        lobby.Context.Act("sign_up", ("ref", button.Str("ref")));
        RunUntil(w, () => Window(windows, boss, Limits.LobbyUiInterface).Context.Data.Str("traitFeedback") == "pro-skub", 2 * Second, "the button answers pro-skub");
        button = Window(windows, boss, Limits.LobbyUiInterface).Context.Data.Arr("stationTraits").Single();
        Assert.Equal("signup_on", button.Str("iconState"));
        Assert.Equal(new[] { "pro_skub" }, button.Arr("overlays").Select(o => o.Text));
    }

    private static mob MobOf(RealHeadlessWorld w, RealHeadlessWorld.Player p)
        => w.World.Objects.TryGet(w.Host.AvatarOf(p.Name), out object o) ? o as mob : null;

    [MetaServerFact]
    public void On_MetaStation_the_player_who_signs_up_for_Pun_Pun_starts_the_round_as_the_bars_monkey()
    {
        ServerConfig meta = MapJoinTests.MapConfig("metastation", MetaServer.Folder());
        var config = new ServerConfig
        {
            Seed = meta.Seed, Console = false, StatusEveryTicks = 100000, Map = meta.Map, MapsFolder = meta.MapsFolder, MoveSpeedScale = 1f,
            Lobby = true, StationTraits = true, DevCommands = true,
        };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 328, TimeoutSteps = 8000 }, config, strict: false);
        RunUntil(w, () => In(w, () => GlobalProcs.SSticker.current_state) >= Defines.GAME_STATE_PREGAME, 10 * Second, "the pregame");

        // the bar's Pun Pun, then the trait (as the holidays' monkey day sets it up): he gives his place to the job's start
        (int X, int Y) bar = In(w, () =>
        {
            mob_living punpun = GlobalProcs.GLOB.the_one_and_only_punpun;
            Assert.NotNull(punpun);
            var spot = (punpun.x, punpun.y, punpun.z);
            GlobalProcs.SSstation.setup_trait(typeof(datum_station_trait_job_pun_pun));
            Assert.True(GlobalProcs.QDELETED(punpun));
            Assert.Null(GlobalProcs.GLOB.the_one_and_only_punpun);
            Assert.NotNull(Dm.locate<obj_effect_landmark_start_pun_pun>(Builtins.locate(spot.x, spot.y, spot.z)));
            return ((int)spot.x, (int)spot.y);
        });

        RealHeadlessWorld.Player alice = w.Join("Alice");
        TguiWindows windows = WindowsOf(alice);
        RunUntil(w, () => Window(windows, alice, Limits.LobbyUiInterface)?.Context.Data.Arr("stationTraits")
            .Any(t => t.Str("name") == "Pun Pun is a Crewmember") == true, 5 * Second, "the lobby shows Pun Pun's button");
        TguiWindow lobby = Window(windows, alice, Limits.LobbyUiInterface);
        UiValue button = lobby.Context.Data.Arr("stationTraits").Single(t => t.Str("name") == "Pun Pun is a Crewmember");
        Assert.Equal("Ook ook ah ah, sign up to play as the bartender's monkey.", button.Str("description"));
        Assert.Equal(new[] { "pun_pun_off" }, button.Arr("overlays").Select(o => o.Text));
        lobby.Context.Act("sign_up", ("ref", button.Str("ref")));
        RunUntil(w, () => Window(windows, alice, Limits.LobbyUiInterface).Context.Data.Arr("stationTraits")
            .Single(t => t.Str("name") == "Pun Pun is a Crewmember").Arr("overlays").Select(o => o.Text).SequenceEqual(new[] { "pun_pun_on" }),
            2 * Second, "the button shows Alice signed up");
        Window(windows, alice, Limits.LobbyUiInterface).Context.Act("ready_toggle");
        w.Run(Second / 2);
        Assert.True(Window(windows, alice, Limits.LobbyUiInterface).Context.Data.Truthy("isReady"));

        // the round starts: tg's PRE_JOBS_ASSIGNED gives her Pun Pun's job, and the job spawns her as the bar's monkey
        alice.Session.DevCommand("startnow");
        RunUntil(w, () => In(w, () => GlobalProcs.SSticker.current_state) == Defines.GAME_STATE_PLAYING && MobOf(w, alice) is mob_living_carbon_human,
            10 * Second, "the round starts with Alice in her body");
        var monkey = Assert.IsType<mob_living_carbon_human_species_monkey_punpun>(MobOf(w, alice));
        Assert.Equal("Pun Pun", In(w, () => Dm.str(monkey.mind.assigned_role.title)));
        Assert.Same(monkey, In(w, () => GlobalProcs.GLOB.the_one_and_only_punpun));
        Assert.Equal(bar, In(w, () => ((int)monkey.x, (int)monkey.y)));
        Assert.True(In(w, () => GlobalProcs.HAS_TRAIT(monkey, Defines.TRAIT_BORN_MONKEY))); // crewlike_monkify
        Assert.Equal(typeof(obj_item_clothing_under_suit_waiter), In(w, () => monkey.w_uniform?.GetType()));
        RunUntil(w, () => Window(windows, alice, Limits.LobbyUiInterface) == null, 5 * Second, "the lobby closes");
    }
}
