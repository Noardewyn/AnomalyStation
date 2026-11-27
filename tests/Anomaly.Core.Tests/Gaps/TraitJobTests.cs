using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// The two station-trait jobs, the Veteran Security Advisor and the Human AI (jobs/job_types/station_trait/), in booted toy rooms: each
/// trait opens its job's slot before the jobs are assigned and enters the random pool now that its job is live; the veteran starts on a
/// security officer's spot in his outfit with his PTSD and his trim's access; the human AI starts at the AI's spot with its gear, its
/// robotic tongue and the silicons' factions (and a void suit where the spot is not safe), opens a door from afar with its omni remote,
/// unpacks its camera console only in the satellite's chamber and packs it back with a screwdriver, and its lawset note copies the core
/// rack's laws.
/// </summary>
public class TraitJobTests
{
    private const int W = 15, H = 11;

    // A booted room with tg's ALLOW_AI as tgstation ships it (game_options.txt:171) and tg's strings for the names
    private static AudioRoom Room(ulong seed, Action<World> mapExtras = null)
    {
        var r = new AudioRoom(W, H, seed: seed, mapExtras: mapExtras, configure: w =>
        {
            IDmFileSystem files = DmWorldState.Of(w).Files;
            files.WriteText("config/config.txt", "ALLOW_AI\n");
        });
        r.In(() => TgStrings.Mount(DmWorldState.Of(r.World).Files, TgClone.Root));
        r.Step(Second);
        return r;
    }

    private static T Job<T>(AudioRoom r) where T : datum_job => r.In(() => (T)SSjob.get_job_type(typeof(T)));

    // tg's SSjob.equip_rank after the job's spawn: the outfit, the announcement, after_spawn
    private static void Equip(AudioRoom r, mob_living_carbon_human who, datum_job job)
    {
        r.In(() => Waiting.Detach(SSjob.equip_rank(who, job, who.client)));
        r.Step(5);
    }

    private static List<Type> Carried(AudioRoom r, mob_living_carbon_human who) => r.In(() =>
        Dm.each<atom>(who.get_all_contents()).Select(a => a.GetType()).ToList());

    [Fact]
    public void The_trait_jobs_and_their_gear_are_live_and_their_traits_enter_the_random_pool()
    {
        foreach (Type type in new[]
        {
            typeof(datum_job_veteran_advisor), typeof(datum_job_human_ai), typeof(datum_outfit_job_veteran_advisor), typeof(datum_outfit_job_human_ai),
            typeof(datum_id_trim_job_veteran_advisor), typeof(datum_id_trim_job_human_ai), typeof(obj_item_paper_default_lawset_list),
            typeof(obj_item_secure_camera_console_pod), typeof(obj_machinery_computer_camera_advanced_human_ai),
            typeof(obj_item_modular_computer_pda_veteran_advisor), typeof(obj_item_modular_computer_pda_human_ai),
        })
            Assert.False(InertSeam.IsInertType(type), type.Name + " is inert");
        Assert.False(datum_controller_subsystem_processing_station.WaitsForItsSystem(typeof(datum_station_trait_job_veteran_advisor)));
        Assert.False(datum_controller_subsystem_processing_station.WaitsForItsSystem(typeof(datum_station_trait_job_human_ai)));
    }

