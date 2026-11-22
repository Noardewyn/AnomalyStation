using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Boot;
using Anomaly.Core.Tests.Tg.M3Maps.P2;
using Anomaly.Core.Tg;
using Xunit;
using Xunit.Abstractions;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M10Shuttles;

/// <summary>
/// M10 slice G1 (D272): tg's turf reservations on MetaStation. SSmapping's initialize adds the first transit level (z 2, tg's
/// "Transit/Reserved #1"); a reservation takes a free block of it with tg's cordon around it, its turfs the reservation's type; its
/// release hands them back through SSmapping's fire.
/// </summary>
public class ReservationTests
{
    private readonly ITestOutputHelper _output;

    public ReservationTests(ITestOutputHelper output) => _output = output;

    [MetaFact]
    public void Meta_has_a_reserved_level_and_a_transit_reservation_takes_a_block_of_it_with_a_cordon()
    {
        World world = MetaMaps.Boot(out TgTestWorld.Log log);
        int bootRuntimes = log.Runtimes.Count;
        datum_turf_reservation_transit reservation;
        using (world.Enter())
        {
            Assert.Equal(4f, Dm.world.maxz); // the station, CentCom (DV239), Lavaland (L1, D304), the reserved level
            DmList reserved = SSmapping.levels_by_trait(ZTRAIT_RESERVED) as DmList;
            Assert.Equal(new object[] { 4f }, Dm.each(reserved).ToArray());
            Assert.Equal("Transit/Reserved #2", ((datum_space_level)SSmapping.z_list[4]).name); // tg's counter starts at 1 and counts up first
            int unused = (int)((DmList)SSmapping.unused_turfs["4"]).len;
            // the block between the transit borders, less what the round-start shuttles' reservations hold until SSmapping's fire frees them
            Assert.InRange(unused, 30000, (255 - 2 * SHUTTLE_TRANSIT_BORDER + 1) * (255 - 2 * SHUTTLE_TRANSIT_BORDER + 1));
            Assert.True(Dm.truthy(locate(100, 100, 4).blocks_air));

            reservation = (datum_turf_reservation_transit)Waiting.NoSleep(SSmapping.request_turf_block_reservation(10, 12, reservation_type: typeof(datum_turf_reservation_transit)));
            Assert.NotNull(reservation);
            Assert.Equal(120, reservation.reserved_turfs.len);
            var bottomLeft = (turf)reservation.bottom_left_turfs[1];
            Assert.Equal(4f, bottomLeft.z);
            Assert.IsType<turf_open_space_transit>(bottomLeft);
            Assert.True(Dm.truthy(Dm.bitand(bottomLeft.turf_flags, RESERVATION_TURF)));
            Assert.Same(reservation, SSmapping.get_reservation_from_turf(bottomLeft));
            Assert.Equal(2 * (10 + 2) + 2 * 12, reservation.cordon_turfs.len);
            Assert.IsType<turf_cordon>(locate(bottomLeft.x - 1, bottomLeft.y - 1, 4));
            Assert.IsType<area_misc_cordon>(locate(bottomLeft.x - 1, bottomLeft.y - 1, 4).loc);
            Assert.Equal(unused - 120 - reservation.cordon_turfs.len, ((DmList)SSmapping.unused_turfs["4"]).len);
            qdel(reservation);
        }
        for (int i = 0; i < 60; i++) world.Step(); // SSmapping fires every 2 ds, after its first wait
        using (world.Enter())
        {
            Assert.Equal(0, Dm.each<DmList>(SSmapping.lists_to_reserve).Sum(l => l.len));
            Assert.IsType<turf_open_space_basic>(locate(100, 100, 4));
            Assert.True(log.Runtimes.Count == bootRuntimes, string.Join("\n", log.Runtimes.Skip(bootRuntimes)));
        }
    }
}
