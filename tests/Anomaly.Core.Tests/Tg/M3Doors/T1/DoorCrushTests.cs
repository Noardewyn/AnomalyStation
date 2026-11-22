using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Doors.T1;

// The crushing of /obj/machinery/door/proc/crush (door.dm:666): an unsafe door closing on a living mob, and the window's
// wash (window.dm:384).

public class DoorCrushTests
{
    [Fact]
    public void AnUnsafeDoorClosingOnAHumanCrushesAndParalyzesThem()
    {
        // door.dm:645-652: `if(safe) CheckForMobs() else crush()`; poddoor.dm:13 safe = FALSE. door.dm:666-680 for a human:
        // the doorcrush signal, add_splatter_floor(loc), apply_damage(DOOR_CRUSH_DAMAGE, BRUTE, BODY_ZONE_CHEST) and Paralyze(10 SECONDS).
        var room = T1.Room();
        var door = T1.At<obj_machinery_door_poddoor_preopen>(room, 4, 4);
        var human = T1.Human(room, 4, 4);
        room.In(() =>
        {
            Assert.False(Dm.truthy(door.safe));
            Assert.False(Dm.truthy(human.IsParalyzed()));
            Assert.Equal(0f, Dm.num(human.get_brute_loss()));
            Waiting.Detach(door.close());
        });
        room.Step(60);
        room.In(() =>
        {
            Assert.True(T1.Dense(door));
            Assert.Equal(DOOR_CRUSH_DAMAGE, Dm.num(human.get_brute_loss()));
            Assert.True(Dm.truthy(human.IsParalyzed()));
            Assert.InRange(Dm.num(human.AmountParalyzed()), 50f, 100f); // 10 SECONDS, less the time that passed since
            Assert.NotEmpty(Dm.each_cast<atom>(locate(4, 4, 1).contents).OfType<obj_effect_decal_cleanable_blood>());
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void ASafeAirlockDoesNotCrushUntilItsSafetyWireIsCut()
    {
        // airlock.dm:1410: `dangerous_close = !safe || force_crush`; a safe airlock refuses to close on a dense mob, a cut
        // WIRE_SAFETY (wires/airlock.dm:104) clears safe and the airlock closes on them (airlock.dm:1625 crush()).
        var room = T1.Room();
        var door = T1.At<obj_machinery_door_airlock_public>(room, 4, 4);
        var human = T1.Human(room, 4, 4);
        room.In(() => Waiting.Detach(door.open()));
        room.Step(20);
        room.In(() =>
        {
            Assert.True(Dm.truthy(door.safe));
            Waiting.Detach(door.close());
        });
        room.Step(60);
        room.In(() =>
        {
            Assert.False(T1.Dense(door)); // the human blocks a safe door
            Assert.Equal(0f, Dm.num(human.get_brute_loss()));
            Waiting.NoSleep(door.wires.cut(WIRE_SAFETY));
            Assert.False(Dm.truthy(door.safe));
            Waiting.Detach(door.close());
        });
        room.Step(60);
        room.In(() =>
        {
            Assert.True(T1.Dense(door));
            Assert.Equal(DOOR_CRUSH_DAMAGE, Dm.num(human.get_brute_loss()));
            Assert.True(Dm.truthy(human.IsParalyzed()));
        });
        T1.NoRuntimes(room);
    }

    [Fact]
    public void AWindowWashedWithScrubCleansWhatIsOnItAndItsPaintedOpacity()
    {
        // window.dm:384: a CLEAN_SCRUB wash restores the window's initial opacity (spray paint) and washes the cleanables
        // inside it (vis_contents -= each one that washed); a weaker wash does nothing.
        var room = T1.Room();
        var window = T1.At<obj_structure_window>(room, 4, 4);
        var blood = room.In(() => Dm.@new<obj_effect_decal_cleanable_blood>(window));
        room.Step(2);
        room.In(() =>
        {
            Waiting.NoSleep(window.set_opacity(255));
            Dm.append(window.vis_contents, blood);
            Assert.Equal(255f, Dm.num(window.opacity));
            Assert.Same(window, blood.loc);

            // CLEAN_TYPE_RADIATION shares no bit with CLEAN_SCRUB (`!(clean_types & CLEAN_SCRUB)`): the window returns before
            // it reaches what is inside
            Waiting.NoSleep(window.wash(CLEAN_TYPE_RADIATION));
            Assert.Equal(255f, Dm.num(window.opacity));
            Assert.False(QDELETED(blood));

            object washed = Waiting.NoSleep(window.wash(CLEAN_SCRUB));
            Assert.True(Dm.truthy(Dm.bitand(washed, COMPONENT_CLEANED)));
            Assert.Equal(0f, Dm.num(window.opacity));
            Assert.True(QDELETED(blood));
            Assert.DoesNotContain(blood, Dm.each_cast<atom>(window.vis_contents).ToList());
        });
        T1.NoRuntimes(room);
    }
}
