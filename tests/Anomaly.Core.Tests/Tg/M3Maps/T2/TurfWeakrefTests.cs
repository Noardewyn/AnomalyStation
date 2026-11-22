using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Xunit;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M3Maps.T2;

/// <summary>M3 T2: the turf's weak reference over ChangeTurf (TURF-IDENTITY, object-model.md 7.1). Fixture: turfs.dmm.</summary>
public class TurfWeakrefTests
{
    // tg: change_turf.dm:151-153 `new_turf.weak_reference = old_ref`. A weakref resolves by `locate(reference)` (weakrefs.dm:76)
    // and BYOND's reference of a turf is its location, so a holder of the old turf's weakref reaches the new turf: that is why
    // ChangeTurf moves the weakref over ("Turf references survive replacement", change_turf.dm:104).
    [Fact]
    public void A_weak_reference_to_a_turf_resolves_to_the_new_turf_after_its_replacement()
    {
        World world = T2Maps.Boot("turfs");
        using (world.Enter())
        {
            turf old = locate(6, 3, 1);
            var weak = (datum_weakref)WEAKREF(old);
            Assert.Same(old, weak.resolve());
            var wall = (turf)Waiting.NoSleep(old.dm_change_turf(typeof(turf_closed_wall), null, null));
            Assert.Same(weak, wall.weak_reference);
            Assert.Same(wall, weak.resolve());
            Assert.Same(wall, WEAKREF(wall) is datum_weakref w ? w.resolve() : null);
            // and again, after a second replacement
            var floor = (turf)Waiting.NoSleep(wall.dm_change_turf(typeof(turf_open_floor_plating), null, null));
            Assert.Same(floor, weak.resolve());
        }
    }

}
