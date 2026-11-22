using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Tgui;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Net;
using Anomaly.Protocol;
using Anomaly.Server.Tests.M3Maps.R4;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Integration;

/// <summary>
/// Syndicate N1b (D316) through the real server on MetaStation: with the syndicate base loaded (the admin's lazyload), a player made the
/// operatives' leader wakes on it, with the uplink, the implants and the war declaration; the infiltrator loads and flies to its dock at the base,
/// and the team finds the base's nuke and learns its code, the leader on paper. The team's result is a crew win while the disk stays
/// on the station and the infiltrator at home. From its console's window the infiltrator flies to MetaStation's northwest dock.
/// N1c: the midround nukies ruleset, forced, polls the ghosts; two ghosts who want the role and sign up wake on the base as the team.
/// </summary>
public class NukieGameplayTests(ITestOutputHelper output)
{
    private const int Second = 20;

    private static T In<T>(RealHeadlessWorld w, Func<T> f)
    {
        using (w.World.Enter())
            return f();
    }

    private static void Do(RealHeadlessWorld w, Action a)
    {
        using (w.World.Enter())
            a();
    }

    private static void RunUntil(RealHeadlessWorld w, Func<bool> done, int seconds)
    {
        for (int i = 0; i < seconds * 4 && !In(w, done); i++)
            w.Run(Second / 4);
    }

    private static string DockedAt(obj_docking_port_mobile ship) => ship.get_docked() is obj_docking_port_stationary dock ? Dm.str(dock.shuttle_id) : null;

    [MetaServerFact]
    public void The_leader_wakes_on_the_base_and_the_infiltrator_flies_to_the_station()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 48, TimeoutSteps = 60000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        var me = (mob_living_carbon_human)w.MobOf(alice);
        w.Run(Second);

        // the admin loads the base first (the lazyload command, tg's Load/Jump Lazy Template): a leader made before it calls an infiltrator
        // whose dock at the base does not exist yet, and it stays in transit (tg's own: initiate_docking's null destination)
        Do(w, () => Waiting.Detach(SSmapping.lazy_load_template(LAZY_TEMPLATE_KEY_NUKIEBASE)));
        RunUntil(w, () => SSshuttle.getDock("syndicate_away") != null, 60);
        Assert.NotNull(In(w, () => SSshuttle.getDock("syndicate_away")));
        var leader = In(w, () => Dm.@new<datum_antagonist_nukeop_leader>());
        Do(w, () => Waiting.Detach(me.mind.add_antag_datum(leader)));
        RunUntil(w, () => Dm.truthy(me.onSyndieBase()), 60);
        output.WriteLine("at " + In(w, () => Dm.str(AREACOORD(me))) + "; server errors:\n" + string.Join("\n", w.Err.ToString().Split('\n').TakeLast(60)));
        Assert.True(In(w, () => Dm.truthy(me.onSyndieBase())), "on the syndicate base");
        // made one over her crew clothes (an outfit does not replace what is worn: the round's own squad is NukieRoundTests'): the uplink
        // in her bag, the weapons and explosive implants
        Assert.NotNull(In(w, () => me.mind.find_syndicate_uplink()));
        Assert.Contains(In(w, () => Dm.each<obj_item_implant>(me.implants).ToList()), i => i is obj_item_implant_explosive);
        // the base loaded whole, no runtime: its nitrogen tank (on the catwalk outside) live and full
        Assert.DoesNotContain("runtime", w.Err.ToString());
        var tank = In(w, () => Dm.each<obj_machinery_atmospherics_components_tank_nitrogen>(Dm.world).FirstOrDefault(t => Dm.truthy(is_reserved_level(t.z))));
        Assert.NotNull(tank);
        Assert.True(In(w, () => Dm.num(tank.air_contents.return_pressure())) > 20000);
        Assert.Contains(In(w, () => Dm.each<obj_item>(me.held_items).ToList()), item => item is obj_item_nuclear_challenge);

        // the infiltrator loads on the reserved level and flies home
        var ship = In(w, () => SSshuttle.getShuttle("syndicate") as obj_docking_port_mobile);
        Assert.NotNull(ship);
        RunUntil(w, () => DockedAt(ship) == "syndicate_away", 120);
        Assert.Equal("syndicate_away", In(w, () => DockedAt(ship)));

        // the team's nuke is the base's; the code is the leader's, on paper too
        datum_team_nuclear team = leader.nuke_team;
        RunUntil(w, () => team.tracked_nuke != null, 40);
        var nuke = In(w, () => team.tracked_nuke as obj_machinery_nuclearbomb_syndicate);
        Assert.NotNull(nuke);
        Assert.True(In(w, () => Dm.truthy(nuke.onSyndieBase())));
        string code = In(w, () => Dm.str(team.memorized_code));
        Assert.Equal(code, In(w, () => Dm.str(nuke.r_code)));
        Assert.Contains(code, In(w, () => Dm.str(leader.antag_memory)));
        Assert.NotNull(In(w, () => Dm.locate<obj_item_paper>(me.get_all_contents())));
        output.WriteLine("team " + In(w, () => Dm.str(team.name)) + ", leader " + In(w, () => Dm.str(me.real_name)) + ", code " + code);

        // nothing went off, the disk is on the station: the crew is winning
        Assert.True(In(w, () => is_infiltrator_docked_at_syndiebase()));
        Assert.Equal(NUKE_RESULT_CREW_WIN, In(w, () => team.get_result()));

