using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M2Locomotion.P4;

/// <summary>
/// The click chain from the client seam (M2 P4, scope 2f, Q8): <c>ClientSeam.Click</c> runs tg's <c>/client/Click</c>,
/// BYOND's default after it and <c>/atom/Click</c>, then <c>ClickOn</c> (next_click, the incapacitated check, face_atom,
/// next_move, reach by Adjacent on tiles, <c>CLICK_CD_MELEE</c>) and the unarmed attack chain to <c>attack_hand</c>. The
/// shove itself (the human's <c>attack_hand</c> to <c>spec_attack_hand</c> to <c>disarm</c>) is M2 P1's and P2's; this
/// pins that the chain reaches <c>attack_hand</c> with tg's right-click modifiers.
/// </summary>
public class ClickChainTests
{
    private static (CapsuleRoom Room, mob_living_carbon_human A, mob_living_carbon_human B, datum_test_m2p4_listener La, datum_test_m2p4_listener Lb) Setup(ulong seed, int bx = 6, int by = 5)
    {
        var room = new CapsuleRoom(seed);
        mob_living_carbon_human b = room.Dummy(bx, by);
        mob_living_carbon_human a = room.Join<mob_living_carbon_human_test_m2p4_hands>(5, 5, "Alice");
        // tg's brain organ gives a human TRAIT_ADVANCEDTOOLUSER (organ_traits); the organs are M2 P2's.
        room.In(() => ADD_TRAIT(a, TRAIT_ADVANCEDTOOLUSER, "test"));
        datum_test_m2p4_listener la = room.In(() => Dm.@new<datum_test_m2p4_listener>());
        datum_test_m2p4_listener lb = room.In(() => Dm.@new<datum_test_m2p4_listener>());
        room.In(() => { la.Watch(a); lb.Watch(b); });
        return (room, a, b, la, lb);
    }

    private static void Click(CapsuleRoom room, mob a, atom target, string button)
    {
        room.Command(() => ClientSeam.Click(a.client, target, ClientSeam.ClickParams(button)));
        room.Step();
    }

