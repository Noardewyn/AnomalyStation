using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Motion;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.Wave4.Integration.Toy;

namespace Anomaly.Core.Tests.Tg.M2Locomotion.T2;

/// <summary>
/// Facing (scope 2b): the view's yaw, quantized to the four directions with M1's 5 degree hysteresis, is tg's dir, and the
/// bridge calls setDir only when it changes; the crossing Move passes update_dir = FALSE (tg's move loops do the same,
/// atoms_movable.dm:700-705), so moving never turns the mob. A mob that never got a frame keeps the dir tg gave it.
/// The hysteresis is "once a mob faces a direction it keeps it until the yaw is more than 45 degrees plus 5 away from that
/// direction's bearing" (MotionFacing); a yaw unit is 360/65536 degrees: 45 degrees are 8192, 5 degrees about 910.
/// </summary>
public class FacingTests
{
    // yaw bearings: north 0, east 16384, south 32768, west 49152 (clockwise from above)
    public static TheoryData<int, float> Sequence => new()
    {
        // (yaw, the dir after the tick) from a mob that starts facing south (a new human, living/Initialize)
        { 32768, SOUTH },
        { 32768 + 9000, SOUTH },      // 49.4 degrees off south: still south (within 45 + 5)
        { 32768 + 9300, WEST },       // 51.1 degrees off south: the nearest direction, west (7084 away)
        { 49152 - 9000, WEST },       // 49.4 off west, nearer to south: kept
        { 49152 - 9300, SOUTH },      // 51.1 off west: south is the nearest
        { 0, NORTH },                 // across the whole circle: the nearest
        { 65535 - 8000, NORTH },      // wraps over zero: 8001 off north, kept
        { 56000, WEST },              // 9536 off north: west (6848 away) is the nearest
        { 16384, EAST },
        { 16384 + 9100, EAST },       // at the limit
        { 16384 - 9100, EAST },
        { 16384 + 12000, SOUTH },     // 65.9 degrees off east: south (4384 away)
    };

    [Fact]
    public void TheDirFollowsTheYawWithTheHysteresisOfFortyFivePlusFiveDegrees()
    {
        var r = new T2Room(501);
        mob_living_carbon_human m = r.Join(8, 8, "Alice", name: "mover");
        Assert.Equal(SOUTH, r.In(() => m.dir)); // a new mob faces south
        foreach (object[] row in Sequence)
        {
            ushort yaw = (ushort)(int)row[0];
            float expected = (float)row[1];
            r.Go(m, 0, 0, yaw);
            Assert.True(expected == r.In(() => m.dir), $"yaw {yaw}: expected dir {expected} but the mob faces {r.In(() => m.dir)}");
            Assert.Equal(yaw, r.State(m).Yaw); // the state keeps the exact view yaw
        }
        r.AssertNoRuntimes();
    }

