using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M6 slice 4 (dismemberment) through the real server on MetaStation with a headless client, in the corridor east of the wall
/// at (42,155): the player's dev console cuts off the left arm of a person who holds a wrench (the dev command dismember, tg's
/// dismember()): the client hears tg's dismember sound, the arm is gone from the person's composed picture, and the severed
/// arm and the dropped wrench lie on the floor in the client's world; then the dev command wound tears the right leg open and
/// dismember takes both legs off, and the person lies on the floor.
/// </summary>
public class DismembermentGameplayTests
{
    private const int Second = 20;
    private const int X = 43, Y = 155;

    private readonly ITestOutputHelper _output;

    public DismembermentGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static turf At(int x, int y) => Builtins.locate(x, y, 1);

    private static List<(string Icon, string State, float Layer)> Looks(ReplicaWorld replica, long id)
        => replica.TryGet(id, out ReplicaEntity e) ? CrewOutfitTests.Icons(replica, e.State.Appearance) : new();

    private static obj_item_bodypart Part(mob_living_carbon_human h, string zone) => (obj_item_bodypart)h.get_bodypart(zone);

    [MetaServerFact]
    public void The_dev_console_cuts_off_an_arm_the_client_sees_it_on_the_floor_and_lost_legs_floor_the_person()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 65, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        var texts = new List<string>();
        p.Client.ServerTextReceived += t => texts.Add(t.Html);
        int bootRuntimes = w.World.RuntimeErrorCount;
        Assert.All(Enumerable.Range(X, 4), x => Assert.True(In(w, () => At(x, Y) is turf_open && !Dm.truthy(At(x, Y).density)), $"({x},{Y}) is open floor"));

        mob_living_carbon_human bob = In(w, () =>
        {
            Waiting.NoSleep(me.forceMove(At(X, Y)));
            return Dm.@new<mob_living_carbon_human>(At(X + 2, Y));
        });
        obj_item_wrench wrench = In(w, () =>
        {
            var made = Dm.@new<obj_item_wrench>(bob.loc);
            Waiting.NoSleep(bob.put_in_l_hand(made));
            return made;
        });
        w.Run(Second);
        obj_item_bodypart arm = In(w, () => Part(bob, BODY_ZONE_L_ARM));
        List<string> before = Looks(replica, bob.dm_id).Select(l => l.State).Where(s => s != null).ToList();
        _output.WriteLine("Bob before: " + string.Join(", ", before));
        Assert.Contains(before, s => s.Contains("l_arm", StringComparison.Ordinal));

        // The dev console: tg's dismember() of Bob's left arm.
        p.Session.DevCommand($"dismember #{bob.dm_id} l_arm");
        w.Run(2 * Second);
        Assert.Null(In(w, () => arm.owner));
        Assert.Contains(texts, t => t.Contains("l_arm dismembered", StringComparison.Ordinal));
        Assert.Contains(heard, h => h.File == "sound/effects/dismember.ogg");
        List<string> after = Looks(replica, bob.dm_id).Select(l => l.State).Where(s => s != null).ToList();
        _output.WriteLine("Bob after: " + string.Join(", ", after));
        Assert.DoesNotContain(after, s => s.Contains("l_arm", StringComparison.Ordinal) && !s.Contains("stump", StringComparison.Ordinal));
        Assert.True(replica.TryGet(arm.dm_id, out ReplicaEntity severed), "the client sees the severed arm");
        _output.WriteLine($"the severed arm at ({severed.State.X},{severed.State.Y}): " + string.Join(", ", Looks(replica, arm.dm_id).Select(l => $"{l.Icon}:{l.State}")));
        Assert.Contains(Looks(replica, arm.dm_id), l => l.State != null && l.State.Contains("l_arm", StringComparison.Ordinal)); // tg's dropped limb sprite
        Assert.True(In(w, () => wrench.loc is turf), "the lost hand dropped the wrench");
        Assert.True(replica.TryGet(wrench.dm_id, out _), "the client sees the dropped wrench");

        // A torn-open leg, then both legs off: Bob lies on the floor.
        p.Session.DevCommand($"wound #{bob.dm_id} r_leg slash critical");
        w.Run(Second);
        Assert.Contains(texts, t => t.Contains("r_leg has", StringComparison.Ordinal));
        p.Session.DevCommand($"dismember #{bob.dm_id} l_leg");
        p.Session.DevCommand($"dismember #{bob.dm_id} r_leg");
        w.Run(2 * Second);
        Assert.Equal((0f, LYING_DOWN), In(w, () => (bob.num_legs, bob.body_position)));
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
