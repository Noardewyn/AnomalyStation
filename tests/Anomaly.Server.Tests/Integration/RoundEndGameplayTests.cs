using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M10 slice H through the real server on MetaStation: the emergency shuttle (called with the dev command, tg's timers shortened)
/// takes Bob to CentCom while Alice stays aboard the station; at SHUTTLE_ENDGAME tg's ticker ends the round (declare_completion):
/// each player gets tg's round-end report in chat (DV192: Bob survived, Alice was marooned), the observers become visible, and after
/// tg's countdown world/Reboot asks the host for a new round. The host builds a fresh world in-process while the connections wait
/// between rounds (D306, protocol 62); Alice's client joins the new world on the same connection once it opens its joins, into a
/// new round: a new body, the emergency shuttle back at CentCom, the world's tick near its start.
/// </summary>
public class RoundEndGameplayTests(ITestOutputHelper output)
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

    [MetaServerFact]
    public void The_round_ends_at_centcom_with_the_report_and_the_server_restarts_into_a_new_round_that_Alice_rejoins()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 37, TimeoutSteps = 60000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceTexts = new List<string>();
        var bobTexts = new List<string>();
        alice.Client.ServerTextReceived += t => aliceTexts.Add(t.Html);
        bob.Client.ServerTextReceived += t => bobTexts.Add(t.Html);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        using (w.World.Enter())
        {
            SSshuttle.emergency_call_time = 100;
            SSshuttle.emergency_dock_time = 400;
            SSshuttle.emergency_escape_time = 100;
        }
        alice.Session.DevCommand("shuttle call");
        RunUntil(w, () => In(w, () => Dm.eq(SSshuttle.emergency.mode, SHUTTLE_DOCKED)), 30 * Second, "the shuttle docks");
        using (w.World.Enter())
        {
            var seat = Dm.each<obj_structure_chair>(Dm.world).First(c => get_area(c) is area_shuttle_escape && c.z == 1);
            Waiting.NoSleep(bobMob.forceMove(seat.loc));
        }
        RunUntil(w, () => In(w, () => Dm.eq(SSshuttle.emergency.mode, SHUTTLE_ENDGAME)), 60 * Second, "the shuttle reaches CentCom");

        // tg's ticker ends the round: the report, the line to everyone, the countdown
        RunUntil(w, () => aliceTexts.Any(t => t.Contains("Rebooting World in", StringComparison.Ordinal)), 30 * Second, "the reboot countdown is announced");
        output.WriteLine("Bob's report: " + bobTexts.FirstOrDefault(t => t.Contains("boxed_message", StringComparison.Ordinal)));
        output.WriteLine("Alice's report: " + aliceTexts.FirstOrDefault(t => t.Contains("boxed_message", StringComparison.Ordinal)));
        Assert.Contains(aliceTexts, t => t.Contains("The round has ended.", StringComparison.Ordinal));
        Assert.Contains(bobTexts, t => t.Contains("You managed to survive the events on", StringComparison.Ordinal));
        Assert.Contains(aliceTexts, t => t.Contains("marooned", StringComparison.Ordinal));
        Assert.Contains(aliceTexts, t => t.Contains("Station Integrity", StringComparison.Ordinal) && t.Contains("Survival Rate", StringComparison.Ordinal));
        Assert.Equal(GameStateFinished, In(w, () => Dm.num(SSticker.current_state)));

        // the reboot (D306): the host's new round, the connections kept between rounds
        World first = w.World;
        RunUntil(w, () => w.Host.Restarts == 1, 60 * Second, "world/Reboot replaced the world");
        w.Run(1);
        Assert.NotSame(first, w.World);
        Assert.Equal(FeedPhase.BetweenRounds, alice.Client.Phase);

        // Alice's client joins the new round on the same connection once the new world opens its joins
        RunUntil(w, () => alice.Client.Phase == FeedPhase.Playing, 120 * Second, "Alice joins the new round");
        bob.Gone = true;
        var newBody = w.MobOf(alice); // the game scene started again on the same feed, with a new play session
        Assert.NotNull(newBody);
        Assert.Equal(SHUTTLE_IDLE, In(w, () => Dm.str(SSshuttle.emergency.mode)));
        Assert.True(In(w, () => Dm.truthy(SSticker.IsRoundInProgress())), "the new round is in progress");
        Assert.True(w.World.Tick < 120 * Second, "the new world started afresh");
        output.WriteLine(w.Err.ToString());
    }

    private const float GameStateFinished = 4;
}
