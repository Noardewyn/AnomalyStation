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
/// tg's legacy simple animal base, which the megafauna stand on: a simple animal lives, takes a man's punches and pets, dies into
/// its loot or leaves its body; a hostile one, driven by SSnpcpool, finds a man, closes in and bites him, and a lazarus injector
/// brings it back friendly to its reviver.
/// </summary>
public class SimpleAnimalTests
{
    private const int Second = 20;

    private static void RunFor(AudioRoom r, float deciseconds)
    {
        float end = r.In(() => world.time) + deciseconds;
        while (r.In(() => world.time) < end)
            r.Step();
    }

    private static bool RunUntil(AudioRoom r, float deciseconds, Func<bool> done)
    {
        float end = r.In(() => world.time) + deciseconds;
        while (r.In(() => world.time) < end)
        {
            if (r.In(done))
                return true;
            r.Step();
        }
        return r.In(done);
    }

    [Fact]
    public void A_simple_animal_dies_into_its_loot_or_leaves_its_body()
    {
        var r = new AudioRoom(11, 9, seed: 13341);
        var gone = r.In(() =>
        {
            var a = Dm.@new<mob_living_simple_animal>(locate(4, 4, 1));
            a.loot = Dm.list(typeof(obj_item_stack_sheet_bone));
            a.del_on_death = TRUE;
            return a;
        });
        var stays = r.In(() =>
        {
            var a = Dm.@new<mob_living_simple_animal>(locate(7, 4, 1));
            a.icon_dead = "dead";
            return a;
        });
        r.Step(2);
        Assert.False(r.In(() => InertSeam.IsInert(gone)), "the simple animal is inert");
        r.In(() =>
        {
            Waiting.Detach(gone.adjust_brute_loss(Dm.num(gone.maxHealth) + 1));
            Waiting.Detach(stays.adjust_brute_loss(Dm.num(stays.maxHealth) + 1));
        });
        Assert.True(RunUntil(r, 20, () => QDELETED(gone)), "the first one went away");
        Assert.True(r.In(() => Dm.each<atom>(locate(4, 4, 1)).Any(a => a is obj_item_stack_sheet_bone)), "its loot dropped");
        Assert.Equal(DEAD, r.In(() => stays.stat));
        Assert.Equal("dead", r.In(() => stays.icon_state));
        Assert.True(r.In(() => HAS_TRAIT(stays, TRAIT_UNDENSE)), "the body lies down");
        r.In(() => Waiting.NoSleep(stays.revive(ADMIN_HEAL_ALL)));
        Assert.Equal(STABLE, r.In(() => stays.stat));
        Assert.Equal(r.In(() => Dm.num(stays.maxHealth)), r.In(() => Dm.num(stays.health)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_man_pets_a_simple_animal_and_punches_it_in_combat()
    {
        var r = new AudioRoom(11, 9, seed: 13342);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        var animal = r.In(() => Dm.@new<mob_living_simple_animal>(locate(5, 4, 1)));
        r.Step(2);
        float before = r.In(() => Dm.num(animal.health));
        r.In(() => Waiting.NoSleep(animal.attack_hand(bob)));
        Assert.Equal(before, r.In(() => Dm.num(animal.health)));
        r.In(() => bob.set_combat_mode(TRUE));
        r.In(() => bob.next_move = 0);
        r.In(() => Waiting.NoSleep(animal.attack_hand(bob)));
        Assert.True(r.In(() => Dm.num(animal.health)) < before, "the punch hurt it");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_hostile_simple_animal_hunts_a_man_and_bites_him()
    {
        var r = new AudioRoom(13, 9, seed: 13343);
        mob_living_carbon_human bob = r.Dummy(9, 4);
        var hostile = r.In(() =>
        {
            var h = Dm.@new<mob_living_simple_animal_hostile>(locate(3, 4, 1));
            h.melee_damage_lower = 5;
            h.melee_damage_upper = 5;
            return h;
        });
        Assert.NotNull(r.In(() => SSnpcpool));
        float before = r.In(() => Dm.num(bob.health));
        Assert.True(RunUntil(r, 20 * 10, () => hostile.target == bob), "it found Bob");
        Assert.True(RunUntil(r, 30 * 10, () => Dm.num(bob.health) < before), "it bit Bob");
        Assert.True(r.In(() => get_dist(hostile, bob)) <= 1, "it closed in");
        r.In(() => qdel(bob));
        RunFor(r, 40);
        Assert.Null(r.In(() => hostile.target));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_lazarus_injector_brings_a_hostile_back_friendly_to_its_reviver()
    {
        var r = new AudioRoom(11, 9, seed: 13344);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        var hostile = r.In(() => Dm.@new<mob_living_simple_animal_hostile>(locate(6, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.Detach(hostile.adjust_brute_loss(Dm.num(hostile.maxHealth) + 1)));
        Assert.True(RunUntil(r, 20, () => hostile.stat == DEAD), "it died");
        r.In(() => Waiting.Detach(hostile.lazarus_revive(bob, FALSE)));
        Assert.True(RunUntil(r, 20, () => hostile.stat == STABLE), "it came back");
        Assert.Equal(TRUE, r.In(() => hostile.faction_check_atom(bob)));
        Assert.Equal(r.In(() => Dm.num(Dm.initial(hostile, "robust_searching"))), r.In(() => Dm.num(hostile.robust_searching)));
        r.In(() => Waiting.Detach(hostile.adjust_brute_loss(Dm.num(hostile.maxHealth) + 1)));
        Assert.True(RunUntil(r, 20, () => hostile.stat == DEAD), "it died again");
        r.In(() => Waiting.Detach(hostile.lazarus_revive(bob, TRUE)));
        Assert.True(RunUntil(r, 20, () => hostile.stat == STABLE), "the faulty injector brought it back");
        Assert.Equal(TRUE, r.In(() => hostile.robust_searching));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_gibbed_simple_animal_plays_its_gib_animation()
    {
        var r = new AudioRoom(11, 9, seed: 13345);
        var animal = r.In(() =>
        {
            var a = Dm.@new<mob_living_simple_animal>(locate(5, 4, 1));
            a.icon_gib = "gibbed";
            return a;
        });
        r.Step(2);
        // gibbed standing: a body already lying plays no animation (death.dm:15)
        r.In(() => Waiting.Detach(animal.gib()));
        Assert.True(RunUntil(r, 10, () => Dm.each<atom>(range(1, locate(5, 4, 1))).Any(a => a is obj_effect_temp_visual_gib_animation_animal)), "the gib animation played");
        Assert.True(RunUntil(r, 20, () => QDELETED(animal)), "the animal is gone");
        r.AssertNoRuntimes();
    }
}
