using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M7x;

/// <summary>
/// M7x slice A (D177, DV95): the 3D crossing of an aimed shot's line with a carbon's drawn body parts (ShotGeometry.Cross, the committed
/// body-part mask table): the front, side and back frames, the head at the head's rows, the chest, the gap between the legs and over a
/// shoulder, a lying body hit only where the line crosses it, and a picture off its tile's centre.
/// </summary>
public class ShotGeometryTests
{
    private const int Tx = 8, Ty = 4;
    private const double Cx = Tx * 32.0, Cy = Ty * 32.0; // the target tile's centre in tile pixels

    private static (AudioRoom R, mob_living_carbon_human Bob) Room(ulong seed)
    {
        var r = new AudioRoom(14, 7, seed: seed);
        r.Step(20);
        mob_living_carbon_human bob = r.Dummy(Tx, Ty);
        r.Step(2);
        return (r, bob);
    }

    // A level or sloped line from five tiles off along `angle`, which meets the picture's plane at tg's (iconX, iconY) of the
    // standing picture (mob scale 0.8, its feet on the floor: the human's lowest row is icon-y 1).
    private static string ZoneOfShot(AudioRoom r, mob_living target, double angle, double iconX, double iconY, double slope = 0, double offX = 0, double offY = 0)
        => r.In(() =>
        {
            (double ux, double uy) = ShotGeometry.Heading(angle);
            double lateral = (iconX - 0.5 - 16) * 0.8, height = (iconY - 0.5) * 0.8;
            double cx = Cx + offX, cy = Cy + offY;
            double px = cx + uy * lateral, py = cy - ux * lateral; // the point of the plane at that lateral, along the viewer's right (uy, -ux)
            var p = Dm.@new<obj_projectile_beam_emitter_hitscan>(locate(3, Ty, 1));
            p.angle = (float)angle;
            p.dm_aimed = true;
            p.dm_shot = true;
            p.dm_origin_x = (float)(px - ux * 160);
            p.dm_origin_y = (float)(py - uy * 160);
            p.dm_slope = (float)slope;
            p.dm_origin_h = (float)(height - slope * 160);
            ShotGeometry.Crossing result = ShotGeometry.Cross(p, target, out string zone);
            qdel(p);
            return result == ShotGeometry.Crossing.Hit ? zone : null;
        });

