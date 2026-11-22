using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Anomaly.Tools.Bots;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// ROADMAP M10's demo check (multiplayer.md section 1, slice K) through the real server on MetaStation with the shipped lobby config
/// (the lobby on, open jobs): ten bots join tg's pregame lobby and ready up; Alice names her character in tg's character setup, sets the
/// medical doctor to High on its Occupations page and readies; Bob connects through the connect screen's join (his token kept) and stays
/// not ready. Alice's startnow starts the round: she plays her character in tg's doctor outfit, every bot a living human with the job tg's
/// DivideOccupations gave it, and the lobby closes. Bob late-joins from the lobby's Join and tg's job menu as a station engineer on the
/// arrivals shuttle. The bots wander for half a minute without a desync; the emergency shuttle (tg's timers shortened) ends the round with
/// tg's report and the reboot countdown, the host restarts, Alice's and Bob's clients wait between rounds on their connections (D306) and both join
/// the new round's lobby on them, as every bot does. No runtime beyond the boots' in either world, and the old world is collected.
/// </summary>
public class M10ScenarioTests(ITestOutputHelper output)
{
    private const int Second = 20;
    private const int BotCount = 10;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void RunUntil(RealHeadlessWorld w, Func<bool> until, int maxTicks, string what)
    {
        for (int i = 0; i < maxTicks && !until(); i++)
            w.Run(1);
        Assert.True(until(), what);
    }

    private static object Mob(RealHeadlessWorld w, string name) => w.World.Objects.TryGet(w.Host.AvatarOf(name), out object o) ? o : null;

    private static bool Ready(Bot bot) => bot.Replica.UiWindows.TryGetValue(Limits.LobbyUiWindow, out UiWindowState lobby)
        && lobby.Merged.Get("isReady")?.Truthy == true;

    private static TguiWindows WindowsOf(RealHeadlessWorld.Player p) => new()
    {
        SendAct = (window, action, parameters) => p.Session.UiAct(window, action, parameters),
        SendClose = window => p.Session.UiClose(window),
    };

    // The windows are kept per player where a page's local state (the character setup's tab) matters.
    private static TguiWindow Window(RealHeadlessWorld.Player p, string @interface, TguiWindows windows = null)
    {
        windows ??= WindowsOf(p);
        windows.Sync(p.Session.Replica.UiWindows, 1280, 720, 1);
        return windows.Open.FirstOrDefault(x => x.Interface == @interface);
    }

    private static void WaitForPregame(RealHeadlessWorld w)
        => RunUntil(w, () => In(w, () => Dm.num(SSticker.current_state)) >= GAME_STATE_PREGAME, 30 * Second, "tg's ticker enters the pregame");

