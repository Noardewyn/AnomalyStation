using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Doors.T1;

// T1: the tests that failed before a "Fix translation:" commit of P3's files.

public class FixTranslationTests
{
    [Fact]
    public void AnUnsafeDoorClosingOnAnEmptyDoorwayHasNothingToCrush()
    {
        // door.dm:645-652: `if(safe) CheckForMobs() else crush()`; poddoor.dm:13 safe = FALSE. crush() (door.dm:666) loops over
        // the door's turfs for living mobs and mechas; with none in them it does nothing. The port had the whole proc as
        // Dm.NotTranslated, which threw on every pod door and shutter that closed.
        var room = T1.Room();
        var blast = T1.At<obj_machinery_door_poddoor_preopen>(room, 4, 4);
        var shutters = T1.At<obj_machinery_door_poddoor_shutters_preopen>(room, 5, 4);
        room.In(() =>
        {
            Assert.False(Dm.truthy(blast.safe));
            Assert.False(Dm.truthy(shutters.safe));
            Waiting.Detach(blast.close());
            Waiting.Detach(shutters.close());
        });
        room.Step(40);
        room.In(() =>
        {
            Assert.True(T1.Dense(blast));
            Assert.True(T1.Dense(shutters));
            Assert.False(Dm.truthy(blast.operating));
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AMachineMadeWithASecondArgumentThatIsNoCircuitBoardPassesItToInitialize()
    {
        // _machinery.dm:166 `/obj/machinery/New(location, obj/item/circuitboard/board, ...)`: only a real board is dropped; anything
        // else goes on to /atom/New by the zero-argument `..()` and reaches Initialize, e.g. a windoor's `set_dir`
        // (windowdoor.dm:50 Initialize(mapload, set_dir, unres_sides)). The port typed `board` as a circuit board, so the
        // direction became null.
        var room = T1.Room();
        var windoor = room.In(() => Dm.@new<obj_machinery_door_window>(locate(4, 4, 1), NORTH, Dm.bitor(NORTH, SOUTH)));
        room.In(() =>
        {
            Assert.True(Dm.eq(windoor.dir, NORTH));
            Assert.True(Dm.eq(windoor.unres_sides, Dm.bitor(NORTH, SOUTH))); // dir NORTH drops EAST and WEST only
        });
        T1.NoRuntimes(room);
    }
}
