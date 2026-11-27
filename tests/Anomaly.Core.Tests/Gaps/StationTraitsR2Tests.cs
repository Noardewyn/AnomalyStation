using System.Text.Json;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tests.Gaps.GapsHarness;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps package R2 (D327): tg's station traits in booted toy rooms with a configuration of their own (a world without one forbids
/// them, as tg's unit test build skips them). SSstation rolls the round's traits within tg's budgets and blacklists, or takes the
/// admins' future traits file; each trait gives its code trait, hooks the round start and shows in the round-start report; a
/// positive, a negative, a neutral and a job trait do what tg's do, and the readers that waited for them read them (the random
/// arrivals, the hangover spawn, the wallets, the assistants' gimmicks). The admins' Modify Station Traits window reverts the round's
/// traits and plans the next round's.
/// </summary>
public class StationTraitsR2Tests
{
    private const int W = 25, H = 19;

    // A booted room that rolls tg's station traits, with tg's ALLOW_AI as tgstation ships it (game_options.txt:171), and optionally
    // tg's FUTURE_STATION_TRAITS_FILE (the JSON list of trait paths an admin planned)
    private static AudioRoom Room(ulong seed, string future = null, Action<World> mapExtras = null, bool bobIsAdmin = false) => new(W, H, seed: seed, mapExtras: mapExtras, configure: w =>
    {
        IDmFileSystem files = DmWorldState.Of(w).Files;
        files.WriteText("config/config.txt", "ALLOW_AI\n");
        TgStrings.Mount(files, Tools.TgClone.Root, new[] { "strings/names/cargorilla.txt" }); // the cargorilla's names (names.dm:2)
        if (future != null)
            files.WriteText(FUTURE_STATION_TRAITS_FILE, future);
        if (bobIsAdmin)
        {
            // tg's admin ranks (admin_ranks.txt, admins.txt): Bob holds every right, R_FUN of the panel's verb with them
            files.WriteText("config/admin_ranks.txt", "Name = Host\nInclude = EVERYTHING\nExclude =\nEdit = EVERYTHING\n");
            files.WriteText("config/admins.txt", "Bob = Host\n");
        }
    });

    private static string Future(params string[] paths) => "[" + string.Join(",", paths.Select(p => "\"" + p + "\"")) + "]";

    private static List<datum_station_trait> Traits(AudioRoom r) => r.In(() => Dm.each<datum_station_trait>(SSstation.station_traits).ToList());

    private static void RoundStart(AudioRoom r) => r.In(() =>
    {
        // SSticker's setup (ticker.dm:357): SEND_SIGNAL(src, COMSIG_TICKER_ROUND_STARTING, world.time)
        SSticker._SendSignal(COMSIG_TICKER_ROUND_STARTING, Dm.list(SSticker, world.time));
    });

