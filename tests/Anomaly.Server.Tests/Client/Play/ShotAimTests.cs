using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Core.Tg;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>
/// M7x slice A (D177): the client's side of an aimed shot. The camera ray becomes the wire's aim (PlaySession.AimOf, DV94); tg's recoil kick
/// punches the first-person view and climbs its aim (CameraLayer.Recoil, GunKick, DV96); and the server's mirrored billboard
/// frame choice (ShotGeometry) is the client's (BillboardMath), so both look at the same frame of a body.
/// </summary>
public class ShotAimTests
{
    [Fact]
    public void ACameraRayBecomesTheWiresAim()
    {
        // the eye over tile (10, 5)'s centre (world metres (x - 0.5) * 2), 1.3 m up, looking east and 30 degrees up
        double up = Math.Sin(Math.PI / 6), level = Math.Cos(Math.PI / 6);
        ShotAim aim = PlaySession.AimOf(new PickRay(19, 9, 1.3, 1, level, 0, up, 8));
        Assert.Equal(10 * 256, aim.East);
        Assert.Equal(5 * 256, aim.North);
        Assert.Equal(1300, aim.Up);
        Assert.Equal(16384, aim.Yaw); // a quarter turn clockwise from north
        Assert.InRange(aim.Pitch, 65536 / 12 - 1, 65536 / 12 + 1);
        Assert.True(aim.InRange);
        // straight down is the lowest pitch the wire takes; north is yaw 0
        ShotAim down = PlaySession.AimOf(new PickRay(19, 9, 1.3, 1, 0, 1e-12, -1, 8));
        Assert.Equal(-ShotAim.MaxPitch, down.Pitch);
        Assert.Equal(0, PlaySession.AimOf(new PickRay(19, 9, 1.3, 1, 0, 1, 0, 8)).Yaw);
    }

    [Fact]
    public void ARecoilKickPunchesTheViewAndClimbsTheAimThatComesBackUnlessPulledDown()
    {
        var camera = new CameraLayer();
        camera.Reset(false);
        float degree = MathF.PI / 180f;
        // tg's kick of one tile straight back (the shot east, the view pushed west: pixel_x -32): 3 degrees, half aim, half punch
        camera.Recoil(new[] { (-32, 0, 2), (0, 0, 3) }, facingDegrees: 90);
        float aim = 0f, top = 0f;
        for (int i = 0; i < 8; i++) // 0.13 s: the punch peaks after 1/omega (36 ms); the aim waits 0.15 s to come back
        {
            top = MathF.Max(top, camera.Update(1 / 60.0, false, 0, false).PitchRadians);
            aim += camera.Kick.TakeAim().Up;
        }
        Assert.InRange(top, 1.3f * degree, 1.5f * degree); // the punch: half of 3 degrees at its peak
        Assert.InRange(aim, 1.35f * degree, 1.5f * degree); // the aim: the other half, taken by the view's own pitch
        Assert.Equal(0f, camera.Kick.TakeAim().Right, 5);
        for (int i = 0; i < 120; i++) // the gun stops kicking: the aim comes back by itself
        {
            camera.Update(1 / 60.0, false, 0, false);
            aim += camera.Kick.TakeAim().Up;
        }
        Assert.Equal(0f, aim, 3);
        Assert.Equal(0f, camera.Update(0.05, false, 0, false).PitchRadians, 3);

        // pulled down with the mouse, the aim no longer comes back
        camera.Recoil(new[] { (-32, 0, 2), (0, 0, 3) }, facingDegrees: 90);
        aim = 0f;
        for (int i = 0; i < 8; i++)
        {
            camera.Update(1 / 60.0, false, 0, false);
            aim += camera.Kick.TakeAim().Up;
        }
        camera.Kick.Compensate(-aim, 0f);
        float after = 0f;
        for (int i = 0; i < 120; i++)
        {
            camera.Update(1 / 60.0, false, 0, false);
            after += camera.Kick.TakeAim().Up;
        }
        Assert.InRange(after, -0.1f * degree, 0.1f * degree);

        // a burst climbs: kicks 0.1 s apart leave no time to come back
        camera.Reset(false);
        aim = 0f;
        for (int shot = 0; shot < 5; shot++)
        {
            camera.Recoil(new[] { (-32, 0, 2), (0, 0, 3) }, facingDegrees: 90);
            for (int i = 0; i < 6; i++)
            {
                camera.Update(1 / 60.0, false, 0, false);
                aim += camera.Kick.TakeAim().Up;
            }
        }
        Assert.True(aim > 6.5f * degree, "five kicks of 1.5 degrees of aim each climb");

        // a kick to the side (facing north, pushed east) yaws the view right
        camera.Reset(false);
        camera.Recoil(new[] { (32, 0, 2), (0, 0, 3) }, facingDegrees: 0);
        CameraOffsets side = camera.Update(0.03, false, 0, false);
        Assert.True(side.YawRadians > 0f && camera.Kick.TakeAim().Right > 0f);
        Assert.Equal(0f, side.PitchRadians, 4);
    }

