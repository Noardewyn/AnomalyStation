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
/// Gaps package SE1 (D333, the security gear) in a booted toy room with players. The DRAGnet syncs with its beacon, its snare
/// staggers a man and opens teleportation fields around him that send him to the beacon three seconds later, and a deleted beacon
/// unlinks the gun. The beacon is wrenched down, locked by a security ID (not a cook's), refuses the wrench while locked and is
/// unlocked by an emag that fries its lock. The secway drives with the security key, a banana stuffed into its tailpipe stalls it
/// until it is cleaned out, and broken it smokes until a welder brings it back. The pepper spray refiller refills an empty pepper
/// spray, whose spray blinds and slows a man.
/// </summary>
public class SecurityGearSE1Tests
{
    private static string Diag(Feed feed, AudioRoom r) => "chat: " + string.Join(" | ", feed.Chat.Select(c => c.Html)) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item
    {
        T item = r.In(() => Dm.@new<T>(who.loc));
        r.Step(2);
        r.In(() =>
        {
            Waiting.NoSleep(who.drop_all_held_items());
            Waiting.NoSleep(who.put_in_active_hand(item));
        });
        r.In(() => Assert.Same(item, who.get_active_held_item()));
        return item;
    }

    private static obj_item_card_id Card(AudioRoom r, mob_living_carbon_human who, Type trim) => r.In(() =>
    {
        var card = Dm.@new<obj_item_card_id_advanced>(who.loc);
        Waiting.NoSleep(SSid_access.apply_trim_to_card(card, trim));
        return (obj_item_card_id)card;
    });

