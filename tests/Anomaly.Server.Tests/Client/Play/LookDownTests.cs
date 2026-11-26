using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;

namespace Anomaly.Server.Tests.Client.Play;

/// <summary>UI overhaul slice T5a: looking down at the own body in first person (LookDown.cs), engine-free.</summary>
public class LookDownTests
{
    private const double Degree = Math.PI / 180;
    private const double Eye = 1.3; // the default standing eye (D157)
    private const double Near = 0.05; // the first-person camera's near plane (scenes/game.tscn)

    [Fact]
    public void TheEyeTurnsAboutAPointTwentyFiveCentimetresUnderItAndOnlyLookingDown()
    {
        Assert.Equal((0.0, 0.0), LookDown.EyeOffset(0, Eye));
        Assert.Equal((0.0, 0.0), LookDown.EyeOffset(60 * Degree, Eye));
        (double forward, double down) = LookDown.EyeOffset(-90 * Degree, Eye);
        Assert.Equal(0.25, forward, 9);
        Assert.Equal(0.25, down, 9);
        (forward, down) = LookDown.EyeOffset(-135 * Degree, Eye);
        Assert.Equal(0.25 * Math.Sin(135 * Degree), forward, 9);
        Assert.Equal(0.25 * (1 - Math.Cos(135 * Degree)), down, 9);
        for (double p = 0; p <= 135; p += 5)
        {
            (forward, down) = LookDown.EyeOffset(-p * Degree, Eye);
            // always the pivot's distance from the pivot, which sits 0.25 m under the level eye
            Assert.Equal(LookDown.PivotMetres, Math.Sqrt(forward * forward + (LookDown.PivotMetres - down) * (LookDown.PivotMetres - down)), 9);
            Assert.True(forward >= 0, "never behind the body");
        }
    }

    [Theory]
    [InlineData(0.075)] // a small body lying (BodyEyes: half its 0.15 m eye)
    [InlineData(0.15)] // the smallest body's eye (BodyEyes.MinEye)
    [InlineData(0.4)] // a human lying
    [InlineData(1.3)] // a human standing
    public void TheEyeNeverTurnsUnderTheFloor(double eye)
    {
        for (double p = 0; p <= LookDown.MaxDownDegrees; p += 0.5)
        {
            (double forward, double down) = LookDown.EyeOffset(-p * Degree, eye);
            Assert.True(eye - down > 0, $"an eye {eye} m up at {p} degrees is {eye - down:0.000} m above the floor");
            Assert.True(forward <= LookDown.Pivot(eye) + 1e-12);
        }
        Assert.True(LookDown.Pivot(eye) <= LookDown.PivotMetres);
    }

    [Fact]
    public void ThePivotIsAStandingHumansTwentyFiveCentimetresAtMostAndLessForALowerEye()
    {
        Assert.Equal(0.25, LookDown.Pivot(1.3), 9);
        Assert.Equal(0.25, LookDown.Pivot(1.9), 9); // a raised eye-height option does not lengthen the neck
        Assert.Equal(0.25 * 0.4 / 1.3, LookDown.Pivot(0.4), 6);
        Assert.Equal(0.0, LookDown.Pivot(0), 9);
    }

    [Fact]
    public void ThePitchGoesTo135DegreesDownAnd89UpInTheFirstPersonAlone()
    {
        Assert.Equal(-135 * Degree, LookDown.ClampPitch(-2.5, firstPerson: true), 9);
        Assert.Equal(-135 * Degree, LookDown.ClampPitch(-136 * Degree, firstPerson: true), 9);
        Assert.Equal(-120 * Degree, LookDown.ClampPitch(-120 * Degree, firstPerson: true), 9);
        Assert.Equal(89 * Degree, LookDown.ClampPitch(2.5, firstPerson: true), 9);
        Assert.Equal(-100 * Degree, LookDown.ClampPitch(-100 * Degree, firstPerson: true), 9);
        Assert.Equal(0.3, LookDown.ClampPitch(0.3, firstPerson: true), 9);
        // the free, orbit and map editor cameras keep 89 degrees either way; a first-person look past it is clamped when the camera changes
        Assert.Equal(-89 * Degree, LookDown.ClampPitch(-100 * Degree, firstPerson: false), 9);
        Assert.Equal(-89 * Degree, LookDown.ClampPitch(-2.5, firstPerson: false), 9);
        Assert.Equal(89 * Degree, LookDown.ClampPitch(2.5, firstPerson: false), 9);
        Assert.Equal(-0.5, LookDown.ClampPitch(-0.5, firstPerson: false), 9);
    }

