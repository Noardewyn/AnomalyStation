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
/// Lavaland's common fauna: the goliath, the watchers, the legions with their skulls and corpses, the hivelord, the basilisk,
/// the brimdemon, the bileworm, the lobstrosities and the NODE drone. Each spawns, thinks and goes away cleanly; each one's
/// attack or ability does what tg's does; each dies with its loot, and drops its crusher trophy when a crusher did the work.
/// </summary>
public class LavalandFaunaTests
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

    public static IEnumerable<object[]> Fauna() => new[]
    {
        typeof(mob_living_basic_mining_goliath), typeof(mob_living_basic_mining_goliath_ancient), typeof(mob_living_basic_mining_goliath_ancient_immortal), typeof(mob_living_basic_mining_goliath_deathmatch),
        typeof(mob_living_basic_mining_watcher), typeof(mob_living_basic_mining_watcher_magmawing), typeof(mob_living_basic_mining_watcher_icewing),
        typeof(mob_living_basic_mining_legion), typeof(mob_living_basic_mining_legion_snow), typeof(mob_living_basic_mining_legion_dwarf), typeof(mob_living_basic_mining_legion_large),
        typeof(mob_living_basic_mining_legion_lesser), typeof(mob_living_basic_mining_legion_lesser_snow), typeof(mob_living_basic_mining_legion_spawner_made), typeof(mob_living_basic_mining_legion_snow_spawner_made),
        typeof(mob_living_basic_mining_legion_brood), typeof(mob_living_basic_mining_legion_brood_snow),
        typeof(mob_living_basic_mining_hivelord), typeof(mob_living_basic_hivelord_brood), typeof(mob_living_basic_mining_basilisk), typeof(mob_living_basic_mining_brimdemon),
        typeof(mob_living_basic_mining_bileworm), typeof(mob_living_basic_mining_bileworm_vileworm),
        typeof(mob_living_basic_mining_lobstrosity), typeof(mob_living_basic_mining_lobstrosity_lava), typeof(mob_living_basic_mining_lobstrosity_juvenile), typeof(mob_living_basic_mining_lobstrosity_juvenile_lava),
        typeof(mob_living_basic_node_drone),
    }.Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(Fauna))]
    public void A_lavaland_creature_spawns_hunts_on_its_own_tree_and_goes_away_cleanly(Type path)
    {
        var r = new AudioRoom(13, 9, seed: 13101);
        mob_living_carbon_human bob = r.Dummy(10, 4);
        var mob = (mob_living)r.In(() => Dm.@new(path, locate(4, 4, 1)));
        Assert.False(r.In(() => InertSeam.IsInert(mob)), path.Name + " is inert");
        Assert.NotNull(mob.ai_controller);
        RunFor(r, 100);
        // Bob leaves and the creature calms down (a charge or a beam in flight would outlive its deleted owner, as in tg)
        r.In(() => qdel(bob));
        RunFor(r, 100);
        if (!r.In(() => QDELETED(mob)))
            r.In(() => qdel(mob));
        RunFor(r, 20);
        r.AssertNoRuntimes();
    }

    private static bool OnFloor<T>(AudioRoom r, int dist, atom center) where T : class =>
        r.In(() => Dm.each<atom>(range(dist, center)).Any(a => a is T));

    // The fauna's trophies are crafted from their loot with a crusher as the tool (crafting/melee_weapon.dm:291-365); the brimdemon
    // fang's recipe asks for brimdust and is left out with every recipe needing a reagent (global_lists.dm:20).
    [Theory]
    [InlineData(typeof(datum_crafting_recipe_crusher_trophy_goliath_tentacle), typeof(obj_item_crusher_trophy_goliath_tentacle))]
    [InlineData(typeof(datum_crafting_recipe_crusher_trophy_watcher_wing), typeof(obj_item_crusher_trophy_watcher_wing))]
    [InlineData(typeof(datum_crafting_recipe_crusher_trophy_legion_skull), typeof(obj_item_crusher_trophy_legion_skull))]
    [InlineData(typeof(datum_crafting_recipe_crusher_trophy_lobster_claw), typeof(obj_item_crusher_trophy_lobster_claw))]
    [InlineData(typeof(datum_crafting_recipe_crusher_trophy_bileworm_spewlet), typeof(obj_item_crusher_trophy_bileworm_spewlet))]
    public void A_trophy_is_crafted_from_the_creatures_loot_with_a_crusher(Type recipePath, Type trophy)
    {
        var r = new AudioRoom(13, 9, seed: 13102);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.Step(Second);
        var recipe = r.In(() => GLOB.crafting_recipes_by_typepath[recipePath] as datum_crafting_recipe);
        Assert.NotNull(recipe);
        r.In(() =>
        {
            Dm.@new<obj_item_kinetic_crusher>(locate(5, 4, 1));
            foreach (object part in Dm.each(recipe.reqs))
            {
                var type = (Type)part;
                int amount = (int)Dm.num(recipe.reqs[part]);
                if (typeof(obj_item_stack).IsAssignableFrom(type))
                    Dm.@new(type, locate(5, 4, 1), amount, FALSE);
                else
                    for (int i = 0; i < amount; i++)
                        Dm.@new(type, locate(5, 4, 1));
            }
        });
        r.Step(2);
        var crafting = r.In(() => bob.GetComponent(typeof(datum_component_personal_crafting)) as datum_component_personal_crafting);
        object made = null;
        r.Command(() => Waiting.Detach(Make()));
        async DmTask Make() => made = await crafting.construct_item(bob, recipe);
        float end = r.In(() => world.time) + 200;
        while (made == null && r.In(() => world.time) < end)
            r.Step();
        Assert.True(made != null && made.GetType() == trophy, recipePath.Name + " made " + made);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_dead_goliath_is_butchered_for_its_hide()
    {
        var r = new AudioRoom(13, 9, seed: 13103);
        var goliath = r.In(() => Dm.@new<mob_living_basic_mining_goliath>(locate(6, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(goliath.death(FALSE)));
        RunFor(r, 10);
        Assert.Equal(1f, r.In(() => Dm.num(goliath.guaranteed_butcher_results[typeof(obj_item_stack_sheet_animalhide_goliath_hide)])));
        Assert.False(OnFloor<obj_item_crusher_trophy>(r, 3, r.In(() => locate(6, 4, 1))), "no trophy drops by itself");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_goliaths_tentacles_rise_under_its_target_grab_and_hurt_it()
    {
        var r = new AudioRoom(13, 9, seed: 13104);
        mob_living_carbon_human bob = r.Dummy(8, 4);
        var goliath = r.In(() => Dm.@new<mob_living_basic_mining_goliath>(locate(4, 4, 1)));
        r.Step(2);
        float before = r.In(() => Dm.num(bob.health));
        r.In(() => Waiting.NoSleep(goliath.tentacles.Trigger(null, null, bob)));
        Assert.True(r.In(() => Dm.each<atom>(range(1, bob)).Any(a => a is obj_effect_goliath_tentacle)), "a tentacle rose at Bob");
        Assert.True(RunUntil(r, 40, () => Dm.num(bob.health) < before), "the tentacle hurt Bob");
        Assert.True(r.In(() => bob.legcuffed is obj_item_restraints_legcuffs_goliath_tentacle || Dm.truthy(bob.IsStun()) || Dm.truthy(bob.IsParalyzed()) || Dm.num(bob.health) < before));
        RunFor(r, 100);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_goliath_bites_what_stands_next_to_it()
    {
        var r = new AudioRoom(13, 9, seed: 13105);
        r.In(() => r.World.Rng.Reseed(13105, 1));
        mob_living_carbon_human bob = r.Join(5, 4, "Bob");
        var goliath = r.In(() => Dm.@new<mob_living_basic_mining_goliath>(locate(4, 4, 1)));
        float before = r.In(() => Dm.num(bob.health));
        Assert.True(RunUntil(r, 30 * 10, () => Dm.num(bob.health) < before), "the goliath attacked");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_watchers_gaze_flashes_those_looking_at_it_and_its_beam_freezes()
    {
        var r = new AudioRoom(13, 9, seed: 13106);
        mob_living_carbon_human bob = r.Dummy(8, 4);
        var watcher = r.In(() => Dm.@new<mob_living_basic_mining_watcher>(locate(4, 4, 1)));
        r.Step(2);
        var gaze = r.In(() => Dm.each<datum_action_cooldown_mob_cooldown_watcher_gaze>(watcher.actions).First());
        r.In(() => Waiting.Detach(gaze.Trigger(null, null, bob)));
        RunFor(r, 40);
        Assert.True(r.In(() => gaze.next_use_time > world.time), "the gaze went on cooldown");

        // its frost shot chills
        float temp = r.In(() => Dm.num(bob.bodytemperature));
        r.In(() =>
        {
            var shot = Dm.@new<obj_projectile_temp_watcher>(locate(6, 4, 1));
            Waiting.NoSleep(shot.on_hit(bob));
        });
        Assert.True(r.In(() => Dm.num(bob.bodytemperature)) < temp, "the watcher's shot cooled Bob");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_legion_launches_skulls_and_a_skull_turns_a_corpse_into_a_legion()
    {
        var r = new AudioRoom(13, 9, seed: 13107);
        mob_living_carbon_human bob = r.Dummy(9, 4);
        var legion = r.In(() => Dm.@new<mob_living_basic_mining_legion>(locate(4, 4, 1)));
        r.Step(2);
        var launcher = r.In(() => Dm.each<datum_action_cooldown_mob_cooldown_skull_launcher>(legion.actions).First());
        r.In(() => Waiting.NoSleep(launcher.Trigger(null, null, bob)));
        Assert.True(RunUntil(r, 60, () => Dm.each<atom>(range(6, locate(6, 4, 1))).Any(a => a is mob_living_basic_mining_legion_brood)), "a skull flew out");

        // a skull on a body infests it
        mob_living_carbon_human corpse = r.Dummy(2, 7);
        r.In(() => Waiting.NoSleep(corpse.death(FALSE)));
        var skull = r.In(() => Dm.@new<mob_living_basic_mining_legion_brood>(locate(2, 6, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(skull.melee_attack(corpse)));
        RunFor(r, 10);
        Assert.True(r.In(() => Dm.each<atom>(range(2, locate(2, 7, 1))).Any(a => a is mob_living_basic_mining_legion)), "the corpse became a legion");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_dead_legion_leaves_its_regenerative_core_and_its_corpse()
    {
        var r = new AudioRoom(13, 9, seed: 13108);
        var legion = r.In(() => Dm.@new<mob_living_basic_mining_legion>(locate(6, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(legion.death(FALSE)));
        RunFor(r, 10);
        atom spot = r.In(() => locate(6, 4, 1));
        Assert.True(OnFloor<obj_item_organ_monster_core_regenerative_core_legion>(r, 2, spot), "a legion core dropped");
        Assert.True(OnFloor<mob_living_carbon_human>(r, 2, spot), "the legion's corpse dropped");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_hivelord_spawns_broods_and_dies_into_its_core()
    {
        var r = new AudioRoom(13, 9, seed: 13109);
        mob_living_carbon_human bob = r.Dummy(9, 4);
        var hivelord = r.In(() => Dm.@new<mob_living_basic_mining_hivelord>(locate(4, 4, 1)));
        r.Step(2);
        r.In(() => Waiting.NoSleep(hivelord.spawn_brood.Trigger(null, null, bob)));
        Assert.True(RunUntil(r, 40, () => Dm.each<atom>(range(8, locate(4, 4, 1))).Any(a => a is mob_living_basic_hivelord_brood)), "a brood spawned");
        r.In(() => Waiting.NoSleep(hivelord.death(FALSE)));
        RunFor(r, 10);
        Assert.True(OnFloor<obj_item_organ_monster_core_regenerative_core>(r, 2, r.In(() => locate(4, 4, 1))), "a regenerative core dropped");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_basilisk_overheats_when_burnt_and_shoots_hot()
    {
        var r = new AudioRoom(13, 9, seed: 13110);
        var basilisk = r.In(() => Dm.@new<mob_living_basic_mining_basilisk>(locate(4, 4, 1)));
        r.Step(2);
        r.In(() =>
        {
            var laser = Dm.@new<obj_projectile_beam_laser>(locate(5, 4, 1));
            Waiting.NoSleep(basilisk.bullet_act(laser));
        });
        r.Step(2);
        Assert.True(r.In(() => basilisk.has_status_effect(typeof(datum_status_effect_basilisk_overheat)) != null), "a laser overheated the basilisk");
        RunFor(r, 30 * 10);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_brimdemons_beam_burns_along_its_line()
    {
        var r = new AudioRoom(13, 9, seed: 13111);
        mob_living_carbon_human bob = r.Dummy(8, 4);
        var demon = r.In(() => Dm.@new<mob_living_basic_mining_brimdemon>(locate(4, 4, 1)));
        r.Step(2);
        r.In(() => demon.dir = EAST);
        float before = r.In(() => Dm.num(bob.health));
        r.In(() => Waiting.Detach(demon.beam.Trigger(null, null, bob)));
        Assert.True(RunUntil(r, 80, () => Dm.each<atom>(range(4, locate(6, 4, 1))).Any(a => a is obj_effect_brimbeam)), "the beam fired");
        Assert.True(RunUntil(r, 80, () => Dm.num(bob.health) < before), "the beam burnt Bob");
        RunFor(r, 100);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_bileworm_spits_bile_and_burrows_away()
    {
        var r = new AudioRoom(13, 9, seed: 13112);
        mob_living_carbon_human bob = r.Dummy(8, 4);
        var worm = r.In(() => Dm.@new<mob_living_basic_mining_bileworm>(locate(4, 4, 1)));
        r.Step(2);
        var spew = r.In(() => Dm.each<datum_action_cooldown_mob_cooldown_bileworm_spew>(worm.actions).First());
        r.In(() => Waiting.Detach(spew.Trigger(null, null, bob)));
        RunFor(r, 40);
        Assert.True(r.In(() => spew.next_use_time > world.time), "the spew went on cooldown");
        var resurface = r.In(() => Dm.each<datum_action_cooldown_mob_cooldown_resurface>(worm.actions).First());
        r.In(() => Waiting.Detach(resurface.Trigger(null, null, bob)));
        RunFor(r, 60);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_lobstrosity_charges_and_snips()
    {
        var r = new AudioRoom(13, 9, seed: 13113);
        mob_living_carbon_human bob = r.Dummy(9, 4);
        var lobster = r.In(() => Dm.@new<mob_living_basic_mining_lobstrosity>(locate(4, 4, 1)));
        r.Step(2);
        var charge = r.In(() => Dm.each<datum_action_cooldown_mob_cooldown_charge_basic_charge_lobster>(lobster.actions).First());
        float before = r.In(() => Dm.num(bob.health));
        r.In(() => Waiting.Detach(charge.Trigger(null, null, bob)));
        RunFor(r, 60);
        Assert.True(r.In(() => charge.next_use_time > world.time), "the charge went on cooldown");
        RunFor(r, 100);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_node_drone_flies_in_takes_the_vents_health_and_flies_off()
    {
        var r = new AudioRoom(13, 9, seed: 13114);
        var vent = r.In(() => Dm.@new<obj_structure_ore_vent>(locate(6, 4, 1)));
        var drone = r.In(() => Dm.@new<mob_living_basic_node_drone>(locate(6, 4, 1)));
        r.In(() => drone.arrive(vent));
        Assert.Equal("mining_node_flying", r.In(() => drone.icon_state));
        Assert.Equal(300 + r.In(() => vent.boulder_size) / BOULDER_SIZE_SMALL * 100, r.In(() => Dm.num(drone.maxHealth)));
        r.In(() => drone.pre_escape(TRUE));
        Assert.True(RunUntil(r, 100, () => QDELETED(drone)), "the drone left");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_happiness_of_a_lobstrosity_rises_with_food_and_pets()
    {
        var r = new AudioRoom(13, 9, seed: 13115);
        mob_living_carbon_human bob = r.Dummy(5, 4);
        var juvenile = r.In(() => Dm.@new<mob_living_basic_mining_lobstrosity_juvenile>(locate(4, 4, 1)));
        r.Step(2);
        var happiness = r.In(() => juvenile.GetComponent(typeof(datum_component_happiness)) as datum_component_happiness);
        if (happiness == null)
            return; // tg gives the happiness only to some juveniles
        r.In(() => happiness.change_happiness_level(100));
        Assert.True(r.In(() => Dm.num(happiness.happiness_level)) > 0);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Loot_uses_goliath_plates_go_onto_an_explorer_suit()
    {
        var r = new AudioRoom(13, 9, seed: 13116);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        var suit = r.In(() => Dm.@new<obj_item_clothing_suit_hooded_explorer>(locate(4, 4, 1)));
        var plates = r.In(() => Dm.@new<obj_item_stack_sheet_animalhide_goliath_hide>(locate(4, 4, 1), 3));
        r.Step(2);
        float melee = r.In(() => Dm.num(suit.get_armor_rating(MELEE)));
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(plates)));
        r.In(() => Waiting.NoSleep(suit.attackby(plates, bob)));
        Assert.True(r.In(() => Dm.num(suit.get_armor_rating(MELEE))) > melee, "a plate armoured the suit");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_wave_defence_brings_the_node_drone_and_the_fauna()
    {
        var r = new AudioRoom(13, 13, seed: 13117);
        var vent = r.In(() => Dm.@new<obj_structure_ore_vent>(locate(7, 7, 1)));
        r.Step(2);
        r.In(() =>
        {
            vent.node = Dm.@new<mob_living_basic_node_drone>(vent.loc);
            vent.node.arrive(vent);
            Waiting.NoSleep(vent.start_wave_defense());
        });
        Assert.NotNull(r.In(() => vent.GetComponent(typeof(datum_component_spawner))));
        Assert.True(RunUntil(r, 30 * 10, () => Dm.each<atom>(range(6, locate(7, 7, 1))).Any(a => a is mob_living_basic_mining)), "the vent's defenders came");
        r.AssertNoRuntimes();
    }
}