    [Fact]
    public void The_DRAGnet_snare_sends_its_target_to_the_synced_beacon()
    {
        var r = new AudioRoom(15, 9, seed: 9971);
        Feed feed = Listen(r);
        var bob = r.Join(3, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        var rob = r.Dummy(8, 4);
        var beacon = Place<obj_item_dragnet_beacon>(r, 12, 7);
        r.Step(Second);
        var gun = Hold<obj_item_gun_energy_e_gun_dragnet>(r, bob);
        Assert.False(r.In(() => InertSeam.IsInert(gun)));

        // Bob syncs the gun by hitting the beacon with it; a second sync says so
        r.In(() => Waiting.NoSleep(beacon.item_interaction(bob, gun, Dm.list())));
        Assert.Same(beacon, r.In(() => gun.linked_beacon));
        Assert.True(feed.Any("beacon synced"), Diag(feed, r));
        r.In(() => gun.link_beacon(bob, beacon));
        Assert.True(feed.Any("already synced!"), Diag(feed, r));

        // the snare mode, fired at Rob
        r.In(() => Waiting.NoSleep(gun.select_fire(bob)));
        Assert.Equal(2f, r.In(() => gun.select));
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(gun.try_fire_gun(rob, bob, "icon-x=16;icon-y=16")))), "the DRAGnet did not fire");
        int fields = 0;
        for (int i = 0; i < 2 * Second && fields == 0; i++)
        {
            r.Step();
            fields = r.In(() => Dm.each<obj_effect_nettingportal>().Count());
        }
        Assert.Equal(9, fields); // every open turf around Rob
        r.In(() =>
        {
            Assert.NotNull(rob.has_status_effect(typeof(datum_status_effect_staggered)));
            Assert.True(Dm.num(rob.get_stamina_loss()) > 0, "no stamina damage");
            Assert.All(Dm.each<obj_effect_nettingportal>(), f => Assert.Equal("dragnetfield", Dm.str(f.icon_state)));
        });

        // three seconds later the fields pop and Rob lands at the beacon
        r.Step(3 * Second + 4);
        Assert.Equal(0, r.In(() => Dm.each<obj_effect_nettingportal>().Count()));
        Assert.True(r.In(() => Dm.num(get_dist(rob, beacon))) <= 1, "Rob was not sent to the beacon: " + r.In(() => $"{rob.x},{rob.y}"));
        Assert.Equal(3f, r.In(() => bob.x)); // Bob was not caught

        // a deleted beacon unlinks the gun
        r.In(() => qdel(beacon));
        Assert.Null(r.In(() => gun.linked_beacon));
        Assert.True(feed.Any("no longer linked with a DRAGnet beacon!"), Diag(feed, r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_beacon_is_wrenched_down_locked_by_security_and_fried_by_an_emag()
    {
        var r = new AudioRoom(13, 9, seed: 9972);
        Feed feed = Listen(r);
        var bob = r.Join(5, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        var beacon = Place<obj_item_dragnet_beacon>(r, 6, 4);
        r.Step(Second);
        var officer = Card(r, bob, typeof(datum_id_trim_job_security_officer));
        var cook = Card(r, bob, typeof(datum_id_trim_job_cook));

        r.In(() => Waiting.NoSleep(beacon.item_interaction(bob, officer, Dm.list())));
        Assert.True(feed.Any("wrench the beacon first!"), Diag(feed, r));
        Assert.Equal(0f, r.In(() => beacon.locked));

        var wrench = Hold<obj_item_wrench>(r, bob);
        r.In(() => Waiting.NoSleep(beacon.wrench_act(bob, wrench)));
        Assert.Equal(1f, r.In(() => beacon.anchored));
        Assert.True(feed.Any("balloon: anchored"), Diag(feed, r));

        r.In(() => Waiting.NoSleep(beacon.item_interaction(bob, officer, Dm.list())));
        Assert.Equal(1f, r.In(() => beacon.locked));
        Assert.True(feed.Any("beacon locked"), Diag(feed, r));
        r.In(() => Waiting.NoSleep(beacon.wrench_act(bob, wrench)));
        Assert.True(feed.Any("must be unlocked first!"), Diag(feed, r));
        Assert.Equal(1f, r.In(() => beacon.anchored));

        r.In(() => Waiting.NoSleep(beacon.item_interaction(bob, cook, Dm.list())));
        Assert.True(feed.Any("no access!"), Diag(feed, r));
        Assert.Equal(1f, r.In(() => beacon.locked));

        // the emag: unlocked, unanchored, the access control fried for good
        r.In(() => Assert.Equal(TRUE, Waiting.NoSleep(beacon.emag_act(bob, Dm.@new<obj_item_card_emag>(bob.loc)))));
        Assert.Equal((0f, 0f), r.In(() => (beacon.locked, beacon.anchored)));
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(beacon.obj_flags, EMAGGED))));
        r.In(() => Assert.Equal(FALSE, Waiting.NoSleep(beacon.emag_act(bob, Dm.@new<obj_item_card_emag>(bob.loc)))));
        r.In(() => Waiting.NoSleep(beacon.wrench_act(bob, wrench)));
        r.In(() => Waiting.NoSleep(beacon.item_interaction(bob, officer, Dm.list())));
        Assert.True(feed.Any("the access control is fried!"), Diag(feed, r));
        Assert.Equal(0f, r.In(() => beacon.locked));

        // held, it cannot be wrenched
        r.In(() => Waiting.NoSleep(beacon.wrench_act(bob, wrench))); // unanchor it first
        r.In(() =>
        {
            Waiting.NoSleep(bob.drop_all_held_items());
            Waiting.NoSleep(bob.put_in_active_hand(beacon));
            Waiting.NoSleep(beacon.wrench_act(bob, wrench));
        });
        Assert.True(feed.Any("put it down first!"), Diag(feed, r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_secway_drives_stalls_on_a_banana_and_is_welded_back()
    {
        var r = new AudioRoom(13, 9, seed: 9973);
        Feed feed = Listen(r);
        var bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        var al = r.Join(6, 5, "Al");
        var way = Place<obj_vehicle_ridden_secway>(r, 4, 4);
        r.Step(Second);
        Assert.False(r.In(() => InertSeam.IsInert(way)));
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(way.buckle_mob(bob, force: TRUE)))));
        Assert.NotNull(r.In(() => way.GetComponent(typeof(datum_component_riding_vehicle_secway))));

        var key = Hold<obj_item_key_security>(r, bob);
        r.In(() => Waiting.NoSleep(way.item_interaction(bob, key, Dm.list())));
        r.In(() => Assert.Same(key, way.inserted_key));
        r.In(() => way.relaydrive(bob, EAST));
        r.Step(Second);
        Assert.Equal(5f, r.In(() => way.x));
        Assert.Equal(5f, r.In(() => bob.x)); // the rider rides along

        // Al stuffs a banana into the tailpipe: the secway sputters
        var banana = Hold<obj_item_food_grown_banana>(r, al);
        r.In(() => Waiting.Detach(way.item_interaction(al, banana, Dm.list())));
        r.Step(4 * Second);
        Assert.Same(banana, r.In(() => way.eddie_murphy));
        Assert.True(feed.Any("You stuff the banana into the secway's tailpipe."), Diag(feed, r));
        r.In(() => way.relaydrive(bob, EAST));
        r.Step(Second);
        Assert.Equal(5f, r.In(() => way.x));
        Assert.True(feed.Any("sputters and refuses to move!"), Diag(feed, r));
        Assert.Contains(r.Sounds.Heard, s => (s.File ?? "").EndsWith("stall.ogg", StringComparison.Ordinal));
        Assert.Contains(r.In(() => Dm.each(Waiting.NoSleep(way.examine(al)) as DmList).Select(Dm.str).ToList()), l => l.Contains("Something appears to be stuck in its exhaust...", StringComparison.Ordinal));

        // Al cleans it out by hand: it drives again
        r.In(() => Waiting.Detach(way.attack_hand(al, Dm.list())));
        r.Step(7 * Second);
        Assert.Null(r.In(() => way.eddie_murphy));
        Assert.True(r.In(() => banana.loc is turf));
        r.In(() => way.relaydrive(bob, EAST));
        r.Step(Second);
        Assert.Equal(6f, r.In(() => way.x));

        // broken, it smokes; a welder brings it back to full health and the smoke stops
        r.In(() => Waiting.NoSleep(way.unbuckle_mob(bob, TRUE)));
        r.In(() => Waiting.NoSleep(way.take_damage(40, BRUTE, MELEE, 0)));
        Assert.Equal(24f, r.In(() => Dm.num(way.atom_integrity))); // 40 melee through its 10 melee armor: 36 off 60, below the 50% failure line
        Assert.True(r.In(() => Dm.@in(way, SSobj.processing)), "a broken secway does not process");
        bool smoked = false;
        for (int i = 0; i < 10 && !smoked; i++)
        {
            r.Step(Second);
            smoked = r.In(() => Dm.each<obj_effect_particle_effect_fluid_smoke>().Any());
        }
        Assert.True(smoked, "the broken secway does not smoke");

        var welder = Hold<obj_item_weldingtool_largetank>(r, bob);
        r.In(() => welder.switched_on(bob));
        r.In(() => Assert.True(Dm.truthy(welder.welding)));
        r.In(() => Waiting.Detach(way.welder_act(bob, welder)));
        r.Step(13 * Second);
        Assert.Equal(60f, r.In(() => Dm.num(way.atom_integrity)));
        Assert.True(feed.Any("fully repaired the secway"), Diag(feed, r));
        r.Step(3 * Second);
        Assert.False(r.In(() => Dm.@in(way, SSobj.processing)), "a repaired secway still processes");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_pepper_spray_refiller_refills_a_pepper_spray_that_blinds_a_man()
    {
        var r = new AudioRoom(13, 9, seed: 9974);
        Feed feed = Listen(r);
        var bob = r.Join(5, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        var rob = r.Dummy(7, 4);
        var tank = Place<obj_structure_reagent_dispensers_wall_peppertank>(r, 5, 5);
        r.Step(Second);
        Assert.False(r.In(() => InertSeam.IsInert(tank)));
        Assert.True(r.In(() => Dm.truthy(((datum_reagents)tank.reagents).has_reagent(typeof(datum_reagent_consumable_condensedcapsaicin)))));

        var spray = Hold<obj_item_reagent_containers_spray_pepper>(r, bob);
        r.In(() => Waiting.NoSleep(((datum_reagents)spray.reagents).clear_reagents()));
        r.In(() => Waiting.NoSleep(spray.try_spray(tank, bob)));
        Assert.True(r.In(() => Dm.num(((datum_reagents)spray.reagents).total_volume)) > 0, Diag(feed, r));
        Assert.True(r.In(() => Dm.truthy(((datum_reagents)spray.reagents).has_reagent(typeof(datum_reagent_consumable_condensedcapsaicin)))));

        r.Step(Second);
        r.In(() => Waiting.NoSleep(spray.try_spray(rob, bob)));
        bool hit = false;
        for (int i = 0; i < 3 * Second && !hit; i++)
        {
            r.Step();
            hit = r.In(() => Dm.truthy(rob.is_blind()));
        }
        Assert.True(hit, "the pepper spray did not blind Rob: " + Diag(feed, r));
        Assert.NotNull(r.In(() => rob.has_movespeed_modifier(typeof(datum_movespeed_modifier_reagent_pepperspray))));
        r.AssertNoRuntimes();
    }
    [Fact]
    public void The_barrier_grenade_cycles_its_modes_and_raises_a_row_of_security_barriers()
    {
        var r = new AudioRoom(13, 9, seed: 9975);
        Feed feed = Listen(r);
        var bob = r.Join(3, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        var grenade = Hold<obj_item_grenade_barrier>(r, bob);
        Assert.False(r.In(() => InertSeam.IsInert(grenade)));
        Assert.Contains(r.In(() => Dm.each(Waiting.NoSleep(grenade.examine(bob)) as DmList).Select(Dm.str).ToList()), l => l.Contains("Alt-click to toggle modes.", StringComparison.Ordinal));

        // alt-click: single -> vertical -> horizontal
        r.In(() => Waiting.NoSleep(grenade.click_alt(bob)));
        Assert.Equal("vertical", r.In(() => grenade.mode));
        r.In(() => Waiting.NoSleep(grenade.click_alt(bob)));
        Assert.Equal("horizontal", r.In(() => grenade.mode));
        Assert.True(feed.Any("is now in horizontal mode."), Diag(feed, r));

        // set down in the open and set off: a row of three barriers, east to west
        r.In(() =>
        {
            Waiting.NoSleep(bob.dropItemToGround(grenade));
            Waiting.NoSleep(grenade.forceMove(locate(7, 4, 1)));
            Waiting.NoSleep(grenade.detonate(bob));
        });
        r.Step(2);
        Assert.True(r.In(() => QDELETED(grenade)));
        var barriers = r.In(() => Dm.each<obj_structure_barricade_security>().Select(b => ((int)b.x, (int)b.y)).OrderBy(p => p).ToList());
        Assert.Equal(new[] { (6, 4), (7, 4), (8, 4) }, barriers);
        r.In(() => Assert.False(InertSeam.IsInert(Dm.each<obj_structure_barricade_security>().First())));
        r.AssertNoRuntimes();
    }
}
