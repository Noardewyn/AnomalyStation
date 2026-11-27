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
/// The mooks: the workers carry ore to their village's stand and swing their axes, leap at their prey and jump home to the
/// village; the healers mend their kin; the chief lights the bonfire; a village rises together against whoever hurt one.
/// </summary>
public class MookTests
{
    public static IEnumerable<object[]> Mooks() => new[]
    {
        typeof(mob_living_basic_mining_mook), typeof(mob_living_basic_mining_mook_worker), typeof(mob_living_basic_mining_mook_worker_bard),
        typeof(mob_living_basic_mining_mook_worker_tribal_chief), typeof(obj_structure_ore_container_material_stand),
        typeof(obj_effect_landmark_mook_village), typeof(obj_effect_temp_visual_mook_dust), typeof(obj_effect_temp_visual_mook_dust_small),
    }.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(Mooks))]
    public void A_mook_thing_is_made_lives_and_goes_away_cleanly(Type path) =>
        Lifecycle(new AudioRoom(13, 9, seed: 13221), path);

    [Theory]
    [InlineData(typeof(mob_living_basic_mining_mook_worker))]
    [InlineData(typeof(mob_living_basic_mining_mook_worker_tribal_chief))]
    [InlineData(typeof(mob_living_basic_mining_mook_worker_bard))]
    public void A_mook_lives_beside_a_player_without_an_error(Type path)
    {
        var r = new AudioRoom(15, 11, seed: 13233);
        mob_living_carbon_human bob = r.Join(11, 5, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_GODMODE, "test"));
        r.In(() => Dm.@new(path, locate(3, 5, 1)));
        RunFor(r, 60 * Second);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_worker_mook_carries_ore_to_the_material_stand()
    {
        var r = new AudioRoom(13, 9, seed: 13222);
        var mook = r.In(() => Dm.@new<mob_living_basic_mining_mook_worker>(locate(4, 4, 1)));
        var stand = r.In(() => Dm.@new<obj_structure_ore_container_material_stand>(locate(6, 4, 1)));
        var ore = r.In(() => Dm.@new<obj_item_stack_ore_iron>(locate(5, 4, 1), 2, FALSE));
        r.Step(2);
        r.In(() => Waiting.NoSleep(mook.attack_sequence(ore)));
        Assert.True(r.In(() => mook.held_ore == ore), "the mook picked up the iron");
        // the worker swings its axe as it grabs (mook.dm:223-232), and shows the ore once it settles
        Assert.Equal(MOOK_ATTACK_STRIKE, r.In(() => mook.attack_state));
        RunFor(r, 10);
        var overlays = r.In(() => Waiting.NoSleep(mook.update_overlays()) as DmList);
        Assert.True(r.In(() => Dm.@in(mook.ore_overlay, overlays)), "the mook shows the ore it carries");
        r.In(() => Waiting.NoSleep(mook.attack_sequence(stand)));
        Assert.True(r.In(() => ore.loc == stand && mook.held_ore == null), "the mook left the iron on the stand");
        var standOverlays = r.In(() => Waiting.NoSleep(stand.update_overlays()) as DmList);
        Assert.True(r.In(() => Dm.each<object>(standOverlays).OfType<image>().Any()), "the stand shows the ore on it");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_worker_mook_swings_its_axe_and_settles_back()
    {
        var r = new AudioRoom(13, 9, seed: 13223);
        mob_living_carbon_human bob = r.Dummy(5, 4);
        var mook = r.In(() => Dm.@new<mob_living_basic_mining_mook_worker>(locate(4, 4, 1)));
        r.Step(2);
        Assert.Equal("mook", r.In(() => mook.icon_state));
        r.In(() => Waiting.NoSleep(mook.attack_sequence(bob)));
        Assert.Equal(MOOK_ATTACK_STRIKE, r.In(() => mook.attack_state));
        Assert.Equal("mook_strike", r.In(() => mook.icon_state));
        RunFor(r, 10);
        Assert.Equal(MOOK_ATTACK_NEUTRAL, r.In(() => mook.attack_state));
        Assert.Equal("mook", r.In(() => mook.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_worker_mook_leaps_at_a_far_target_and_strikes_it()
    {
        var r = new AudioRoom(15, 9, seed: 13224);
        mob_living_carbon_human bob = r.Dummy(9, 4);
        var mook = r.In(() => Dm.@new<mob_living_basic_mining_mook_worker>(locate(3, 4, 1)));
        r.Step(2);
        var leap = r.In(() => Dm.each<datum_action_cooldown_mob_cooldown_mook_ability_mook_leap>(mook.actions).First());
        float before = r.In(() => Dm.num(bob.health));
        r.In(() => Waiting.NoSleep(leap.Trigger(null, null, bob)));
        Assert.Equal(MOOK_ATTACK_WARMUP, r.In(() => mook.attack_state));
        Assert.Equal("mook_warmup", r.In(() => mook.icon_state));
        Assert.True(RunUntil(r, 5 * Second, () => get_dist(mook, bob) <= 1), "the mook leapt to Bob");
        Assert.True(RunUntil(r, 5 * Second, () => Dm.num(bob.health) < before), "the mook struck Bob after its leap");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_mook_jumps_home_to_its_village()
    {
        var r = new AudioRoom(15, 9, seed: 13225);
        r.In(() => Dm.@new<obj_effect_landmark_mook_village>(locate(12, 4, 1)));
        var mook = r.In(() => Dm.@new<mob_living_basic_mining_mook>(locate(3, 4, 1)));
        r.Step(2);
        var jump = r.In(() => Dm.each<datum_action_cooldown_mob_cooldown_mook_ability_mook_jump>(mook.actions).First());
        r.In(() => Waiting.NoSleep(jump.Trigger(null, null, mook)));
        Assert.Equal(MOOK_ATTACK_ACTIVE, r.In(() => mook.attack_state));
        Assert.Equal("mook_leap", r.In(() => mook.icon_state));
        Assert.True(OnFloor<obj_effect_temp_visual_mook_dust>(r, 0, r.In(() => locate(3, 4, 1))), "dust rose where the mook jumped");
        Assert.True(RunUntil(r, 3 * Second, () => get_dist(mook, locate(12, 4, 1)) <= 3), "the mook landed by the village");
        Assert.True(RunUntil(r, 3 * Second, () => mook.attack_state == MOOK_ATTACK_NEUTRAL), "the mook settled after landing");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_healer_mook_mends_a_hurt_worker()
    {
        var r = new AudioRoom(13, 9, seed: 13226);
        var healer = r.In(() => Dm.@new<mob_living_basic_mining_mook>(locate(4, 4, 1)));
        var worker = r.In(() => Dm.@new<mob_living_basic_mining_mook_worker>(locate(5, 4, 1)));
        r.Step(2);
        Assert.NotNull(r.In(() => healer.GetComponent(typeof(datum_component_healing_touch))));
        Assert.Null(r.In(() => worker.GetComponent(typeof(datum_component_healing_touch))));
        r.In(() => Waiting.NoSleep(worker.adjust_brute_loss(30)));
        float hurt = r.In(() => Dm.num(worker.health));
        r.In(() => healer.combat_mode = FALSE);
        r.Command(() => Waiting.Detach(healer.UnarmedAttack(worker, TRUE)));
        Assert.True(RunUntil(r, 5 * Second, () => Dm.num(worker.health) > hurt), "the healer mended the worker");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_tribal_chief_lights_the_bonfire()
    {
        var r = new AudioRoom(13, 9, seed: 13227);
        var chief = r.In(() => Dm.@new<mob_living_basic_mining_mook_worker_tribal_chief>(locate(4, 4, 1)));
        var fire = r.In(() => Dm.@new<obj_structure_bonfire>(locate(5, 4, 1)));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy((chief.ai_controller as datum_ai_controller).blackboard[BB_BONFIRE_TARGETS])));
        var overlays = r.In(() => Waiting.NoSleep(chief.update_overlays()) as DmList);
        Assert.True(r.In(() => Dm.@in(mob_living_basic_mining_mook_worker_tribal_chief.chief_neutral, overlays)), "the chief wears its headdress");
        r.In(() => Waiting.NoSleep(chief.attack_sequence(fire)));
        Assert.True(r.In(() => Dm.truthy(fire.burning)), "the chief lit the bonfire");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_bard_mook_carries_its_guitar()
    {
        var r = new AudioRoom(13, 9, seed: 13228);
        var bard = r.In(() => Dm.@new<mob_living_basic_mining_mook_worker_bard>(locate(4, 4, 1)));
        r.Step(2);
        Assert.True(r.In(() => bard.held_guitar is obj_item_instrument_guitar && bard.held_guitar.loc == bard));
        Assert.True(r.In(() => (bard.ai_controller as datum_ai_controller).blackboard[BB_SONG_INSTRUMENT] == bard.held_guitar));
        r.In(() => qdel(bard));
        Assert.True(r.In(() => QDELETED(bard.held_guitar) || bard.held_guitar == null));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_village_turns_on_whoever_hurts_one_of_it()
    {
        var r = new AudioRoom(13, 9, seed: 13229);
        mob_living_carbon_human bob = r.Dummy(3, 4);
        var hurt = r.In(() => Dm.@new<mob_living_basic_mining_mook_worker>(locate(5, 4, 1)));
        var friend = r.In(() => Dm.@new<mob_living_basic_mining_mook_worker>(locate(8, 4, 1)));
        r.Step(2);
        r.In(() => hurt.attack_intruder(bob));
        Assert.True(r.In(() => Dm.truthy(((friend.ai_controller as datum_ai_controller).blackboard[BB_BASIC_MOB_RETALIATE_LIST] as DmList)?[bob])), "the other mook remembers Bob");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_dead_mook_shows_what_it_really_was()
    {
        var r = new AudioRoom(13, 9, seed: 13230);
        var mook = r.In(() => Dm.@new<mob_living_basic_mining_mook_worker>(locate(4, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(mook.death(FALSE)));
        Assert.Contains("machine made", r.In(() => mook.desc));
        r.AssertNoRuntimes();
    }
}
