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
/// Syndicate stage N3a (D316) in a booted toy room: the clown operatives. A clown operative is naive, clumsy, has a comedy liver and wears
/// the clown kit with the ultra-honk pin on the toy gun; the leader carries a clown title; the bananium sword slips when lit and honks
/// sadly against another; the bananium shield comes back; the bombanana's peel hides a live minibomb; the tear-stache grenade glues
/// moustaches on the unmasked; the mk-honk combat shoes leave peels behind; super lube slicks the floor and the items it touches.
/// </summary>
public class SyndicateN3aTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(15, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        foreach (obj_item held in Dm.each<obj_item>(who.held_items).ToList())
            Waiting.NoSleep(who.dropItemToGround(held));
        T item = Dm.@new<T>(who.loc);
        Waiting.NoSleep(who.put_in_active_hand(item));
        return item;
    });

    /// The toy room has no nukie base map: the team's lazy load of it is the one runtime allowed (as SyndicateN1cTests).
    private static void OnlyTheBaseRuntime(AudioRoom r)
    {
        bool allowed(string text) => text.Contains("nukie_base.dmm") || text.Contains("_lazy_load_template");
        Assert.True(r.Room.RuntimeText.All(allowed), string.Join("\n---\n", r.Room.RuntimeText.Where(t => !allowed(t)).Select(t => t.Length > 900 ? t[..900] : t)));
    }

    [Fact]
    public void TheClownOperativesAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(datum_antagonist_nukeop_clownop), typeof(datum_antagonist_nukeop_leader_clownop), typeof(datum_outfit_syndicate_clownop),
            typeof(datum_dynamic_ruleset_roundstart_nukies_clown), typeof(datum_dynamic_ruleset_midround_from_ghosts_nukies_clown),
            typeof(obj_item_melee_energy_sword_bananium), typeof(obj_item_shield_energy_bananium), typeof(obj_item_food_grown_banana_bombanana),
            typeof(obj_item_grown_bananapeel_bombanana), typeof(obj_item_grenade_chem_grenade_teargas_moustache),
            typeof(obj_item_clothing_mask_fakemoustache_sticky), typeof(obj_item_clothing_shoes_clown_shoes_banana_shoes_combat),
            typeof(obj_item_clothing_shoes_clown_shoes_combat), typeof(obj_item_storage_backpack_duffelbag_clown_syndie),
            typeof(datum_reagent_lube), typeof(datum_component_slippery_item), typeof(obj_item_storage_box_syndie_kit_induction_kit_clown),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void AClownOperativeIsNaiveClumsyWithAComedyLiverInTheClownKit()
    {
        var (r, _, bob) = Room(1901);
        r.In(() =>
        {
            var op = Dm.@new<datum_antagonist_nukeop_clownop>();
            op.send_to_spawnpoint = FALSE;
            Waiting.NoSleep(bob.mind.add_antag_datum(op));
        });
        r.Step(Second);
        Assert.True(r.In(() => HAS_TRAIT(bob.mind, TRAIT_NAIVE)));
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_CLUMSY)), "the clown kit's clumsiness");
        Assert.True(r.In(() => HAS_TRAIT(bob.get_organ_slot(ORGAN_SLOT_LIVER) as atom, TRAIT_COMEDY_METABOLISM)));
        Assert.IsType<obj_item_clothing_shoes_clown_shoes_combat>(r.In(() => bob.shoes));
        Assert.IsType<obj_item_clothing_mask_gas_clown_hat_clownops>(r.In(() => bob.wear_mask));
        var gun = r.In(() => Dm.locate<obj_item_gun_ballistic_automatic_pistol_toy_riot_clandestine>(bob.get_all_contents() as DmList));
        Assert.NotNull(gun);
        Assert.IsType<obj_item_firing_pin_clown_ultra>(r.In(() => gun.pin));
        Assert.Null(r.In(() => Dm.locate<obj_item_clothing_mask_gas_syndicate>(bob.get_all_contents() as DmList)));
        OnlyTheBaseRuntime(r);
    }

    [Fact]
    public void TheClownLeaderCarriesAClownTitle()
    {
        var (r, _, bob) = Room(1902);
        r.In(() =>
        {
            var op = Dm.@new<datum_antagonist_nukeop_leader_clownop>();
            op.send_to_spawnpoint = FALSE;
            op.spawn_ship = FALSE;
            Waiting.NoSleep(bob.mind.add_antag_datum(op));
        });
        r.Step(Second);
        string name = r.In(() => Dm.str(bob.real_name));
        Assert.True(new[] { "Head Honker", "Slipmaster", "Clown King", "Honkbearer" }.Any(name.StartsWith), name);
        OnlyTheBaseRuntime(r);
    }

    [Fact]
    public void TheBananiumSwordSlipsWhenLitAndHonksSadlyAgainstAnother()
    {
        var (r, feed, bob) = Room(1903);
        var sword = Hold<obj_item_melee_energy_sword_bananium>(r, bob);
        Assert.NotNull(r.In(() => sword.GetComponent(typeof(datum_component_slippery))));
        Assert.False(r.In(() => Dm.truthy(sword.is_active())));
        r.In(() => Waiting.NoSleep(sword.attack_self(bob)));
        Assert.True(r.In(() => Dm.truthy(sword.is_active())));
        Assert.Equal("sound/misc/slip.ogg", r.In(() => Dm.str(sword.hitsound)));

        var other = r.In(() => Dm.@new<obj_item_melee_energy_sword_bananium>(bob.loc));
        r.In(() => Waiting.NoSleep(sword.item_interaction(bob, other)));
        Assert.True(feed.Any("Sadly, they do not seem to fit!"));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/misc/sadtrombone.ogg");
        Assert.Equal(ITEM_INTERACT_BLOCKING, r.In(() => Waiting.NoSleep(sword.item_interaction(bob, other))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBananiumShieldComesBackWhenLit()
    {
        var (r, _, bob) = Room(1904);
        var shield = Hold<obj_item_shield_energy_bananium>(r, bob);
        r.In(() => Waiting.NoSleep(shield.attack_self(bob)));
        Assert.True(r.In(() => HAS_TRAIT(shield, TRAIT_TRANSFORM_ACTIVE)));
        Assert.NotNull(r.In(() => shield.GetComponent(typeof(datum_component_boomerang))));
        Assert.NotNull(r.In(() => shield.GetComponent(typeof(datum_component_slippery))));
        r.In(() => Waiting.NoSleep(shield.attack_self(bob)));
        Assert.Null(r.In(() => shield.GetComponent(typeof(datum_component_boomerang))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBombananaPeelHidesALiveMinibombAndItsEaterIsLogged()
    {
        var (r, _, bob) = Room(1905);
        var peel = r.In(() => Dm.@new<obj_item_grown_bananapeel_bombanana>(locate(10, 4, 1)));
        Assert.NotNull(r.In(() => peel.bomb));
        Assert.Equal("bombanana peel", r.In(() => Dm.str(peel.bomb.name)));
        Assert.True(r.In(() => Dm.truthy(peel.bomb.active)), "the peel's minibomb is armed");
        Assert.NotNull(r.In(() => peel.GetComponent(typeof(datum_component_slippery))));

        var banana = r.In(() => Dm.@new<obj_item_food_grown_banana_bombanana>(bob.loc));
        r.In(() => banana._SendSignal(COMSIG_FOOD_CONSUMED, Dm.list(banana, bob, bob)));
        Assert.Contains(r.In(() => Dm.each(GLOB.bombers).Select(Dm.str).ToList()), m => m.Contains("has eaten a bombanana!"));
    }

    [Fact]
    public void TheTearStacheGrenadeGluesMoustachesOnTheUnmasked()
    {
        var (r, _, bob) = Room(1906);
        mob_living_carbon_human carol = r.Dummy(6, 4);
        mob_living_carbon_human clown = r.Dummy(7, 4);
        r.In(() => Waiting.NoSleep(clown.equip_to_slot_or_del(Dm.@new<obj_item_clothing_mask_gas_clown_hat>(), ITEM_SLOT_MASK)));
        var grenade = r.In(() => Dm.@new<obj_item_grenade_chem_grenade_teargas_moustache>(locate(5, 4, 1)));
        r.In(() => Waiting.Detach(grenade.detonate()));
        r.Step(Second);
        var stash = r.In(() => carol.wear_mask as obj_item_clothing_mask_fakemoustache_sticky);
        Assert.NotNull(stash);
        Assert.True(r.In(() => HAS_TRAIT(stash, TRAIT_NODROP)));
        Assert.IsType<obj_item_clothing_mask_gas_clown_hat>(r.In(() => clown.wear_mask));
        r.Step(61 * Second);
        Assert.False(r.In(() => HAS_TRAIT(stash, TRAIT_NODROP)), "it comes unstuck after a minute");
    }

    [Fact]
    public void TheMkHonkCombatShoesLeavePeelsBehind()
    {
        var (r, feed, bob) = Room(1907);
        var shoes = r.In(() => Dm.@new<obj_item_clothing_shoes_clown_shoes_banana_shoes_combat>(bob.loc));
        r.In(() => Waiting.NoSleep(bob.equip_to_slot_or_del(shoes, ITEM_SLOT_FEET)));
        Assert.Same(shoes, r.In(() => bob.shoes));
        Assert.True(r.In(() => Waiting.NoSleep(shoes.bananium.get_material_amount(typeof(datum_material_bananium)))) > 0, "charged");
        r.In(() => Waiting.NoSleep(shoes.attack_self(bob)));
        Assert.True(feed.Any("You activate the prototype shoes."));
        r.In(() => Waiting.NoSleep(bob.Move(locate(4, 4, 1), EAST)));
        r.Step(2);
        Assert.True(r.In(() => Dm.each<obj_item_grown_bananapeel_specialpeel>(locate(3, 4, 1).contents).Any()), "a peel on the tile behind");
        Assert.Equal("clown_prototype_on", r.In(() => Dm.str(shoes.icon_state)));
    }

    [Fact]
    public void SuperLubeSlicksTheFloorAndTheItemsItTouches()
    {
        var (r, _, bob) = Room(1908);
        turf_open floor = r.In(() => locate(8, 4, 1) as turf_open);
        var lube = r.In(() => Dm.@new<datum_reagent_lube_superlube>());
        r.In(() => Waiting.NoSleep(lube.expose_turf(floor, 10)));
        var wet = r.In(() => floor.GetComponent(typeof(datum_component_wet_floor)) as datum_component_wet_floor);
        Assert.NotNull(wet);
        Assert.True(r.In(() => Dm.truthy(Dm.bitand(wet.highest_strength, TURF_WET_SUPERLUBE))), "super lube on the floor");

        var wrench = r.In(() => Dm.@new<obj_item_wrench>(bob.loc));
        r.In(() => Waiting.NoSleep(lube.expose_obj(wrench, 10)));
        var slick = r.In(() => wrench.GetComponent(typeof(datum_component_slippery_item)) as datum_component_slippery_item);
        Assert.NotNull(slick);
        Assert.Equal(80f, r.In(() => slick.fall_chance));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheClownCostumeBagHoldsTheKit()
    {
        var (r, _, _) = Room(1909);
        var bag = r.In(() => Dm.@new<obj_item_storage_backpack_duffelbag_clown_syndie>(locate(5, 4, 1)));
        r.Step(2);
        var contents = r.In(() => Dm.each(bag.contents).Select(c => c.GetType()).ToList());
        Assert.Contains(typeof(obj_item_clothing_under_rank_civilian_clown), contents);
        Assert.Contains(typeof(obj_item_bikehorn), contents);
        Assert.Contains(typeof(obj_item_implanter_sad_trombone), contents);
        r.AssertNoRuntimes();
    }
}
