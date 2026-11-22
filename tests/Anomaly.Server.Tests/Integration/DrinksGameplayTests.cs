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
/// Gaps RG1 (D339) through the real server with headless clients: Alice pours drinks into Bob with the <c>reagent</c> dev verb.
/// Vodka makes him drunk: tg's "drunk" alert reaches his screen (drunk.dm:242) and nobody else's. Very drunk, his first-person
/// view sways (the dizziness's view turn, DV106) and blurs (the eye blur on his game plane). The
/// thermonuclear daiquiri puts tg's "druggy" fullscreen of the drugginess over his view.
/// </summary>
public class DrinksGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    // the icon states of a player's screen entries and their overlays (as the client's ScreenEffects reads them)
    private static HashSet<string> ScreenStates(ReplicaWorld replica)
    {
        var states = new HashSet<string>(StringComparer.Ordinal);
        foreach (ScreenEntry entry in replica.Screen.Values)
        {
            if (!replica.TryGetAppearance(entry.Appearance, out AppearanceEntry a))
                continue;
            if (replica.ResolveString(a.IconState) is string state)
                states.Add(state);
            foreach (TableRef overlay in a.Overlays)
            {
                if (replica.TryGetAppearance(overlay, out AppearanceEntry o) && replica.ResolveString(o.IconState) is string s)
                    states.Add(s);
            }
        }
        return states;
    }

    [Fact]
    public void Drinks_make_Bob_drunk_on_his_screen_sway_and_blur_his_view_and_drugs_trip_it()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 9911, TimeoutSteps = 20000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player bob = w.Join("Bob");
        var bobMob = (mob_living_carbon_human)w.MobOf(bob);
        var texts = new List<string>();
        alice.Client.ServerTextReceived += t => texts.Add(t.Html);
        var bobShakes = new List<CameraShakeMessage>();
        bob.Session.Replica.CameraShakeReceived += m => bobShakes.Add(m);
        w.Run(Second);
        Assert.DoesNotContain("drunk", ScreenStates(bob.Session.Replica));

        // 1. Vodka: tipsy, then drunk at 6 (each Life tick adds sqrt(volume) * 65 * ALCOHOL_RATE): the "drunk" alert on Bob's screen.
        alice.Session.DevCommand("reagent Bob /datum/reagent/consumable/ethanol/vodka 60");
        w.Run(3);
        Assert.Contains(texts, t => t.Contains("of Vodka added", StringComparison.Ordinal));
        w.Run(12 * Second);
        float drunkValue = In(w, () => Dm.num(bobMob.get_drunk_amount()));
        output.WriteLine($"drunk value after 12 s of vodka: {drunkValue}");
        Assert.True(drunkValue >= 6, "Bob is not drunk: " + drunkValue);
        Assert.Contains("drunk", ScreenStates(bob.Session.Replica));
        Assert.DoesNotContain("drunk", ScreenStates(alice.Session.Replica));

        // 2. Very drunk (85, as a few strong drinks make him; under 91, where he would black out): over 41 his view sways, over 71
        // it blurs, (drunk value * 2 - 140) seconds a tick
        bobShakes.Clear();
        In(w, () => { Waiting.NoSleep(((datum_reagents)bobMob.reagents).clear_reagents()); Waiting.NoSleep(bobMob.set_drunk_effect(85)); return 0; });
        w.Run(5 * Second);
        drunkValue = In(w, () => Dm.num(bobMob.get_drunk_amount()));
        output.WriteLine($"drunk value a few ticks after 85 (4% and 0.01 off a tick): {drunkValue}");
        Assert.True(drunkValue >= 71, "Bob is not drunk enough to blur: " + drunkValue);
        Assert.Contains(bobShakes, m => m.Kind == CameraShakeKind.Recoil && m.Steps.Count == 3);
        Assert.True(ViewBlur.Sigma(bob.Session.Replica.GamePlaneFilters, 1080) > 0f, "Bob's view is not blurred");
        Assert.Empty(alice.Session.Replica.GamePlaneFilters);

        // 3. The thermonuclear daiquiri's drugginess: tg's "druggy" fullscreen on Bob's screen.
        alice.Session.DevCommand("reagent Bob /datum/reagent/consumable/ethanol/nuclear_daiquiri_thermo 10");
        w.Run(4 * Second);
        Assert.NotNull(In(w, () => bobMob.has_status_effect(typeof(datum_status_effect_drugginess))));
        Assert.Contains("druggy", ScreenStates(bob.Session.Replica));
        Assert.DoesNotContain("druggy", ScreenStates(alice.Session.Replica));
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}
