using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.M4bAudio;
using Xunit;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The monster organs: they decay in four minutes unless stabilized or implanted; the regenerative core heals in hand and
/// revives when implanted; the brimdust sac coats its user in explosive dust; the rush gland gives a burst of speed.
/// </summary>
public class MonsterOrganTests
{
    private const int Second = 20;

    private static void RunFor(AudioRoom r, float deciseconds)
    {
        float end = r.In(() => world.time) + deciseconds;
        while (r.In(() => world.time) < end)
            r.Step();
    }

    [Fact]
    public void A_regenerative_core_heals_its_user_and_is_used_up()
    {
        var r = new AudioRoom(13, 9, seed: 13131);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => Waiting.NoSleep(bob.adjust_brute_loss(60)));
        var core = r.In(() => Dm.@new<obj_item_organ_monster_core_regenerative_core_legion>(locate(4, 4, 1)));
        r.Step(2);
        float before = r.In(() => Dm.num(bob.get_brute_loss()));
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(core)));
        r.In(() => Waiting.NoSleep(core.attack_self(bob)));
        r.Step(2);
        Assert.True(r.In(() => Dm.num(bob.get_brute_loss())) < before, "the core healed Bob");
        Assert.True(r.In(() => bob.has_status_effect(typeof(datum_status_effect_regenerative_core)) != null));
        Assert.True(r.In(() => QDELETED(core)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_core_decays_after_four_minutes_and_a_stabilizer_keeps_it()
    {
        var r = new AudioRoom(13, 9, seed: 13132);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        var core = r.In(() => Dm.@new<obj_item_organ_monster_core_regenerative_core_legion>(locate(4, 4, 1)));
        var kept = r.In(() => Dm.@new<obj_item_organ_monster_core_regenerative_core_legion>(locate(4, 4, 1)));
        var serum = r.In(() => Dm.@new<obj_item_mining_stabilizer>(locate(4, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(serum.interact_with_atom(kept, bob)));
        Assert.Equal("legion_core_stable", r.In(() => kept.icon_state));
        RunFor(r, 4 * 60 * 10 + 20);
        Assert.True(r.In(() => Dm.truthy(core.inert)), "the core decayed");
        Assert.Equal("legion_core_decayed", r.In(() => core.icon_state));
        Assert.False(r.In(() => Dm.truthy(kept.inert)), "the stabilized core kept");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void An_implanted_core_revives_its_host_when_they_fall_into_crit()
    {
        var r = new AudioRoom(13, 9, seed: 13133);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        var core = r.In(() => Dm.@new<obj_item_organ_monster_core_regenerative_core>(locate(4, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(core.Insert(bob, TRUE)));
        Assert.False(r.In(() => Dm.truthy(core.decay_timer)), "implanting preserved the core");
        r.In(() => Waiting.NoSleep(bob.adjust_brute_loss(150)));
        RunFor(r, 40);
        Assert.True(r.In(() => Dm.num(bob.health)) > r.In(() => Dm.num(bob.crit_threshold)), "the core brought Bob back up");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_brimdust_sac_coats_its_user_and_the_dust_ignites_when_they_are_hit()
    {
        var r = new AudioRoom(13, 9, seed: 13134);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        mob_living_carbon_human next = r.Dummy(5, 4);
        var sac = r.In(() => Dm.@new<obj_item_organ_monster_core_brimdust_sac>(locate(4, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(sac)));
        r.In(() => Waiting.NoSleep(sac.attack_self(bob)));
        var dust = r.In(() => bob.has_status_effect(typeof(datum_status_effect_stacking_brimdust_coating)) as datum_status_effect_stacking_brimdust_coating);
        Assert.NotNull(dust);
        Assert.Equal(3f, r.In(() => Dm.num(dust.stacks)));
        float before = r.In(() => Dm.num(next.health));
        r.In(() => Waiting.NoSleep(bob.apply_damage(5, BRUTE)));
        RunFor(r, 10);
        Assert.True(r.In(() => Dm.num(next.health)) < before, "the dust exploded on Bob's neighbour");
        Assert.Equal(2f, r.In(() => Dm.num(dust.stacks)));
        Assert.True(r.In(() => bob.has_status_effect(typeof(datum_status_effect_brimdust_concussion)) != null), "under pressure the blast knocks its wearer off balance");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_rush_gland_speeds_up_its_user_for_thirty_seconds()
    {
        var r = new AudioRoom(13, 9, seed: 13135);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        var gland = r.In(() => Dm.@new<obj_item_organ_monster_core_rush_gland>(locate(4, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(gland)));
        r.In(() => Waiting.NoSleep(gland.attack_self(bob)));
        Assert.True(r.In(() => bob.has_status_effect(typeof(datum_status_effect_lobster_rush)) != null));
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_TENTACLE_IMMUNE)));
        RunFor(r, 31 * 10);
        Assert.False(r.In(() => bob.has_status_effect(typeof(datum_status_effect_lobster_rush)) != null), "the rush wore off");
        r.AssertNoRuntimes();
    }
}
