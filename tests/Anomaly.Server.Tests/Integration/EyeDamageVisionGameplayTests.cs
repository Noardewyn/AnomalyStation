using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// The owner's play test of 2026-10-27: welding without protection gave a light blur that never got worse. tg's damaged eyes make the sight
/// nearsighted (the impaired fullscreen, severity 2 past the low threshold, 3 past the high one) and blind when they fail (the blind
/// fullscreen: dark with a small circle of sight), and healing takes it back (_eyes.dm:432-453, nearsighted.dm). The client of the player
/// sees each fullscreen's picture.
/// </summary>
public class EyeDamageVisionGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    // The icon states of the fullscreens the client holds.
    private static List<string> Fullscreens(RealHeadlessWorld.Player p)
        => p.Session.Replica.Screen.Values.SelectMany(e => CrewOutfitTests.Icons(p.Session.Replica, e.Appearance)).Select(i => i.State)
            .Where(s => s != null && (s.StartsWith("impairedoverlay", StringComparison.Ordinal) || s.StartsWith("blackimageoverlay", StringComparison.Ordinal)))
            .ToList();

    [Fact]
    public void Damaged_eyes_blur_the_sight_more_then_blind_it_and_heal_back()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 3, TimeoutSteps = 2000 }, strict: false);
        RealHeadlessWorld.Player p = w.Join("Welder");
        var me = (mob_living_carbon_human)w.MobOf(p);
        w.Run(Second);
        var eyes = In(w, () => (obj_item_organ_eyes)me.get_organ_slot(ORGAN_SLOT_EYES));
        float low = In(w, () => eyes.low_threshold), high = In(w, () => eyes.high_threshold), most = In(w, () => Dm.num(eyes.maxHealth));
        output.WriteLine($"thresholds {low} {high}, max {most}");
        Assert.Empty(Fullscreens(p));

        void Damage(float amount)
        {
            In(w, () => Waiting.NoSleep(eyes.apply_organ_damage(amount)));
            w.Run(3);
        }

        Damage(low + 1);
        Assert.Contains("impairedoverlay2", Fullscreens(p));
        Damage(high - low);
        Assert.Contains("impairedoverlay3", Fullscreens(p));
        Assert.DoesNotContain("impairedoverlay2", Fullscreens(p));
        Damage(most);
        Assert.Contains(Fullscreens(p), s => s.StartsWith("blackimageoverlay", StringComparison.Ordinal));
        Assert.True(In(w, () => Dm.truthy(me.is_blind())));

        Damage(-most); // healed
        w.Run(2 * Second); // clear_fullscreen fades the blind overlay out over a second
        Assert.Empty(Fullscreens(p));
        Assert.False(In(w, () => Dm.truthy(me.is_nearsighted())));
    }
}
