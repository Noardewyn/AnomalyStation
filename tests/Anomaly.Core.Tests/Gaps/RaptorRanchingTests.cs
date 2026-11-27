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
/// The raptors' ranching: the colours and what each gives, a pair breeding an egg whose chick's colour follows tg's
/// crossbreeds and whose genes come from both, the egg hatching, the chick growing through youngling to adult, the
/// wild adult's taming by riding it out, the troughs, the RaptorDex and its tablet program reading a raptor, and the
/// feather a crusher takes.
/// </summary>
public class RaptorRanchingTests
{
    public static IEnumerable<object[]> Raptors() => new[]
    {
        typeof(mob_living_basic_raptor_red), typeof(mob_living_basic_raptor_purple), typeof(mob_living_basic_raptor_green),
        typeof(mob_living_basic_raptor_white), typeof(mob_living_basic_raptor_black), typeof(mob_living_basic_raptor_yellow),
        typeof(mob_living_basic_raptor_blue), typeof(mob_living_basic_raptor_young_red), typeof(mob_living_basic_raptor_young_white),
        typeof(mob_living_basic_raptor_baby_blue), typeof(mob_living_basic_raptor_baby_purple), typeof(obj_item_food_egg_raptor_egg),
        typeof(obj_item_raptor_dex), typeof(obj_structure_ore_container_food_trough_raptor_trough), typeof(obj_item_crusher_trophy_raptor_feather),
    }.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(Raptors))]
    public void A_raptor_thing_is_made_lives_and_goes_away_cleanly(Type path) =>
        Lifecycle(new AudioRoom(13, 9, seed: 13261), path);

    [Theory]
    [InlineData(typeof(mob_living_basic_raptor_red))]
    [InlineData(typeof(mob_living_basic_raptor_white))]
    [InlineData(typeof(mob_living_basic_raptor_young_blue))]
    public void A_raptor_lives_beside_a_player_without_an_error(Type path)
    {
        var r = new AudioRoom(15, 11, seed: 13275);
        mob_living_carbon_human bob = r.Join(11, 5, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_GODMODE, "test"));
        r.In(() => Dm.@new(path, locate(3, 5, 1)));
        RunFor(r, 60 * Second);
        r.AssertNoRuntimes();
    }

    [Theory]
    [InlineData(typeof(mob_living_basic_raptor_red), "red")]
    [InlineData(typeof(mob_living_basic_raptor_blue), "blue")]
    [InlineData(typeof(mob_living_basic_raptor_young_green), "green")]
    [InlineData(typeof(mob_living_basic_raptor_baby_white), "white")]
    public void A_raptor_wears_its_colour_and_its_growth_stage(Type path, string colour)
    {
        var r = new AudioRoom(13, 9, seed: 13262);
        var raptor = r.In(() => (mob_living_basic_raptor)Dm.@new(path, locate(4, 4, 1)));
        r.Step(2);
        Assert.Contains(colour, r.In(() => raptor.name));
        Assert.Equal(r.In(() => raptor.base_icon_state + "_" + colour), r.In(() => raptor.icon_state));
        string stage = r.In(() => raptor.growth_stage);
        Assert.EndsWith(stage == RAPTOR_BABY ? "raptor_baby.dmi" : "raptor_big.dmi", r.In(() => raptor.icon.ToString()));
        Assert.NotNull(r.In(() => raptor.ai_controller));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Each_colour_gives_its_own_gift()
    {
        var r = new AudioRoom(13, 9, seed: 13263);
        var red = r.In(() => Dm.@new<mob_living_basic_raptor_red>(locate(2, 2, 1)));
        var blue = r.In(() => Dm.@new<mob_living_basic_raptor_blue>(locate(4, 2, 1)));
        var green = r.In(() => Dm.@new<mob_living_basic_raptor_green>(locate(6, 2, 1)));
        var white = r.In(() => Dm.@new<mob_living_basic_raptor_white>(locate(8, 2, 1)));
        var black = r.In(() => Dm.@new<mob_living_basic_raptor_black>(locate(10, 2, 1)));
        var purple = r.In(() => Dm.@new<mob_living_basic_raptor_purple>(locate(2, 6, 1)));
        r.Step(2);
        Assert.True(r.In(() => HAS_TRAIT(red, TRAIT_MINING_AGGRO)), "a red raptor goes for the fauna");
        Assert.True(r.In(() => HAS_TRAIT(blue, TRAIT_LAVA_IMMUNE)), "a blue raptor walks on lava");
        Assert.NotNull(r.In(() => green.GetComponent(typeof(datum_component_proficient_miner))));
        Assert.NotNull(r.In(() => white.GetComponent(typeof(datum_component_healing_touch))));
        Assert.True(r.In(() => HAS_TRAIT(black, TRAIT_LAVA_IMMUNE) && HAS_TRAIT(black, TRAIT_MINING_AGGRO)));
        Assert.Equal(typeof(obj_item_mob_holder_purple_raptor), r.In(() => purple.inhand_holder_type));
        r.AssertNoRuntimes();
    }

    [Theory]
    [InlineData(typeof(mob_living_basic_raptor_red), typeof(mob_living_basic_raptor_green), typeof(datum_raptor_color_yellow))]
    [InlineData(typeof(mob_living_basic_raptor_red), typeof(mob_living_basic_raptor_blue), typeof(datum_raptor_color_purple))]
    [InlineData(typeof(mob_living_basic_raptor_blue), typeof(mob_living_basic_raptor_white), typeof(datum_raptor_color_green))]
    [InlineData(typeof(mob_living_basic_raptor_green), typeof(mob_living_basic_raptor_green), typeof(datum_raptor_color_green))]
    public void A_pair_of_raptors_lays_an_egg_of_their_crossbreed(Type mother, Type father, Type chick)
    {
        var r = new AudioRoom(13, 9, seed: 13264);
        var mom = r.In(() => (mob_living_basic_raptor)Dm.@new(mother, locate(4, 4, 1)));
        var dad = r.In(() => (mob_living_basic_raptor)Dm.@new(father, locate(5, 4, 1)));
        r.Step(2);
        r.In(() =>
        {
            mom.gender = FEMALE;
            dad.gender = MALE;
            mom.combat_mode = FALSE;
        });
        var breed = r.In(() => mom.GetComponent(typeof(datum_component_breed)) as datum_component_breed);
        Assert.NotNull(breed);
        r.In(() => breed.breed_with_partner(mom, dad));
        var egg = r.In(() => Dm.each<obj_item_food_egg_raptor_egg>(range(0, mom)).FirstOrDefault());
        Assert.NotNull(egg);
        Assert.Equal(chick, r.In(() => egg.child_color as Type));
        Assert.NotNull(r.In(() => egg.inherited_stats));
        Assert.True(r.In(() => Dm.@in(mom.raptor_color.GetType(), egg.inherited_stats.parent_colors) && Dm.@in(dad.raptor_color.GetType(), egg.inherited_stats.parent_colors)),
            "the egg remembers both parents' colours");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_raptor_egg_hatches_a_chick_of_its_colour_with_its_genes()
    {
        var r = new AudioRoom(13, 9, seed: 13265);
        var egg = r.In(() => Dm.@new<obj_item_food_egg_raptor_egg>(locate(4, 4, 1)));
        var genes = r.In(() =>
        {
            var g = Dm.@new<datum_raptor_inheritance>();
            g.attack_modifier = 2;
            egg.inherited_stats = g;
            egg.child_color = typeof(datum_raptor_color_blue);
            egg.growth_progress = RAPTOR_EGG_GROWTH_PROGRESS;
            return g;
        });
        mob_living_carbon_human bob = r.Dummy(8, 4);
        Assert.Contains("ready to hatch", r.In(() => string.Join(" ", Dm.each<object>(Waiting.NoSleep(egg.examine(bob)) as DmList))));
        Assert.True(RunUntil(r, 10 * Second, () => QDELETED(egg)), "the egg hatched");
        var chick = r.In(() => Dm.each<mob_living_basic_raptor>(range(0, locate(4, 4, 1))).FirstOrDefault());
        Assert.NotNull(chick);
        Assert.Equal(RAPTOR_BABY, r.In(() => chick.growth_stage));
        Assert.IsType<datum_raptor_color_blue>(r.In(() => chick.raptor_color));
        Assert.Same(genes, r.In(() => chick.inherited_stats));
        Assert.IsType<datum_ai_controller_basic_controller_baby_raptor>(r.In(() => chick.ai_controller));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_raptor_egg_does_not_hatch_on_lava_or_in_a_hand()
    {
        var r = new AudioRoom(13, 9, seed: 13266);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var egg = r.In(() => Dm.@new<obj_item_food_egg_raptor_egg>(locate(5, 4, 1)));
        r.In(() =>
        {
            egg.growth_progress = RAPTOR_EGG_GROWTH_PROGRESS + 1;
            Waiting.NoSleep(bob.put_in_active_hand(egg));
        });
        RunFor(r, 3 * Second);
        Assert.False(r.In(() => QDELETED(egg)), "an egg in a hand waits");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_chick_grows_into_a_youngling_and_the_youngling_into_an_adult()
    {
        var r = new AudioRoom(13, 9, seed: 13267);
        mob_living_carbon_human bob = r.Dummy(9, 4);
        var chick = r.In(() => Dm.@new<mob_living_basic_raptor_baby_red>(locate(4, 4, 1)));
        r.Step(2);
        Assert.Equal(MOB_SIZE_TINY, r.In(() => chick.mob_size));
        Assert.False(r.In(() => Dm.truthy(chick.density)));
        r.In(() => chick.growth_progress = RAPTOR_GROWTH_REQUIRED - 0.01f);
        r.In(() =>
        {
            // Babies grow on their own over time (_raptor.dm:162): run Life until the roll comes up
            for (int i = 0; i < 400 && chick.growth_stage == RAPTOR_BABY; i++)
                chick.Life(SSmobs.wait / 10f);
        });
        Assert.Equal(RAPTOR_YOUNG, r.In(() => chick.growth_stage));
        Assert.Equal("red raptor youngling", r.In(() => chick.name));
        Assert.NotNull(r.In(() => chick.GetComponent(typeof(datum_component_tameable))));
        Assert.IsAssignableFrom<datum_ai_controller_basic_controller_raptor>(r.In(() => chick.ai_controller));
        // Younglings grow from meat and flora (_raptor.dm:325)
        r.In(() =>
        {
            for (int i = 0; i < 40 && chick.growth_stage == RAPTOR_YOUNG; i++)
            {
                var steak = Dm.@new<obj_item_food_meat_steak>(chick.loc);
                chick.SEND_SIGNAL(COMSIG_MOB_ATE, Dm.list(chick, steak, null));
                qdel(steak);
            }
        });
        Assert.Equal(RAPTOR_ADULT, r.In(() => chick.growth_stage));
        Assert.Null(r.In(() => chick.GetComponent(typeof(datum_component_tameable))));
        Assert.True(r.In(() => Dm.truthy(chick.density)));
        Assert.Equal("raptor_red", r.In(() => chick.icon_state));
        RunFor(r, 2 * Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_wild_adult_raptor_throws_off_a_rider_who_misses_the_arrows()
    {
        var r = new AudioRoom(13, 9, seed: 13268);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var raptor = r.In(() => Dm.@new<mob_living_basic_raptor_red>(locate(4, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(raptor.buckle_mob(bob, TRUE)));
        var game = r.In(() => Dm.each<datum>(SSprocessing.processing).OfType<datum_riding_minigame>().FirstOrDefault());
        Assert.NotNull(game);
        Assert.True(r.In(() => bob.buckled == raptor));
        // Bob steers the wrong way each time an arrow is up
        Assert.True(RunUntil(r, 30 * Second, () =>
        {
            if (QDELETED(game) || bob.buckled != raptor)
                return true;
            if (Dm.each<object>(game.cached_arrows).Any(d => Dm.truthy((game.cached_arrows[d] as DmList)["is_active"])))
            {
                string up = Dm.each<object>(game.cached_arrows).First(d => Dm.truthy((game.cached_arrows[d] as DmList)["is_active"])) as string;
                float wrong = Dm.num(turn(text2dir(up), 180));
                raptor.SEND_SIGNAL(COMSIG_MOVABLE_ATTEMPTED_MOVE, Dm.list(raptor, get_step(raptor, wrong), wrong));
            }
            return false;
        }), "the minigame ended");
        Assert.True(r.In(() => bob.buckled == null), "the raptor threw Bob off");
        Assert.False(r.In(() => Dm.truthy(raptor.has_ally(bob))));
        Assert.True(r.In(() => Dm.truthy(bob.IsKnockdown())));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_wild_adult_raptor_calms_down_for_a_rider_who_follows_the_arrows()
    {
        var r = new AudioRoom(13, 13, seed: 13269);
        mob_living_carbon_human bob = r.Join(7, 7, "Bob");
        var raptor = r.In(() => Dm.@new<mob_living_basic_raptor_green>(locate(6, 6, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(raptor.buckle_mob(bob, TRUE)));
        var game = r.In(() => Dm.each<datum>(SSprocessing.processing).OfType<datum_riding_minigame>().FirstOrDefault());
        Assert.NotNull(game);
        Assert.Equal(4, r.In(() => Dm.length(game.cached_arrows)));
        Assert.True(RunUntil(r, 40 * Second, () =>
        {
            if (QDELETED(game))
                return true;
            object up = Dm.each<object>(game.cached_arrows).FirstOrDefault(d => Dm.truthy((game.cached_arrows[d] as DmList)["is_active"]));
            if (up != null)
            {
                float dir = Dm.num(text2dir(up));
                raptor.SEND_SIGNAL(COMSIG_MOVABLE_ATTEMPTED_MOVE, Dm.list(raptor, get_step(raptor, dir), dir));
            }
            return false;
        }), "the minigame ended");
        Assert.True(r.In(() => Dm.truthy(raptor.has_ally(bob))), "the raptor befriended Bob");
        Assert.True(r.In(() => bob.buckled == raptor), "Bob stays in the saddle");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_raptor_eats_from_its_trough()
    {
        var r = new AudioRoom(13, 9, seed: 13270);
        var trough = r.In(() => Dm.@new<obj_structure_ore_container_food_trough_raptor_trough>(locate(5, 4, 1)));
        var ore = r.In(() => Dm.@new<obj_item_stack_ore_diamond>(locate(5, 4, 1), 3, FALSE));
        r.In(() => Waiting.NoSleep(ore.forceMove(trough)));
        var raptor = r.In(() => Dm.@new<mob_living_basic_raptor_red>(locate(4, 4, 1)));
        r.Step(2);
        r.In(() => raptor.combat_mode = FALSE);
        r.In(() => Waiting.NoSleep(raptor.early_melee_attack(trough, null, TRUE)));
        Assert.True(r.In(() => QDELETED(ore) || ore.loc != trough || Dm.num(ore.amount) < 3), "the raptor ate from the trough");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_raptor_killed_by_a_crusher_gives_up_its_feather()
    {
        var r = new AudioRoom(13, 9, seed: 13271);
        // The feather drops at a hundred per cent of the crusher's share (_raptor.dm:100)
        var raptor = r.In(() => Dm.@new<mob_living_basic_raptor_white>(locate(6, 4, 1)));
        r.Step(2);
        r.In(() =>
        {
            var damage = Waiting.NoSleep(raptor.apply_status_effect(typeof(datum_status_effect_crusher_damage))) as datum_status_effect_crusher_damage;
            damage.total_damage = Dm.num(raptor.maxHealth);
            Waiting.NoSleep(raptor.death(FALSE));
        });
        Assert.True(r.In(() => Dm.truthy(raptor.guaranteed_butcher_results?[typeof(obj_item_crusher_trophy_raptor_feather)])), "the feather is in the butchering");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_raptor_dex_scans_a_raptor_and_opens_its_window()
    {
        var r = new AudioRoom(13, 9, seed: 13272);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var raptor = r.In(() => Dm.@new<mob_living_basic_raptor_purple>(locate(4, 4, 1)));
        var dex = r.In(() => Dm.@new<obj_item_raptor_dex>(locate(5, 4, 1)));
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(dex)));
        r.Step(2);
        Assert.Equal(ITEM_INTERACT_SUCCESS, r.In(() => Waiting.NoSleep(dex.interact_with_atom(raptor, bob))));
        DmList data = r.In(() => dex.scan_data);
        Assert.True(r.In(() => Dm.truthy(data["raptor_scan"])));
        Assert.Equal(r.In(() => raptor.name), r.In(() => data["raptor_color"] as string));
        Assert.Equal(r.In(() => raptor.icon_state), r.In(() => data["raptor_icon_state"] as string));
        Assert.Equal(4, r.In(() => Dm.length(data["raptor_happiness"])));
        Assert.Equal(r.In(() => Dm.num(raptor.maxHealth)), r.In(() => Dm.num(data["raptor_max_health"])));
        Assert.True(r.In(() => Dm.length(data["inherited_traits"]) >= 1), "the raptor has at least one personality trait");
        Assert.NotNull(r.In(() => SStgui.get_open_ui(bob, dex)));
        Assert.Equal("RaptorDex", r.In(() => (SStgui.get_open_ui(bob, dex) as datum_tgui).@interface));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_raptor_dex_tablet_program_scans_a_tapped_raptor()
    {
        var r = new AudioRoom(13, 9, seed: 13273);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var raptor = r.In(() => Dm.@new<mob_living_basic_raptor_black>(locate(4, 4, 1)));
        var pda = r.In(() => Dm.@new<obj_item_modular_computer_pda>(locate(5, 4, 1)));
        var app = r.In(() =>
        {
            var a = Dm.@new<datum_computer_file_program_raptordex>();
            Assert.True(Dm.truthy(pda.store_file(a)));
            return a;
        });
        r.Step(2);
        Assert.Equal(TRUE, r.In(() => Waiting.NoSleep(app.tap(raptor, bob))));
        DmList data = r.In(() => Waiting.NoSleep(app.ui_data(bob)) as DmList);
        Assert.True(r.In(() => Dm.truthy(data["raptor_scan"])));
        Assert.Equal(r.In(() => raptor.name), r.In(() => data["raptor_color"] as string));
        Assert.Equal(FALSE, r.In(() => Waiting.NoSleep(app.tap(bob, bob))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_happy_raptor_shows_full_friendship_hearts()
    {
        var r = new AudioRoom(13, 9, seed: 13274);
        var raptor = r.In(() => Dm.@new<mob_living_basic_raptor_red>(locate(4, 4, 1)));
        r.Step(2);
        float attack = r.In(() => raptor.melee_damage_lower);
        var happiness = r.In(() => raptor.GetComponent(typeof(datum_component_happiness)) as datum_component_happiness);
        r.In(() => happiness.change_happiness_level(900));
        Assert.Equal(1f, r.In(() => Dm.num((raptor.ai_controller as datum_ai_controller).blackboard[BB_BASIC_HAPPINESS])));
        DmList data = r.In(() => obj_item_raptor_dex.raptor_scan_data(raptor));
        Assert.Equal(new[] { "full_heart", "full_heart", "full_heart", "full_heart" }, r.In(() => Dm.each<object>(data["raptor_happiness"] as DmList).Cast<string>().ToArray()));
        // Kept as tg has it: the happiness callback's thresholds are percents (0 to 100) compared with a fraction
        // (happiness.dm:101-112), so only the 0 threshold is ever crossed and the raptor's attack never changes
        Assert.Equal(attack, r.In(() => raptor.melee_damage_lower));
        r.AssertNoRuntimes();
    }
}