        // the infiltrator's console flies it to MetaStation's northwest dock
        var console = In(w, () => Dm.each<obj_machinery_computer_shuttle_syndicate>(Dm.world).First(c => c is not obj_machinery_computer_shuttle_syndicate_recall
            && c is not obj_machinery_computer_shuttle_syndicate_drop_pod && Dm.eq(c.shuttleId, "syndicate")));
        Do(w, () =>
        {
            turf_open front = Dm.each<turf>(orange(1, console)).OfType<turf_open>().First(t => !Dm.truthy(t.density));
            Waiting.NoSleep(me.forceMove(front));
            // her hands hold the war declaration and the code: set down, so her click works the console
            foreach (obj_item held in Dm.each<obj_item>(me.held_items).ToList())
                Waiting.NoSleep(me.dropItemToGround(held));
        });
        w.Run(Second / 2);
        Assert.True(alice.Session.Click(CryoGameplayTests.On(console), ClickButton.Left, ClickModifiers.None));
        w.Run(10);
        var windows = new TguiWindows { SendAct = (window, action, parameters) => alice.Session.UiAct(window, action, parameters), SendClose = window => alice.Session.UiClose(window) };
        windows.Sync(alice.Session.Replica.UiWindows, 1280, 720, 1);
        TguiWindow window = windows.Open.First(x => x.Interface == "ShuttleConsole");
        window.Context.Act("move", ("shuttle_id", "syndicate_nw"));
        RunUntil(w, () => DockedAt(ship) == "syndicate_nw", 180);
        Assert.Equal("syndicate_nw", In(w, () => DockedAt(ship)));
        Assert.True(In(w, () => Dm.truthy(is_station_level(((turf)get_turf(console)).z))), "the console flew with the infiltrator");
        Assert.False(In(w, () => is_infiltrator_docked_at_syndiebase()));
        output.WriteLine("server errors at the end:\n" + string.Join("\n", w.Err.ToString().Split('\n').TakeLast(40)));
    }

    [MetaServerFact]
    public void Two_ghosts_who_sign_up_for_the_midround_nukies_wake_on_the_base_as_the_team()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 50, TimeoutSteps = 60000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player[] ghosts = { w.Join("Gina"), w.Join("Hal") };
        w.Run(Second);

        // Gina and Hal want the midround operative (the lobby's Antagonists page) and are ghosts. The ruleset forced (tg's admin panel)
        // polls them; tg's cap for three players is two operatives (1 + ceil(3 / 18)), so both sign up
        client[] clients = ghosts.Select(p => In(w, () => w.MobOf(p).client)).ToArray();
        Do(w, () =>
        {
            foreach (client c in clients)
            {
                Dm.append(ref c.prefs.be_special, ROLE_OPERATIVE_MIDROUND);
                ((mob)c.mob).ghostize(Dm.TRUE);
            }
        });
        w.Run(2);
        Do(w, () => Waiting.Detach(SSdynamic.force_run_midround(typeof(datum_dynamic_ruleset_midround_from_ghosts_nukies))));
        for (int g = 0; g < ghosts.Length; g++)
        {
            var ghost = Assert.IsType<mob_dead_observer>(In(w, () => clients[g].mob));
            atom_movable_screen_alert_poll_alert alert = null;
            for (int i = 0; i < 20 && alert == null; i++)
            {
                w.Run(Second / 2);
                alert = In(w, () => Dm.each(ghost.alerts).Select(k => ghost.alerts[k]).OfType<atom_movable_screen_alert_poll_alert>().SingleOrDefault());
            }
            Assert.NotNull(alert);
            w.Run(2);
            Assert.True(ghosts[g].Session.Click(new ClickTarget(TargetKind.Screen, In(w, () => alert.dm_id), 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
            w.Run(4);
            Assert.True(In(w, () => alert.poll != null && Dm.@in(ghost, alert.poll.signed_up)), ghosts[g].Name + " signed up");
        }

        // the poll's end: both wake in new bodies on the base, one the leader, and the infiltrator is called to it
        RunUntil(w, () => clients.All(c => c.mob is mob_living_carbon_human h && h.mind?.has_antag_datum(typeof(datum_antagonist_nukeop)) != null && Dm.truthy(h.onSyndieBase())), 90);
        output.WriteLine("server errors:\n" + string.Join("\n", w.Err.ToString().Split('\n').TakeLast(30)));
        foreach (client c in clients)
        {
            var op = Assert.IsType<mob_living_carbon_human>(In(w, () => c.mob));
            Assert.NotNull(In(w, () => op.mind.has_antag_datum(typeof(datum_antagonist_nukeop))));
            Assert.True(In(w, () => Dm.truthy(op.onSyndieBase())), "on the syndicate base");
        }
        Assert.Single(clients, c => In(w, () => ((mob)c.mob).mind.has_antag_datum(typeof(datum_antagonist_nukeop_leader)) != null));
        Assert.NotNull(In(w, () => SSshuttle.getShuttle("syndicate")));
        Assert.Contains(In(w, () => Dm.each<datum_dynamic_ruleset>(SSdynamic.executed_rulesets).ToList()), r => r is datum_dynamic_ruleset_midround_from_ghosts_nukies);
        Assert.DoesNotContain("runtime", w.Err.ToString());
    }
}
