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
/// Gaps package CL1 (D333) through the real server: the welding goggles darken their wearer's view (tg's "tint" fullscreen, the
/// impaired overlay at severity 2, carbon.dm:455) until their visor is pushed up, and another player sees them worn down and up
/// (tg's "welding-g" and "welding-gup" worn states); the welding mask's visor goes the same way with its "weldingmask" states.
/// </summary>
public class ClothingCL1GameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    // The icon states of the impaired fullscreens the client holds.
    private static List<string> Fullscreens(RealHeadlessWorld.Player p)
        => p.Session.Replica.Screen.Values.SelectMany(e => CrewOutfitTests.Icons(p.Session.Replica, e.Appearance)).Select(i => i.State)
            .Where(s => s != null && s.StartsWith("impairedoverlay", StringComparison.Ordinal))
            .ToList();

    // The icon states the watcher's client draws the mob with (its overlays: the worn clothing).
    private static List<string> SeenStates(RealHeadlessWorld.Player watcher, mob who)
    {
        ReplicaEntity seen = watcher.Session.Replica.Entities.Single(e => e.Id == who.dm_id);
        return CrewOutfitTests.Icons(watcher.Session.Replica, seen.State.Appearance).Select(i => i.State).Where(s => s != null).ToList();
    }

    [Fact]
    public void Welding_visors_darken_the_view_until_pushed_up_and_others_see_them_flipped()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 7, TimeoutSteps = 2000 }, strict: false);
        RealHeadlessWorld.Player welder = w.Join("Welder");
        RealHeadlessWorld.Player watcher = w.Join("Watcher");
        var me = (mob_living_carbon_human)w.MobOf(welder);
        w.Run(Second);
        Assert.Empty(Fullscreens(welder));

        var goggles = In(w, () =>
        {
            var g = Dm.@new<obj_item_clothing_glasses_welding>(me.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(me.equip_to_slot_if_possible(g, ITEM_SLOT_EYES, bypass_equip_delay_self: 1))));
            return g;
        });
        w.Run(5);
        Assert.Contains("impairedoverlay2", Fullscreens(welder));
        Assert.Contains("welding-g", SeenStates(watcher, me));

        // the visor's toggle (the action button's attack_self): up, the view clears, the goggles are drawn pushed up
        In(w, () => Waiting.NoSleep(goggles.attack_self(me)));
        w.Run(5);
        Assert.DoesNotContain("impairedoverlay2", Fullscreens(welder));
        List<string> up = SeenStates(watcher, me);
        output.WriteLine("goggles up: " + string.Join(", ", up));
        Assert.Contains("welding-gup", up);

        // the welding mask: the same tint, and its visor up and down drawn on the face
        In(w, () => Waiting.NoSleep(me.dropItemToGround(goggles)));
        var mask = In(w, () =>
        {
            var m = Dm.@new<obj_item_clothing_mask_gas_welding>(me.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(me.equip_to_slot_if_possible(m, ITEM_SLOT_MASK, bypass_equip_delay_self: 1))));
            return m;
        });
        w.Run(5);
        Assert.Contains("impairedoverlay2", Fullscreens(welder));
        Assert.Contains("weldingmask", SeenStates(watcher, me));
        In(w, () => Waiting.NoSleep(mask.attack_self(me)));
        w.Run(5);
        Assert.DoesNotContain("impairedoverlay2", Fullscreens(welder));
        Assert.Contains("weldingmaskup", SeenStates(watcher, me));
        output.WriteLine($"runtimes {w.World.RuntimeErrorCount}");
    }
}