    [Fact]
    public void ARightClickOnAnAdjacentHumanReachesItsAttackHandWithTheMeleeCooldown()
    {
        var (room, a, b, la, lb) = Setup(71);
        float clickedAt = room.In(() => world.time) + World.TickLag;
        Click(room, a, b, "right");
        string by = "#" + a.dm_id;
        Assert.Equal(new[] { "attack_hand_secondary " + by + " right", "attack_hand " + by + " right" },
            lb.Trace.Where(t => t.StartsWith("attack_hand", StringComparison.Ordinal)).ToArray());
        Assert.Contains("unarmed #" + b.dm_id + " 1", la.Trace);
        Assert.Equal(EAST, room.In(() => a.dir)); // face_atom
        Assert.Equal(clickedAt + CLICK_CD_MELEE, Dm.num(room.In(() => a.next_move)));
        Assert.Equal(clickedAt + 1, room.In(() => a.next_click));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ALeftClickGoesStraightToAttackHand()
    {
        var (room, a, b, _, lb) = Setup(72);
        Click(room, a, b, "left");
        Assert.Equal(new[] { "attack_hand #" + a.dm_id + " left" }, lb.Trace.Where(t => t.StartsWith("attack_hand", StringComparison.Ordinal)).ToArray());
        room.AssertNoRuntimes();
    }

    [Fact]
    public void TheNextClickWaitsForTheMeleeCooldown()
    {
        var (room, a, b, _, lb) = Setup(73);
        var attacks = new List<long>();
        long start = room.World.Tick;
        for (int i = 0; i <= 18; i += 2) // a click every 2 ticks (under tg's per-second click limit)
        {
            int before = lb.Count("attack_hand ");
            // a left click: with the human attack_hand chain landed (M2 T2), a right click shoves the target out of reach
            Click(room, a, b, "left");
            if (lb.Count("attack_hand ") > before) attacks.Add(room.World.Tick - start);
            room.Step();
        }
        Assert.Equal(2, attacks.Count);
        Assert.Equal(16, attacks[1] - attacks[0] - 0); // CLICK_CD_MELEE 8 ds = 16 ticks
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AHumanTwoTilesAwayIsOutOfReach()
    {
        var (room, a, b, la, lb) = Setup(74, bx: 7);
        float before = Dm.num(room.In(() => a.next_move)); // tg's mob/Login set it to 1
        Click(room, a, b, "right");
        Assert.Equal(0, lb.Count("attack_hand"));
        Assert.Contains("ranged_secondary #" + b.dm_id, la.Trace);
        Assert.Equal(before, Dm.num(room.In(() => a.next_move))); // no melee cooldown for a ranged click
        room.AssertNoRuntimes();
    }

    [Fact]
    public void WithoutAdvancedToolUseTheHumanAttacksLikeAMonkey()
    {
        var (room, a, b, _, lb) = Setup(75);
        // M2 P2: the human's brain now gives the trait too (organ_traits), so every source but ROUNDSTART_TRAIT goes (null
        // sources, _traits.dm REMOVE_TRAIT).
        room.In(() => REMOVE_TRAIT(a, TRAIT_ADVANCEDTOOLUSER, null));
        Click(room, a, b, "left");
        Assert.Equal(0, lb.Count("attack_hand ")); // the carbon's resolve_unarmed_attack: attack_paw
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AnIncapacitatedOrHandsBlockedMobClicksAsTgSays()
    {
        var (room, a, b, la, lb) = Setup(76);
        room.In(() => a.incapacitated = INCAPABLE_STASIS); // ignored by ClickOn's check (INCAPACITATED_IGNORING)
        Click(room, a, b, "left");
        Assert.Equal(1, lb.Count("attack_hand "));
        room.Step(20);
        room.In(() => a.incapacitated = INCAPABLE_GRAB);
        Click(room, a, b, "left");
        Assert.Equal(1, lb.Count("attack_hand ")); // incapacitated: nothing
        room.In(() => a.incapacitated = NONE);
        room.In(() => ADD_TRAIT(a, TRAIT_HANDS_BLOCKED, "test"));
        room.Step(2); // next_click: one decisecond after the last click that passed it
        Click(room, a, b, "left");
        // Hands blocked: CLICK_CD_HANDCUFFED and UnarmedAttack, whose can_unarmed_attack refuses (no attack_hand).
        Assert.Equal(1, lb.Count("attack_hand "));
        Assert.Equal(room.In(() => world.time) + CLICK_CD_HANDCUFFED, Dm.num(room.In(() => a.next_move)));
        room.AssertNoRuntimes();
    }

    [Fact]
    public void AClickOnADeletedTargetOrFromAClientThatLeftDoesNothing()
    {
        var (room, a, b, la, lb) = Setup(77);
        client player = room.In(() => a.client);
        room.Command(() => qdel(b));
        room.Step();
        room.Command(() => ClientSeam.Click(player, b, ClientSeam.ClickParams("left")));
        room.Step();
        Assert.Equal(0, lb.Count("attack_hand"));
        room.Command(() => ClientSeam.Disconnect(player));
        room.Step(2);
        mob_living_carbon_human c = room.Dummy(5, 6);
        room.Command(() => ClientSeam.Click(player, c, ClientSeam.ClickParams("left")));
        room.Step();
        room.AssertNoRuntimes();
    }

    [Fact]
    public void ATurfClickIsAClickOnTheTurf()
    {
        var (room, a, _, la, _) = Setup(78);
        turf floor = room.In(() => locate(5, 7, 1));
        Click(room, a, floor, "left"); // two tiles north: out of reach, a ranged attack with no target effect
        Assert.Equal(NORTH, room.In(() => a.dir));
        room.AssertNoRuntimes();
    }
}
