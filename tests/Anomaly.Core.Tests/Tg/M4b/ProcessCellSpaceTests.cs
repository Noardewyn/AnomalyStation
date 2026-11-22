using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4b;

/// <summary>
/// DEVIATION DV84: a turf next to space that processes with no excited group (its neighbours, the space turf among them, were
/// processed earlier in the cycle, so the main loop of process_cell skips them, LINDA_turf_tile.dm:301) still shares its air with
/// the space turf in the share_end loop and finishes its step; tg's LAST_SHARE_CHECK would call reset_cooldowns() on null there.
/// </summary>
public class ProcessCellSpaceTests
{
    private const int Second = 20;

    [Fact]
    public void ATurfWithoutAGroupSharesWithTheSpaceTurfAlreadyProcessedThisCycleAndFinishesItsStep()
    {
        var r = new T2Room(4106);
        r.Step(Second);
        r.In(() => DevCommands.Breach(locate(1, 8, 1)));
        // the hole is space now and the air flows: step until SSair has processed the space turf with the floor still holding air
        // (SSair's first fire follows the seeded Master's offsets; silicons SI3: one more subsystem shifted them)
        for (int tick = 0; tick < 40; tick++)
        {
            r.Step();
            if (r.In(() => Dm.truthy(((turf_open)locate(1, 8, 1)).run_later) && Dm.num(((turf_open)locate(2, 8, 1)).air.return_pressure()) > 0))
                break;
        }
        r.In(() =>
        {
            var floor = (turf_open)locate(2, 8, 1);
            var space = Assert.IsType<turf_open_space>(locate(1, 8, 1));
            SSair.remove_from_active(floor); // kills the turf's excited group
            SSair.add_to_active(floor);      // active again, with no group
            Assert.Null(floor.excited_group);
            Assert.True(Dm.truthy(floor.excited));
            float fire = Dm.num(SSair.times_fired) + 1;
            // every neighbour (the space turf included) has been processed in this cycle already
            foreach (turf_open n in Dm.each<turf_open>(floor.atmos_adjacent_turfs))
                n.current_cycle = fire;
            float before = Dm.num(floor.air.return_pressure());
            Assert.True(Dm.truthy(space.run_later));

            Waiting.NoSleep(floor.process_cell(fire)); // threw a NullReferenceException before DV84

            Assert.Equal(fire, floor.current_cycle);
            Assert.True(Dm.num(floor.air.return_pressure()) < before, $"the turf shared its air with the space turf: {before} -> {floor.air.return_pressure()}, last_share {floor.air.last_share}, run_later {space.run_later}, adj {Dm.each<turf_open>(floor.atmos_adjacent_turfs).Count()}");
        });
        r.AssertNoRuntimes();
    }
}
