using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.M4bAudio;
using Xunit;
using static Anomaly.Core.Tests.Gaps.FaunaTestSteps;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The gutlunchers: the milk-giving ones fill their udders from the ore they eat and are milked, the warriors roll their
/// stats and obey, a pair breeds a grub that takes after its father, the grub grows into one of the two, and the food
/// trough holds the ore they eat from.
/// </summary>
public class GutlunchTests
{
    public static IEnumerable<object[]> Lunchers() => new[]
    {
        typeof(mob_living_basic_mining_gutlunch), typeof(mob_living_basic_mining_gutlunch_milk), typeof(mob_living_basic_mining_gutlunch_warrior),
        typeof(mob_living_basic_mining_gutlunch_grub), typeof(obj_structure_ore_container_food_trough_gutlunch_trough),
    }.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(Lunchers))]
    public void A_gutluncher_thing_is_made_lives_and_goes_away_cleanly(Type path) =>
        Lifecycle(new AudioRoom(13, 9, seed: 13211), path);

    [Theory]
    [InlineData(typeof(mob_living_basic_mining_gutlunch_warrior))]
    [InlineData(typeof(mob_living_basic_mining_gutlunch_milk))]
    public void A_gutluncher_lives_beside_a_player_without_an_error(Type path)
    {
        var r = new AudioRoom(15, 11, seed: 13220);
        mob_living_carbon_human bob = r.Join(11, 5, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_GODMODE, "test"));
        r.In(() => Dm.@new(path, locate(3, 5, 1)));
        RunFor(r, 60 * Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_gutlunchers_are_counted_while_they_live()
    {
        var r = new AudioRoom(13, 9, seed: 13212);
        float before = r.In(() => GLOB.gutlunch_count);
        var lunch = r.In(() => Dm.@new<mob_living_basic_mining_gutlunch_milk>(locate(4, 4, 1)));
        Assert.Equal(before + 1, r.In(() => GLOB.gutlunch_count));
        r.In(() => qdel(lunch));
        Assert.Equal(before, r.In(() => GLOB.gutlunch_count));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_milk_gutluncher_fed_gold_fills_its_udder_with_cream_and_is_milked()
    {
        var r = new AudioRoom(13, 9, seed: 13213);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var lunch = r.In(() => Dm.@new<mob_living_basic_mining_gutlunch_milk>(locate(4, 4, 1)));
        r.Step(2);
        var udder = r.In(() => (lunch.GetComponent(typeof(datum_component_udder)) as datum_component_udder).udder);
        Assert.IsType<obj_item_udder_gutlunch>(udder);
        r.In(() => Waiting.NoSleep(Dm.@new<obj_item_stack_ore_gold>(locate(4, 4, 1), 1, FALSE).forceMove(udder)));
        bool creamed = false;
        for (int i = 0; i < 40 && !creamed; i++)
        {
            r.In(() => Waiting.NoSleep(udder.generate()));
            creamed = r.In(() => Dm.truthy((udder.reagents as datum_reagents).has_reagent(typeof(datum_reagent_consumable_cream))));
        }
        Assert.True(creamed, "the gold made cream in the udder");
        var bucket = r.In(() => Dm.@new<obj_item_reagent_containers_cup_beaker>(locate(5, 4, 1)));
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(bucket)));
        r.In(() => Waiting.NoSleep(udder.milk(bucket, bob)));
        Assert.True(r.In(() => Dm.num((bucket.reagents as datum_reagents).total_volume)) > 0, "the beaker caught the milk");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_milk_gutlunchers_overlays_show_its_full_udder()
    {
        var r = new AudioRoom(13, 9, seed: 13214);
        var lunch = r.In(() => Dm.@new<mob_living_basic_mining_gutlunch_milk>(locate(4, 4, 1)));
        r.Step(2);
        Assert.Equal("gl_full", r.In(() => lunch.full_udder.icon_state));
        var overlays = r.In(() => Waiting.NoSleep(lunch.update_overlays()) as DmList);
        Assert.Contains(r.In(() => lunch.full_udder), r.In(() => Dm.each<object>(overlays).ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_gutlunch_warrior_rolls_its_stats_inside_tgs_bounds()
    {
        var r = new AudioRoom(13, 9, seed: 13215);
        for (int i = 0; i < 6; i++)
        {
            var warrior = r.In(() => Dm.@new<mob_living_basic_mining_gutlunch_warrior>(locate(4, 4, 1)));
            float lower = r.In(() => warrior.melee_damage_lower);
            Assert.InRange(lower, 8, 15);
            Assert.Equal(lower + 3, r.In(() => warrior.melee_damage_upper));
            Assert.InRange(r.In(() => warrior.speed), 1, 5);
            Assert.InRange(r.In(() => Dm.num(warrior.maxHealth)), 70, 100);
            Assert.NotNull(r.In(() => warrior.GetComponent(typeof(datum_component_obeys_commands))));
            r.In(() => qdel(warrior));
        }
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_pair_of_gutlunchers_breeds_a_grub_that_takes_after_its_father()
    {
        var r = new AudioRoom(13, 9, seed: 13216);
        var mother = r.In(() => Dm.@new<mob_living_basic_mining_gutlunch_milk>(locate(4, 4, 1)));
        var father = r.In(() => Dm.@new<mob_living_basic_mining_gutlunch_warrior>(locate(5, 4, 1)));
        r.Step(2);
        r.In(() =>
        {
            mother.gender = FEMALE;
            father.gender = MALE;
            mother.combat_mode = FALSE;
        });
        var breed = r.In(() => mother.GetComponent(typeof(datum_component_breed)) as datum_component_breed);
        r.In(() => breed.breed_with_partner(mother, father));
        var grub = r.In(() => Dm.each<atom>(range(0, mother)).OfType<mob_living_basic_mining_gutlunch_grub>().FirstOrDefault());
        Assert.NotNull(grub);
        RunFor(r, 2);
        Assert.Equal(r.In(() => father.melee_damage_lower), r.In(() => Dm.num(grub.inherited_stats.attack)));
        Assert.Equal(r.In(() => Dm.num(father.maxHealth)), r.In(() => Dm.num(grub.inherited_stats.health)));
        Assert.False(r.In(() => Dm.truthy(breed.ready_to_breed)), "the mother rests before breeding again");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_gutlunch_grub_grows_into_a_warrior_or_a_milker()
    {
        var r = new AudioRoom(13, 9, seed: 13217);
        var grub = r.In(() => Dm.@new<mob_living_basic_mining_gutlunch_grub>(locate(4, 4, 1)));
        r.Step(2);
        var growth = r.In(() => grub.GetComponent(typeof(datum_component_growth_and_differentiation)) as datum_component_growth_and_differentiation);
        // the growth waits for the round to start (growth_and_differentiation.dm:64-67): start it as the round would
        r.In(() =>
        {
            growth.comp_on_round_start();
            growth.percent_grown = 99.9f;
        });
        Assert.True(RunUntil(r, 30 * Second, () => QDELETED(grub)), "the grub grew");
        Assert.True(r.In(() => Dm.each<atom>(range(0, locate(4, 4, 1))).Any(a => a is mob_living_basic_mining_gutlunch_warrior || a is mob_living_basic_mining_gutlunch_milk)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_gutluncher_eats_the_ore_in_its_trough()
    {
        var r = new AudioRoom(13, 9, seed: 13218);
        var trough = r.In(() => Dm.@new<obj_structure_ore_container_food_trough_gutlunch_trough>(locate(5, 4, 1)));
        var ore = r.In(() => Dm.@new<obj_item_stack_ore_iron>(locate(5, 4, 1), 5, FALSE));
        r.In(() => Waiting.NoSleep(ore.forceMove(trough)));
        Assert.True(r.In(() => Dm.truthy(trough.list_of_materials[typeof(obj_item_stack_ore_iron)])), "the trough shows the iron");
        var overlays = r.In(() => Waiting.NoSleep(trough.update_overlays()) as DmList);
        Assert.True(r.In(() => Dm.each<object>(overlays).OfType<image>().Any(i => Dm.eq(i.icon_state, Dm.initial(typeof(obj_item_stack_ore_iron), (atom a) => a.icon_state)))));
        var lunch = r.In(() => Dm.@new<mob_living_basic_mining_gutlunch_milk>(locate(4, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(lunch.early_melee_attack(trough, null, TRUE)));
        Assert.True(r.In(() => QDELETED(ore) || ore.loc != trough || Dm.num(ore.amount) < 5), "the gutluncher ate from the trough");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_broken_trough_leaves_its_wood()
    {
        var r = new AudioRoom(13, 9, seed: 13219);
        var trough = r.In(() => Dm.@new<obj_structure_ore_container_food_trough_gutlunch_trough>(locate(5, 4, 1)));
        r.In(() => Waiting.NoSleep(trough.deconstruct(TRUE)));
        Assert.True(OnFloor<obj_item_stack_sheet_mineral_wood>(r, 0, r.In(() => locate(5, 4, 1))));
        r.AssertNoRuntimes();
    }
}