    [Fact]
    public void FromTheFrontTheHeadChestArmsAndLegsAreWhereTheyAreDrawnAndTheGapsMiss()
    {
        (AudioRoom r, mob_living_carbon_human bob) = Room(7801);
        r.In(() => bob.dir = WEST); // it faces the shooter in the west: the front (south) frame
        Assert.Equal(BODY_ZONE_HEAD, ZoneOfShot(r, bob, 90, 16, 27));
        Assert.Equal(BODY_ZONE_CHEST, ZoneOfShot(r, bob, 90, 16, 18));
        Assert.Equal(BODY_ZONE_R_ARM, ZoneOfShot(r, bob, 90, 9, 17)); // the mob's right on the viewer's left
        Assert.Equal(BODY_ZONE_L_ARM, ZoneOfShot(r, bob, 90, 23, 17));
        Assert.Equal(BODY_ZONE_R_LEG, ZoneOfShot(r, bob, 90, 13, 4));
        Assert.Equal(BODY_ZONE_L_LEG, ZoneOfShot(r, bob, 90, 19, 4));
        Assert.Null(ZoneOfShot(r, bob, 90, 16, 4)); // between the legs
        Assert.Null(ZoneOfShot(r, bob, 90, 11, 25)); // over the right shoulder, beside the neck
        Assert.Null(ZoneOfShot(r, bob, 90, 16, 31)); // over the head
        Assert.Null(ZoneOfShot(r, bob, 90, 2, 18)); // beside the body
        // the eye's level line (1.3 m: 20.8 px) of a standing shooter crosses a standing man's head
        Assert.Equal(BODY_ZONE_HEAD, ZoneOfShot(r, bob, 90, 16, 20.8 / 0.8 + 0.5));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void TheBackAndTheSideFramesSwapTheArmsAndNarrowTheBody()
    {
        (AudioRoom r, mob_living_carbon_human bob) = Room(7802);
        r.In(() => bob.dir = WEST);
        // from the east (heading west) the viewer is behind it: the back (north) frame, the mob's right arm on the viewer's right
        Assert.Equal(BODY_ZONE_HEAD, ZoneOfShot(r, bob, 270, 16, 27));
        Assert.Equal(BODY_ZONE_R_ARM, ZoneOfShot(r, bob, 270, 23, 17));
        Assert.Equal(BODY_ZONE_L_ARM, ZoneOfShot(r, bob, 270, 9, 17));
        // from the south (heading north) the viewer sees its left side (west frame): narrower, the chest where the front's arm was
        Assert.Equal(BODY_ZONE_HEAD, ZoneOfShot(r, bob, 0, 16, 27));
        Assert.Null(ZoneOfShot(r, bob, 0, 9, 17));
        Assert.Equal(BODY_ZONE_CHEST, ZoneOfShot(r, bob, 0, 14, 17));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void ALyingBodyIsHitOnlyWhereTheLineCrossesItsTurnedPicture()
    {
        (AudioRoom r, mob_living_carbon_human bob) = Room(7803);
        r.In(() => Waiting.NoSleep(bob.set_resting(Dm.TRUE, Dm.TRUE)));
        r.Step(5);
        Assert.Equal(LYING_DOWN, r.In(() => bob.body_position));
        // the standing head's line passes over the body on the floor
        Assert.Null(ZoneOfShot(r, bob, 90, 16, 27));
        Assert.Null(ZoneOfShot(r, bob, 90, 16, 20.8 / 0.8 + 0.5));
        // a line pitched down onto the body (its lowest rows) crosses a body part; the turned picture holds the head at one end
        var zones = new HashSet<string>();
        for (int x = 1; x <= 32; x++)
            for (int y = 1; y <= 8; y++)
                if (ZoneOfShot(r, bob, 90, x, y, slope: -0.12) is string zone)
                    zones.Add(zone);
        Assert.Contains(BODY_ZONE_HEAD, zones);
        Assert.Contains(BODY_ZONE_CHEST, zones);
        Assert.True(zones.Count >= 4, string.Join(",", zones));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void APictureOffItsTilesCentreIsCrossedWhereItStandsAndALineBehindTheStartMisses()
    {
        (AudioRoom r, mob_living_carbon_human bob) = Room(7804);
        r.In(() => bob.dir = WEST);
        // tg's pixel offsets move the picture on the ground at the mob scale (the client's ground offset): 10 pixels north
        r.In(() => bob.pixel_y = 10);
        Assert.Null(ZoneOfShot(r, bob, 90, 16, 27)); // the tile centre's head line now passes 8 px south of the head
        Assert.Equal(BODY_ZONE_HEAD, ZoneOfShot(r, bob, 90, 16, 27, offY: 8));
        r.In(() => bob.pixel_y = 0);
        // a crossing behind the line's start: the line starts past the mob
        string behind = r.In(() =>
        {
            var p = Dm.@new<obj_projectile_beam_emitter_hitscan>(locate(10, Ty, 1));
            (p.angle, p.dm_aimed, p.dm_shot, p.dm_origin_x, p.dm_origin_y, p.dm_origin_h) = (90f, true, true, (float)(Cx + 40), (float)Cy, 21.2f);
            ShotGeometry.Crossing result = ShotGeometry.Cross(p, bob, out string zone);
            return result == ShotGeometry.Crossing.Hit ? zone : "miss";
        });
        Assert.Equal("miss", behind);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void OfTwoBodiesOnOneTurfTheNearestAlongTheShotIsHit()
    {
        (AudioRoom r, mob_living_carbon_human bob) = Room(7806);
        mob_living_carbon_human ann = r.Dummy(Tx, Ty);
        r.In(() =>
        {
            (bob.pixel_x, ann.pixel_x) = (10, -10); // Ann stands 8 px west of Bob on the same turf (the mob scale)
            (bob.dir, ann.dir) = (WEST, WEST);
        });
        (mob_living hit, string zone) = r.In(() =>
        {
            var p = Dm.@new<obj_projectile_beam_emitter_hitscan>(locate(3, Ty, 1));
            (p.angle, p.dm_aimed, p.dm_shot, p.dm_origin_x, p.dm_origin_y, p.dm_origin_h) = (90f, true, true, 96f, (float)Cy, 21.2f);
            mob_living nearest = ShotGeometry.Nearest(p, Dm.list(bob, ann));
            return (nearest, p.def_zone);
        });
        Assert.Same(ann, hit);
        Assert.Equal(BODY_ZONE_HEAD, zone);
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AHitLightsTheHitBodyPartForAMoment()
    {
        (AudioRoom r, mob_living_carbon_human bob) = Room(7807);
        int before = r.In(() => (int)bob.overlays.len);
        r.In(() => ShotGeometry.ShowHit(bob, BODY_ZONE_CHEST));
        r.Step(1);
        Assert.Equal(before + 1, r.In(() => (int)bob.overlays.len)); // DV400: the chest's images as one overlay
        r.Step(20);
        Assert.Equal(before, r.In(() => (int)bob.overlays.len));
        r.AssertNoRuntimes();
    }

    [Fact]
    public void AShotsLineEntersAWallsTileAtItsSide()
    {
        // from tile (6, 6)'s centre heading west: tile (4, 6)'s east side is 16 px east of its centre, the point half a pixel before it
        (double x, double y) = ShotGeometry.EnterTile(192, 192, 270, 4, 6).Value;
        Assert.Equal(144.5, x, 6);
        Assert.Equal(192, y, 6);
        // a slanted line enters by the side it reaches last
        (x, y) = ShotGeometry.EnterTile(192, 192, 225, 4, 4).Value;
        Assert.Equal(144.35, x, 2);
        Assert.Equal(144.35, y, 2);
        Assert.Null(ShotGeometry.EnterTile(192, 192, 0, 4, 6)); // it passes the tile by
        Assert.Null(ShotGeometry.EnterTile(130, 192, 270, 4, 6)); // it starts in the tile
    }

    [Fact]
    public void ANonCarbonKeepsTgsRule()
    {
        (AudioRoom r, _) = Room(7805);
        mob_living_basic_mouse mouse = r.In(() => Dm.@new<mob_living_basic_mouse>(locate(Tx, Ty + 1, 1)));
        ShotGeometry.Crossing result = r.In(() =>
        {
            var p = Dm.@new<obj_projectile_beam_emitter_hitscan>(locate(3, Ty + 1, 1));
            (p.angle, p.dm_aimed, p.dm_shot, p.dm_origin_x, p.dm_origin_y, p.dm_origin_h) = (90f, true, true, 96f, (float)(Cy + 32), 20.8f);
            return ShotGeometry.Cross(p, mouse, out _);
        });
        Assert.Equal(ShotGeometry.Crossing.NotApplicable, result);
    }
}
