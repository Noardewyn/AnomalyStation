using System.Globalization;
using System.Text.RegularExpressions;
using Anomaly.Client.Shared;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M8 slice B through the real server with headless clients: Alice puts libital into the wounded Bob with the <c>reagent</c> dev verb
/// (D195) and reads him with <c>analyze</c> and <c>reagents</c>; the brute damage her client reads drops by tg's 3 a metabolism (0.2u of
/// libital each, cat2_medicine_reagents.dm:113). Morphine's drowsiness blurs Bob's own view (tg's eye blur on his game plane, protocol
/// 29) and nobody else's; his dizziness sways his first-person view (the client's chain of view offsets, DV106).
/// </summary>
public class MedicineGameplayTests(ITestOutputHelper output)
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

    // The healthscan's overall brute (health_analyzer.dm: ceil(brute_loss) in the damage table's Overall row) and the reagents verb's
    // libital, from Alice's chat.
    private static readonly Regex Overall = new(@"Overall:</b></font></td><td><font color='#ff3333'><b>(\d+)</b>", RegexOptions.CultureInvariant);
    private static readonly Regex Libital = new(@"blood: Libital ([0-9.]+)u", RegexOptions.CultureInvariant);

    private static (int Brute, float Libital) Read(RealHeadlessWorld w, RealHeadlessWorld.Player alice, List<string> texts)
    {
        texts.Clear();
        alice.Session.DevCommand("analyze Bob");
        alice.Session.DevCommand("reagents Bob");
        w.Run(3);
        int brute = texts.Select(t => Overall.Match(t)).Where(m => m.Success).Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).Single();
        float libital = texts.Select(t => Libital.Match(t)).Where(m => m.Success).Select(m => float.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture)).Single();
        return (brute, libital);
    }

    [Fact]
    public void Libital_put_into_Bob_by_dev_heals_him_at_tgs_rate_on_Alices_analyzer_and_morphine_blurs_only_his_view()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 85, TimeoutSteps = 20000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var texts = new List<string>();
        alice.Client.ServerTextReceived += t => texts.Add(t.Html);
        var bobShakes = new List<CameraShakeMessage>();
        bob.Session.Replica.CameraShakeReceived += m => bobShakes.Add(m);
        w.Run(Second);

        // 1. Bob is hurt (no wound: the wound's determination would heal him too) and given libital.
        Do(w, () => Waiting.NoSleep(((obj_item_bodypart)bobMob.get_bodypart(BODY_ZONE_CHEST)).receive_damage(40, 0, wound_bonus: CANT_WOUND)));
        alice.Session.DevCommand("reagent Bob /datum/reagent/medicine/c2/libital 10");
        w.Run(3);
        Assert.Contains(texts, t => t.Contains("of Libital added", StringComparison.Ordinal));

        (int brute0, float libital0) = Read(w, alice, texts);
        w.Run(10 * Second);
        (int brute1, float libital1) = Read(w, alice, texts);
        int metabolisms = (int)Math.Round((libital0 - libital1) / 0.2f);
        output.WriteLine($"brute {brute0} -> {brute1}, libital {libital0} -> {libital1}: {metabolisms} metabolisms");
        Assert.InRange(metabolisms, 4, 6); // a Life tick every 2 seconds
        Assert.Equal(3 * metabolisms, brute0 - brute1);

        // 2. Morphine at its drowsy cycles: the drowsiness's eye blur reaches Bob's game plane, not Alice's.
        Assert.Empty(bob.Session.Replica.GamePlaneFilters);
        alice.Session.DevCommand("reagent Bob /datum/reagent/medicine/morphine 9");
        w.Run(3);
        // tg drowses from the 24th cycle (48 seconds of metabolism): the test moves the reagent to the 48th, where each metabolism
        // drowses him for 3 seconds (3 SECONDS * a ratio of 0.5 * 2), longer than a Life tick, so the drowsiness lasts to tick its blur
        // (from the 36th it is exactly 2 seconds, and whether it lasts depends on which subsystem fires first in the tick)
        Do(w, () => ((datum_reagent)((datum_reagents)bobMob.reagents).has_reagent(typeof(datum_reagent_medicine_morphine))).current_cycle = 48);
        w.Run(5 * Second);
        Assert.NotNull(In(w, () => bobMob.has_status_effect(typeof(datum_status_effect_drowsiness))));
        FilterEntry blur = Assert.Single(bob.Session.Replica.GamePlaneFilters);
        Assert.Equal(FilterKind.Blur, blur.Kind);
        Assert.True(ViewBlur.Sigma(bob.Session.Replica.GamePlaneFilters, 1080) > 0f, "Bob's view is blurred");
        Assert.Empty(alice.Session.Replica.GamePlaneFilters);

        // 3. Dizziness: tg's three relative steps of Bob's client, sent as a view turn that comes back to rest.
        bobShakes.Clear();
        Do(w, () => Waiting.NoSleep(bobMob.set_dizzy_if_lower(10 * SECONDS)));
        w.Run(3 * Second);
        CameraShakeMessage sway = Assert.Single(bobShakes.Where(m => m.Steps.Count == 3).Take(1));
        Assert.Equal(CameraShakeKind.Recoil, sway.Kind);
        Assert.All(sway.Steps, s => Assert.Equal(3, s.Time));
        Assert.Equal((0, 0), (sway.Steps[2].PixelX, sway.Steps[2].PixelY));
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}
