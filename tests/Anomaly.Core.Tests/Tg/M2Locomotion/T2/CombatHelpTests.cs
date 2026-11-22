using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tests.Tg.Wave4.Integration;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.Wave4.Integration.Toy;

namespace Anomaly.Core.Tests.Tg.M2Locomotion.T2;

/// <summary>
/// combat_help.dm in the capsule world: clicking on people with the help intent (combat mode off) ends in help_shake_act,
/// which sends COMSIG_CARBON_PRE_MISC_HELP to the clicked one before anything else (carbon_defense.dm:296), and shaking a
/// lying mob gets it up (:355-358).
/// </summary>
public class CombatHelpTests
{
    private static readonly ushort East = T2Room.Yaw(EAST);

    // tg's click_wrapper (unit_test.dm:201): clicker.next_click = -1, next_move = -1, then ClickOn(clicked_on, params)
    private static void ClickWrapper(T2Room r, mob_living clicker, atom clicked_on)
    {
        r.Command(() =>
        {
            clicker.next_click = -1;
            clicker.next_move = -1;
            Waiting.NoSleep(clicker.ClickOn(clicked_on, ClientSeam.ClickParams(LEFT_CLICK)));
        });
        r.Step();
    }

    // tg: code/modules/unit_tests/combat_help.dm:/datum/unit_test/help_click/Run
    [Fact]
    public void UnitTestHelpClickClickingOnYourselfChecksYourselfAndClickingOnAnotherHelpsThem()
    {
        var r = new T2Room(1401);
        mob_living_carbon_human helps_the_guy = r.Dummy<mob_living_carbon_human_consistent>(5, 5, name: "helper");
        mob_living_carbon_human gets_the_help = r.Dummy<mob_living_carbon_human_consistent>(5, 5, name: "helpee");
        r.Command(() => Waiting.NoSleep(gets_the_help.forceMove(locate(6, 5, 1))));
        r.Step();
        int helper_times_helped = 0, helpee_times_helped = 0;
        r.In(() =>
        {
            On(helps_the_guy, COMSIG_CARBON_PRE_MISC_HELP, "helper_help_received", 0, act: _ => helper_times_helped++);
            On(gets_the_help, COMSIG_CARBON_PRE_MISC_HELP, "helpee_help_received", 0, act: _ => helpee_times_helped++);
        });

        // Click on self
        ClickWrapper(r, helps_the_guy, helps_the_guy);
        Assert.Equal(1, helper_times_helped); // "Helper should have been helped once - clicking on themselves should check self."
        Assert.Equal(0, helpee_times_helped); // "Helpee should not have been helped - helper clicked on themselves."

        // Click on the other guy
        ClickWrapper(r, helps_the_guy, gets_the_help);
        Assert.Equal(1, helper_times_helped); // "Helper should not have been helped - helper clicked on helpee."
        Assert.Equal(1, helpee_times_helped); // "Helpee should have been helped once - helper clicked on helpee."
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHelpClickFromAPlayerGoesThroughTheClientSeamToo()
    {
        var r = new T2Room(1402);
        mob_living_carbon_human b = r.Dummy(9, 8, name: "helpee");
        mob_living_carbon_human a = r.Join(8, 8, "Alice", name: "helper");
        int helped = 0;
        r.In(() => On(b, COMSIG_CARBON_PRE_MISC_HELP, "helpee_help_received", 0, act: _ => helped++));
        r.Command(() => ClientSeam.Click(a.client, b, ClientSeam.ClickParams(LEFT_CLICK)));
        r.Step();
        Assert.Equal(1, helped);
        Assert.Equal((9, 8, 1), r.LocOf(b)); // nobody is pushed or moved by a hug
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABlockedHelpActStopsAtOnceAndHelpActAndHelpedAreSentOtherwise()
    {
        var r = new T2Room(1403);
        mob_living_carbon_human b = r.Dummy(9, 8, name: "helpee");
        mob_living_carbon_human a = r.Join(8, 8, "Alice", name: "helper");
        datum_test_integration_probe gate = null;
        r.In(() =>
        {
            gate = On(b, COMSIG_CARBON_PRE_MISC_HELP, "pre", 1, result: COMPONENT_BLOCK_MISC_HELP);
            On(b, COMSIG_CARBON_HELP_ACT, "help_act", 1);
            On(a, COMSIG_CARBON_HELPED, "helped", 1);
        });
        r.Command(() => ClientSeam.Click(a.client, b, ClientSeam.ClickParams(LEFT_CLICK)));
        r.Step(16); // the melee cooldown of the first click
        Assert.Equal(new[] { "pre helpee helper" }, r.Trace);
        r.Trace.Clear();
        r.In(() => gate.result = NONE);
        r.Command(() => ClientSeam.Click(a.client, b, ClientSeam.ClickParams(LEFT_CLICK)));
        r.Step(4);
        Assert.Equal(new[] { "pre helpee helper", "help_act helpee helper", "helped helper helpee" }, r.Trace);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ShakingARestingMobGetsItUpAndItsCapsuleStandsAgain()
    {
        var r = new T2Room(1404);
        mob_living_carbon_human b = r.Dummy(9, 8, name: "helpee");
        mob_living_carbon_human a = r.Join(8, 8, "Alice", name: "helper");
        r.Command(() => Waiting.NoSleep(b.set_resting(TRUE, TRUE)));
        r.Step(2);
        Assert.Equal(Posture.Lying, r.State(b).Posture);
        r.Command(() => ClientSeam.Click(a.client, b, ClientSeam.ClickParams(LEFT_CLICK)));
        r.Step(2);
        Assert.False(Dm.truthy(r.In(() => b.resting)));
        Assert.Equal(STANDING_UP, Dm.num(r.In(() => b.body_position)));
        Assert.Equal(Posture.Standing, r.State(b).Posture);
        r.AssertNoRuntimes();
    }
}
