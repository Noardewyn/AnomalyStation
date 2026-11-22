using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.Tg.Wave3.Engine;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.Tg.Wave4.Contracts;

/// <summary>
/// The lines of /atom/proc/CanPass, CanAllowThrough and has_gravity that waited for the wave-4 vars (W4.0, plan D8,
/// scope section 5), written from tg's code (game/atom/_atom.dm:225-250, :778-800), and the preloader's
/// bullet_bounce_sound (preloader.dm:42).
/// </summary>
public class AtomGapEditTests
{
    [Fact]
    public void APhasingMoverPassesAnything()
    {
        var w = new World();
        using (w.Enter())
        {
            var block = Dm.@new<obj_test_w40_block>();
            var mover = Dm.@new<obj_test_w40_mover>();
            Assert.True(Dm.eq(Waiting.NoSleep(block.CanPass(mover, NORTH)), FALSE)); // dense: !density
            mover.movement_type = PHASING;
            Assert.True(Dm.eq(Waiting.NoSleep(block.CanPass(mover, NORTH)), TRUE));
        }
    }

    [Fact]
    public void PassFlagsMatchingPassFlagsSelfOrAThrownMoverOverLetPassThrowPass()
    {
        var w = new World();
        using (w.Enter())
        {
            var block = Dm.@new<obj_test_w40_block>();
            var mover = Dm.@new<obj_test_w40_mover>();
            block.pass_flags_self = PASSTABLE;
            mover.pass_flags = PASSTABLE;
            Assert.True(Dm.eq(Waiting.NoSleep(block.CanAllowThrough(mover, NORTH)), TRUE));
            mover.pass_flags = NONE;
            block.pass_flags_self = LETPASSTHROW;
            Assert.True(Dm.eq(Waiting.NoSleep(block.CanAllowThrough(mover, NORTH)), FALSE)); // not thrown
            mover.throwing = Dm.@new<datum_thrownthing>();
            Assert.True(Dm.eq(Waiting.NoSleep(block.CanAllowThrough(mover, NORTH)), TRUE));
        }
    }

    [Fact]
    public void ANonGenericMoverDecidesThroughCanPassThroughWithTheReversedDirection()
    {
        var map = new EngineWorld(1, 1, 1);
        using (map.World.Enter())
        {
            var block = Dm.@new<obj_test_w40_block>();
            var mover = Dm.@new<obj_test_w40_mover>();
            map.Trace.Clear();
            Assert.True(Dm.eq(Waiting.NoSleep(block.CanPass(mover, NORTH)), FALSE)); // generic_canpass TRUE: CanAllowThrough
            Assert.Empty(map.Trace);
            mover.generic_canpass = FALSE;
            object result = Waiting.NoSleep(block.CanPass(mover, NORTHEAST));
            Assert.True(Dm.eq(result, 7));
            Assert.Equal(new[] { "CanPassThrough " + EngineTrace.D(block) + " dir=" + EngineTrace.D(SOUTHWEST) + " opinion=" + EngineTrace.D(FALSE) }, map.Trace);
        }
    }

    // A map of one turf in the given area type, with SSmapping's gravity list grown as tg's manage_z_level does
    // (controllers/subsystem/mapping.dm:756); its z level's gravity is the given value.
    private static (EngineWorld map, obj_test_engine_box box) OnTurf(Type areaType, Type turfType = null, object zGravity = null)
    {
        var map = new EngineWorld(1, 1, 1, turfType, areaType);
        obj_test_engine_box box;
        using (map.World.Enter())
        {
            Dm.@new<datum_controller_subsystem_mapping>(); // NEW_SS_GLOBAL(SSmapping)
            SSmapping.gravity_by_z_level.len = 1;
            ToyRoom.LinkLevels(1);
            SSmapping.gravity_by_z_level[1f] = zGravity;
            box = Dm.@new<obj_test_engine_box>(locate(1, 1, 1));
        }
        return (map, box);
    }

    [Fact]
    public void GravityComesFromTheAreaUnlessTheZLevelGivesSome()
    {
        var (room, inRoom) = OnTurf(typeof(area_misc_testroom_test_w40_room));
        using (room.World.Enter())
            Assert.True(Dm.eq(inRoom.has_gravity(), STANDARD_GRAVITY));

        var (lit, inLit) = OnTurf(typeof(area_misc_testroom_test_w40_room), zGravity: 2);
        using (lit.World.Enter())
            Assert.True(Dm.eq(inLit.has_gravity(), 2)); // gravity_by_z_level[z] || default_gravity
    }

    [Fact]
    public void NoGravityAreasAndTurfsGiveNoGravity()
    {
        var (space, inSpace) = OnTurf(typeof(area_space_test_w40_space));
        using (space.World.Enter())
            Assert.True(Dm.eq(inSpace.has_gravity(), FALSE)); // area_flags NO_GRAVITY (misc.dm:13)

        var (nograv, onNograv) = OnTurf(typeof(area_misc_testroom_test_w40_room), typeof(turf_test_w40_nograv), zGravity: 1);
        using (nograv.World.Enter())
            Assert.True(Dm.eq(onNograv.has_gravity(), FALSE)); // force_no_gravity (turf.dm:88)

        var w = new World();
        using (w.Enter())
            Assert.True(Dm.eq(Dm.@new<obj_test_w40_block>().has_gravity(), FALSE)); //no gravity in nullspace
    }

    [Fact]
    public void ThePreloadersNoopTurfHasNoBulletBounceSound()
    {
        Assert.NotNull(new turf_open_indestructible().bullet_bounce_sound); // turf.dm:51
        Assert.Null(new turf_template_noop().bullet_bounce_sound);           // preloader.dm:42
    }
}
