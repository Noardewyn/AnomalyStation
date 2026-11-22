using Anomaly.Client.Shared;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>M2 P8: the first-person camera layer (D41, D70): eye height, head bob and shake with off switches.</summary>
public class CameraLayerTests
{
    private static CameraOffsets Run(CameraLayer layer, double seconds, bool lying, float speed, bool walking)
    {
        CameraOffsets last = default;
        for (double t = 0; t < seconds; t += 1.0 / 120)
            last = layer.Update(1.0 / 120, lying, speed, walking);
        return last;
    }

    [Fact]
    public void StandingTheEyeIsAtTheStandingHeightAndStays()
    {
        var layer = new CameraLayer();
        CameraOffsets o = Run(layer, 1, false, 0, false);
        Assert.Equal(CameraLayer.StandingEye, o.Up, 4);
        Assert.Equal(0f, o.Right, 4);
        Assert.Equal(0f, o.RollRadians, 4);
    }

    [Fact]
    public void LyingTheEyeSinksToTheLyingEyeAndComesBackUp()
    {
        var layer = new CameraLayer();
        CameraOffsets down = Run(layer, 1, true, 0, false);
        Assert.Equal(CameraLayer.LyingEye, down.Up, 3);
        CameraOffsets mid = Run(layer, 0.2, false, 0, false);
        Assert.InRange(mid.Up, CameraLayer.LyingEye + 0.1f, CameraLayer.StandingEye - 0.05f);
        Assert.Equal(CameraLayer.StandingEye, Run(layer, 1, false, 0, false).Up, 3);
    }

    [Fact]
    public void WalkingBobsTheEyeByDistanceAndStandingStillDoesNot()
    {
        var layer = new CameraLayer();
        float min = 9, max = 0;
        for (double t = 0; t < 3; t += 1.0 / 120)
        {
            CameraOffsets o = layer.Update(1.0 / 120, false, 6.67f, true);
            if (t > 0.5)
            {
                min = Math.Min(min, o.Up);
                max = Math.Max(max, o.Up);
            }
        }
        Assert.InRange(max - min, 0.01f, 0.05f);
        // Stopped: the bob dies away.
        CameraOffsets still = Run(layer, 1, false, 0, false);
        Assert.Equal(CameraLayer.StandingEye, still.Up, 3);
    }

    [Fact]
    public void TheBobHasAnOffSwitchAndIsSmallEnoughNotToFlicker()
    {
        var off = new CameraLayer { HeadBob = false };
        float min = 9, max = 0;
        for (double t = 0; t < 3; t += 1.0 / 120)
        {
            CameraOffsets o = off.Update(1.0 / 120, false, 13.3f, true);
            min = Math.Min(min, o.Up);
            max = Math.Max(max, o.Up);
        }
        Assert.Equal(CameraLayer.StandingEye, min, 4);
        Assert.Equal(CameraLayer.StandingEye, max, 4);
        // At a run's speed the cycle rate is capped.
        var on = new CameraLayer();
        int crossings = 0;
        float previous = 0;
        for (double t = 0; t < 4; t += 1.0 / 240)
        {
            CameraOffsets o = on.Update(1.0 / 240, false, 13.3f, true);
            if (t > 1 && previous < 0 && o.Right >= 0)
                crossings++;
            previous = o.Right;
        }
        Assert.InRange(crossings / 3.0, 1.0, CameraLayer.MaxBobHz + 0.3);
    }