    private static string Server(RealHeadlessWorld w) => "127.0.0.1:" + w.Server.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);

    [MetaServerFact]
    public void A_round_runs_from_the_lobby_to_the_restart_with_bots_and_two_clients()
    {
        ServerConfig meta = MapJoinTests.MapConfig("metastation", MetaServer.Folder());
        var config = new ServerConfig
        {
            Seed = meta.Seed, Console = false, StatusEveryTicks = 100000, Map = meta.Map, MapsFolder = meta.MapsFolder, MoveSpeedScale = 1f,
            Lobby = true, OpenJobs = true,
        };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 71, TimeoutSteps = 60000 }, config, strict: false);
        var bobTokens = new ReconnectTokens(null);
        (RealHeadlessWorld.Player alice, RealHeadlessWorld.Player bob, int bootRuntimes, World[] first, WeakReference oldWorld) = FirstRound(w, bobTokens);

        // the restart (D306): the host's new world, Alice's and Bob's connections kept while they wait between rounds
        RunUntil(w, () => w.Host.Restarts == 1, 240 * Second, "world/Reboot asks the host for a new round");
        w.Run(1);
        foreach (RealHeadlessWorld.Player p in new[] { alice, bob })
            Assert.Equal(FeedPhase.BetweenRounds, p.Client.Phase);
        output.WriteLine(w.Err.ToString());
        Assert.Equal(bootRuntimes, TakeRuntimes(first)); // the whole first round, its end and the teardown, without a runtime
        WaitForPregame(w);
        int newBootRuntimes = w.World.RuntimeErrorCount;

        // both join the new round's lobby on the same connections (no reconnect: a GameClient connects once)
        RunUntil(w, () => alice.Client.Phase == FeedPhase.Playing && bob.Client.Phase == FeedPhase.Playing, 30 * Second, "both clients join the new round");
        foreach (RealHeadlessWorld.Player p in new[] { alice, bob })
        {
            Assert.False(p.BetweenRounds); // the game scene started again on the same feed, with a new play session
            Assert.IsType<mob_dead_new_player>(Mob(w, p.Name));
            w.Run(Second / 2);
            TguiWindow lobby = Window(p, Limits.LobbyUiInterface);
            Assert.NotNull(lobby);
            Assert.Equal("pregame", lobby.Context.Data.Str("gamePhase"));
            Assert.False(lobby.Context.Data.Truthy("isReady"));
        }

        // every bot rejoins the next lobby and readies
        RunUntil(w, () => w.Bots.All(x => x.State == BotState.Joined && Mob(w, x.Options.Name) is mob_dead_new_player && Ready(x)), 60 * Second,
            "every bot rejoins the next lobby and readies");
        foreach (Bot bot in w.Bots)
        {
            Assert.Equal(1, bot.Stats.RoundRestarts);
            Assert.Equal(2, bot.Stats.Joins);
            Assert.Equal(0, bot.Stats.Desyncs);
        }
        w.Run(Second);
        output.WriteLine(w.Err.ToString());
        Assert.Equal(newBootRuntimes, w.World.RuntimeErrorCount);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(oldWorld.IsAlive, "the old world is still held after the restart");
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static int TakeRuntimes(World[] holder)
    {
        int count = holder[0].RuntimeErrorCount;
        holder[0] = null; // the last strong reference of the old world
        return count;
    }

    // The first round, in its own frame (no reference of its world outlives the call but the returned holder, cleared after the restart): from
    // the pregame to the round end at CentCom.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private (RealHeadlessWorld.Player, RealHeadlessWorld.Player, int, World[], WeakReference) FirstRound(RealHeadlessWorld w, ReconnectTokens bobTokens)
    {
        WaitForPregame(w);
        int bootRuntimes = w.World.RuntimeErrorCount;

        // the pregame: ten bots ready up
        for (int i = 0; i < BotCount; i++)
        {
            var bot = new Bot(() => w.Network.CreateClient(), new BotOptions
            {
                Name = "Bot" + (char)('A' + i), Seed = (ulong)(i + 1), Behaviour = BotBehaviour.Wander, JoinRetryUpdates = 120 * Second,
            });
            w.Bots.Add(bot);
            bot.Connect("127.0.0.1", w.Server.Port);
        }
        RunUntil(w, () => w.Bots.All(b => b.State == BotState.Joined && Ready(b)), 30 * Second, "every bot joins and readies up");
        Assert.All(w.Bots, b => Assert.IsType<mob_dead_new_player>(Mob(w, b.Options.Name)));

        // Alice: the character setup, her name, the medical doctor High on the Occupations page, Ready
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var aliceTexts = new List<string>();
        alice.Client.ServerTextReceived += t => aliceTexts.Add(t.Html);
        TguiWindows aliceWindows = WindowsOf(alice);
        Window(alice, Limits.LobbyUiInterface).Context.Act("character_setup");
        w.Run(Second / 2);
        TguiWindow prefs = Window(alice, "PreferencesMenu", aliceWindows);
        prefs.Context.Act("set_preference", ("preference", "real_name"), ("value", "ada lovelace"));
        w.Run(Second / 2);
        prefs = Window(alice, "PreferencesMenu", aliceWindows);
        prefs.Content.Walk().Single(n => n.Kind == UiKind.Button && n.Text == "Occupations").OnClick();
        prefs = Window(alice, "PreferencesMenu", aliceWindows);
        UiNode High() => prefs.Content.Walk().Single(n => n.Key == "job:Medical Doctor").Walk().Single(n => n.Kind == UiKind.Button && n.Tooltip == "High");
        High().OnClick();
        w.Run(Second / 2);
        prefs = Window(alice, "PreferencesMenu", aliceWindows);
        Assert.Equal("green", High().Color);
        Window(alice, Limits.LobbyUiInterface).Context.Act("ready_toggle");
        w.Run(Second / 2);
        Assert.True(Window(alice, Limits.LobbyUiInterface).Context.Data.Truthy("isReady"));

        // Bob: the connect screen's join (his token kept for the next round), not ready
        var bobJoin = new JoinAttempt(() => w.Network.CreateClient(), bobTokens, "127.0.0.1", w.Server.Port, "Bob");
        Assert.Equal(FeedPhase.Playing, w.Join(bobJoin));
        RealHeadlessWorld.Player bob = w.Adopt("Bob", bobJoin.Client);
        var bobTexts = new List<string>();
        bob.Client.ServerTextReceived += t => bobTexts.Add(t.Html);
        Assert.IsType<mob_dead_new_player>(Mob(w, "Bob"));
        Assert.Equal(1, bobTokens.Count);

        // the round start: Alice the doctor, every bot a living human with a job, Bob still in the lobby
        alice.Session.DevCommand("startnow");
        RunUntil(w, () => In(w, () => Dm.num(SSticker.current_state)) == GAME_STATE_PLAYING, 10 * Second, "startnow starts the round");
        w.Run(Second);
        var ada = Assert.IsType<mob_living_carbon_human>(Mob(w, "Alice"));
        Assert.Equal("Ada Lovelace", In(w, () => ada.real_name));
        Assert.Equal("Medical Doctor", In(w, () => ada.mind.assigned_role.title));
        Assert.Equal(typeof(obj_item_clothing_under_rank_medical_doctor), In(w, () => ada.w_uniform?.GetType()));
        Assert.Null(Window(alice, Limits.LobbyUiInterface));
        var jobs = new List<string>();
        foreach (Bot b in w.Bots)
        {
            var human = Assert.IsType<mob_living_carbon_human>(Mob(w, b.Options.Name));
            Assert.NotEqual(DEAD, In(w, () => human.stat));
            string title = In(w, () => Dm.str(human.mind?.assigned_role?.title));
            Assert.False(string.IsNullOrEmpty(title), b.Options.Name + " has a job");
            jobs.Add(b.Options.Name + ": " + title);
        }
        output.WriteLine("bots' jobs: " + string.Join(", ", jobs));
        Assert.IsType<mob_dead_new_player>(Mob(w, "Bob"));
        Assert.NotNull(Window(bob, Limits.LobbyUiInterface));

        // Bob late-joins: the lobby's Join, tg's job menu, the station engineer on the arrivals shuttle
        Window(bob, Limits.LobbyUiInterface).Context.Act("join", ("ctrlClick", false));
        w.Run(Second / 2);
        TguiWindow menu = Window(bob, "JobSelection");
        Assert.NotNull(menu);
        Assert.Null(menu.Context.Data.Get("departments")?.Get("Engineering")?.Get("jobs")?.Get("Station Engineer")?.Get("unavailable_reason"));
        menu.Context.Act("select_job", ("job", "Station Engineer"));
        w.Run(2 * Second);
        var engineer = Assert.IsType<mob_living_carbon_human>(Mob(w, "Bob"));
        Assert.Equal("Station Engineer", In(w, () => engineer.mind.assigned_role.title));
        Assert.IsType<area_shuttle_arrival>(In(w, () => get_area(engineer)));
        Assert.Null(Window(bob, Limits.LobbyUiInterface));

        // the play: half a minute of the bots wandering
        long[] moves = w.Bots.Select(b => b.Stats.OwnMoves).ToArray();
        w.Run(30 * Second);
        for (int i = 0; i < w.Bots.Count; i++)
        {
            Assert.True(w.Bots[i].Stats.OwnMoves > moves[i], w.Bots[i].Options.Name + " wanders");
            Assert.Equal(0, w.Bots[i].Stats.Desyncs);
        }
        output.WriteLine($"after the play: runtimes {w.World.RuntimeErrorCount - bootRuntimes} beyond the boot's");

        // the round end: the shuttle with tg's timers shortened, the report and the reboot countdown at SHUTTLE_ENDGAME
        using (w.World.Enter())
        {
            SSshuttle.emergency_call_time = 100;
            SSshuttle.emergency_dock_time = 100;
            SSshuttle.emergency_escape_time = 100;
        }
        alice.Session.DevCommand("shuttle call");
        RunUntil(w, () => aliceTexts.Any(t => t.Contains("Rebooting World in", StringComparison.Ordinal))
            && bobTexts.Any(t => t.Contains("Rebooting World in", StringComparison.Ordinal)), 120 * Second, "the round ends with the reboot countdown");
        Assert.Equal(SHUTTLE_ENDGAME, In(w, () => Dm.str(SSshuttle.emergency.mode)));
        Assert.Contains(aliceTexts, t => t.Contains("The round has ended.", StringComparison.Ordinal));
        Assert.Contains(bobTexts, t => t.Contains("The round has ended.", StringComparison.Ordinal));
        Assert.Equal(GAME_STATE_FINISHED, In(w, () => Dm.num(SSticker.current_state)));
        Assert.Equal(0, w.Host.Restarts);
        return (alice, bob, bootRuntimes, [w.World], new WeakReference(w.World));
    }
}
