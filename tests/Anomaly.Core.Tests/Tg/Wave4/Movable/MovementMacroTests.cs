using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.Tg.Wave3.Engine;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using static Anomaly.Core.Tests.Tg.Wave4.Movable.Chain;

namespace Anomaly.Core.Tests.Tg.Wave4.Movable;

/// <summary>
/// The macros of package A's define files: __DEFINES/movement.dm (glide size arithmetic, CHECK_MOVE_LOOP_FLAGS, its
/// constants), __DEFINES/movement_info.dm (the active movement indices), __DEFINES/directional.dm and the two macros of
/// __DEFINES/spatial_gridmap.dm. Expected values are worked out by hand from the DM text of each macro.
/// </summary>
public class MovementMacroTests
{
    private static EngineWorld Room()
    {
        var map = MovableWorld.Create();
        using (map.World.Enter())
        {
            GLOB.gvars_datum_init_order = Dm.list();
            GLOB.InitGlobalglide_size_multiplier();
        }
        return map;
    }

    // ---- glide size ----

    [Theory]
    [InlineData(0.25f, 32f)]   // 32 / max(0.5, 1) = 32
    [InlineData(0.5f, 32f)]    // 32 / max(1, 1) = 32
    [InlineData(1f, 16f)]      // 32 / max(2, 1)
    [InlineData(2f, 8f)]       // 32 / 4
    [InlineData(3f, 5.3333335f)] // 32 / 6
    [InlineData(10f, 1.6f)]    // 32 / 20
    [InlineData(32f, 1f)]      // 0.5 clamped up to MIN_GLIDE_SIZE
    [InlineData(1000f, 1f)]
    public void DelayToGlideSizeDividesTheIconSizeByTheDelayInTicksAndClampsIt(float delay, float expected)
    {
        var map = Room();
        using (map.World.Enter())
            Assert.Equal(expected, DELAY_TO_GLIDE_SIZE(delay), 4);
    }

    [Fact]
    public void TheGlideSizeMultiplierScalesBothMacrosAndDelayToGlideSizeClampsAfterScaling()
    {
        var map = Room();
        using (map.World.Enter())
        {
            Assert.Equal(1f, GLOB.glide_size_multiplier); // GLOBAL_VAR_INIT(glide_size_multiplier, 1.0)
            GLOB.glide_size_multiplier = 1.25f;
            Assert.Equal(20f, DELAY_TO_GLIDE_SIZE(1), 4);       // 32 / 2 * 1.25
            Assert.Equal(32f, DELAY_TO_GLIDE_SIZE(0.5f), 4);    // 40 clamped down to MAX_GLIDE_SIZE
            Assert.Equal(10f, MOVEMENT_ADJUSTED_GLIDE_SIZE(2, 1), 4); // 32 / 4 * 1 * 1.25
        }
    }

    [Theory]
    [InlineData(2f, 1f, 8f)]    // 32 / (2 / 0.5) * 1
    [InlineData(1f, 2f, 32f)]   // 32 / 2 * 2: no clamping
    [InlineData(0.5f, 3f, 96f)] // 32 / 1 * 3
    [InlineData(4f, 0.5f, 2f)]  // 32 / 8 * 0.5
    public void MovementAdjustedGlideSizeIsUnclampedAndTakesAnUnrelatedScalar(float delay, float disparity, float expected)
    {
        var map = Room();
        using (map.World.Enter())
            Assert.Equal(expected, MOVEMENT_ADJUSTED_GLIDE_SIZE(delay, disparity), 4);
    }