    [Theory]
    [InlineData(0.3)] // under about 0.33 m up the neck (0.25 m in proportion) is too short for the near plane
    [InlineData(0.15)]
    public void ABodyWhoseNeckIsTooShortToClearTheNearPlaneIsNotShown(double eye)
    {
        Assert.Equal(double.PositiveInfinity, LookDown.BodyFrom(75, Near, eye));
        Assert.False(LookDown.ShowsOwnBody(-LookDown.MaxDownDegrees * Degree, 75, Near, eye, true, true, false, false, false, false, false, false));
    }

    // The own body is a card standing across the view at the player's feet (z = 0, z growing backward); the eye at pitch p below level is
    // EyeOffset ahead of it and down from Eye. Whether any ray within the vertical field of view reaches the card plane behind the eye, and the
    // highest point of the card such a ray reaches (null: none).
    private static double? HighestSeen(double pitchDown, double fovDegrees)
    {
        (double ahead, double drop) = LookDown.EyeOffset(-pitchDown * Degree, Eye);
        double height = Eye - drop, half = Math.Tan(fovDegrees / 2 * Degree), p = pitchDown * Degree;
        double? best = null;
        for (int i = 0; i <= 400; i++)
        {
            double yc = -half + 2 * half * i / 400; // the screen's height, -half at its bottom edge
            double back = -Math.Cos(p) - yc * Math.Sin(p), up = -Math.Sin(p) + yc * Math.Cos(p); // forward + yc * camera up
            if (back <= 1e-9)
                continue;
            double hit = height + up * (ahead / back);
            if (hit >= 0 && (best == null || hit > best))
                best = hit;
        }
        return best;
    }

    [Theory]
    [InlineData(75)]
    [InlineData(50)]
    [InlineData(110)]
    [InlineData(114)] // the widest option and the gun kick's widening
    public void TheBodyShowsOnlyFromWhereItRisesInFromTheBottomEdge(double fov)
    {
        double from = LookDown.BodyFrom(fov, Near, Eye) / Degree;
        Assert.InRange(from, 30, 66);
        // just short of it nothing of the card is in view (at the widest view, where the near plane sets the start, the feet at the very bottom
        // edge at most), so it cannot pop into the middle of the screen
        double? before = HighestSeen(from - 1, fov);
        Assert.True(before == null || before < 0.25, $"at {from - 1:0.0} degrees with a {fov} degree view the card is seen up to {before:0.00} m");
        // further down it rises in from the bottom edge: the legs first (with the 0.25 m neck the first rays to reach the card's plane meet it
        // under the floor, so the feet come in a few degrees later, by the time the held things are down)
        double? seen = HighestSeen(from + 15, fov);
        Assert.NotNull(seen);
        Assert.True(seen < 1.0, $"at {from + 15:0.0} degrees with a {fov} degree view the card is seen up to {seen:0.00} m");
    }

    [Fact]
    public void WithTheDefaultViewTheBodyShowsFrom52AndAHalfDegrees()
        => Assert.Equal(52.5, LookDown.BodyFrom(75, Near, Eye) / Degree, 6);

    // Where the crosshair's ray (the screen's middle, the camera's forward) meets the card, metres above the feet; past straight down only.
    private static double CrosshairHeight(double pitchDown)
    {
        (double ahead, double drop) = LookDown.EyeOffset(-pitchDown * Degree, Eye);
        return Eye - drop + Math.Tan(pitchDown * Degree) * ahead;
    }

