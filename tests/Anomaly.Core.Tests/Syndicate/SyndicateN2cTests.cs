using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Syndicate;

/// <summary>
/// Syndicate stage N2c (D316) in a booted toy room: the operatives' reinforcements. The reinforcement beacon refuses anyone but an
/// operative and drops a ghost who signs up in a pod as a teammate; the overwatch beacon drops its agent at the outpost, who speaks
/// codespeak and gives every operative a bodycam (a later teammate too); the ED-209 beacon drops the Syndicate bot; the assault pod's
/// targeting device sets the drop pod console's landing zone; the deluxe codespeak manual teaches codespeak and is never spent.
/// </summary>
public class SyndicateN2cTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(30, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.Step(Second);
        // CentCom's holding area for pods in transit (tg's supplypod_temp_holding), which the toy room lacks: its corner turf (as G4c)
        r.In(() =>
        {
            var lane = GLOB.areas_by_type[typeof(area_centcom_central_command_areas_supplypod_supplypod_temp_holding)] as area
                ?? Dm.@new<area_centcom_central_command_areas_supplypod_supplypod_temp_holding>();
            set_turf_to_area(locate(1, 1, 1), lane);
        });
        return (r, feed, bob);
    }

    private static T Hold<T>(AudioRoom r, mob_living_carbon_human who) where T : obj_item => r.In(() =>
    {
        foreach (obj_item held in Dm.each<obj_item>(who.held_items).ToList())
            Waiting.NoSleep(who.dropItemToGround(held));
        T item = Dm.@new<T>(who.loc);
        Waiting.NoSleep(who.put_in_active_hand(item));
        return item;
    });

    private static datum_antagonist_nukeop Operative(AudioRoom r, mob_living_carbon_human who, datum_team team = null) => r.In(() =>
    {
        var op = Dm.@new<datum_antagonist_nukeop>();
        op.send_to_spawnpoint = FALSE;
        Waiting.NoSleep(who.mind.add_antag_datum(op, team));
        return op;
    });

    private static mob_dead_observer Ghost(AudioRoom r, int x, int y, string key)
    {
        mob_living_carbon_human body = r.Join(x, y, key);
        client player = body.client;
        // the beacons poll only ghosts who opted into midround operatives (tg's be_special)
        r.In(() => Dm.append(ref player.prefs.be_special, ROLE_OPERATIVE_MIDROUND));
        r.In(() => body.ghostize(TRUE));
        r.Step(2);
        return Assert.IsType<mob_dead_observer>(player.mob);
    }

    /// The toy room has no nukie base map: the operative team's lazy load of it is the one runtime allowed (as SyndicateN1cTests).
    private static void OnlyTheBaseRuntime(AudioRoom r, string alsoAllowed = null)
    {
        bool allowed(string text) => text.Contains("nukie_base.dmm") || text.Contains("_lazy_load_template") || (alsoAllowed != null && text.Contains(alsoAllowed));
        Assert.True(r.Room.RuntimeText.All(allowed), string.Join("\n---\n", r.Room.RuntimeText.Where(t => !allowed(t)).Select(t => t.Length > 900 ? t[..900] : t)));
    }

    private static void SignUp(AudioRoom r, mob_dead_observer ghost)
    {
        atom_movable_screen_alert_poll_alert alert = r.In(() => Dm.each(ghost.alerts).Select(k => ghost.alerts[k]).OfType<atom_movable_screen_alert_poll_alert>().SingleOrDefault());
        Assert.NotNull(alert);
        Click(r, ghost, alert);
    }

    private static void Act(AudioRoom r, mob who, string iface, string action, string key, object value)
    {
        r.Step(2);
        var open = r.In(() => Dm.each_cast<datum_tgui>(who.tgui_open_uis).Select(u => Dm.str(u.@interface)).ToList());
        Assert.True(open.Contains(iface), "no " + iface + " open; open: " + string.Join(", ", open));
        string window = r.In(() => Dm.str(Dm.each_cast<datum_tgui>(who.tgui_open_uis).Last(u => Dm.str(u.@interface) == iface).window.id));
        r.Command(() => ClientSeam.TguiMessage(who.client, window, "act/" + action, Dm.list(Dm.kv(key, value))));
        r.Step(4);
    }

    [Fact]
    public void TheReinforcementsAreLive()
    {
        foreach (Type type in new[]
        {
            typeof(obj_item_antag_spawner_nuke_ops), typeof(obj_item_antag_spawner_nuke_ops_overwatch), typeof(obj_item_antag_spawner_nuke_ops_ed209_nukie),
            typeof(datum_antagonist_nukeop_support), typeof(datum_objective_overwatch), typeof(datum_component_simple_bodycam),
            typeof(datum_language_codespeak), typeof(obj_item_language_manual_codespeak_manual_unlimited), typeof(obj_item_book_manual_random),
            typeof(obj_item_assault_pod), typeof(obj_docking_port_mobile_assault_pod), typeof(obj_item_clothing_suit_jacket_letterman_syndie),
            typeof(obj_item_clothing_under_syndicate_tacticool),
        })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void TheReinforcementBeaconRefusesAnyoneButAnOperative()
    {
        var (r, feed, bob) = Room(1801);
        var beacon = Hold<obj_item_antag_spawner_nuke_ops>(r, bob);
        r.In(() => Waiting.Detach(beacon.attack_self(bob)));
        r.Step(2);
        Assert.True(feed.Any("AUTHENTICATION FAILURE. ACCESS DENIED."));
        Assert.False(r.In(() => QDELETED(beacon)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheReinforcementBeaconDropsAGhostWhoSignsUpAsATeammate()
    {
        var (r, feed, bob) = Room(1802);
        var op = Operative(r, bob);
        mob_dead_observer ghost = Ghost(r, 20, 2, "Gina");
        client gina = ghost.client;
        var beacon = Hold<obj_item_antag_spawner_nuke_ops>(r, bob);
        r.In(() => Waiting.Detach(beacon.attack_self(bob)));
        r.Step(2);
        Assert.True(feed.Any("and wait for confirmation"));
        SignUp(r, ghost);

        // the poll's 15 seconds: Gina is a reinforcement operative of Bob's team, in a pod over the beacon's turf
        r.Step(16 * Second);
        mob_living_carbon_human nukie = r.In(() => gina.mob as mob_living_carbon_human);
        Assert.NotNull(nukie);
        Assert.True(r.In(() => QDELETED(beacon)));
        var reinforcement = r.In(() => nukie.mind.has_antag_datum(typeof(datum_antagonist_nukeop_reinforcement)) as datum_antagonist_nukeop_reinforcement);
        Assert.NotNull(reinforcement);
        Assert.Same(r.In(() => op.get_team()), r.In(() => reinforcement.get_team()));
        Assert.Contains("Operative", r.In(() => Dm.each(nukie.mind.special_roles).Select(Dm.str).ToList()));
        Assert.IsAssignableFrom<obj_structure_closet_supplypod>(r.In(() => nukie.loc));

        // the pod lands and opens on Bob's turf
        r.Step(10 * Second);
        Assert.Same(r.In(() => get_turf(bob)), r.In(() => nukie.loc));
        OnlyTheBaseRuntime(r);
    }

    [Fact]
    public void TheOverwatchAgentLandsAtTheOutpostSpeaksCodespeakAndGivesTheTeamBodycams()
    {
        var (r, feed, bob) = Room(1803);
        // the toy room has no nukie base: it stands in loaded, with no reservation, so the agent's move_to_spawnpoint (operative.dm:182)
        // goes straight to the outpost landmark (on Meta the base loads; the in-game check)
        r.In(() =>
        {
            SSmapping.loaded_lazy_templates ??= Dm.list();
            SSmapping.loaded_lazy_templates[LAZY_TEMPLATE_KEY_NUKIEBASE] = TRUE;
            var none = Dm.list();
            none.Add(new object[] { null });
            ((datum_lazy_template)GLOB.lazy_templates[LAZY_TEMPLATE_KEY_NUKIEBASE]).reservations = none;
        });
        var op = Operative(r, bob);
        r.In(() => Dm.@new<obj_effect_landmark_start_nukeop_base_overwatch>(locate(25, 6, 1)));
        mob_dead_observer ghost = Ghost(r, 20, 2, "Gina");
        client gina = ghost.client;
        var beacon = Hold<obj_item_antag_spawner_nuke_ops_overwatch>(r, bob);
        Assert.Same(r.In(() => locate(25, 6, 1)), r.In(() => beacon.spawn_location));
        r.In(() => Waiting.Detach(beacon.attack_self(bob)));
        r.Step(2);
        SignUp(r, ghost);
        r.Step(26 * Second);

        mob_living_carbon_human agent = r.In(() => gina.mob as mob_living_carbon_human);
        Assert.NotNull(agent);
        var support = r.In(() => agent.mind.has_antag_datum(typeof(datum_antagonist_nukeop_support)) as datum_antagonist_nukeop_support);
        Assert.NotNull(support);
        Assert.Contains(r.In(() => Dm.each(support.objectives).ToList()), o => o is datum_objective_overwatch);
        Assert.True(r.In(() => Dm.truthy(agent.has_language(typeof(datum_language_codespeak)))), "the agent speaks codespeak");
        Assert.Same(r.In(() => locate(25, 6, 1)), r.In(() => agent.loc));

        // Bob wears a bodycam on the operative network, and was told so
        var cam = r.In(() => Dm.each<datum_component_simple_bodycam>(bob.GetComponents(typeof(datum_component_simple_bodycam))).SingleOrDefault());
        Assert.NotNull(cam);
        Assert.Contains(OPERATIVE_CAMERA_NET, r.In(() => Dm.each(cam.bodycam.network).Select(Dm.str).ToList()));
        Assert.Equal(r.In(() => Dm.str(bob.real_name)), r.In(() => Dm.str(cam.bodycam.c_tag)));
        Assert.True(feed.Any("Smile, you're on camera!"));

        // a teammate who joins later gets one too
        mob_living_carbon_human carol = r.Join(6, 4, "Carol");
        Operative(r, carol, r.In(() => op.get_team()) as datum_team);
        r.Step(2);
        Assert.Single(r.In(() => Dm.each<datum_component_simple_bodycam>(carol.GetComponents(typeof(datum_component_simple_bodycam))).ToList()));
        Assert.True(feed.Any("You have been equipped with a bodycam"));
        // move_to_spawnpoint's check that the agent stands on the base (operative.dm:189): the toy room's outpost is not the base
        OnlyTheBaseRuntime(r, "move_to_spawnpoint resulted in a location not on the syndicate base");
    }

    [Fact]
    public void TheEd209BeaconDropsTheSyndicateBot()
    {
        var (r, _, bob) = Room(1804);
        Operative(r, bob);
        var beacon = Hold<obj_item_antag_spawner_nuke_ops_ed209_nukie>(r, bob);
        r.In(() => Waiting.Detach(beacon.attack_self(bob)));
        r.Step(2);
        Assert.True(r.In(() => QDELETED(beacon)));
        var bot = r.In(() => Dm.each<mob_living_basic_bot_secbot_ed209_nukie>(world).SingleOrDefault());
        Assert.NotNull(bot);
        Assert.IsAssignableFrom<obj_structure_closet_supplypod>(r.In(() => bot.loc));
        r.Step(10 * Second);
        Assert.Same(r.In(() => get_turf(bob)), r.In(() => bot.loc));
        // tg's spawner makes the bot in nullspace before the pod takes it: basic.dm:117's stack_trace, as in tg
        OnlyTheBaseRuntime(r, "Basic mob being instantiated in nullspace");
    }

    [Fact]
    public void TheAssaultPodTargeterSetsTheDropPodsLandingZone()
    {
        var (r, feed, bob) = Room(1805);
        var console = r.In(() => Dm.@new<obj_machinery_computer_shuttle_syndicate_drop_pod>(locate(8, 6, 1)));
        area room = r.In(() => get_area(bob) as area);
        r.In(() => GLOB.teleportlocs["Landing room"] = room);
        var targeter = Hold<obj_item_assault_pod>(r, bob);
        r.In(() => Waiting.Detach(targeter.attack_self(bob)));
        Act(r, bob, "ListInputWindow", "submit", "entry", "Landing room");
        Assert.True(feed.Any("Landing zone set."));
        Assert.True(r.In(() => QDELETED(targeter)));
        var zone = r.In(() => Dm.each<obj_docking_port_stationary>(world).SingleOrDefault(p => Dm.str(p.name) == "Landing Zone"));
        Assert.NotNull(zone);
        Assert.Same(room, r.In(() => get_area(zone)));
        Assert.StartsWith("assault_pod(", r.In(() => Dm.str(zone.shuttle_id)));
        Assert.Equal(7f, r.In(() => zone.width));
        Assert.Equal(r.In(() => Dm.str(zone.shuttle_id)), r.In(() => Dm.str(console.possible_destinations)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheDeluxeCodespeakManualTeachesCodespeakAndIsNeverSpent()
    {
        var (r, feed, bob) = Room(1806);
        var manual = Hold<obj_item_language_manual_codespeak_manual_unlimited>(r, bob);
        r.In(() => Waiting.NoSleep(manual.attack_self(bob)));
        Assert.True(r.In(() => Dm.truthy(bob.has_language(typeof(datum_language_codespeak)))));
        Assert.True(feed.Any("suddenly your mind is filled with codewords and responses"));
        Assert.False(r.In(() => QDELETED(manual)));

        // a plain manual's single charge is spent on a teaching blow: it turns into a random manual
        mob_living_carbon_human carol = r.Dummy(4, 4);
        var plain = Hold<obj_item_language_manual_codespeak_manual>(r, bob);
        r.In(() => Waiting.NoSleep(plain.attack__proc(carol, bob)));
        Assert.True(r.In(() => Dm.truthy(carol.has_language(typeof(datum_language_codespeak)))));
        Assert.True(r.In(() => QDELETED(plain)));

        // codespeak scrambles a sentence into code phrases, and keeps its punctuation
        string scrambled = r.In(() => Dm.str(Dm.@new<datum_language_codespeak>().scramble_sentence("Where is the disk?")));
        Assert.EndsWith("?", scrambled);
        Assert.NotEqual("Where is the disk?", scrambled);
        r.AssertNoRuntimes();
    }
}