    [Fact]
    public void The_veteran_advisors_trait_opens_his_slot_and_he_starts_on_an_officers_spot_with_his_gear_and_his_ptsd()
    {
        AudioRoom r = Room(14901, w =>
        {
            Dm.@new<obj_effect_landmark_start_security_officer>(locate(9, 6, 1));
        });
        var job = Job<datum_job_veteran_advisor>(r);
        r.In(() =>
        {
            Assert.Equal(0f, job.total_positions);
            SSstation.setup_trait(typeof(datum_station_trait_job_veteran_advisor));
            var trait = Dm.locate<datum_station_trait_job_veteran_advisor>(SSstation.station_traits);
            Assert.NotNull(trait);
            Assert.Contains(trait, Dm.each<datum_station_trait>(GLOB.lobby_station_traits));
            Assert.Equal("veteran_advisor", Dm.str((trait.get_lobby_overlay_states(null) as DmList)[1]));
            // tg's PRE_JOBS_ASSIGNED (job.dm's DivideOccupations): the trait opens its job's one slot before the roles are dealt
            SSdcs._SendSignal(COMSIG_GLOB_PRE_JOBS_ASSIGNED, Dm.list(SSdcs));
            Assert.Equal(1f, job.total_positions);
            Assert.Equal(1f, job.spawn_positions);
        });

        // his spot: a security officer's start (veteran_advisor.dm:40)
        var spot = r.In(() => Waiting.NoSleep(job.get_roundstart_spawn_point()) as obj_effect_landmark_start);
        Assert.Equal("Security Officer", r.In(() => spot?.name));
        mob_living_carbon_human bob = r.Join(9, 6, "Bob");
        r.Step(2);
        Equip(r, bob, job);

        // the outfit (veteran_advisor.dm:61)
        Assert.IsType<obj_item_clothing_under_rank_security_officer_formal>(r.In(() => bob.w_uniform));
        Assert.IsType<obj_item_clothing_head_soft_veteran>(r.In(() => bob.head));
        Assert.IsType<obj_item_cigarette_cigar>(r.In(() => bob.wear_mask));
        Assert.IsType<obj_item_clothing_suit_jacket_leather_trenchcoat>(r.In(() => bob.wear_suit));
        Assert.IsType<obj_item_storage_belt_holster_detective_full_ert>(r.In(() => bob.belt));
        Assert.IsType<obj_item_radio_headset_heads_hos_advisor>(r.In(() => bob.ears));
        Assert.IsType<obj_item_clothing_glasses_eyepatch>(r.In(() => bob.glasses));
        Assert.IsType<obj_item_clothing_shoes_jackboots>(r.In(() => bob.shoes));
        Assert.IsType<obj_item_coin_antagtoken>(r.In(() => bob.l_store));
        Assert.IsType<obj_item_melee_baton_telescopic>(r.In(() => bob.r_store));
        Assert.Contains(r.In(() => Dm.each<obj_item>(bob.held_items).Select(i => i.GetType()).ToList()), t => t == typeof(obj_item_cane));
        List<Type> carried = Carried(r, bob);
        foreach (Type t in new[] { typeof(obj_item_modular_computer_pda_veteran_advisor), typeof(obj_item_storage_fancy_cigarettes_cigars), typeof(obj_item_lighter),
            typeof(obj_item_clothing_accessory_medal_bronze_heart), typeof(obj_item_gun_ballistic_automatic_pistol_m1911) })
            Assert.Contains(t, carried);

        // the mindshield, the trim's access, the PTSD; the paraplegic quirk is a listed leaf
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_MINDSHIELD)));
        var id = r.In(() => bob.wear_id as obj_item_card_id);
        Assert.NotNull(id);
        Assert.IsType<datum_id_trim_job_veteran_advisor>(r.In(() => id.trim));
        Assert.True(r.In(() => Dm.@in(ACCESS_BRIG, id.access) && Dm.@in(ACCESS_WEAPONS, id.access) && Dm.@in(ACCESS_COMMAND, id.access)));
        Assert.Equal("Veteran Security Advisor", r.In(() => Dm.str(bob.mind.assigned_role.title)));
        Assert.True(r.In(() => Dm.truthy(bob.has_trauma_type(typeof(datum_brain_trauma_special_ptsd)))));
        Assert.Contains(DmWorldState.Of(r.World).DeferredSites, s => s.StartsWith("code/modules/jobs/job_types/station_trait/veteran_advisor.dm:57", StringComparison.Ordinal));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_human_ai_starts_at_the_ais_spot_with_its_remotes_its_robotic_tongue_and_the_silicons_factions()
    {
        AudioRoom r = Room(14902, w =>
        {
            Dm.@new<turf_open_floor_iron>(locate(4, 6, 1)); // a station floor with tg's breathable air
            Dm.@new<obj_effect_landmark_start_ai>(locate(4, 6, 1));
        });
        var job = Job<datum_job_human_ai>(r);
        r.In(() =>
        {
            SSstation.setup_trait(typeof(datum_station_trait_job_human_ai));
            Assert.True(HAS_TRAIT(SSstation, STATION_TRAIT_HUMAN_AI));
            SSdcs._SendSignal(COMSIG_GLOB_PRE_JOBS_ASSIGNED, Dm.list(SSdcs));
            Assert.Equal(1f, job.total_positions);
            Assert.Equal("<b>Prefix your message with :b to speak with cyborgs.</b>", Dm.str(job.get_radio_information()));
            Assert.False(Dm.truthy(job.special_check_latejoin(null))); // no free latejoin core
        });
        var spot = r.In(() => Waiting.NoSleep(job.get_roundstart_spawn_point()) as obj_effect_landmark_start_ai);
        Assert.NotNull(spot);
        Assert.True(r.In(() => Dm.truthy(spot.used)));
        mob_living_carbon_human bob = r.Join(4, 6, "Bob");
        r.Step(2);
        Equip(r, bob, job);

        Assert.IsType<obj_item_clothing_under_rank_station_trait_human_ai>(r.In(() => bob.w_uniform));
        Assert.IsType<obj_item_modular_computer_pda_human_ai>(r.In(() => bob.belt));
        Assert.IsType<obj_item_radio_headset_silicon_human_ai>(r.In(() => bob.ears));
        Assert.IsType<obj_item_clothing_glasses_hud_diagnostic>(r.In(() => bob.glasses));
        Assert.IsType<obj_item_laser_pointer_infinite_range>(r.In(() => bob.l_store));
        Assert.IsType<obj_item_assembly_flash_handheld>(r.In(() => bob.r_store));
        Assert.IsType<obj_item_card_id_advanced_robotic>(r.In(() => bob.wear_id));
        Assert.IsType<datum_id_trim_job_human_ai>(r.In(() => (bob.wear_id as obj_item_card_id).trim));
        Assert.Null(r.In(() => bob.wear_suit)); // the floor's air is safe: no void suit
        List<Type> carried = Carried(r, bob);
        foreach (Type t in new[] { typeof(obj_item_door_remote_omni), typeof(obj_item_machine_remote), typeof(obj_item_secure_camera_console_pod),
            typeof(obj_item_sensor_device), typeof(obj_item_paper_default_lawset_list) })
            Assert.Contains(t, carried);
        // post_equip (human_ai.dm:126): the robotic tongue and its respect, the silicons' and turrets' factions
        Assert.IsType<obj_item_organ_tongue_robot>(r.In(() => bob.get_organ_slot(ORGAN_SLOT_TONGUE)));
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_COMMISSIONED)));
        Assert.True(r.In(() => Dm.@in(FACTION_SILICON, bob.faction) && Dm.@in(FACTION_TURRET, bob.faction)));
        Assert.Equal(JOB_HUMAN_AI, r.In(() => Dm.str(bob.mind.assigned_role.title))); // "Big Brother"
        r.AssertNoRuntimes();
    }

    [Fact]
    public void A_human_ai_on_an_unsafe_spot_gets_a_void_suit_and_its_internals()
    {
        AudioRoom r = Room(14903);
        // the outfit alone (tg's equipOutfit) on a man whose floor's air is pumped out: pre_equip (human_ai.dm:114) sees it is not safe
        mob_living_carbon_human carl = r.Dummy(11, 3);
        r.In(() =>
        {
            ((turf_open)carl.loc).air.remove_ratio(1);
            Assert.False(Dm.truthy(is_safe_turf(carl.loc as turf, dense_atoms: TRUE)));
            Waiting.NoSleep(carl.equipOutfit(typeof(datum_outfit_job_human_ai)));
        });
        r.Step(2);
        Assert.IsType<obj_item_clothing_suit_space_nasavoid>(r.In(() => carl.wear_suit));
        Assert.IsType<obj_item_clothing_head_helmet_space_nasavoid>(r.In(() => carl.head));
        Assert.IsType<obj_item_tank_internals_oxygen>(r.In(() => carl.s_store));
        Assert.Same(r.In(() => carl.s_store), r.In(() => carl.@internal));
    }

    [Fact]
    public void The_human_ais_omni_remote_opens_a_far_door_and_its_console_unpacks_only_in_the_satellite()
    {
        obj_machinery_door_airlock door = null;
        AudioRoom r = Room(14904, w =>
        {
            door = Dm.@new<obj_machinery_door_airlock_command>(locate(10, 6, 1));
        });
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(4, 6, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        Equip(r, bob, Job<datum_job_human_ai>(r));

        // the omni remote opens the command airlock six tiles away (control_wand.dm:168)
        var remote = r.In(() => Dm.each<obj_item_door_remote_omni>(bob.get_all_contents()).First());
        Assert.True(r.In(() => Dm.truthy(door.density)));
        r.In(() => Waiting.Detach(remote.ranged_interact_with_atom(door, bob)));
        r.Step(Second);
        Assert.False(r.In(() => Dm.truthy(door.density)));

        // the camera console pod: refused outside the AI satellite's chamber
        var pod = r.In(() => Dm.each<obj_item_secure_camera_console_pod>(bob.get_all_contents()).First());
        r.In(() =>
        {
            foreach (obj_item held in Dm.each<obj_item>(bob.held_items).ToList())
                Waiting.NoSleep(bob.dropItemToGround(held));
            Assert.True(Dm.truthy(Waiting.NoSleep(bob.put_in_hands(pod))));
        });
        r.In(() => Waiting.Detach(pod.attack_self(bob)));
        r.Step(Second);
        Assert.True(feed.Any("balloon: not in the sat!"));
        Assert.False(r.In(() => QDELETED(pod)));

        // in the chamber: five seconds of unpacking, the drill's sound and the console on the floor
        r.In(() =>
        {
            var chamber = (area)Dm.@new(typeof(area_station_ai_satellite_chamber));
            foreach (turf t in Dm.each<turf>(block(2, 2, 1, W - 1, H - 1, 1)))
                t.change_area((area)t.loc, chamber);
        });
        r.In(() => Waiting.Detach(pod.attack_self(bob)));
        r.Step(6 * Second);
        Assert.True(feed.Any("balloon: unpacking..."));
        Assert.True(r.In(() => QDELETED(pod)));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/tools/drill_use.ogg");
        var console = r.In(() => Dm.locate<obj_machinery_computer_camera_advanced_human_ai>(get_turf(bob)));
        Assert.NotNull(console);

        // a screwdriver packs it back
        var screwdriver = r.In(() => Dm.@new<obj_item_screwdriver>(get_turf(bob)));
        r.In(() => Waiting.Detach(console.screwdriver_act(bob, screwdriver)));
        r.Step(6 * Second);
        Assert.True(r.In(() => QDELETED(console)));
        Assert.NotNull(r.In(() => Dm.locate<obj_item_secure_camera_console_pod>(get_turf(bob))));
    }

    [Fact]
    public void The_lawset_note_copies_the_core_racks_laws()
    {
        AudioRoom r = Room(14905);
        r.In(() =>
        {
            var rack = Dm.@new<obj_machinery_ai_law_rack_base_core>(locate(6, 5, 1));
            rack.anchored = TRUE;
            rack.secured = TRUE;
            Waiting.NoSleep(rack.load_config_law());
        });
        r.Step(2);
        var note = r.In(() => Dm.@new<obj_item_paper_default_lawset_list>(locate(7, 5, 1)));
        r.Step(2);
        string text = r.In(() => string.Join("\n", Dm.each<datum_paper_input>(note.raw_text_inputs).Select(i => Dm.str(i.raw_text))));
        Assert.Contains("This is your lawset, you and your Cyborgs must adhere to this at all times.", text);
        Assert.Contains("LAWS:", text);
        Assert.True(text.Split('\n').Length > 4, text);

        // without a core rack the note stays blank
        AudioRoom bare = Room(14906);
        var blank = bare.In(() => Dm.@new<obj_item_paper_default_lawset_list>(locate(7, 5, 1)));
        bare.Step(2);
        Assert.Equal(0f, bare.In(() => Dm.num(length(blank.raw_text_inputs))));
    }
}
