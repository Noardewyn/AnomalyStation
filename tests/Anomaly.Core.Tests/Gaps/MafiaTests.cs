using System.IO;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Gaps;

/// <summary>
/// Gaps S4d (D327): Mafia (modules/mafia): ghosts sign up and vote to start, the map loads on the game area with its landmarks and
/// curtains, the roles are dealt and the ghosts put in bodies; the day vote puts a player on trial, guilty votes lynch them and reveal
/// the role; the night resolves the abilities in tg's order (a heal prevents the changelings' kill); town wins when the last
/// changeling hangs; a PDA signs up through its app; the ghost's Minigames menu opens the panel.
/// </summary>
public class MafiaTests
{
    private const int Second = 20; // the world ticks 20 times a second

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(34, 34, seed: seed);
        r.In(() =>
        {
            // tg's _maps/minigame/mafia, which the host mounts from the converted maps (MapSeam)
            string folder = Path.Combine(MapFixtures.RepoRoot(), "maps", "minigame", "mafia");
            foreach (string converted in Directory.GetFiles(folder, "*" + MapText.Extension))
                DmWorldState.Of(r.World).Files.WriteText("_maps/minigame/mafia/" + Path.GetFileNameWithoutExtension(converted) + ".dmm", File.ReadAllText(converted));
            // CentCom's game area (CentCom.dmm's landmark), here at the room's corner
            Dm.@new<obj_effect_landmark_mafia_game_area>(locate(3, 3, 1));
            SSticker.current_state = GAME_STATE_PLAYING; // signups wait for the round's start
        });
        return r;
    }

    private static List<mob_dead_observer> Ghosts(AudioRoom r, int count)
    {
        var ghosts = new List<mob_dead_observer>();
        for (int i = 0; i < count; i++)
        {
            mob_living_carbon_human body = r.Join(2 + i, 31, "player" + i);
            client player = body.client;
            r.In(() => body.ghostize(TRUE));
            r.Step(2);
            ghosts.Add(Assert.IsType<mob_dead_observer>(player.mob));
        }
        return ghosts;
    }

    /// A game of the given roles with these ghosts, set up as tg's custom setup (the admins' debug setup) starts one.
    private static datum_mafia_controller Game(AudioRoom r, List<mob_dead_observer> ghosts, params Type[] roles)
    {
        datum_mafia_controller game = r.In(() =>
        {
            // one map for every game, as tg's unit tests have (map_pieces.dm:39, "for consistency"): prepare_game's pick() follows
            // the seed's stream, and tg's snow map burns its own catwalks while its lava initializes (a tg runtime, which the port's
            // runtimes unwind further, DV1)
            TgState.Current.datum_mafia_controller__possible_maps = Dm.list(typeof(datum_map_template_mafia_summerball));
            datum_mafia_controller made = create_mafia_game();
            foreach (mob_dead_observer ghost in ghosts)
                Waiting.NoSleep(made.signup_mafia(ghost, ghost.client, null));
            DmList setup = Dm.list();
            foreach (Type role in roles)
                setup[role] = Dm.add(setup[role], 1);
            made.custom_setup = setup;
            return made;
        });
        r.Command(() => Waiting.Detach(game.basic_setup()));
        for (int i = 0; i < 40 && r.In(() => game.phase) == MAFIA_PHASE_SETUP; i++)
            r.Step();
        Assert.Equal(MAFIA_PHASE_DAY, r.In(() => game.phase));
        return game;
    }

    private static T Role<T>(AudioRoom r, datum_mafia_controller game) where T : datum_mafia_role
        => r.In(() => Dm.each<datum_mafia_role>(game.all_roles).OfType<T>().First());

    private static TA Ability<TA>(AudioRoom r, datum_mafia_role role) where TA : datum_mafia_ability
        => r.In(() => Dm.each<datum_mafia_ability>(role.role_unique_actions).OfType<TA>().Single());

    private static readonly Type[] Six =
    [
        typeof(datum_mafia_role_mafia), typeof(datum_mafia_role_detective), typeof(datum_mafia_role_medical_doctor),
        typeof(datum_mafia_role_lawyer), typeof(datum_mafia_role), typeof(datum_mafia_role),
    ];

    [Fact]
    public void A_game_loads_its_map_deals_the_roles_and_puts_the_ghosts_in_bodies()
    {
        AudioRoom r = Room(5101);
        List<mob_dead_observer> ghosts = Ghosts(r, 6);
        datum_mafia_controller game = Game(r, ghosts, Six);
        r.In(() =>
        {
            Assert.Same(game, GLOB.mafia_game);
            Assert.NotNull(game.current_map);
            Assert.Equal(12, length(game.landmarks)); // the map's twelve houses
            Assert.IsType<obj_effect_landmark_mafia_town_center>(game.town_center_landmark);
            Assert.Equal(6, length(game.all_roles));
            Assert.Equal(6, length(game.living_roles));
            Assert.Equal(1f, game.turn);
            Assert.Contains(I18n.T("Assistant x2"), Dm.each<object>(game.current_setup_text).Select(Dm.str));
            foreach (datum_mafia_role role in Dm.each<datum_mafia_role>(game.all_roles))
            {
                Assert.NotNull(role.body);
                Assert.NotNull(role.body.client); // the ghost is in the body
                Assert.True(HAS_TRAIT(role.body, TRAIT_GODMODE));
                Assert.Same(get_turf(role.assigned_landmark), get_turf(role.body));
                Assert.Same(role, game.player_role_lookup[role.player_key]);
                Assert.Contains(role.mafia_panel, Dm.each<datum_action>(role.body.actions));
                Assert.NotNull(role.body.w_uniform); // the map's or the default outfit
                Assert.NotNull(role.body.GetComponent(typeof(datum_component_temporary_body))); // back to the ghost's mind after the game
            }
            Assert.Empty(Dm.each<object>(GLOB.mafia_signup)); // the players leave the lobby when the game takes them
            // the lighting curtains of the map, open by day
            var curtains = Dm.each<obj_machinery_door_poddoor>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_door_poddoor)))
                .Where(d => Dm.eq(d.id, "mafia")).ToList();
            Assert.NotEmpty(curtains);
            Assert.All(curtains, d => Assert.False(Dm.truthy(d.density)));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_day_vote_puts_a_player_on_trial_and_guilty_votes_lynch_and_reveal_them()
    {
        AudioRoom r = Room(5102);
        datum_mafia_controller game = Game(r, Ghosts(r, 6), Six);
        datum_mafia_role_detective detective = Role<datum_mafia_role_detective>(r, game);
        r.In(() =>
        {
            game.start_voting_phase();
            foreach (datum_mafia_role voter in Dm.each<datum_mafia_role>(game.all_roles).Where(x => x != detective).Take(4).ToList())
            {
                datum_mafia_ability_voting vote = Dm.each<datum_mafia_ability>(voter.role_unique_actions).OfType<datum_mafia_ability_voting>().Single();
                vote.using_ability = TRUE;
                Waiting.NoSleep(vote.perform_action_target(game, detective));
                Assert.Equal(detective.body.real_name, Dm.str(voter.body.maptext).Split('>')[1].Split('<')[0]); // the vote over the voter
            }
            Assert.Equal(4f, game.get_vote_count(detective, "Day"));
        });
        r.Command(() => Waiting.Detach(game.check_trial()));
        r.Step(2);
        r.In(() =>
        {
            Assert.Same(detective, game.on_trial);
            Assert.Equal(MAFIA_PHASE_JUDGEMENT, game.phase);
            Assert.Same(get_turf(game.town_center_landmark as atom), get_turf(detective.body));
            Assert.Null(Dm.each<datum_mafia_role>(game.all_roles).First(x => x != detective).body.maptext); // the day votes are cleared
            foreach (datum_mafia_role voter in Dm.each<datum_mafia_role>(game.judgement_abstain_votes).ToList())
            {
                Dm.remove(ref game.judgement_abstain_votes, voter);
                Dm.append(ref game.judgement_guilty_votes, voter);
            }
        });
        r.Command(() => Waiting.Detach(game.lynch()));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal((float)MAFIA_DEAD, detective.game_status);
            Assert.Equal(DEAD, detective.body.stat);
            Assert.DoesNotContain(detective, Dm.each<datum_mafia_role>(game.living_roles));
            Assert.True(Dm.truthy(Dm.bitand(detective.role_flags, ROLE_REVEALED))); // a lynch reveals the role
            Assert.True(Dm.truthy(Dm.bitand(detective.team, MAFIA_TEAM_DEAD))); // and lets them hear dead chat
            Assert.Null(game.on_trial);
            Assert.Contains(Dm.each<object>(detective.role_messages).Select(Dm.str), m => m.Contains("has been lynched", StringComparison.Ordinal));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_night_resolves_a_heal_against_the_changelings_kill_and_the_curtains_close()
    {
        AudioRoom r = Room(5103);
        datum_mafia_controller game = Game(r, Ghosts(r, 6), Six);
        datum_mafia_role_mafia changeling = Role<datum_mafia_role_mafia>(r, game);
        datum_mafia_role_detective detective = Role<datum_mafia_role_detective>(r, game);
        datum_mafia_role_medical_doctor doctor = Role<datum_mafia_role_medical_doctor>(r, game);
        r.In(() => game.lockdown());
        r.Step(Second); // the curtains shut
        r.In(() =>
        {
            Assert.Equal(MAFIA_PHASE_NIGHT, game.phase);
            Assert.All(Dm.each<obj_machinery_door_poddoor>(SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery_door_poddoor))).Where(d => Dm.eq(d.id, "mafia")),
                d => Assert.True(Dm.truthy(d.density)));
            Ability<datum_mafia_ability_changeling_kill>(r, changeling).set_target(detective);
            Assert.Same(detective, game.get_vote_winner("Mafia"));
            Ability<datum_mafia_ability_heal>(r, doctor).set_target(detective);
        });
        r.Command(() => Waiting.Detach(game.resolve_night()));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal((float)MAFIA_ALIVE, detective.game_status); // healed
            Assert.Equal(MAFIA_PHASE_DAY, game.phase);
            Assert.Equal(2f, game.turn);
            Assert.Null(Ability<datum_mafia_ability_heal>(r, doctor).target_role); // the night's refs are cleared
        });
        // the next night nobody heals: the changelings' kill lands
        r.In(() =>
        {
            game.lockdown();
            Ability<datum_mafia_ability_changeling_kill>(r, changeling).set_target(detective);
        });
        r.Command(() => Waiting.Detach(game.resolve_night()));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal((float)MAFIA_DEAD, detective.game_status);
            Assert.Equal(DEAD, detective.body.stat);
            Assert.False(Dm.truthy(Dm.bitand(detective.role_flags, ROLE_REVEALED))); // a night kill does not reveal
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void Town_wins_when_the_last_changeling_hangs_and_the_game_cleans_up_after_its_victory_lap()
    {
        AudioRoom r = Room(5104);
        List<mob_dead_observer> ghosts = Ghosts(r, 6);
        datum_mafia_controller game = Game(r, ghosts, Six);
        datum_mafia_role_mafia changeling = Role<datum_mafia_role_mafia>(r, game);
        r.In(() =>
        {
            game.on_trial = changeling;
            game.phase = MAFIA_PHASE_JUDGEMENT;
            foreach (datum_mafia_role voter in Dm.each<datum_mafia_role>(game.living_roles).Where(x => x != changeling).ToList())
                Dm.append(ref game.judgement_guilty_votes, voter);
        });
        r.Command(() => Waiting.Detach(game.lynch()));
        r.Step(2);
        r.In(() =>
        {
            Assert.Equal(MAFIA_PHASE_VICTORY_LAP, game.phase);
            foreach (datum_mafia_role role in Dm.each<datum_mafia_role>(game.all_roles))
            {
                Assert.True(Dm.truthy(Dm.bitand(role.role_flags, ROLE_REVEALED))); // everyone's role is shown at the end
                Assert.Contains(Dm.each<object>(role.role_messages).Select(Dm.str), m => m.Contains("TOWN VICTORY!", StringComparison.Ordinal));
            }
        });
        r.Step(VictoryLapTicks + 2 * Second);
        r.In(() =>
        {
            Assert.Null(GLOB.mafia_game); // the controller deletes itself
            Assert.True(QDELETED(changeling));
            Assert.Empty(Dm.each<obj_effect_landmark_mafia>(GLOB.landmarks_list)); // the map is mass-deleted
        });
        r.AssertNoRuntimes();
    }

    private static readonly int VictoryLapTicks = (int)(VICTORY_LAP_PERIOD_LENGTH / 10 * Second); // tg's 20 s of deciseconds

    [Fact]
    public void A_pda_signs_up_through_its_app_and_sees_the_lobby()
    {
        AudioRoom r = Room(5105);
        mob_living_carbon_human alice = r.Join(5, 31, "alice");
        obj_item_modular_computer pda = r.In(() => Dm.@new<obj_item_modular_computer_pda_crew_assistant>(alice.loc));
        datum_computer_file_program_mafia app = r.In(() =>
        {
            var made = Dm.@new<datum_computer_file_program_mafia>();
            Assert.True(Dm.truthy(pda.store_file(made)));
            return made;
        });
        r.In(() =>
        {
            World.Current.usr = alice;
            Waiting.NoSleep(app.ui_act("mf_signup", Dm.list(), Dm.@new<datum_tgui>(alice, pda, "NtosMafiaPanel")));
            World.Current.usr = null;
            Assert.True(Dm.truthy(GLOB.pda_mafia_signup[pda]));
            DmList data = Waiting.NoSleep(app.ui_data(alice)) as DmList;
            Assert.Equal(MAFIA_PHASE_SETUP, Dm.str(data["phase"]));
            DmList lobby = data["lobbydata"] as DmList;
            Assert.Equal("Ready", Dm.str((lobby[1] as DmList)["status"]));
            DmList staticData = Waiting.NoSleep(app.ui_static_data(alice)) as DmList;
            Assert.Equal(FALSE, staticData["is_observer"]); // the PDA is not an observer
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void The_ghosts_minigames_menu_opens_the_mafia_panel()
    {
        AudioRoom r = Room(5106);
        mob_dead_observer ghost = Ghosts(r, 1)[0];
        r.Command(() => ghost.open_minigames_menu());
        r.Step(2);
        Assert.Contains("MinigamesMenu", r.In(() => Dm.each_cast<datum_tgui>(ghost.tgui_open_uis).Select(u => Dm.str(u.@interface)).ToList()));
        r.In(() =>
        {
            World.Current.usr = ghost;
            datum_tgui menu = Dm.each_cast<datum_tgui>(ghost.tgui_open_uis).Single(u => Dm.str(u.@interface) == "MinigamesMenu");
            Waiting.NoSleep(ghost.minigames_menu.ui_act("mafia", Dm.list(), menu));
            World.Current.usr = null;
        });
        r.Step(2);
        Assert.Contains("MafiaPanel", r.In(() => Dm.each_cast<datum_tgui>(ghost.tgui_open_uis).Select(u => Dm.str(u.@interface)).ToList()));
        r.In(() =>
        {
            datum_mafia_controller game = GLOB.mafia_game as datum_mafia_controller;
            Assert.NotNull(game);
            DmList data = Waiting.NoSleep(game.ui_data(ghost)) as DmList;
            Assert.Equal(MAFIA_PHASE_SETUP, Dm.str(data["phase"]));
            DmList staticData = Waiting.NoSleep(game.ui_static_data(ghost)) as DmList;
            Assert.Equal(TRUE, staticData["is_observer"]);
        });
        r.AssertNoRuntimes();
    }
}
