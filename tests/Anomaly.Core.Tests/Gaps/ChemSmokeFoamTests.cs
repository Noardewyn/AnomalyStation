using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package F1 (D298): chem smoke and foam in a booted toy room. Foam spreads to its factory's size and dissolves, metal foam
/// leaves foamed metal, firefighting foam puts a fire out, the atmos resin scrubs the air, the foam and smoke powder reactions
/// make foam and smoke (and a gradual reaction runs on SSreagents), the smoke machine smokes its tank, a hand-built chem grenade
/// and the teargas grenade go off, a clusterbuster splits, the firepack's resin launcher and resin foam work, and every type spawns
/// and goes with no runtimes.
/// </summary>
public class ChemSmokeFoamTests
{
    private const int X = 6, Y = 4;

    private static readonly Type[] AllTypes =
    {
        typeof(obj_effect_particle_effect_fluid_foam), typeof(obj_effect_particle_effect_fluid_foam_short_life), typeof(obj_effect_particle_effect_fluid_foam_long_life), typeof(obj_effect_particle_effect_fluid_foam_firefighting),
        typeof(obj_effect_particle_effect_fluid_foam_metal), typeof(obj_effect_particle_effect_fluid_foam_metal_iron), typeof(obj_effect_particle_effect_fluid_foam_metal_smart), typeof(obj_effect_particle_effect_fluid_foam_metal_resin),
        typeof(obj_effect_particle_effect_fluid_foam_metal_resin_halon), typeof(obj_effect_particle_effect_fluid_foam_dirty), typeof(obj_structure_foamedmetal), typeof(obj_structure_foamedmetal_iron), typeof(obj_structure_foamedmetal_resin),
        typeof(obj_effect_spawner_foam_starter), typeof(obj_effect_spawner_foam_starter_small), typeof(obj_machinery_smoke_machine), typeof(obj_item_circuitboard_machine_smoke_machine),
        typeof(obj_item_grenade_chem_grenade), typeof(obj_item_grenade_chem_grenade_large), typeof(obj_item_grenade_chem_grenade_cryo), typeof(obj_item_grenade_chem_grenade_pyro), typeof(obj_item_grenade_chem_grenade_adv_release),
        typeof(obj_item_grenade_chem_grenade_metalfoam), typeof(obj_item_grenade_chem_grenade_smart_metal_foam), typeof(obj_item_grenade_chem_grenade_incendiary), typeof(obj_item_grenade_chem_grenade_antiweed), typeof(obj_item_grenade_chem_grenade_cleaner),
        typeof(obj_item_grenade_chem_grenade_ez_clean), typeof(obj_item_grenade_chem_grenade_teargas), typeof(obj_item_grenade_chem_grenade_facid), typeof(obj_item_grenade_chem_grenade_colorful), typeof(obj_item_grenade_chem_grenade_glitter),
        typeof(obj_item_grenade_chem_grenade_glitter_pink), typeof(obj_item_grenade_chem_grenade_glitter_blue), typeof(obj_item_grenade_chem_grenade_clf3), typeof(obj_item_grenade_chem_grenade_bioterrorfoam), typeof(obj_item_grenade_chem_grenade_tuberculosis),
        typeof(obj_item_grenade_chem_grenade_holy), typeof(obj_item_grenade_clusterbuster), typeof(obj_item_grenade_clusterbuster_smoke), typeof(obj_item_grenade_clusterbuster_metalfoam), typeof(obj_item_grenade_clusterbuster_teargas),
        typeof(obj_item_grenade_gas_crystal_healium_crystal), typeof(obj_item_grenade_gas_crystal_proto_nitrate_crystal), typeof(obj_item_grenade_gas_crystal_nitrous_oxide_crystal), typeof(obj_item_grenade_gas_crystal_crystal_foam),
        typeof(obj_item_storage_box_metalfoam), typeof(obj_item_storage_box_smart_metal_foam), typeof(obj_item_storage_box_teargas), typeof(obj_item_implant_smoke), typeof(obj_item_implanter_smoke),
    };

    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(13, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        // the toy room has no APC: free power, as use_energy gives shuttles
        r.In(() => (get_area(locate(X, Y, 1)) as area).requires_power = FALSE);
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, bob);
    }

    private static int Count<T>(AudioRoom r) where T : class => r.In(() => Dm.each<T>().Count());

    private static obj_item_reagent_containers_cup_beaker Beaker(AudioRoom r, Func<atom> where, params (Type Reagent, float Amount)[] contents) => r.In(() =>
    {
        var beaker = Dm.@new<obj_item_reagent_containers_cup_beaker_large>(where());
        foreach ((Type reagent, float amount) in contents)
            Waiting.NoSleep(((datum_reagents)beaker.reagents).add_reagent(reagent, amount, no_react: TRUE));
        return (obj_item_reagent_containers_cup_beaker)beaker;
    });

    [Fact]
    public void FoamSpreadsToItsSizeAndDissolves()
    {
        var (r, _) = Room(8101);
        r.In(() => Waiting.NoSleep(do_foam(2, null, locate(X, Y, 1))));
        r.Step(2 * Second);
        int spread = Count<obj_effect_particle_effect_fluid_foam>(r);
        Assert.InRange(spread, 13, 16); // DIAMOND_AREA(2) = 13 wanted: a node's last neighbours may overshoot, as in tg
        r.In(() => Assert.Equal("foam", locate<obj_effect_particle_effect_fluid_foam>(locate(X, Y, 1)).icon_state));
        Assert.True(r.In(() => (locate(X, Y, 1).GetComponent(typeof(datum_component_slippery)) ?? locate<obj_effect_particle_effect_fluid_foam>(locate(X, Y, 1)).GetComponent(typeof(datum_component_slippery))) != null), "tg's foam slips");
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/bubbles/bubbles2.ogg");

        r.Step(10 * Second); // 8 s of lifetime and the half second of its dissolve flick
        Assert.Equal(0, Count<obj_effect_particle_effect_fluid_foam>(r));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void MetalFoamLeavesFoamedMetalAndIronFoamTougherWalls()
    {
        var (r, _) = Room(8102);
        r.In(() => Waiting.NoSleep(do_foam(1, null, locate(X, Y, 1), foam_type: typeof(datum_effect_system_fluid_spread_foam_metal))));
        r.In(() => Waiting.NoSleep(do_foam(0, null, locate(X + 4, Y, 1), foam_type: typeof(datum_effect_system_fluid_spread_foam_metal_iron))));
        r.Step(Second);
        int nodes = Count<obj_effect_particle_effect_fluid_foam_metal>(r);
        Assert.True(nodes >= 6, "the metal foam spread: " + nodes);
        r.In(() => Assert.Equal("mfoam", locate<obj_effect_particle_effect_fluid_foam_metal>(locate(X, Y, 1)).icon_state));

        r.Step(10 * Second);
        Assert.Equal(0, Count<obj_effect_particle_effect_fluid_foam>(r));
        Assert.Equal(nodes, Count<obj_structure_foamedmetal>(r));
        r.In(() =>
        {
            obj_structure_foamedmetal wall = locate<obj_structure_foamedmetal>(locate(X, Y, 1));
            Assert.True(Dm.truthy(wall.density) && Dm.truthy(wall.opacity));
            Assert.Equal("metalfoam", wall.icon_state);
            obj_structure_foamedmetal_iron iron = locate<obj_structure_foamedmetal_iron>(locate(X + 4, Y, 1));
            Assert.NotNull(iron);
            Assert.Equal(50, iron.max_integrity);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void FirefightingFoamPutsOutAFireAndTheResinScrubsTheAir()
    {
        var (r, _) = Room(8103);
        turf_open fire = r.In(() => (turf_open)locate(X, Y, 1));
        r.In(() =>
        {
            fire.air.adjust_gas(typeof(datum_gas_plasma), 20);
            fire.air.temperature = 1000;
            fire.hotspot_expose(1000, 500);
        });
        r.Step(2);
        Assert.NotNull(r.In(() => locate<obj_effect_hotspot>(fire)));

        r.In(() => Waiting.NoSleep(do_foam(1, null, fire, foam_type: typeof(datum_effect_system_fluid_spread_foam_firefighting))));
        r.Step(Second);
        Assert.Null(r.In(() => locate<obj_effect_hotspot>(fire)));
        Assert.True(r.In(() => fire.air.temperature < 1000), "the foam halves the hot air's temperature");
        Assert.True(r.In(() => locate<obj_effect_particle_effect_fluid_foam_firefighting>(fire).absorbed_plasma > 0), "the foam takes up the burning plasma");

        // The atmos resin (effects_foam.dm:435): every gas but nitrogen, oxygen, pluoxium and halon goes, the air is 20 C.
        turf_open dirty = r.In(() => (turf_open)locate(X + 4, Y + 2, 1));
        r.In(() =>
        {
            dirty.air.adjust_gas(typeof(datum_gas_carbon_dioxide), 30);
            dirty.air.adjust_gas(typeof(datum_gas_plasma), 10);
            dirty.air.temperature = 400;
            Dm.@new<obj_structure_foamedmetal_resin>(dirty);
        });
        r.In(() =>
        {
            Assert.Equal(0, Dm.num(dirty.air.moles[typeof(datum_gas_carbon_dioxide)]));
            Assert.Equal(0, Dm.num(dirty.air.moles[typeof(datum_gas_plasma)]));
            Assert.True(Dm.num(dirty.air.moles[typeof(datum_gas_oxygen)]) > 0);
            Assert.Equal(T20C, dirty.air.temperature);
            Assert.False(Dm.truthy(locate<obj_structure_foamedmetal_resin>(dirty).opacity));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheFoamAndSmokePowderReactionsMakeFoamAndSmoke()
    {
        var (r, _) = Room(8104);
        var foamBeaker = Beaker(r, () => locate(X, Y, 1), (typeof(datum_reagent_fluorosurfactant), 10));
        r.In(() => Waiting.NoSleep(((datum_reagents)foamBeaker.reagents).add_reagent(typeof(datum_reagent_water), 10)));
        Assert.Equal(0, r.In(() => ((datum_reagents)foamBeaker.reagents).total_volume)); // the foam reaction takes both
        r.Step(2 * Second);
        Assert.True(Count<obj_effect_particle_effect_fluid_foam>(r) > 5, "the solution spews out foam");

        var smokeBeaker = Beaker(r, () => locate(X + 4, Y, 1), (typeof(datum_reagent_potassium), 10), (typeof(datum_reagent_consumable_sugar), 10));
        r.In(() => Waiting.NoSleep(((datum_reagents)smokeBeaker.reagents).add_reagent(typeof(datum_reagent_phosphorus), 10)));
        Assert.Equal(0, r.In(() => ((datum_reagents)smokeBeaker.reagents).total_volume));
        r.Step(Second);
        Assert.True(Count<obj_effect_particle_effect_fluid_smoke_chem>(r) > 5, "the smoke powder burst into smoke");
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/smoke.ogg");

        // A gradual reaction (no REACTION_INSTANT) runs through an equilibrium on SSreagents: lithium and hydrogen make foaming agent.
        var agent = Beaker(r, () => locate(2, 2, 1), (typeof(datum_reagent_lithium), 10));
        r.In(() =>
        {
            ((datum_reagents)agent.reagents).chem_temp = 300;
            Waiting.NoSleep(((datum_reagents)agent.reagents).add_reagent(typeof(datum_reagent_hydrogen), 10, reagtemp: 300));
        });
        r.Step(3 * Second);
        Assert.True(r.In(() => Dm.num(((datum_reagents)agent.reagents).get_reagent_amount(typeof(datum_reagent_foaming_agent)))) > 5, "the foaming agent forms over SSreagents' steps");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EveryLiveReactionIsIndexedAndACocktailMixes()
    {
        // The owner (2026-11-03): every live reaction reacts, the var-only drinks and mixes too; an inert one would wait (the food's are
        // live since gaps H2, the slime extracts' since xenobiology).
        var (r, _) = Room(8105);
        Assert.True(r.In(() => Dm.truthy(GLOB.chemical_reactions_list[typeof(datum_chemical_reaction_drink_screwdriver)])));
        Assert.True(r.In(() => Dm.truthy(GLOB.chemical_reactions_list[typeof(datum_chemical_reaction_food_tofu)])));
        Assert.True(r.In(() => Dm.truthy(GLOB.chemical_reactions_list[typeof(datum_chemical_reaction_slime_slimespawn)])));
        int indexed = r.In(() => (int)Dm.num(length(GLOB.chemical_reactions_list)));
        int liveWithReagents = r.In(() => Dm.each<Type>(subtypesof(typeof(datum_chemical_reaction)))
            .Count(type => !InertSeam.IsInertType(type) && Dm.truthy(length((Dm.@new(type) as datum_chemical_reaction).required_reagents))));
        Assert.Equal(liveWithReagents, indexed);
        Assert.True(indexed > 400, $"{indexed} reactions indexed");

        var glass = Beaker(r, () => locate(X, Y, 1), (typeof(datum_reagent_consumable_ethanol_vodka), 10));
        r.In(() => Waiting.NoSleep(((datum_reagents)glass.reagents).add_reagent(typeof(datum_reagent_consumable_orangejuice), 20)));
        Assert.Equal(30, r.In(() => Dm.num(((datum_reagents)glass.reagents).get_reagent_amount(typeof(datum_reagent_consumable_ethanol_screwdrivercocktail)))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheSmokeMachineSmokesItsTank()
    {
        var (r, bob) = Room(8105);
        var machine = Place<obj_machinery_smoke_machine>(r, 4, 4); // beside Bob
        Assert.Equal("smoke0", r.In(() => machine.icon_state));
        var beaker = Beaker(r, () => bob.loc, (typeof(datum_reagent_water), 50));
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(beaker)));
        r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(machine.item_interaction(bob, beaker))));
        Assert.True(r.In(() => ((datum_reagents)machine.reagents).total_volume) > 0);

        r.In(() =>
        {
            machine.on = TRUE;
            machine.begin_processing();
            machine.update_appearance(UPDATE_ICON_STATE);
        });
        Assert.Equal("smoke1", r.In(() => machine.icon_state));
        r.Step(3 * Second);
        Assert.True(Count<obj_effect_particle_effect_fluid_smoke_chem_smoke_machine>(r) > 0, "the machine puffs chem smoke");
        r.In(() =>
        {
            obj_effect_particle_effect_fluid_smoke_chem_smoke_machine puff = Dm.each<obj_effect_particle_effect_fluid_smoke_chem_smoke_machine>().First();
            Assert.Equal(100, puff.alpha); // see-through, tg's smoke machine kind
            Assert.True(((datum_reagents)puff.reagents).has_reagent(typeof(datum_reagent_water)) is datum_reagent);
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHandBuiltChemGrenadeAndTheTeargasGrenadeGoOff()
    {
        var (r, bob) = Room(8106);
        var casing = r.In(() => Dm.@new<obj_item_grenade_chem_grenade>(bob.loc));
        Assert.Equal("chemical grenade casing", r.In(() => casing.name));
        var coil = r.In(() => Dm.@new<obj_item_stack_cable_coil>(bob.loc));
        r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(casing.item_interaction(bob, coil))));
        Assert.Equal("chemg_ass", r.In(() => casing.icon_state));
        foreach (var beaker in new[]
        {
            Beaker(r, () => bob.loc, (typeof(datum_reagent_fluorosurfactant), 15)),
            Beaker(r, () => bob.loc, (typeof(datum_reagent_water), 15)),
        })
        {
            r.In(() => Waiting.NoSleep(bob.put_in_active_hand(beaker)));
            r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(casing.item_interaction(bob, beaker))));
        }
        var screwdriver = r.In(() => Dm.@new<obj_item_screwdriver>(bob.loc));
        r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(casing.screwdriver_act(bob, screwdriver))));
        Assert.Equal("chemg_locked", r.In(() => casing.icon_state));
        Assert.Equal("chemical grenade", r.In(() => casing.name));

        r.In(() => Waiting.NoSleep(casing.arm_grenade(bob)));
        Assert.Equal("chemg_active", r.In(() => casing.icon_state));
        r.Step(4 * Second);
        Assert.Equal(0, Count<obj_effect_particle_effect_fluid_foam>(r));
        r.Step(3 * Second); // tg's 5 s fuse
        Assert.True(Count<obj_effect_particle_effect_fluid_foam>(r) > 5, "the grenade mixed its beakers into foam");

        // The teargas grenade: the smoke powder reaction carries the condensed capsaicin through the room.
        mob_living_carbon_human victim = r.Dummy(10, 6);
        var teargas = r.In(() => Dm.@new<obj_item_grenade_chem_grenade_teargas>(locate(10, 4, 1)));
        r.In(() => Waiting.NoSleep(teargas.detonate()));
        r.Step(2 * Second);
        Assert.True(Count<obj_effect_particle_effect_fluid_smoke_chem>(r) > 20, "the teargas fills the room");
        Assert.True(r.In(() => ((datum_reagents)victim.reagents).has_reagent(typeof(datum_reagent_consumable_condensedcapsaicin)) is datum_reagent), "the victim breathes the capsaicin");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AClusterbusterSplitsIntoItsPayload()
    {
        var (r, _) = Room(8107);
        var cluster = r.In(() => Dm.@new<obj_item_grenade_clusterbuster_smoke>(locate(X, Y, 1)));
        r.In(() => Waiting.NoSleep(cluster.detonate()));
        Assert.True(r.In(() => QDELETED(cluster)));
        int bombs = Count<obj_item_grenade_smokebomb>(r) + Count<obj_item_grenade_clusterbuster_segment>(r);
        Assert.True(bombs >= 4, "four to eight payloads and segments: " + bombs);
        Assert.Equal(0, Count<obj_effect_payload_spawner>(r)); // the spawner deletes itself (INITIALIZE_HINT_QDEL)
        r.Step(8 * Second); // each payload goes off 1.5 to 6 s later
        Assert.True(Count<obj_effect_particle_effect_fluid_smoke>(r) > 10, "the smoke bombs went off");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheFirepacksResinLauncherAndResinFoamWork()
    {
        var (r, bob) = Room(8108);
        var pack = r.In(() => Dm.@new<obj_item_watertank_atmos>(bob.loc));
        r.In(() =>
        {
            Waiting.NoSleep(bob.equip_to_slot_or_del(pack, ITEM_SLOT_BACK));
            Waiting.NoSleep(pack.toggle_mister(bob));
        });
        var nozzle = r.In(() => bob.get_active_held_item() as obj_item_extinguisher_mini_nozzle);
        Assert.NotNull(nozzle);
        r.In(() => Waiting.NoSleep(nozzle.attack_self(bob))); // extinguisher -> resin launcher
        Assert.Equal(RESIN_LAUNCHER, r.In(() => Dm.num(nozzle.nozzle_mode)));

        turf target = r.In(() => locate(9, 4, 1));
        r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(nozzle.ranged_interact_with_atom(target, bob))));
        r.Step(3 * Second); // the capsule flies at 2 ticks a tile and bursts where it stops
        Assert.True(Count<obj_effect_particle_effect_fluid_foam_metal_resin>(r) > 5, "the capsule burst into resin foam");
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/bamf.ogg");
        r.Step(9 * Second);
        Assert.True(Count<obj_structure_foamedmetal_resin>(r) > 5, "the resin foam set into resin walls");

        r.In(() => Waiting.NoSleep(nozzle.attack_self(bob))); // resin launcher -> resin foam
        turf next = r.In(() => locate(3, 5, 1));
        r.In(() => Assert.Equal(ITEM_INTERACT_SUCCESS, Waiting.NoSleep(nozzle.ranged_interact_with_atom(next, bob))));
        r.Step(Second);
        Assert.NotNull(r.In(() => locate<obj_effect_particle_effect_fluid_foam_metal_resin>(next)));
        Assert.Null(r.In(() => locate<obj_effect_particle_effect_fluid_foam_metal_resin>(locate(3, 6, 1)))); // a single node: its group's target is 0
        r.AssertNoRuntimes();
    }

    [Fact]
    public void EveryTypeSpawnsLiveAndGoes()
    {
        var (r, _) = Room(8109);
        foreach (Type type in AllTypes)
        {
            Assert.False(Anomaly.Core.Tg.InertSeam.IsInertType(type), type.Name + " is inert");
            atom thing = r.In(() => Dm.@new(type, locate(2, 2, 1)) as atom);
            r.Step(2);
            r.In(() => { if (!QDELETED(thing)) qdel(thing); });
        }
        r.Step(2 * Second);
        r.AssertNoRuntimes();
    }
}
