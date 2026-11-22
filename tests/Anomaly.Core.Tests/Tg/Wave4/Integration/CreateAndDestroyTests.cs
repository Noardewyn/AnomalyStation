using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tests.Tg.UnitTests;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.Integration;

/// <summary>
/// The wave-4 extension of tg's <c>create_and_destroy</c> (UnitTests/create_and_destroy.cs, ported in wave 3): its type list is
/// tg's <c>valid_typesof(/atom/movable)</c>, which drops the abstract types (<c>/atom/movable</c>, <c>/obj</c> and <c>/mob</c> are
/// their own <c>abstract_type</c> since wave 4), and it checks that every GLOB mob list ends empty. Here the same loop runs on
/// the toy room (real turfs and area, a world with the toy room's whole bring-up), one type at a time.
/// </summary>
public class CreateAndDestroyTests
{
    // tg's uncreatables that exist in the port (unit_test.dm:271-338)
    private static bool Uncreatable(Type t) => t == typeof(obj_item_bodypart) || typeof(obj_docking_port).IsAssignableFrom(t)
        || typeof(obj_effect_mapping_helpers).IsAssignableFrom(t) // unit_test.dm:302, translated since M3 P3
        || typeof(obj_item_mob_holder).IsAssignableFrom(t) // unit_test.dm:361: expects a mob to holderize (gaps C2)
        || t == typeof(obj_item_inhand_desk_bell) // desk_bell.dm:140: only made around its bell (gaps C5)
        || t == typeof(obj_effect_bug_moving) // unit_test.dm:286: not meant to spawn without the machine wand (gaps DV2)
        || typeof(obj_effect_skyfall_landingzone).IsAssignableFrom(t) // unit_test.dm:358: needs a linked mecha (D321)
        || typeof(obj_effect_pod_landingzone).IsAssignableFrom(t) || typeof(obj_effect_pod_landingzone_effect).IsAssignableFrom(t) // unit_test.dm:318-319: need their pod (syndicate G4c)
        || typeof(mob_eye_camera_remote_base_construction).IsAssignableFrom(t) // unit_test.dm:327: no linked console (the aux base console, 2026-11-14)
        || typeof(mob_eye_camera_remote_shuttle_docker).IsAssignableFrom(t) // unit_test.dm:329: no linked console (syndicate N1c)
        || typeof(mob_eye_imaginary_friend).IsAssignableFrom(t) // unit_test.dm:323: no trauma to pass in (gaps K1)
        || typeof(obj_effect_baseturf_helper).IsAssignableFrom(t) // unit_test.dm:321: the baseturf limit (live since gaps BR1)
        || t == typeof(obj_effect_landmark_bitrunning_crate_replacer) // landmarks.dm:43: returns under UNIT_TESTS; the port's production branch needs its domain's crates (gaps BR1)
        // M3 R4: the main gravity generator builds a 3x3 frame of parts around itself and needs it; the toy room's corner
        // floor cannot hold it (tg's unit test room can).
        || typeof(obj_machinery_gravity_generator_main).IsAssignableFrom(t)
        // unit_test.dm:375: "A lot of these depend on a hud datum to function and should not be created in a vacuum" (M4c slice 3:
        // the action palette and its scroll buttons, live since then, need their hud)
        || typeof(atom_movable_screen).IsAssignableFrom(t)
        // unit_test.dm:290: "Should not exist outside of holders" (M6 slice 1: the blood trail, live since then)
        || t == typeof(obj_effect_decal_cleanable_blood_trail)
        // unit_test.dm:299: "This should be obvious" (silicons SI8: the malf AI's doomsday device, live since then)
        || typeof(obj_machinery_doomsday_device).IsAssignableFrom(t)
        // the APC (M7 slice 3) sets its area's channels off when it is made outside the map load and when it goes
        // (apc_main.dm:225, :329, update_area_power_usage), so the test room's later lights start unpowered and START_PROCESSING
        // their emergency power on SSmachines, which the harness world lacks (tg's runs with every subsystem)
        || typeof(obj_machinery_power_apc).IsAssignableFrom(t)
        // unit_test.dm:312-316: these need a mob to see them (M8 slice E, the hallucinations, translated them)
        || typeof(obj_effect_client_image_holder).IsAssignableFrom(t) || typeof(obj_projectile_hallucination).IsAssignableFrom(t) || typeof(obj_item_hallucinated).IsAssignableFrom(t)
        // unit_test.dm:309: the modular computer console's processing unit crashes outside its console (E2: live since then)
        || typeof(obj_item_modular_computer_processor).IsAssignableFrom(t)
        // unit_test.dm:300: "briefcase launchpads erroring" (the pad wants its briefcase; syndicate G2: live since then)
        || typeof(obj_machinery_launchpad_briefcase).IsAssignableFrom(t)
        // the dark matt-eor raises the alert when it is made (meteor_dark_matteor.dm:23); SSsecurity_level's level datums read the
        // configuration, which the harness world lacks (security_level_datums.dm:44; tg's runs with every subsystem). Gaps E2b
        || t == typeof(obj_effect_meteor_dark_matteor)
        // unit_test.dm:325: the ethereal crystal needs its heart (live since gaps OG1)
        || typeof(obj_structure_ethereal_crystal).IsAssignableFrom(t)
        // the infected assistants' corpses dress in the assistant's outfit, whose ID trim reads SSid_access (outfit.dm:213), which the harness
        // world does not initialize (the other job corpses are generated-only and skipped). Gaps E3
        || new[] { typeof(obj_effect_mob_spawn_corpse_human_assistant_beesease_infection), typeof(obj_effect_mob_spawn_corpse_human_assistant_brainrot_infection), typeof(obj_effect_mob_spawn_corpse_human_assistant_spanishflu_infection) }.Contains(t)
        // the improvised chem heater's RefreshParts reads its subsystem's wait (spaceheater.dm:537); the toy room's Master lists no SSmachines (E9)
        || typeof(obj_machinery_space_heater_improvised_chem_heater).IsAssignableFrom(t)
        // the manufacturing crafter's Initialize starts processing on SSmanufacturing (crafter.dm:25); the toy room's Master does not list it (E16)
        || typeof(obj_machinery_power_manufacturing_crafter).IsAssignableFrom(t)
        // unit_test.dm:367: a tram "runtimes if not paired with a landmark" (its controller CRASHes without the nav beacons; the tram port)
        || typeof(obj_structure_transport_linear_tram).IsAssignableFrom(t)
        // unit_test.dm:307, :339, :373 (silicons SI2: live since then): the silicons' PDAs delete themselves outside a silicon, the
        // preset-model cyborgs invoke async in their Initialize, and the robot models never spawn outside a robot
        || typeof(obj_item_modular_computer_pda_silicon).IsAssignableFrom(t) || typeof(mob_living_silicon_robot_model).IsAssignableFrom(t)
        || typeof(obj_item_robot_model).IsAssignableFrom(t);

