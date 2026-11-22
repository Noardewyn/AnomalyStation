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
/// M4b slice 1 (D134, the air that moves): tg's LINDA in a booted 16 x 16 toy room (walls on the rim, floor 2..15), where
/// SSair fires every 0.5 s (10 ticks): a room at rest has no active turfs; a gas difference spreads and its moles are kept; a
/// wall tile separates two halves and an opening equalizes them; a full-tile window blocks the air until it is deleted; a
/// breach empties the room into space and its wind pushes a human toward the hole; a patch stops the loss.
/// </summary>
public class AirMovesTests
{
    private const int Second = 20; // ticks per second

    private static float Pressure(T2Room r, int x, int y) => r.In(() => Dm.num(((turf_open)locate(x, y, 1)).air.return_pressure()));

    private static float FloorMoles(T2Room r) => r.In(() =>
    {
        float total = 0;
        for (int x = 2; x < T2Room.Size; x++)
            for (int y = 2; y < T2Room.Size; y++)
                if (locate(x, y, 1) is turf_open t && t is not turf_open_space)
                    total += Dm.num(t.air.total_moles());
        return total;
    });

    private static void SetAir(T2Room r, int x, int y, string mix) => r.In(() =>
    {
        var t = (turf_open)locate(x, y, 1);
        t.air = SSair.parse_gas_string(mix) as datum_gas_mixture;
        SSair.add_to_active(t);
    });

