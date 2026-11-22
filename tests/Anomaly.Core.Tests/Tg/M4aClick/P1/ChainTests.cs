using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.P4;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4aClick.P1;

/// <summary>
/// tg's click and item chain (M4a P1) from the client seam on a fixture room: ClickOn's branches with an empty hand and with
/// an item, the modifier clicks of click_ctrl.dm and click_alt.dm, reach, and tg's params keys reaching the item procs.
/// </summary>
public class ChainTests
{
    private const string Left = "left=1;button=left";
    private const string Right = "right=1;button=right";

    [Fact]
    public void AnEmptyHandOnAMachineReachesItsAttackHand()
    {
        var (room, a, _) = P1.Room(401);
        var machine = P1.At<obj_structure_test_p1_machine>(room, 6, 5);
        P1.Click(room, a, machine, Left);
        Assert.Equal(new[] { "attack_hand left" }, machine.Trace);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AnEmptyHandRightClickRunsTheSecondaryChainFirst()
    {
        var (room, a, _) = P1.Room(402);
        var machine = P1.At<obj_structure_test_p1_machine>(room, 6, 5);
        P1.Click(room, a, machine, Right);
        Assert.Equal(new[] { "attack_hand_secondary", "attack_hand right" }, machine.Trace);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AnEmptyHandOnAFloorRunsTheTurfsAttackHandAndAnItemOnItTheTurfsItemInteraction()
    {
        // tg's /turf/attack_hand (turf.dm:244: Move_Pulled, nothing pulled) and /turf/item_interaction (turf.dm:417: no
        // cable, pipe cleaner or RCL in hand) run; neither is refused by the guard.
        var (room, a, sinks) = P1.Room(403);
        turf floor = room.In(() => locate(6, 5, 1) as turf);
        P1.Click(room, a, floor, Left);
        var item = P1.Hold<obj_item_test_p1_stub>(room, a);
        room.Step(4);
        P1.Click(room, a, floor, Left);
        Assert.DoesNotContain(P1.Deferred(room), s => s.StartsWith("inert guard:", StringComparison.Ordinal));
        Assert.DoesNotContain(sinks.Lines, l => l.EndsWith("not ported yet", StringComparison.Ordinal));
        Assert.Equal(new[] { "interact_with_atom ,", "pre_attack" }, item.Trace); // a turf is not attacked (atom attackby)
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AnEmptyHandOnAHumanReachesTheHumansAttackHand()
    {
        var (room, a, _) = P1.Room(404);
        mob_living_carbon_human b = room.Dummy(6, 5);
        var lb = room.In(() => Dm.@new<datum_test_m2p4_listener>());
        room.In(() => lb.Watch(b));
        P1.Click(room, a, b, Left);
        Assert.Equal(1, lb.Count("attack_hand "));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AnItemOnAHumanRunsTgsWholeMeleeChainInOrder()
    {
        var (room, a, _) = P1.Room(405);
        var b = P1.At<mob_living_carbon_human_test_p1_target>(room, 6, 5);
        var item = P1.Hold<obj_item_test_p1_stub>(room, a);
        float at = room.In(() => world.time) + World.TickLag;
        P1.Click(room, a, b, Left + ";icon-x=12;icon-y=20");
        // base_item_interaction: tool_act (no tool behaviour), the living mob's item_interaction (NONE), the tool's
        // interact_with_atom with tg's params; then pre_attack, the living attackby (attack_speed), attack, attacked_by,
        // afterattack.
        Assert.Equal(new[] { "interact_with_atom 12,20", "pre_attack", "afterattack" }, item.Trace);
        Assert.Equal(new[] { "attacked_by " + room.In(() => item.name) }, b.Trace);
        Assert.Equal(at + room.In(() => item.attack_speed), Dm.num(room.In(() => a.next_move)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AnItemOnAMachineGoesThroughItsItemInteractionThenTheObjAttack()
    {
        var (room, a, _) = P1.Room(406);
        var machine = P1.At<obj_structure_test_p1_machine>(room, 6, 5);
        var item = P1.Hold<obj_item_test_p1_stub>(room, a);
        room.In(() => machine.obj_flags = Dm.num(Dm.bitor(machine.obj_flags, CAN_BE_HIT)));
        P1.Click(room, a, machine, Left);
        Assert.Equal(new[] { "item_interaction", "attacked_by" }, machine.Trace);
        Assert.Equal(new[] { "interact_with_atom ,", "pre_attack", "afterattack" }, item.Trace);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AnItemOnAnotherItemTakesTheChainOnTheTargetItem()
    {
        var (room, a, _) = P1.Room(407);
        var target = P1.At<obj_item_test_p1_stub>(room, 6, 5);
        var item = P1.Hold<obj_item_test_p1_stub>(room, a);
        P1.Click(room, a, target, Left);
        // /obj/attackby: an item is not CAN_BE_HIT, so no attack_atom and no afterattack; the chain ends at the tutorial.
        Assert.Equal(new[] { "interact_with_atom ,", "pre_attack" }, item.Trace);
        Assert.Empty(target.Trace);
        Assert.Contains("code/_onclick/item_attack.dm:79", P1.Deferred(room));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AClickOnTheHeldItemIsItsAttackSelfAndTheRightClickItsSecondary()
    {
        var (room, a, _) = P1.Room(408);
        var item = P1.Hold<obj_item_test_p1_stub>(room, a);
        P1.Click(room, a, item, Left);
        room.Step(4);
        P1.Click(room, a, item, Right);
        Assert.Equal(new[] { "attack_self", "interact", "attack_self_secondary" }, item.Trace);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AnItemOutOfReachIsARangedItemInteraction()
    {
        var (room, a, _) = P1.Room(409);
        var b = P1.At<mob_living_carbon_human_test_p1_target>(room, 8, 5);
        var item = P1.Hold<obj_item_test_p1_stub>(room, a);
        float before = Dm.num(room.In(() => a.next_move));
        P1.Click(room, a, b, Left);
        Assert.Equal(new[] { "ranged_interact_with_atom" }, item.Trace);
        Assert.Empty(b.Trace);
        Assert.Equal(before, Dm.num(room.In(() => a.next_move)));
        room.AssertNoRuntimes();
    }

    [Theory]
    [InlineData("left=1;ctrl=1;button=left", "click_ctrl")]
    [InlineData("left=1;alt=1;button=left", "click_alt")]
    [InlineData("right=1;alt=1;button=right", "click_alt_secondary")]
    [InlineData("left=1;ctrl=1;shift=1;button=left", "click_ctrl_shift")]
    public void TheModifierClicksReachTheTargetsClickProcs(string @params, string expected)
    {
        var (room, a, _) = P1.Room(410);
        var machine = P1.At<obj_structure_test_p1_machine>(room, 6, 5);
        P1.Click(room, a, machine, @params);
        Assert.Equal(new[] { expected }, machine.Trace);
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AModifierClickOutOfReachDoesNothing()
    {
        var (room, a, _) = P1.Room(411);
        var machine = P1.At<obj_structure_test_p1_machine>(room, 8, 5);
        P1.Click(room, a, machine, "left=1;alt=1;button=left");
        Assert.Empty(machine.Trace); // can_perform_action: not adjacent
        room.AssertNoRuntimes();
    }
}
