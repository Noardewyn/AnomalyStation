using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using T1 = Anomaly.Core.Tests.Tg.M3Doors.T1.T1;

namespace Anomaly.Core.Tests.M7;

/// <summary>A machine that records what its EMP draws (machinery/emp_act calls use_energy, _machinery.dm:353).</summary>
[DmPath("/obj/machinery/t7_emp_probe")]
public class obj_machinery_t7_emp_probe : obj_machinery
{
    public float Drawn;
    public int Calls;

    public obj_machinery_t7_emp_probe() { use_power = IDLE_POWER_USE; }

    public override async DmTask<object> use_energy(object amount = null, object channel = null, float? ignore_apc = null, float? force = null)
    {
        Drawn += Dm.num(amount);
        Calls++;
        return amount;
    }
}

/// <summary>Listens to the signals of atom/emp_act and to the pulses of a wire set.</summary>
[DmPath("/datum/t7_emp_probe")]
public class datum_t7_emp_probe : datum
{
    public readonly List<(object Severity, object Protection)> Acts = new();
    public int WirePulses;

    [SignalHandler]
    public virtual void on_emp(datum source = null, object severity = null, object protection = null) => Acts.Add((severity, protection));

    [SignalHandler]
    public virtual void on_wire_pulse(datum source = null, object wire = null, object user = null) => WirePulses++;
}

/// <summary>
/// M7 EMP slice (D162): tg's electromagnetic pulse. <c>empulse</c> (game/objects/empulse.dm), the <c>emp_act</c> chain
/// (game/atom/atom_act.dm:69 and the overrides the port runs), EMP_PROTECT_* and the empprotection element
/// (datums/elements/empprotection.dm). A power cell stands for what a pulse reaches: <c>power_store/emp_act</c> uses
/// <c>(STANDARD_CELL_CHARGE / severity) * emp_damage_modifier</c> of it (power_store.dm:240), so its drain tells the severity.
/// </summary>
public class EmpTests
{
    private const float Heavy = STANDARD_CELL_CHARGE;       // severity 1
    private const float Light = STANDARD_CELL_CHARGE / 2f;  // severity 2

    /// <summary>A plain power cell (cell.dm:19 emp_damage_modifier 1) with room for several pulses (tg's own holds one STANDARD_CELL_CHARGE).</summary>
    private static obj_item_stock_parts_power_store_cell Cell(TgToyRoom room, int x, int y)
        => room.In(() =>
        {
            var cell = Dm.@new<obj_item_stock_parts_power_store_cell>(locate(x, y, 1));
            cell.maxcharge = 10 * STANDARD_CELL_CHARGE;
            cell.charge = cell.maxcharge;
            return cell;
        });

    private static float Drained(TgToyRoom room, obj_item_stock_parts_power_store cell) => room.In(() => cell.maxcharge - cell.charge);

    private static void Pulse(TgToyRoom room, int x, int y, float heavy, float light)
        => room.In(() => Waiting.NoSleep(empulse(locate(x, y, 1), heavy, light)));

    [Fact]
    public void AnEmpulseReachesTheHeavyAndLightRangesAsTgComputesThem()
    {
        var room = T1.Room(width: 15, height: 15);
        // empulse.dm:24-35: distance (get_dist) under heavy_range is heavy, over it and within light_range light, a coin toss on the edge
        var onLine = new List<obj_item_stock_parts_power_store_cell>();
        for (int d = 0; d <= 6; d++)
            onLine.Add(Cell(room, 8 + d, 8));
        // the ring at distance heavy_range (2): 16 turfs, each its own coin
        var ring = new List<obj_item_stock_parts_power_store_cell>();
        for (int dx = -2; dx <= 2; dx++)
            for (int dy = -2; dy <= 2; dy++)
                if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == 2 && dy != 0)
                    ring.Add(Cell(room, 8 + dx, 8 + dy));
        Assert.Equal(14, ring.Count);

        Assert.Equal(1f, Dm.num(room.In(() => Waiting.NoSleep(empulse(locate(8, 8, 1), 2, 4))))); // return 1