    private static void Wall(T2Room r, int x, int y) => r.In(() => Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_closed_indestructible))));

    private static void Floor(T2Room r, int x, int y) => r.In(() => Waiting.NoSleep(locate(x, y, 1).ChangeTurf(typeof(turf_open_indestructible))));

    [Fact]
    public void ARoomAtRestHasNoActiveTurfsAndSsairFires()
    {
        var r = new T2Room(4101);
        float fired = r.In(() => SSair.times_fired);
        r.Step(2 * Second);
        Assert.True(r.In(() => SSair.times_fired) >= fired + 3, "SSair fires every 0.5 seconds");
        Assert.Equal(0, r.In(() => SSair.active_turfs.len));
        Assert.Equal(0, r.In(() => SSair.excited_groups.len));
        Assert.True(Pressure(r, 8, 8) > 100f && Pressure(r, 8, 8) < 103f, "one atmosphere: " + Pressure(r, 8, 8));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AVacuumTileFillsFromItsNeighboursAndTheMolesAreKept()
    {
        var r = new T2Room(4102);
        r.Step(Second);
        SetAir(r, 8, 8, AIRLESS_ATMOS);
        float before = FloorMoles(r);
        Assert.True(Pressure(r, 8, 8) < 1f);
        r.Step(10 * Second);
        Assert.True(Pressure(r, 8, 8) > 95f, "the hole fills from around it: " + Pressure(r, 8, 8));
        Assert.True(Pressure(r, 3, 3) < 101.3f, "the room gave some of its air: " + Pressure(r, 3, 3));
        Assert.InRange(FloorMoles(r), before * 0.999f, before * 1.001f);
        r.Step(30 * Second);
        Assert.Equal(0, r.In(() => SSair.active_turfs.len)); // the excited group broke down and dismantled
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWallSeparatesTwoHalvesAndAnOpeningEqualizesThem()
    {
        var r = new T2Room(4103);
        for (int y = 2; y < T2Room.Size; y++)
            Wall(r, 8, y);
        r.Step(Second);
        for (int x = 2; x < 8; x++)
            for (int y = 2; y < T2Room.Size; y++)
                SetAir(r, x, y, AIRLESS_ATMOS);
        r.Step(10 * Second);
        Assert.True(Pressure(r, 4, 8) < 1f, "the west half stays empty: " + Pressure(r, 4, 8));
        Assert.True(Pressure(r, 12, 8) > 100f, "the east half keeps its air: " + Pressure(r, 12, 8));

        Floor(r, 8, 8); // tg's ChangeTurf to an open turf: the adjacency is recalculated and the turf woken
        r.Step(60 * Second);
        float west = Pressure(r, 4, 8);
        float east = Pressure(r, 12, 8);
        Assert.True(west > 30f && east < 70f, $"the halves share through the opening: west {west}, east {east}");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AFullTileWindowBlocksTheAirUntilItIsGone()
    {
        var r = new T2Room(4104);
        for (int y = 2; y < T2Room.Size; y++)
        {
            if (y != 8)
                Wall(r, 8, y);
        }
        obj_structure_window window = r.In(() => Dm.@new<obj_structure_window_fulltile>(locate(8, 8, 1)));
        r.Step(Second);
        for (int x = 2; x < 8; x++)
            for (int y = 2; y < T2Room.Size; y++)
                SetAir(r, x, y, AIRLESS_ATMOS);
        r.Step(10 * Second);
        Assert.True(Pressure(r, 7, 8) < 1f, "the window keeps the air out: " + Pressure(r, 7, 8));
        Assert.True(Pressure(r, 9, 8) > 100f, "the window keeps the air in: " + Pressure(r, 9, 8));

        r.In(() => qdel(window));
        r.Step(30 * Second);
        Assert.True(Pressure(r, 7, 8) > 20f, "the air flows where the window was: " + Pressure(r, 7, 8));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ABreachEmptiesTheRoomIntoSpaceAndItsWindPushesAHumanTowardTheHole()
    {
        var r = new T2Room(4105);
        // tg's push (LINDA_turf_tile.dm:398): a human (pressure_resistance 25, move_resist 1000) moves for a share difference of
        // 25 kPa or more, which the room has within about two tiles of the hole
        mob_living_carbon_human bob = r.Dummy(3, 8, name: "bob");
        r.In(() => ADD_TRAIT(bob, TRAIT_GODMODE, "test")); // the wind, not the vacuum, is under test here
        r.Step(Second);
        float before = FloorMoles(r);
        string reply = r.In(() => DevCommands.Breach(locate(1, 8, 1)));
        Assert.Contains("(1,8,1) is space now", reply);
        Assert.IsType<turf_open_space>(r.In(() => locate(1, 8, 1)));

        int startX = r.LocOf(bob).X;
        bool pushed = false;
        for (int i = 0; i < 20 && !pushed; i++)
        {
            r.Step(Second / 2);
            pushed = r.LocOf(bob).X < startX;
        }
        Assert.True(pushed, "the space wind pushed bob toward the breach");
        r.AssertOnLoc(bob, "after the push");

        // tg's LINDA drains a 14 x 14 room through one hole slowly: the shares by the hole stay above MINIMUM_AIR_TO_SUSPEND,
        // so the excited group never breaks down to average the room (LINDA_turf_tile.dm:226), and the far side follows by
        // diffusion (measured: about 4% of the air in the first 10 s, half of it in about 4.5 minutes)
        r.Step(60 * Second);
        Assert.True(FloorMoles(r) < before * 0.9f, $"the room vents: {FloorMoles(r)} of {before} moles left");
        Assert.True(Pressure(r, 14, 14) < 100f, "the far corner too: " + Pressure(r, 14, 14));
        Assert.True(Pressure(r, 2, 8) < 1f, "the tile by the hole is empty: " + Pressure(r, 2, 8));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APatchStopsTheLossAndTheRoomSettles()
    {
        var r = new T2Room(4106);
        r.Step(Second);
        Type wall = r.In(() => locate(1, 8, 1).GetType());
        r.In(() => DevCommands.Breach(locate(1, 8, 1)));
        r.Step(3 * Second);
        string reply = r.In(() => DevCommands.Patch(locate(1, 8, 1)));
        Assert.Contains("again", reply); // the owner (2026-10-15): patch puts back what the breach removed
        Assert.Equal(wall, r.In(() => locate(1, 8, 1).GetType()));
        r.Step(2 * Second);
        float after = FloorMoles(r);
        r.Step(20 * Second);
        Assert.InRange(FloorMoles(r), after * 0.99f, after * 1.01f);
        r.Step(30 * Second);
        float near = Pressure(r, 2, 8);
        float far = Pressure(r, 14, 14);
        Assert.True(Math.Abs(near - far) < 5f, $"the room equalized: {near} by the patch, {far} in the far corner");
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AWallDeletedBetweenTheRoomAndSpaceVentsTheRoom()
    {
        // the owner's play test (2026-10-13): a wall with space behind it, deleted with the dev tool (tg's ScrapeAway: the wall
        // becomes its baseturf), must let the air out as a removed floor does
        var r = new T2Room(4107);
        r.In(() => Waiting.NoSleep(locate(1, 8, 1).ChangeTurf(typeof(turf_open_space))));
        r.In(() => Waiting.NoSleep(locate(2, 8, 1).ChangeTurf(typeof(turf_closed_wall))));
        r.Step(10 * Second);
        Assert.True(Pressure(r, 3, 8) > 100f, "the wall holds the air: " + Pressure(r, 3, 8));

        string reply = r.In(() => DevCommands.Delete(locate(2, 8, 1)));
        Assert.Contains("scraped away", reply);
        Assert.IsAssignableFrom<turf_open>(r.In(() => locate(2, 8, 1)));
        r.Step(10 * Second);
        Assert.True(Pressure(r, 2, 8) < 20f, $"the hole empties ({reply}): " + Pressure(r, 2, 8));
        Assert.True(Pressure(r, 3, 8) < 60f, "the tile by the hole too: " + Pressure(r, 3, 8));
        r.AssertNoRuntimes();
    }
}