    [Theory]
    // the picture is 0.05 m a pixel: the playtest clicked icon rows 2 (the left leg), 11 (tg's groin rows 10 to 13) and 14 (the chest)
    [InlineData(105, 0.084)] // the feet
    [InlineData(119, 0.534)] // the groin
    [InlineData(135, 0.696)] // the chest
    public void TheCrosshairRisesUpTheOwnBodyFromTheFeetToTheChest(double pitchDown, double height)
    {
        Assert.Equal(height, CrosshairHeight(pitchDown), 3);
        for (double p = 95; p < LookDown.MaxDownDegrees; p += 1)
            Assert.True(CrosshairHeight(p + 1) > CrosshairHeight(p), $"the crosshair goes down the body from {p} to {p + 1} degrees");
        Assert.True(CrosshairHeight(LookDown.MaxDownDegrees) < 1.0, "never above the chest");
    }

    [Theory]
    [InlineData(50)]
    [InlineData(75)]
    [InlineData(114)]
    public void TheNearPlaneNeverReachesTheCardWhileItShows(double fov)
    {
        double half = fov / 2 * Degree;
        for (double p = LookDown.BodyFrom(fov, Near, Eye) / Degree; p <= LookDown.MaxDownDegrees; p += 0.5)
        {
            double pr = p * Degree;
            (double ahead, _) = LookDown.EyeOffset(-pr, Eye);
            // the near plane's rectangle reaches back (z growing backward) at most to its bottom edge: near * (forward + its edge * up)
            double reachBack = Near * (-Math.Cos(pr) + Math.Tan(half) * Math.Sin(pr));
            Assert.True(reachBack < ahead, $"at {p} degrees the near plane reaches {reachBack:0.000} m back, the card is {ahead:0.000} m behind the eye");
        }
    }

    [Theory]
    [InlineData(50)]
    [InlineData(75)]
    [InlineData(114)]
    public void TheHeadIsNeverInView(double fov)
    {
        // a human's picture is drawn at 0.05 m a pixel with its eyes at about 1.275 m (the default eye's height); its jaw is about three pixels
        // under the eyes, so its head starts at about 1.10 m. Nothing that high is seen at any pitch the view can take.
        const double Jaw = 1.10;
        for (double p = 0; p <= LookDown.MaxDownDegrees; p += 0.5)
        {
            double? seen = HighestSeen(p, fov);
            Assert.True(seen == null || seen < Jaw, $"at {p} degrees with a {fov} degree view the card is seen up to {seen:0.000} m");
        }
    }

    [Fact]
    public void TheBodyIsShownOnlyInTheOwnStandingFirstPersonLookingDownFarEnough()
    {
        double down = -80 * Degree;
        bool Shows(double pitch = -80 * Degree, bool firstPerson = true, bool onTurf = true, bool ghost = false, bool observing = false, bool lying = false,
            bool limp = false, bool exosuit = false, bool scoped = false)
            => LookDown.ShowsOwnBody(pitch, 75, Near, Eye, firstPerson, onTurf, ghost, observing, lying, limp, exosuit, scoped);
        Assert.True(Shows(down));
        Assert.True(Shows(-LookDown.MaxDownDegrees * Degree));
        Assert.False(Shows(-45 * Degree), "looking at the floor ahead: its shadow only");
        Assert.False(Shows(0));
        Assert.False(Shows(60 * Degree));
        Assert.False(Shows(firstPerson: false), "the free, orbit and third-person cameras");
        Assert.False(Shows(onTurf: false), "inside a locker");
        Assert.False(Shows(ghost: true));
        Assert.False(Shows(observing: true), "seeing through another's eyes");
        Assert.False(Shows(lying: true));
        Assert.False(Shows(limp: true), "unconscious or dead");
        Assert.False(Shows(exosuit: true));
        Assert.False(Shows(scoped: true));
    }