    [Fact]
    public void CheckMoveLoopFlagsMasksTheMovePacketsFlagsAndIsNoneWithoutAPacket()
    {
        var map = Room();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            Assert.Equal((float)NONE, CHECK_MOVE_LOOP_FLAGS(mover, MOVEMENT_LOOP_OUTSIDE_CONTROL));

            Dm.@new<datum_movement_packet>(mover); // movement_packet/New(parent) sets mover.move_packet (move_manager.dm:66)
            mover.move_packet.processing_move_loop_flags = MOVEMENT_LOOP_OUTSIDE_CONTROL | MOVEMENT_LOOP_NO_DIR_UPDATE;
            Assert.Equal((float)MOVEMENT_LOOP_OUTSIDE_CONTROL, CHECK_MOVE_LOOP_FLAGS(mover, MOVEMENT_LOOP_OUTSIDE_CONTROL | MOVEMENT_LOOP_IGNORE_GLIDE));
            Assert.Equal(48f, CHECK_MOVE_LOOP_FLAGS(mover, 255));
            Assert.Equal(0f, CHECK_MOVE_LOOP_FLAGS(mover, MOVEMENT_LOOP_START_INSTANT));
        }
    }

    [Fact]
    public void TheMovementConstantsHaveTgsValues()
    {
        Assert.Equal(new[] { 1, 2, 4, 8, 16, 32 }, new[] { MOVEMENT_LOOP_START_INSTANT, MOVEMENT_LOOP_START_FAST, MOVEMENT_LOOP_IGNORE_PRIORITY, MOVEMENT_LOOP_IGNORE_GLIDE, MOVEMENT_LOOP_NO_DIR_UPDATE, MOVEMENT_LOOP_OUTSIDE_CONTROL });
        Assert.Equal(new[] { 1, 2, 4 }, new[] { MOVELOOP_STATUS_PAUSED, MOVELOOP_STATUS_RUNNING, MOVELOOP_STATUS_QUEUED });
        Assert.Equal(new[] { 10, 100, 101 }, new[] { MOVEMENT_DEFAULT_PRIORITY, MOVEMENT_SPACE_PRIORITY, MOVEMENT_ABOVE_SPACE_PRIORITY });
        Assert.Equal(new[] { 1, 2 }, new[] { MOVEMENT_BUCKET_TIME, MOVEMENT_BUCKET_LIST });
        Assert.Equal(new[] { 1, 2, 3, 4 }, new[] { CURRENTLY_Z_FALLING, CURRENTLY_Z_MOVING_GENERIC, CURRENTLY_Z_FALLING_FROM_MOVE, CURRENTLY_Z_ASCENDING });
        Assert.Equal(new[] { 1, 2 }, new[] { FIRST_DIAG_STEP, SECOND_DIAG_STEP });
        Assert.Equal(new[] { 1, 2, 3, 4 }, new[] { ACTIVE_MOVEMENT_OLDLOC, ACTIVE_MOVEMENT_DIRECTION, ACTIVE_MOVEMENT_FORCED, ACTIVE_MOVEMENT_OLDLOCS });
        Assert.Equal(1, MIN_GLIDE_SIZE);
        Assert.Equal(32, MAX_GLIDE_SIZE);
        // the z-move flags are single bits, and the groups are their unions (movement.dm: ZMOVE_CHECK_PULLS, the flight, stairs
        // and fall flag sets)
        Assert.Equal(ZMOVE_CHECK_PULLING | ZMOVE_CHECK_PULLEDBY, ZMOVE_CHECK_PULLS);
        Assert.Equal(ZMOVE_CHECK_PULLS | ZMOVE_CAN_FLY_CHECKS | ZMOVE_INCAPACITATED_CHECKS | ZMOVE_ALLOW_BUCKLED | ZMOVE_INCLUDE_PULLED, ZMOVE_FLIGHT_FLAGS);
        Assert.Equal(ZMOVE_ALLOW_BUCKLED | ZMOVE_CHECK_PULLEDBY, ZMOVE_STAIRS_FLAGS);
        Assert.Equal(ZMOVE_FALL_CHECKS | ZMOVE_ALLOW_BUCKLED, ZMOVE_FALL_FLAGS);
    }

    // ---- directions ----

    [Theory]
    [InlineData(NORTH, 0)]
    [InlineData(SOUTH, 0)]
    [InlineData(EAST, 0)]
    [InlineData(WEST, 0)]
    [InlineData(NORTHEAST, 4)] // 5 & 4
    [InlineData(NORTHWEST, 8)] // 9 & 8
    [InlineData(SOUTHEAST, 4)] // 6 & 5
    [InlineData(SOUTHWEST, 8)] // 10 & 9
    [InlineData(0, 0)]
    public void IsDiagonalDirIsZeroForACardinalOrNoDirection(int dir, int expected)
    {
        Assert.Equal((float)expected, ISDIAGONALDIR(dir));
    }

    [Fact]
    public void TheDirectionComponentsAndFlipsWorkOnEveryDirection()
    {
        Assert.Equal((float)NORTH, NSCOMPONENT(NORTHEAST));
        Assert.Equal((float)SOUTH, NSCOMPONENT(SOUTHWEST));
        Assert.Equal(0f, NSCOMPONENT(EAST));
        Assert.Equal((float)EAST, EWCOMPONENT(SOUTHEAST));
        Assert.Equal((float)WEST, EWCOMPONENT(NORTHWEST));
        Assert.Equal(0f, EWCOMPONENT(NORTH));

        Assert.Equal((float)SOUTHEAST, NSDIRFLIP(NORTHEAST));
        Assert.Equal((float)NORTHWEST, NSDIRFLIP(SOUTHWEST));
        Assert.Equal((float)SOUTH, NSDIRFLIP(NORTH));
        Assert.Equal(7f, NSDIRFLIP(EAST)); // d ^ (NORTH | SOUTH): a dir without them gains both
        Assert.Equal((float)NORTHWEST, EWDIRFLIP(NORTHEAST));
        Assert.Equal((float)SOUTHEAST, EWDIRFLIP(SOUTHWEST));
        Assert.Equal((float)WEST, EWDIRFLIP(EAST));
    }

    [Theory]
    [InlineData(NORTH, SOUTH)]
    [InlineData(SOUTH, NORTH)]
    [InlineData(EAST, WEST)]
    [InlineData(WEST, EAST)]
    [InlineData(NORTHEAST, SOUTHWEST)]
    [InlineData(NORTHWEST, SOUTHEAST)]
    [InlineData(SOUTHEAST, NORTHWEST)]
    [InlineData(SOUTHWEST, NORTHEAST)]
    [InlineData(UP, DOWN)]
    [InlineData(DOWN, UP)]
    [InlineData(17, 34)] // UP | NORTH, DOWN | SOUTH
    [InlineData(0, 0)]
    public void ReverseDirIsTheOppositeDirectionIncludingUpAndDown(int dir, int expected)
    {
        Assert.Equal((float)expected, REVERSE_DIR(dir));
        Assert.Equal((float)dir, REVERSE_DIR(REVERSE_DIR(dir)));
    }

    // ---- the spatial grid macros ----

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(17, 1)]
    [InlineData(18, 2)]
    [InlineData(34, 2)]
    [InlineData(35, 3)]
    [InlineData(255, 15)]
    [InlineData(256, 16)]
    public void GetSpatialIndexIsTheCellOfACoordinateRoundedUp(int coord, int expected)
    {
        Assert.Equal((float)expected, GET_SPATIAL_INDEX(coord));
    }

    [Fact]
    public void HasSpatialGridContentsReadsTheSpatialGridKey()
    {
        var map = Room();
        using (map.World.Enter())
        {
            var mover = Dm.@new<obj_test_movable_thing>(locate(1, 1, 1));
            Assert.Null(HAS_SPATIAL_GRID_CONTENTS(mover));
            mover.spatial_grid_key = "hearing";
            Assert.Equal("hearing", HAS_SPATIAL_GRID_CONTENTS(mover));
        }
    }
}