    [Fact]
    public void TheStationTraitsTheirPanelAndTheirReadersAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(datum_station_trait), typeof(datum_station_trait_lucky_winner), typeof(datum_station_trait_glowsticks),
            typeof(datum_station_trait_deathrattle_department_engineering), typeof(datum_station_trait_cybernetic_revolution),
            typeof(datum_station_trait_missing_wallet), typeof(datum_station_trait_random_event_weight_modifier_ion_storms),
            typeof(datum_station_trait_mail_blocked), typeof(datum_station_trait_hangover), typeof(datum_station_trait_blackout),
            typeof(datum_station_trait_revenge_of_pun_pun), typeof(datum_station_trait_revolutionary_trashing),
            typeof(datum_station_trait_nebula_hostile_radiation), typeof(datum_station_trait_storm_foreverstorm),
            typeof(datum_station_trait_birthday), typeof(datum_station_trait_skub), typeof(datum_station_trait_wallets),
            typeof(datum_station_trait_background_checks), typeof(datum_station_trait_pet_day), typeof(datum_station_trait_job),
            typeof(datum_station_trait_job_bridge_assistant), typeof(datum_station_trait_job_human_ai), typeof(datum_station_traits_panel),
            typeof(obj_item_birthday_invite), typeof(obj_item_clothing_head_costume_party), typeof(obj_item_storage_wallet),
            typeof(obj_effect_landmark_start_hangover), typeof(datum_outfit_job_assistant_gimmick_mopper),
            typeof(obj_item_storage_box_stickers_skub),
        })
            Assert.False(InertSeam.IsInertType(type), type.Name + " is inert");
    }

    [Fact]
    public void TheRoundRollsItsTraitsWithinTgsBudgetsAndBlacklists()
    {
        int rolled = 0;
        foreach (ulong seed in new ulong[] { 3, 17, 29, 41 })
        {
            AudioRoom r = Room(seed);
            List<datum_station_trait> traits = Traits(r);
            rolled += traits.Count;
            r.In(() =>
            {
                // the config's budgets (game_options.dm:459-472): at most 3 positive, 2.5 neutral and 3 negative
                foreach ((float sign, float budget) in new[] { (1f, 3f), (2f, 2.5f), (3f, 3f) })
                    Assert.True(traits.Where(t => t.trait_type == sign).Sum(t => t.cost) <= budget + 0.001f, "over the budget of sign " + sign);
                Assert.Equal(traits.Count, traits.Select(t => t.GetType()).Distinct().Count());
                foreach (datum_station_trait trait in traits)
                {
                    Assert.False(datum_controller_subsystem_processing_station.WaitsForItsSystem(trait.GetType()), trait.name + " waits for its system");
                    Assert.True(Dm.truthy(Dm.bitand(trait.trait_flags, STATION_TRAIT_SPACE_BOUND)), trait.name + " is not for a space station");
                    if (Dm.truthy(trait.trait_to_give))
                        Assert.True(HAS_TRAIT(SSstation, trait.trait_to_give), trait.name + "'s code trait");
                    foreach (Type banned in Dm.each_cast<Type>(trait.blacklist))
                        Assert.DoesNotContain(traits, other => other.GetType() == banned);
                }
            });
        }
        Assert.True(rolled > 0, "four rounds rolled no trait at all");
    }

    [Fact]
    public void AWorldWithoutTgsConfigurationRollsNoTraits()
    {
        var r = new AudioRoom(W, H, seed: 3);
        Assert.Empty(Traits(r));
        Assert.True(r.In(() => Dm.truthy(config.Get(typeof(datum_config_entry_flag_forbid_station_traits)))));
    }

    [Fact]
    public void TheAdminsFutureTraitsAreTheRoundsTraitsTheReportListsThemAndTheirEffectsRunAtRoundStart()
    {
        AudioRoom r = Room(7, Future("/datum/station_trait/announcement_intern", "/datum/station_trait/bountiful_bounties",
            "/datum/station_trait/premium_internals_box", "/datum/station_trait/galactic_grant", "/datum/nope"));
        Assert.Equal(new[] { typeof(datum_station_trait_announcement_intern), typeof(datum_station_trait_bountiful_bounties), typeof(datum_station_trait_premium_internals_box), typeof(datum_station_trait_galactic_grant) },
            Traits(r).Select(t => t.GetType()));
        r.In(() =>
        {
            Assert.Equal(0f, Dm.fexists(FUTURE_STATION_TRAITS_FILE)); // read once: SSstation deletes it (station.dm:79)
            Assert.IsType<datum_centcom_announcer_intern>(SSstation.announcer); // the intern's New set the type, Initialize made it
            Assert.True(HAS_TRAIT(SSstation, STATION_TRAIT_PREMIUM_INTERNALS));
            Assert.Equal(1f, SSeconomy.bounty_modifier);
        });
        float cargo = r.In(() => Dm.num(SSeconomy.get_dep_account(ACCOUNT_CAR).account_balance));

        RoundStart(r);
        r.In(() =>
        {
            Assert.Equal(1.2f, SSeconomy.bounty_modifier, 3); // Bountiful bounties
            float grant = Dm.num(SSeconomy.get_dep_account(ACCOUNT_CAR).account_balance) - cargo;
            Assert.InRange(grant, 2000, 5000); // Galactic grant
        });

        // the round-start command report: the traits shown in it under "Identified shift divergencies" (communications.dm:114-120)
        // (the report makes the station goals first, gaps R1, whose creation may wait a tick: the report is read once it is done)
        DmTask<object> task = default;
        r.In(() =>
        {
            // without SSdynamic's round (the toy room has no tier), the report leaves its threat advisory out, as tg's NO_DYNAMIC_REPORT
            ((datum_config_entry)config.entries_by_type[typeof(datum_config_entry_flag_no_dynamic_report)]).config_entry_value = TRUE;
            task = GLOB.communications_controller.send_roundstart_report(TRUE);
            Waiting.Detach(task);
        });
        r.Step(GapsHarness.Second);
        string report = r.In(() =>
        {
            Assert.True(task.TryGetResult(out object text), "the round-start report did not finish: " + string.Join("\n", r.Room.RuntimeText));
            return Dm.str(text);
        });
        Assert.Contains("<hr><h4>Identified shift divergencies:</h4>", report);
        Assert.Contains("<i>Bountiful bounties</i> - It seems collectors in this system are extra keen to on bounties, and will pay more to see their completion.<BR>", report);
        Assert.Contains("<i>Premium internals boxes</i> - The internals boxes for your crew have been upsized and filled with bonus equipment.<BR>", report);
        Assert.Contains("<i>Announcement Intern</i> - Please be nice to him.<BR>", report);
    }

    [Fact]
    public void SupplyLinesTheMailStrikeAndTheIonStormfrontChangeTheEconomyAndTheEvents()
    {
        AudioRoom good = Room(11, Future("/datum/station_trait/strong_supply_lines"));
        AudioRoom bad = Room(11, Future("/datum/station_trait/distant_supply_lines", "/datum/station_trait/mail_blocked",
            "/datum/station_trait/random_event_weight_modifier/ion_storms"));
        float Cost(AudioRoom r) => r.In(() => Dm.@new<datum_supply_pack_engineering_shieldgen>().get_cost());
        float baseCost = Cost(good);
        float mail = bad.In(() => SSeconomy.mail_blocked);
        float ionWeight = bad.In(() => Dm.locate<datum_round_event_control_ion_storm>(SSevents.control).weight);

        RoundStart(good);
        RoundStart(bad);
        Assert.Equal(baseCost * 0.8f, Cost(good), 3); // Strong supply lines (positive_traits.dm:136)
        Assert.Equal(baseCost * 1.2f, Cost(bad), 3); // Distant supply lines (negative_traits.dm:18)
        bad.In(() =>
        {
            Assert.Equal(Dm.truthy(mail) ? 0f : 1f, SSeconomy.mail_blocked); // the postal strike flips the mail
            Assert.Equal(ionWeight * 2, Dm.locate<datum_round_event_control_ion_storm>(SSevents.control).weight); // the Ionic Stormfront
        });
    }

    [Fact]
    public void TheSkubButtonCyclesTheStancesAndAJobTraitsButtonSignsUp()
    {
        AudioRoom r = Room(13, Future("/datum/station_trait/skub", "/datum/station_trait/job/bridge_assistant"));
        r.In(() =>
        {
            var skub = Dm.locate<datum_station_trait_skub>(GLOB.lobby_station_traits);
            var bridge = Dm.locate<datum_station_trait_job_bridge_assistant>(GLOB.lobby_station_traits);
            Assert.NotNull(skub);
            Assert.NotNull(bridge);
            var player = Dm.@new<mob_dead_new_player>();
            player.ckey = "alice";

            Assert.True(Dm.truthy(skub.can_display_lobby_button(null)));
            Assert.Equal("Are you pro-skub or anti-skub? Click to cycle through pro-skub, anti-skub, random and neutral.", skub.get_lobby_description());
            var seen = new List<(object, object, object)> { (null, skub.get_lobby_icon_state(player), Dm.str((skub.get_lobby_overlay_states(player) as DmList)[1])) };
            for (int i = 0; i < 4; i++)
            {
                object feedback = Waiting.NoSleep(skub.on_lobby_button_click(player));
                seen.Add((feedback, skub.get_lobby_icon_state(player), Dm.str((skub.get_lobby_overlay_states(player) as DmList)[1])));
            }
            Assert.Equal(new (object, object, object)[]
            {
                (null, "signup_neutral", "random_skub"), ("pro-skub", "signup_on", "pro_skub"), ("anti-skub", "signup", "anti_skub"),
                ("don't care", "signup_neutral", "neutral_skub"), ("on the best side", "signup_neutral", "random_skub"),
            }, seen);

            // the job trait: its own overlay, the sign-up toggled by the button, every other job trait blacklisted
            Assert.Equal("Sign up to become the Bridge Assistant and watch over the Bridge.", bridge.get_lobby_description());
            Assert.Equal("signup", bridge.get_lobby_icon_state(player));
            Waiting.NoSleep(bridge.on_lobby_button_click(player));
            Assert.Equal("signup_on", bridge.get_lobby_icon_state(player));
            Assert.Contains(player, Dm.each_cast<object>(bridge.lobby_candidates));
            Waiting.NoSleep(bridge.on_lobby_button_click(player));
            Assert.Equal("signup", bridge.get_lobby_icon_state(player));
            Assert.Equal("bridge_assistant", Dm.str((bridge.get_lobby_overlay_states(player) as DmList)[1]));
            Assert.Contains(typeof(datum_station_trait_job_human_ai), Dm.each_cast<Type>(bridge.blacklist));
            // its job is ported (gaps R2): it rolls, and so do the veteran advisor's and the human AI's since their jobs are (DV432)
            Assert.False(datum_controller_subsystem_processing_station.WaitsForItsSystem(typeof(datum_station_trait_job_bridge_assistant)));
            Assert.False(datum_controller_subsystem_processing_station.WaitsForItsSystem(typeof(datum_station_trait_job_veteran_advisor)));
            Assert.False(datum_controller_subsystem_processing_station.WaitsForItsSystem(typeof(datum_station_trait_job_human_ai)));
            Assert.False(datum_controller_subsystem_processing_station.WaitsForItsSystem(typeof(datum_station_trait_skub)));
        });
    }

    [Fact]
    public void AnAdminRevertsATraitAndPlansTheNextRoundsTraitsInThePanel()
    {
        AudioRoom r = Room(19, Future("/datum/station_trait/premium_internals_box", "/datum/station_trait/filled_maint"), bobIsAdmin: true);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        Assert.True(r.In(() => Dm.truthy(check_rights_for(bob.client, R_FUN))), "Bob is not an admin");

        // the admin console's station-traits: the verb's body opens the window (ADMIN_VERB(station_traits_panel, R_FUN, ...))
        Take(bob);
        r.In(() => Waiting.Detach(station_traits_panel(bob.client)));
        r.Step(4);
        List<TguiOutput> opened = Take(bob);
        JsonElement payload = LastUpdate(opened, out string window);
        Assert.Equal("StationTraitsPanel", InterfaceOf(payload));
        JsonElement data = payload.GetProperty("data");
        Assert.Equal(0, data.GetProperty("too_late_to_revert").GetInt32());
        JsonElement[] current = data.GetProperty("current_traits").EnumerateArray().ToArray();
        Assert.Equal(new[] { "Premium internals boxes", "Filled up maintenance" }, current.Select(t => t.GetProperty("name").GetString()));
        Assert.Equal(1, current[0].GetProperty("can_revert").GetInt32());
        Assert.Equal(0, current[1].GetProperty("can_revert").GetInt32()); // the filled maintenance is read at map load: tg's can_revert FALSE
        Assert.True(data.GetProperty("future_station_traits").ValueKind == JsonValueKind.Null);
        // the static data (ui_static_data, admin_panel.dm:26): every trait by name and path, sent with the window's first update
        string valid = string.Join("\n", opened.Where(o => o.Kind == TguiOutputKind.Message).Select(o => o.Json));
        Assert.Contains("\"valid_station_traits\"", valid);
        Assert.True(valid.Contains("{\"name\":\"The Great Skub Contention\",\"path\":\"/datum/station_trait/skub\"}", StringComparison.Ordinal), valid);

        // revert the premium internals: the trait and its code trait go
        Act(r, bob, window, "revert", ("ref", current[0].GetProperty("ref").GetString()));
        Assert.False(r.In(() => HAS_TRAIT(SSstation, STATION_TRAIT_PREMIUM_INTERNALS)));
        Assert.Equal(new[] { "Filled up maintenance" }, Data(r, bob, out _).GetProperty("current_traits").EnumerateArray().Select(t => t.GetProperty("name").GetString()));
        // a trait that cannot be reverted stays (tg's stack_trace)
        Act(r, bob, window, "revert", ("ref", current[1].GetProperty("ref").GetString()));
        Assert.Single(r.In(() => Dm.each<datum_station_trait>(SSstation.station_traits).ToList()));

        // plan the next round's: written to the future traits file
        Act(r, bob, window, "setup_future_traits", ("station_traits", Dm.list("/datum/station_trait/skub", "/datum/station_trait/wallets")));
        Assert.Equal("[\"/datum/station_trait/skub\",\"/datum/station_trait/wallets\"]", r.In(() => file2text(FUTURE_STATION_TRAITS_FILE)));
        JsonElement future = Data(r, bob, out _).GetProperty("future_station_traits");
        Assert.Equal(new[] { "The Great Skub Contention", "Wallets!" }, future.EnumerateArray().Select(t => t.GetProperty("name").GetString()));

        // an invalid path changes nothing; clearing deletes the file
        Act(r, bob, window, "setup_future_traits", ("station_traits", Dm.list("/datum/nope")));
        Assert.Equal("[\"/datum/station_trait/skub\",\"/datum/station_trait/wallets\"]", r.In(() => file2text(FUTURE_STATION_TRAITS_FILE)));
        Act(r, bob, window, "clear_future_traits");
        Assert.Equal(0f, r.In(() => Dm.fexists(FUTURE_STATION_TRAITS_FILE)));
        Assert.True(Data(r, bob, out _).GetProperty("future_station_traits").ValueKind == JsonValueKind.Null);

        // once the round has started the traits stay (too_late_to_revert)
        r.In(() => SSticker.current_state = GAME_STATE_PLAYING);
        Act(r, bob, window, "revert", ("ref", current[1].GetProperty("ref").GetString()));
        Assert.Equal(1, Data(r, bob, out _).GetProperty("too_late_to_revert").GetInt32());
    }

    // A joined player's client and a fresh mind for the mob a job made (tg's SSjob moves the player's mind into it before after_spawn)
    private static void Spawned(mob_living made) => Waiting.NoSleep(made.mind_initialize());

    [Fact]
    public void TheTraitJobsSpawnTheirPlayersAsPunPunTheCargorillaAndTheBridgeAssistant()
    {
        AudioRoom r = StationRoom(31, Future("/datum/station_trait/bountiful_bounties"));
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        var punpun = r.In(() => Dm.@new<mob_living_carbon_human_species_monkey_punpun>(locate(10, 10, 1)));
        r.Step();
        r.In(() =>
        {
            // Pun Pun is a Crewmember (job_traits.dm:229): before the round, the bar's Pun Pun gives his place to the job's start
            GLOB.the_one_and_only_punpun = punpun;
            SSstation.setup_trait(typeof(datum_station_trait_job_pun_pun));
            Assert.True(QDELETED(punpun));
            var start = Dm.locate<obj_effect_landmark_start_pun_pun>(locate(10, 10, 1));
            Assert.NotNull(start);
            // the signed-up player spawns as Pun Pun (pun_pun.dm:30-40): the one and only, a crew monkey
            var pun_job = (datum_job_pun_pun)SSjob.get_job_type(typeof(datum_job_pun_pun));
            var monkey = Waiting.NoSleep(pun_job.get_spawn_mob(bob.client, start)) as mob_living_carbon_human;
            Assert.IsType<mob_living_carbon_human_species_monkey_punpun>(monkey);
            Assert.Same(monkey, GLOB.the_one_and_only_punpun);
            Spawned(monkey);
            Waiting.NoSleep(pun_job.after_spawn(monkey, bob.client));
            Assert.True(HAS_TRAIT(monkey, TRAIT_BORN_MONKEY));
            Assert.True(Dm.truthy(monkey.dna.get_mutation(typeof(datum_mutation_clever))));

            // the Cargo Gorilla (cargo_gorilla.dm): at the cargo sloth's spot, named from tg's list, paid, with his ID
            Dm.append(ref GLOB.gorilla_start, locate(14, 10, 1)); // the map's cargo sloth marks its spot at map load (sloth.dm:63)
            var gorilla_job = (datum_job_cargo_gorilla)SSjob.get_job_type(typeof(datum_job_cargo_gorilla));
            var spot = Waiting.NoSleep(gorilla_job.get_roundstart_spawn_point()) as turf;
            Assert.Same(locate(14, 10, 1), spot);
            var gorilla = Waiting.NoSleep(gorilla_job.get_spawn_mob(bob.client, spot)) as mob_living;
            Assert.IsType<mob_living_basic_gorilla_cargorilla>(gorilla);
            Assert.Contains(gorilla.real_name, Dm.each_cast<string>(GLOB.cargorilla_names));
            Spawned(gorilla);
            Waiting.NoSleep(gorilla_job.after_spawn(gorilla, bob.client));
            var id = Dm.locate<obj_item_card_id_advanced_cargo_gorilla>(gorilla);
            Assert.NotNull(id);
            Assert.Equal(gorilla.name, id.registered_name);
            Assert.True(Dm.num(id.registered_account?.account_balance) > 0, "the gorilla's account has no starting pay");
            Assert.Contains("Cargorilla", Dm.each_cast<object>(gorilla.mind.special_roles));

            // the Bridge Assistant (bridge_assistant.dm:44): a chair on the bridge first
            var bridge = (area)Dm.@new(typeof(area_station_command_bridge));
            foreach (turf t in Dm.each<turf>(block(16, 14, 1, 20, 16, 1)))
                t.change_area((area)t.loc, bridge);
            Dm.@new<obj_structure_chair>(locate(19, 15, 1));
            var bridge_job = (datum_job_bridge_assistant)SSjob.get_job_type(typeof(datum_job_bridge_assistant));
            Assert.Same(locate(19, 15, 1), Waiting.NoSleep(bridge_job.get_roundstart_spawn_point()));
        });
    }

    // the E2c room's station: the iron floors on the station's level are the primary hallway
    private static AudioRoom StationRoom(ulong seed, string future = null)
    {
        AudioRoom r = Room(seed, future, _ =>
        {
            for (int y = 2; y < H; y++)
                for (int x = 2; x < W; x++)
                    Dm.@new<turf_open_floor_iron>(locate(x, y, 1));
        });
        r.In(() =>
        {
            GLOB.station_levels_cache.len = 1;
            GLOB.station_levels_cache[1] = TRUE;
            SSmapping.z_list = Dm.list(Dm.@new<datum_space_level>(1, "station", Dm.list(Dm.kv(ZTRAIT_STATION, TRUE))));
            var station = (area)Dm.@new(typeof(area_station_hallway_primary_central));
            foreach (turf t in Dm.each<turf>(block(2, 2, 1, W - 1, H - 1, 1)))
                t.change_area((area)t.loc, station);
            station.requires_power = FALSE;
            GLOB.the_station_areas = Dm.list(station.type);
        });
        r.Step(Second);
        return r;
    }

    [Fact]
    public void TheTraitsColourTheDecalsBeforeTheHolidaysAndMonkeyDayMakesPunPunACrewmember()
    {
        AudioRoom r = StationRoom(37, Future("/datum/station_trait/nebula/hostile/radiation"));
        r.In(() =>
        {
            // request_decoration_colors (events.dm:96, gaps R3): the radioactive nebula greens the hallways' decals (negative_traits.dm:717)
            Assert.Equal(COLOR_GREEN, request_decoration_colors(locate(5, 5, 1), PATTERN_DEFAULT));
            Assert.Null(request_decoration_colors(locate(5, 5, 1), PATTERN_DEFAULT, skip_station_trait: TRUE)); // no holiday here
            Assert.Null(request_decoration_colors(locate(1, 1, 1), PATTERN_DEFAULT)); // off the hallway: the next trait's or holiday's

            // monkey day (holidays.dm:857): Pun Pun is a Crewmember, set up once
            Assert.Null(Dm.locate<datum_station_trait_job_pun_pun>(SSstation.station_traits));
            Dm.@new<datum_holiday_monkey>().celebrate();
            Dm.@new<datum_holiday_monkey>().celebrate();
            Assert.Single(Dm.each<datum_station_trait_job_pun_pun>(SSstation.station_traits));
            Assert.Contains(Dm.each<datum_station_trait_job_pun_pun>(GLOB.lobby_station_traits), t => Dm.eq(t.name, "Pun Pun is a Crewmember"));
        });
    }

    [Fact]
    public void ADriveByLandingPutsTheRoundStartsCrewInTheHallways()
    {
        AudioRoom r = StationRoom(23, Future("/datum/station_trait/random_spawns"));
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(SSstation, STATION_TRAIT_RANDOM_ARRIVALS));
            var job = SSjob.get_job_type(typeof(datum_job_assistant)) as datum_job;
            for (int i = 0; i < 5; i++)
            {
                var spot = Waiting.NoSleep(job.get_roundstart_spawn_point()) as turf;
                Assert.NotNull(spot);
                Assert.IsAssignableFrom<area_station_hallway>(get_area(spot));
            }
        });
    }

    [Fact]
    public void AHangoverSpawnLeavesItsDebrisAndWhoJoinsThereWakesUpHungover()
    {
        AudioRoom r = StationRoom(29, Future("/datum/station_trait/hangover", "/datum/station_trait/birthday"));
        obj_effect_landmark_start_hangover spot = Place<obj_effect_landmark_start_hangover>(r, 10, 10);
        mob_living_carbon_human dummy = r.Dummy(6, 6);
        r.In(() =>
        {
            // the birthday's confetti under it, the hangover's vomit and bottles by chance
            Assert.Contains(Dm.each<obj_effect_decal_cleanable_confetti>(get_turf(spot)), c => Dm.@in(c, spot.party_debris));
            Assert.All(Dm.each<atom>(spot.hangover_debris), d => Assert.True(d is obj_effect_decal_cleanable_vomit || d is obj_item_reagent_containers_cup_glass_bottle_beer_almost_empty));

            float disgust = dummy.disgust;
            Waiting.NoSleep(spot.JoinPlayerHere(dummy, FALSE));
            Assert.Same(get_turf(spot), get_turf(dummy));
            Assert.True(dummy.disgust > disgust, "the joiner is not disgusted");
            Assert.True(Dm.truthy(dummy.resting), "the joiner is not lying down");
        });
    }

    [Fact]
    public void AWalletShowsItsMostCommandingIdAndOpensWithAllItsAccess()
    {
        AudioRoom r = Room(31);
        var wallet = Place<obj_item_storage_wallet>(r, 5, 5);
        r.In(() =>
        {
            var plain = Dm.@new<obj_item_card_id_advanced>(get_turf(wallet));
            plain.access = Dm.list(ACCESS_MAINT_TUNNELS);
            plain.registered_name = "Alice";
            var captain = Dm.@new<obj_item_card_id_advanced>(get_turf(wallet));
            captain.access = Dm.list(ACCESS_CAPTAIN, ACCESS_COMMAND);
            captain.registered_name = "Bob";
            Waiting.NoSleep(plain.forceMove(wallet));
            Assert.Same(plain, wallet.front_id);
            Waiting.NoSleep(captain.forceMove(wallet));
            Assert.Same(captain, wallet.front_id); // the card with the most command access is the front one
            Assert.Same(captain, wallet.GetID());
            Assert.StartsWith("wallet displaying ", Dm.str(wallet.name));
            var access = wallet.GetAccess() as DmList;
            foreach (object a in new object[] { ACCESS_MAINT_TUNNELS, ACCESS_CAPTAIN, ACCESS_COMMAND })
                Assert.True(Dm.@in(a, access), a + " is not in the wallet's access");
            Waiting.NoSleep(captain.forceMove(get_turf(wallet)));
            Assert.Same(plain, wallet.front_id);
        });
    }

    [Fact]
    public void TheGearedAssistantsPilotDressesTheAssistantsInGimmicks()
    {
        AudioRoom r = Room(37);
        r.In(() =>
        {
            var job = SSjob.get_job_type(typeof(datum_job_assistant)) as datum_job;
            Assert.False(istype(job.get_outfit(), typeof(datum_outfit_job_assistant_gimmick)) || ispath(job.get_outfit(), typeof(datum_outfit_job_assistant_gimmick)));
            ADD_TRAIT(SSstation, STATION_TRAIT_ASSISTANT_GIMMICKS, STATION_TRAIT);
            for (int i = 0; i < 5; i++)
                Assert.True(ispath(job.get_outfit(), typeof(datum_outfit_job_assistant_gimmick)), "not a gimmick outfit");
            Assert.Equal(typeof(datum_outfit_job_assistant_preview), job.get_outfit(TRUE)); // the preview stays the plain one
        });
    }
}
