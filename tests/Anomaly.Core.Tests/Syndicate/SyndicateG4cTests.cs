using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Syndicate;

/// <summary>
/// Syndicate stage G4c (D316): His Grace wakes, hungers, guards its wielder from stuns (tg's stun absorption), heals them, devours the
/// downed and falls asleep again; a supply pod falls on its landing zone, lands with its door masks drawn by the icon recipe, drops
/// its cargo and leaves; the surplus crate comes by pod; the monkey agent beacon polls the ghosts and drops the chosen one in a pod as a
/// clever syndicate monkey bound to its buyer, with an explosive implant (tg's allergy dies with the beacon); the monkey gun case opens only
/// to a paw.
/// </summary>
public class SyndicateG4cTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed = 61, int width = 13)
    {
        var r = new AudioRoom(width, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.In(() => Waiting.NoSleep(bob.equipOutfit(typeof(datum_outfit_job_assistant))));
        r.Step(Second);
        HoldingArea(r);
        return (r, feed, bob);
    }

    /// <summary>CentCom's holding area for pods in transit (tg's supplypod_temp_holding), which the toy room lacks: its corner turf.</summary>
    private static void HoldingArea(AudioRoom r) => r.In(() =>
    {
        var lane = GLOB.areas_by_type[typeof(area_centcom_central_command_areas_supplypod_supplypod_temp_holding)] as area
            ?? Dm.@new<area_centcom_central_command_areas_supplypod_supplypod_temp_holding>();
        set_turf_to_area(locate(1, 1, 1), lane);
    });

    private static T Held<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        T item = Dm.@new<T>(who.loc);
        Waiting.NoSleep(who.put_in_active_hand(item));
        return item;
    });

    [Fact]
    public void TheTypesAreLiveAndTheirEntriesSell()
    {
        foreach (Type type in new[]
        {
            typeof(obj_structure_closet_crate_secure), typeof(obj_structure_closet_crate_secure_syndicrate), typeof(obj_item_syndicrate_key),
            typeof(datum_mutation_race), typeof(datum_mutation_clever), typeof(obj_item_his_grace), typeof(datum_status_effect_his_grace), typeof(datum_status_effect_his_wrath), typeof(datum_status_effect_stun_absorption),
            typeof(obj_structure_closet_supplypod), typeof(obj_structure_closet_supplypod_podspawn), typeof(obj_effect_pod_landingzone),
            typeof(obj_effect_pod_landingzone_effect), typeof(obj_effect_supplypod_rubble), typeof(obj_effect_supplypod_smoke), typeof(obj_effect_engineglow),
            typeof(obj_item_antag_spawner_loadout_monkey_man), typeof(datum_antagonist_syndicate_monkey), typeof(datum_objective_syndicate_monkey),
            typeof(obj_item_storage_toolbox_guncase_monkeycase), typeof(datum_storage_toolbox_guncase_monkey), typeof(obj_item_switchblade_extended),
            typeof(obj_item_gun_ballistic_automatic_tommygun_chimpgun), typeof(obj_item_gun_ballistic_automatic_pistol_m1911_chimpgun),
            typeof(obj_item_ammo_box_magazine_uzim9mm),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
        var r = new AudioRoom(5, 5, seed: 3);
        foreach (Type entry in new[]
        {
            typeof(datum_uplink_item_role_restricted_his_grace), typeof(datum_uplink_item_role_restricted_monkey_agent),
            typeof(datum_uplink_item_role_restricted_monkey_supplies), typeof(datum_uplink_item_bundles_tc_surplus),
            typeof(datum_uplink_item_bundles_tc_surplus_united),
        })
            Assert.False(r.In(() => uplink_item_hidden(Dm.@new(entry) as datum_uplink_item)), entry.Name + " is hidden");
    }

    [Fact]
    public void HisGraceWakesGuardsAgainstStunsDevoursTheDownedAndFallsAsleep()
    {
        var (r, feed, bob) = Room(61);
        mob_living_carbon_human alice = r.Join(4, 4, "Alice");
        r.Step(Second);
        var grace = Held<obj_item_his_grace>(r, bob);
        r.In(() => Waiting.NoSleep(grace.attack_self(bob)));
        r.Step(2);
        Assert.Equal(1f, r.In(() => grace.awakened));
        Assert.Equal("His Grace", r.In(() => grace.name));
        Assert.True(feed.Any("begins to rattle"));
        Assert.Contains(r.In(() => Dm.each(grace.overlays).Select(o => Dm.str(o is IDmAppearance a ? a.icon_state : o)).ToList()), s => s == "single_latch_open");

        // held, it hungers and lays its effect on the wielder, who shrugs off a stun
        r.Step(2 * Second);
        Assert.True(r.In(() => grace.bloodthirst) > 1);
        Assert.NotNull(r.In(() => bob.has_status_effect(typeof(datum_status_effect_his_grace))));
        Assert.NotNull(r.In(() => bob.has_status_effect(typeof(datum_status_effect_stun_absorption))));
        r.In(() => Waiting.NoSleep(bob.Paralyze(10 * SECONDS)));
        r.Step(2);
        Assert.False(r.In(() => Dm.truthy(bob.IsParalyzed())), "His Grace did not absorb the stun");
        Assert.True(feed.Any(" Grace protects you from the stun!")); // tg applies the absorption before it sets His word: " Grace"

        // a downed victim is devoured: into the toolbox, its force grows
        r.In(() => Waiting.NoSleep(alice.adjust_oxy_loss(150)));
        r.Step(2);
        Assert.True(r.In(() => IS_UNCONSCIOUS_OR_CRIT(alice)));
        r.In(() => Waiting.NoSleep(grace.attack__proc(alice, bob)));
        r.Step(2);
        Assert.Same(grace, r.In(() => alice.loc));
        Assert.True(feed.Any("swings open and devours"));
        Assert.Equal(12f + HIS_GRACE_FORCE_BONUS, r.In(() => grace.force));

        // starved of time, it falls asleep: the latch snaps shut, the name and force come back
        r.In(() =>
        {
            grace.bloodthirst = 0;
            grace.process(1);
        });
        r.Step(2);
        Assert.Equal(0f, r.In(() => grace.awakened));
        Assert.Equal("artistic toolbox", r.In(() => grace.name));
        Assert.Equal(12f, r.In(() => grace.force));
        Assert.True(feed.Any("slowly stops rattling"));

        // the wielder's effect is gone a tick later (His Wrath follows, and ends at once: Bob still holds Him)
        r.Step(Second);
        Assert.Null(r.In(() => bob.has_status_effect(typeof(datum_status_effect_his_grace))));
        Assert.Null(r.In(() => bob.has_status_effect(typeof(datum_status_effect_stun_absorption))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ASupplyPodFallsLandsDropsItsCargoAndLeaves()
    {
        var (r, feed, bob) = Room(62);
        turf target = r.In(() => locate(8, 4, 1));
        obj_structure_closet_crate crate = r.In(() => Dm.@new<obj_structure_closet_crate>(locate(2, 1, 1)));
        obj_structure_closet_supplypod pod = r.In(() => Waiting.NoSleep(podspawn(Dm.list(
            Dm.kv("target", target),
            Dm.kv("style", typeof(datum_pod_style_syndicate)),
            Dm.kv("spawn", crate)))) as obj_structure_closet_supplypod);
        Assert.NotNull(pod);
        Assert.Equal("blood-red supply pod", r.In(() => pod.name));
        Assert.Same(pod, r.In(() => crate.loc));
        obj_effect_pod_landingzone zone = r.In(() => Dm.each<obj_effect_pod_landingzone>(target).Single());
        Assert.NotNull(r.In(() => zone.helper));

        // its door, masked by the fin shape (tg's SwapColor and blends), goes to the client as an icon recipe
        string recipe = r.In(() =>
        {
            object overlays = Waiting.NoSleep(pod.update_overlays());
            return Dm.each(overlays).OfType<icon>().Select(i => i.dm_resource()).FirstOrDefault();
        });
        Assert.NotNull(recipe);
        Assert.Contains(",w,", Uri.UnescapeDataString(recipe));

        // 3 s in transit, 0.4 s falling: it lands on the zone, then opens after 3 s and its cargo is out
        r.Step(4 * Second);
        Assert.Same(target, r.In(() => pod.loc));
        Assert.True(r.In(() => QDELETED(zone)));
        r.Step(4 * Second);
        Assert.Same(target, r.In(() => crate.loc));
        Assert.Equal(1f, r.In(() => pod.opened));

        // a bluespace pod sparks away after its leaving delay
        r.Step(4 * Second);
        Assert.True(r.In(() => QDELETED(pod)));
        Assert.Same(target, r.In(() => crate.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSurplusCrateComesByPodFullOfTelecrystalsWorth()
    {
        var (r, feed, bob) = Room(63);
        datum_antagonist_traitor traitor = r.In(() => Waiting.NoSleep(bob.mind.add_antag_datum(typeof(datum_antagonist_traitor))) as datum_antagonist_traitor);
        r.Step(4);
        var surplus = r.In(() => SStraitor.uplink_items_by_type[typeof(datum_uplink_item_bundles_tc_surplus)] as datum_uplink_item_bundles_tc_surplus);
        obj_structure_closet_crate crate = r.In(() => Waiting.NoSleep(surplus.spawn_item(null, bob, traitor.uplink_handler, null)) as obj_structure_closet_crate);
        Assert.NotNull(crate);
        Assert.IsType<obj_structure_closet_supplypod_podspawn>(r.In(() => crate.loc));
        Assert.True(r.In(() => Dm.length(crate.contents)) > 0);
        r.Step(10 * Second);
        Assert.Same(r.In(() => get_turf(bob)), r.In(() => crate.loc));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMonkeyAgentBeaconDropsAGhostAsABoundSyndicateMonkey()
    {
        var (r, feed, bob) = Room(64, width: 30);
        mob_living_carbon_human body = r.Join(20, 2, "Gina");
        client gina = body.client;
        r.In(() => body.ghostize(TRUE));
        r.Step(2);
        mob_dead_observer ghost = Assert.IsType<mob_dead_observer>(gina.mob);
        var beacon = Held<obj_item_antag_spawner_loadout_monkey_man>(r, bob);
        r.In(() => Waiting.Detach(beacon.attack_self(bob)));
        r.Step(2);
        Assert.True(feed.Any("and wait for confirmation"));
        atom_movable_screen_alert_poll_alert alert = r.In(() => Dm.each(ghost.alerts).Select(k => ghost.alerts[k]).OfType<atom_movable_screen_alert_poll_alert>().SingleOrDefault());
        Assert.NotNull(alert);
        Click(r, ghost, alert);

        // the poll's 10 seconds: Gina is a syndicate monkey in a pod over Bob's turf; the beacon is spent
        r.Step(11 * Second);
        mob_living_carbon_human monkey = r.In(() => gina.mob as mob_living_carbon_human);
        Assert.NotNull(monkey);
        Assert.True(r.In(() => QDELETED(beacon)));
        Assert.True(r.In(() => HAS_TRAIT(monkey, TRAIT_SIMIAN)));
        Assert.True(r.In(() => HAS_TRAIT(monkey, TRAIT_ADVANCEDTOOLUSER)), "crewlike_monkify's clever mutation");
        Assert.Contains(r.In(() => Dm.str(monkey.real_name)), r.In(() => Dm.each(GLOB.syndicate_monkey_names).Select(Dm.str).ToList()));
        var antag = r.In(() => monkey.mind.has_antag_datum(typeof(datum_antagonist_syndicate_monkey)) as datum_antagonist_syndicate_monkey);
        Assert.NotNull(antag);
        // tg adds the antagonist before the beacon binds the mind to its buyer: on_gain finds no master and forges no objective
        Assert.Null(r.In(() => antag.monky_master));
        Assert.Same(bob, r.In(() => monkey.mind.enslaved_to.resolve()));
        Assert.Contains(r.In(() => Dm.each(monkey.implants).ToList()), i => i is obj_item_implant_explosive);
        Assert.IsAssignableFrom<obj_structure_closet_supplypod>(r.In(() => monkey.loc));

        // the pod lands and opens: the monkey stands on Bob's turf
        r.Step(9 * Second);
        Assert.Same(r.In(() => get_turf(bob)), r.In(() => monkey.loc));

        // tg's allergy is registered by the beacon, which deletes itself right after and so drops its signals: a new species does nothing
        r.In(() => monkey.set_species(typeof(datum_species_human)));
        r.Step(2);
        Assert.False(feed.Any("starts swelling unhealthily in size"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheMonkeyGunCaseOpensOnlyToAPaw()
    {
        var (r, feed, bob) = Room(65);
        var box = Held<obj_item_storage_toolbox_guncase_monkeycase>(r, bob);
        Assert.True(r.In(() => Dm.length(box.contents)) >= 6);
        Assert.Contains(r.In(() => Dm.each(box.contents).ToList()), o => o is obj_item_storage_backpack_messenger);
        Assert.Contains(r.In(() => Dm.each(box.contents).ToList()), o => o is obj_item_food_grown_banana_bunch_monkeybomb);
        Assert.Contains(r.In(() => Dm.each(box.contents).ToList()), o => o is obj_item_gun_ballistic_automatic_mini_uzi_chimpgun
            or obj_item_gun_ballistic_automatic_tommygun_chimpgun or obj_item_gun_ballistic_automatic_pistol_m1911_chimpgun);

        Assert.False(r.In(() => Dm.truthy(box.monkey_check(bob))));
        Assert.True(feed.Any("angry chimpanzee screech"));
        Assert.Equal((float)STORAGE_SOFT_LOCKED, r.In(() => box.atom_storage.locked));

        r.In(() => ADD_TRAIT(bob, TRAIT_SIMIAN, "test"));
        Assert.True(r.In(() => Dm.truthy(box.monkey_check(bob))));
        Assert.True(feed.Any("hear a soft click"));
        Assert.Equal((float)STORAGE_NOT_LOCKED, r.In(() => box.atom_storage.locked));
        r.AssertNoRuntimes();
    }
}
