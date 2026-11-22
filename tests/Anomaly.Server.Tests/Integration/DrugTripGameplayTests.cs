using Anomaly.Client.Shared;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Gaps RG3 through the real server with headless clients: Alice puts the mushroom hallucinogen into Bob with the <c>reagent</c>
/// dev verb (D195); tg's trip filters on his game plane (drug_reagents.dm: the HSL hue turn and the wave) reach his client and
/// nobody else's, and go when the drug is gone; blastoff's HCY hue and wave do the same.
/// </summary>
public class DrugTripGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    [Fact]
    public void The_mushroom_and_blastoff_trips_tint_and_warp_only_Bobs_view_until_the_drug_is_gone()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 10150, TimeoutSteps = 20000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        w.Run(Second);
        Assert.Empty(bob.Session.Replica.GamePlaneFilters);

        alice.Session.DevCommand("reagent Bob /datum/reagent/drug/mushroomhallucinogen 10");
        w.Run(3 * Second);
        IReadOnlyList<FilterEntry> filters = bob.Session.Replica.GamePlaneFilters;
        output.WriteLine(string.Join(", ", filters.Select(f => f.Kind + "/" + f.Space)));
        var steps = ViewBlur.ColorSteps(filters);
        Assert.Contains(steps, s => s.Space == 2); // FILTER_COLOR_HSL
        Assert.Single(ViewBlur.Waves(filters, 1080));
        Assert.Null(ViewBlur.ColorMatrix(filters)); // no RGB tint: the hue turn is not a red screen
        Assert.Empty(alice.Session.Replica.GamePlaneFilters);

        Do(w, () => Waiting.NoSleep(((datum_reagents)bobMob.reagents).clear_reagents()));
        w.Run(Second);
        Assert.Empty(bob.Session.Replica.GamePlaneFilters);

        alice.Session.DevCommand("reagent Bob /datum/reagent/drug/blastoff 10");
        w.Run(3 * Second);
        filters = bob.Session.Replica.GamePlaneFilters;
        Assert.Contains(ViewBlur.ColorSteps(filters), s => s.Space == 3); // FILTER_COLOR_HCY
        Assert.Single(ViewBlur.Waves(filters, 1080));
        (float r, float g, float b) = ViewBlur.ApplySteps(ViewBlur.ColorSteps(filters), 0f, 0.6f, 0f);
        output.WriteLine($"green through blastoff: {r}, {g}, {b}");
        Assert.True(b > g, "blastoff's hue between red and blue");
        Assert.Empty(alice.Session.Replica.GamePlaneFilters);
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}
