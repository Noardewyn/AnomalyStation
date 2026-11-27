// Translated from tgstation (snapshot 1d99bd7f): code/modules/unit_tests/create_and_destroy.dm (a subset)
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.UnitTests;

///Delete one of every type, sleep a while, then check to see if anything has gone fucky
// tg: code/modules/unit_tests/create_and_destroy.dm:2 (/datum/unit_test/create_and_destroy; class named by rules 2.3)
//
// ADAPTED (wave 3, the atoms package): the subset the port can run. tg's test runs after SSatoms initialized, on the
// unit test map; until the boot package's bootstrap and the map loader exist, New() brings up SSatoms (with the
// subsystems its path reaches) and a one-turf map, whose turf is run_loc_floor_bottom_left. Turf types are created by
// ChangeTurf in tg (create_and_destroy.dm:51-59), which is translated in M3: only movables are created. The GC queue
// wait (:93-131) waits for SSgarbage's queues, which the port does not have (DV7): the checks of :133-154 follow the qdels
// directly.
public sealed class datum_unit_test_create_and_destroy : TgUnitTest
{
    // tg: code/modules/unit_tests/create_and_destroy.dm:4-6
    public datum_unit_test_create_and_destroy()
    {
        test_flags = Dm.bitand(UNIT_TEST_BASIC, Dm.bitnot(UNIT_TEST_DEBUG_MAP_ONLY)); // parent_type::test_flags & ~UNIT_TEST_DEBUG_MAP_ONLY (UNIT_TEST_BASIC)
        priority = TEST_CREATE_AND_DESTROY;
    }

    /// <summary>
    /// The atom types the port has translated (or declared) so far, one per tg path, without the abstract ones:
    /// <c>valid_typesof(/atom/movable)</c> (__HELPERS/abstract_types.dm:25) drops a type whose <c>abstract_type</c> is itself, and
    /// since wave 4 <c>/atom/movable</c>, <c>/obj</c> and <c>/mob</c> are their own <c>abstract_type</c> (W4.M).
    /// </summary>
    internal static List<Type> TranslatedMovableTypes()
    {
        var types = new List<Type>();
        foreach (Type type in Dm.each_cast<Type>(valid_typesof(typeof(atom_movable))))
        {
            // ADAPTED: typesof also lists the test assembly's fixture types; tg's test creates tg's own types.
            if (type.Assembly != typeof(atom).Assembly)
                continue;
            // GAP(M3 P1 -> lead): generated-only types skipped until translated (DV25; types-converter.md 8).
            // C4: these spawn live vermin that outlive the sweep's one object; the mega carp and the ghost (C6) read names the toy world's global init does not load.
            if (typeof(obj_effect_spawner_random_trash).IsAssignableFrom(type) || typeof(obj_effect_spawner_random_frog).IsAssignableFrom(type) || typeof(mob_living_basic_carp_mega).IsAssignableFrom(type) || type == typeof(mob_living_basic_ghost))
                continue;
            if (type.IsDefined(typeof(DmGeneratedOnlyAttribute), inherit: false))
                continue;
            types.Add(type);
        }
        return types;
    }

