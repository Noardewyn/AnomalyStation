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
/// Syndicate stage N2b (D316) in a booted toy room: the operatives' kits. The duffel bags hold their ammunition and explosives; the large
/// crate opens only to a crowbar and spills its contents; an unusual hat glows; the outlaw's hat flies off when it catches a bullet and a
/// snake hides in the spurred boots; the weapons grade pinata sets up in four seconds, drops treats when struck hard and its debris and
/// loot when broken; chewed gum feeds its sugar and a tator tot dunks into a beaker.
/// </summary>
public class SyndicateN2bTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(11, 9, seed: seed);
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

    private static int Count<T>(AudioRoom r, atom where) where T : class => r.In(() => Dm.each<T>(where.contents).Count());

    [Fact]
    public void TheKitsAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(obj_structure_pinata), typeof(obj_structure_pinata_syndie), typeof(obj_structure_pinata_donk), typeof(obj_item_pinata_syndie),
            typeof(datum_component_pinata), typeof(datum_element_chewable), typeof(datum_element_dunkable), typeof(obj_item_food_lollipop_cyborg),
            typeof(obj_item_food_gumball), typeof(obj_item_food_bubblegum), typeof(obj_item_food_tatortot), typeof(obj_item_storage_box_donkpockets),
            typeof(obj_structure_closet_crate_large), typeof(obj_structure_closet_crate_large_hats), typeof(datum_component_unusual_effect),
            typeof(obj_item_clothing_head_cowboy), typeof(obj_item_clothing_shoes_cowboy), typeof(obj_item_storage_backpack_duffelbag_syndie_c4),
            typeof(obj_item_storage_backpack_duffelbag_syndie_x4), typeof(obj_item_storage_backpack_duffelbag_syndie_ammo_mauler),
            typeof(obj_item_storage_backpack_duffelbag_syndie_ammo_darkgygax), typeof(obj_item_storage_backpack_duffelbag_syndie_firestarter),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void TheDuffelBagsHoldTheirKits()
    {
        var (r, _, _) = Room(1701);
        var c4 = r.In(() => Dm.@new<obj_item_storage_backpack_duffelbag_syndie_c4>(locate(5, 4, 1)));
        var x4 = r.In(() => Dm.@new<obj_item_storage_backpack_duffelbag_syndie_x4>(locate(5, 4, 1)));
        var mauler = r.In(() => Dm.@new<obj_item_storage_backpack_duffelbag_syndie_ammo_mauler>(locate(5, 4, 1)));
        var gygax = r.In(() => Dm.@new<obj_item_storage_backpack_duffelbag_syndie_ammo_darkgygax>(locate(5, 4, 1)));
        r.Step(2);
        Assert.Equal(10, r.In(() => Dm.each<obj_item_grenade_c4>(c4.contents).Count(g => g.GetType() == typeof(obj_item_grenade_c4))));
        Assert.Equal(10, Count<obj_item_grenade_c4_x4>(r, x4));
        Assert.Equal(3, Count<obj_item_mecha_ammo_lmg>(r, mauler));
        Assert.Equal(3, Count<obj_item_mecha_ammo_missiles_srm>(r, mauler));
        Assert.Equal(1, Count<obj_item_storage_belt_utility_syndicate>(r, mauler));
        Assert.Equal(6, Count<obj_item_mecha_ammo_scattershot>(r, gygax));
    }

    [Fact]
    public void TheLargeCrateOpensOnlyToACrowbarAndSpillsItsContents()
    {
        var (r, feed, bob) = Room(1702);
        var crate = r.In(() => Dm.@new<obj_structure_closet_crate_large>(locate(4, 4, 1)));
        var hat = r.In(() => Dm.@new<obj_item_clothing_head_cowboy>(crate));
        r.Step(2);

        // a bare hand only tells you to fetch a crowbar
        r.In(() => Waiting.NoSleep(crate.attack_hand(bob)));
        Assert.True(feed.Any("You need a crowbar to pry this open!"));
        Assert.Same(crate, r.In(() => hat.loc));

        // the crowbar pries it open: the planks drop, the hat falls out, the crate is gone
        var crowbar = Hold<obj_item_crowbar>(r, bob);
        r.In(() => Waiting.NoSleep(crate.crowbar_act(bob, crowbar)));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(crate)));
        Assert.Same(r.In(() => locate(4, 4, 1)), r.In(() => hat.loc));
        Assert.True(r.In(() => Dm.each<obj_item_stack_sheet_mineral_wood>(locate(4, 4, 1).contents).Any()), "the crate's planks");
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/weapons/slashmiss.ogg");
    }

    [Fact]
    public void AnUnusualHatGlowsInItsOutline()
    {
        var (r, _, _) = Room(1703);
        var hat = r.In(() => Dm.@new<obj_item_clothing_head_cowboy>(locate(5, 4, 1)));
        r.In(() => Waiting.NoSleep(hat.AddComponent(typeof(datum_component_unusual_effect), Dm.named("color", "#FFEA0030"), Dm.named("include_particles", TRUE))));
        r.Step(Second);
        Assert.NotNull(r.In(() => hat.get_filter("unusual_effect")));
        r.In(() => qdel(hat.GetComponent(typeof(datum_component_unusual_effect))));
        Assert.Null(r.In(() => hat.get_filter("unusual_effect")));
    }

    [Fact]
    public void TheOutlawsHatFliesOffWhenItCatchesABullet()
    {
        var (r, feed, bob) = Room(1704);
        var hat = r.In(() => Dm.@new<obj_item_clothing_head_cowboy>(bob.loc));
        r.In(() => Waiting.NoSleep(bob.equip_to_slot(hat, ITEM_SLOT_HEAD)));
        Assert.Same(hat, r.In(() => bob.head));

        var bullet = r.In(() => Dm.@new<obj_projectile_bullet_c38>(locate(6, 4, 1)));
        r.In(() => Waiting.Detach(hat.on_intercepted_bullet(bob, bullet)));
        r.Step(Second);
        Assert.Null(r.In(() => bob.head));
        Assert.NotSame(bob, r.In(() => hat.loc));
        Assert.True(feed.Any("hat flying!"));
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.Contains("ric", StringComparison.Ordinal));
    }

    [Fact]
    public void ASnakeHidesInTheSpurredBootsAndCrawlsOut()
    {
        var (r, feed, bob) = Room(1705);
        var boots = r.In(() => Dm.@new<obj_item_clothing_shoes_cowboy>(locate(4, 4, 1)));
        var snake = r.In(() => Dm.@new<mob_living_basic_snake>(locate(4, 4, 1)));
        r.Step(2);

        r.In(() => Waiting.NoSleep(boots.mouse_drop_receive(snake, bob)));
        Assert.Same(boots, r.In(() => snake.loc));
        Assert.True(feed.Any("slithers into"));

        r.In(() => Waiting.Detach(boots.container_resist_act(snake)));
        r.Step(2 * Second);
        Assert.Same(r.In(() => locate(4, 4, 1)), r.In(() => snake.loc));
    }

    [Fact]
    public void TheWeaponsGradePinataSetsUpDropsTreatsAndBreaks()
    {
        var (r, feed, bob) = Room(1706);
        var kit = Hold<obj_item_pinata_syndie>(r, bob);
        r.In(() => Waiting.Detach(kit.attack_self(bob)));
        r.Step(Second);
        Assert.True(feed.Any("balloon: setting up pinata..."));
        r.Step(4 * Second);
        Assert.True(r.In(() => QDELETED(kit)), "the kit is used up");
        var pinata = r.In(() => Dm.each<obj_structure_pinata_syndie>(bob.loc.contents).Single());
        Assert.Equal("pinata_syndie_placed", r.In(() => pinata.icon_state));

        // a weak tap drops nothing
        int Loot() => r.In(() => Dm.each<turf>(Builtins.range(1, bob.loc)).Sum(t => Dm.each<obj_item>(t.contents).Count(i => i is obj_item_food || i is obj_item_grenade)));
        r.In(() => Waiting.NoSleep(pinata.take_damage(5, BRUTE, MELEE)));
        Assert.Equal(0, Loot());

        // hard hits drop treats and explosives around it, and past half its integrity it shows the damage
        for (int i = 0; i < 6; i++)
            r.In(() => Waiting.NoSleep(pinata.take_damage(30, BRUTE, MELEE)));
        Assert.True(Loot() > 0, "a hard hit drops loot");
        Assert.Equal("pinata_syndie_placed_damaged", r.In(() => pinata.icon_state));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/weapons/slash.ogg");

        // broken, it leaves its wrapping and its two last treats
        int before = Loot();
        r.In(() => Waiting.NoSleep(pinata.take_damage(1000, BRUTE, MELEE)));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(pinata)));
        Assert.True(Loot() >= before + 2, "the death drop");
        Assert.True(r.In(() => Dm.each<obj_effect_decal_cleanable_wrapping_pinata_syndie>(bob.loc.contents).Any()), "the debris");
    }

    [Fact]
    public void AToolboxSwungInCombatModeDamagesThePinata()
    {
        var (r, feed, bob) = Room(1709);
        var pinata = r.In(() => Dm.@new<obj_structure_pinata_syndie>(locate(3, 5, 1)));
        var toolbox = Hold<obj_item_storage_toolbox_mechanical>(r, bob);
        r.Step(2);
        float start = r.In(() => Dm.num(pinata.get_integrity()));
        // outside combat mode a full toolbox offers its tools (_toolbox.dm:51); in combat mode it swings
        r.In(() => bob.set_combat_mode(TRUE));
        Click(r, bob, pinata);
        r.Step(Second);
        Assert.True(r.In(() => Dm.num(pinata.get_integrity())) < start, "the swing lands: " + string.Join(" | ", feed.Chat.Select(c => c.Html)));
    }

    [Fact]
    public void ChewedGumFeedsItsSugar()
    {
        var (r, _, bob) = Room(1707);
        var gum = r.In(() => Dm.@new<obj_item_food_bubblegum>(bob.loc));
        r.In(() => Waiting.NoSleep(bob.equip_to_slot(gum, ITEM_SLOT_MASK)));
        Assert.Same(gum, r.In(() => bob.wear_mask));
        float before = r.In(() => Dm.num(((datum_reagents)gum.reagents).total_volume));
        r.Step(5 * Second);
        Assert.True(r.In(() => Dm.num(((datum_reagents)gum.reagents).total_volume)) < before, "the gum is chewed down");
    }

    [Fact]
    public void ATatorTotDunksIntoABeaker()
    {
        var (r, feed, bob) = Room(1708);
        var beaker = r.In(() => Dm.@new<obj_item_reagent_containers_cup_beaker>(locate(4, 4, 1)));
        r.In(() => Waiting.NoSleep(((datum_reagents)beaker.reagents).add_reagent(typeof(datum_reagent_water), 30)));
        var tot = Hold<obj_item_food_tatortot>(r, bob);
        float before = r.In(() => Dm.num(((datum_reagents)tot.reagents).total_volume));
        r.In(() => Waiting.NoSleep(tot.melee_attack_chain(bob, beaker)));
        Assert.True(feed.Any("You dunk"));
        Assert.Equal(before + 10, r.In(() => Dm.num(((datum_reagents)tot.reagents).total_volume)));
    }
}
