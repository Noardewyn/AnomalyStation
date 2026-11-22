using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M8 slice G (D187) through the real server with headless clients. A supermatter at high power pulses Alice, who stands in view of it
/// (tg's psychological pulse): she hallucinates and Bob, out of its view, does not. A hallucinated body reaches Alice's replica as a private
/// image on a tile and Bob's replica holds none (D40). A delusion puts an override image on Bob in Alice's replica (protocol 35: the client
/// draws it in place of Bob) and Carol, who has no delusion, still sees Bob as he is.
/// </summary>
public class HallucinationGameplayTests(ITestOutputHelper output)
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

    private static List<string> States(ReplicaWorld replica, Func<ImageEntry, bool> which)
    {
        var states = new List<string>();
        foreach (ImageEntry image in replica.Images.Values)
        {
            if (which(image) && replica.TryGetAppearance(image.Appearance, out AppearanceEntry entry))
                states.Add(replica.ResolveString(entry.IconState));
        }
        return states;
    }

    [Fact]
    public void A_supermatter_pulses_the_one_who_watches_it_and_the_hallucinations_reach_her_client_alone()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 93, TimeoutSteps = 30000 });
        var runtimes = new List<string>();
        w.World.RuntimeErrorSink = e => runtimes.Add(e.ToString());
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        w.Run(10);
        int bootRuntimes = w.World.RuntimeErrorCount;
        In(w, () =>
        {
            Waiting.NoSleep(aliceMob.forceMove(locate(12, 12, 1)));
            Waiting.NoSleep(bobMob.forceMove(locate(28, 12, 1)));
            return 0;
        });
        w.Run(Second);

        // the crystal's pulse: Alice, two tiles from it, hallucinates for a while; Bob, far away, not at all
        Do(w, () =>
        {
            var sm = Dm.@new<obj_machinery_power_supermatter_crystal>(locate(14, 12, 1));
            sm.internal_energy = 3000;
            Waiting.NoSleep(sm.psychological_examination());
            qdel(sm); // its own cycle would log the energy this test set by hand
        });
        Assert.True(In(w, () => Dm.truthy(aliceMob.has_status_effect(typeof(datum_status_effect_hallucination)))));
        Assert.False(In(w, () => Dm.truthy(bobMob.has_status_effect(typeof(datum_status_effect_hallucination)))));

        // a hallucinated body: a private image on a tile in Alice's replica, none in Bob's (the session's own block, D40)
        Do(w, () => Waiting.NoSleep(aliceMob.cause_hallucination(typeof(datum_hallucination_body_husk), "test")));
        w.Run(Second / 2);
        List<string> aliceBodies = States(alice.Session.Replica, i => i.TurfX != 0);
        output.WriteLine("Alice's tile images: " + string.Join(", ", aliceBodies));
        Assert.Contains("husk", aliceBodies);
        Assert.DoesNotContain("husk", States(bob.Session.Replica, i => i.TurfX != 0));
        Assert.True(bootRuntimes == w.World.RuntimeErrorCount, string.Join("\n", runtimes));
    }

    [Fact]
    public void A_delusion_shows_the_others_as_an_override_picture_to_the_hallucinator_alone()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 94, TimeoutSteps = 30000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        RealHeadlessWorld.Player carol = w.Join("Carol");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var carolMob = (mob_living_carbon_human)w.MobOf(carol);
        w.Run(10);
        Do(w, () =>
        {
            Waiting.NoSleep(aliceMob.forceMove(locate(12, 12, 1)));
            Waiting.NoSleep(bobMob.forceMove(locate(13, 12, 1)));
            Waiting.NoSleep(carolMob.forceMove(locate(13, 13, 1)));
        });
        w.Run(Second);
        long bobId = bobMob.dm_id;

        Do(w, () => Waiting.NoSleep(aliceMob.cause_hallucination(typeof(datum_hallucination_delusion_preset_monkey), "test",
            Dm.named("duration", 20 * SECONDS), Dm.named("affects_others", TRUE), Dm.named("affects_us", FALSE))));
        w.Run(Second);

        // Alice's replica holds an override image on Bob with the monkey's picture; Carol's holds nothing on Bob (she still sees him as he is)
        List<ImageEntry> onBob = alice.Session.Replica.Images.Values.Where(i => i.AttachedTo == bobId).ToList();
        Assert.Contains(onBob, i => i.Override && alice.Session.Replica.TryGetAppearance(i.Appearance, out AppearanceEntry e) && alice.Session.Replica.ResolveString(e.IconState) == "monkey");
        Assert.DoesNotContain(carol.Session.Replica.Images.Values, i => i.AttachedTo == bobId);
        Assert.DoesNotContain(bob.Session.Replica.Images.Values, i => i.Override);

        // after its time the delusion is gone
        w.Run(25 * Second);
        Assert.DoesNotContain(alice.Session.Replica.Images.Values, i => i.Override);
    }
}
