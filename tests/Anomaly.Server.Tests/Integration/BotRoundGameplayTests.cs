using Anomaly.Core.DmShim;
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
/// M10 slice J through the real server on MetaStation with tg's lobby on: two bots join into the pregame and ready up; the dev command
/// startnow (sent by a bot) starts the round and both play as station engineers; a third bot joins after the start and late-joins through
/// tg's job menu. The emergency shuttle (tg's timers shortened) ends the round, the host restarts, and every bot rejoins with its token into
/// the next round's lobby and readies again.
/// </summary>
public class BotRoundGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

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

    private static Bot Add(RealHeadlessWorld w, string name)
    {
        var bot = new Bot(() => w.Network.CreateClient(), new BotOptions
        {
            Name = name, Seed = (ulong)name.GetHashCode() & 0xFFFF, Behaviour = BotBehaviour.Wander, JoinRetryUpdates = 120 * Second,
        });
        w.Bots.Add(bot);
        bot.Connect("127.0.0.1", w.Server.Port);
        RunUntil(w, () => bot.State == BotState.Joined, 10 * Second, name + " joins");
        return bot;
    }

    private static object Mob(RealHeadlessWorld w, Bot bot) => w.World.Objects.TryGet(w.Host.AvatarOf(bot.Options.Name), out object o) ? o : null;

    private static bool Ready(Bot bot) => bot.Replica.UiWindows.TryGetValue(Limits.LobbyUiWindow, out UiWindowState lobby)
        && lobby.Merged.Get("isReady")?.Truthy == true;

    private static void WaitForPregame(RealHeadlessWorld w)
        // the rebooted world makes its ticker during its first ticks: until then there is none
        => RunUntil(w, () => In(w, () => SSticker == null ? 0 : Dm.num(SSticker.current_state)) >= GameStatePregame, 30 * Second, "tg's ticker enters the pregame");

    [MetaServerFact]
    public void Bots_ready_up_late_join_and_ride_the_round_restart_into_the_next_lobby()
    {
        ServerConfig meta = MapJoinTests.MapConfig("metastation", MetaServer.Folder());
        var config = new ServerConfig
        {
            Seed = meta.Seed, Console = false, StatusEveryTicks = 100000, Map = meta.Map, MapsFolder = meta.MapsFolder, MoveSpeedScale = 1f,
            Lobby = true,
        };
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 61, TimeoutSteps = 60000 }, config, strict: false);
        (Bot a, Bot b, Bot c, WeakReference oldWorld) = FirstRound(w);

        // the round end and the restart: every bot rejoins into the next lobby and readies again
        RunUntil(w, () => w.Host.Restarts == 1, 240 * Second, "the round ends and the host restarts");
        WaitForPregame(w);
        int newBootRuntimes = w.World.RuntimeErrorCount;
        RunUntil(w, () => new[] { a, b, c }.All(x => x.State == BotState.Joined && Mob(w, x) is mob_dead_new_player && Ready(x)), 60 * Second,
            "every bot rejoins the next lobby and readies");
        foreach (Bot bot in new[] { a, b, c })
        {
            Assert.Equal(1, bot.Stats.RoundRestarts);
            Assert.Equal(2, bot.Stats.Joins);
            Assert.Equal(0, bot.Stats.Desyncs);
        }
        w.Run(Second);
        output.WriteLine(w.Err.ToString());
        Assert.Equal(newBootRuntimes, w.World.RuntimeErrorCount);
        Assert.DoesNotContain("clear_important_client_contents", w.Err.ToString()); // the old world's clients are not deleted in the new one
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        Assert.False(oldWorld.IsAlive, "the old world is still held after the restart");
    }

    // The first round, in its own frame (no reference of it outlives the call): two bots ready up, the round starts, a third late-joins,
    // the shuttle is called with tg's timers shortened.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private (Bot, Bot, Bot, WeakReference) FirstRound(RealHeadlessWorld w)
    {
        WaitForPregame(w);
        int bootRuntimes = w.World.RuntimeErrorCount;

        // the pregame: both bots ready up and stay new players
        Bot a = Add(w, "BotA"), b = Add(w, "BotB");
        RunUntil(w, () => Ready(a) && Ready(b), 5 * Second, "both bots ready up");
        Assert.IsType<mob_dead_new_player>(Mob(w, a));

        // the round start: both play as engineers and walk
        Assert.True(a.SendDevCommand("startnow"));
        RunUntil(w, () => Mob(w, a) is mob_living_carbon_human && Mob(w, b) is mob_living_carbon_human, 10 * Second, "the round starts with both");
        Assert.Equal("Station Engineer", In(w, () => ((mob_living_carbon_human)Mob(w, b)).mind.assigned_role.title));
        long moves = a.Stats.OwnMoves;
        w.Run(3 * Second);
        Assert.True(a.Stats.OwnMoves > moves, "the bot walks once it plays");

        // a late joiner: Join, tg's job menu, the engineer
        Bot c = Add(w, "BotC");
        RunUntil(w, () => Mob(w, c) is mob_living_carbon_human, 10 * Second, "the third bot late-joins");
        Assert.Equal("Station Engineer", In(w, () => ((mob_living_carbon_human)Mob(w, c)).mind.assigned_role.title));
        output.WriteLine($"lobby presses: {a.Stats.LobbyActs}, {b.Stats.LobbyActs}, {c.Stats.LobbyActs}");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount); // the localhost admins' auto deadmin at login among them

        using (w.World.Enter())
        {
            SSshuttle.emergency_call_time = 100;
            SSshuttle.emergency_dock_time = 100;
            SSshuttle.emergency_escape_time = 100;
        }
        Assert.True(a.SendDevCommand("shuttle call"));
        return (a, b, c, new WeakReference(w.World));
    }

    private const float GameStatePregame = GAME_STATE_PREGAME;
}