    public override void New()
    {
        base.New();
        // ADAPTED: what world start and SSatoms' Initialize do before tg's tests run (see above).
        SSatoms = null; // the harness's stand-in; SUBSYSTEM_DEF's New would Recover() from it
        TgStrings.Mount(DmWorldState.Of(World.Current).Files, Anomaly.Core.Tests.Tools.TgClone.Root); // J2: the french beret and the italian moustache load their speech at init
        Dm.@new<datum_controller_subsystem_mapping>();
        Dm.@new<datum_controller_subsystem_icon_smooth>();
        Dm.@new<datum_controller_subsystem_atoms>();
        TgToyRoom.MakeAi();
        TgTestGenesis.Install(); // wave 4: /mob/Initialize logs every mob's tag (mob.dm:101) through the logger that /world/Genesis creates
        Dm.@new<datum_controller_subsystem_points_of_interest>(); // M10x slice C: a living mob is a point of interest (living.dm:19), tg's MC always creates it
        Dm.@new<datum_controller_subsystem_spatial_grid>(); // wave 4: /mob/Initialize makes the mob hearing sensitive (mob.dm:100), which registers it with the (stub) spatial grid
        Dm.@new<datum_controller_subsystem_vis_overlays>(); // M3 P4: the plastic flaps' Initialize adds their vis overlay (plasticflaps.dm:121); tg's MC always creates it
        // Gaps S3: the fortune, fishing tip and junk mail papers pick from GLOB string lists that tg's GLOB reads at world start
        // (strings.dm:4-12), here after the strings' mount
        GLOB.InitGlobalfirst_names(); // the bitrunning revolutionary names itself from them (revolutionary.dm:96-105)
        GLOB.InitGlobalfirst_names_male();
        GLOB.InitGlobalfirst_names_female();
        GLOB.InitGloballast_names();
        GLOB.InitGlobalphonetic_alphabet(); // syndicate G3a: a deathrattle group names itself from it (implant_deathrattle.dm)
        GLOB.InitGlobalwisdoms();
        GLOB.InitGlobalfishing_tips();
        GLOB.InitGlobaljunkmail_messages();
        GLOB.InitGlobaladjectives(); // syndicate G4a: junk mail's pamphlet and red pill letter pick an adjective (mail.dm:232)
        GLOB.InitGlobalposibrain_names(); // silicons SI2: a positronic brain names itself from it (positronic.dm:33)
        GLOB.InitGlobalai_names(); // silicons SI9: the saboteur cyborg's chameleon picks an AI name (borgchameleon.dm:32)
        GLOB.InitGlobalgross_adjectives(); // gaps P3b: the fly's infused organs name themselves from it (odd_organ_name, names.dm:288)
        // M6 slice 5: a gib spawner's gibs streak along tg's move_to loop (blood.dm:610), on SSmovement, which tg's MC always creates
        if (SSmovement == null)
            Dm.@new<datum_controller_subsystem_movement>();
        // M2 F1: a GAGS item's Initialize names its icons through SSgreyscale (atom_greyscale.dm:26); tg's MC creates it and
        // counts the SS_NO_INIT subsystem initialized.
        if (SSgreyscale == null)
        {
            Dm.@new<datum_controller_subsystem_processing_greyscale>();
            SSgreyscale.initialized = Dm.TRUE;
        }
        // ADAPTED (M4b slice 2): tg runs the test on the unit test map's floors. The one-turf map is a floor (as area_contents'),
        // not BYOND's default /turf, which tg never makes: its air (create_gas_mixture) reads its parent /atom's temperature,
        // and an air alarm reads its turf's air at Initialize.
        Dm.world.turf = typeof(turf_open_indestructible);
        // ADAPTED (M4c slice 5): tg's unit test map (_maps/templates/unit_tests.dmm) has floors and walls all around the
        // spawn turf; the port's map is 5 x 5 floors with the spawn in the middle, so a thing that looks a few turfs around
        // (the airlock pump's find_density, 2 turfs, airlock_pump.dm:520) never steps off the map, which tg's map never lets it.
        Dm.world.maxx = 5;
        Dm.world.maxy = 5;
        Dm.world.maxz = 1;
        // M2: as manage_z_level grows it (mapping.dm:756); a living mob's Initialize reads it (gravity_setup, has_gravity). tg
        // runs the test in the station's test room, which has gravity: the level gets standard gravity, so no mob floats.
        SSmapping.gravity_by_z_level.len = Dm.world.maxz;
        ToyRoom.LinkLevels((int)Dm.world.maxz);
        SSmapping.gravity_by_z_level[1f] = STANDARD_GRAVITY;
        run_loc_floor_bottom_left = locate(3, 3, 1);
        // M4b audio slice 6: a hotspot's Initialize plays its puff (LINDA_fire.dm:144); playsound takes a channel from SSsounds
        // and its listeners from SSspatial_grid's cells, both of which tg's MC initializes before the atoms (INITSTAGE_EARLY).
        if (SSsounds == null)
        {
            Dm.@new<datum_controller_subsystem_sounds>();
            Waiting.NoSleep(SSsounds.Initialize());
        }
        Waiting.NoSleep(SSspatial_grid.Initialize());
        Waiting.NoSleep(SSatoms.Initialize());
    }

