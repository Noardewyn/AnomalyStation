using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Core.Tests.Tg.Wave3.Engine;
using static Anomaly.Core.Tg.Builtins;

namespace Anomaly.Core.Tests.Tg.Wave4.Contracts;

/// <summary>
/// The hand-written builtin <c>step(Ref, Dir, Speed)</c> (W4.0, plan "step"; docs/ai/specs/move-chain.md E2; BYOND ref
/// <c>/proc/step</c>; OpenDream DMStandard/_Standard.dm:168-174 and its BYOND-parity test move.dm): Move is not called
/// at the map edge or with Dir 0, and step returns 0 there; otherwise it returns Move's own value.
/// </summary>
public class StepTests
{
    private static (EngineWorld map, obj_test_w40_mover mover) MoverAt(int x, int y)
    {
        var map = new EngineWorld(3, 3, 1);
        obj_test_w40_mover mover;
        using (map.World.Enter())
            mover = Dm.@new<obj_test_w40_mover>(locate(x, y, 1));
        map.Trace.Clear();
        return (map, mover);
    }

    [Fact]
    public void StepCallsMoveWithTheNextTurfAndTheDirectionAndReturnsItsValue()
    {
        var (map, mover) = MoverAt(2, 2);
        using (map.World.Enter())
        {
            turf east = locate(3, 2, 1);
            object result = Waiting.NoSleep(step(mover, Dm.EAST));
            Assert.Equal(new[] { "Move " + EngineTrace.D(east) + " dir=" + EngineTrace.D(Dm.EAST) }, map.Trace);
            Assert.True(Dm.eq(result, Dm.TRUE));
        }
    }

    [Fact]
    public void StepReturnsMovesValueUnconvertedEvenNullAndFalse()
    {
        var (map, mover) = MoverAt(2, 2);
        using (map.World.Enter())
        {
            mover.move_result = null; // tg's Move returns null when a diagonal step fails both ways (move-chain.md 3f)
            Assert.Null(Waiting.NoSleep(step(mover, Dm.NORTHEAST)));
            mover.move_result = Dm.FALSE;
            object result = Waiting.NoSleep(step(mover, Dm.SOUTH));
            Assert.True(Dm.eq(result, Dm.FALSE));
            Assert.Equal(2, map.Trace.Count);
        }
    }

    [Fact]
    public void StepOffTheMapEdgeOrWithDirZeroNeverCallsMoveAndReturnsZero()
    {
        var (map, mover) = MoverAt(3, 3);
        using (map.World.Enter())
        {
            Assert.True(Dm.eq(Waiting.NoSleep(step(mover, Dm.EAST)), 0));   // get_step is null at the edge
            Assert.True(Dm.eq(Waiting.NoSleep(step(mover, Dm.NORTH)), 0));
            Assert.True(Dm.eq(Waiting.NoSleep(step(mover, 0)), 0));         // get_step(Ref, 0) is Ref.loc
            Assert.Empty(map.Trace);
        }
    }

    [Fact]
    public void StepOfAMovableOffTheMapOrOfNullReturnsZero()
    {
        var map = new EngineWorld(2, 2, 1);
        using (map.World.Enter())
        {
            var nowhere = Dm.@new<obj_test_w40_mover>();
            map.Trace.Clear();
            Assert.True(Dm.eq(Waiting.NoSleep(step(nowhere, Dm.EAST)), 0));
            Assert.True(Dm.eq(Waiting.NoSleep(step(null, Dm.EAST)), 0));
            Assert.Empty(map.Trace);
        }
    }
}
