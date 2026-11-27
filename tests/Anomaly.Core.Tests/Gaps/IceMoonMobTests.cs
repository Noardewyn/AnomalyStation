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
/// The ice moon's mobs: the ice demon teleports, ices the floor around it and splits off afterimages that share its wounds;
/// the ice whelp's breath freezes in a line or erupts in a cross, it carves icy rocks into statues of itself and eats its
/// own dead; the wolves are tamed with meat, ridden and take their tamer's side; the polar bears maul. Each drops its
/// crusher trophy when a crusher did the work.
/// </summary>
public class IceMoonMobTests
{
    public static IEnumerable<object[]> IceMobs() => new[]
    {
        typeof(mob_living_basic_mining_ice_demon), typeof(mob_living_basic_mining_demon_afterimage), typeof(mob_living_basic_mining_demon_afterimage_crusher),
        typeof(mob_living_basic_mining_ice_whelp), typeof(mob_living_basic_mining_ice_whelp_ash),
        typeof(mob_living_basic_mining_polarbear), typeof(mob_living_basic_mining_polarbear_lesser), typeof(mob_living_basic_mining_wolf),
        typeof(obj_effect_temp_visual_slippery_ice), typeof(obj_item_crusher_trophy_tail_spike),
    }.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(IceMobs))]
    public void An_ice_moon_thing_is_made_lives_and_goes_away_cleanly(Type path) =>
        Lifecycle(new AudioRoom(13, 9, seed: 13241), path);

    [Theory]
    [InlineData(typeof(mob_living_basic_mining_ice_demon), typeof(obj_item_crusher_trophy_ice_demon_cube))]
    [InlineData(typeof(mob_living_basic_mining_ice_whelp), typeof(obj_item_crusher_trophy_tail_spike))]
    [InlineData(typeof(mob_living_basic_mining_polarbear), typeof(obj_item_crusher_trophy_bear_paw))]
    [InlineData(typeof(mob_living_basic_mining_wolf), typeof(obj_item_crusher_trophy_wolf_ear))]
    public void An_ice_moon_mob_a_crusher_killed_drops_its_trophy(Type mob, Type trophy)
    {
        var r = new AudioRoom(13, 9, seed: 13242);
        Assert.True(DropsTrophy(r, mob, trophy, 6, 4), mob.Name + " dropped no " + trophy.Name);
        r.AssertNoRuntimes();
    }

    [Theory]
    [InlineData(typeof(mob_living_basic_mining_ice_demon))]
    [InlineData(typeof(mob_living_basic_mining_ice_whelp))]
    [InlineData(typeof(mob_living_basic_mining_polarbear))]
    [InlineData(typeof(mob_living_basic_mining_wolf))]
    public void An_ice_moon_mob_hunts_a_player_without_an_error(Type path)
    {
        var r = new AudioRoom(15, 11, seed: 13254);
        mob_living_carbon_human bob = r.Join(11, 5, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_GODMODE, "test"));
        r.In(() => Dm.@new(path, locate(3, 5, 1)));
        RunFor(r, 60 * Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_ice_demon_fights_a_player_to_the_end_without_an_error()
    {
        var r = new AudioRoom(15, 11, seed: 13256);
        mob_living_carbon_human bob = r.Join(11, 5, "Bob");
        r.In(() => Dm.@new<mob_living_basic_mining_ice_demon>(locate(3, 5, 1)));
        RunFor(r, 120 * Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_ice_demon_teleports_where_it_aims()
    {
        var r = new AudioRoom(13, 9, seed: 13243);
        var demon = r.In(() => Dm.@new<mob_living_basic_mining_ice_demon>(locate(3, 4, 1)));
        r.Step(2);
        var teleport = r.In(() => Dm.each<datum_action_cooldown_mob_cooldown_ice_demon_teleport>(demon.actions).First());
        turf there = r.In(() => (turf)locate(9, 4, 1));
        r.In(() => Waiting.NoSleep(teleport.Trigger(null, null, there)));
        Assert.True(r.In(() => teleport.next_use_time > world.time), "the teleport went on cooldown");
        Assert.True(RunUntil(r, 3 * Second, () => demon.loc == there), "the demon arrived");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_ice_demon_will_not_teleport_into_a_wall()
    {
        var r = new AudioRoom(13, 9, seed: 13244);
        var demon = r.In(() => Dm.@new<mob_living_basic_mining_ice_demon>(locate(3, 4, 1)));
        turf wall = r.In(() => (turf)Waiting.NoSleep(locate(9, 4, 1).ChangeTurf(typeof(turf_closed_wall))));
        r.Step(2);
        var teleport = r.In(() => Dm.each<datum_action_cooldown_mob_cooldown_ice_demon_teleport>(demon.actions).First());
        Assert.False(r.In(() => Dm.truthy(Waiting.NoSleep(teleport.Activate(wall)))));
        RunFor(r, 2 * Second);
        Assert.True(r.In(() => demon.loc == locate(3, 4, 1)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_ice_demon_spreads_slippery_ice_around_itself()
    {
        var r = new AudioRoom(13, 13, seed: 13245);
        var demon = r.In(() => Dm.@new<mob_living_basic_mining_ice_demon>(locate(6, 6, 1)));
        r.Step(2);
        var slip = r.In(() => Dm.each<datum_action_cooldown_mob_cooldown_slippery_ice_floors>(demon.actions).First());
        r.In(() => Waiting.NoSleep(slip.Trigger(null, null, demon)));
        r.Step(2);
        Assert.True(OnFloor<obj_effect_temp_visual_slippery_ice>(r, 0, r.In(() => locate(6, 6, 1))), "ice formed under the demon");
        // radius 1 (ice_demon_abilities.dm:40): the demon's tile and the four beside it
        Assert.True(RunUntil(r, 3 * Second, () => Dm.each<atom>(range(2, demon)).Count(a => a is obj_effect_temp_visual_slippery_ice) == 5), "the ice spread outwards");
        var ice = r.In(() => Dm.each<obj_effect_temp_visual_slippery_ice>(range(0, demon)).First());
        Assert.NotNull(r.In(() => ice.GetComponent(typeof(datum_component_slippery))));
        Assert.True(RunUntil(r, 15 * Second, () => !Dm.each<atom>(range(4, demon)).Any(a => a is obj_effect_temp_visual_slippery_ice)), "the ice melted away");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_ice_demons_ice_slips_a_walker_and_melts_away_cleanly()
    {
        var r = new AudioRoom(13, 13, seed: 13255);
        mob_living_carbon_human bob = r.Join(10, 6, "Bob");
        var demon = r.In(() => Dm.@new<mob_living_basic_mining_ice_demon>(locate(6, 6, 1)));
        r.Step(2);
        r.In(() =>
        {
            if (demon.ai_controller is datum_ai_controller ai)
                Waiting.NoSleep(ai.set_ai_status(AI_STATUS_OFF));
        });
        var slip = r.In(() => Dm.each<datum_action_cooldown_mob_cooldown_slippery_ice_floors>(demon.actions).First());
        r.In(() => Waiting.NoSleep(slip.Trigger(null, null, demon)));
        RunFor(r, Second);
        // Bob runs onto the ice beside the demon
        r.In(() => Waiting.NoSleep(bob.Move(locate(7, 6, 1), WEST)));
        Assert.True(r.In(() => Dm.truthy(bob.IsKnockdown())), "the ice slipped Bob");
        Assert.True(RunUntil(r, 15 * Second, () => !Dm.each<atom>(range(2, demon)).Any(a => a is obj_effect_temp_visual_slippery_ice)), "the ice melted away");
        // and later walks over where it was
        RunFor(r, 5 * Second);
        r.In(() =>
        {
            Waiting.NoSleep(bob.forceMove(locate(7, 7, 1)));
            Waiting.NoSleep(bob.forceMove(locate(7, 6, 1)));
            Waiting.NoSleep(bob.forceMove(locate(6, 5, 1)));
        });
        RunFor(r, Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_ice_demons_afterimages_share_its_wounds_and_fade()
    {
        var r = new AudioRoom(13, 9, seed: 13246);
        var demon = r.In(() => Dm.@new<mob_living_basic_mining_ice_demon>(locate(6, 4, 1)));
        r.Step(2);
        var clones = r.In(() => Dm.each<datum_action_cooldown_spell_conjure_limit_summons_create_afterimages>(demon.actions).First());
        r.In(() => Waiting.NoSleep(clones.Trigger(null, null, demon)));
        r.Step(2);
        var images = r.In(() => Dm.each<mob_living_basic_mining_demon_afterimage>(range(2, demon)).ToList());
        Assert.Equal(2, images.Count);
        Assert.Equal(2f, r.In(() => clones.number_of_summons));
        Assert.NotNull(r.In(() => images[0].GetComponent(typeof(datum_component_joint_damage))));
        Assert.False(r.In(() => Dm.truthy(Waiting.NoSleep(clones.can_cast_spell(FALSE)))), "no more than two at once");
        float before = r.In(() => Dm.num(demon.health));
        r.In(() => Waiting.NoSleep(images[0].adjust_brute_loss(20)));
        Assert.True(r.In(() => Dm.num(demon.health)) < before, "the demon felt its afterimage's wound");
        Assert.True(RunUntil(r, 20 * Second, () => images.All(i => QDELETED(i))), "the afterimages faded");
        Assert.Equal(0f, r.In(() => clones.number_of_summons));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_dead_ice_demon_leaves_bluespace_crystals()
    {
        var r = new AudioRoom(13, 9, seed: 13247);
        var demon = r.In(() => Dm.@new<mob_living_basic_mining_ice_demon>(locate(6, 4, 1)));
        r.Step(2);
        r.In(() => r.World.Rng.Reseed(13247, 1));
        r.In(() => Waiting.NoSleep(demon.death(FALSE)));
        r.Step(2);
        Assert.Equal(3, r.In(() => Dm.each<obj_item_stack_ore_bluespace_crystal>(range(1, locate(6, 4, 1))).Sum(s => (int)Dm.num(s.amount))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_ice_whelps_breath_freezes_and_burns_what_it_hits()
    {
        var r = new AudioRoom(15, 9, seed: 13248);
        mob_living_carbon_human bob = r.Dummy(8, 4);
        var whelp = r.In(() => Dm.@new<mob_living_basic_mining_ice_whelp>(locate(3, 4, 1)));
        r.Step(2);
        var breath = r.In(() => Dm.each<datum_action_cooldown_mob_cooldown_fire_breath_ice>(whelp.actions).First(a => a.GetType() == typeof(datum_action_cooldown_mob_cooldown_fire_breath_ice)));
        float before = r.In(() => Dm.num(bob.get_fire_loss()));
        r.Command(() => Waiting.Detach(breath.Trigger(null, null, bob)));
        r.Step(1);
        Assert.True(r.In(() => Dm.@in(breath.forecast_overlay, whelp.overlays)), "the whelp telegraphs its breath");
        Assert.True(RunUntil(r, 3 * Second, () => Dm.num(bob.get_fire_loss()) > before), "the breath hurt Bob");
        Assert.NotNull(r.In(() => bob.has_status_effect(typeof(datum_status_effect_ice_block_talisman))));
        Assert.False(r.In(() => Dm.@in(breath.forecast_overlay, whelp.overlays)), "the telegraph went away");
        RunFor(r, 3 * Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_ice_whelps_eruption_freezes_all_around_it()
    {
        var r = new AudioRoom(15, 15, seed: 13249);
        mob_living_carbon_human north = r.Dummy(7, 10);
        mob_living_carbon_human east = r.Dummy(10, 7);
        var whelp = r.In(() => Dm.@new<mob_living_basic_mining_ice_whelp>(locate(7, 7, 1)));
        r.Step(2);
        var eruption = r.In(() => Dm.each<datum_action_cooldown_mob_cooldown_fire_breath_ice_eruption>(whelp.actions).First());
        r.Command(() => Waiting.Detach(eruption.Trigger(null, null, north)));
        Assert.True(RunUntil(r, 4 * Second, () => Dm.num(north.get_fire_loss()) > 0 && Dm.num(east.get_fire_loss()) > 0), "the eruption hit both");
        Assert.Equal(0f, r.In(() => Dm.num(whelp.get_fire_loss())));
        RunFor(r, 3 * Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_ice_whelp_carves_an_icy_rock_into_its_statue()
    {
        var r = new AudioRoom(13, 9, seed: 13250);
        var whelp = r.In(() => Dm.@new<mob_living_basic_mining_ice_whelp>(locate(4, 4, 1)));
        var rock = r.In(() => Dm.@new<obj_structure_flora_rock_icy>(locate(5, 4, 1)));
        r.Step(2);
        r.Command(() => Waiting.Detach(whelp.early_melee_attack(rock, null, TRUE)));
        Assert.True(RunUntil(r, 8 * Second, () => QDELETED(rock)), "the rock was carved");
        var statue = r.In(() => Dm.each<obj_structure_statue_custom>(range(0, locate(5, 4, 1))).FirstOrDefault());
        Assert.NotNull(statue);
        Assert.True(r.In(() => Dm.truthy(statue.anchored)));
        Assert.Contains("statue of", r.In(() => statue.name));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_ice_whelp_devours_a_dead_whelp_and_heals()
    {
        var r = new AudioRoom(13, 9, seed: 13251);
        var whelp = r.In(() => Dm.@new<mob_living_basic_mining_ice_whelp>(locate(4, 4, 1)));
        var dead = r.In(() => Dm.@new<mob_living_basic_mining_ice_whelp>(locate(5, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(dead.death(FALSE)));
        r.In(() => Waiting.NoSleep(whelp.adjust_brute_loss(40)));
        float hurt = r.In(() => Dm.num(whelp.health));
        r.Command(() => Waiting.Detach(whelp.early_melee_attack(dead, null, TRUE)));
        Assert.True(RunUntil(r, 8 * Second, () => QDELETED(dead)), "the dead whelp was devoured");
        Assert.True(r.In(() => Dm.num(whelp.health)) > hurt, "the whelp healed");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_wolf_is_tamed_with_meat_ridden_and_takes_its_tamers_side()
    {
        var r = new AudioRoom(13, 9, seed: 13252);
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var wolf = r.In(() => Dm.@new<mob_living_basic_mining_wolf>(locate(4, 4, 1)));
        r.Step(2);
        Assert.NotNull(r.In(() => wolf.GetComponent(typeof(datum_component_tameable))));
        r.In(() => Waiting.NoSleep(wolf.tamed(bob, null)));
        r.In(() => Waiting.NoSleep(wolf.befriend(bob)));
        Assert.True(r.In(() => Dm.truthy(wolf.has_ally(bob))));
        Assert.True(r.In(() => Dm.eq(Dm.num((wolf.ai_controller as datum_ai_controller).blackboard[BB_BASIC_MOB_FLEE_DISTANCE]), 7)));
        Assert.True(r.In(() => Dm.truthy(Dm.bitand((wolf.ai_controller as datum_ai_controller).ai_traits, STOP_MOVING_WHEN_PULLED))));
        Assert.True(r.In(() => Dm.each<object>(wolf.get_faction()).SequenceEqual(Dm.each<object>(bob.get_faction()))), "the wolf joined Bob's side");
        Assert.NotNull(r.In(() => wolf.GetComponent(typeof(datum_component_obeys_commands))));
        r.In(() => Waiting.NoSleep(wolf.buckle_mob(bob, TRUE)));
        Assert.True(r.In(() => bob.buckled == wolf), "Bob rides the wolf");
        Assert.NotNull(r.In(() => wolf.GetComponent(typeof(datum_component_riding_creature_wolf))));
        RunFor(r, 2 * Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_polar_bear_mauls_what_stands_next_to_it()
    {
        var r = new AudioRoom(13, 9, seed: 13253);
        mob_living_carbon_human bob = r.Dummy(5, 4);
        var bear = r.In(() => Dm.@new<mob_living_basic_mining_polarbear>(locate(4, 4, 1)));
        r.Step(2);
        float before = r.In(() => Dm.num(bob.health));
        r.In(() => Waiting.NoSleep(bear.melee_attack(bob, null, TRUE)));
        Assert.True(r.In(() => Dm.num(bob.health)) < before, "the bear hurt Bob");
        Assert.True(r.In(() => HAS_TRAIT(bear, TRAIT_SNOWSTORM_IMMUNE)));
        r.AssertNoRuntimes();
    }
}
