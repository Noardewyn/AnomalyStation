using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Render;

/// <summary>Frame choice by the viewer's angle, the picture's rectangle, ray picks and the turf kit's plan (D104).</summary>
public class BillboardMathTests
{
    // The mob stands at (10, 10) and faces a yaw (256 per turn, clockwise from north: 0 north, 64 east, 128 south, 192 west).
    private static byte Choose(byte yaw, double viewerX, double viewerY, int directions = 4)
        => BillboardMath.ChooseDir(yaw, 10, 10, viewerX, viewerY, directions);

    [Fact]
    public void AViewerInFrontSeesSouthBehindSeesNorthAndTheSidesSeeEastAndWest()
    {
        // The mob faces north (yaw 0): a viewer to the north is in front.
        Assert.Equal(ByondDir.South, Choose(0, 10, 15));
        Assert.Equal(ByondDir.North, Choose(0, 10, 5));
        Assert.Equal(ByondDir.East, Choose(0, 15, 10));  // east is the mob's right
        Assert.Equal(ByondDir.West, Choose(0, 5, 10));
    }

    [Fact]
    public void TheFrameFollowsTheMobsFacing()
    {
        // The same viewer (south of the mob) as the mob turns clockwise: facing south he sees its front, facing north its back.
        Assert.Equal(ByondDir.South, Choose(128, 10, 5));
        Assert.Equal(ByondDir.North, Choose(0, 10, 5));
        // Facing east, the viewer to the south is at the mob's right (a clockwise quarter from its facing): east.
        Assert.Equal(ByondDir.East, Choose(64, 10, 5));
        Assert.Equal(ByondDir.West, Choose(192, 10, 5));
    }

    [Fact]
    public void EightDirectionSectorsAreFortyFiveDegreesWide()
    {
        // The mob faces north; a viewer 45 degrees clockwise from its facing (north-east of it) sees its front right: south-east.
        Assert.Equal(ByondDir.SouthEast, Choose(0, 15, 15, 8));
        Assert.Equal(ByondDir.SouthWest, Choose(0, 5, 15, 8));
        Assert.Equal(ByondDir.NorthEast, Choose(0, 15, 5, 8));
        Assert.Equal(ByondDir.NorthWest, Choose(0, 5, 5, 8));
        Assert.Equal(ByondDir.South, Choose(0, 10 + 1, 10 + 10, 8)); // 5.7 degrees off
    }

    [Fact]
    public void FourDirectionSectorsSwitchAtFortyFiveDegrees()
    {
        Assert.Equal(ByondDir.South, Choose(0, 10 + 4.4, 10 + 10)); // 23.8 degrees off the facing
        Assert.Equal(ByondDir.East, Choose(0, 10 + 10, 10 + 8));    // 51 degrees
        Assert.Equal(ByondDir.South, Choose(0, 10 + 9.9, 10 + 10)); // just inside 45
    }

    [Fact]
    public void AStillIconAndAViewerOnTheMobShowSouth()
    {
        Assert.Equal(ByondDir.South, Choose(0, 10, 5, directions: 1));
        Assert.Equal(ByondDir.South, Choose(77, 10, 10));
    }

    [Fact]
    public void YawOfDirFacesTheDirection()
    {
        Assert.Equal(0, BillboardMath.YawOfDir(ByondDir.North));
        Assert.Equal(64, BillboardMath.YawOfDir(ByondDir.East));
        Assert.Equal(128, BillboardMath.YawOfDir(ByondDir.South));
        Assert.Equal(192, BillboardMath.YawOfDir(ByondDir.West));
        Assert.Equal(32, BillboardMath.YawOfDir(ByondDir.NorthEast));
    }

    [Fact]
    public void ThePictureIsCentredOnTheFeetWithTheSquaresBottomOnTheFloor()
    {
        // A plain 32 x 32 sprite: 2 m wide and 2 m high, centred, standing on the floor.
        SpriteRect plain = SpriteRect.Of(32, 32, 0, 0);
        Assert.Equal((-1.0, 1.0, 0.0, 2.0), (plain.Left, plain.Right, plain.Bottom, plain.Top));
        // A picture that grew 6 pixels below the square and 4 to the left: it hangs below the floor and to the left.
        SpriteRect grown = SpriteRect.Of(36, 38, 4, 6);
        Assert.Equal(-(4 + 16) * 0.0625, grown.Left, 9);
        Assert.Equal((36 - 4 - 16) * 0.0625, grown.Right, 9);
        Assert.Equal(-6 * 0.0625, grown.Bottom, 9);
        Assert.Equal((38 - 6) * 0.0625, grown.Top, 9);
    }

