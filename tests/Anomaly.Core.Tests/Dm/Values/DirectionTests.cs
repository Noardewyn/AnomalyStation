using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;

namespace Anomaly.Core.Tests.DmValues;

/// <summary>
/// <c>turn()</c> of a direction (BYOND ref <c>/proc/turn</c>; OpenDream <c>DreamProcNativeRoot.NativeProc_turn</c>;
/// docs/ai/translation-rules.md Appendix C). Expected values come from the reference's text and example, never from
/// running the port.
/// </summary>
public class DirectionTests
{
    [Fact]
    public void TurnRotatesCounterclockwiseAsTheReferenceExampleShows()
    {
        // BYOND ref: dir = turn(NORTH, 90) // dir = west; dir = turn(dir, -90) // dir = north;
        // dir = turn(dir, 45) // dir = northwest.
        float dir = turn(NORTH, 90);
        Assert.Equal(WEST, dir);
        dir = turn(dir, -90);
        Assert.Equal(NORTH, dir);
        dir = turn(dir, 45);
        Assert.Equal(NORTHWEST, dir);
    }

    [Fact]
    public void EveryDirectionTurnsAroundTheCompass()
    {
        // "An angle in degrees (counterclockwise rotation)": EAST, NORTHEAST, NORTH, ... 45 degrees apart.
        float[] ring = { EAST, NORTHEAST, NORTH, NORTHWEST, WEST, SOUTHWEST, SOUTH, SOUTHEAST };
        for (int i = 0; i < 8; i++)
        {
            Assert.Equal(ring[i], turn(ring[i], 0));
            Assert.Equal(ring[(i + 1) % 8], turn(ring[i], 45));
            Assert.Equal(ring[(i + 2) % 8], turn(ring[i], 90));
            Assert.Equal(ring[(i + 4) % 8], turn(ring[i], 180));
            Assert.Equal(ring[(i + 6) % 8], turn(ring[i], -90));
            Assert.Equal(ring[(i + 7) % 8], turn(ring[i], -45));
            Assert.Equal(ring[i], turn(ring[i], 360));
            Assert.Equal(ring[i], turn(ring[i], -720));
            Assert.Equal(ring[(i + 3) % 8], turn(ring[i], 495));                    // 360 + 135
        }
        Assert.Equal(SOUTH, turn(NORTH, 180));
        Assert.Equal(SOUTHEAST, turn(EAST, -45));
    }

    [Fact]
    public void AnAngleThatIsNoMultipleOf45CountsAsTheClosestMultipleTowardZero()
    {
        // "If an invalid angle is used, it will be treated as the closest multiple of 45 to 0."
        Assert.Equal(NORTHWEST, turn(NORTH, 50));
        Assert.Equal(NORTH, turn(NORTH, 44.9f));
        Assert.Equal(NORTHEAST, turn(NORTH, -50));
        Assert.Equal(EAST, turn(NORTH, -134));          // -90
        Assert.Equal(WEST, turn(NORTH, 134.5f));        // 90
        Assert.Equal(WEST, turn(NORTH, 90 + 360 * 1000000f)); // a huge angle still turns by a multiple of 45
    }

    [Fact]
    public void AnInvalidDirIsARandomDirectionUnlessTheAngleIsZero()
    {
        // "If the supplied Dir is invalid, such as 0, or something like UP or DOWN, the result is a random direction
        // unless the angle is also 0." With an angle of 0 the port gives 0, as OpenDream does.
        var w = new World(3);
        using (w.Enter())
        {
            Assert.Equal(0f, turn(0, 0));
            Assert.Equal(0f, turn(UP, 0));
            Assert.Equal(0f, turn(DOWN, 0));
            float[] dirs = { NORTH, SOUTH, EAST, WEST, NORTHEAST, SOUTHEAST, SOUTHWEST, NORTHWEST };
            var seen = new HashSet<float>();
            for (int i = 0; i < 200; i++)
            {
                float d = turn(i % 2 == 0 ? 0 : UP, 90);
                Assert.Contains(d, dirs);
                seen.Add(d);
            }
            Assert.Equal(8, seen.Count);                // every direction comes up
            Assert.Contains(turn(NORTH + SOUTH, 45), dirs); // 3 is not a direction
        }
    }

    [Fact]
    public void AnInvalidDirDrawsFromTheWorldRng()
    {
        // Rules 4.13: the draw comes from World.Rng, so the same seed and tick give the same directions.
        float[] Draw(ulong seed)
        {
            var w = new World(seed);
            using (w.Enter())
                return Enumerable.Range(0, 16).Select(_ => turn(0, 45)).ToArray();
        }
        Assert.Equal(Draw(11), Draw(11));
    }

    [Fact]
    public void TurnOfAnyValueTakesNumbersAndTurnsACopyOfAMatrix()
    {
        Assert.Equal(WEST, turn((object)NORTH, 90));
        Assert.Equal(WEST, turn((object)1, 90));       // an int box is the same number
        Assert.Equal(NORTH, turn(1.5f, 0));             // Dir is truncated to an integer
        Assert.Equal(-1f, ((matrix)turn(matrix(), 90)).d, 5); // M4v slice 4: a turned copy (AnimationSiteTests.TurnOfAMatrixIsATurnedCopy)
        Assert.Throws<DmRuntimeException>(() => turn((object)"north", 90));
        Assert.Throws<DmRuntimeException>(() => turn((object)null, 90));
    }
}
