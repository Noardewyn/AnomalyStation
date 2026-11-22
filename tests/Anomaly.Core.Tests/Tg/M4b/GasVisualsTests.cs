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
/// M4b slice 3 (the gas visuals): tg's update_visuals puts one /obj/effect/overlay/gas per visible gas into a turf's
/// vis_contents (GAS_OVERLAYS): the overlay of the gas's icon state at the level ceil(moles / 0.25) (at most 80), whose
/// alpha is log4((level + 32) / 28) * 255; a gas under its moles_visible, or one with no overlay (oxygen), shows nothing;
/// the overlays follow the air as it spreads and go when it is gone.
/// </summary>
public class GasVisualsTests
{
    private const int Second = 20; // ticks per second

    private static void SetAir(T2Room r, int x, int y, string mix) => r.In(() =>
    {
        var t = (turf_open)locate(x, y, 1);
        t.air = SSair.parse_gas_string(mix) as datum_gas_mixture;
        t.update_visuals();
        SSair.add_to_active(t);
    });

    private static List<obj_effect_overlay_gas> Overlays(T2Room r, int x, int y) => r.In(() =>
        new List<obj_effect_overlay_gas>(Dm.each<obj_effect_overlay_gas>(locate(x, y, 1).vis_contents)));

    [Fact]
    public void AVisibleGasPutsItsOverlayOnTheTurf()
    {
        var r = new T2Room(4501);
        r.Step(Second);
        SetAir(r, 8, 8, "o2=22;n2=82;plasma=5;water_vapor=0.2;TEMP=293.15");
        List<obj_effect_overlay_gas> overlays = Overlays(r, 8, 8);
        // plasma at 5 moles (level 20); water vapour at 0.2 is under its moles_visible (0.25); oxygen and nitrogen have no overlay
        obj_effect_overlay_gas plasma = Assert.Single(overlays);
        Assert.Equal("plasma", plasma.icon_state);
        Assert.Equal(Math.Log((20 + 32) / 28.0, 4) * 255, plasma.alpha, 2);
        Assert.Null(plasma.loc);
        Assert.Equal(r.In(() => ((DmList)((DmList)((DmList)GLOB.meta_gas_info[META_GAS_OVERLAY])[typeof(datum_gas_plasma)])[1])[20]), plasma);
        Assert.Empty(Overlays(r, 3, 3));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheOverlaysFollowTheAirAsItSpreads()
    {
        var r = new T2Room(4502);
        r.Step(Second);
        SetAir(r, 8, 8, "o2=22;n2=82;plasma=60;TEMP=293.15");
        Assert.Equal(TOTAL_VISIBLE_STATES, PlasmaLevel(r, 8, 8));
        r.Step(5 * Second);
        // the plasma spread: the source thinned and its neighbour shows the gas too
        Assert.NotEmpty(Overlays(r, 9, 8));
        Assert.InRange(PlasmaLevel(r, 8, 8), 1, TOTAL_VISIBLE_STATES - 1);
        r.In(() =>
        {
            var t = (turf_open)locate(8, 8, 1);
            t.air.moles[typeof(datum_gas_plasma)] = 0;
            t.update_visuals();
        });
        Assert.Empty(Overlays(r, 8, 8));
        Assert.Null(r.In(() => ((turf_open)locate(8, 8, 1)).atmos_overlay_types));
        r.AssertNoRuntimes();
    }

    // The level (1-80) of the plasma overlay on a tile: its index in tg's overlay list of the gas (plane offset 0).
    private static int PlasmaLevel(T2Room r, int x, int y) => r.In(() =>
    {
        var levels = (DmList)((DmList)((DmList)GLOB.meta_gas_info[META_GAS_OVERLAY])[typeof(datum_gas_plasma)])[1];
        foreach (obj_effect_overlay_gas overlay in Dm.each<obj_effect_overlay_gas>(locate(x, y, 1).vis_contents))
            return (int)Dm.num(levels.Find(overlay));
        return 0;
    });
}
