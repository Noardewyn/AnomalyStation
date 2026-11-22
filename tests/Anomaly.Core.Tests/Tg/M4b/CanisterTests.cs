using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.M2Locomotion.T2;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.M4b;

/// <summary>
/// M4b slice 2 (the canisters and portable machines): tg's canisters in a booted 16 x 16 toy room. A gas canister is filled
/// to half its maximum pressure with its gas (an air canister with tg's oxygen and nitrogen shares); its opened valve
/// releases gas into a vacuum room until the room is within the valve's friction margin of the canister (short of its
/// release pressure, one atmosphere) and a closed valve keeps it; a canister
/// connected to a connector port joins the port's pipeline; a portable scrubber that is on takes the room's carbon dioxide.
/// </summary>
public class CanisterTests
{
    private const int Second = 20; // ticks per second

    private static float RoomPressure(T2Room r, int x, int y) => r.In(() => Dm.num(((turf_open)locate(x, y, 1)).air.return_pressure()));

    private static void Empty(T2Room r) => r.In(() =>
    {
        for (int x = 2; x < T2Room.Size; x++)
            for (int y = 2; y < T2Room.Size; y++)
                if (locate(x, y, 1) is turf_open t)
                {
                    t.air = SSair.parse_gas_string(AIRLESS_ATMOS) as datum_gas_mixture;
                    SSair.add_to_active(t);
                }
    });

    [Fact]
    public void AGasCanisterIsFilledToHalfItsMaximumPressure()
    {
        var r = new T2Room(4501);
        var o2 = r.In(() => Dm.@new<obj_machinery_portable_atmospherics_canister_oxygen>(locate(4, 4, 1)));
        var air = r.In(() => Dm.@new<obj_machinery_portable_atmospherics_canister_air>(locate(6, 4, 1)));
        float half = r.In(() => o2.maximum_pressure * o2.filled); // 45 atmospheres
        Assert.InRange(r.In(() => Dm.num(o2.air_contents.return_pressure())), half - 1, half + 1);
        Assert.InRange(r.In(() => Dm.num(air.air_contents.return_pressure())), half - 1, half + 1);
        float o2share = r.In(() => Dm.num(air.air_contents.moles[typeof(datum_gas_oxygen)]) / Dm.num(air.air_contents.total_moles()));
        Assert.InRange(o2share, O2STANDARD - 0.001f, O2STANDARD + 0.001f);
        Assert.Contains("xygen", r.In(() => o2.desc).ToString()); // GLOB.meta_gas_info's name and description
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AnOpenedValveFillsAVacuumRoomUpToItsReleasePressure()
    {
        var r = new T2Room(4502);
        var can = r.In(() => Dm.@new<obj_machinery_portable_atmospherics_canister_air>(locate(8, 8, 1)));
        r.Step(Second);
        Empty(r);
        r.Step(10 * Second);
        Assert.True(RoomPressure(r, 4, 4) < 1f, "the closed valve keeps the gas: " + RoomPressure(r, 4, 4));

        r.In(() => can.toggle_valve(null));
        r.Step(240 * Second);
        float room = RoomPressure(r, 4, 4);
        float canKpa = r.In(() => Dm.num(can.air_contents.return_pressure()));
        // Half a canister of air (about 3,700 mol) is far less than this 14 x 14 room holds at one atmosphere (about
        // 20,000 mol), so release_gas_to stops where the room is within its 10 kPa friction margin of the canister
        // (portable_atmospherics' release_gas_to, gas_mixture.dm:723), at about 18 kPa, short of the release pressure.
        Assert.True(room > 12f, "the valve released the canister's air into the room: " + room);
        Assert.InRange(canKpa - room, 0f, 12f);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ACanisterConnectedToAPortJoinsItsPipeline()
    {
        var r = new T2Room(4503);
        var port = r.In(() => Dm.@new<obj_machinery_atmospherics_components_unary_portables_connector_visible>(locate(5, 5, 1), TRUE, EAST));
        var pipes = new List<obj_machinery_atmospherics> { port };
        for (int x = 6; x <= 9; x++)
            pipes.Add(r.In(() => Dm.@new<obj_machinery_atmospherics_pipe_smart>(locate(x, 5, 1))));
        r.In(() => SSair.setup_template_machinery(Dm.list(pipes.ToArray())));
        var can = r.In(() => Dm.@new<obj_machinery_portable_atmospherics_canister_nitrogen>(locate(5, 5, 1)));
        Assert.True(r.In(() => Dm.truthy(Waiting.NoSleep(can.connect(port)))));
        Assert.Same(can, r.In(() => port.connected_device));
        Assert.True(r.In(() => Dm.truthy(can.anchored)));

        r.Step(5 * Second);
        datum_pipeline net = r.In(() => port.parents[1] as datum_pipeline);
        float pipeKpa = r.In(() => Dm.num(net.air.return_pressure()));
        float canKpa = r.In(() => Dm.num(can.air_contents.return_pressure()));
        Assert.True(pipeKpa > 1000f, "the canister's nitrogen is in the pipeline: " + pipeKpa);
        Assert.InRange(pipeKpa, canKpa * 0.95f, canKpa * 1.05f); // the port's reconcile_air shares the canister's air by volume

        r.In(() => Waiting.NoSleep(can.disconnect()));
        Assert.Null(r.In(() => port.connected_device));
        Assert.False(r.In(() => Dm.truthy(can.anchored)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APortableScrubberThatIsOnTakesTheRoomsCarbonDioxide()
    {
        var r = new T2Room(4504);
        var scrubber = r.In(() => Dm.@new<obj_machinery_portable_atmospherics_scrubber>(locate(8, 8, 1)));
        r.Step(Second);
        r.In(() =>
        {
            var t = (turf_open)locate(8, 8, 1);
            t.air.merge(SSair.parse_gas_string("co2=10;TEMP=293.15") as datum_gas_mixture);
            SSair.add_to_active(t);
            scrubber.on = TRUE; // the tgui window's "power" (scrubber.dm:174)
            SSair.start_processing_machine(scrubber);
        });
        r.Step(30 * Second);
        float held = r.In(() => Dm.num(scrubber.air_contents.moles[typeof(datum_gas_carbon_dioxide)]));
        Assert.True(held > 2f, "the scrubber holds the carbon dioxide it took: " + held);
        Assert.True(r.In(() => Dm.num(scrubber.air_contents.moles[typeof(datum_gas_oxygen)])) < 0.01f, "and no oxygen");
        r.AssertNoRuntimes();
    }
}
