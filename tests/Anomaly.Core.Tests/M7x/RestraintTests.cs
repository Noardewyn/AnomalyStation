using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7x;

/// <summary>
/// The M7x slice E (restraints and batons, docs/ai/specs/weapons.md 3): the bear trap closing on a walker's legs (handcuffs.dm
/// spring_trap), the cuff resist timer (carbon.dm cuff_resist, a restrained man's resisting takes the cuffs' breakouttime and
/// frees his hands), the cable restraints' and the zipties' break (the used zipties cannot cuff), a stun baton's cell, charge and
/// toggle (baton.dm) and the OZtek boomerang flying back to its thrower (components/boomerang.dm).
/// </summary>
public class RestraintTests
{
    private const int Second = 20;
    private const int Width = 14, Height = 7, LineY = 4;

    private static AudioRoom Room(ulong seed)
    {
        var r = new AudioRoom(Width, Height, seed: seed);
        r.Step(Second);
        return r;
    }

    [Fact]
    public void ABearTrapClosesOnTheLegsOfAManWhoStepsOnIt()
    {
        AudioRoom r = Room(7801);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        obj_item_restraints_legcuffs_beartrap trap = r.In(() => Dm.@new<obj_item_restraints_legcuffs_beartrap_prearmed>(locate(4, LineY, 1)));
        r.Step(Second);
        Assert.Equal("beartrap1", r.In(() => trap.icon_state));
        Assert.Equal(0f, r.In(() => Dm.num(bob.get_brute_loss())));

        r.In(() => Assert.True(Dm.truthy(Waiting.NoSleep(bob.Move(locate(4, LineY, 1), EAST))), "the step was refused"));
        // read on the tick the trap closes: the wounded man's healing starts at once (as EmbeddingTests reads the star's damage)
        for (int tick = 0; tick < Second && r.In(() => bob.legcuffed) == null; tick++)
            r.Step(1);

        // the trap closed, caught a leg and bit it: tg's trap_damage 20 on one of the legs
        Assert.Equal(0f, r.In(() => trap.armed));
        Assert.Equal("beartrap0", r.In(() => trap.icon_state));
        Assert.Same(trap, r.In(() => bob.legcuffed));
        Assert.Equal(20f, r.In(() => Dm.num(bob.get_brute_loss())));
        Assert.Equal(20f, r.In(() => Dm.num((bob.get_bodypart(BODY_ZONE_L_LEG) as obj_item_bodypart).brute_dam) + Dm.num((bob.get_bodypart(BODY_ZONE_R_LEG) as obj_item_bodypart).brute_dam)));
        Assert.True(r.In(() => Dm.truthy(bob.alerts?["legcuffed"])), "the legcuffed alert");
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/snap.ogg");
        // a closed trap bites nobody else
        mob_living_carbon_human rob = r.Dummy(5, LineY);
        r.In(() => Waiting.NoSleep(rob.Move(locate(4, LineY, 1), WEST)));
        r.Step(Second);
        Assert.Equal(0f, r.In(() => Dm.num(rob.get_brute_loss())));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnUnarmedBearTrapIsHarmlessAndTheSecondaryClickArmsIt()
    {
        AudioRoom r = Room(7802);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        obj_item_restraints_legcuffs_beartrap trap = r.In(() => Dm.@new<obj_item_restraints_legcuffs_beartrap>(locate(4, LineY, 1)));
        Assert.Equal("beartrap0", r.In(() => trap.icon_state));
        r.In(() => Waiting.NoSleep(bob.Move(locate(4, LineY, 1), EAST)));
        r.Step(Second);
        Assert.Null(r.In(() => bob.legcuffed));
        Assert.Equal(0f, r.In(() => Dm.num(bob.get_brute_loss())));

        // used in hand it arms: attack_self toggles armed (handcuffs.dm:466)
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(trap)));
        r.In(() => Waiting.NoSleep(trap.attack_self(bob)));
        Assert.Equal(1f, r.In(() => trap.armed));
        Assert.Equal("beartrap1", r.In(() => trap.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ResistingTheCuffsTakesTheirBreakoutTimeAndFreesTheHands()
    {
        AudioRoom r = Room(7803);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        obj_item_restraints_handcuffs cuffs = r.In(() => Dm.@new<obj_item_restraints_handcuffs>(locate(3, LineY, 1)));
        obj_item_pen pen = r.In(() => Dm.@new<obj_item_pen>(locate(3, LineY, 1)));
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(pen)));
        r.In(() => cuffs.breakouttime = 5 * SECONDS);
        r.In(() => Waiting.NoSleep(bob.equip_to_slot(cuffs, ITEM_SLOT_HANDCUFFED)));
        r.Step();

        // cuffed: restrained, the hands emptied, the alert thrown, the worn cuffs drawn
        Assert.Same(cuffs, r.In(() => bob.handcuffed));
        Assert.True(r.In(() => HAS_TRAIT(bob, TRAIT_RESTRAINED)));
        Assert.True(r.In(() => Dm.truthy(bob.alerts?[ALERT_HANDCUFFED])));
        Assert.Empty(r.In(() => bob.get_active_held_item() == null ? new List<object>() : new List<object> { bob.get_active_held_item() }));
        Assert.NotNull(r.In(() => bob.overlays_standing[HANDCUFF_LAYER]));

        // he resists: the do_after takes the breakout time (5 s), not a tick less
        r.Command(() => Waiting.Detach(bob.execute_resist()));
        r.Step(4 * Second);
        r.AssertNoRuntimes();
        Assert.Same(cuffs, r.In(() => bob.handcuffed));
        r.Step(2 * Second);
        Assert.Null(r.In(() => bob.handcuffed));
        Assert.False(r.In(() => HAS_TRAIT(bob, TRAIT_RESTRAINED)));
        Assert.False(r.In(() => Dm.truthy(bob.alerts?[ALERT_HANDCUFFED])));
        Assert.Equal(r.In(() => get_turf(bob)), r.In(() => get_turf(cuffs)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ZiptiesAreUsedUpWhenTheyComeOffAndCannotCuffAgain()
    {
        AudioRoom r = Room(7804);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        obj_item_restraints_handcuffs_cable_zipties ties = r.In(() => Dm.@new<obj_item_restraints_handcuffs_cable_zipties>(locate(3, LineY, 1)));
        Assert.Equal(45f * SECONDS, r.In(() => ties.breakouttime));
        r.In(() => Waiting.NoSleep(bob.equip_to_slot(ties, ITEM_SLOT_HANDCUFFED)));
        r.Step();
        Assert.Same(ties, r.In(() => bob.handcuffed));
        r.In(() => Waiting.NoSleep(bob.dropItemToGround(ties)));
        r.Step();
        Assert.Null(r.In(() => bob.handcuffed));
        Assert.Equal(1f, r.In(() => ties.used));
        Assert.Equal("cuff_used", r.In(() => ties.icon_state));
        Assert.Equal("A pair of broken zipties.", r.In(() => ties.desc));
        // a used pair does not cuff (attack returns before attempt_to_cuff)
        mob_living_carbon_human rob = r.Dummy(4, LineY);
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(ties)));
        r.In(() => Waiting.NoSleep(ties.attack__proc(rob, bob)));
        r.Step(5 * Second);
        Assert.Null(r.In(() => rob.handcuffed));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACableRestraintTakesItsColourAndWirecuttersSnapItFromAWristInstantly()
    {
        AudioRoom r = Room(7805);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        mob_living_carbon_human rob = r.Dummy(4, LineY);
        obj_item_restraints_handcuffs_cable_blue ties = r.In(() => Dm.@new<obj_item_restraints_handcuffs_cable_blue>(locate(4, LineY, 1)));
        Assert.Equal(CABLE_HEX_COLOR_BLUE, r.In(() => ties.color));
        Assert.Equal("coil_blue", r.In(() => ties.inhand_icon_state));
        r.In(() => Waiting.NoSleep(rob.equip_to_slot(ties, ITEM_SLOT_HANDCUFFED)));
        r.Step();
        Assert.Same(ties, r.In(() => rob.handcuffed));

        // the wirecutters' cuffsnapping element: a secondary click on a restrained man cuts weak restraints with no delay
        obj_item_wirecutters cutters = r.In(() => Dm.@new<obj_item_wirecutters>(locate(3, LineY, 1)));
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(cutters)));
        r.In(() => Waiting.NoSleep(cutters.attack_secondary(rob, bob, Dm.list(Dm.kv(RIGHT_CLICK, 1)), Dm.list())));
        r.Step(2 * Second);
        Assert.Null(r.In(() => rob.handcuffed));
        Assert.True(r.In(() => QDELETED(ties)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AStunBatonNeedsACellTurnsOnWithItAndSwapsItsPicture()
    {
        AudioRoom r = Room(7806);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        obj_item_melee_baton_security baton = r.In(() => Dm.@new<obj_item_melee_baton_security>(locate(3, LineY, 1)));
        Assert.Equal("stunbaton_nocell", r.In(() => baton.icon_state));
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(baton)));
        r.In(() => Waiting.NoSleep(baton.attack_self(bob)));
        Assert.True(r.In(() => baton.active) == 0f, "no cell, no stun");

        obj_item_stock_parts_power_store_cell_high cell = r.In(() => Dm.@new<obj_item_stock_parts_power_store_cell_high>(locate(3, LineY, 1)));
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(baton)));
        r.In(() => Waiting.NoSleep(baton.item_interaction(bob, cell, Dm.list())));
        Assert.Same(cell, r.In(() => baton.cell));
        Assert.Equal("stunbaton", r.In(() => baton.icon_state));