    [Fact]
    public void AShakeWobblesAndDiesAwayAndHasAnOffSwitch()
    {
        var layer = new CameraLayer();
        layer.AddShake(0.08f, 0.4f);
        Assert.True(layer.Shaking);
        float widest = 0;
        for (double t = 0; t < 0.4; t += 1.0 / 120)
        {
            CameraOffsets o = layer.Update(1.0 / 120, false, 0, false);
            widest = Math.Max(widest, Math.Abs(o.Right));
        }
        Assert.InRange(widest, 0.01f, 0.09f);
        CameraOffsets after = Run(layer, 0.2, false, 0, false);
        Assert.False(layer.Shaking);
        Assert.Equal(0f, after.Right, 4);
        Assert.Equal(CameraLayer.StandingEye, after.Up, 4);

        var off = new CameraLayer { Shake = false };
        off.AddShake(0.5f, 1f);
        Assert.False(off.Shaking);
        Assert.Equal(CameraLayer.StandingEye, Run(off, 0.3, false, 0, false).Up, 4);
    }

    [Fact]
    public void AWeakerShakeDoesNotCutAStrongerOne()
    {
        var layer = new CameraLayer();
        layer.AddShake(0.2f, 1f);
        layer.AddShake(0.01f, 0.1f);
        Run(layer, 0.5, false, 0, false);
        Assert.True(layer.Shaking);
    }

    [Fact]
    public void ResetPutsTheEyeAtOnce()
    {
        var layer = new CameraLayer();
        layer.Reset(true);
        Assert.Equal(CameraLayer.LyingEye, layer.EyeHeight);
        layer.Reset(false);
        Assert.Equal(CameraLayer.StandingEye, layer.EyeHeight);
    }

    [Fact]
    public void TheEyeFollowsTgsShakePathAndComesBackAndTheSwitchTurnsItOff()
    {
        var camera = new CameraLayer();
        camera.Update(0.1, false, 0f, false);
        // 16 pixels right in 0.2 s, then 16 up in 0.2 s, then home in 0.3 s (tg's last step: time 3)
        camera.FollowShakePath(new[] { (16, 0, 2), (16, 16, 2), (0, 0, 3) });
        CameraOffsets half = camera.Update(0.1, false, 0f, false);
        Assert.Equal(0.25f * TileSpace.TileSize, half.Right, 3); // halfway to half a tile
        camera.Update(0.1, false, 0f, false);
        CameraOffsets up = camera.Update(0.2, false, 0f, false);
        Assert.Equal(0.5f * TileSpace.TileSize, up.Right, 3);
        Assert.Equal(CameraLayer.StandingEye + 0.5f * TileSpace.TileSize, up.Up, 3);
        Assert.True(camera.FollowingPath);
        camera.Update(0.15, false, 0f, false); // (a frame is at most 0.25 s)
        CameraOffsets home = camera.Update(0.15, false, 0f, false);
        Assert.Equal(0f, home.Right, 3);
        Assert.Equal(CameraLayer.StandingEye, home.Up, 3);
        Assert.False(camera.FollowingPath);

        var off = new CameraLayer { Shake = false };
        off.Update(0.1, false, 0f, false);
        off.FollowShakePath(new[] { (32, 32, 5) });
        Assert.Equal(0f, off.Update(0.2, false, 0f, false).Right);
    }

    [Fact]
    public void A_pilot_sees_from_the_exosuits_head_and_its_walk_bobs_harder_than_a_mans()
    {
        static (float Eye, float Swing) Walk(bool exosuit)
        {
            var camera = new CameraLayer { InExosuit = exosuit };
            camera.Reset(false);
            float low = float.MaxValue, high = float.MinValue;
            for (int i = 0; i < 400; i++)
            {
                float up = camera.Update(1.0 / 60, false, 6f, true).Up;
                if (i < 100)
                    continue; // the eye climbs and the bob fades in
                low = Math.Min(low, up);
                high = Math.Max(high, up);
            }
            return ((low + high) / 2f, high - low);
        }

        (float manEye, float manSwing) = Walk(false);
        (float pilotEye, float pilotSwing) = Walk(true);
        Assert.True(pilotEye > manEye + 0.3f, $"{pilotEye} against {manEye}");
        Assert.True(pilotSwing > manSwing * 3f, $"{pilotSwing} against {manSwing}");
    }
}