    // tg: code/modules/unit_tests/create_and_destroy.dm:9
    public override async DmTask Run()
    {
        TgStrings.Mount(DmWorldState.Of(World.Current).Files, TgClone.Root); // C4: the crab's and carp's speech (speechmod)
        SSai_controllers.on_max_z_changed(); // the unit test room has its level by now
        if (SSstation == null)
        {
            Dm.@new<datum_controller_subsystem_processing_station>(); // the small crystal announces itself (supermatter_variants.dm:67), tg's MC always has it
            SSstation.announcer = Dm.@new<datum_centcom_announcer_default>();
        }
        // silicons SI3: the printers' auto-unlocking techwebs (the lathe's, the smelter's, the biogenerator's) register with SSresearch and
        // read its design table; tg's MC always makes it
        if (SSresearch == null)
        {
            Dm.@new<datum_controller_subsystem_research>();
            Waiting.NoSleep(SSresearch.Initialize());
        }
        //We'll spawn everything here
        turf spawn_at = run_loc_floor_bottom_left as turf;

        DmList cached_contents = spawn_at.contents.Copy();

        // GLOB.running_create_and_destroy = TRUE: ADAPTED, the global (create_and_destroy.dm:8) is test-only and read by
        // no translated code yet.
        // ADAPTED: (valid_typesof(/atom/movable) + valid_typesof(/turf)) - uncreatables: the translated movable types
        // minus tg's uncreatables that exist in the port (unit_test.dm:271-338: /obj/item/bodypart, typesof(/obj/docking_port),
        // typesof(/obj/effect/mapping_helpers) since M3 P3, unit_test.dm:302).
        // valid_typesof drops the types whose abstract_type is themselves (__HELPERS/abstract_types.dm:25): since wave 4
        // that is /atom/movable, /obj and /mob (W4.M), so the abstract roots are no longer created.
        var uncreatables = new HashSet<Type> { typeof(obj_item_bodypart) };
        //Singleton (unit_test.dm:280; M7 power helpers slice: the dview mob, live since then)
        uncreatables.Add(typeof(mob_dview));
        //Not meant to spawn without the machine wand (unit_test.dm:286; gaps DV2: live since then)
        uncreatables.Add(typeof(obj_effect_bug_moving));
        // ADAPTED, GAP(M3 P1 -> lead): the hand-written skeletons that got tg's var block reach a leaf of a later package
        // (Tools/Types/VarBlockLeaves.cs, which the lifecycle tests check type by type); they are uncreatable here until then.
        uncreatables.UnionWith(Anomaly.Core.Tests.Tools.Types.VarBlockLeaves.Leaves.Keys);
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(obj_docking_port))))
            uncreatables.Add(t);
        //Needs clients / mobs to observe it to exist. Also includes hallucinations (unit_test.dm:312-316; M8 slice G).
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(obj_effect_client_image_holder))))
            uncreatables.Add(t);
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(obj_projectile_hallucination))))
            uncreatables.Add(t);
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(obj_item_hallucinated))))
            uncreatables.Add(t);
        //Say it with me now, type template
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(obj_effect_mapping_helpers))))
            uncreatables.Add(t);
        //Expects a mob to holderize, we have nothing to give (unit_test.dm:361; gaps C2)
        uncreatables.Add(typeof(obj_item_inhand_desk_bell)); // desk_bell.dm:140: only made around its bell (gaps C5)
        // ADAPTED: the dark matt-eor raises the alert when it is made (meteor_dark_matteor.dm:23); the harness world has no
        // configuration, which SSsecurity_level's level datums read (security_level_datums.dm:44; tg's runs with every subsystem). Gaps E2b
        uncreatables.Add(typeof(obj_effect_meteor_dark_matteor));
        // ADAPTED: the infected assistants' corpses dress in the assistant's outfit, whose ID trim reads SSid_access (outfit.dm:213), which the
        // harness world does not initialize (tg's runs with every subsystem; the other job corpses are generated-only and skipped). Gaps E3; the bitrunner's and the cyber police's corpses (the
        // bitrunning domains) dress in outfits with an ID trim too
        foreach (Type t in new[] { typeof(obj_effect_mob_spawn_corpse_human_assistant_beesease_infection), typeof(obj_effect_mob_spawn_corpse_human_assistant_brainrot_infection), typeof(obj_effect_mob_spawn_corpse_human_assistant_spanishflu_infection), typeof(obj_effect_mob_spawn_corpse_human_bitrunner), typeof(obj_effect_mob_spawn_corpse_human_cyber_police) })
            uncreatables.Add(t);
        uncreatables.Add(typeof(obj_item_storage_box_syndie_kit_induction_kit_clown)); // tg's own stack trace, see VarBlockLeaves
        // ADAPTED: the second syndi-kit picks its kit at random (uplink_kits.dm:186, pick_weight): its bees kit holds the beesease bottle,
        // whose disease is not ported (reagent_containers.dm:81, diseases after the core version), and the uplink hides the kit (DV343);
        // new live types move the sweep's random stream onto that kit (as MobLifecycleTests since syndicate G4d; here since N3a)
        uncreatables.Add(typeof(obj_item_storage_box_syndicate_bundle_b));
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(obj_item_mob_holder))))
            uncreatables.Add(t);
        //Needs a linked mecha (unit_test.dm:358; D321)
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(obj_effect_skyfall_landingzone))))
            uncreatables.Add(t);
        // unit_test.dm:318-319: the pod landing zone and its effect want their pod (syndicate G4c: live since then)
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(obj_effect_pod_landingzone_effect))))
            uncreatables.Add(t);
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(obj_effect_pod_landingzone))))
            uncreatables.Add(t);
        //We have a baseturf limit of 10, adding more than 10 baseturf helpers will kill CI, so here's a future edge case to fix. (unit_test.dm:321; live since gaps BR1)
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(obj_effect_baseturf_helper))))
            uncreatables.Add(t);
        // ADAPTED: the goal crate randomizer returns under UNIT_TESTS (bitrunning/objects/landmarks.dm:43); the port is the production
        // branch, which stack_traces without its domain's crates (gaps BR1)
        uncreatables.Add(typeof(obj_effect_landmark_bitrunning_crate_replacer));
        //needs a heart (unit_test.dm:325; live since gaps OG1)
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(obj_structure_ethereal_crystal))))
            uncreatables.Add(t);
        //No tauma to pass in (unit_test.dm:323; live since gaps B1)
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(mob_eye_imaginary_friend))))
            uncreatables.Add(t);
        //No linked console (unit_test.dm:327-329: the base construction and shuttle docker eyes; live since syndicate N1c and the aux base console)
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(mob_eye_camera_remote_base_construction))))
            uncreatables.Add(t);
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(mob_eye_camera_remote_shuttle_docker))))
            uncreatables.Add(t);
        // ADAPTED (M3 R4): the main gravity generator builds its 3x3 frame of parts around itself (setup_parts) and needs it
        // (center_part); the port's one-turf map cannot hold it, where tg's unit test room can.
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(obj_machinery_gravity_generator_main))))
            uncreatables.Add(t);
        // ADAPTED (the ghosts, D151): a ghost made on a turf, not in a body, goes to the arrivals shuttle or the station's centre
        // (observer.dm:127-131, SSmapping.get_station_center); the port's test room has no station level, where tg's unit test
        // runs on a station map.
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(mob_dead_observer))))
            uncreatables.Add(t);
        //Should not exist outside of holders (unit_test.dm:290, M6 slice 1: the blood trail, live since then)
        uncreatables.Add(typeof(obj_effect_decal_cleanable_blood_trail));
        //This should be obvious (unit_test.dm:299; silicons SI8: the malf AI's doomsday device, live since then)
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(obj_machinery_doomsday_device))))
            uncreatables.Add(t);
        //A lot of these depend on a hud datum to function and should not be created in a vacuum
        // (unit_test.dm:375; M4c slice 3: the action palette and its scroll buttons, live since then, need their hud)
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(atom_movable_screen))))
            uncreatables.Add(t);
        // ADAPTED: the APC (M7 slice 3) sets its area's channels off when it is made outside the map load and when it goes
        // (apc_main.dm:225, :329, update_area_power_usage), so the test room's later lights start unpowered and START_PROCESSING
        // their emergency power on SSmachines, which the harness world lacks (tg's runs with every subsystem).
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(obj_machinery_power_apc))))
            uncreatables.Add(t);
        //briefcase launchpads erroring (unit_test.dm:300; live since the syndicate campaign G2)
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(obj_machinery_launchpad_briefcase))))
            uncreatables.Add(t);
        //This one demands a computer, ditto (unit_test.dm:309; the processor is live since engineering E2)
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(obj_item_modular_computer_processor))))
            uncreatables.Add(t);
        // ADAPTED, as the APC's: the improvised chem heater finds its subsystem with `locate(subsystem_type) in Master.subsystems`
        // (spaceheater.dm:341) and reads its wait in RefreshParts (:537); the harness world's Master lists no subsystems (engineering E9)
        uncreatables.Add(typeof(obj_machinery_space_heater_improvised_chem_heater));
        //runtimes if not paired with a landmark (unit_test.dm:367: typesof(/obj/structure/transport/linear); the tram port took the trams,
        // whose controller CRASHes without its nav beacons; the elevators, which make none, stay checked)
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(obj_structure_transport_linear_tram))))
            uncreatables.Add(t);
        // unit_test.dm:307, :339, :373 (silicons SI2: live since then): the silicons' PDAs delete themselves outside a silicon, the
        // preset-model cyborgs invoke async in their Initialize, and the robot models should never be spawned outside a robot
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(obj_item_modular_computer_pda_silicon))))
            uncreatables.Add(t);
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(mob_living_silicon_robot_model))))
            uncreatables.Add(t);
        foreach (Type t in Dm.each_cast<Type>(typesof(typeof(obj_item_robot_model))))
            uncreatables.Add(t);
        List<Type> type_paths_to_check = TranslatedMovableTypes().Where(t => !uncreatables.Contains(t)).ToList();
        TEST_ASSERT(type_paths_to_check.Count > 0, "no translated movable type to check");

        foreach (Type type_path in type_paths_to_check)
        {
            atom creation = Dm.@new(type_path, spawn_at) as atom;
            if (!QDELETED(creation))
            {
                //Go all in
                qdel(creation, force: TRUE);
            }
            //This will hold a ref to the last thing we process unless we set it to null
            //Yes byond is fucking sinful
            creation = null;

            //There's a lot of stuff that either spawns stuff in on create, or removes stuff on destroy. Let's cut it all out so things are easier to deal with
            DmList to_del = Dm.sub(spawn_at.contents, cached_contents) as DmList;
            if (Dm.truthy(length(to_del)))
            {
                foreach (atom to_kill in Dm.each_cast<atom>(to_del))
                    qdel(to_kill, force: TRUE);
            }
            to_del = null;
        }

        //Clear it, just in case
        cached_contents.Cut();

        //Alright, time to see if anything messed up
        DmList cache_for_sonic_speed = SSgarbage.items;
        foreach (object path in Dm.each(cache_for_sonic_speed))
        {
            datum_qdel_item item = cache_for_sonic_speed[path] as datum_qdel_item;
            if (Dm.truthy(item.failures))
                TEST_FAIL(Dm.text("[] hard deleted [] times out of a total del count of []", item.name, item.failures, item.qdels));
            if (Dm.truthy(item.no_respect_force))
                TEST_FAIL(Dm.text("[] failed to respect force deletion [] times out of a total del count of []", item.name, item.no_respect_force, item.qdels));
            if (Dm.truthy(item.no_hint))
                TEST_FAIL(Dm.text("[] failed to return a qdel hint [] times out of a total del count of []", item.name, item.no_hint, item.qdels));
            if (Dm.truthy(LAZYLEN(item.extra_details)))
            {
                string details = item.extra_details.Join("\n");
                TEST_FAIL(Dm.text("[] failed with extra info: \n[]", item.name, details));
            }
        }

        cache_for_sonic_speed = SSatoms.BadInitializeCalls;
        foreach (object path in Dm.each(cache_for_sonic_speed))
        {
            object fails = cache_for_sonic_speed[path];
            if (Dm.truthy(Dm.bitand(fails, BAD_INIT_NO_HINT)))
                TEST_FAIL(Dm.text("[] didn't return an Initialize hint", path));
            if (Dm.truthy(Dm.bitand(fails, BAD_INIT_QDEL_BEFORE)))
                TEST_FAIL(Dm.text("[] qdel'd before we could call Initialize()", path));
            if (Dm.truthy(Dm.bitand(fails, BAD_INIT_SLEPT)))
                TEST_FAIL(Dm.text("[] slept during Initialize()", path));
        }
        // PORT CHECK (W4.M, not in tg's test): every GLOB list of mobs ends empty after the create and destroy of every type:
        // /mob/Initialize puts a mob on the mob lists (mob.dm:81-96) and /mob/Destroy takes it off them (mob.dm:44-79).
        foreach ((string name, DmList list) in new (string, DmList)[]
        {
            ("mob_list", GLOB.mob_list), ("alive_mob_list", GLOB.alive_mob_list), ("dead_mob_list", GLOB.dead_mob_list),
            ("suicided_mob_list", GLOB.suicided_mob_list), ("player_list", GLOB.player_list), ("keyloop_list", GLOB.keyloop_list),
            ("new_player_list", GLOB.new_player_list), ("joined_player_list", GLOB.joined_player_list), ("drones_list", GLOB.drones_list),
            ("silicon_mobs", GLOB.silicon_mobs), ("mob_living_list", GLOB.mob_living_list), ("carbon_list", GLOB.carbon_list),
            ("human_list", GLOB.human_list), ("ai_list", GLOB.ai_list), ("spidermobs", GLOB.spidermobs), ("bots_list", GLOB.bots_list), ("camera_eyes", GLOB.camera_eyes),
            ("alive_player_list", GLOB.alive_player_list), ("dead_player_list", GLOB.dead_player_list),
            ("current_observers_list", GLOB.current_observers_list), ("current_living_antags", GLOB.current_living_antags),
        })
        {
            if (Dm.truthy(length(list)))
                TEST_FAIL(Dm.text("GLOB.[] still lists [] after every type was created and destroyed: []", name, length(list), jointext(list, ", ")));
        }

        // GLOB.simple_animals is one list per AI status (mobs.dm:41: list(list(), list(), list())): each of them ends empty
        for (float status = 1; status <= length(GLOB.simple_animals); status++)
        {
            DmList of_status = GLOB.simple_animals[status] as DmList;
            if (Dm.truthy(length(of_status)))
                TEST_FAIL(Dm.text("GLOB.simple_animals[[]] still lists [] after every type was created and destroyed: []", status, length(of_status), jointext(of_status, ", ")));
        }

        // ADAPTED: SSgarbage.collection_timeout[GC_QUEUE_CHECK] = GC_CHECK_QUEUE: no GC queues in the port (DV7).
    }
}