    [Fact]
    public void TheHeldThingsGoDownAsTheBodyRisesIntoView()
    {
        double from = LookDown.BodyFrom(75, Near, Eye);
        Assert.Equal(0f, LookDown.HeldLowered(-from + 0.01, 75, Near, Eye));
        Assert.Equal(0f, LookDown.HeldLowered(0, 75, Near, Eye));
        Assert.Equal(1f, LookDown.HeldLowered(-from - 15 * Degree, 75, Near, Eye), 5);
        Assert.Equal(1f, LookDown.HeldLowered(-LookDown.MaxDownDegrees * Degree, 75, Near, Eye), 5);
        float last = 0f;
        for (double p = from; p <= from + 15 * Degree; p += Degree)
        {
            float lowered = LookDown.HeldLowered(-p, 75, Near, Eye);
            Assert.True(lowered >= last, "it only goes down as the view turns down");
            last = lowered;
        }
    }

    // Godot's camera basis for a yaw and a pitch (Basis(Up, yaw) * Basis(Right, pitch)): its right and its forward (-Z).
    private static ((double X, double Z) Right, (double X, double Y, double Z) Forward) Camera(double yaw, double pitch)
        => ((Math.Cos(yaw), -Math.Sin(yaw)), (-Math.Cos(pitch) * Math.Sin(yaw), Math.Sin(pitch), -Math.Cos(pitch) * Math.Cos(yaw)));

    [Fact]
    public void TheWayThePlayerFacesComesFromTheYawAtEveryPitch()
    {
        foreach (double yawDegrees in new[] { 0.0, 37, 90, -135, 180 })
        {
            double yaw = yawDegrees * Degree;
            for (double p = -135; p <= 89; p += 1)
            {
                ((double rx, double rz), (double fx, double fy, double fz)) = Camera(yaw, p * Degree);
                (double ax, double az) = LookDown.Ahead(rx, rz);
                Assert.Equal(-Math.Sin(yaw), ax, 9);
                Assert.Equal(-Math.Cos(yaw), az, 9);
                // compass: clockwise from north, Godot's yaw counter-clockwise
                Assert.Equal(0, Math.Abs(Math.IEEERemainder(LookDown.CompassYaw(rx, rz) + yaw, 2 * Math.PI)), 9);
                // the pitch goes on past straight down, where the arcsine of the forward's height turns back
                Assert.Equal(p * Degree, LookDown.Pitch(fx, fy, fz, rx, rz), 9);
            }
        }
        // past straight down the camera's forward itself points back on the ground
        ((_, _), (double bx, _, double bz)) = Camera(0, -100 * Degree);
        Assert.True(bz > 0 && Math.Abs(bx) < 1e-9, "looking north 100 degrees down, the forward's ground part points south");
    }

    [Fact]
    public void TheOwnMobIsATargetOnlyWhileItsBodyIsShownToItsOwnEye()
    {
        // Slice T5b: shown (looking down far enough), nothing is left out of the pick; otherwise the own mob is, as before
        Assert.Equal(0, LookDown.PickExclude(42, bodyShown: true));
        Assert.Equal(42, LookDown.PickExclude(42, bodyShown: false));
        Assert.Equal(0, LookDown.PickExclude(0, bodyShown: false)); // no own mob
    }

    [Fact]
    public void TheOwnBodyShowsItsFrontToItsOwnEyeLookingDownPastStraightDown()
    {
        // The body at tile (10, 10) facing north (yaw byte 0); the frame is the one a viewer ahead of it along the view's yaw sees (EntityLayer's
        // FrontViewer): its front, at every pitch, also past straight down where the camera's forward points back.
        foreach (double p in new[] { -45.0, -89, -91, -100, -105, -120, -135 })
        {
            ((double rx, double rz), _) = Camera(0, p * Degree);
            (double ax, double az) = LookDown.Ahead(rx, rz);
            double viewerX = 10 + ax * 2, viewerY = 10 - az * 2; // tile coordinates: y north is -z
            Assert.Equal(ByondDir.South, BillboardMath.ChooseDir(0, 10, 10, viewerX, viewerY, 4));
        }
    }
}