    [Fact]
    public void AHeavyKickRisesSlowerAndShovesTheEyeWidensTheViewAndRolls()
    {
        var light = new CameraLayer();
        light.Reset(false);
        light.Recoil(new[] { (-16, 0, 2), (0, 0, 3) }, facingDegrees: 90); // a pistol's 1.5 degrees
        Assert.Equal(0f, light.Kick.Heavy);
        // tg's anti-tank rifle: 15 tiles, 45 degrees asked, 15 given, fully heavy
        var heavy = new CameraLayer();
        heavy.Reset(false);
        heavy.Recoil(new[] { (-15 * 32, 0, 16), (0, 0, 23) }, facingDegrees: 90);
        Assert.Equal(GunKick.MostDegrees, heavy.Kick.LastDegrees, 3);
        Assert.Equal(1f, heavy.Kick.Heavy);
        float degree = MathF.PI / 180f, early = heavy.Update(0.036, false, 0, false).PitchRadians, top = early;
        CameraOffsets at = default;
        for (int i = 0; i < 6; i++)
        {
            CameraOffsets now = heavy.Update(1 / 60.0, false, 0, false);
            if (now.PitchRadians > top)
                (top, at) = (now.PitchRadians, now);
        }
        Assert.True(early < 0.8f * top, "the heavy kick is still rising when a light one has peaked");
        Assert.InRange(top, 6.5f * degree, 7.5f * degree);
        Assert.True(at.Forward < -0.05f && at.FovDegrees > 2f && MathF.Abs(at.RollRadians) > 1f * degree);
        for (int i = 0; i < 60; i++)
            heavy.Update(1 / 60.0, false, 0, false);
        Assert.InRange(heavy.Update(0, false, 0, false).PitchRadians, -0.1f * degree, 0.2f * degree); // back within a second
    }

    [Fact]
    public void TheServersFrameChoiceIsTheClients()
    {
        var rng = new Random(177);
        for (int i = 0; i < 5000; i++)
        {
            byte yaw = (byte)rng.Next(256);
            double mx = rng.NextDouble() * 50, my = rng.NextDouble() * 50, vx = rng.NextDouble() * 50, vy = rng.NextDouble() * 50;
            Assert.Equal(BillboardMath.ChooseDir(yaw, mx, my, vx, vy, 4), ShotGeometry.ChooseDir(yaw, mx, my, vx, vy));
            bool limp = rng.Next(2) == 0;
            Assert.Equal(BillboardMath.ChooseLyingDir(yaw, mx, my, vx, vy, 4, limp), ShotGeometry.ChooseLyingDir(yaw, mx, my, vx, vy, limp));
        }
        foreach (byte dir in new byte[] { 0, 1, 2, 4, 5, 6, 8, 9, 10 })
            Assert.Equal(BillboardMath.YawOfDir(dir), ShotGeometry.YawOfDir(dir));
        Assert.Equal(WorldScale.MobScale, ShotGeometry.MobScale);
        Assert.Equal(WorldScale.DefaultWallHeight / TileSpace.TileSize * 32f, ShotGeometry.CeilingPixels);
        Assert.Equal(WorldScale.DefaultEyeHeight * 16f, ShotGeometry.StandingEyePixels, 3);
        Assert.Equal(CameraLayer.LyingEye * 16f, ShotGeometry.LyingEyePixels, 3);
        Assert.Equal(WorldScale.MaxEyeHeight * 16f, ShotGeometry.MaxStandingEyePixels, 3);
    }
}
