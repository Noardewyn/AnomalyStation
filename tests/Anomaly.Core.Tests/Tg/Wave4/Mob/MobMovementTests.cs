using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.Tg.Wave3.Engine;
using Anomaly.Core.Tests.Tg.Wave4.Movable;
using Anomaly.Core.Tg;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;
using TgMob = Anomaly.Core.Tg.mob;

namespace Anomaly.Core.Tests.Tg.Wave4.Mob;

/// <summary>
/// The mob_movement.dm procs M0 uses (the file is a partial; client/Move is the M1 client layer, decision D5):
/// Process_Spacemove (:264-296), has_gravity and mob_negates_gravity (:361-369), abstract_move and Moved (:582-591),
/// with real turfs of package B and the real Move chain of package A. A turf's force_no_gravity makes the no-gravity world.
/// </summary>
public class MobMovementTests
{
    private static T NewAt<T>(int x = 3, int y = 3) where T : atom => Dm.@new<T>(MobRoom.T(x, y));

    private static object Spacemove(TgMob m, float dir = EAST, float cont = FALSE) => Waiting.NoSleep(m.Process_Spacemove(dir, cont));

    private static mob_test_mob_backup NewDrifter()
    {
        MobRoom.T(3, 3).force_no_gravity = TRUE;
        return NewAt<mob_test_mob_backup>();
    }

    // ---- gravity ----

