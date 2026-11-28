using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M8z;

/// <summary>
/// ui-overhaul.md section 6, slice E (DV139): the radial's "Offer item" (<see cref="RadialExtras.Of"/>), in a booted toy room with two joined players,
/// Alice and Bob, side by side. It is offered after Strip exactly when tg's <c>give(offered)</c> (living_item_handling.dm:130) would go through to the
/// offer, and its pick (<see cref="RadialExtras.Pick"/>) is tg's give, which makes tg's offer alert on Bob's side.
/// </summary>
public class RadialOfferTests
{
    private const int Second = 20;
    private const string Offer = "Offer item";

    private sealed record Rig(AudioRoom R, mob_living_carbon_human Alice, mob_living_carbon_human Bob);

    private static Rig Build(ulong seed, bool hold = true)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        mob_living_carbon_human alice = r.Join(4, 4, "Alice");
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        r.Step(Second);
        if (hold)
            r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(alice.put_in_hands(Dm.@new<obj_item_crowbar>(alice.loc as atom))))));
        return new Rig(r, alice, bob);
    }

    private static RadialExtra[] Of(Rig g, mob user, atom target) => g.R.In(() => RadialExtras.Of(user, target).Select(e => e.Kind).ToArray());

    private static bool Offered(Rig g) => Of(g, g.Alice, g.Bob).Contains(RadialExtra.Offer);

    // tg's give alert from Alice on Bob's side (status_effect/offering: throw_alert keyed by the offerer's name)
    private static bool AlertOnBob(Rig g) => g.R.In(() => g.Bob.alerts[Dm.str(g.Alice.name)] is atom_movable_screen_alert_give);

    [Fact]
    public void ItIsOfferedAfterStripOnAnotherPersonBesideHimWithAnItemInHand()
    {
        var g = Build(9701);
        RadialExtra[] kinds = Of(g, g.Alice, g.Bob);
        Assert.Contains(RadialExtra.Offer, kinds);
        Assert.True(Array.IndexOf(kinds, RadialExtra.Strip) < Array.IndexOf(kinds, RadialExtra.Offer), string.Join(",", kinds));
        Assert.Equal(Offer, g.R.In(() => RadialExtras.Of(g.Alice, g.Bob).Single(e => e.Kind == RadialExtra.Offer).Label));
        // not on himself
        Assert.DoesNotContain(RadialExtra.Offer, Of(g, g.Alice, g.Alice));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ItIsNotOfferedWithAnEmptyHandOrAnAbstractItemButAHandItemIs()
    {
        var g = Build(9702, hold: false);
        Assert.False(Offered(g), "an empty hand");
        // an abstract item give does not take (item_flags ABSTRACT), and one it takes all the same: a hand item (HAND_ITEM, the *slap hand)
        obj_item thing = g.R.In(() =>
        {
            var item = Dm.@new<obj_item_crowbar>(g.Alice.loc as atom);
            Assert.True(Dm.truthy(Waiting.NoSleep(g.Alice.put_in_hands(item))));
            item.item_flags = Dm.num(Dm.bitor(item.item_flags, ABSTRACT));
            return (obj_item)item;
        });
        Assert.False(Offered(g), "an abstract item");
        g.R.In(() => thing.item_flags = Dm.num(Dm.bitor(thing.item_flags, HAND_ITEM)));
        Assert.True(Offered(g), "a hand item");
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ItIsNotOfferedToOrByAnIncapacitatedMobNorWhileOfferingAlready()
    {
        var g = Build(9703);
        Assert.True(Offered(g));
        g.R.In(() => Waiting.NoSleep(g.Bob.Unconscious(30 * SECONDS)));
        g.R.Step(2);
        Assert.True(g.R.In(() => Dm.truthy(g.Bob.incapacitated)));
        Assert.False(Offered(g), "Bob unconscious");

        var h = Build(9704);
        h.R.In(() => Waiting.NoSleep(h.Alice.Paralyze(30 * SECONDS)));
        h.R.Step(2);
        Assert.True(h.R.In(() => Dm.truthy(h.Alice.incapacitated)));
        Assert.False(Offered(h), "Alice paralyzed");

        var k = Build(9705);
        k.R.Command(() => Waiting.Detach(k.Alice.give(k.Bob)));
        k.R.Step(5);
        Assert.True(AlertOnBob(k), "the first offer");
        Assert.False(Offered(k), "already offering");
        g.R.AssertNoRuntimes();
        h.R.AssertNoRuntimes();
        k.R.AssertNoRuntimes();
    }

    [Fact]
    public void ItIsNotOfferedFromTwoTilesAwayOrToAMobThatCannotHoldItems()
    {
        var g = Build(9706);
        g.R.In(() => Waiting.NoSleep(g.Bob.forceMove(locate(6, 4, 1))));
        g.R.Step(2);
        Assert.False(Offered(g), "two tiles away");
        // a cow beside her has no hands (no TRAIT_CAN_HOLD_ITEMS)
        mob_living cow = g.R.In(() => (mob_living)Dm.@new<mob_living_basic_cow>(locate(4, 5, 1)));
        g.R.Step(2);
        Assert.False(g.R.In(() => HAS_TRAIT(cow, TRAIT_CAN_HOLD_ITEMS)));
        Assert.DoesNotContain(RadialExtra.Offer, Of(g, g.Alice, cow));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void ThePickIsTgsGiveAndMakesTgsOfferAlertOnHisSide()
    {
        var g = Build(9707);
        Assert.False(AlertOnBob(g));
        g.R.Command(() => RadialExtras.Pick(g.Alice.client, g.Bob, Offer));
        g.R.Step(5);
        Assert.True(AlertOnBob(g), "tg's give alert from Alice");
        Assert.True(g.R.In(() => Dm.truthy(g.Alice.has_status_effect(typeof(datum_status_effect_offering)))));
        g.R.AssertNoRuntimes();
    }

    [Fact]
    public void APickWhenItIsNotOfferedDoesNothing()
    {
        // an empty hand: the label sent all the same (a crafted pick) runs nothing
        var g = Build(9708, hold: false);
        g.R.Command(() => RadialExtras.Pick(g.Alice.client, g.Bob, Offer));
        g.R.Step(5);
        Assert.False(AlertOnBob(g));
        Assert.False(g.R.In(() => Dm.truthy(g.Alice.has_status_effect(typeof(datum_status_effect_offering)))));
        // two tiles away with the item: nothing either
        var h = Build(9709);
        h.R.In(() => Waiting.NoSleep(h.Bob.forceMove(locate(6, 4, 1))));
        h.R.Command(() => RadialExtras.Pick(h.Alice.client, h.Bob, Offer));
        h.R.Step(5);
        Assert.False(AlertOnBob(h));
        g.R.AssertNoRuntimes();
        h.R.AssertNoRuntimes();
    }
}
