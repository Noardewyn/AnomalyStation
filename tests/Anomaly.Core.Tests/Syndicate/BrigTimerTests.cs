using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Gaps.GapsHarness;

namespace Anomaly.Core.Tests.Syndicate;

/// <summary>
/// The brig cell timers (syndicate campaign, D316, from gaps W1) in a booted toy room: a cell's door timer links its brig windoor, flasher
/// and locker by id; a preset sets the time and shows it on the display; starting it shuts the door and locks the locker; when the time
/// runs out the door opens and the locker unlocks; the flash button flashes the cell and recharges; without security access the window
/// refuses; a mounted flasher frame carries its id.
/// </summary>
public class BrigTimerTests
{
    private static (AudioRoom R, Feed Feed, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(15, 9, seed: seed);
        Feed feed = Listen(r);
        mob_living_carbon_human bob = r.Join(3, 4, "Bob");
        r.Step(Second);
        return (r, feed, bob);
    }

    private sealed record Cell(obj_machinery_status_display_door_timer Timer, obj_machinery_door_window_brigdoor Door, obj_machinery_flasher Flasher, obj_structure_closet_secure_closet_brig Locker);

    /// <summary>An atom as the map loader makes it: the preloader's var edits, then Initialize with mapload (as P4bButtonTests).</summary>
    private static T Mapped<T>(AudioRoom r, int x, int y, DmList edits) where T : atom => r.In(() =>
    {
        float previous = SSatoms.initialized;
        SSatoms.initialized = INITIALIZATION_INNEW_MAPLOAD;
        world.preloader_setup(edits, typeof(T));
        T made = Dm.@new<T>(locate(x, y, 1));
        SSatoms.initialized = previous;
        return made;
    });

    private static Cell Build(AudioRoom r)
    {
        var door = Mapped<obj_machinery_door_window_brigdoor>(r, 8, 4, Dm.list(Dm.kv("id", "Cell 1")));
        var flasher = Mapped<obj_machinery_flasher>(r, 10, 5, Dm.list(Dm.kv("id", "Cell 1")));
        var locker = Mapped<obj_structure_closet_secure_closet_brig>(r, 10, 4, Dm.list(Dm.kv("id", "Cell 1")));
        var timer = Mapped<obj_machinery_status_display_door_timer>(r, 7, 5, Dm.list(Dm.kv("id", "Cell 1"), Dm.kv("name", "Cell 1")));
        r.Step(2);
        return new Cell(timer, door, flasher, locker);
    }

    [Fact]
    public void TheBrigTimersAreLive()
    {
        foreach (Type type in new[] { typeof(obj_machinery_status_display_door_timer), typeof(obj_machinery_flasher), typeof(obj_machinery_flasher_portable), typeof(obj_item_wallframe_flasher) })
            Assert.False(Attribute.IsDefined(type, typeof(DmInertAttribute), inherit: false), type.Name + " is inert");
    }

    [Fact]
    public void ATimedSentenceShutsTheCellAndOpensItWhenTheTimeIsUp()
    {
        var (r, _, bob) = Room(2101);
        var cell = Build(r);
        Assert.Equal(1, r.In(() => Dm.length(cell.Timer.doors)));
        Assert.Equal(1, r.In(() => Dm.length(cell.Timer.flashers)));
        Assert.Equal(1, r.In(() => Dm.length(cell.Timer.closets)));
        // the cell's windoor stands open for the prisoner to walk in
        r.In(() => Waiting.Detach(cell.Door.open()));
        r.Step(2 * Second);
        Assert.False(r.In(() => Dm.truthy(cell.Door.density)), "the windoor is open");

        // a short preset: two minutes on the display
        r.In(() => cell.Timer.set_timer(2 * MINUTES));
        Assert.Equal("02:00", r.In(() => cell.Timer.message2));
        r.In(() => Waiting.NoSleep(cell.Timer.timer_start()));
        r.Step(3 * Second);
        Assert.True(r.In(() => Dm.truthy(cell.Door.density)), "the door shuts");
        Assert.True(r.In(() => Dm.truthy(cell.Locker.locked)), "the locker locks");
        Assert.StartsWith("01:5", r.In(() => cell.Timer.message2));

        // the sentence runs out: the door opens and the locker unlocks
        r.Step(2 * 60 * Second);
        Assert.False(r.In(() => Dm.truthy(cell.Timer.timing)));
        Assert.False(r.In(() => Dm.truthy(cell.Door.density)), "the door opens");
        Assert.False(r.In(() => Dm.truthy(cell.Locker.locked)), "the locker unlocks");
        Assert.Equal("", r.In(() => cell.Timer.message2));
    }

    [Fact]
    public void TheFlashButtonFlashesTheCellAndRecharges()
    {
        var (r, _, bob) = Room(2102);
        var cell = Build(r);
        mob_living_carbon_human prisoner = r.Dummy(10, 6);
        r.In(() => Waiting.NoSleep(cell.Flasher.flash()));
        r.Step(2);
        Assert.True(r.In(() => Dm.truthy(prisoner.IsParalyzed())), "the prisoner is flashed down");
        var data = r.In(() => Waiting.NoSleep(cell.Timer.ui_data(bob)) as DmList);
        Assert.True(r.In(() => Dm.truthy(data["flash_charging"])), "the flash recharges");
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/items/weapons/flash.ogg");
    }

    [Fact]
    public void TheMountedFlasherFrameCarriesItsCellId()
    {
        var (r, feed, bob) = Room(2103);
        var flasher = Mapped<obj_machinery_flasher>(r, 6, 5, Dm.list(Dm.kv("id", "Cell 2")));
        r.Step(2);
        r.In(() => Waiting.NoSleep(flasher.bulb.forceMove(flasher.loc)));
        r.In(() => Waiting.NoSleep(flasher.on_deconstruction(TRUE)));
        var frame = r.In(() => Dm.each<obj_item_wallframe_flasher>(locate(6, 5, 1).contents).Single());
        Assert.Equal("Cell 2", r.In(() => Dm.str(frame.id)));
    }
}