    private static List<Type> ValidMovableTypes() => datum_unit_test_create_and_destroy.TranslatedMovableTypes().Where(t => !Uncreatable(t)).ToList();

    [Fact]
    public void ValidTypesofDropsTheAbstractRootsAndKeepsTheirCreatableDescendants()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            var types = ValidMovableTypes();

            Assert.DoesNotContain(typeof(atom_movable), types);
            Assert.DoesNotContain(typeof(obj), types);
            Assert.DoesNotContain(typeof(mob), types);
            Assert.DoesNotContain(typeof(mob_living), types);   // abstract_type = /mob/living (living_defines.dm:3, M2 var block)
            Assert.DoesNotContain(typeof(obj_item), types); // items.dm:5 (the M2 contracts step's var block)
            // M3 P1: /obj/effect is abstract by tg's own var block (abstract_type = /obj/effect, effects.dm:6), which the types
            // converter now gives the class; a concrete effect stands for it.
            Assert.DoesNotContain(typeof(obj_effect), types);
            foreach (Type creatable in new[] { typeof(obj_item_bodypart_head), typeof(obj_structure_table), typeof(obj_effect_decal_cleanable_blood), typeof(mob_dead_observer), typeof(mob_dead_new_player) })
                Assert.Contains(creatable, types);
            // tg's abstract_type is a var of the type itself: each dropped root names itself, and nothing created names a parent
            foreach (Type t in types)
                Assert.False(Dm.eq(Dm.initial(t, (datum p) => p.abstract_type), t), t.Name + " is its own abstract_type but was listed");
        }
    }

    [Fact]
    public void EveryValidMovableTypeCreatesAndDestroysInTheToyRoomLeavingEveryGlobMobListEmpty()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            TgStrings.Mount(DmWorldState.Of(room.World).Files, TgClone.Root); // C4: the crab's and carp's speech (speechmod)
            // M9 slice E: a relay counts the relays and a head's PDA looks for its fax on SSmachines (relays.dm:37,
            // role_tablet_presets.dm:34), the message server stamps the round time from SSticker (time.dm:8); tg's create_and_destroy
            // runs with the whole MC (the toy room leaves SSmachines out for the machinery tests of a world without it).
            Dm.@new<datum_controller_subsystem_machines>();
            Dm.@new<datum_controller_subsystem_ticker>();
            // E15: a plumbing component stops processing on SSplumbing when it is deleted (_plumbing.dm:114)
            Dm.@new<datum_controller_subsystem_processing_plumbing>();
            // syndicate N1a: a nuke reads the security level in its Initialize (_nuclear_bomb.dm:66)
            Dm.@new<datum_controller_subsystem_security_level>();
            // silicons SI3: the printers' auto-unlocking techwebs (the lathe's, the smelter's, the biogenerator's) register with SSresearch and
            // read its design table; tg's MC always makes it
            if (SSresearch == null)
            {
                Dm.@new<datum_controller_subsystem_research>();
                Waiting.NoSleep(SSresearch.Initialize());
            }
            // Gaps S3: the fortune, fishing tip and junk mail papers pick from GLOB string lists that tg's GLOB reads at world start
            // (strings.dm:4-12): the toy room mounts the strings after its GLOB
            GLOB.InitGlobalphonetic_alphabet(); // syndicate G3a: a deathrattle group names itself from it (implant_deathrattle.dm)
            GLOB.InitGlobalwisdoms();
            GLOB.InitGlobalfishing_tips();
            GLOB.InitGlobaljunkmail_messages();
            GLOB.InitGlobaladjectives(); // syndicate G4a: junk mail's pamphlet and red pill letter pick an adjective (mail.dm:232)
            GLOB.InitGlobalposibrain_names(); // silicons SI2: a positronic brain names itself from it (positronic.dm:33)
            GLOB.InitGlobalai_names(); // silicons SI9: the saboteur cyborg's chameleon picks an AI name (borgchameleon.dm:32)
            GLOB.InitGlobalgross_adjectives(); // gaps P3b: the fly's infused organs name themselves from it (odd_organ_name, names.dm:288)
            turf spawn_at = ToyRoom.FloorBottomLeft();
            DmList cached_contents = spawn_at.contents.Copy();
            // The ghosts (D151): a ghost made on a turf goes to the arrivals shuttle or the station's centre (observer.dm:127-131);
            // the toy room has no station level (tg's unit test room is on the station map).
            var types = ValidMovableTypes().Where(t => !typeof(mob_dead_observer).IsAssignableFrom(t)).ToList();
            Assert.True(types.Count > 20, "found only " + types.Count + " types");

            // GAP(M3 P1 -> lead): the hand-written skeletons that got tg's var block reach their listed leaf (VarBlockLeaves).
            var unexpected = new List<string>();
            foreach (Type type_path in types)
            {
                int before = room.Runtimes.Count;
                var creation = Dm.@new(type_path, spawn_at) as atom;
                unexpected.AddRange(Anomaly.Core.Tests.Tools.Types.VarBlockLeaves.Unexpected(type_path, room.RuntimeText.Skip(before).ToList()));
                if (!QDELETED(creation))
                    qdel(creation, force: TRUE);   // go all in
                creation = null;
                DmList to_del = Dm.sub(spawn_at.contents, cached_contents) as DmList;
                foreach (atom to_kill in Dm.each_cast<atom>(to_del))
                    qdel(to_kill, force: TRUE);

                foreach ((string name, DmList list) in MobLists())
                    Assert.True(length(list) == 0, "after " + type_path.Name + ", GLOB." + name + " lists " + length(list));
            }

            Assert.Equal(cached_contents.Count, spawn_at.contents.Count);
            Anomaly.Core.Tests.Tools.Types.VarBlockLeaves.AssertOnlyListedBadInitializeCalls(SSatoms.BadInitializeCalls); // GAP(M3 P1 -> lead)
            foreach (object path in Dm.each(SSgarbage.items))
            {
                var item = (datum_qdel_item)SSgarbage.items[path];
                Assert.False(Dm.truthy(item.failures), item.name + " hard deleted");
                Assert.False(Dm.truthy(item.no_respect_force), item.name + " failed to respect force deletion");
                Assert.False(Dm.truthy(item.no_hint), item.name + " failed to return a qdel hint");
            }
            Assert.Empty(unexpected);
        }
    }

    private static IEnumerable<(string, DmList)> MobLists()
    {
        yield return ("mob_list", GLOB.mob_list);
        yield return ("alive_mob_list", GLOB.alive_mob_list);
        yield return ("dead_mob_list", GLOB.dead_mob_list);
        yield return ("suicided_mob_list", GLOB.suicided_mob_list);
        yield return ("player_list", GLOB.player_list);
        yield return ("keyloop_list", GLOB.keyloop_list);
        yield return ("new_player_list", GLOB.new_player_list);
        yield return ("current_observers_list", GLOB.current_observers_list);
        yield return ("mob_living_list", GLOB.mob_living_list);
        yield return ("silicon_mobs", GLOB.silicon_mobs);
        yield return ("carbon_list", GLOB.carbon_list);
        yield return ("human_list", GLOB.human_list);
    }
}
