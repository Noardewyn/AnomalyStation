using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using Xunit.Abstractions;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The code of Lavaland's ruins: the sliding puzzle and its necropolis chest, the prison cube, gluttony's wall, envy's knife, greed's
/// cursed slot machine and its bag of money, the freeze cube, the watcher egg and its hatchling, the ruins' step triggers, the
/// firepit, the cult airlock, the gas miner, the all-terrain vehicle; and a sweep over every type the ruins made live.
/// </summary>
public class LavalandRuinTests
{
    private readonly ITestOutputHelper _output;

    public LavalandRuinTests(ITestOutputHelper output) => _output = output;

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

    private static void Jumpsuit(mob_living_carbon_human wearer)
    {
        if (wearer.w_uniform == null)
            Waiting.NoSleep(wearer.equip_to_slot_or_del(Dm.@new<obj_item_clothing_under_color_grey>(wearer.loc), ITEM_SLOT_ICLOTHING));
    }

    private static T Held<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        var thing = Dm.@new<T>(who.loc);
        Assert.True(Dm.truthy(Waiting.NoSleep(who.put_in_active_hand(thing))));
        return thing;
    });

    /// Puts every pillar on the tile its piece of the picture belongs to (the solved puzzle).
    private static void Solve(AudioRoom r, obj_effect_sliding_puzzle puzzle) => r.In(() =>
    {
        foreach (obj_structure_puzzle_element e in Dm.each<obj_structure_puzzle_element>(puzzle.elements.Copy()))
        {
            if (QDELETED(e) || Dm.truthy(puzzle.finished))
                continue;
            Waiting.NoSleep(e.forceMove(puzzle.get_turf_for_id(e.id)));
        }
    });

    [Fact]
    public void The_sliding_puzzle_scrambles_its_pillars_and_solving_it_collapses_them_into_its_reward()
    {
        var r = new AudioRoom(11, 11, seed: 13641);
        var puzzle = r.In(() => Dm.@new<obj_effect_sliding_puzzle_lavaland>(locate(6, 6, 1)));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal(8f, puzzle.elements.len);
            var ids = Dm.each<obj_structure_puzzle_element>(puzzle.elements).Select(e => (int)e.id).OrderBy(i => i).ToList();
            var expected = Enumerable.Range(1, 9).Where(i => i != (int)Dm.num(puzzle.empty_tile_id)).ToList();
            Assert.Equal(expected, ids);
            Assert.True(Dm.truthy(puzzle.is_solvable()), "the scramble can be solved");
            for (int id = 1; id <= 9; id++)
                Assert.IsType<turf_open_floor_vault>(puzzle.get_turf_for_id(id));
            Assert.All(Dm.each<obj_structure_puzzle_element>(puzzle.elements), e => Assert.Same(puzzle, e.source));
            Assert.All(Dm.each<obj_structure_puzzle_element>(puzzle.elements), e => Assert.NotEmpty(Dm.each(e.overlays).ToList()));
        });

        // a pillar can't be pushed off the puzzle's three by three
        var corner = r.In(() => Dm.each<obj_structure_puzzle_element>(puzzle.elements).First(e => get_dist(e, puzzle) == 1));
        r.In(() =>
        {
            object outward = get_dir(puzzle, corner);
            Assert.False(Dm.truthy(Waiting.NoSleep(corner.Move(get_step(corner, Dm.num(outward)), outward))), "a pillar stays on the puzzle");
        });

        Solve(r, puzzle);
        Assert.True(r.In(() => Dm.truthy(puzzle.finished)), "the puzzle is solved");
        Assert.True(RunUntil(r, 2 * Second, () => Dm.each<obj_structure_puzzle_element>().All(e => QDELETED(e))), "the pillars collapsed");
        Assert.True(r.In(() => Dm.each<obj_structure_closet_crate_necropolis_puzzle>(locate(6, 6, 1)).Any() || Dm.each<mob_living_simple_animal_hostile_megafauna_bubblegum>().Any()),
            "the necropolis chest (or, once in four, bubblegum) came of it");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_prison_cube_shuts_an_unconscious_prisoner_in_a_pillar_until_the_puzzle_is_solved()
    {
        var r = new AudioRoom(13, 13, seed: 13642);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(5, 7, "Bob");
        var al = r.Dummy(7, 7);
        r.In(() => Jumpsuit(bob));
        var cube = Held<obj_item_prisoncube>(r, bob);
        // only the restrained or the unconscious
        r.In(() => Waiting.NoSleep(cube.interact_with_atom(al, bob)));
        Assert.True(feed.Any("only accepts restrained or unconscious prisoners"));
        r.In(() => Waiting.NoSleep(al.Unconscious(100 * SECONDS)));
        r.In(() => Waiting.NoSleep(cube.interact_with_atom(al, bob)));
        Assert.True(feed.Any("You trap"), "the cube took Al");
        Assert.True(r.In(() => QDELETED(cube)));
        var prison = r.In(() => Dm.each<obj_effect_sliding_puzzle_prison>().Single());
        r.In(() =>
        {
            Assert.Same(al, prison.prisoner);
            Assert.IsType<obj_structure_puzzle_element_prison>(al.loc);
            Assert.True(HAS_TRAIT(al, TRAIT_NO_TRANSFORM));
            Assert.True(get_dist(bob, prison) > 1, "the cube threw Bob clear");
        });
        r.Step(Second);
        Solve(r, prison);
        r.Step(Second);
        r.In(() =>
        {
            Assert.Same(get_turf(prison), al.loc);
            Assert.False(HAS_TRAIT(al, TRAIT_NO_TRANSFORM), "Al is free");
            Assert.Null(prison.prisoner);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Gluttonys_wall_lets_through_only_the_overfed()
    {
        var r = new AudioRoom(11, 9, seed: 13643);
        Feed feed = Listen(r);
        r.In(() => Dm.@new<obj_effect_gluttony>(locate(5, 5, 1)));
        var thin = r.Join(4, 5, "Thin");
        var fat = r.Dummy(4, 4);
        r.In(() =>
        {
            Waiting.NoSleep(thin.set_nutrition(NUTRITION_LEVEL_FED));
            Assert.False(Dm.truthy(Waiting.NoSleep(thin.Move(locate(5, 5, 1), EAST))), "the thin are turned back");
            Waiting.NoSleep(fat.set_nutrition(NUTRITION_LEVEL_FAT + 10));
            Waiting.NoSleep(fat.forceMove(locate(4, 5, 1)));
            Waiting.NoSleep(thin.forceMove(locate(4, 4, 1)));
            Assert.True(Dm.truthy(Waiting.NoSleep(fat.Move(locate(5, 5, 1), EAST))), "the overfed push through");
        });
        Assert.True(feed.Any("Only a pig could force themselves to go through it."));
        Assert.True(feed.Any("pushes through"));
    }

    [Fact]
    public void Envys_knife_gives_its_wielder_the_look_of_whoever_it_strikes()
    {
        var r = new AudioRoom(11, 9, seed: 13644);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        var al = r.Dummy(6, 5);
        r.In(() =>
        {
            Jumpsuit(bob);
            bob.set_combat_mode(TRUE);
            al.real_name = "Al Envied";
            al.dna.real_name = "Al Envied";
        });
        var knife = Held<obj_item_knife_envy>(r, bob);
        Click(r, bob, al);
        r.In(() =>
        {
            Assert.Equal("Al Envied", bob.real_name);
            Assert.Equal(Dm.str(al.dna.unique_enzymes), Dm.str(bob.dna.unique_enzymes));
        });
        _ = knife;
    }

    [Fact]
    public void Greeds_slot_machine_curses_the_loser_and_turns_into_a_bag_of_money_for_the_winner()
    {
        var r = new AudioRoom(11, 9, seed: 13645);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(5, 5, "Bob");
        var machine = r.In(() => Dm.@new<obj_structure_cursed_slot_machine>(locate(6, 5, 1)));
        r.In(() => machine.win_prob = 0);
        r.In(() => Waiting.NoSleep(machine.interact(bob)));
        Assert.Equal("slots_screen_working", r.In(() => machine.icon_screen));
        Assert.Contains(r.Sounds.Heard, h => (h.File ?? "").Contains("cursed_slot_machine", StringComparison.Ordinal));
        // a second pull while it spins
        r.In(() => Waiting.NoSleep(machine.interact(bob)));
        Assert.True(feed.Any("balloon: already spinning!"));
        r.Step(6 * Second);
        r.In(() =>
        {
            Assert.Equal("slots_screen", machine.icon_screen);
            Assert.NotNull(bob.has_status_effect(typeof(datum_status_effect_slot_machine_curse)));
        });
        Assert.True(feed.Any("balloon: you lost!"));

        // the cooldown, then a winning pull
        r.Step(16 * Second);
        r.In(() => machine.win_prob = 100);
        r.In(() => Waiting.NoSleep(machine.interact(bob)));
        r.Step(6 * Second);
        Assert.True(r.In(() => QDELETED(machine)), "the machine went");
        var money = r.In(() => Dm.each<obj_structure_cursed_money>(locate(6, 5, 1)).Single());
        Assert.True(feed.Any("You've hit the jackpot!!!"));
        r.In(() => Waiting.NoSleep(money.attack_hand(bob)));
        Assert.True(r.In(() => QDELETED(money)), "the bag gave its die and went");
        Assert.True(r.In(() => Dm.each<obj_item_dice_d20_fate_one_use>().Any()), "the die of fate");
    }

    [Fact]
    public void The_freeze_cube_freezes_what_it_hits_in_an_ice_block()
    {
        var r = new AudioRoom(13, 9, seed: 13646);
        mob_living_carbon_human bob = r.Join(3, 5, "Bob");
        var al = r.Dummy(8, 5);
        var cube = r.In(() => Dm.@new<obj_item_freeze_cube>(locate(7, 5, 1)));
        r.In(() => cube.throw_impact(al, null));
        r.In(() => Assert.NotNull(al.has_status_effect(typeof(datum_status_effect_ice_block_talisman))));
        Assert.Contains(r.Sounds.Heard, h => (h.File ?? "").Contains("glassbr3", StringComparison.Ordinal));
        _ = bob;
    }

    [Fact]
    public void A_watcher_egg_carried_far_enough_hatches_a_watcher_that_flies_about_its_owner()
    {
        var r = new AudioRoom(13, 9, seed: 13647);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 5, "Bob");
        var egg = r.In(() => Dm.@new<obj_item_food_egg_watcher>(locate(4, 5, 1)));
        r.In(() => egg.steps_travelled = egg.steps_to_hatch - 1);
        r.In(() => Waiting.NoSleep(egg.forceMove(locate(5, 5, 1))));
        Assert.True(r.In(() => QDELETED(egg)), "the egg hatched");
        Assert.True(feed.Any("splits and unfurls into a baby Watcher!"));
        var hatchling = r.In(() => Dm.each<obj_item_watcher_hatchling>(locate(5, 5, 1)).Single());
        r.In(() => Waiting.NoSleep(hatchling.forceMove(bob.loc)));
        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_active_hand(hatchling)))));
        r.In(() => Waiting.NoSleep(hatchling.attack_self(bob)));
        r.Step(2);
        r.In(() =>
        {
            Assert.NotNull(hatchling.orbiter);
            Assert.Same(bob, hatchling.owner);
        });
        r.In(() => Waiting.NoSleep(hatchling.attack_self(bob)));
        r.Step(2);
        r.In(() => Assert.Null(hatchling.orbiter));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_step_trigger_tells_who_steps_on_it_once_and_the_cult_altars_whisper_plays_for_them_alone()
    {
        var r = new AudioRoom(11, 9, seed: 13648);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 5, "Bob");
        var message = r.In(() =>
        {
            var m = Dm.@new<obj_effect_step_trigger_message>(locate(4, 5, 1));
            m.message = "You feel watched.";
            return m;
        });
        var whisper = r.In(() => Dm.@new<obj_effect_step_trigger_sound_effect_lavaland_cult_altar>(locate(5, 5, 1)));
        r.In(() => Waiting.NoSleep(bob.Move(locate(4, 5, 1), EAST)));
        r.Step(2);
        Assert.True(feed.Any("You feel watched."));
        Assert.True(r.In(() => QDELETED(message)), "the message is told once");
        r.In(() => Waiting.NoSleep(bob.Move(locate(5, 5, 1), EAST)));
        r.Step(2);
        Assert.Contains(r.Sounds.Heard, h => (h.File ?? "").Contains("i_see_you1", StringComparison.Ordinal));
        Assert.True(r.In(() => QDELETED(whisper)), "the whisper happens once");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_firepit_starts_cold_and_a_flame_lights_it()
    {
        var r = new AudioRoom(11, 9, seed: 13649);
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        r.In(() => Jumpsuit(bob));
        var pit = r.In(() => Dm.@new<obj_structure_firepit>(locate(5, 5, 1)));
        r.Step(2);
        r.In(() =>
        {
            // tg's firepit is mapped lit and its Initialize toggles it: it starts cold
            Assert.Equal(0f, Dm.num(pit.active));
            Assert.Equal("firepit", pit.icon_state);
            Assert.Equal(0f, Dm.num(pit.light_range));
        });
        // tg's fire_act sets it lit and then toggles it back: a fire does not light it
        r.In(() => Waiting.NoSleep(pit.fire_act(1000, 100)));
        r.In(() => Assert.Equal("firepit", pit.icon_state));
        // a lit lighter does
        var lighter = Held<obj_item_lighter>(r, bob);
        r.In(() => lighter.set_lit(TRUE));
        r.In(() => Waiting.NoSleep(pit.item_interaction(bob, lighter)));
        r.In(() =>
        {
            Assert.Equal(1f, Dm.num(pit.active));
            Assert.Equal("firepit-active", pit.icon_state);
            Assert.Equal(8f, Dm.num(pit.light_range));
        });
        // and tg's hand and extinguish set it out and toggle it back: it burns on
        r.In(() => Waiting.NoSleep(pit.interact(bob)));
        r.In(() => Assert.Equal("firepit-active", pit.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_cult_airlock_opens_for_cultists_and_flings_back_everyone_else()
    {
        var r = new AudioRoom(13, 9, seed: 13650);
        var door = r.In(() => Dm.@new<obj_machinery_door_airlock_cult_unruned>(locate(6, 5, 1)));
        var friendly = r.In(() => Dm.@new<obj_machinery_door_airlock_cult_unruned_friendly>(locate(6, 3, 1)));
        var al = r.Dummy(5, 5);
        r.Step(2);
        r.In(() =>
        {
            Assert.False(Dm.truthy(Waiting.NoSleep(door.allowed__proc(al))), "the door bars a stranger");
            Assert.True(Dm.truthy(Waiting.NoSleep(friendly.allowed__proc(al))), "a friendly door lets anyone in");
            Assert.Equal(TRUE, door.hasPower());
            Assert.Equal(FALSE, door.isElectrified());
            Assert.True(Dm.each<obj_effect_temp_visual_cult_sac>().Any(), "the stranger saw the door's sigil");
        });
        Assert.True(RunUntil(r, 2 * Second, () => get_dist(al, door) > 1), "the stranger was flung back");
        // a concealed door looks like a maintenance airlock until revealed
        r.In(() =>
        {
            door.conceal();
            Assert.Equal("Airlock", door.name);
            Assert.Equal(1f, door.stealthy);
            door.reveal();
            Assert.Equal("cult airlock", door.name);
            Assert.Equal(0f, door.stealthy);
        });
    }

    [Fact]
    public void A_gas_miner_fills_a_closed_room_with_its_gas_and_stops_on_the_open_planet()
    {
        var r = new AudioRoom(9, 9, seed: 13651);
        var miner = r.In(() =>
        {
            ((area)get_area(locate(5, 5, 1))).requires_power = FALSE;
            return Dm.@new<obj_machinery_atmospherics_miner_oxygen>(locate(5, 5, 1));
        });
        float before = r.In(() => Dm.num(((datum_gas_mixture)Waiting.NoSleep(locate(5, 5, 1).return_air())).moles[typeof(datum_gas_oxygen)]));
        r.Step(5 * Second);
        float after = r.In(() => Dm.num(((datum_gas_mixture)Waiting.NoSleep(locate(5, 5, 1).return_air())).moles[typeof(datum_gas_oxygen)]));
        Assert.True(after > before, $"the miner added oxygen ({before} -> {after})");
        r.In(() => Assert.Equal(0f, miner.broken));

        // on a planet's open air it reports and stops
        r.In(() => ((turf_open)locate(5, 5, 1)).planetary_atmos = TRUE);
        r.Step(2 * Second);
        r.In(() =>
        {
            Assert.Equal(1f, miner.broken);
            Assert.Contains("DEVICE NOT ENCLOSED IN A PRESSURIZED ENVIRONMENT", miner.broken_message);
            Assert.Contains("broken", Dm.each(miner.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)));
        });
    }

    [Fact]
    public void The_all_terrain_vehicle_draws_its_cover_over_its_rider_and_smokes_while_broken()
    {
        var r = new AudioRoom(11, 9, seed: 13652);
        mob_living_carbon_human bob = r.Join(4, 5, "Bob");
        var atv = r.In(() => Dm.@new<obj_vehicle_ridden_atv>(locate(5, 5, 1)));
        r.Step(2);
        r.In(() => Assert.NotNull(atv.GetComponent(typeof(datum_component_riding_vehicle_atv)) ?? (object)atv._listen_lookup));
        r.In(() => Waiting.NoSleep(atv.buckle_mob(bob, TRUE)));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(Dm.@in(bob, atv.buckled_mobs));
            Assert.Contains("atvcover", Dm.each(atv.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o)));
        });
        r.In(() => Waiting.NoSleep(atv.unbuckle_mob(bob)));
        r.Step(2);
        r.In(() => Assert.DoesNotContain("atvcover", Dm.each(atv.overlays).Select(o => o is IDmAppearance x ? x.icon_state : Dm.str(o))));
        r.In(() => Waiting.NoSleep(atv.take_damage(200, BRUTE, MELEE, FALSE))); // its melee armour halves it: 100 of its 150, past its failure at half
        r.In(() => Assert.True(Dm.@in(atv, SSobj.processing), "the broken ATV smokes"));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_dark_wizard_and_the_skeletons_take_their_looks_and_fall_to_their_remains()
    {
        var r = new AudioRoom(13, 9, seed: 13653);
        var wizard = r.In(() => Dm.@new<mob_living_basic_dark_wizard>(locate(3, 5, 1)));
        var settler = r.In(() => Dm.@new<mob_living_basic_skeleton_settler>(locate(9, 5, 1)));
        r.Step(2);
        r.In(() =>
        {
            Assert.NotNull(wizard.GetComponent(typeof(datum_component_ranged_attacks)));
            Assert.Contains(Dm.each<datum_action>(wizard.actions), a => a is datum_action_cooldown_spell_teleport_radius_turf_blink_slow);
            Assert.True(Dm.each<obj_item_clothing_head_wizard_hood>(wizard.contents).Any(), "the hat that lets him cast");
            Assert.True(HAS_TRAIT(wizard, TRAIT_LAVA_IMMUNE));
            Assert.True(HAS_TRAIT(settler, TRAIT_SNOWSTORM_IMMUNE));
        });
        r.In(() => Waiting.NoSleep(settler.death()));
        r.Step(2);
        r.In(() =>
        {
            Assert.True(QDELETED(settler));
            Assert.True(Dm.each<obj_effect_decal_remains_human>(locate(9, 5, 1)).Any());
            Assert.True(Dm.each<obj_item_spear>(locate(9, 5, 1)).Any(), "the settler's spear fell");
        });
        r.In(() => Waiting.NoSleep(wizard.death()));
        r.Step(2);
        r.In(() => Assert.True(Dm.each<obj_effect_decal_remains_human>(locate(3, 5, 1)).Any()));
    }

    /// The types the ruins' package made live (their files' procs translated): each made on a floor and deleted, without an error.
    [Fact]
    public void Every_newly_live_ruin_type_is_made_and_deleted_cleanly()
    {
        var r = new AudioRoom(15, 15, seed: 13654);
        Type[] types =
        {
            typeof(obj_structure_lavaland_ash_walker), typeof(obj_effect_collapse), typeof(obj_structure_ash_walker_eggshell),
            typeof(obj_effect_mob_spawn_ghost_role_human_ash_walker), typeof(obj_effect_mob_spawn_ghost_role_human_hermit),
            typeof(obj_effect_mob_spawn_ghost_role_human_beach_lifeguard), typeof(obj_effect_mob_spawn_ghost_role_human_beach),
            typeof(obj_effect_mob_spawn_ghost_role_human_bartender), typeof(obj_effect_sliding_puzzle_lavaland), typeof(obj_item_prisoncube),
            typeof(obj_effect_gluttony), typeof(obj_item_knife_envy), typeof(obj_structure_cursed_slot_machine), typeof(obj_structure_cursed_money),
            typeof(obj_item_freeze_cube), typeof(obj_structure_dead_ratvar), typeof(obj_item_food_egg_watcher), typeof(obj_item_watcher_hatchling),
            typeof(obj_structure_closet_crate_secure_freezer_commsagent), typeof(obj_machinery_power_supermatter_crystal_shard_syndicate),
            typeof(obj_effect_step_trigger_message), typeof(obj_effect_step_trigger_sound_effect_lavaland_cult_altar), typeof(obj_structure_firepit),
            typeof(mob_living_basic_dark_wizard), typeof(mob_living_basic_skeleton), typeof(mob_living_basic_skeleton_settler),
            typeof(mob_living_basic_skeleton_templar), typeof(mob_living_basic_skeleton_ice), typeof(mob_living_basic_skeleton_plasmaminer),
            typeof(obj_vehicle_ridden_atv), typeof(obj_vehicle_ridden_atv_turret), typeof(obj_effect_decal_cleanable_rubble),
            typeof(obj_structure_table_wood_shuttle_bar), typeof(obj_structure_sign_poster_official_plasma_effects),
            typeof(obj_item_circuitboard_machine_processor), typeof(obj_item_clothing_gloves_butchering),
            typeof(obj_machinery_door_airlock_cult), typeof(obj_machinery_door_airlock_cult_unruned_friendly),
            typeof(obj_machinery_atmospherics_miner_oxygen), typeof(obj_machinery_atmospherics_miner_plasma),
            typeof(obj_effect_mapping_helpers_no_lava), typeof(obj_effect_landmark_ruin),
        };
        foreach (Type type in types)
        {
            var made = r.In(() =>
            {
                var thing = (datum)Dm.@new(type, locate(7, 7, 1));
                Assert.False(InertSeam.IsInertType(type), type.Name + " is inert");
                return thing;
            });
            r.Step(2);
            r.In(() => { if (!QDELETED(made)) qdel(made); });
            r.Step(2);
        }
        foreach (Type turfType in new[]
        {
            typeof(turf_open_misc_beach_sand), typeof(turf_open_misc_beach_coast), typeof(turf_open_misc_beach_coast_corner),
            typeof(turf_open_misc_ice), typeof(turf_open_misc_asteroid_snow), typeof(turf_open_misc_grass_lavaland),
            typeof(turf_open_misc_ashplanet_wateryrock_lavaland_atmos), typeof(turf_open_floor_plating_abductor),
            typeof(turf_open_water_beach), typeof(turf_closed_wall_mineral_cult), typeof(turf_closed_wall_rust),
            typeof(turf_closed_wall_mineral_wood), typeof(turf_closed_wall_mineral_wood_nonmetal),
            typeof(turf_closed_wall_mineral_plastitanium_explosive), typeof(turf_closed_indestructible_necropolis),
            typeof(turf_closed_indestructible_riveted_hierophant),
        })
        {
            r.In(() =>
            {
                turf t = Waiting.NoSleep(locate(3, 3, 1).ChangeTurf(turfType)) as turf;
                Assert.IsType(turfType, t);
                Assert.False(InertSeam.IsInert(t), turfType.Name + " is inert");
            });
            r.Step(2);
        }
        foreach (string text in r.Room.RuntimeText)
            _output.WriteLine(text);
        r.AssertNoRuntimes();
    }
}
