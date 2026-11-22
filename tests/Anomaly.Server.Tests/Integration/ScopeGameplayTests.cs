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

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// M7x slice D, DV98 (the battle rifle's scope) on a real server and a real client: Alice's right click with the BR-38 scopes in (tg's
/// scope component puts the "scope" fullscreen on her screen) and her client's zoom is the rifle's range modifier, 2; a second right
/// click scopes out and the zoom goes back to nothing. No protocol change: the zoom rides in the screen object's layer.
/// </summary>
public class ScopeGameplayTests(ITestOutputHelper output)
{
    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void In(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    [Fact]
    public void A_right_click_with_the_battle_rifle_zooms_the_clients_view_by_the_range_modifier_and_a_second_one_puts_it_back()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 63, TimeoutSteps = 6000 });
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var aliceMob = (mob_living_carbon_human)w.MobOf(alice);
        w.Run(10);
        obj_item_gun_ballistic_automatic_battle_rifle gun = In(w, () =>
        {
            var g = Dm.@new<obj_item_gun_ballistic_automatic_battle_rifle>(get_turf(aliceMob));
            Waiting.NoSleep(aliceMob.put_in_active_hand(g));
            return g;
        });
        w.Run(20);
        Assert.Equal(1f, ScopeZoom.Of(alice.Session.Replica));

        // the right click on the floor a few tiles away (a ranged click, not an adjacent one: the rifle's component takes the secondary click)
        turf far = In(w, () => locate(Dm.num(get_turf(aliceMob).x) + 3, Dm.num(get_turf(aliceMob).y), Dm.num(get_turf(aliceMob).z)));
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Turf, 0, In(w, () => (int)Dm.num(far.x)), In(w, () => (int)Dm.num(far.y)), In(w, () => (int)Dm.num(far.z)), 16, 16), ClickButton.Right, ClickModifiers.None));
        w.Run(20);
        output.WriteLine("zoom in: " + ScopeZoom.Of(alice.Session.Replica) + " trait " + In(w, () => HAS_TRAIT(aliceMob, TRAIT_USER_SCOPED)));
        Assert.Equal(2f, ScopeZoom.Of(alice.Session.Replica), 2);
        Assert.True(In(w, () => HAS_TRAIT(aliceMob, TRAIT_USER_SCOPED)));
        Assert.Equal(41.98f, ScopeZoom.FovDegrees(75f, ScopeZoom.Of(alice.Session.Replica)), 1);

        // the second right click scopes out: the fullscreen fades, the zoom is gone at once
        Assert.True(alice.Session.Click(new ClickTarget(TargetKind.Turf, 0, In(w, () => (int)Dm.num(far.x)), In(w, () => (int)Dm.num(far.y)), In(w, () => (int)Dm.num(far.z)), 16, 16), ClickButton.Right, ClickModifiers.None));
        w.Run(20);
        Assert.False(In(w, () => HAS_TRAIT(aliceMob, TRAIT_USER_SCOPED)));
        Assert.Equal(1f, ScopeZoom.Of(alice.Session.Replica));
        Assert.Equal(0, w.World.RuntimeErrorCount);
    }
}
