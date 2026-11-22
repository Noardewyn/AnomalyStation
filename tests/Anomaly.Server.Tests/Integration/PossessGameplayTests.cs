using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// D346 (the owner, 2026-11-21): an admin takes over any body from the dev menu and plays it, through the real server. <c>possess #ID</c>
/// (tg's Assume Direct Control) moves the player into an NPC corgi: the session follows (the client's own entity is the corgi), the corgi
/// walks by the player's keys, its AI is off; possessing a cyborg from the corgi gives the corgi its AI back; <c>unpossess</c> takes the
/// player back to the human they left. A mob another player plays needs <c>force</c> (that player becomes a ghost), and the console's
/// <c>givecontrol</c> (tg's Give Direct Control) puts a ghost into a mob and deletes the ghost.
/// </summary>
public class PossessGameplayTests
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static mob MobOf(RealHeadlessWorld w, RealHeadlessWorld.Player p)
        => w.World.Objects.TryGet(w.Host.AvatarOf(p.Name), out object o) ? o as mob : null;

    private static string AiStatus(RealHeadlessWorld w, mob_living m) => In(w, () => (m.ai_controller as datum_ai_controller)?.ai_status as string);

    private static (float X, float Y) Where(RealHeadlessWorld w, atom a) => In(w, () => get_turf(a) is turf t ? (t.x, t.y) : (0f, 0f));

    private static T SpawnBeside<T>(RealHeadlessWorld w, mob near, int dx) where T : mob_living
        => In(w, () =>
        {
            turf at = get_turf(near);
            return Dm.@new<T>(locate(at.x + dx, at.y, at.z));
        });

    private static Anomaly.Client.Shared.Dev.MobInfoModel MobInfo(RealHeadlessWorld w, RealHeadlessWorld.Player p, List<string> texts)
    {
        var answers = new List<string>();
        void Take(ServerText t) { if (t.Type == ServerHost.MobInfoTextType) answers.Add(t.Html); }
        p.Client.ServerTextReceived += Take;
        p.Session.DevCommand("mobinfo me");
        w.Run(5);
        p.Client.ServerTextReceived -= Take;
        return Anomaly.Client.Shared.Dev.MobInfoModel.Parse(System.Net.WebUtility.HtmlDecode(Assert.Single(answers)));
    }

    private static void Hold(RealHeadlessWorld w, RealHeadlessWorld.Player p, double yaw, int ticks)
    {
        p.Yaw = yaw;
        p.Keys = MoveKeys.Forward;
        w.Run(ticks);
        p.Keys = MoveKeys.None;
        w.Run(3);
    }

    [Fact]
    public void An_admin_possesses_a_corgi_walks_it_moves_on_to_a_cyborg_and_returns_to_the_own_body()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 31, TimeoutSteps = 4000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var texts = new List<string>();
        alice.Client.ServerTextReceived += t => texts.Add(t.Html ?? "");
        var human = (mob_living)MobOf(w, alice);
        var corgi = SpawnBeside<mob_living_basic_pet_dog_corgi>(w, human, 2);
        var borg = SpawnBeside<mob_living_silicon_robot>(w, human, -2);
        w.Run(Second);
        Assert.NotEqual(Defines.AI_STATUS_OFF, AiStatus(w, corgi));

        // the dev menu's list: the mobs round Alice from the mobinfo answer
        Anomaly.Client.Shared.Dev.MobInfoModel info = MobInfo(w, alice, texts);
        Anomaly.Client.Shared.Dev.MobInfoNearby seen = Assert.Single(info.Nearby, n => n.Id == corgi.dm_id);
        Assert.Equal(("/mob/living/basic/pet/dog/corgi", "", 2, "possess #" + corgi.dm_id), (seen.TypePath, seen.Player, seen.Distance, seen.PossessCommand));
        Assert.Contains(info.Nearby, n => n.Id == borg.dm_id);
        Assert.Null(info.PossessReturn);

        alice.Session.DevCommand(Inv($"possess #{corgi.dm_id}"));
        w.Run(10);
        Assert.Same(corgi, MobOf(w, alice));
        Assert.Equal("alice", In(w, () => corgi.ckey));
        Assert.Null(In(w, () => human.ckey)); // the body stays, without a player (tg's SSD)
        Assert.NotEqual((float)Defines.DEAD, In(w, () => human.stat));
        Assert.Equal(corgi.dm_id, alice.Session.Replica.OwnEntity);
        Assert.Equal(Defines.AI_STATUS_OFF, AiStatus(w, corgi)); // tg's on_sentience_gained
        Assert.Equal(human.dm_id, w.Host.PossessReturnOf("Alice"));
        Assert.Contains(texts, t => t.Contains("You assume direct control of"));
        Assert.Equal(Dm.str(human.name), MobInfo(w, alice, texts).PossessReturn); // the panel's way back

        // the corgi walks by the player's keys (east: yaw -90 degrees)
        (float X, float Y) before = Where(w, corgi);
        Hold(w, alice, -Math.PI / 2, Second);
        Assert.NotEqual(before, Where(w, corgi));

        // a chain: the corgi gets its AI back, the way back is still the human
        alice.Session.DevCommand(Inv($"possess #{borg.dm_id}"));
        w.Run(10);
        Assert.Same(borg, MobOf(w, alice));
        Assert.Null(In(w, () => corgi.ckey));
        Assert.NotEqual(Defines.AI_STATUS_OFF, AiStatus(w, corgi)); // tg's on_sentience_lost
        Assert.Equal(human.dm_id, w.Host.PossessReturnOf("Alice"));
        Assert.Equal(borg.dm_id, alice.Session.Replica.OwnEntity);

        alice.Session.DevCommand("unpossess");
        w.Run(10);
        Assert.Same(human, MobOf(w, alice));
        Assert.Equal("alice", In(w, () => human.ckey));
        Assert.Null(In(w, () => borg.ckey));
        Assert.Null(w.Host.PossessReturnOf("Alice"));
        Assert.Equal(human.dm_id, alice.Session.Replica.OwnEntity);
        Assert.Contains(texts, t => t.Contains("You return to"));

        alice.Session.DevCommand("unpossess");
        w.Run(5);
        Assert.Contains(texts, t => t.Contains("you possess nothing"));
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void Another_players_mob_needs_force_and_givecontrol_puts_a_ghost_into_a_mob()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 32, TimeoutSteps = 4000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var texts = new List<string>();
        alice.Client.ServerTextReceived += t => texts.Add(t.Html ?? "");
        var aliceBody = (mob_living)MobOf(w, alice);
        var bobBody = (mob_living)MobOf(w, bob);

        alice.Session.DevCommand("possess Bob");
        w.Run(10);
        Assert.Same(bobBody, MobOf(w, bob));
        Assert.Contains(texts, t => t.Contains("is being controlled by Bob") && t.Contains("force"));

        alice.Session.DevCommand("possess Bob force");
        w.Run(10);
        Assert.Same(bobBody, MobOf(w, alice));
        mob bobGhost = MobOf(w, bob);
        Assert.IsType<mob_dead_observer>(bobGhost);
        Assert.False(In(w, () => Dm.truthy(((mob_dead_observer)bobGhost).can_reenter_corpse))); // tg's ghostize(FALSE)
        Assert.Equal(bobGhost.dm_id, bob.Session.Replica.OwnEntity);

        alice.Session.DevCommand("unpossess");
        w.Run(10);
        Assert.Same(aliceBody, MobOf(w, alice));
        Assert.Null(In(w, () => bobBody.ckey));

        // the console gives the ghost a corgi; tg deletes the ghost
        var corgi = SpawnBeside<mob_living_basic_pet_dog_corgi>(w, aliceBody, 2);
        w.Host.EnqueueConsoleLine(Inv($"givecontrol #{corgi.dm_id} Bob"));
        w.Run(10);
        Assert.Same(corgi, MobOf(w, bob));
        Assert.True(In(w, () => QDELETED(bobGhost)));
        Assert.Equal(corgi.dm_id, bob.Session.Replica.OwnEntity);
        Assert.Equal(Defines.AI_STATUS_OFF, AiStatus(w, corgi));
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }

    [Fact]
    public void A_ghost_possesses_a_mob_losing_the_ghost_and_unpossess_takes_it_back_to_its_body()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 33, TimeoutSteps = 4000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var body = (mob_living)MobOf(w, alice);
        mob ghost = In(w, () => body.ghostize(Dm.TRUE) as mob);
        w.Run(5);
        Assert.Same(ghost, MobOf(w, alice));
        var corgi = SpawnBeside<mob_living_basic_pet_dog_corgi>(w, body, 2);

        alice.Session.DevCommand(Inv($"possess #{corgi.dm_id}"));
        w.Run(10);
        Assert.Same(corgi, MobOf(w, alice));
        Assert.True(In(w, () => QDELETED(ghost))); // tg's qdel(adminmob)
        Assert.Equal(body.dm_id, w.Host.PossessReturnOf("Alice")); // the ghost could re-enter its body: that is the way back

        alice.Session.DevCommand("unpossess");
        w.Run(10);
        Assert.Same(body, MobOf(w, alice));
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_possessed_body_that_dies_is_the_one_revive_brings_back(bool ghostFirst)
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 34, TimeoutSteps = 4000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var human = (mob_living)MobOf(w, alice);
        var corgi = SpawnBeside<mob_living_basic_pet_dog_corgi>(w, human, 2);
        alice.Session.DevCommand(Inv($"possess #{corgi.dm_id}"));
        w.Run(10);
        Assert.Same(corgi, MobOf(w, alice));

        alice.Session.DevCommand("kill me");
        w.Run(10);
        Assert.Equal((float)Defines.DEAD, In(w, () => corgi.stat));
        if (ghostFirst)
        {
            mob ghost = In(w, () => corgi.ghostize(Dm.TRUE) as mob);
            w.Run(5);
            Assert.Same(ghost, MobOf(w, alice));
        }
        alice.Session.DevCommand("revive me");
        w.Run(20);
        Assert.NotEqual((float)Defines.DEAD, In(w, () => corgi.stat));
        Assert.Same(corgi, MobOf(w, alice));
        Assert.Null(In(w, () => human.ckey)); // the human the player left stays as it was
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }

    private static string Inv(FormattableString s) => s.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
