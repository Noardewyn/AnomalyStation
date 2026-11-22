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
/// M4b slice 3 (wet floors and slipping): tg's water vapour condensation (reactions.dm, the vapour reaction) wets the floor
/// it condenses on through the wet_floor component, which gives the turf tg's slippery component; a human who runs onto it
/// slips and falls (handle_slip's knockdown), one who walks does not (NO_SLIP_WHEN_WALKING); the floor dries over time
/// (SSwet_floors, by the air's temperature).
/// </summary>
public class WetFloorTests
{
    private const int Second = 20; // ticks per second
    private static readonly ushort East = T2Room.Yaw(EAST);

    private static datum_component_wet_floor WetOf(T2Room r, int x, int y) => r.In(() => locate(x, y, 1).GetComponent(typeof(datum_component_wet_floor)) as datum_component_wet_floor);

    [Fact]
    public void WaterVapourCondensesIntoAWetFloor()
    {
        var r = new T2Room(4401);
        r.Step(Second);
        r.In(() =>
        {
            var t = (turf_open)locate(8, 8, 1);
            t.air = SSair.parse_gas_string("o2=22;n2=82;water_vapor=2;TEMP=293.15") as datum_gas_mixture;
            SSair.add_to_active(t);
        });
        bool wet = false;
        for (int i = 0; i < 10 && !wet; i++)
        {
            r.Step(Second / 2);
            wet = WetOf(r, 8, 8) != null;
        }
        Assert.True(wet, "the vapour condensed on the tile");
        Assert.Equal((float)TURF_WET_WATER, r.In(() => WetOf(r, 8, 8).highest_strength));
        Assert.NotNull(r.In(() => locate(8, 8, 1).GetComponent(typeof(datum_component_slippery))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ARunningHumanSlipsOnTheWetFloorAndAWalkingOneDoesNot()
    {
        var r = new T2Room(4402);
        mob_living_carbon_human runner = r.Join(4, 8, "Runner", name: "runner");
        mob_living_carbon_human walker = r.Join(4, 10, "Walker", name: "walker");
        r.In(() =>
        {
            for (int x = 6; x <= 9; x++)
            {
                Waiting.NoSleep(((turf_open)locate(x, 8, 1)).MakeSlippery(TURF_WET_WATER, 100));
                Waiting.NoSleep(((turf_open)locate(x, 10, 1)).MakeSlippery(TURF_WET_WATER, 100));
            }
            Waiting.NoSleep(walker.set_move_intent(MOVE_INTENT_WALK));
        });
        r.Go(runner, 0, 0, East, 2);
        r.Go(walker, 0, 0, East, 2);

        bool fell = false;
        for (int i = 0; i < 3 * Second && !fell; i++)
        {
            r.Frame(runner, 127, 0, East);
            r.Go(walker, 127, 0, East);
            fell = r.In(() => runner.body_position == LYING_DOWN);
        }
        Assert.True(fell, "the runner slipped on the wet floor and fell");
        for (int i = 0; i < 4 * Second && r.LocOf(walker).X < 9; i++)
            r.Go(walker, 127, 0, East);
        Assert.True(r.LocOf(runner).X >= 6, "where the wet floor starts: " + r.LocOf(runner));
        Assert.Equal(STANDING_UP, r.In(() => walker.body_position));
        Assert.True(r.LocOf(walker).X >= 6, "the walker crossed the wet floor: " + r.LocOf(walker));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWetFloorDriesInRoomAir()
    {
        var r = new T2Room(4403);
        r.Step(Second);
        r.In(() => Waiting.NoSleep(((turf_open)locate(8, 8, 1)).MakeSlippery(TURF_WET_WATER, 100)));
        Assert.NotNull(WetOf(r, 8, 8));
        // SSwet_floors: at 20 C the floor loses (20 / 2) * (1 s / 1.5 s) of its 100 deciseconds each second
        r.Step(10 * Second);
        Assert.NotNull(WetOf(r, 8, 8));
        r.Step(10 * Second);
        Assert.Null(WetOf(r, 8, 8));
        Assert.Null(r.In(() => locate(8, 8, 1).GetComponent(typeof(datum_component_slippery))));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void FreezingVapourFreezesTheItemsAndAColdPersonOnTheFloor()
    {
        // M10x closing: freeze_turf (_open.dm:520) with the frozen element and the freon effect
        var r = new T2Room(4405);
        mob_living_carbon_human cold = r.Join(8, 8, "Cold", name: "cold");
        obj_item_wrench wrench = r.In(() => Dm.@new<obj_item_wrench>(locate(8, 8, 1)));
        r.In(() =>
        {
            cold.bodytemperature = 40;
            Assert.Equal(TRUE, Waiting.NoSleep(((turf_open)locate(8, 8, 1)).freeze_turf()));
        });
        r.In(() =>
        {
            Assert.True(HAS_TRAIT(wrench, TRAIT_FROZEN));
            Assert.Equal("frozen wrench", Dm.str(wrench.name));
            Assert.Equal(230, Dm.num(wrench.alpha));
            Assert.NotNull(cold.has_status_effect(typeof(datum_status_effect_freon)));
            Assert.True(HAS_TRAIT(cold, TRAIT_IMMOBILIZED));
            Assert.Equal((float)TURF_WET_PERMAFROST, WetOf(r, 8, 8).highest_strength);
        });

        // carried onto a warm floor, the wrench thaws (on_moved)
        r.In(() => Waiting.NoSleep(wrench.forceMove(locate(10, 8, 1))));
        r.In(() =>
        {
            Assert.False(HAS_TRAIT(wrench, TRAIT_FROZEN));
            Assert.Equal("wrench", Dm.str(wrench.name));
            Assert.Equal(255, Dm.num(wrench.alpha));
        });
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AShatteringThingThrownIntoAFloorBreaksIntoItsShards()
    {
        // M10x closing: the can_shatter element (a glass material's, basemats.dm:73) on its throw impact
        var r = new T2Room(4406);
        obj_item_wrench wrench = r.In(() => Dm.@new<obj_item_wrench>(locate(8, 8, 1)));
        r.In(() =>
        {
            Waiting.NoSleep(wrench.AddElement(typeof(datum_element_can_shatter), typeof(obj_item_shard), 3, SFX_SHATTER));
            wrench._SendSignal(COMSIG_MOVABLE_IMPACT, Dm.list(wrench, locate(9, 8, 1), null, FALSE));
        });
        r.In(() =>
        {
            Assert.True(QDELETED(wrench));
            Assert.Equal(3, Dm.each<obj_item_shard>(locate(9, 8, 1).contents).Count());
        });
        r.AssertNoRuntimes();
    }
}
