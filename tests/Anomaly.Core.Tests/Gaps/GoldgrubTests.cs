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
/// The goldgrub: it eats the ore lying about, spits it back, burrows into the rock and out again, bounces kinetic shots off
/// its girth, is tamed with ore and ridden, lays an egg after eating bluespace, and dies spitting out what it ate.
/// </summary>
public class GoldgrubTests
{
    public static IEnumerable<object[]> Grubs() => new[]
    {
        typeof(mob_living_basic_mining_goldgrub), typeof(mob_living_basic_mining_goldgrub_baby),
        typeof(obj_item_food_egg_green_grub_egg),
    }.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(Grubs))]
    public void A_goldgrub_thing_is_made_lives_and_goes_away_cleanly(Type path) =>
        Lifecycle(new AudioRoom(13, 9, seed: 13201), path);

    [Theory]
    [InlineData(typeof(mob_living_basic_mining_goldgrub))]
    public void A_goldgrub_lives_beside_a_player_without_an_error(Type path)
    {
        var r = new AudioRoom(15, 11, seed: 13232);
        mob_living_carbon_human bob = r.Join(11, 5, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_GODMODE, "test"));
        r.In(() => Dm.@new(path, locate(3, 5, 1)));
        RunFor(r, 60 * Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_goldgrub_swallows_plain_ore_and_spits_it_back_out()
    {
        var r = new AudioRoom(13, 9, seed: 13202);
        var grub = r.In(() => Dm.@new<mob_living_basic_mining_goldgrub>(locate(4, 4, 1)));
        var ore = r.In(() => Dm.@new<obj_item_stack_ore_iron>(locate(5, 4, 1), 3, FALSE));
        r.Step(2);
        // one ore of the stack at a time, swallowed whole: iron heals nothing (basic_eating.dm:125-131)
        r.In(() => Waiting.NoSleep(grub.UnarmedAttack(ore, TRUE)));
        Assert.Equal(2f, r.In(() => Dm.num(ore.amount)));
        var eaten = r.In(() => Dm.each<obj_item_stack_ore_iron>(grub.contents).Single());
        var spit = r.In(() => Dm.each<datum_action_cooldown_mob_cooldown_spit_ore>(grub.actions).First());
        r.In(() => Waiting.NoSleep(spit.Trigger(null, null, grub)));
        Assert.True(r.In(() => eaten.loc == grub.loc || QDELETED(eaten)), "the grub spat the iron out");
        Assert.Empty(r.In(() => Dm.each<obj_item_stack_ore>(grub.contents).ToList()));
        Assert.True(r.In(() => spit.next_use_time > world.time), "the spit went on cooldown");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_goldgrub_eating_gold_heals_and_digests_it()
    {
        var r = new AudioRoom(13, 9, seed: 13231);
        var grub = r.In(() => Dm.@new<mob_living_basic_mining_goldgrub>(locate(4, 4, 1)));
        var ore = r.In(() => Dm.@new<obj_item_stack_ore_gold>(locate(5, 4, 1), 3, FALSE));
        r.Step(2);
        r.In(() => Waiting.NoSleep(grub.adjust_brute_loss(20)));
        float hurt = r.In(() => Dm.num(grub.health));
        r.In(() => Waiting.NoSleep(grub.UnarmedAttack(ore, TRUE)));
        Assert.Equal(2f, r.In(() => Dm.num(ore.amount)));
        Assert.Equal(hurt + 5, r.In(() => Dm.num(grub.health)));
        Assert.Empty(r.In(() => Dm.each<obj_item_stack_ore>(grub.contents).ToList()));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_goldgrub_burrows_into_the_ground_and_comes_back_up()
    {
        var r = new AudioRoom(13, 9, seed: 13203);
        r.In(() => Waiting.NoSleep(locate(4, 4, 1).ChangeTurf(typeof(turf_open_misc_asteroid))));
        var grub = r.In(() => Dm.@new<mob_living_basic_mining_goldgrub>(locate(4, 4, 1)));
        r.Step(2);
        var burrow = r.In(() => Dm.each<datum_action_cooldown_mob_cooldown_burrow>(grub.actions).First());
        // back onto the asteroid patch if its idle wander took it off (the wander stops while it digs, a do_after)
        r.In(() => Waiting.NoSleep(grub.forceMove(locate(4, 4, 1))));
        r.Command(() => Waiting.Detach(burrow.Trigger(null, null, grub)));
        Assert.True(RunUntil(r, 5 * Second, () => grub.loc is obj_effect_dummy_phased_mob_grub_burrow), "the grub went under");
        Assert.True(r.In(() => GlobalProcs.is_jaunting(grub)));
        r.Command(() => Waiting.Detach(burrow.Trigger(null, null, grub)));
        Assert.True(RunUntil(r, 5 * Second, () => grub.loc is turf), "the grub came back up");
        Assert.True(r.In(() => burrow.next_use_time > world.time), "the burrow went on cooldown");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_goldgrub_cannot_burrow_through_a_station_floor()
    {
        var r = new AudioRoom(13, 9, seed: 13204);
        var grub = r.In(() => Dm.@new<mob_living_basic_mining_goldgrub>(locate(4, 4, 1)));
        r.Step(2);
        var burrow = r.In(() => Dm.each<datum_action_cooldown_mob_cooldown_burrow>(grub.actions).First());
        Assert.False(r.In(() => Dm.truthy(Waiting.NoSleep(burrow.IsAvailable(FALSE)))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_kinetic_shot_bounces_off_a_goldgrub()
    {
        var r = new AudioRoom(13, 9, seed: 13205);
        var grub = r.In(() => Dm.@new<mob_living_basic_mining_goldgrub>(locate(4, 4, 1)));
        r.Step(2);
        var shot = r.In(() => Dm.@new<obj_projectile_kinetic>(locate(6, 4, 1)));
        r.In(() => shot.armour_penetration = 0);
        Assert.Equal(COMPONENT_BULLET_BLOCKED, r.In(() => grub.block_bullets(grub, shot)));
        r.In(() => qdel(shot));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_goldgrub_dies_and_spits_out_what_it_ate()
    {
        var r = new AudioRoom(13, 9, seed: 13206);
        var grub = r.In(() => Dm.@new<mob_living_basic_mining_goldgrub>(locate(4, 4, 1)));
        var ore = r.In(() => Dm.@new<obj_item_stack_ore_diamond>(grub, 2, FALSE));
        r.Step(2);
        r.In(() => Waiting.NoSleep(grub.death(FALSE)));
        Assert.True(r.In(() => ore.loc == grub.loc), "the diamonds fell out of the dead grub");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_wild_goldgrub_from_the_map_carries_ore_in_its_belly()
    {
        var r = new AudioRoom(13, 9, seed: 13207);
        // Only a goldgrub made with the map rolls its loot (goldgrub.dm:47)
        var mapped = Mapped<mob_living_basic_mining_goldgrub>(r, 5, 4);
        r.Step(2);
        int ores = r.In(() => Dm.each<obj_item_stack_ore>(mapped.contents).Count());
        Assert.InRange(ores, 1, 3);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_tamed_goldgrub_takes_a_rider_and_obeys()
    {
        var r = new AudioRoom(13, 9, seed: 13208);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var grub = r.In(() => Dm.@new<mob_living_basic_mining_goldgrub>(locate(4, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(grub.tamed(bob, null)));
        Assert.NotNull(r.In(() => grub.GetComponent(typeof(datum_component_obeys_commands))));
        r.In(() => Waiting.NoSleep(grub.buckle_mob(bob, TRUE)));
        Assert.True(r.In(() => bob.buckled == grub), "Bob sits on the grub");
        Assert.NotNull(r.In(() => grub.GetComponent(typeof(datum_component_riding_creature_goldgrub))));
        RunFor(r, 2 * Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_wild_goldgrub_lays_an_egg_after_eating_bluespace_and_the_egg_hatches_a_grub()
    {
        var r = new AudioRoom(13, 9, seed: 13209);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var grub = Mapped<mob_living_basic_mining_goldgrub>(r, 4, 4);
        r.Step(2);
        var layer = r.In(() => grub.GetComponent(typeof(datum_component_egg_layer)) as datum_component_egg_layer);
        Assert.NotNull(layer);
        // hand-fed (egg_layer.dm:55), not eaten off the floor
        var crystal = r.In(() => Dm.@new<obj_item_stack_ore_bluespace_crystal>(locate(5, 4, 1), 1, FALSE));
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(crystal)));
        r.In(() => grub.SEND_SIGNAL(COMSIG_ATOM_ATTACKBY, Dm.list(grub, crystal, bob, null)));
        Assert.Equal(1f, r.In(() => Dm.num(layer.eggs_left)));
        var egg = r.In(() => Dm.@new<obj_item_food_egg_green_grub_egg>(locate(6, 4, 1)));
        var fertile = r.In(() => egg.GetComponent(typeof(datum_component_fertile_egg)) as datum_component_fertile_egg);
        Assert.NotNull(fertile);
        r.In(() => fertile.current_growth = Dm.num(fertile.total_growth_required) - 0.5f);
        Assert.True(RunUntil(r, 20 * Second, () => OnFloor<mob_living_basic_mining_goldgrub_baby>(r, 1, locate(6, 4, 1))), "the egg hatched a baby grub");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_baby_goldgrub_grows_up()
    {
        var r = new AudioRoom(13, 9, seed: 13210);
        var baby = r.In(() => Dm.@new<mob_living_basic_mining_goldgrub_baby>(locate(4, 4, 1)));
        r.Step(2);
        var growth = r.In(() => baby.GetComponent(typeof(datum_component_growth_and_differentiation)) as datum_component_growth_and_differentiation);
        Assert.NotNull(growth);
        // the growth waits for the round to start (growth_and_differentiation.dm:64-67): start it as the round would
        r.In(() =>
        {
            growth.comp_on_round_start();
            growth.percent_grown = 99.9f;
        });
        Assert.True(RunUntil(r, 30 * Second, () => QDELETED(baby)), "the baby grew");
        Assert.True(OnFloor<mob_living_basic_mining_goldgrub>(r, 1, r.In(() => locate(4, 4, 1))));
        r.AssertNoRuntimes();
    }
}
