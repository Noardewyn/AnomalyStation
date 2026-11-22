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
/// Gaps package ST1 (D333's station order: section 9's decor) in a booted toy room with Bob as a player. The flora: a saw chops a
/// pine tree down into logs and a stump with its chopping and falling sounds, a burnt bush takes four times the damage and a wrecked
/// dead tree still leaves its share of logs; grass is picked by hand into grass and seeds and grows back; a pickaxe mines a rock pile
/// into sand; a shovel uproots a bush (turned on its side, loose) and replants it, and digs a stump out; the Christmas tree hands
/// each player one present (the endless one more). The potted plants: a random plant picks one of tg's looks at init and a plastic
/// one a plastic look; picked up it takes both hands and hides its carrier (tactical) with its own sounds; a bonsai trim with a
/// knife turns it into the next look; a dead plant comes back with a seed; Potty's light toggles with its blink.
/// </summary>
public class FloraST1Tests
{
    private static string Diag(Feed feed, AudioRoom r) => "chat: " + string.Join(" | ", feed.Chat.Select(c => c.Html)) + " runtimes: " + string.Join("\n", r.Room.RuntimeText);

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

    private static int Heard(AudioRoom r, string part) => r.Sounds.Heard.Count(s => (s.File ?? "").Contains(part, StringComparison.Ordinal));

    private static int Said(Feed feed, string text) => feed.Chat.Count(c => c.Html != null && c.Html.Contains(text, StringComparison.Ordinal));