        r.In(() => Waiting.NoSleep(baton.attack_self(bob)));
        Assert.Equal(1f, r.In(() => baton.active));
        Assert.Equal("stunbaton_active", r.In(() => baton.icon_state));
        Assert.Equal("stunbaton_active", r.In(() => baton.inhand_icon_state));
        Assert.Contains(r.Sounds.Heard, h => h.File != null && h.File.Contains("sparks"));

        r.In(() => Waiting.NoSleep(baton.attack_self(bob)));
        Assert.Equal(0f, r.In(() => baton.active));
        Assert.Equal("stunbaton", r.In(() => baton.icon_state));
        // the screwdriver takes the cell out again
        obj_item_screwdriver driver = r.In(() => Dm.@new<obj_item_screwdriver>(locate(3, LineY, 1)));
        r.In(() => Waiting.NoSleep(baton.screwdriver_act(bob, driver)));
        r.Step();
        Assert.Null(r.In(() => baton.cell));
        Assert.Equal("stunbaton_nocell", r.In(() => baton.icon_state));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ATelescopicBatonExtendsIntoItsHeavierForm()
    {
        AudioRoom r = Room(7807);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        obj_item_melee_baton_telescopic baton = r.In(() => Dm.@new<obj_item_melee_baton_telescopic_gold>(locate(3, LineY, 1)));
        r.Step();
        Assert.Equal("telebaton_gold", r.In(() => baton.icon_state));
        Assert.Equal(0f, r.In(() => baton.force));
        Assert.Equal((float)WEIGHT_CLASS_SMALL, r.In(() => baton.w_class));
        Assert.Equal(0f, r.In(() => baton.active));
        r.In(() => Waiting.NoSleep(bob.put_in_active_hand(baton)));
        r.In(() => Waiting.NoSleep(baton.attack_self(bob)));
        r.Step();
        Assert.Equal(1f, r.In(() => baton.active));
        Assert.Equal("telebaton_gold_on", r.In(() => baton.icon_state));
        Assert.Equal(10f, r.In(() => baton.force));
        Assert.Equal((float)WEIGHT_CLASS_NORMAL, r.In(() => baton.w_class));
        Assert.Equal(50f, r.In(() => baton.stun_armour_penetration));
        r.Step(2 * Second);
        r.In(() => Waiting.NoSleep(baton.attack_self(bob)));
        Assert.Equal("telebaton_gold", r.In(() => baton.icon_state));
        Assert.Equal(0f, r.In(() => baton.force));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABoomerangThrownAtNothingFliesBackToItsThrower()
    {
        AudioRoom r = Room(7808);
        mob_living_carbon_human bob = r.Join(3, LineY, "Bob");
        obj_item_melee_baton_security_boomerang_loaded boomerang = r.In(() => Dm.@new<obj_item_melee_baton_security_boomerang_loaded>(locate(3, LineY, 1)));
        r.Step(Second);
        r.In(() => Waiting.NoSleep(boomerang.throw_at(locate(9, LineY, 1), 6, 1, bob)));
        r.Step(Second / 2);
        float outX = r.In(() => get_turf(boomerang)?.x ?? 0);
        Assert.True(outX > 3, "the boomerang flew east: " + outX);
        // it lands, rebounds after a decisecond and flies back; the easy catch turned bob's throw mode on, so he catches it
        r.Step(6 * Second);
        object where = r.In(() => boomerang.loc);
        Assert.True(ReferenceEquals(where, bob) || r.In(() => get_turf(boomerang)?.x ?? 99) <= 4, "it came back: it is at " + r.In(() => get_turf(boomerang)?.x));
        r.AssertNoRuntimes();
    }
}