        Assert.Equal(Heavy, Drained(room, onLine[0]));  // distance 0
        Assert.Equal(Heavy, Drained(room, onLine[1]));  // 1 < heavy_range
        float edge = Drained(room, onLine[2]);           // 2 == heavy_range: prob(50) heavy else light
        Assert.Contains(edge, new[] { Heavy, Light });
        Assert.Equal(Light, Drained(room, onLine[3]));   // 3 and 4 are within light_range
        Assert.Equal(Light, Drained(room, onLine[4]));
        Assert.Equal(0f, Drained(room, onLine[5]));      // beyond light_range
        Assert.Equal(0f, Drained(room, onLine[6]));
        var edgeDrains = ring.Select(c => Drained(room, c)).ToList();
        Assert.All(edgeDrains, e => Assert.Contains(e, new[] { Heavy, Light }));
        Assert.Contains(Heavy, edgeDrains); // both outcomes of the edge's coin occur among the 14
        Assert.Contains(Light, edgeDrains);
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ALightRangeUnderTheHeavyOneIsRaisedToIt_AndAnEmpulseOfNothingDoesNothing()
    {
        var room = T1.Room(width: 15, height: 15);
        var near = Cell(room, 10, 8);
        var far = Cell(room, 12, 8); // 4 away
        // empulse.dm:16: if(heavy_range > light_range) light_range = heavy_range
        Pulse(room, 8, 8, 3, 0);
        Assert.Equal(Heavy, Drained(room, near));   // 2 < 3
        Assert.Equal(0f, Drained(room, far));       // 4 > 3
        // a null epicenter (empulse.dm:2) and an atom as the epicenter: its turf
        Assert.Null(room.In(() => Waiting.NoSleep(empulse(null, 5, 5))));
        var holder = Cell(room, 8, 8);
        room.In(() => Waiting.NoSleep(empulse(holder, 3, 3)));
        Assert.Equal(Heavy, Drained(room, holder));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AnEmpulseMakesTgsPulseEffectAtTheEpicenterOnlyWhenTheHeavyRangeExceedsOne()
    {
        var room = T1.Room(width: 15, height: 15);
        int Pulses() => room.In(() => Dm.each<obj_effect_temp_visual_emp_pulse>(locate(8, 8, 1)).Count());
        Pulse(room, 8, 8, 1, 3); // empulse.dm:12: if(heavy_range > 1)
        Assert.Equal(0, Pulses());
        Pulse(room, 8, 8, 2, 3);
        Assert.Equal(1, Pulses());
        var pulse = room.In(() => Dm.each<obj_effect_temp_visual_emp_pulse>(locate(8, 8, 1)).Single());
        Assert.Equal("emppulse", room.In(() => pulse.icon_state as string));
        Assert.Equal(8f, room.In(() => Dm.num(pulse.duration)));
        // temp_visual: QDEL_IN_STOPPABLE(src, duration), 8 deciseconds
        room.Step(8 * T1.TicksPerDs + 2);
        Assert.Equal(0, Pulses());
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ACellIsDrainedAsTgDrainsIt_AndAnEmpProofCellIsNot()
    {
        var room = T1.Room(width: 15, height: 15);
        var standard = Cell(room, 8, 8);
        var proof = room.In(() => Dm.@new<obj_item_stock_parts_power_store_cell_emproof>(locate(8, 9, 1)));
        // power_store.dm:240: use((STANDARD_CELL_CHARGE / severity) * emp_damage_modifier, force = TRUE)
        room.In(() => Waiting.NoSleep(standard.emp_act(EMP_HEAVY)));
        Assert.Equal(Heavy, Drained(room, standard));
        room.In(() => Waiting.NoSleep(standard.emp_act(EMP_LIGHT)));
        Assert.Equal(Heavy + Light, Drained(room, standard));
        // a modifier of 4 (the maintenance cell, maintenance_loot.dm:44) and 0 (cell.dm:219 emproof)
        room.In(() => standard.emp_damage_modifier = 4);
        room.In(() => standard.charge = standard.maxcharge);
        room.In(() => Waiting.NoSleep(standard.emp_act(EMP_LIGHT)));
        Assert.Equal(4 * Light, Drained(room, standard));
        // the drain is forced: a cell with less charge than the drain is emptied
        room.In(() => standard.charge = 100);
        room.In(() => Waiting.NoSleep(standard.emp_act(EMP_HEAVY)));
        Assert.Equal(0f, room.In(() => standard.charge));
        // the EMP-proof cell: cell.dm:214 AddElement(/datum/element/empprotection, EMP_PROTECT_SELF); its modifier is 0 as well
        Assert.Equal(EMP_PROTECT_SELF, Dm.num(room.In(() => Waiting.NoSleep(proof.emp_act(EMP_HEAVY)))));
        Assert.Equal(0f, Drained(room, proof));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void EmpProtectFlagsAreCollectedByTheElementAndBlockWhatTheyProtect()
    {
        var room = T1.Room(width: 15, height: 15);
        var self = Cell(room, 8, 8);
        room.In(() => Waiting.NoSleep(self.AddElement(typeof(datum_element_empprotection), EMP_PROTECT_SELF)));
        // atom/emp_act returns the protection (atom_act.dm:84)
        Assert.Equal(EMP_PROTECT_SELF, Dm.num(room.In(() => Waiting.NoSleep(self.emp_act(EMP_HEAVY)))));
        Assert.Equal(0f, Drained(room, self));

        // the element's flags are masked with EMP_PROTECT_ALL (empprotection.dm:20): the examine flag is not protection
        var examine = Cell(room, 9, 8);
        room.In(() => Waiting.NoSleep(examine.AddElement(typeof(datum_element_empprotection), EMP_PROTECT_SELF | EMP_NO_EXAMINE)));
        Assert.Equal(EMP_PROTECT_SELF, Dm.num(room.In(() => Waiting.NoSleep(examine.emp_act(EMP_LIGHT)))));

        // EMP_PROTECT_CONTENTS: a suit keeps its cell out of it (_spacesuits.dm:375), while an unprotected suit zaps it
        var open = room.In(() => Dm.@new<obj_item_clothing_suit_space>(locate(8, 9, 1)));
        var shielded = room.In(() => Dm.@new<obj_item_clothing_suit_space>(locate(9, 9, 1)));
        room.In(() => Waiting.NoSleep(shielded.AddElement(typeof(datum_element_empprotection), EMP_PROTECT_CONTENTS)));
        room.In(() => Waiting.NoSleep(open.emp_act(EMP_HEAVY)));
        room.In(() => Waiting.NoSleep(shielded.emp_act(EMP_HEAVY)));
        Assert.Equal(open.cell_instance.maxcharge - 3 * Heavy, room.In(() => open.cell_instance.charge)); // the suit's high-capacity cell: emp_damage_modifier 3 (cell.dm:117)
        Assert.Equal(shielded.cell_instance.maxcharge, room.In(() => shielded.cell_instance.charge));

        // an EMP-proof pulse: empulse reaches it, the flags hold
        Pulse(room, 8, 8, 2, 2);
        Assert.Equal(0f, Drained(room, self));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AtomEmpActSendsTheSignalTgSendsTwice()
    {
        // COMSIG_ATOM_PRE_EMP_ACT and COMSIG_ATOM_EMP_ACT are the same string in tg (signals_atom_x_act.dm:12,14): a listener hears
        // (severity) from the first send and (severity, protection) from the second
        var room = T1.Room(width: 15, height: 15);
        var cell = Cell(room, 8, 8);
        var probe = room.In(() => Dm.@new<datum_t7_emp_probe>());
        room.In(() => probe.RegisterSignal(cell, COMSIG_ATOM_EMP_ACT, nameof(datum_t7_emp_probe.on_emp)));
        room.In(() => Waiting.NoSleep(cell.emp_act(EMP_LIGHT)));
        Assert.Equal(2, probe.Acts.Count);
        Assert.Equal(EMP_LIGHT, Dm.num(probe.Acts[0].Severity));
        Assert.Null(probe.Acts[0].Protection);
        Assert.Equal(EMP_LIGHT, Dm.num(probe.Acts[1].Severity));
        Assert.Equal(0f, Dm.num(probe.Acts[1].Protection));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AMachineDrawsPowerAndShowsSparksAsTgsMachineryDoes()
    {
        var room = T1.Room(width: 15, height: 15);
        var machine = room.In(() => Dm.@new<obj_machinery_t7_emp_probe>(locate(8, 8, 1)));
        int Sparks() => room.In(() => Dm.each<obj_effect_temp_visual_emp>(locate(8, 8, 1)).Count(e => e is not obj_effect_temp_visual_emp_pulse));
        // _machinery.dm:353: use_energy(7.5 KILO JOULES / severity) and new /obj/effect/temp_visual/emp(loc)
        room.In(() => Waiting.NoSleep(machine.emp_act(EMP_HEAVY)));
        Assert.Equal(7500f, machine.Drawn);
        Assert.Equal(1, Sparks());
        room.In(() => Waiting.NoSleep(machine.emp_act(EMP_LIGHT)));
        Assert.Equal(7500f + 3750f, machine.Drawn);
        Assert.Equal(2, Sparks());
        // no power use, a machine_stat (broken, unpowered) or EMP_PROTECT_SELF: nothing (:351)
        room.In(() => machine.use_power = NO_POWER_USE);
        room.In(() => Waiting.NoSleep(machine.emp_act(EMP_HEAVY)));
        room.In(() => machine.use_power = IDLE_POWER_USE);
        room.In(() => machine.machine_stat = BROKEN);
        room.In(() => Waiting.NoSleep(machine.emp_act(EMP_HEAVY)));
        room.In(() => machine.machine_stat = 0);
        room.In(() => Waiting.NoSleep(machine.AddElement(typeof(datum_element_empprotection), EMP_PROTECT_SELF)));
        room.In(() => Waiting.NoSleep(machine.emp_act(EMP_HEAVY)));
        Assert.Equal(2, machine.Calls);
        Assert.Equal(2, Sparks());
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ALightFixtureReactsToAPulseLikeAnyMachine()
    {
        var room = T1.Room(width: 15, height: 15);
        var lamp = room.In(() => Dm.@new<obj_machinery_light_directional_south>(locate(8, 8, 1)));
        room.In(() => lamp.allow_break_on_init = FALSE);
        int Sparks() => room.In(() => Dm.each<obj_effect_temp_visual_emp>(locate(8, 8, 1)).Count(e => e is not obj_effect_temp_visual_emp_pulse));
        Assert.Equal(0, Sparks());
        Pulse(room, 8, 8, 2, 3);
        // tg's light fixture has no emp_act of its own: /obj/machinery's makes the sparks at it and draws the power
        Assert.Equal(1, Sparks());
        Assert.True(room.In(() => Dm.truthy(lamp.on)), "the fixture stays lit");
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AStoragePulsesItsContents_AndTgsDoubleSignalPulsesThemTwice()
    {
        var room = T1.Room(width: 15, height: 15);
        var bag = room.In(() => Dm.@new<obj_item_storage_backpack>(locate(8, 8, 1)));
        var cell = Cell(room, 9, 9);
        Assert.True(Dm.truthy(room.In(() => bag.atom_storage.attempt_insert(cell, null))));
        Assert.Same(bag, room.In(() => cell.loc));
        // storage.dm:738 on_emp_act is registered on COMSIG_ATOM_EMP_ACT, the string of the PRE signal too (storage.dm:252): the
        // first send of atom/emp_act (protection not passed) and the second (protection 0) each pulse the contents
        room.In(() => Waiting.NoSleep(bag.emp_act(EMP_HEAVY)));
        Assert.Equal(2 * Heavy, Drained(room, cell));
        // a pulse of the room reaches the bag on the floor, and through it the cell inside
        room.In(() => cell.charge = cell.maxcharge);
        Pulse(room, 8, 8, 2, 2);
        Assert.Equal(2 * Heavy, Drained(room, cell));
        // with the bag's contents protected the second send is skipped (storage.dm:741), the first still pulses
        room.In(() => cell.charge = cell.maxcharge);
        room.In(() => Waiting.NoSleep(bag.AddElement(typeof(datum_element_empprotection), EMP_PROTECT_CONTENTS)));
        room.In(() => Waiting.NoSleep(bag.emp_act(EMP_HEAVY)));
        Assert.Equal(Heavy, Drained(room, cell));
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ALivingMobPulsesItsContentsWithTheSeverityRaisedByItsEmpProtection()
    {
        var room = T1.Room(width: 15, height: 15);
        var human = T1.Human(room, 8, 8);
        var cell = Cell(room, 9, 9);
        Assert.True(Dm.truthy(room.In(() => Waiting.NoSleep(human.put_in_active_hand(cell)))));
        // living_defense.dm:643: contents get emp_act(severity + get_emp_protection())
        room.In(() => Waiting.NoSleep(human.emp_act(EMP_HEAVY)));
        Assert.Equal(Heavy, Drained(room, cell));
        room.In(() => cell.charge = cell.maxcharge);
        room.In(() => human.emp_protection = EMP_PROTECTION_MODERATE);
        room.In(() => Waiting.NoSleep(human.emp_act(EMP_HEAVY)));
        Assert.Equal(Light, Drained(room, cell)); // severity 1 + 1
        // a human adds the emp_protection of every worn piece of clothing (human_defense.dm:770)
        room.In(() => cell.charge = cell.maxcharge);
        room.In(() => human.emp_protection = EMP_PROTECTION_NONE);
        var suit = room.In(() => Dm.@new<obj_item_clothing_suit_space>(locate(8, 8, 1)));
        room.In(() => suit.emp_protection = EMP_PROTECTION_HIGH);
        Assert.True(Dm.truthy(room.In(() => Waiting.NoSleep(human.equip_to_slot_if_possible(suit, ITEM_SLOT_OCLOTHING)))));
        Assert.Equal(EMP_PROTECTION_HIGH, Dm.num(room.In(() => human.get_emp_protection())));
        room.In(() => Waiting.NoSleep(human.emp_act(EMP_HEAVY)));
        Assert.Equal(STANDARD_CELL_CHARGE / 3f, Drained(room, cell), 0.1f); // severity 1 + 2
        // and the worn suit's own cell, one of the mob's contents
        Assert.Equal(suit.cell_instance.maxcharge - 3 * STANDARD_CELL_CHARGE / 3f, room.In(() => suit.cell_instance.charge), 0.1f); // severity 3, modifier 3
        // a pulse of the room reaches the mob on its turf
        room.In(() => cell.charge = cell.maxcharge);
        Pulse(room, 8, 8, 2, 2);
        Assert.True(Drained(room, cell) > 0f);
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AJetpackIsSwitchedOffAndDisabledForFourSeconds()
    {
        var room = T1.Room(width: 15, height: 15);
        var jetpack = room.In(() => Dm.@new<obj_item_tank_jetpack_carbondioxide>(locate(8, 8, 1)));
        room.In(() => jetpack.on = TRUE);
        room.In(() => Waiting.NoSleep(jetpack.emp_act(EMP_LIGHT)));
        room.In(() =>
        {
            Assert.False(Dm.truthy(jetpack.on));
            Assert.True(Dm.truthy(jetpack.disabled));
        });
        room.Step(4 * 10 * T1.TicksPerDs - 2);
        Assert.True(room.In(() => Dm.truthy(jetpack.disabled)), "still disabled just before the four seconds are up");
        room.Step(4);
        Assert.False(room.In(() => Dm.truthy(jetpack.disabled))); // remove_emp, addtimer 4 SECONDS (jetpack.dm:170)
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ASpaceSuitZapsItsCellAndAnAirlockMayOpen()
    {
        var room = T1.Room(width: 15, height: 15);
        var suit = room.In(() => Dm.@new<obj_item_clothing_suit_space>(locate(8, 8, 1)));
        room.In(() => Waiting.NoSleep(suit.emp_act(EMP_LIGHT)));
        Assert.Equal(suit.cell_instance.maxcharge - 3 * Light, room.In(() => suit.cell_instance.charge)); // emp_damage_modifier 3 (cell.dm:117)

        // door.dm:515: prob(20 / severity) opens an airlock (heavy: 20 %), through INVOKE_ASYNC(open)
        int opened = 0;
        for (int i = 0; i < 40; i++)
        {
            var door = room.In(() => Dm.@new<obj_machinery_door_airlock>(locate(4, 4, 1)));
            room.In(() => Waiting.NoSleep(door.emp_act(EMP_HEAVY)));
            room.Step(30);
            if (!room.In(() => Dm.truthy(door.density))) opened++;
            room.In(() => qdel(door));
        }
        Assert.InRange(opened, 1, 20);
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AnEmpPulsesAtMostThreeWiresOfAnAirlock_AndAProtectedAirlockKeepsItsWires()
    {
        var room = T1.Room(width: 15, height: 15);
        var door = room.In(() => Dm.@new<obj_machinery_door_airlock>(locate(4, 4, 1)));
        var protectedDoor = room.In(() => Dm.@new<obj_machinery_door_airlock>(locate(6, 6, 1)));
        room.In(() => Waiting.NoSleep(protectedDoor.AddElement(typeof(datum_element_empprotection), EMP_PROTECT_WIRES)));
        var probe = room.In(() => Dm.@new<datum_t7_emp_probe>());
        room.In(() => probe.RegisterSignal(door.wires, COMSIG_PULSE_WIRE, nameof(datum_t7_emp_probe.on_wire_pulse)));
        var probe2 = room.In(() => Dm.@new<datum_t7_emp_probe>());
        room.In(() => probe2.RegisterSignal(protectedDoor.wires, COMSIG_PULSE_WIRE, nameof(datum_t7_emp_probe.on_wire_pulse)));
        // wires.dm:230: each wire pulses with prob(33), the pulses stop at MAXIMUM_EMP_WIRES (3)
        int most = 0, total = 0;
        for (int i = 0; i < 30; i++)
        {
            int before = probe.WirePulses;
            room.In(() => Waiting.NoSleep(door.wires.emp_pulse()));
            int pulsed = probe.WirePulses - before;
            Assert.InRange(pulsed, 0, 3);
            most = Math.Max(most, pulsed);
            total += pulsed;
            room.In(() => Waiting.NoSleep(protectedDoor.emp_act(EMP_LIGHT)));
        }
        Assert.True(total > 0 && most >= 1, "some wire pulsed over 30 pulses");
        Assert.Equal(0, probe2.WirePulses);
        // atom/emp_act pulses the wires of an unprotected atom (atom_act.dm:72)
        int viaAct = probe.WirePulses;
        for (int i = 0; i < 30; i++)
            room.In(() => Waiting.NoSleep(door.emp_act(EMP_LIGHT)));
        Assert.True(probe.WirePulses > viaAct);
        T1.NoRuntimes(room);
    }

    [Fact]
    public void TheSingularitysEmpAreaIsAnEmpulseOfEightAndTen()
    {
        var room = T1.Room(width: 30, height: 30);
        var near = Cell(room, 21, 15); // 6 away: heavy
        var middle = Cell(room, 25, 15); // 10 away would be light; 9 here
        var sing = room.In(() => Dm.@new<obj_singularity>(locate(15, 15, 1), 600f));
        room.In(() => Waiting.NoSleep(sing.emp_area()));
        Assert.Equal(Heavy, Drained(room, near));
        Assert.Equal(Light, Drained(room, middle));
        Assert.True(room.In(() => Dm.each<obj_effect_temp_visual_emp_pulse>(locate(15, 15, 1)).Any()), "heavy_range 8 > 1: the pulse effect");
    }
}
