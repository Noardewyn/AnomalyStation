using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M5;

/// <summary>
/// The M5 leftovers (2026-10-16), the atmospherics rows that waited for hands: a portable machine's tank slot (a tank put in by
/// hand, swapped, taken out with an alt-click, portable_atmospherics.dm:215-281, the canister closing its valve, canister.dm:498),
/// the wrench onto a connector port and off it (:283), and a vent and a scrubber welded shut and open (vent_pump.dm:318,
/// vent_scrubber.dm:287), through the real click chain.
/// </summary>
public class AtmosHandsTests
{
    private const int Second = 20;

    private static (AudioRoom r, mob_living_carbon_human bob) Room(ulong seed)
    {
        var r = new AudioRoom(12, 9, seed: seed);
        mob_living_carbon_human bob = r.Join(4, 4, "Bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_ADVANCEDTOOLUSER, "test"));
        r.Step(Second);
        return (r, bob);
    }

    [Fact]
    public void ATankGoesIntoACanisterIsSwappedAndComesOutWithAnAltClick()
    {
        var (r, bob) = Room(5501);
        var can = r.In(() => Dm.@new<obj_machinery_portable_atmospherics_canister_air>(locate(5, 4, 1)));
        r.Step(2);
        var first = WallTests.Hold<obj_item_tank_internals_oxygen>(r, bob);
        WallTests.Click(r, bob, can, 1);
        Assert.Same(first, r.In(() => can.holding));
        Assert.Contains(r.Sounds.Heard, h => h.File == "sound/effects/compressed_air/tank_insert_clunky.ogg");

        var second = WallTests.Hold<obj_item_tank_internals_oxygen>(r, bob);
        r.In(() => can.valve_open = TRUE);
        WallTests.Click(r, bob, can, 1);
        Assert.Same(second, r.In(() => can.holding));
        Assert.True(r.In(() => Dm.truthy(bob.is_holding(first))), "the swapped-out tank is in hand");
        Assert.True(r.In(() => Dm.truthy(can.valve_open)), "a swap leaves the valve as it was");

        r.In(() => qdel(first));
        r.Command(() => ClientSeam.Click(bob.client, can, "left=1;alt=1;button=left"));
        r.Step(Second);
        Assert.Null(r.In(() => can.holding));
        Assert.True(r.In(() => Dm.truthy(bob.is_holding(second))));
        Assert.False(r.In(() => Dm.truthy(can.valve_open)), "taking the tank out closes the valve (canister.dm:503)");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWrenchFastensACanisterToTheConnectorUnderItAndUnfastensIt()
    {
        var (r, bob) = Room(5502);
        var port = r.In(() => Dm.@new<obj_machinery_atmospherics_components_unary_portables_connector_visible>(locate(5, 4, 1), TRUE, EAST));
        r.In(() => SSair.setup_template_machinery(Dm.list(port)));
        var can = r.In(() => Dm.@new<obj_machinery_portable_atmospherics_canister_air>(locate(5, 4, 1)));
        r.Step(2);
        WallTests.Hold<obj_item_wrench>(r, bob);
        WallTests.Click(r, bob, can, 1);
        Assert.Same(port, r.In(() => can.connected_port));
        Assert.True(r.In(() => Dm.truthy(can.anchored)));
        WallTests.Click(r, bob, can, 1);
        Assert.Null(r.In(() => can.connected_port));
        Assert.False(r.In(() => Dm.truthy(can.anchored)));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALitWelderWeldsAVentAndAScrubberShutAndOpenAgain()
    {
        var (r, bob) = Room(5503);
        var vent = r.In(() => Dm.@new<obj_machinery_atmospherics_components_unary_vent_pump_on>(locate(5, 4, 1)));
        var scrubber = r.In(() => Dm.@new<obj_machinery_atmospherics_components_unary_vent_scrubber_on>(locate(4, 5, 1)));
        r.Step(2);
        WallTests.Hold<obj_item_weldingtool>(r, bob);
        WallTests.Click(r, bob, vent, 3);
        Assert.True(r.In(() => Dm.truthy(vent.welded)));
        Assert.Equal("vent_welded", r.In(() => vent.icon_state));
        WallTests.Click(r, bob, scrubber, 3);
        Assert.True(r.In(() => Dm.truthy(scrubber.welded)));
        Assert.Equal("scrub_welded", r.In(() => scrubber.icon_state));
        WallTests.Click(r, bob, vent, 3);
        Assert.False(r.In(() => Dm.truthy(vent.welded)), "welded open again");
        r.AssertNoRuntimes();
    }
}
