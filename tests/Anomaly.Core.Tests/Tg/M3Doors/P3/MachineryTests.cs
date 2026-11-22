using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Doors.P3;

// M3 P3's smoke tests of the machinery base (_machinery.dm, power.dm's machine procs) and SSmachines (machines.dm).
// On a booted world (the real Master) a machine registers and processes exactly as in tg; in a test harness world without
// SSmachines or the Master it skips both (rules 10.6, the lead's decision).

public class MachineryTests
{
    [Fact]
    public void OnABootedWorldAMachineRegistersProcessesAndUnregistersAsInTg()
    {
        var room = TgToyRoom.Boot();
        using (room.World.Enter())
        {
            Assert.NotNull(SSmachines); // Master/New created it (a hand-written subsystem, DV25)
            Assert.Contains(SSmachines, Dm.each(Master.subsystems));

            var machine = Dm.@new<obj_machinery>(ToyRoom.FloorBottomLeft());
            Assert.Empty(room.RuntimeText);
            // register_machine (machines.dm:38)
            Assert.True(Dm.@in(machine, (DmList)SSmachines.get_machines_by_type(typeof(obj_machinery))));
            Assert.True(Dm.@in(machine, (DmList)SSmachines.get_machines_by_type_and_subtypes(typeof(obj_machinery))));
            Assert.True(Dm.@in(machine, (DmList)SSmachines.get_all_machines()));
            // begin_processing on SSmachines, the default subsystem_type (_machinery.dm:296)
            Assert.True(Dm.@in(machine, SSmachines.processing));
            Assert.True(Dm.truthy(Dm.bitand(machine.datum_flags, DF_ISPROCESSING)));
            // post_machine_initialize (LateInitialize at once outside a map load): powered, idle draw on the static channel
            Assert.False(Dm.truthy(Dm.bitand(machine.machine_stat, NOPOWER)));
            Assert.True(Dm.eq(machine.static_power_usage, machine.idle_power_usage));
            area home = get_area(machine) as area;
            Assert.True(Dm.eq(home.energy_usage[AREA_USAGE_STATIC_EQUIP], machine.idle_power_usage * SSmachines.wait / (1 * SECONDS))); // power_to_energy
            Assert.True(HAS_TRAIT_FROM(machine, TRAIT_AREA_SENSITIVE, INNATE_TRAIT));

            // the base process() returns PROCESS_KILL (_machinery.dm:310): one fire takes it off the list (machines.dm:149-151)
            room.World.Step((int)(SSmachines.wait / world.tick_lag) + 2); // a full wait of SSmachines (2 s)
            Assert.False(Dm.@in(machine, SSmachines.processing));
            Assert.False(Dm.truthy(Dm.bitand(machine.datum_flags, DF_ISPROCESSING)));

            qdel(machine);
            Assert.False(Dm.@in(machine, (DmList)SSmachines.get_all_machines()));
            Assert.True(Dm.eq(((DmList)SSmachines.get_machines_by_type(typeof(obj_machinery))).len, 0));
            Assert.True(Dm.eq(home.energy_usage[AREA_USAGE_STATIC_EQUIP], 0)); // unset_static_power
            Assert.Empty(room.RuntimeText);
        }
    }

    [Fact]
    public void InAHarnessWorldWithoutTheMasterAMachineSkipsRegistrationAndProcessing()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            Assert.Null(SSmachines);
            Assert.Null(Master);
            var machine = Dm.@new<obj_machinery>(ToyRoom.FloorBottomLeft());
            Assert.Empty(room.RuntimeText);
            Assert.False(Dm.truthy(Dm.bitand(machine.datum_flags, DF_ISPROCESSING)));
            Assert.True(Dm.truthy(Dm.bitand(machine.flags_1, INITIALIZED_1)));
            Assert.True(Dm.eq(machine.static_power_usage, machine.idle_power_usage)); // the power bookkeeping still runs
            qdel(machine);
            Assert.True(QDELETED(machine));
            Assert.Empty(room.RuntimeText);
        }
    }

    [Fact]
    public void AnAreaPowerChangeReachesTheMachineThroughItsSignal()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            var machine = Dm.@new<obj_machinery>(ToyRoom.FloorBottomLeft());
            area home = get_area(machine) as area;
            home.requires_power = TRUE; // the test room needs none (areas/misc.dm:38)
            home.power_equip = FALSE;
            home.power_change(); // COMSIG_AREA_POWER_CHANGE -> /obj/machinery/power_change (power.dm:273)
            Assert.True(Dm.truthy(Dm.bitand(machine.machine_stat, NOPOWER)));
            Assert.True(Dm.eq(machine.is_operational, FALSE)); // on_set_machine_stat, from on to off
            Assert.True(Dm.eq(machine.appearance_power_state, NOPOWER)); // update_appearance's hook (power.dm:296)

            home.power_equip = TRUE;
            home.power_change();
            Assert.False(Dm.truthy(Dm.bitand(machine.machine_stat, NOPOWER)));
            Assert.True(Dm.eq(machine.is_operational, TRUE));
            Assert.Empty(room.RuntimeText);
        }
    }

    [Fact]
    public void UpdateUsePowerMovesTheStaticDraw()
    {
        var room = TgToyRoom.Create();
        using (room.World.Enter())
        {
            var machine = Dm.@new<obj_machinery>(ToyRoom.FloorBottomLeft());
            Assert.True(Dm.eq(machine.update_use_power(ACTIVE_POWER_USE), TRUE));
            Assert.True(Dm.eq(machine.static_power_usage, machine.active_power_usage));
            Assert.True(Dm.eq(machine.update_use_power(ACTIVE_POWER_USE), FALSE)); // unchanged (_machinery.dm:472)
            Assert.True(Dm.eq(machine.update_use_power(NO_POWER_USE), TRUE));
            Assert.True(Dm.eq(machine.static_power_usage, 0));
            Assert.False(HAS_TRAIT_FROM(machine, TRAIT_AREA_SENSITIVE, INNATE_TRAIT)); // remove_area_power_relationship
            Assert.Empty(room.RuntimeText);
        }
    }
}
