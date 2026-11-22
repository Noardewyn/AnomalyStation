using Anomaly.Client.Shared;
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
/// Gaps X5 (D333) through the real server on MetaStation: the alien infestation (tg's xenomorph midround ruleset), forced as tg's admin
/// panel does, polls the ghosts; the ghost who wants the alien role and signs up wakes as an alien larva inside one of the station's
/// vents (a vent on a network of more than twenty machines, tg's find_vent_spawns), a xenomorph of the hive; the larva climbs out of
/// the vent onto the station.
/// </summary>
public class XenomorphGameplayTests(ITestOutputHelper output)
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

    [MetaServerFact]
    public void The_infestation_puts_the_ghost_who_signs_up_into_a_vent_as_a_larva_that_climbs_out()
    {
        using var w = new RealHeadlessWorld(new LoopbackOptions { Seed = 53, TimeoutSteps = 60000 },
            MapJoinTests.MapConfig("metastation", MetaServer.Folder(), noInterest: true), strict: false);
        RealHeadlessWorld.Player alice = w.Join("Alice");
        RealHeadlessWorld.Player gina = w.Join("Gina");
        w.Run(Second);

        // the station has vents for it: tg's ruleset needs a vent network of more than twenty machines
        Assert.True(In(w, () => Dm.length(find_vent_spawns())) > 0, "Meta's vents");

        // Gina wants the alien role (the lobby's Antagonists page) and is a ghost; the ruleset forced polls her
        client ginaClient = In(w, () => w.MobOf(gina).client);
        Do(w, () =>
        {
            Dm.append(ref ginaClient.prefs.be_special, ROLE_ALIEN);
            ((mob)ginaClient.mob).ghostize(Dm.TRUE);
        });
        w.Run(2);
        Do(w, () => Waiting.Detach(SSdynamic.force_run_midround(typeof(datum_dynamic_ruleset_midround_from_ghosts_xenomorph))));
        var ghost = Assert.IsType<mob_dead_observer>(In(w, () => ginaClient.mob));
        atom_movable_screen_alert_poll_alert alert = null;
        for (int i = 0; i < 20 && alert == null; i++)
        {
            w.Run(Second / 2);
            alert = In(w, () => Dm.each(ghost.alerts).Select(k => ghost.alerts[k]).OfType<atom_movable_screen_alert_poll_alert>().SingleOrDefault());
        }
        Assert.NotNull(alert);
        w.Run(2);
        Assert.True(gina.Session.Click(new ClickTarget(TargetKind.Screen, In(w, () => alert.dm_id), 0, 0, 0, 16, 16), ClickButton.Left, ClickModifiers.None));
        w.Run(4);
        Assert.True(In(w, () => alert.poll != null && Dm.@in(ghost, alert.poll.signed_up)), "Gina signed up");

        // the poll's end: Gina is a larva inside a vent, a xenomorph of the hive
        RunUntil(w, () => ginaClient.mob is mob_living_carbon_alien_larva, 60);
        output.WriteLine("server errors:\n" + string.Join("\n", w.Err.ToString().Split('\n').TakeLast(30)));
        var larva = Assert.IsType<mob_living_carbon_alien_larva>(In(w, () => ginaClient.mob));
        var vent = Assert.IsAssignableFrom<obj_machinery_atmospherics_components_unary_vent_pump>(In(w, () => larva.loc));
        Assert.True(In(w, () => HAS_TRAIT(larva, TRAIT_MOVE_VENTCRAWLING)));
        Assert.True(In(w, () => Dm.truthy(is_station_level(vent.z))));
        Assert.NotNull(In(w, () => larva.mind.has_antag_datum(typeof(datum_antagonist_xeno))));
        Assert.Contains(In(w, () => Dm.each<datum_dynamic_ruleset>(SSdynamic.executed_rulesets).ToList()), r => r is datum_dynamic_ruleset_midround_from_ghosts_xenomorph);

        // she climbs out of the vent onto its tile (tg's second of do_after)
        Do(w, () => Waiting.Detach(larva.handle_ventcrawl(vent)));
        RunUntil(w, () => larva.loc is turf, 5);
        Assert.Same(In(w, () => vent.loc), In(w, () => larva.loc));
        Assert.False(In(w, () => HAS_TRAIT(larva, TRAIT_MOVE_VENTCRAWLING)));
        Assert.DoesNotContain("runtime", w.Err.ToString());
    }
}
