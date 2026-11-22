using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M4aTools.P4;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Playtest;

/// <summary>
/// The owner's M5 play test (2026-10-16): cable restraints made from a coil (cable.dm:629, 15 cable, new(null) then put_in_hands)
/// were lost with the cable spent. The restraints were inert then (M7x slice E translated them; a fishing rod, whose dropped is untranslated, stands in now): the
/// interaction guard refuses an inert item a hand, and it ends where tg's put_in_hands leaves an item no hand takes, at the user's
/// feet; one already somewhere stays there.
/// </summary>
public class InertResultTests
{
    [Fact]
    public void AnInertItemRefusedAHandLandsAtTheFeetInsteadOfNullspace()
    {
        var (room, a) = P4aToolTests.Room(9801);
        room.In(() =>
        {
            var cuffs = Dm.@new<obj_item_fishing_rod>(null);
            Assert.False(Dm.truthy(Waiting.NoSleep(a.put_in_hands(cuffs))));
            Assert.Same(get_turf(a), cuffs.loc);
            var other = Dm.@new<obj_item_fishing_rod>(locate(7, 7, 1));
            Assert.False(Dm.truthy(Waiting.NoSleep(a.put_in_hands(other))));
            Assert.Same(locate(7, 7, 1), other.loc);
            var gone = Dm.@new<obj_item_fishing_rod>(null);
            Assert.False(Dm.truthy(Waiting.NoSleep(a.put_in_hands(gone, del_on_fail: 1))));
            Assert.True(QDELETED(gone));
        });
    }

    [Fact]
    public void EveryReagentDispenserIsLiveSoABlastBurstsItWithItsReagents()
    {
        // the full suite (2026-10-22): a singularity's blasts reached Meta's wall pepper tank, inert until its own procs are translated
        // (no Initialize, so no reagents), and boom() ran into its missing holder; boom guards an inert dispenser since. The pepper
        // tank went live with gaps SE1, and the serving dish, the last inert one, with gaps ST3: no reagent dispenser is inert now,
        // and a blast bursts the serving dish with its slop as tg's boom does.
        foreach (Type type in Dm.each_cast<Type>(Dm.typesof(typeof(obj_structure_reagent_dispensers))))
            Assert.False(InertSeam.IsInertType(type), type.Name + " is inert");
        var (room, _) = P4aToolTests.Room(9802);
        room.In(() =>
        {
            var dish = Dm.@new<obj_structure_reagent_dispensers_servingdish>(locate(6, 6, 1));
            Assert.False(InertSeam.IsInert(dish));
            Assert.True(Dm.num(((datum_reagents)dish.reagents).total_volume) > 0);
            Waiting.NoSleep(dish.boom());
            Assert.True(QDELETED(dish));
        });
        room.AssertNoRuntimes();
    }
}
