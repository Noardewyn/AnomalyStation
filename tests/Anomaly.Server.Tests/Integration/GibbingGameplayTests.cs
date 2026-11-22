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
/// M6 slice 5 (gibbing) through the real server on MetaStation with a headless client, in the corridor east of the wall at
/// (42,155): the player's dev console gibs a person holding a wrench (the dev command gib, tg's admin Gib with the brain): the
/// client hears tg's gib sound, sees tg's gibbing animation and the gibs on the floor, the person is gone from its world, and the
/// head (with the brain inside), the other limbs and the wrench lie about; a second person gibbed without the brain leaves no
/// head; the player gibbing themselves becomes a ghost, still playing, their mind kept by the brain mob.
/// </summary>
public class GibbingGameplayTests
{
    private const int Second = 20;
    private const int X = 43, Y = 155;

    private readonly ITestOutputHelper _output;

    public GibbingGameplayTests(ITestOutputHelper output) => _output = output;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static turf At(int x, int y) => Builtins.locate(x, y, 1);

    private static List<(string Icon, string State, float Layer)> Looks(ReplicaWorld replica, long id)
        => replica.TryGet(id, out ReplicaEntity e) ? CrewOutfitTests.Icons(replica, e.State.Appearance) : new();

    [MetaServerFact]
    public void The_dev_console_gibs_a_person_the_client_sees_the_gibs_and_the_remains_fly_apart()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 66, TimeoutSteps = 8000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player p = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(p);
        ReplicaWorld replica = p.Session.Replica;
        var heard = new List<SoundEvent>();
        replica.SoundsReceived += events => heard.AddRange(events);
        var seen = new HashSet<string>();
        var texts = new List<string>();
        p.Client.ServerTextReceived += t => texts.Add(t.Html);
        int bootRuntimes = w.World.RuntimeErrorCount;
        Assert.All(Enumerable.Range(X, 4), x => Assert.True(In(w, () => At(x, Y) is turf_open && !Dm.truthy(At(x, Y).density)), $"({x},{Y}) is open floor"));

        mob_living_carbon_human bob = In(w, () =>
        {
            Waiting.NoSleep(me.forceMove(At(X, Y)));
            return Dm.@new<mob_living_carbon_human>(At(X + 3, Y));
        });
        obj_item_wrench wrench = In(w, () =>
        {
            var made = Dm.@new<obj_item_wrench>(bob.loc);
            Waiting.NoSleep(bob.put_in_l_hand(made));
            return made;
        });
        (obj_item_bodypart_head head, obj_item_organ_brain brain) = In(w, () =>
            ((obj_item_bodypart_head)bob.get_bodypart(BODY_ZONE_HEAD), (obj_item_organ_brain)bob.get_organ_slot(ORGAN_SLOT_BRAIN)));
        w.Run(Second);
        Assert.True(replica.TryGet(bob.dm_id, out _), "the client sees Bob");

        // The dev console: tg's admin Gib, the brain dropped.
        p.Session.DevCommand($"gib #{bob.dm_id} brain");
        for (int i = 0; i < Second; i++)
        {
            w.Run(1);
            foreach (ReplicaEntity e in replica.Entities)
                foreach ((string icon, string state, float _) in CrewOutfitTests.Icons(replica, e.State.Appearance))
                    if (state != null) seen.Add(icon + ":" + state);
        }
        w.Run(Second);
        _output.WriteLine("seen: " + string.Join(", ", seen.Where(s => s.Contains("gib", StringComparison.Ordinal))));
        Assert.Contains(texts, t => t.Contains("gibbed", StringComparison.Ordinal));
        Assert.True(In(w, () => QDELETED(bob)), "Bob is gibbed");
        Assert.False(replica.TryGet(bob.dm_id, out _), "Bob is gone from the client's world");
        Assert.Contains(heard, h => h.File == "sound/effects/blob/blobattack.ogg"); // the human gib spawner's sound
        Assert.Contains(seen, s => s == "icons/mob/simple/mob.dmi:gibbed-h"); // tg's gibbing animation (the human's gib_anim)
        Assert.Contains(seen, s => s.StartsWith("icons/effects/blood.dmi:gib", StringComparison.Ordinal)); // the gibs on the floor

        // The remains: the head keeps the brain, the limbs and the wrench lie about, seen by the client.
        Assert.False(In(w, () => QDELETED(head)));
        Assert.True(In(w, () => brain.loc == head), "the brain stays in the head");
        Assert.True(In(w, () => head.loc is turf), "the head lies on the floor");
        Assert.True(replica.TryGet(head.dm_id, out _), "the client sees the head");
        Assert.True(In(w, () => wrench.loc is turf), "the wrench is dropped");
        int limbs = In(w, () => Dm.each(range(5, At(X + 3, Y))).Count(a => a is obj_item_bodypart));
        Assert.Equal(5, limbs); // the arms, the legs and the head; the chest goes with the body

        // Without the brain: no head is left.
        mob_living_carbon_human carol = In(w, () => Dm.@new<mob_living_carbon_human>(At(X + 1, Y)));
        obj_item_bodypart carols_head = In(w, () => (obj_item_bodypart)carol.get_bodypart(BODY_ZONE_HEAD));
        w.Run(Second);
        p.Session.DevCommand($"gib #{carol.dm_id}");
        w.Run(2 * Second);
        Assert.True(In(w, () => QDELETED(carol) && QDELETED(carols_head)), "gibbed without the brain: the head goes with the body");

        // The player gibs themselves with the brain: they become a ghost and stay in the game; their mind is in the brain mob.
        obj_item_organ_brain own_brain = In(w, () => (obj_item_organ_brain)me.get_organ_slot(ORGAN_SLOT_BRAIN));
        p.Session.DevCommand("gib me brain");
        w.Run(2 * Second);
        Assert.True(In(w, () => QDELETED(me)), "Alice is gibbed");
        datum_mind mind = In(w, () => own_brain.brainmob?.mind);
        mob_dead_observer ghost = In(w, () => Dm.each(Dm.world).OfType<mob_dead_observer>().FirstOrDefault(g => g.mind == mind && mind != null));
        Assert.NotNull(ghost);
        Assert.Equal(FeedPhase.Playing, p.Client.Phase);
        Assert.Equal(ghost.dm_id, replica.OwnEntity);
        Assert.True(In(w, () => ghost.client != null), "Alice plays the ghost");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
