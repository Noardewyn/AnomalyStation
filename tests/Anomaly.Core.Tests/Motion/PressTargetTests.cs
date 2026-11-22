using Anomaly.Core.Motion;
using static Anomaly.Core.Tests.Motion.MotionTest;

namespace Anomaly.Core.Tests.Motion;

/// <summary>
/// M2 integration (found by the headless prediction run against the real server): a press is a tg Move into the tile ahead
/// (D105), so it must never target an open tile. The north/south sweep runs first: a capsule sliding east along a wall's
/// corner found the old tile's north face blocked, then crossed into a tile whose north is open, and the press went to that
/// open tile, where tg's Move succeeded and the mob jumped a tile ahead of its capsule.
/// </summary>
public class PressTargetTests
{
    [Fact]
    public void APressNeverTargetsAnOpenTileWhenTheCapsuleSlidesPastAWallCorner()
    {
        var view = new GridView(12, 12).Solid(4, 8).Solid(3, 8).Solid(2, 8); // a wall north of x 2..4, open north of x 5
        int presses = 0, runs = 0;
        // Every start along the wall and several headings: whichever tick the press timer runs out on, its target is solid.
        for (int startX = -120; startX <= 120; startX += 12)
        {
            foreach (sbyte moveX in new sbyte[] { 40, 70, 100, 127 })
            {
                MotionState s = MotionState.AtTile(4, 7, 1);
                s.X += startX;
                runs++;
                for (int tick = 0; tick < 40; tick++)
                {
                    foreach (MotionEvent e in Step(ref s, Frame(moveX, 127), Run, view)) // north-east, steep enough to press north
                    {
                        if (e.Kind != MotionEventKind.Pressing)
                            continue;
                        presses++;
                        Assert.True(view.IsSolid(e.ToX, e.ToY, 1), $"start {startX}, move {moveX}, tick {tick}: a press from ({e.FromX},{e.FromY}) into the open tile ({e.ToX},{e.ToY})");
                    }
                }
                Assert.True(s.TileY >= 8, "it went north once past the corner");
            }
        }
        Assert.True(presses >= runs, "the capsules pressed the wall while they slid along it");
    }
}