    private static Raster Stripe()
    {
        // 32 x 32 with only the left half columns 0..15 opaque.
        var r = new Raster(32, 32);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 16; x++)
                r.Set(x, y, 0xFFFFFFFF);
        }
        return r;
    }

    [Fact]
    public void ARayHitsTheOpaquePartOfAStandingPictureAndMissesTheClearPart()
    {
        var feet = new Vec3(0, 0, -10);
        var right = new Vec3(1, 0, 0); // looking north (-Z) the camera's right is east
        SpriteRect rect = SpriteRect.Of(32, 32, 0, 0);
        Raster image = Stripe();
        var origin = new Vec3(0, 1.0, 0);
        // Straight ahead at the picture's centre column: x = 0 is the boundary (column 16) so the right half, clear.
        Assert.False(BillboardMath.RayStanding(origin, new Vec3(0, 0, -1), 50, feet, right, rect, image, out _));
        // A little to the left (x = -0.5 at the picture): inside the opaque half.
        var aim = new Vec3(-0.5, 0, -10);
        double length = aim.Length;
        Assert.True(BillboardMath.RayStanding(origin, aim * (1 / length), 50, feet, right, rect, image, out BillboardHit hit));
        Assert.Equal(length, hit.Distance, 6);
        Assert.False(BillboardMath.RayStanding(origin, aim * (1 / length), 5, feet, right, rect, image, out _)); // too short
        Assert.False(BillboardMath.RayStanding(origin, new Vec3(0, 0, 1), 50, feet, right, rect, image, out _));  // looking away
        Assert.True(BillboardMath.RayStanding(origin, aim * (1 / length), 50, feet, right, rect, null, out _));
    }

    [Fact]
    public void ARayAboveOrBelowThePictureMisses()
    {
        var feet = new Vec3(0, 0, -10);
        SpriteRect rect = SpriteRect.Of(32, 32, 0, 0);
        Assert.False(BillboardMath.RayStanding(new Vec3(-0.5, 2.5, 0), new Vec3(0, 0, -1), 50, feet, new Vec3(1, 0, 0), rect, null, out _));
        Assert.True(BillboardMath.RayStanding(new Vec3(-0.5, 1.9, 0), new Vec3(0, 0, -1), 50, feet, new Vec3(1, 0, 0), rect, null, out _));
    }

    [Fact]
    public void ARayFromAboveHitsALyingPictureWhereItsPixelIsOpaque()
    {
        // A lying picture with its marked pixel at the top left of the image: the north-west corner when seen from above.
        var image = new Raster(32, 32);
        image.Set(0, 0, 0xFFFFFFFF);
        SpriteRect rect = SpriteRect.OfFlat(32, 32, 0, 0);
        var centre = new Vec3(4, 0, -6);
        Assert.True(BillboardMath.RayLying(new Vec3(3.05, 3, -6 - 0.95), new Vec3(0, -1, 0), 10, centre, 0.01, rect, image, out BillboardHit hit));
        Assert.Equal(3 - 0.01, hit.Distance, 6);
        Assert.False(BillboardMath.RayLying(new Vec3(4.95, 3, -6 + 0.95), new Vec3(0, -1, 0), 10, centre, 0.01, rect, image, out _)); // the south-east corner is clear
        Assert.False(BillboardMath.RayLying(new Vec3(3.05, 3, -6 - 0.95), new Vec3(1, 0, 0), 10, centre, 0.01, rect, image, out _)); // parallel to the floor
    }

    [Fact]
    public void TheTurfKitGroupsCellsByKindAndPictureAndSkipsOpenSpace()
    {
        var map = new MapGrid { Width = 3, Height = 2, Levels = 1 };
        map.TurfTypes.AddRange(new[] { "/turf/open/floor/plating", "/turf/closed/wall", "/turf/open/space" });
        // y=1: floor floor wall; y=2: wall space floor
        foreach (int type in new[] { 0, 0, 1, 1, 2, 0 })
            map.Runs.Add(new TurfRun(1, type));
        var floorA = TableRef.Public(1);
        var wallA = TableRef.Public(2);
        List<TurfGroup> groups = TurfKitPlan.Build(map, (x, y, z) => (x, y) switch
        {
            (1, 1) => floorA,
            (2, 1) => floorA,
            (3, 2) => TableRef.Public(3), // a different floor picture
            (3, 1) => wallA,
            (1, 2) => wallA,
            _ => TableRef.None,
        });
        Assert.Equal(3, groups.Count);
        Assert.Equal(new[] { (1, 1, 1), (2, 1, 1) }, groups[0].Cells);
        Assert.Equal((TurfKind.Open, floorA), (groups[0].Kind, groups[0].Appearance));
        Assert.Equal(new[] { (3, 1, 1), (1, 2, 1) }, groups[1].Cells);
        Assert.Equal(TurfKind.Closed, groups[1].Kind);
        Assert.Equal(new[] { (3, 2, 1) }, groups[2].Cells);
        Assert.DoesNotContain(groups.SelectMany(g => g.Cells), c => c == (2, 2, 1)); // open space draws nothing
        Assert.Equal(1.0, TurfKitPlan.WallVerticalRepeats); // D119: the wall is as high as the sprite is wide, one picture
    }

    [Fact]
    public void CellsWithoutAPictureAreGroupedByTheirTurfType()
    {
        var map = new MapGrid { Width = 2, Height = 1, Levels = 1 };
        map.TurfTypes.AddRange(new[] { "/turf/open/a", "/turf/open/b" });
        map.Runs.Add(new TurfRun(1, 0));
        map.Runs.Add(new TurfRun(1, 1));
        List<TurfGroup> groups = TurfKitPlan.Build(map, null);
        Assert.Equal(2, groups.Count);
        Assert.Equal(new[] { 0, 1 }, groups.Select(g => g.TypeIndex));
    }

    [Fact]
    public void AConsciousLyingMobShowsItsBackWhateverTheSide()
    {
        foreach (double viewerX in new[] { 5.0, 10.0, 15.0 })
            Assert.Equal(ByondDir.North, BillboardMath.ChooseLyingDir(0, 10, 10, viewerX, 5, 4, limp: false));
        Assert.Equal(ByondDir.South, BillboardMath.ChooseLyingDir(0, 10, 10, 10, 5, 1, limp: false)); // a still icon
    }

    [Fact]
    public void ALimpLyingMobShowsFrontOrBackByTheViewersSide()
    {
        // Faces north (yaw 0): a viewer to the north sees its front, one to the south its back; a side viewer the front.
        Assert.Equal(ByondDir.South, BillboardMath.ChooseLyingDir(0, 10, 10, 10, 15, 4, limp: true));
        Assert.Equal(ByondDir.North, BillboardMath.ChooseLyingDir(0, 10, 10, 10, 5, 4, limp: true));
        Assert.Equal(ByondDir.South, BillboardMath.ChooseLyingDir(0, 10, 10, 15, 10, 4, limp: true));
        // Faces south (yaw 128): the viewer to the south now sees the front.
        Assert.Equal(ByondDir.South, BillboardMath.ChooseLyingDir(128, 10, 10, 10, 5, 4, limp: true));
    }

    [Fact]
    public void AnItemOnTheFloorShowsItsEastOrWestSideByTheViewersSide()
    {
        // Faces north (yaw 0): a viewer to the east has its north on the right (east), one to the west on the left (west); in front or behind, the
        // side it was on stays east or west, never the south or north picture.
        Assert.Equal(ByondDir.East, BillboardMath.ChooseSideDir(0, 10, 10, 15, 10, 4));
        Assert.Equal(ByondDir.West, BillboardMath.ChooseSideDir(0, 10, 10, 5, 10, 4));
        Assert.Equal(ByondDir.East, BillboardMath.ChooseSideDir(0, 10, 10, 11, 15, 4));
        Assert.Equal(ByondDir.West, BillboardMath.ChooseSideDir(0, 10, 10, 9, 5, 4));
        Assert.Equal(ByondDir.West, BillboardMath.ChooseSideDir(128, 10, 10, 15, 10, 4)); // faces south: east of it is its left
        Assert.Equal(ByondDir.South, BillboardMath.ChooseSideDir(0, 10, 10, 15, 10, 1)); // a still icon
    }

    [Fact]
    public void ALyingMobKeepsTheYawItFellWith()
    {
        var facing = new LyingFacing();
        Assert.Equal(40, facing.Update(false, 40));  // standing: the pose's yaw
        Assert.Equal(40, facing.Update(true, 40));   // falls looking where it looked
        Assert.Equal(40, facing.Update(true, 200));  // the camera turns: the body does not
        Assert.Equal(77, facing.Update(false, 77));  // gets up: the pose's yaw again
    }

    [Fact]
    public void ABigIconCentredByItsOwnOffsetsIsRecognised()
    {
        // DV49: tg's explosion (96 x 96 at -32, -32) and the singularity's stage 5 (352 x 352 at -160, -160)
        Assert.True(SpriteRect.Of(96, 96, 0, 0).IsCentredBigIcon(-32, -32));
        Assert.True(SpriteRect.Of(352, 352, 0, 0).IsCentredBigIcon(-160, -160));
        Assert.False(SpriteRect.Of(96, 96, 0, 0).IsCentredBigIcon(0, 0)); // a big icon drawn from its corner
        Assert.False(SpriteRect.Of(32, 32, 0, 0).IsCentredBigIcon(0, 0)); // a tile-sized icon
        Assert.False(SpriteRect.Of(32, 32, 0, 0).IsCentredBigIcon(4, -6)); // a small one with a ground offset
    }

    [Fact]
    public void AWidePictureCentredAcrossByItsPixelXIsRecognised()
    {
        // DV49: tg's tree (64 x 64 at pixel_x -16) and the colossus (96 x 96 at -32, pixel_y 0), at the tile scale and drawn smaller
        Assert.True(SpriteRect.Of(64, 64, 0, 0).IsCentredWide(-16));
        Assert.True(SpriteRect.Of(96, 96, 0, 0).IsCentredWide(-32));
        Assert.True(SpriteRect.Of(96, 96, 0, 0).AsItem(0.5, 3).IsCentredWide(-32));
        Assert.False(SpriteRect.Of(64, 64, 0, 0).IsCentredWide(0)); // drawn from its corner, as tg draws it
        Assert.False(SpriteRect.Of(32, 64, 0, 0).IsCentredWide(0)); // a tall one (a palm) is no wider than its square
        Assert.False(SpriteRect.Of(32, 32, 0, 0).IsCentredWide(4)); // a small one with a ground offset
    }

    [Fact]
    public void AnImageHangingOnAMobKeepsItsRowsInTheMobsCellAtTheMobScale()
    {
        // tg's human (rows 3 to 30 from the top, one clear row under the feet) and a medical HUD bar (hud.dmi's hudhealth100: rows 0 to 2 from the
        // top, over the head), both 32 x 32 and drawn at a mob scale below 1, where the pictures rest their drawn pixels on the floor
        static ComposedRaster Rows(int fromTop, int toTop)
        {
            var image = new Raster(32, 32);
            for (int y = fromTop; y <= toTop; y++)
                for (int x = 8; x < 23; x++)
                    image.Set(x, y, 0xFF0000FFu);
            return new ComposedRaster { Image = image };
        }
        const double scale = 0.85;
        SpriteRect mob = AtlasBuilder.PictureRect(Rows(3, 30), lying: false, scale, null, out int mobTrim);
        Assert.Equal(1, mobTrim);
        double headTop = mob.Bottom + (32 - 3) * mob.PixelSize;
        SpriteRect bar = AtlasBuilder.PictureRect(Rows(0, 2), lying: false, scale, mobTrim, out int barTrim);
        Assert.Equal(mobTrim, barTrim);
        Assert.Equal(mob.Bottom, bar.Bottom, 9); // the same cell
        Assert.True(bar.Bottom + (32 - 2 - 1) * bar.PixelSize >= headTop, "the bar's lowest row is over the head");
        // by its own clear rows it would lie on the floor (the bug the visual check found)
        SpriteRect own = AtlasBuilder.PictureRect(Rows(0, 2), lying: false, scale, null, out int ownTrim);
        Assert.Equal(29, ownTrim);
        Assert.Equal(0, own.Bottom + 29 * own.PixelSize, 9);
        // at the tile scale standing nothing is dropped
        Assert.Equal(SpriteRect.Of(32, 32, 0, 0), AtlasBuilder.PictureRect(Rows(0, 2), lying: false, 1, 1, out int none));
        Assert.Equal(0, none);
    }

    [Fact]
    public void AnItemFacingTheEyeFromAboveIsHitOnItsTiltedTopWhereAnUprightPictureIsMissed()
    {
        // The eye 10 m up and 10 m south of the feet, looking down at 45 degrees: the item's up is the camera's up, tilted back (north, -Z).
        var feet = new Vec3(0, 0, 0);
        var right = new Vec3(1, 0, 0);
        double s = Math.Sqrt(0.5);
        var up = new Vec3(0, s, -s);
        SpriteRect rect = SpriteRect.Of(32, 32, 0, 0);
        var origin = new Vec3(0, 10, 10);
        Vec3 aim = feet + up * 1.9 - origin;
        Vec3 dir = aim * (1 / aim.Length);
        Assert.True(BillboardMath.RayPlane(origin, dir, 50, feet, right, up, rect, null, out BillboardHit hit));
        Assert.Equal(aim.Length, hit.Distance, 6);
        Assert.False(BillboardMath.RayStanding(origin, dir, 50, feet, right, rect, null, out _));
    }

    [Fact]
    public void APickShapeThatFacesTheEyeIsHitAlongTheViewAxisOnItsTiltedTop()
    {
        // Looking north and 45 degrees down; the picture (2 m tall, feet at east 1, north 1) tilts north about its feet.
        double s = Math.Sqrt(0.5);
        PickShape shape = PickShape.StandingPicture(1, PickSurface.Billboard, 1, 1, 1, 1, 1, 0, SpriteRect.Of(32, 32, 0, 0), null, 0, 0);
        // the point 1.9 m up the tilted picture, and the eye 10 m back along the view axis from it
        double pe = 1, pn = 1 + 1.9 * s, pu = 1.9 * s;
        var ray = new PickRay(pe, pn - 10 * s, pu + 10 * s, 1, 0, s, -s, 50);
        Assert.False(shape.TryHit(ray, 50, out _, out _, out _, out _, out _, out _));
        shape.FacesEye = true;
        Assert.True(shape.TryHit(ray, 50, out double t, out _, out double north, out _, out _, out byte iconY));
        Assert.Equal(10 - PickShape.FacingLift, t, 6); // (picked FacingLift nearer the eye, as drawn)
        Assert.Equal(pn - s * PickShape.FacingLift, north, 6);
        Assert.Equal(31, iconY); // 1.9 m of 2 m: the 31st pixel row from the bottom
    }

    [Fact]
    public void APickShapeThatFacesTheEyeIsSquareToTheViewNotToAnOffCentreCursorRay()
    {
        // The camera looks north and 45 degrees down; the cursor's ray goes through the tilted picture's point 1.9 m up it from an eye off the view axis.
        double s = Math.Sqrt(0.5);
        PickShape shape = PickShape.StandingPicture(1, PickSurface.Billboard, 1, 1, 1, 1, 1, 0, SpriteRect.Of(32, 32, 0, 0), null, 0, 0);
        shape.FacesEye = true;
        double pe = 1, pn = 1 + 1.9 * s, pu = 1.9 * s;
        double oe = 0.5, on = pn - 10 * s, ou = pu + 9;
        double de = pe - oe, dn = pn - on, du = pu - ou, length = Math.Sqrt(de * de + dn * dn + du * du);
        var cursor = new PickRay(oe, on, ou, 1, de / length, dn / length, du / length, 50, 0, s, -s);
        Assert.True(shape.TryHit(cursor, 50, out double t, out _, out double north, out _, out _, out byte iconY));
        Assert.InRange(t, length - 2 * PickShape.FacingLift, length - PickShape.FacingLift / 2);
        Assert.InRange(north, pn - 2 * PickShape.FacingLift, pn);
        Assert.Equal(31, iconY);
    }

    [Fact]
    public void APickShapeThatFacesTheEyeIsHitFromStraightAboveByTheCameraRight()
    {
        // The editor's top view: straight down, north up. The picture lies flat, its up pointing north; its right is the camera's (east).
        PickShape shape = PickShape.StandingPicture(1, PickSurface.Billboard, 1, 1, 1, 1, 1, 0, SpriteRect.Of(32, 32, 0, 0), null, 0, 0);
        shape.FacesEye = true;
        var down = new PickRay(1, 1 + 1.9, 10, 1, 0, 0, -1, 50, 0, 0, -1);
        Assert.False(shape.TryHit(down, 50, out _, out _, out _, out _, out _, out _)); // no right: none can be had from a view straight down
        Assert.True(shape.TryHit(down with { RightEast = 1 }, 50, out double t, out _, out _, out _, out _, out byte iconY));
        Assert.Equal(10 - PickShape.FacingLift, t, 6);
        Assert.Equal(31, iconY);
        Assert.False(shape.TryHit(down with { RightEast = 1, OriginNorth = 1 - 0.1 }, 50, out _, out _, out _, out _, out _, out _)); // south of its feet
    }
}
