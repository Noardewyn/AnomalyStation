using Anomaly.Client.Shared;
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
/// Gaps package VI1 (D341) through the real server with headless clients: Bob catches the flu, which SSdisease processes with his
/// Life; Alice scans him with the health analyzer and her readout names the flu, its stage and its cure; Bob drinks spaceacillin
/// and the flu is cured within a minute or two of his Life ticks, leaving him resistant to it.
/// </summary>
public class DiseasesGameplayTests(ITestOutputHelper output)
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

    [Fact]
    public void A_player_catches_the_flu_the_analyzer_names_it_and_spaceacillin_cures_it()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 10350, TimeoutSteps = 20000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var aliceTexts = new List<string>();
        alice.Client.ServerTextReceived += t => aliceTexts.Add(t.Html);
        w.Run(10);
        int bootRuntimes = w.World.RuntimeErrorCount;

        datum_disease_flu flu = In(w, () =>
        {
            Assert.NotNull(SSdisease); // tg's MC makes it
            Waiting.NoSleep(aliceMob.forceMove(locate(12, 12, 1)));
            Waiting.NoSleep(bobMob.forceMove(locate(13, 12, 1)));
            var d = Dm.@new<datum_disease_flu>();
            Assert.True(Dm.truthy(Waiting.NoSleep(bobMob.ForceContractDisease(d, FALSE))));
            Assert.True(Dm.@in(d, SSdisease.active_diseases));
            return d;
        });
        w.Run(Second);

        // the scan: Alice's readout holds the flu's lines
        Do(w, () =>
        {
            Waiting.NoSleep(aliceMob.dropItemToGround(aliceMob.get_active_held_item() as obj_item));
            Assert.True(Dm.truthy(Waiting.NoSleep(aliceMob.put_in_active_hand(Dm.@new<obj_item_healthanalyzer>(aliceMob.loc)))));
        });
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Entity, bobMob.dm_id, 0, 0, 0, 16, 18, AimZone.Chest), ClickButton.Left, ClickModifiers.None));
        w.Run(Second);
        string scan = aliceTexts.LastOrDefault(t => t.Contains("boxed_message blue_box", StringComparison.Ordinal));
        Assert.NotNull(scan);
        output.WriteLine(scan);
        Assert.Contains("Virus detected", scan);
        Assert.Contains("Name: The Flu.", scan);
        Assert.Contains("Cure: Spaceacillin", scan);

        // spaceacillin in Bob's blood: the flu's cure chance on each of his Life ticks (SSmobs)
        object id = In(w, () => flu.GetDiseaseID());
        Do(w, () => Waiting.NoSleep(((datum_reagents)bobMob.reagents).add_reagent(typeof(datum_reagent_medicine_spaceacillin), 50)));
        int seconds = 0;
        while (seconds < 180 && In(w, () => Dm.locate<datum_disease_flu>(bobMob.diseases) != null))
        {
            w.Run(5 * Second);
            seconds += 5;
            if (seconds % 30 == 0) // keep the medicine in the blood
                Do(w, () => Waiting.NoSleep(((datum_reagents)bobMob.reagents).add_reagent(typeof(datum_reagent_medicine_spaceacillin), 20)));
        }
        output.WriteLine($"cured after {seconds} s");
        Assert.Null(In(w, () => Dm.locate<datum_disease_flu>(bobMob.diseases)));
        Assert.True(In(w, () => Dm.@in(id, bobMob.disease_resistances)), "the cure leaves a resistance");
        Assert.Equal(bootRuntimes, w.World.RuntimeErrorCount);
    }
}
