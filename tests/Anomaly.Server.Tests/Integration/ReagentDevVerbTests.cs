using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M8 slice A (D195) through the real server and a real client: Alice's dev console puts water and rat spit into her blood with
/// <c>reagent</c> (tg's admin Add reagent), her Life metabolizes the water away at tg's 0.4 units a tick, the rat spit past its
/// threshold starts tg's overdose with its chat line to her, and <c>reagents</c> lists what her blood holds.
/// </summary>
public class ReagentDevVerbTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    [Fact]
    public void Reagent_and_reagents_add_list_and_metabolize_the_blood()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 81, TimeoutSteps = 4000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        var texts = new List<string>();
        alice.Client.ServerTextReceived += t => texts.Add(t.Html);
        datum_reagents Blood() => (datum_reagents)me.reagents;

        alice.Session.DevCommand("reagent me water 10");
        w.Run(2);
        output.WriteLine(string.Join(" | ", texts));
        Assert.Contains(texts, t => t.Contains("10u of Water added", StringComparison.Ordinal));
        float added = In(w, () => Dm.num(Blood().get_reagent_amount(typeof(datum_reagent_water))));
        Assert.InRange(added, 9.6f, 10f);

        alice.Session.DevCommand("reagent me rat_spit 25");
        alice.Session.DevCommand("reagents me");
        w.Run(2);
        Assert.Contains(texts, t => t.Contains("blood: Water", StringComparison.Ordinal));
        Assert.Contains(texts, t => t.Contains("blood: Rat Spit 25", StringComparison.Ordinal));

        // a few Life ticks: each takes 0.4 units of water, and the rat spit's overdose starts once
        w.Run(10 * Second);
        float left = In(w, () => Dm.num(Blood().get_reagent_amount(typeof(datum_reagent_water))));
        output.WriteLine($"water {added} -> {left}; " + In(w, () => Dm.str(Blood().get_reagent_log_string())));
        Assert.True(left < added - 0.79f, "two or more Life ticks metabolized the water");
        Assert.Equal(0f, (added - left) * 10 % 4, 2); // whole ticks of tg's 0.2 x 2 seconds
        Assert.Single(texts, t => t.Contains("You feel like you took too much of Rat Spit!", StringComparison.Ordinal));
        Assert.True(In(w, () => Dm.truthy(((datum_reagent)Blood().has_reagent(typeof(datum_reagent_rat_spit))).overdosed)));

        alice.Session.DevCommand("reagent me no_such_reagent 5");
        w.Run(2);
        Assert.Contains(texts, t => t.Contains("is no reagent", StringComparison.Ordinal));
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}
