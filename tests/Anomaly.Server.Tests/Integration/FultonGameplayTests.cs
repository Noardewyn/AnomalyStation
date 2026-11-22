using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Syndicate X1b (D316) through the real server on MetaStation: in the holodeck, a contractor's fulton pack, linked to a recovery beacon six
/// tiles away, is used on the other player with a click. The clients see him replaced by the extraction holder, his picture under the
/// balloon (tg's fulton_expand, then fulton_balloon), animated up and away; it comes down next to the beacon, and he stands there again.
/// </summary>
public class FultonGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    [MetaServerFact]
    public void A_fulton_pack_lifts_a_player_on_a_balloon_and_sets_him_down_at_its_beacon()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 43, TimeoutSteps = 20000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder()), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        ReplicaWorld seen = alice.Session.Replica;
        var heard = new List<SoundEvent>();
        seen.SoundsReceived += events => heard.AddRange(events);
        w.Run(Second);

        // the holodeck (lit, empty): Alice at 153,173 with the pack, Bob in front of her, the beacon at 155,179
        var beacon = In(w, () => Dm.@new<obj_structure_extraction_point_syndicate>(locate(155, 179, 1)));
        var pack = In(w, () =>
        {
            Waiting.NoSleep(aliceMob.forceMove(locate(153, 173, 1)));
            Waiting.NoSleep(bobMob.forceMove(locate(153, 174, 1)));
            var made = Dm.@new<obj_item_extraction_pack_syndicate>(aliceMob.loc);
            Waiting.NoSleep(aliceMob.put_in_active_hand(made));
            made.beacon_ref = WEAKREF(beacon) as datum_weakref; // linked (its list window is the Core test's)
            return made;
        });
        w.Run(Second);
        Assert.Contains(seen.Entities, e => e.Id == bobMob.dm_id);

        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(6 * Second); // five seconds of attaching, the balloon after 0.4 s more
        var holder = In(w, () => bobMob.loc as obj_effect_extraction_holder);
        Assert.NotNull(holder);
        Assert.False(seen.Entities.Any(e => e.Id == bobMob.dm_id), "Bob himself is inside the holder");
        Assert.True(seen.TryGet(holder.dm_id, out ReplicaEntity held), "the clients have no holder");
        List<string> states = CryoGameplayTests.States(seen, held.State.Appearance);
        output.WriteLine("the holder: " + string.Join(", ", states));
        Assert.Contains("fulton_balloon", states);
        Assert.Contains(states, s => s != null && s.Contains("chest", StringComparison.Ordinal)); // his picture
        Assert.NotNull(held.Animation); // the bob up and down
        Assert.Contains(heard, h => h.File == "sound/items/fulton/fultext_deploy.ogg");

        // the launch, the descent next to the beacon, the landing
        w.Run(17 * Second);
        Assert.Contains(heard, h => h.File == "sound/items/fulton/fultext_launch.ogg");
        Assert.True(In(w, () => QDELETED(holder)));
        Assert.True(seen.Entities.Any(e => e.Id == bobMob.dm_id), "Bob is an entity again");
        Assert.True(In(w, () => get_dist(bobMob, beacon)) <= 1);
        Assert.Equal(2f, In(w, () => pack.uses_left));
    }
}