    [Fact]
    public void AMobOnAGravityTurfHasGravityAndCanSpacemoveWithoutLookingForABackup()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<mob_test_mob_backup>();
            Assert.True(Dm.truthy(m.has_gravity()));
            Assert.True(Dm.eq(Spacemove(m), TRUE));
            Assert.Empty(m.asked);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void HasGravityIsFalseOnATurfWithForceNoGravityAndInNullspace()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewDrifter();
            Assert.False(Dm.truthy(m.has_gravity()));
            Assert.False(Dm.truthy(m.has_gravity(MobRoom.T(3, 3))));
            Assert.True(Dm.truthy(m.has_gravity(MobRoom.T(4, 4)))); // an explicit turf argument is used instead of the mob's own
            var nowhere = Dm.@new<TgMob>();
            Assert.False(Dm.truthy(nowhere.has_gravity())); // "no gravity in nullspace"
        });
    }

    [Fact]
    public void AMobThatNegatesGravityHasItEverywhereAndReturnsThatValue()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            MobRoom.T(3, 3).force_no_gravity = TRUE;
            var plain = NewAt<TgMob>();
            Assert.True(Dm.eq(plain.mob_negates_gravity(), FALSE));
            var floater = NewAt<mob_test_mob_gravity_negator>();
            Assert.True(Dm.eq(floater.mob_negates_gravity(), TRUE));
            Assert.True(Dm.eq(floater.has_gravity(), TRUE)); // `mob_negates_gravity() || ..()`
            Assert.False(Dm.truthy(plain.has_gravity()));
            floater.negates = FALSE;
            Assert.False(Dm.truthy(floater.has_gravity())); // falls through to the turf's
        });
    }

    [Fact]
    public void AGravityHasGravitySignalCanForceGravityOnAMob()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewDrifter();
            Assert.False(Dm.truthy(m.has_gravity()));
            var forcer = Dm.@new<datum_test_mob_gravity_forcer>();
            forcer.RegisterSignal(m, COMSIG_ATOM_HAS_GRAVITY, nameof(datum_test_mob_gravity_forcer.on_has_gravity));
            Assert.True(Dm.eq(m.has_gravity(), 1)); // max(forced) + min(min(forced), 0)
        });
    }

    // ---- Process_Spacemove without gravity ----

    [Fact]
    public void ASpacewalkerCanAlwaysMoveWithoutAskingForABackup()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewDrifter();
            ADD_TRAIT(m, TRAIT_SPACEWALK, "test");
            Assert.True(Dm.eq(Spacemove(m), TRUE));
            Assert.Empty(m.asked);
        });
    }

    [Fact]
    public void ABuckledMobCanMoveInSpace()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewDrifter();
            m.buckled = NewAt<obj>();
            Assert.True(Dm.eq(Spacemove(m), TRUE));
            Assert.Empty(m.asked);
        });
    }

    [Fact]
    public void AFlyingMobAndAFreeFloatingOneCanMoveInSpace()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var flier = NewDrifter();
            flier.movement_type = FLYING;
            Assert.True(Dm.eq(Spacemove(flier), TRUE));
            var floater = NewAt<mob_test_mob_backup>();
            ADD_TRAIT(floater, TRAIT_FREE_FLOAT_MOVEMENT, "test");
            Assert.True(Dm.eq(Spacemove(floater), TRUE));
            Assert.Empty(flier.asked);
            Assert.Empty(floater.asked);
        });
    }

    [Fact]
    public void ATraitNogravAlwaysDriftCannotMoveEvenWithABackup()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewDrifter();
            m.backup = NewAt<obj>(); // something to push off
            ADD_TRAIT(m, TRAIT_NOGRAV_ALWAYS_DRIFT, "test");
            Assert.True(Dm.eq(Spacemove(m), FALSE));
            Assert.Empty(m.asked);
        });
    }

    [Fact]
    public void WithoutABackupAMobInSpaceCannotMove()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewDrifter();
            Assert.True(Dm.eq(Spacemove(m, EAST, TRUE), FALSE));
            Assert.Equal(new[] { Dm.str(EAST) + "/" + Dm.str(TRUE) }, m.asked); // moving_direction, continuous_move
        });
    }

    [Fact]
    public void TheBackupSearchDecidesTheSpacemoveOfAMobInZeroGravity()
    {
        // get_spacemove_backup (translated in M4b slice 1): a mob in zero gravity stops only with something to push off
        var room = new MobRoom();
        room.In(() =>
        {
            MobRoom.T(3, 3).force_no_gravity = TRUE;
            var m = NewAt<TgMob>();
            object backup = Waiting.NoSleep(m.get_spacemove_backup(0, FALSE));
            Assert.Equal(backup != null, Dm.truthy(Spacemove(m)));
        });
    }

    [Fact]
    public void ABackupAllowsTheMoveAndAListenerCanStopTheHalt()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewDrifter();
            var wall = NewAt<obj_test_mob_pusher>(4, 3);
            m.backup = wall;
            // a signal returning COMPONENT_PREVENT_SPACEMOVE_HALT makes the mob drift on instead (:285)
            Chain.On(m, COMSIG_MOB_ATTEMPT_HALT_SPACEMOVE, "halt", result: COMPONENT_PREVENT_SPACEMOVE_HALT);
            Assert.True(Dm.eq(Spacemove(m, EAST), FALSE));
            Assert.Equal(new[] { "halt " + EngineTrace.D(m) + " " + Dm.str(EAST) + " " + Dm.str(FALSE) + " " + EngineTrace.D(wall) }, EngineTrace.Of(room.World).Lines);
            Assert.Empty(wall.pushed); // returned before the push off
        });
    }

    [Theory]
    [InlineData(true, false, false)] // continuous_move
    [InlineData(false, false, true)] // no direction
    [InlineData(false, true, false)] // an anchored backup
    public void AMoveThatDoesNotPushOffTakesTheBackupAsEnoughAndPushesNothing(bool continuous, bool anchored, bool noDirection)
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewDrifter();
            var wall = NewAt<obj_test_mob_pusher>(4, 3);
            wall.anchored = anchored ? TRUE : FALSE;
            m.backup = wall;
            Assert.True(Dm.eq(Spacemove(m, noDirection ? 0 : EAST, continuous ? TRUE : FALSE), TRUE)); // `continuous_move || !istype(backup) || !movement_dir || backup.anchored`
            Assert.Empty(wall.pushed);
            Assert.Null(m.last_pushoff);
        });
    }

    [Fact]
    public void ATurfBackupCannotBePushedOffSoItIsEnoughAndNothingMoves()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewDrifter();
            m.backup = MobRoom.T(4, 3);
            Assert.True(Dm.eq(Spacemove(m, EAST), TRUE)); // `!istype(backup)`: not an /atom/movable
            Assert.Null(m.last_pushoff);
        });
    }

    [Fact]
    public void PushingOffAMovableMovesItTheOtherWayAndStampsBothWithTheTime()
    {
        var room = new MobRoom();
        room.Tick(3); // some world time
        room.In(() =>
        {
            var m = NewDrifter();
            var crate = NewAt<obj_test_mob_pusher>(4, 3);
            m.backup = crate;
            crate.newtonian_result = TRUE;
            Assert.True(Dm.eq(Spacemove(m, EAST), TRUE));
            // newtonian_move(dir2angle(REVERSE_DIR(EAST)), instant = TRUE): the angle of WEST, 270 degrees
            Assert.Equal(new[] { "270 instant=1" }, crate.pushed);
            Assert.Equal(room.World.Time, Dm.num(m.last_pushoff)); // last_pushoff = world.time before the push (:291)
            Assert.Equal(room.World.Time, Dm.num(crate.last_pushoff)); // and the backup's, since it moved
            Assert.True(room.World.Time > 0);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AMovableThatDidNotMoveWhenPushedIsNotStamped()
    {
        var room = new MobRoom();
        room.Tick(3);
        room.In(() =>
        {
            var m = NewDrifter();
            var crate = NewAt<obj_test_mob_pusher>(4, 3);
            m.backup = crate;
            crate.newtonian_result = FALSE;
            Assert.True(Dm.eq(Spacemove(m, WEST), TRUE));
            Assert.Equal(new[] { "90 instant=1" }, crate.pushed); // REVERSE_DIR(WEST) is EAST: 90 degrees
            Assert.Equal(room.World.Time, Dm.num(m.last_pushoff));
            Assert.Null(crate.last_pushoff);
        });
    }

    [Fact]
    public void TheDiagonalPushOffUsesTheOppositeDiagonal()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewDrifter();
            var crate = NewAt<obj_test_mob_pusher>(4, 4);
            m.backup = crate;
            Assert.True(Dm.eq(Spacemove(m, NORTHEAST), TRUE));
            Assert.Equal(new[] { "225 instant=1" }, crate.pushed); // REVERSE_DIR(NORTHEAST) = SOUTHWEST
        });
    }

    // ---- abstract_move and Moved ----

    [Fact]
    public void AbstractMoveToNullspaceTakesTheMobOffTheMap()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            Waiting.NoSleep(m.abstract_move(null)); // no turf: nothing secret to refuse (:583-585)
            Assert.Null(m.loc);
            Assert.Equal(0f, MobRoom.T(3, 3).contents.len);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AbstractMoveOfAMobWithoutAnAdminClientIsRefusedOntoASecretCordon()
    {
        // `new_turf && (istype(new_turf, /turf/cordon/secret) || is_secret_level(new_turf.z)) && !client?.holder` -> return (:583-585)
        var room = new MobRoom();
        room.In(() =>
        {
            var cordon = Dm.@new<turf_cordon_secret>(MobRoom.T(6, 6));
            var m = NewAt<TgMob>();
            Waiting.NoSleep(m.abstract_move(cordon));
            Assert.Same(MobRoom.T(3, 3), m.loc); // refused: the mob stays where it was
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AbstractMoveOntoAnOrdinaryTurfMovesAMobWhileTheLevelTraitsAreDeferredUntilM3()
    {
        // is_secret_level is SSmapping.level_trait(z, ZTRAIT_SECRET), whose look-up is a deferred leaf until the map milestone
        // (M1 integration: no z-level has a trait, so no level is secret)
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            Waiting.NoSleep(m.abstract_move(MobRoom.T(4, 4)));
            Assert.Same(MobRoom.T(4, 4), m.loc);
            Assert.Contains("code/modules/mapping/space_management/traits.dm:5", DmWorldState.Of(World.Current).DeferredSites);
            var o = NewAt<obj>();
            Waiting.NoSleep(o.abstract_move(MobRoom.T(4, 4))); // an obj has no such check
            Assert.Same(MobRoom.T(4, 4), o.loc);
        });
    }

    [Fact]
    public void AMobStepsToTheNextTurfAndMobMovedRunsForEveryMove()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<mob_test_mob_mover>(3, 3);
            Assert.True(Dm.truthy(Waiting.NoSleep(step(m, EAST))));
            Assert.Same(MobRoom.T(4, 3), m.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(step(m, NORTH))));
            Assert.Same(MobRoom.T(4, 4), m.loc);
            Assert.Equal(new[] { Dm.str(EAST), Dm.str(NORTH) }, m.moves.ToArray());
            Assert.Equal(1f, MobRoom.T(4, 4).contents.len);
        });
        Assert.Empty(room.RuntimeText);
    }

    [Fact]
    public void AMobWithAClientAndSoundTokensQueuesItsClientWhenItMoves()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            m.client = Dm.@new<client_test_m1_client>();
            m.sound_tokens = Dm.list(1);
            Waiting.NoSleep(step(m, EAST));
            Assert.True(Dm.truthy(SSsound_tokens.clients_needing_update[m.client])); // mob.Moved (mob_movement.dm:588-591)
            m.client = null;
        });
    }

    [Fact]
    public void AMobWithoutSoundTokensDoesNotQueueItsClient()
    {
        var room = new MobRoom();
        room.In(() =>
        {
            var m = NewAt<TgMob>();
            m.client = Dm.@new<client_test_m1_client>();
            Waiting.NoSleep(step(m, EAST));
            Assert.Equal(0f, SSsound_tokens.clients_needing_update.len);
            m.client = null;
        });
    }

    [Fact]
    public void ALocalMobsMoveInASteppingStormLeavesTheTurfContentsConsistent()
    {
        // twenty mobs stepping around a 7x7 room: every mob is on exactly one turf and every turf lists its mobs
        var room = new MobRoom();
        room.In(() =>
        {
            var mobs = Enumerable.Range(0, 20).Select(i => NewAt<TgMob>(1 + i % 7, 1 + i / 7 % 7)).ToList();
            float[] dirs = { NORTH, EAST, SOUTH, WEST };
            for (int i = 0; i < 200; i++)
            {
                var m = mobs[i % mobs.Count];
                Waiting.NoSleep(step(m, dirs[i * 7 % 4]));
            }
            foreach (var m in mobs)
            {
                Assert.IsAssignableFrom<turf>(m.loc);
                Assert.True(Dm.@in(m, ((turf)m.loc).contents));
            }
            Assert.Equal(20, Enumerable.Range(1, 7).SelectMany(x => Enumerable.Range(1, 7).Select(y => (int)MobRoom.T(x, y).contents.len)).Sum());
        });
        Assert.Empty(room.RuntimeText);
    }
}