    private static List<string> Overlays(AudioRoom r, atom a) => r.In(() => Dm.each(a.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)).ToList());

    [Fact]
    public void A_saw_chops_a_pine_tree_down_into_logs_and_a_stump()
    {
        var (r, feed, bob) = Room(7301);
        var tree = Place<obj_structure_flora_tree_pine>(r, 5, 4);
        r.In(() =>
        {
            Assert.False(InertSeam.IsInert(tree));
            Assert.Equal("pine_1", tree.icon_state);
            Assert.Equal(1f, tree.density);
            Assert.Equal(1f, tree.anchored);
        });
        var saw = Hold<obj_item_circular_saw>(r, bob);
        float speed = r.In(() => saw.toolspeed);

        Use(r, bob, tree);
        Assert.True(feed.Any("You start to chop"), Diag(feed, r));
        Assert.True(Heard(r, "treechop") >= 1, r.Sounds.Dump()); // a tree is wooden and herbal: both sounds
        Assert.True(Heard(r, "crunchybushwhack") >= 1, r.Sounds.Dump());
        r.Step((int)(6 * Second * speed) + 5);

        Assert.True(feed.Any("chops down"), Diag(feed, r));
        Assert.True(feed.Any("logs from the tree") || feed.Any("most of the wood from the tree"), Diag(feed, r));
        Assert.True(Heard(r, "meteorimpact") >= 1, r.Sounds.Dump()); // the fall, with gravity
        r.In(() =>
        {
            Assert.True(QDELETED(tree));
            turf spot = locate(5, 4, 1);
            Assert.InRange(Dm.each<obj_item_grown_log_tree>(spot.contents).Count(), 6, 10);
            var stump = Dm.each<obj_structure_flora_tree_stump>(spot.contents).Single();
            Assert.Equal("pine tree stump", stump.name);
            Assert.Equal("tree_stump", stump.icon_state);
            Assert.Equal(0f, stump.density);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Burning_hurts_flora_four_times_and_a_wrecked_tree_still_leaves_logs()
    {
        var (r, feed, bob) = Room(7302);
        var bush = Place<obj_structure_flora_bush>(r, 5, 4);
        r.In(() =>
        {
            Waiting.NoSleep(bush.take_damage(10, BURN, MELEE, 0));
            Assert.Equal(60f, Dm.num(bush.get_integrity())); // 100 - 4 x 10
            Waiting.NoSleep(bush.take_damage(10, BRUTE, MELEE, 0));
            Assert.Equal(50f, Dm.num(bush.get_integrity()));
        });

        // a dead tree smashed to pieces: its deconstruction harvests 60 % of its two to six logs, and its stump stays
        var dead = Place<obj_structure_flora_tree_dead>(r, 6, 4);
        r.In(() => Waiting.NoSleep(dead.take_damage(1000, BRUTE, MELEE, 0)));
        r.Step(Second);
        r.In(() =>
        {
            Assert.True(QDELETED(dead));
            turf spot = locate(6, 4, 1);
            Assert.InRange(Dm.each<obj_item_grown_log_tree>(spot.contents).Count(), 1, 4);
            Assert.Single(Dm.each<obj_structure_flora_tree_stump>(spot.contents));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Grass_is_picked_by_hand_and_grows_back()
    {
        var (r, feed, bob) = Room(7303);
        var grass = Place<obj_structure_flora_grass_jungle>(r, 5, 4);
        r.In(() => Assert.Equal(0f, grass.density));

        // tg's rand(0, 2) products: a pick that yields none leaves the grass unpicked (harvest() returns before marking it), so try again
        int tries = 0;
        do
        {
            Use(r, bob, grass);
            tries++;
            Assert.Equal(tries, Said(feed, "You start to harvest"));
            r.Step(6 * Second + 5);
        }
        while (r.In(() => grass.harvested) == 0f && tries < 8);
        Assert.True(Heard(r, "crunchybushwhack") >= 2, r.Sounds.Dump());
        Assert.True(feed.Any("You gather up a handful grass."), Diag(feed, r)); // one or two products: tg's high line
        r.In(() =>
        {
            Assert.False(QDELETED(grass)); // grass stays and regrows
            Assert.Equal(1f, grass.harvested);
            turf spot = locate(5, 4, 1);
            int grown = Dm.each<obj_item_food_grown_grass>(spot.contents).Count() + Dm.each<obj_item_seeds_grass>(spot.contents).Count();
            Assert.InRange(grown, 1, 2);
        });

        // picked: a second try does nothing
        Use(r, bob, grass);
        r.Step(6 * Second + 5);
        Assert.Equal(tries, Said(feed, "You start to harvest"));

        // it grows back within tg's eight to sixteen minutes
        r.Step(16 * 60 * Second + 10);
        r.In(() => Assert.Equal(0f, grass.harvested));
        Use(r, bob, grass);
        Assert.Equal(tries + 1, Said(feed, "You start to harvest"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_pickaxe_mines_a_rock_pile_into_sand()
    {
        var (r, feed, bob) = Room(7304);
        var rocks = Place<obj_structure_flora_rock_pile>(r, 5, 4);
        var pick = Hold<obj_item_pickaxe>(r, bob);
        float speed = r.In(() => pick.toolspeed);

        Use(r, bob, rocks);
        Assert.True(feed.Any("You start to mine"), Diag(feed, r));
        Assert.True(Heard(r, "rocktap") >= 1, r.Sounds.Dump());
        r.Step((int)(6 * Second * speed) + 5);

        Assert.True(feed.Any("You finish mining the pile of rocks."), Diag(feed, r));
        r.In(() =>
        {
            Assert.True(QDELETED(rocks));
            float sand = Dm.each<obj_item_stack_ore_glass_basalt>(locate(5, 4, 1).contents).Sum(s => s.amount ?? 0);
            Assert.InRange(sand, 5f, 10f);
        });

        // a hand does nothing to a rock
        var big = Place<obj_structure_flora_rock>(r, 4, 5);
        r.In(() => qdel(pick));
        Use(r, bob, big);
        r.Step(6 * Second + 5);
        Assert.False(r.In(() => QDELETED(big)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_shovel_uproots_and_replants_a_bush_and_digs_out_a_stump()
    {
        var (r, feed, bob) = Room(7305);
        var bush = Place<obj_structure_flora_bush>(r, 5, 4);
        Hold<obj_item_shovel>(r, bob);

        Use(r, bob, bush);
        Assert.True(feed.Any("You start to uproot"), Diag(feed, r));
        r.Step(6 * Second + 5);
        Assert.True(feed.Any("You uproot"), Diag(feed, r));
        r.In(() =>
        {
            Assert.Equal(1f, bush.uprooted);
            Assert.Equal(0f, bush.anchored);
            var turned = (matrix)bush.transform; // on its side, a quarter either way
            Assert.Equal(0f, turned.a, 3);
            Assert.Equal(1f, Math.Abs(turned.b), 3);
            Assert.Equal(90f, Math.Abs(bush.previous_rotation));
        });

        Use(r, bob, bush);
        Assert.True(feed.Any("You start to replant"), Diag(feed, r));
        r.Step(6 * Second + 5);
        Assert.True(feed.Any("You replant"), Diag(feed, r));
        r.In(() =>
        {
            Assert.Equal(0f, bush.uprooted);
            Assert.Equal(1f, bush.anchored);
            var upright = (matrix)bush.transform;
            Assert.Equal(1f, upright.a, 3);
            Assert.Equal(0f, upright.b, 3);
        });

        // a stump dug out with the shovel is gone
        var stump = Place<obj_structure_flora_tree_stump>(r, 4, 5);
        Use(r, bob, stump);
        r.Step(6 * Second + 5);
        Assert.True(feed.Any("You manage to remove"), Diag(feed, r));
        Assert.True(r.In(() => QDELETED(stump)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_christmas_tree_gives_each_player_one_present()
    {
        var (r, feed, bob) = Room(7306);

        // on Christmas day the holidays' spawners put up the tree with presents and the festivus pole (D332's live holidays)
        r.In(() =>
        {
            ((datum_config_entry)config.entries_by_type[typeof(datum_config_entry_flag_allow_holidays)]).config_entry_value = TRUE;
            var clock = DmWorldState.Current.RealtimeStart;
            DmWorldState.Current.RealtimeStart = (new DateTime(2026, 12, 25, 12, 0, 0, DateTimeKind.Utc) - new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds * 10d;
            GLOB.holidays = null;
            Assert.True(Dm.truthy(fill_holidays()), "no holiday on Christmas day");
            // the Master's tick drift reads the real time: the clock goes back, the filled holidays stay
            DmWorldState.Current.RealtimeStart = clock;
        });
        Place<obj_effect_spawner_xmastree>(r, 5, 4);
        Place<obj_effect_spawner_xmastree_rdrod>(r, 8, 6);
        var tree = r.In(() => Dm.each<obj_structure_flora_tree_pine_xmas_presents>(locate(5, 4, 1).contents).Single());
        r.In(() =>
        {
            Assert.False(InertSeam.IsInert(tree));
            Assert.Equal("pinepresents", tree.icon_state);
            Assert.EndsWith("Christmas tree", tree.name, StringComparison.Ordinal); // after tg's \improper mark
            Assert.Single(Dm.each<obj_structure_festivus>(locate(8, 6, 1).contents));
        });

        Use(r, bob, tree);
        Assert.True(feed.Any("you locate a gift with your name on it!"), Diag(feed, r));
        var gift = r.In(() => bob.get_active_held_item());
        Assert.IsType<obj_item_gift_anything>(gift);
        r.In(() => qdel(gift));

        Use(r, bob, tree);
        Assert.True(feed.Any("There are no presents with your name on."), Diag(feed, r));
        r.In(() => Assert.Null(bob.get_active_held_item()));

        // the endless one keeps giving
        var endless = Place<obj_structure_flora_tree_pine_xmas_presents_unlimited>(r, 4, 5);
        Use(r, bob, endless);
        r.In(() => qdel(bob.get_active_held_item()));
        Use(r, bob, endless);
        Assert.IsType<obj_item_gift_anything>(r.In(() => bob.get_active_held_item()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_random_potted_plant_picks_a_look_and_hides_its_carrier()
    {
        var (r, feed, bob) = Room(7307);
        var plant = Place<obj_item_kirbyplants_random>(r, 5, 4);
        var looks = Enumerable.Range(1, 24).Select(i => "plant-" + i.ToString("00", System.Globalization.CultureInfo.InvariantCulture)).Append("applebush").ToList();
        r.In(() =>
        {
            Assert.False(InertSeam.IsInert(plant));
            Assert.Equal("icons/obj/fluff/flora/plants.dmi", (plant.icon as DmResource)?.Path ?? Dm.str(plant.icon));
            Assert.Contains(plant.icon_state, looks);
            Assert.Equal(plant.icon_state, Dm.str(plant.base_icon_state));
            Assert.Equal("potted plant", plant.name);
        });

        // picked up with an empty hand: both hands, its sound, and Bob looks like a potted plant
        Use(r, bob, plant);
        r.In(() =>
        {
            Assert.Same(plant, bob.get_active_held_item());
            var twohanded = plant.GetComponent(typeof(datum_component_two_handed)) as datum_component_two_handed;
            Assert.NotNull(twohanded);
            Assert.Equal(1f, twohanded.wielded);
            Assert.True(HAS_TRAIT(bob, TRAIT_UNKNOWN_APPEARANCE)); // the tactical disguise
        });
        Assert.True(Heard(r, "potted_plant_pickup") >= 1, r.Sounds.Dump());

        r.In(() => Waiting.NoSleep(bob.dropItemToGround(plant)));
        r.Step(2);
        r.In(() => Assert.False(HAS_TRAIT(bob, TRAIT_UNKNOWN_APPEARANCE)));
        Assert.True(Heard(r, "potted_plant_drop") >= 1, r.Sounds.Dump());

        // a plastic one picks among tg's four plastic looks
        var plastic = Place<obj_item_kirbyplants_random_fullysynthetic>(r, 6, 4);
        r.In(() =>
        {
            Assert.Contains(plastic.icon_state, new[] { "plant-26", "plant-27", "plant-28", "plant-29" });
            Assert.Equal("plastic potted plant", plastic.name);
            Assert.Equal(0f, plastic.trimmable);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_bonsai_trim_turns_a_plant_into_the_next_look()
    {
        var (r, feed, bob) = Room(7308);
        var plant = Place<obj_item_kirbyplants_organic_plant3>(r, 5, 4);
        r.In(() => Assert.Equal("plant-03", plant.icon_state));
        Hold<obj_item_knife_kitchen>(r, bob);

        // without the skill nothing is trimmed
        Use(r, bob, plant);
        r.Step(3 * Second + 5);
        Assert.False(feed.Any("You start trimming"), Diag(feed, r));
        r.In(() => Assert.Equal("plant-03", plant.icon_state));

        r.In(() => ADD_TRAIT(bob, TRAIT_BONSAI, "test"));
        Use(r, bob, plant);
        Assert.True(feed.Any("You start trimming"), Diag(feed, r));
        r.Step(3 * Second + 5);
        Assert.True(feed.Any("You finish trimming"), Diag(feed, r));
        r.In(() =>
        {
            Assert.Equal("plant-04", Dm.str(plant.base_icon_state));
            Assert.Equal("plant-04", plant.icon_state);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_dead_plant_comes_back_with_a_seed()
    {
        var (r, feed, bob) = Room(7309);
        var plant = Place<obj_item_kirbyplants_random_dead>(r, 5, 4);
        r.In(() =>
        {
            Assert.Equal("plant-25", plant.icon_state);
            Assert.Equal("dead potted plant", plant.name);
            Assert.Contains("It doesn't look very healthy...", plant.desc, StringComparison.Ordinal);
        });
        var seeds = Hold<obj_item_seeds_grass>(r, bob);

        Use(r, bob, plant);
        Assert.True(feed.Any("You start planting a new seed into the pot."), Diag(feed, r));
        r.Step(3 * Second + 5);
        r.In(() =>
        {
            Assert.True(QDELETED(seeds));
            Assert.Equal(0f, plant.dead);
            Assert.NotEqual("plant-25", plant.icon_state);
            Assert.Equal(plant.icon_state, Dm.str(plant.base_icon_state));
            Assert.Equal("potted plant", plant.name);
            Assert.StartsWith("A gift from the botanical staff", plant.desc, StringComparison.Ordinal);
            Assert.DoesNotContain("healthy", plant.desc, StringComparison.Ordinal);
        });

        // the RD's own keeps its name dead or alive
        var rds = Place<obj_item_kirbyplants_random_dead_research_director>(r, 6, 4);
        r.In(() => Assert.Equal("RD's potted plant", rds.name));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Pottys_light_toggles_with_its_blink()
    {
        var (r, feed, bob) = Room(7310);
        var potty = Place<obj_item_kirbyplants_potty>(r, 5, 4);
        r.In(() =>
        {
            Assert.Equal("potty", potty.icon_state);
            Assert.Equal(1f, potty.light_on);
        });
        Assert.Contains("potty_light", Overlays(r, potty));

        r.In(() => Waiting.NoSleep(potty.attack_self(bob)));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal(0f, potty.light_enabled);
            Assert.Equal(0f, potty.light_on);
        });
        Assert.DoesNotContain("potty_light", Overlays(r, potty));

        r.In(() => Waiting.NoSleep(potty.attack_self(bob)));
        r.Step(2);
        r.In(() => Assert.Equal(1f, potty.light_on));
        Assert.Contains("potty_light", Overlays(r, potty));
        r.AssertNoRuntimes();
    }
}
