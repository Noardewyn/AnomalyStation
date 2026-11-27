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
/// The proto-kinetic crusher: wielded, its right click fires the destabilizing force that marks a large creature; a melee hit
/// detonates the mark for tg's 50 extra damage, 30 more from behind; trophies attach, change it and come off with a crowbar;
/// the retool kits reskin it.
/// </summary>
public class KineticCrusherTests
{
    private const int Second = 20;

    private static void RunFor(AudioRoom r, float deciseconds)
    {
        float end = r.In(() => world.time) + deciseconds;
        while (r.In(() => world.time) < end)
            r.Step();
    }

    private static obj_item_kinetic_crusher Wielded(AudioRoom r, mob_living_carbon_human who)
    {
        var crusher = r.In(() => Dm.@new<obj_item_kinetic_crusher>(who.loc));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_active_hand(crusher))));
            Waiting.NoSleep(crusher.attack_self(who));
        });
        Assert.True(r.In(() => HAS_TRAIT(crusher, TRAIT_WIELDED)), "the crusher is wielded");
        return crusher;
    }

    [Fact]
    public void Unwielded_the_crusher_does_not_swing_and_does_not_fire()
    {
        var r = new AudioRoom(13, 9, seed: 13151);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        var crusher = r.In(() => Dm.@new<obj_item_kinetic_crusher>(bob.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(crusher)));
        mob_living_carbon_human target = r.Dummy(8, 4);
        object result = r.In(() => Waiting.NoSleep(crusher.interact_with_atom_secondary(target, bob)));
        Assert.Equal(ITEM_INTERACT_BLOCKING, result);
        Assert.True(r.In(() => Dm.truthy(crusher.charged)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_right_click_fires_a_destabilizer_that_recharges_in_a_second_and_a_half()
    {
        var r = new AudioRoom(13, 9, seed: 13152);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        var crusher = Wielded(r, bob);
        atom far = r.In(() => locate(10, 4, 1));
        r.In(() => Waiting.NoSleep(crusher.interact_with_atom_secondary(far, bob)));
        Assert.False(r.In(() => Dm.truthy(crusher.charged)), "the crusher fired");
        RunFor(r, 16);
        Assert.True(r.In(() => Dm.truthy(crusher.charged)), "it recharged");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_destabilizer_marks_a_large_creature_and_a_melee_hit_detonates_it()
    {
        var r = new AudioRoom(13, 9, seed: 13153);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        var crusher = Wielded(r, bob);
        var goliath = r.In(() => Dm.@new<mob_living_basic_mining_goliath_ancient_immortal>(locate(4, 4, 1)));
        r.In(() => goliath.ai_controller = null);
        r.Step(2);
        r.In(() =>
        {
            var shot = Dm.@new<obj_projectile_destabilizer>(locate(3, 4, 1));
            shot.firer = bob;
            shot.fired_from = crusher;
            Waiting.NoSleep(shot.on_hit(goliath));
        });
        var mark = r.In(() => goliath.has_status_effect(typeof(datum_status_effect_crusher_mark)) as datum_status_effect_crusher_mark);
        Assert.NotNull(mark);
        float before = r.In(() => Dm.num(goliath.health));
        r.In(() => Waiting.NoSleep(crusher.afterattack(goliath, bob)));
        Assert.Null(r.In(() => goliath.has_status_effect(typeof(datum_status_effect_crusher_mark))));
        float dealt = before - r.In(() => Dm.num(goliath.health));
        Assert.True(dealt > 0, "the detonation hurt the goliath");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_small_creature_takes_no_mark()
    {
        var r = new AudioRoom(13, 9, seed: 13154);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        mob_living_carbon_human small = r.Dummy(6, 4);
        r.In(() => Waiting.NoSleep(small.apply_status_effect(typeof(datum_status_effect_crusher_mark), FALSE, null)));
        Assert.Null(r.In(() => small.has_status_effect(typeof(datum_status_effect_crusher_mark))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_backstab_adds_thirty_damage_to_the_detonation()
    {
        var r = new AudioRoom(13, 9, seed: 13155);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        var crusher = Wielded(r, bob);
        float Detonate(bool behind)
        {
            var target = r.In(() => Dm.@new<mob_living_basic_mining_goliath_ancient_immortal>(locate(4, 4, 1)));
            r.In(() =>
            {
                target.ai_controller = null;
                target.dir = behind ? EAST : WEST; // facing away from Bob at its west, or towards him
            });
            r.Step(2);
            var mark = r.In(() => Waiting.NoSleep(target.apply_status_effect(typeof(datum_status_effect_crusher_mark), FALSE, crusher)) as datum_status_effect_crusher_mark);
            float before = r.In(() => Dm.num(target.health));
            r.In(() => Waiting.NoSleep(mark.detonate(crusher, bob)));
            float dealt = before - r.In(() => Dm.num(target.health));
            r.In(() => qdel(target));
            r.Step(2);
            return dealt;
        }
        float front = Detonate(false);
        float back = Detonate(true);
        Assert.True(back > front, "from behind " + back + ", from the front " + front);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Trophies_attach_change_the_crusher_and_come_off_with_a_crowbar()
    {
        var r = new AudioRoom(13, 9, seed: 13156);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        var crusher = r.In(() => Dm.@new<obj_item_kinetic_crusher>(bob.loc));
        var skull = r.In(() => Dm.@new<obj_item_crusher_trophy_legion_skull>(bob.loc));
        var second = r.In(() => Dm.@new<obj_item_crusher_trophy_legion_skull>(bob.loc));
        var crowbar = r.In(() => Dm.@new<obj_item_crowbar>(bob.loc));
        r.Step(2);
        float charge = r.In(() => crusher.charge_time);
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(skull)));
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(skull.add_to(crusher, bob)))));
        Assert.Equal(charge - 3, r.In(() => crusher.charge_time));
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(second)));
        Assert.False(r.In(() => Dm.truthy(Waiting.NoSleep(second.add_to(crusher, bob)))), "a second skull is denied");
        r.In(() => Waiting.NoSleep(crusher.crowbar_act(bob, crowbar)));
        Assert.Equal(charge, r.In(() => crusher.charge_time));
        Assert.Equal(0, r.In(() => Dm.length(crusher.trophies)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_retool_kit_reskins_the_crusher_and_the_skin_outlives_the_kit()
    {
        var r = new AudioRoom(13, 9, seed: 13157);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        var crusher = r.In(() => Dm.@new<obj_item_kinetic_crusher>(bob.loc));
        var kit = r.In(() => Dm.@new<obj_item_crusher_trophy_retool_kit_ashenskull>(bob.loc));
        r.Step(2);
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(kit)));
        r.In(() => Waiting.NoSleep(kit.add_to(crusher, bob)));
        Assert.Equal("crusher_skull", r.In(() => crusher.icon_state));
        Assert.Equal("pulse_skull", r.In(() => crusher.projectile_icon_state));
        // tg: the kit's removal re-adds the reskin component with no skin, which the existing one absorbs without clearing the
        // applied skull (reskinnable_atom.dm:183-196); the skull is kept until the crusher is reskinned
        r.In(() => Waiting.NoSleep(kit.remove_from(crusher, bob)));
        Assert.Equal("crusher_skull", r.In(() => crusher.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_goliath_tentacle_trophy_adds_damage_for_missing_health()
    {
        var r = new AudioRoom(13, 9, seed: 13158);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        var trophy = r.In(() => Dm.@new<obj_item_crusher_trophy_goliath_tentacle>(bob.loc));
        var goliath = r.In(() => Dm.@new<mob_living_basic_mining_goliath_ancient_immortal>(locate(4, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(bob.adjust_brute_loss(50)));
        float missing = r.In(() => Dm.num(bob.maxHealth) - Dm.num(bob.health));
        object bonus = r.In(() => Waiting.NoSleep(trophy.on_mark_detonation(goliath, bob, null)));
        Assert.True(missing > 0);
        Assert.Equal(missing * 0.1f * 2, Dm.num(bonus), 2);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_crusher_examines_with_its_damage_and_its_trophies()
    {
        var r = new AudioRoom(13, 9, seed: 13159);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        var crusher = r.In(() => Dm.@new<obj_item_kinetic_crusher>(bob.loc));
        r.Step(2);
        string text = r.In(() => string.Join("\n", Dm.each<object>(Waiting.NoSleep(crusher.examine(bob)) as DmList).Select(x => x?.ToString())));
        Assert.Contains("70", text);
        Assert.Contains("100", text);
        r.AssertNoRuntimes();
    }
}