/// <summary>A mob whose get_spacemove_backup (mob_movement.dm:306, M2) answers what the test put in <see cref="backup"/>.</summary>
[DmPath("/mob/test_mob_backup")]
public class mob_test_mob_backup : TgMob
{
    [NotHashed] public object backup;
    [NotHashed] public readonly List<string> asked = new();

    [DmParams("moving_direction", "continuous_move", "include_floors")]
    public override async DmTask<object> get_spacemove_backup(params object[] __extra)
    {
        asked.Add(Dm.str(Dm.arg(__extra, 0)) + "/" + Dm.str(Dm.arg(__extra, 1)));
        return backup;
    }
}

/// <summary>A mob that negates gravity as long as <see cref="negates"/> is set (mob_movement.dm:367).</summary>
[DmPath("/mob/test_mob_gravity_negator")]
public class mob_test_mob_gravity_negator : TgMob
{
    [NotHashed] public object negates = TRUE;

    public override object mob_negates_gravity() => negates;
}

/// <summary>An obj that records the pushes it is given (a movable to push off of; its newtonian_move needs the drift handler of M4).</summary>
[DmPath("/obj/test_mob_pusher")]
public class obj_test_mob_pusher : obj
{
    [NotHashed] public readonly List<string> pushed = new();
    [NotHashed] public object newtonian_result = TRUE;

    public override async DmTask<object> newtonian_move(object inertia_angle = null, float? instant = null, float? start_delay = null, float? drift_force = null, object controlled_cap = null, float? force_loop = null)
    {
        pushed.Add(Dm.str(inertia_angle) + " instant=" + Dm.str(instant));
        return newtonian_result;
    }
}

/// <summary>Forces gravity of 1 through COMSIG_ATOM_HAS_GRAVITY (atom.dm:786: the handler appends to forced_gravity).</summary>
[DmPath("/datum/test_mob_gravity_forcer")]
public class datum_test_mob_gravity_forcer : datum
{
    [SignalHandler]
    public virtual void on_has_gravity(datum source = null, object gravity_turf = null, DmList forced_gravity = null)
    {
        Dm.append(ref forced_gravity, 1);
    }
}
