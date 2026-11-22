using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package H4 (D333, the janitor) in a booted toy room with Bob as a player: the mop bucket wets a mop, the janitorial cart
/// carries the janitor's tools (and shows them) and dumps its bucket, the janicart is driven with its key and hooks a trash bag, its
/// vacuum upgrade sucks up what it drives over and its floor buffer scrubs the floor, and the advanced mop wets itself.
/// </summary>
public class JanitorH4Tests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(who.put_in_hands(item)));
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    // a left click after tg's click cooldown of the one before
    private static void Use(AudioRoom r, mob who, atom target)
    {
        r.Step(Second);
        Click(r, who, target);
    }

    private static void RightClick(AudioRoom r, mob who, atom target)
    {
        r.Step(Second);
        r.Command(() => ClientSeam.Click(who.client, target, "icon-x=16;icon-y=16;right=1;button=right"));
        r.Step(2);
    }

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    private static float Volume(AudioRoom r, atom a) => r.In(() => ((datum_reagents)a.reagents).total_volume);

    [Fact]
    public void TheMopBucketWetsAMopAndShowsItsWater()
    {
        var (r, _, bob) = Room(7401);
        var bucket = Place<obj_structure_mop_bucket>(r, 5, 4);
        Assert.Equal(0f, Volume(r, bucket));
        r.In(() => Waiting.NoSleep(((datum_reagents)bucket.reagents).add_reagent(typeof(datum_reagent_water), 50)));
        r.In(() => bucket.update_appearance());
        Assert.Contains("mopbucket_water", Overlays(r, bucket));

        var mop = Hold<obj_item_mop>(r, bob);
        RightClick(r, bob, bucket);
        Assert.True(Volume(r, mop) > 0, "the mop was not wetted");
        Assert.True(Volume(r, bucket) < 50);
        Assert.Contains(r.Sounds.Heard, h => h.File?.Contains("slosh") == true);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheJanitorialCartCarriesAndShowsTheJanitorsToolsAndDumpsItsBucket()
    {
        var (r, feed, bob) = Room(7402);
        var cart = Place<obj_structure_mop_bucket_janitorialcart>(r, 5, 4);
        Assert.Equal(250f, r.In(() => ((datum_reagents)cart.reagents).maximum_volume)); // 100 * 2.5
        r.In(() => Assert.Contains(cart, Dm.each<object>(GLOB.janitor_devices)));

        Hold<obj_item_mop>(r, bob);
        Use(r, bob, cart);
        Hold<obj_item_pushbroom>(r, bob);
        Use(r, bob, cart);
        Hold<obj_item_storage_bag_trash>(r, bob);
        Use(r, bob, cart);
        Hold<obj_item_reagent_containers_spray_cleaner>(r, bob);
        Use(r, bob, cart);
        Hold<obj_item_clothing_suit_caution>(r, bob);
        Use(r, bob, cart);
        Hold<obj_item_clothing_suit_caution>(r, bob);
        Use(r, bob, cart);
        r.In(() =>
        {
            Assert.NotNull(cart.mymop);
            Assert.NotNull(cart.mybroom);
            Assert.NotNull(cart.mybag);
            Assert.NotNull(cart.myspray);
            Assert.Equal(2, cart.held_signs.len);
        });
        var overlays = Overlays(r, cart);
        foreach (string overlay in new[] { "cart_mop", "cart_broom", "cart_garbage", "cart_spray", "cart_sign2" })
            Assert.Contains(overlay, overlays);

        // a right click with an empty hand takes the mop back
        RightClick(r, bob, cart);
        r.In(() =>
        {
            Assert.Null(cart.mymop);
            Assert.IsType<obj_item_mop>(bob.get_active_held_item());
        });
        Assert.DoesNotContain("cart_mop", Overlays(r, cart));

        // its bucket is dumped on the floor with a crowbar
        r.In(() => qdel(bob.get_active_held_item()));
        r.In(() => Waiting.NoSleep(((datum_reagents)cart.reagents).add_reagent(typeof(datum_reagent_water), 60)));
        Hold<obj_item_crowbar>(r, bob);
        Use(r, bob, cart);
        r.Step(6 * Second);
        Assert.Equal(0f, Volume(r, cart));
        Assert.True(feed.Any("You dumped the contents of"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheJanicartDrivesWithItsKeyAndItsVacuumAndBufferWorkTheFloor()
    {
        var (r, feed, bob) = Room(7403);
        var cart = Place<obj_vehicle_ridden_janicart>(r, 4, 4);
        r.In(() => Assert.Contains(cart, Dm.each<object>(GLOB.janitor_devices)));
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(cart.buckle_mob(bob, force: TRUE)))));
        Assert.NotNull(r.In(() => cart.GetComponent(typeof(datum_component_riding_vehicle_janicart))));
        r.In(() => Assert.True(cart.is_occupant(bob)));

        // no key: the cart refuses to move
        int startX = r.In(() => (int)cart.x);
        r.In(() => cart.relaydrive(bob, EAST));
        r.Step(Second);
        Assert.Equal(startX, r.In(() => (int)cart.x));
        Assert.True(feed.Any("has no key inserted!"));

        // the janitor's key goes in by hand
        var key = Hold<obj_item_key_janitor>(r, bob);
        r.In(() => Waiting.NoSleep(cart.item_interaction(bob, key, Dm.list())));
        r.In(() => Assert.Same(key, cart.inserted_key));

        // a trash bag is hooked on, then the vacuum upgrade installed: what it drives over goes into the bag
        var bag = Hold<obj_item_storage_bag_trash>(r, bob);
        r.In(() => Waiting.NoSleep(cart.item_interaction(bob, bag, Dm.list())));
        r.In(() => Assert.Same(bag, cart.trash_bag));
        Assert.Contains("cart_garbage", Overlays(r, cart));
        var vacuum = Hold<obj_item_janicart_upgrade_vacuum>(r, bob);
        r.In(() => Waiting.NoSleep(cart.item_interaction(bob, vacuum, Dm.list())));
        r.In(() => Assert.NotNull(cart.GetComponent(typeof(datum_component_vacuum))));
        var litter = r.In(() => Dm.@new<obj_item_trash_chips>(locate(5, 4, 1)));
        r.In(() => cart.relaydrive(bob, EAST));
        r.Step(Second);
        Assert.Equal(startX + 1, r.In(() => (int)cart.x));
        Assert.Equal(r.In(() => cart.x), r.In(() => bob.x)); // the rider rides along
        r.In(() => Assert.Same(bag, litter.loc));

        // the vacuum comes out with a screwdriver, and the floor buffer scrubs the blood off the next tile
        var screwdriver = Hold<obj_item_screwdriver>(r, bob);
        r.In(() => Waiting.NoSleep(cart.screwdriver_act(bob, screwdriver)));
        r.In(() => Assert.Null(cart.GetComponent(typeof(datum_component_vacuum))));
        r.In(() => qdel(bob.get_active_held_item()));
        r.In(() => qdel(screwdriver));
        var buffer = Hold<obj_item_janicart_upgrade_buffer>(r, bob);
        r.In(() => Waiting.NoSleep(cart.item_interaction(bob, buffer, Dm.list())));
        var blood = r.In(() => Dm.@new<obj_effect_decal_cleanable_blood>(locate(6, 4, 1)));
        r.In(() => cart.relaydrive(bob, EAST));
        r.Step(Second);
        Assert.Equal(startX + 2, r.In(() => (int)cart.x));
        r.In(() => Assert.True(QDELETED(blood), "the buffer did not scrub the blood"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheAdvancedMopWetsItselfWhileItsCondenserIsOn()
    {
        var (r, feed, bob) = Room(7404);
        var mop = Hold<obj_item_mop_advanced>(r, bob);
        r.Step(10 * Second);
        float wet = Volume(r, mop);
        Assert.True(wet > 3, $"the condenser did not wet the mop: {wet}");
        Assert.True(r.In(() => Dm.truthy(((datum_reagents)mop.reagents).has_reagent(typeof(datum_reagent_water)))));

        // switched off, it stops
        r.Command(() => Waiting.Detach(mop.attack_self(bob)));
        r.Step(2);
        r.In(() => Assert.False(Dm.truthy(mop.refill_enabled)));
        r.In(() => Waiting.NoSleep(((datum_reagents)mop.reagents).clear_reagents()));
        r.Step(5 * Second);
        Assert.Equal(0f, Volume(r, mop));
        r.AssertNoRuntimes();
    }
}
