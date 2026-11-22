using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Getting out of a closed closet on MetaStation through the real server, as tg's closets.dm does: a player in an unlocked, unwelded locker walks out
/// (relaymove opens it); one in a welded locker, or a locked secure one, resists with the HUD's resist button, a watcher hears it shake violently and sees
/// it shake, and after breakout_time (two minutes) the player busts out (container_resist_act, bust_open).
/// </summary>
public class ClosetBreakoutGameplayTests
{
    private const int Second = 20; // ticks per second

    private readonly ITestOutputHelper _output;

    public ClosetBreakoutGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private sealed class Rig : IDisposable
    {
        public RealHeadlessWorld W;
        public RealHeadlessWorld.Player Alice, Bob;
        public mob_living_carbon_human AliceMob, BobMob;
        public readonly List<string> AliceTexts = new(), BobTexts = new();

        public Rig()
        {
            var config = new ServerConfig { Seed = 7, Console = false, StatusEveryTicks = 100000, Map = "metastation", MapsFolder = MetaServer.Folder(), DevCommands = true };
            W = new RealHeadlessWorld(new LoopbackOptions { Seed = 9, TimeoutSteps = 8000 }, config, strict: false);
            Alice = W.Join("Alice");
            Bob = W.Join("Bob");
            AliceMob = (mob_living_carbon_human)W.MobOf(Alice);
            BobMob = (mob_living_carbon_human)W.MobOf(Bob);
            Alice.Client.ServerTextReceived += t => AliceTexts.Add(t.Html);
            Bob.Client.ServerTextReceived += t => BobTexts.Add(t.Html);
            W.Run(Second);
        }

        // A closet of the type on Alice's tile, Alice inside it and Bob beside it.
        public T Shut<T>() where T : obj_structure_closet
        {
            T closet = In(W, () =>
            {
                T c = Dm.@new<T>(AliceMob.loc);
                Waiting.NoSleep(AliceMob.forceMove(c));
                Waiting.NoSleep(BobMob.forceMove(get_step(c, EAST)));
                return c;
            });
            W.Run(Second);
            return closet;
        }

        public void Resist()
        {
            long resist = In(W, () => ((datum)AliceMob.hud_used.screen_objects[HUD_MOB_RESIST]).dm_id);
            Assert.True(Alice.Session.Click(new ClickTarget(TargetKind.Screen, resist, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        }

        public void Dispose() => W.Dispose();
    }

    private static bool Heard(List<string> texts, string part) => texts.Any(t => t.Contains(part, StringComparison.Ordinal));

    [MetaServerFact]
    public void A_player_in_an_unlocked_locker_walks_out()
    {
        using var r = new Rig();
        var closet = r.Shut<obj_structure_closet>();
        Assert.Same(closet, In(r.W, () => r.AliceMob.loc));
        Assert.False(In(r.W, () => Dm.truthy(closet.opened)));

        r.Alice.Yaw = 0;
        r.Alice.Keys = MoveKeys.Forward;
        r.W.Run(Second);
        r.Alice.Keys = MoveKeys.None;
        r.W.Run(Second);

        Assert.True(In(r.W, () => Dm.truthy(closet.opened)), "the closet opened (relaymove)");
        Assert.IsAssignableFrom<turf>(In(r.W, () => r.AliceMob.loc));
    }

    [MetaServerFact]
    public void A_player_in_a_crate_walks_out()
    {
        using var r = new Rig();
        var crate = r.Shut<obj_structure_closet_crate>();
        Assert.Same(crate, In(r.W, () => r.AliceMob.loc));
        r.Alice.Yaw = 0;
        r.Alice.Keys = MoveKeys.Forward;
        r.W.Run(Second);
        r.Alice.Keys = MoveKeys.None;
        r.W.Run(Second);
        Assert.True(In(r.W, () => Dm.truthy(crate.opened)), "the crate opened (relaymove)");
        Assert.IsAssignableFrom<turf>(In(r.W, () => r.AliceMob.loc));
    }

    [MetaServerFact]
    public void A_player_in_a_welded_locker_breaks_out_after_two_minutes_and_a_watcher_sees_it_shake()
    {
        using var r = new Rig();
        var closet = r.Shut<obj_structure_closet>();
        In(r.W, () => { closet.welded = TRUE; closet.update_appearance(); return 0; });
        r.W.Run(Second);

        r.Alice.Yaw = 0;
        r.Alice.Keys = MoveKeys.Forward;
        r.W.Run(Second);
        r.Alice.Keys = MoveKeys.None;
        Assert.Same(closet, In(r.W, () => r.AliceMob.loc)); // welded: walking does not get him out, he resists instead

        BreakOut(r, closet);
        Assert.False(In(r.W, () => Dm.truthy(closet.welded)));
        Assert.True(In(r.W, () => Dm.truthy(closet.broken)));
    }

    [MetaServerFact]
    public void A_player_in_a_locked_secure_closet_breaks_out_after_two_minutes()
    {
        using var r = new Rig();
        var closet = r.Shut<obj_structure_closet_secure_closet>();
        Assert.True(In(r.W, () => Dm.truthy(closet.locked)));

        r.Alice.Yaw = 0;
        r.Alice.Keys = MoveKeys.Forward;
        r.W.Run(Second);
        r.Alice.Keys = MoveKeys.None;
        r.W.Run(Second);
        Assert.True(Heard(r.AliceTexts, "door won't budge!"), "the locked door won't budge (relaymove)");
        Assert.Same(closet, In(r.W, () => r.AliceMob.loc));

        BreakOut(r, closet);
        Assert.False(In(r.W, () => Dm.truthy(closet.locked)));
    }

    private void BreakOut(Rig r, obj_structure_closet closet)
    {
        float breakout = In(r.W, () => closet.breakout_time);
        Assert.Equal(1200f, breakout); // tg's two minutes
        r.Resist();
        bool shook = false;
        long closetId = closet.dm_id;
        r.W.Run(5 * Second, _ => shook |= r.Bob.Session.Replica.Entities.Any(e => e.Id == closetId && e.Animation != null && e.Animation.Tracks.Length > 0));
        // Alice is inside the closet, so visible_message gives the watchers only its blind message (mob.dm:299)
        Assert.True(Heard(r.BobTexts, "You hear banging from"), "Bob hears the banging");
        Assert.False(Heard(r.BobTexts, "begins to shake violently!"));
        Assert.True(Heard(r.AliceTexts, "start pushing the door open"), "Alice's own line");
        Assert.True(shook, "Bob's client got the closet's shake animation");
        Assert.Same(closet, In(r.W, () => r.AliceMob.loc));

        r.W.Run(100 * Second);
        Assert.Same(closet, In(r.W, () => r.AliceMob.loc)); // 105 s: not yet
        Assert.False(In(r.W, () => Dm.truthy(closet.opened)));
        r.W.Run(20 * Second); // 125 s
        _output.WriteLine("Alice: " + string.Join(" | ", r.AliceTexts.Where(t => t.Contains("break", StringComparison.Ordinal) || t.Contains("lean", StringComparison.Ordinal))));
        Assert.True(In(r.W, () => Dm.truthy(closet.opened)), "the closet burst open");
        Assert.IsAssignableFrom<turf>(In(r.W, () => r.AliceMob.loc));
        Assert.True(Heard(r.AliceTexts, "You successfully break out of"));
    }
}
