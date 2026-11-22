using Anomaly.Client.Shared;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// DV371: a level change is drawn without a jump: a fall eases down under gravity from where the thing was drawn, an announced ride
/// (protocol 77) crosses the level over its ticks, the stairs' flight is no jump, a teleport is drawn where it lands.
/// </summary>
public class LevelRiseTests
{
    private static readonly float Level = TileSpace.LevelHeight;
    private const double Frame = 1.0 / 60;

    // The drawn height in levels: the level, the ramp under it and the rise.
    private static double Height(int z, float ramp, float rise) => z + (ramp + rise) / Level;

    [Fact]
    public void AFallThroughOpenSpaceEasesDownFromTheUpperFloorUnderGravity()
    {
        var rise = new LevelRise();
        double clock = 0;
        Assert.Equal(0f, rise.Rise(5, 5, 2, 0, RideState.None, 100, 102, clock));
        clock += Frame;
        float first = rise.Rise(5.1f, 5, 1, 0, RideState.None, 101, 103, clock);
        Assert.Equal(Level, first, 3); // still drawn on the floor it fell from
        double last = Height(1, 0, first);
        for (int i = 0; i < 60; i++)
        {
            clock += Frame;
            double h = Height(1, 0, rise.Rise(5.1f, 5, 1, 0, RideState.None, 101, 103, clock));
            Assert.True(h <= last + 1e-6, "falling never rises");
            Assert.True(last - h < 0.2, "no frame drops a fifth of a level");
            last = h;
        }
        Assert.Equal(1.0, last, 6); // landed in 0.4 s
    }

    [Fact]
    public void AnAnnouncedRideCrossesTheLevelOverItsTicksAndArrivesWithoutAJump()
    {
        var rise = new LevelRise();
        var ride = new RideState(1, EndTick: 160, Ticks: 60); // starts at tick 100
        double clock = 0, last = 1;
        for (double tick = 98; tick < 162; tick += 0.5)
        {
            clock += Frame;
            int z = tick >= 160 ? 2 : 1; // the level changes in the same tick the ride ends
            RideState state = tick >= 160 ? RideState.None : ride;
            double h = Height(z, 0, rise.Rise(3, 3, z, 0, state, tick, (long)tick + 2, clock));
            Assert.True(h >= last - 1e-6, "a ride up never sinks");
            Assert.True(h - last < 0.05, "no step of the ride is a jump at " + tick);
            last = h;
        }
        Assert.Equal(2.0, last, 3);
    }

    [Fact]
    public void ARideAnnouncedAheadOfTheDrawnTickHoldsUntilTheDrawnLevelChanges()
    {
        // another rider is drawn three ticks behind the newest state: the ride's end reaches the replica before the drawn level changes
        var rise = new LevelRise();
        var ride = new RideState(-1, EndTick: 160, Ticks: 60);
        var next = new RideState(-1, EndTick: 221, Ticks: 60); // the next leg, announced a tick after the arrival
        double clock = 0, last = 3;
        for (double tick = 100; tick < 230; tick += 0.5)
        {
            clock += Frame;
            long newest = (long)tick + 3;
            int z = tick >= 221 ? 1 : tick >= 160 ? 2 : 3;
            RideState state = newest >= 221 ? RideState.None : newest >= 161 ? next : newest >= 160 ? RideState.None : ride;
            double h = Height(z, 0, rise.Rise(3, 3, z, 0, state, tick, newest, clock));
            Assert.True(h <= last + 1e-6, "a ride down never rises at " + tick);
            Assert.True(last - h < 0.05, "no step of the ride is a jump at " + tick);
            last = h;
        }
        Assert.Equal(1.0, last, 3);
    }

    [Fact]
    public void AClimbGivenUpSinksBackToTheFloorItStartedOn()
    {
        var rise = new LevelRise();
        var climb = new RideState(1, EndTick: 120, Ticks: 20);
        double clock = 0;
        float half = 0;
        for (double tick = 100; tick < 110; tick += 0.5)
        {
            clock += Frame;
            half = rise.Rise(3, 3, 1, 0, climb, tick, (long)tick, clock);
        }
        Assert.InRange(half / Level, 0.4, 0.5);
        float after = 0;
        for (int i = 0; i < 60; i++)
        {
            clock += Frame;
            after = rise.Rise(3, 3, 1, 0, RideState.None, 110, 110, clock);
            Assert.True(after <= half + 1e-4);
        }
        Assert.Equal(0f, after);
    }

    [Fact]
    public void TheStairsFlightAndATeleportAreNoJumpToEase()
    {
        var rise = new LevelRise();
        // up the flight: the ramp rises to a level at the tile's far edge, where the walker steps onto the level above
        Assert.Equal(0f, rise.Rise(10, 12.4f, 1, 0.98f * Level, RideState.None, 1, 1, 0));
        Assert.Equal(0f, rise.Rise(10, 12.6f, 2, 0, RideState.None, 2, 2, Frame));
        // a teleport across the map to another level is drawn where it lands
        Assert.Equal(0f, rise.Rise(40, 40, 1, 0, RideState.None, 3, 3, 2 * Frame));
    }
}