    [Fact]
    public void SetDirRunsOnlyOnAChangeAndOnceForEachChange()
    {
        var r = new T2Room(502);
        mob_living_carbon_human m = r.Join(8, 8, "Alice", name: "mover");
        r.In(() =>
        {
            On(m, COMSIG_ATOM_PRE_DIR_CHANGE, "pre_dir", 2);
            On(m, COMSIG_ATOM_DIR_CHANGE, "dir", 2);
            On(m, COMSIG_ATOM_POST_DIR_CHANGE, "post_dir", 2);
        });
        r.Go(m, 0, 0, 32768, 4);                       // south again: nothing
        Assert.Empty(r.Trace);
        r.Go(m, 0, 0, 32768 + 3000, 4);                // wobbling inside the band: nothing
        r.Go(m, 0, 0, 32768 - 3000, 4);
        Assert.Empty(r.Trace);
        r.Go(m, 0, 0, T2Room.Yaw(EAST), 5);            // one real turn
        AssertTrace(new[] { "pre_dir mover 2 4", "dir mover 2 4", "post_dir mover 2 4" }, r.Trace);
        r.Go(m, 0, 0, (ushort)(T2Room.Yaw(EAST) + 8000), 3);     // 43.9 degrees: still east
        Assert.Equal(3, r.Trace.Count);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void WalkingNeverTurnsTheMobTheViewDoes()
    {
        var r = new T2Room(503);
        mob_living_carbon_human m = r.Join(5, 5, "Alice", name: "mover");
        r.Go(m, 0, 0, T2Room.Yaw(EAST), 2);
        r.In(() =>
        {
            On(m, COMSIG_ATOM_DIR_CHANGE, "dir", 2);
            On(m, COMSIG_MOVABLE_PRE_MOVE, "pre_move", 1);
            On(m, COMSIG_MOB_CLIENT_MOVED, "client_moved", 3);
        });
        // looking east, walking north, then west, then south: strafing in every direction, the mob keeps facing east
        r.Go(m, 0, 127, T2Room.Yaw(EAST), 9);
        r.Go(m, -127, 0, T2Room.Yaw(EAST), 9);
        r.Go(m, 0, -127, T2Room.Yaw(EAST), 9);
        Assert.True(r.Trace.Count(l => l.StartsWith("pre_move", StringComparison.Ordinal)) >= 6);
        Assert.Equal(0, r.Trace.Count(l => l.StartsWith("dir", StringComparison.Ordinal)));
        Assert.Equal(EAST, r.In(() => m.dir));
        // client/Move's tail (mob_movement.dm:141): COMSIG_MOB_CLIENT_MOVED with the direction of the move, the mob's dir before
        // it (east all along: tg's old_dir) and the loc it left
        string[] tails = r.Trace.Where(l => l.StartsWith("client_moved", StringComparison.Ordinal)).ToArray();
        Assert.Equal("client_moved mover 1 4 (5,5)", tails[0]);                 // north, facing east, from (5,5)
        Assert.All(tails.Where(t => t.Contains(" 1 ")), t => Assert.StartsWith("client_moved mover 1 4 (", t));
        Assert.Contains(tails, t => t.StartsWith("client_moved mover 8 4 (", StringComparison.Ordinal)); // west
        Assert.Contains(tails, t => t.StartsWith("client_moved mover 2 4 (", StringComparison.Ordinal)); // south
        AssertNumber(SOUTH, r.In(() => m.last_move)); // the last crossing's direction, tg's last_move = direct
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ATurnAndACrossingInOneTickTurnFirstSoTheMoveSeesTheNewDir()
    {
        var r = new T2Room(504);
        mob_living_carbon_human m = r.Join(5, 5, "Alice", name: "mover");
        r.Go(m, 127, 0, T2Room.Yaw(EAST), 1); // east, 85 into the tile, not yet across
        r.In(() =>
        {
            On(m, COMSIG_ATOM_DIR_CHANGE, "dir", 2);
            On(m, COMSIG_MOVABLE_ATTEMPTED_MOVE, "attempted", 2);
        });
        r.Go(m, 127, 0, T2Room.Yaw(NORTH), 1); // the view swings north in the very tick the east border is crossed
        AssertTrace(new[] { "dir mover 4 1", "attempted mover (6,5) 4" }, r.Trace);
        Assert.Equal(NORTH, r.In(() => m.dir));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AMobThatNeverGotAFrameKeepsTheDirTgGaveIt()
    {
        var r = new T2Room(505);
        mob_living_carbon_human dummy = r.Dummy(9, 9, name: "dummy");
        r.Step(5);
        Assert.Equal(SOUTH, r.In(() => dummy.dir));
        // tg turns it (a step with update_dir, a setDir): the bridge does not turn it back to a yaw it never had
        r.In(() => Waiting.NoSleep(dummy.setDir(NORTH)));
        r.Step(5);
        Assert.Equal(NORTH, r.In(() => dummy.dir));
        r.Command(() => Waiting.NoSleep(dummy.Move(locate(10, 9, 1), EAST)));
        r.Step(5);
        Assert.Equal(EAST, r.In(() => dummy.dir)); // tg's own Move turns a mob (update_dir default TRUE)
        r.AssertOnLoc(dummy);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void LookingContinuesWhileASlideRunsAndTheYawIsTheStatesAlways()
    {
        var r = new T2Room(506);
        mob_living_carbon_human m = r.Join(8, 8, "Alice", name: "mover");
        r.Go(m, 0, 0, T2Room.Yaw(SOUTH), 2);
        r.Command(() => Waiting.NoSleep(m.Move(locate(9, 8, 1), EAST)));
        r.Go(m, 127, 0, T2Room.Yaw(NORTH), 1); // the slide starts this tick; the view swings north while it plays
        Assert.True(r.State(m).Slide.Active);
        Assert.Equal(T2Room.Yaw(NORTH), r.State(m).Yaw); // looking is not moving
        Assert.Equal(NORTH, r.In(() => m.dir));
        r.AssertNoRuntimes();
    }
}
