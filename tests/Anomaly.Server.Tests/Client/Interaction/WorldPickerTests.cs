using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;

namespace Anomaly.Server.Tests.Client.Interaction;

/// <summary>M4a P7: the world pick over fixtures (docs/ai/specs/interaction.md 4.1-4.3): every surface, walls hiding what is behind them, the table over the floor, icon-x and icon-y.</summary>
public class WorldPickerTests
{
    private const double Eye = 1.2;

    [Theory]
    [InlineData(1.2, 5)] // Captured mouse, level crosshair.
    [InlineData(6.0, 5)] // Camera above the walls, looking down.
    [InlineData(6.0, 7)] // Off-center cursor ray in third person.
    public void ThirdPersonPointingAccountsForTheCameraOffsetAndExcludesTheOwnBody(double cameraHeight, int targetY)
    {
        var world = new PickFixture { Width = 20, Height = 20 };
        world.Moving.Add(PickFixture.Mob(1, 4, 5));
        world.Moving.Add(PickFixture.Mob(20, 10, targetY));
        var picker = new WorldPicker(world);
        PickRay ray = PickFixture.RayTo(1, 5, PickFixture.CentreOf(10), PickFixture.CentreOf(targetY), Eye, eye: cameraHeight);
        double lead = WorldPicker.CameraLead(ray, PickFixture.CentreOf(4), PickFixture.CentreOf(5), Eye);
        Assert.True(lead > 0);
        Assert.NotEqual(20, picker.PickPoint(ray, 1).Id); // Camera-to-target distance exceeds view range.
        Assert.Equal(20, picker.PickPoint(ray, 1, lead).Id);
        world.Walls.Add((9, targetY, 1));
        Assert.NotEqual(20, picker.PickPoint(ray, 1, lead).Id);
    }

    // The map editor's camera stands beside a small map: a ray from outside the map's rectangle meets the walls and the things of the map as one from
    // inside does (it left the walk of the cells at its own cell, and passed through them to the floor behind).
    [Theory]
    [InlineData(-13.0, 12.0, 3.0, 1.0, 0.0)] // from the west, along a row's edge
    [InlineData(-13.0, 11.0, 3.0, 1.0, 0.0)] // from the west, along a row
    [InlineData(-40.0, 11.0, 5.0, 1.0, 0.02)] // from far off and higher, a little askew
    public void ARayFromOutsideTheMapMeetsItsWalls(double east, double north, double up, double dirEast, double dirNorth)
    {
        var world = new PickFixture { Width = 24, Height = 16 };
        for (int y = 4; y <= 9; y++)
            world.Walls.Add((4, y, 1));
        var picker = new WorldPicker(world);
        // three metres up, looking five degrees down: over the tiles before the wall, into its face
        double down = -Math.Sin(5 * Math.PI / 180), along = Math.Cos(5 * Math.PI / 180), length = Math.Sqrt(dirEast * dirEast + dirNorth * dirNorth);
        PickHit hit = picker.Pick(new PickRay(east, north, up, 1, dirEast / length * along, dirNorth / length * along, down, 2000), 0);
        Assert.True(hit.IsHit);
        Assert.Equal(PickSurface.Wall, hit.Surface);
        Assert.Equal(4, hit.X);
        Assert.InRange(hit.HitUp, 0.5, 1.9);
        Assert.Equal(6.0, hit.HitEast, 3);

        // and a ray that never crosses the map meets nothing
        Assert.False(picker.Pick(new PickRay(east, -30.0, up, 1, along, 0, down, 2000), 0).IsHit);
    }

    [Fact]
    public void ThirdPersonPointingDoesNotGainReachFromCameraDistance()
    {
        var world = new PickFixture { Width = 24 };
        world.Moving.Add(PickFixture.Mob(20, 12, 5)); // Eight tiles ahead of the character, outside tg's view radius.
        var picker = new WorldPicker(world);
        PickRay ray = PickFixture.Look(1, 5, 1, 0);
        double lead = WorldPicker.CameraLead(ray, PickFixture.CentreOf(4), PickFixture.CentreOf(5), Eye);
        Assert.NotEqual(20, picker.PickPoint(ray, 1, lead).Id);
    }

    [Fact]
    public void PointingRayFindsADistantObjectAndStopsAtAnOccludingWall()
    {
        var world = new PickFixture { Width = 20 };
        world.Moving.Add(PickFixture.Mob(20, 9, 3));
        var picker = new WorldPicker(world);
        PickRay ray = PickFixture.Look(3, 3, 1, 0);
        Assert.False(picker.Pick(ray, 1).IsHit); // Six tiles away, beyond ordinary interaction picking.
        Assert.Equal(20, picker.PickPoint(ray, 1).Id);
        world.Walls.Add((6, 3, 1));
        PickHit blocked = picker.PickPoint(ray, 1);
        Assert.Equal(TargetKind.Turf, blocked.Kind);
        Assert.Equal((6, 3, 1), (blocked.X, blocked.Y, blocked.Z));
    }

    [Fact]
    public void ABillboardIsHitOnItsOpaquePixelAndGivesTgsPixel()
    {
        var world = new PickFixture();
        world.Moving.Add(PickFixture.Mob(10, 5, 3));
        var picker = new WorldPicker(world);
        // Looking east along row 3 from tile (3, 3): the middle of the mob, 1.2 m up in a 2 m picture.
        PickHit hit = picker.Pick(PickFixture.Look(3, 3, 1, 0), exclude: 1);
        Assert.Equal(PickSurface.Billboard, hit.Surface);
        Assert.Equal(TargetKind.Entity, hit.Kind);
        Assert.Equal(10, hit.Id);
        Assert.Equal((5, 3, 1), (hit.X, hit.Y, hit.Z));
        Assert.Equal(4.0, hit.Distance, 6);
        // The picture is 32 pixels over 2 m: the viewer's right is south, the middle of the mob is pixel 17 of 32 and 1.2 m is the 20th row from the bottom.
        Assert.Equal((17, 20), (hit.IconX, hit.IconY));
    }

    [Fact]
    public void AClearPixelIsMissedAndTheNextThingBehindIsHit()
    {
        var world = new PickFixture();
        var picture = new Raster(32, 32);
        for (int row = 0; row < 32; row++)
        {
            for (int col = 0; col < 16; col++) // only the left half (the viewer's left, north when looking east) is drawn
                picture.Pixels[(row * 32 + col) * 4 + 3] = 255;
        }
        world.Moving.Add(PickFixture.Mob(10, 5, 3, picture));
        world.Moving.Add(PickFixture.Mob(11, 6, 3));
        var picker = new WorldPicker(world);
        PickHit hit = picker.Pick(PickFixture.Look(3, 3, 1, 0), exclude: 1);
        Assert.Equal(11, hit.Id); // the straight ray goes through the clear half of the first
        // Aimed a little north (the drawn half): the first mob.
        PickRay north = PickFixture.RayTo(3, 3, PickFixture.CentreOf(5), PickFixture.CentreOf(3) + 0.5, Eye);
        Assert.Equal(10, picker.Pick(north, exclude: 1).Id);
    }

    [Fact]
    public void TheOwnEntityIsNeverPicked()
    {
        var world = new PickFixture();
        world.Moving.Add(PickFixture.Mob(1, 4, 3));
        world.Moving.Add(PickFixture.Mob(2, 5, 3));
        var picker = new WorldPicker(world);
        Assert.Equal(2, picker.Pick(PickFixture.Look(3, 3, 1, 0), exclude: 1).Id);
    }

    [Fact]
    public void AStaticItemIsPickedAtTheHalfScaleOnItsPicture()
    {
        var world = new PickFixture();
        world.AddStatic(PickFixture.Item(12, 4, 3));
        var picker = new WorldPicker(world);
        // From the eye (1.2 m) toward the item's middle, 0.3 m up.
        PickRay aimed = PickFixture.RayTo(3, 3, PickFixture.CentreOf(4), PickFixture.CentreOf(3), 0.3);
        PickHit hit = picker.Pick(aimed, exclude: 1);
        Assert.Equal(PickSurface.StaticProp, hit.Surface);
        Assert.Equal(12, hit.Id);
        Assert.InRange(hit.IconX, 15, 18);
        Assert.InRange(hit.IconY, 15, 20);
        // Over the item's top (0.75 m at the half scale) the ray flies past it and meets the floor.
        PickRay over = PickFixture.RayTo(3, 3, PickFixture.CentreOf(4), PickFixture.CentreOf(3), 0.9, max: 20);
        Assert.Equal(PickSurface.Floor, picker.Pick(over, exclude: 1).Surface);
    }

    [Fact]
    public void AClosedAirlockIsHitAsAPieceAndAnOpenOneLetsTheRayThrough()
    {
        var closed = new PickFixture().WallColumn(8);
        closed.AddPiece(20, RenderKind.Airlock, 6, 3, ByondDir.East);
        PickHit hit = new WorldPicker(closed).Pick(PickFixture.Look(3, 3, 1, 0, 0, 20), exclude: 1);
        Assert.Equal(PickSurface.Piece, hit.Surface);
        Assert.Equal(20, hit.Id);
        Assert.Equal(PickFixture.CentreOf(6) - 0.1, hit.HitEast, 6);

        var open = new PickFixture().WallColumn(8);
        open.AddPiece(20, RenderKind.Airlock, 6, 3, ByondDir.East, open: true);
        PickHit through = new WorldPicker(open).Pick(PickFixture.Look(3, 3, 1, 0, 0, 20), exclude: 1);
        Assert.Equal(PickSurface.Wall, through.Surface); // the doorway is empty: the wall behind it
        Assert.Equal((8, 3), (through.X, through.Y));
    }

    [Fact]
    public void AnOpenWindoorLetsAClickThroughToTheTableBehindIt()
    {
        // The owner's report (2026-10-19): with a windoor open on a counter's west edge, a click at the table went to the door.
        PickFixture World(bool open)
        {
            var world = new PickFixture();
            world.AddPiece(20, RenderKind.Windoor, 6, 3, ByondDir.West, open: open);
            world.AddStatic(PickShape.BoxOf(21, PickSurface.Piece, 6, 3, 1, PickFixture.CentreOf(6) - 0.97, PickFixture.CentreOf(3) - 0.97, 0,
                PickFixture.CentreOf(6) + 0.97, PickFixture.CentreOf(3) + 0.97, 0.9, 2.0));
            return world;
        }
        PickRay atTable = PickFixture.RayTo(4, 3, PickFixture.CentreOf(6), PickFixture.CentreOf(3), 0.85);
        Assert.Equal(20, new WorldPicker(World(open: false)).Pick(atTable, exclude: 1).Id);
        Assert.Equal(21, new WorldPicker(World(open: true)).Pick(atTable, exclude: 1).Id);
    }

    [Fact]
    public void AnItemOnARacksShelfWinsOverTheRacksPictureButNotOverAShutWindoor()
    {
        // The owner's report (2026-10-19): a rack's items could not be taken. The rack's picture stands in the middle of its tile; an item on the back half
        // of a shelf is behind it, and is clicked over it (tg's item layer over the rack's).
        PickFixture World(bool windoor)
        {
            var world = new PickFixture();
            world.AddStatic(PickShape.StandingPicture(30, PickSurface.StaticProp, 6, 3, 1, PickFixture.CentreOf(6), PickFixture.CentreOf(3), 0,
                SpriteRect.Of(32, 32, 0, 0), Raster.Solid(32, 32, 0xFFFFFFFF), 0, 0));
            PickShape item = PickShape.StandingPicture(31, PickSurface.StaticProp, 6, 3, 1, PickFixture.CentreOf(6) + 0.4, PickFixture.CentreOf(3), 0.6,
                SpriteRect.Of(32, 32, 0, 0).AsItem(0.5, 0), Raster.Solid(32, 32, 0xFFFFFFFF), 0, 0);
            item.OnTop = true;
            world.AddStatic(item);
            if (windoor)
                world.AddPiece(20, RenderKind.Windoor, 6, 3, ByondDir.West);
            return world;
        }
        PickRay atItem = PickFixture.RayTo(4, 3, PickFixture.CentreOf(6) + 0.4, PickFixture.CentreOf(3), 0.9);
        Assert.Equal(31, new WorldPicker(World(windoor: false)).Pick(atItem, exclude: 1).Id);
        Assert.Equal(20, new WorldPicker(World(windoor: true)).Pick(atItem, exclude: 1).Id);
        // A thing of another tile in front of the rack keeps the click.
        PickFixture blocked = World(windoor: false);
        blocked.AddStatic(PickFixture.Mob(40, 5, 3));
        Assert.Equal(40, new WorldPicker(blocked).Pick(atItem, exclude: 1).Id);
    }

    [Fact]
    public void AModelsBoxGivesTheClickToAMobOnItsTile()
    {
        // The owner, 2026-10-20: every 3D model is picked on its model's box; a chair's box holds the mob sitting on it, which keeps the click.
        PickShape chair = PickShape.BoxOf(30, PickSurface.StaticProp, 6, 3, 1, PickFixture.CentreOf(6) - 0.6, PickFixture.CentreOf(3) - 0.6, 0,
            PickFixture.CentreOf(6) + 0.6, PickFixture.CentreOf(3) + 0.6, 1.0, 2.0);
        chair.Yields = true;
        var world = new PickFixture();
        world.AddStatic(chair);
        PickRay atSeat = PickFixture.RayTo(4, 3, PickFixture.CentreOf(6), PickFixture.CentreOf(3), 0.6);
        Assert.Equal(30, new WorldPicker(world).Pick(atSeat, exclude: 1).Id);
        world.Moving.Add(PickFixture.Mob(10, 6, 3));
        Assert.Equal(10, new WorldPicker(world).Pick(atSeat, exclude: 1).Id);
    }

    [Fact]
    public void ADoorBehindAModelOnItsTileDoesNotTakeTheClick()
    {
        // The owner's report (2026-11-05): MetaStation's Cargo Desk, a bell on a table with an open firelock and shutters and a shut windoor on the far edge: the
        // ray went through the bell's box to the windoor, which took the click.
        double east = PickFixture.CentreOf(6), north = PickFixture.CentreOf(3);
        var world = new PickFixture();
        world.AddPiece(20, RenderKind.Windoor, 6, 3, ByondDir.South);
        world.AddPiece(21, RenderKind.Firelock, 6, 3, ByondDir.North, open: true);
        world.AddPiece(22, RenderKind.PodDoor, 6, 3, ByondDir.North, open: true);
        world.AddPiece(23, RenderKind.Table, 6, 3, 0);
        foreach (PickShape piece in world.Static[(6, 3, 1)].Where(s => s.EntityId == 23))
            piece.Yields = true; // as MapRenderer.CollectStatic makes a table's
        PickShape bell = PickShape.BoxOf(30, PickSurface.StaticProp, 6, 3, 1, east + 0.14, north - 0.3, WorldGeometry.TableTop - 0.04, east + 0.74, north + 0.3, WorldGeometry.TableTop + 0.3, 2.0);
        bell.Yields = true;
        world.AddStatic(bell);

        PickRay atBell = PickFixture.RayTo(6, 4, east + 0.44, north, WorldGeometry.TableTop + 0.1, eye: WorldScale.DefaultEyeHeight);
        Assert.Equal(30, new WorldPicker(world).Pick(atBell, exclude: 1).Id);
        PickRay atTable = PickFixture.RayTo(6, 4, east - 0.44, north, WorldGeometry.TableTop, eye: WorldScale.DefaultEyeHeight);
        Assert.Equal(23, new WorldPicker(world).Pick(atTable, exclude: 1).Id);
        // From the other side the shut windoor stands before the bell and keeps the click.
        PickRay throughGlass = PickFixture.RayTo(6, 2, east + 0.44, north, WorldGeometry.TableTop + 0.1, eye: WorldScale.DefaultEyeHeight);
        Assert.Equal(20, new WorldPicker(world).Pick(throughGlass, exclude: 1).Id);
    }

    [Fact]
    public void AWindowSlabAndATableTopAreBoxes()
    {
        var world = new PickFixture();
        world.AddPiece(21, RenderKind.FullWindow, 5, 3, 0, Joints.East | Joints.West);
        world.AddPiece(30, RenderKind.Table, 4, 5, 0);
        var picker = new WorldPicker(world);
        // The window runs east-west through its tile: looking north along x = tile 5's middle meets it; looking east along its row runs along the slab.
        PickHit window = picker.Pick(PickFixture.Look(5, 1, 0, 1), exclude: 1);
        Assert.Equal(21, window.Id);
        Assert.Equal(PickSurface.Piece, window.Surface);
        // A table: aimed at the middle of its top from above-front, the top face gives the tile-relative middle (16, 16).
        PickRay down = PickFixture.RayTo(4, 3, PickFixture.CentreOf(4), PickFixture.CentreOf(5), WorldGeometry.TableTop);
        PickHit top = picker.Pick(down, exclude: 1);
        Assert.Equal((PickSurface.Piece, 30L), (top.Surface, top.Id));
        Assert.Equal((16, 16), (top.IconX, top.IconY));
    }

    [Fact]
    public void ATableInFrontOfItsFloorWinsOverTheFloor()
    {
        var world = new PickFixture();
        world.AddPiece(30, RenderKind.Table, 4, 3, 0);
        var picker = new WorldPicker(world);
        // The ray comes down onto the table top before the floor behind it.
        PickRay onto = PickFixture.RayTo(2, 3, PickFixture.CentreOf(4) + 0.4, PickFixture.CentreOf(3), 0.0);
        PickHit hit = picker.Pick(onto, exclude: 1);
        Assert.Equal(PickSurface.Piece, hit.Surface);
        // The same ray without the table is a floor hit on the cell it ends in.
        PickHit bare = new WorldPicker(new PickFixture()).Pick(onto, exclude: 1);
        Assert.Equal(PickSurface.Floor, bare.Surface);
        Assert.Equal((4, 3), (bare.X, bare.Y));
    }

    [Fact]
    public void TheFloorGivesTheCellAndTheTileRelativePoint()
    {
        var picker = new WorldPicker(new PickFixture());
        // From tile (3, 3) 45 degrees down toward the east: the floor 1.2 m east of the eye, at east 6.2, north 5 (tile (4, 3)).
        PickHit hit = picker.Pick(PickFixture.Look(3, 3, 1, 0, -45), exclude: 1);
        Assert.Equal(PickSurface.Floor, hit.Surface);
        Assert.Equal(TargetKind.Turf, hit.Kind);
        Assert.Equal((4, 3, 1), (hit.X, hit.Y, hit.Z));
        Assert.Equal(6.2, hit.HitEast, 6);
        Assert.Equal((4, 16), (hit.IconX, hit.IconY)); // (6.2 - 6) / 2 * 32 = 3.2 rounds up to 4; north is the tile's middle
        Assert.Equal(1.2 * Math.Sqrt(2), hit.Distance, 6);
    }

    [Fact]
    public void AWallFaceIsAHitAndHidesWhatIsBehindIt()
    {
        var world = new PickFixture().WallColumn(7);
        world.Moving.Add(PickFixture.Mob(11, 9, 3)); // behind the wall
        world.AddPiece(22, RenderKind.Airlock, 9, 5, ByondDir.North);
        var picker = new WorldPicker(world);
        PickHit hit = picker.Pick(PickFixture.Look(3, 3, 1, 0, 0, 20), exclude: 1);
        Assert.Equal(PickSurface.Wall, hit.Surface);
        Assert.Equal(TargetKind.Turf, hit.Kind);
        Assert.Equal((7, 3, 1), (hit.X, hit.Y, hit.Z));
        Assert.Equal(PickFixture.CentreOf(7) - 1.0, hit.HitEast, 6); // the wall's west face
        Assert.Equal((16, 20), (hit.IconX, hit.IconY)); // the face point along the wall (the tile's middle, 16 rounded up) and the height (1.2 of 2 m)
        // Aimed at the mob behind the wall, the wall still wins.
        PickRay at = PickFixture.RayTo(3, 3, PickFixture.CentreOf(9), PickFixture.CentreOf(3), 1.0, 20);
        Assert.Equal(PickSurface.Wall, picker.Pick(at, exclude: 1).Surface);
        Assert.False(picker.Pick(at, exclude: 1, entitiesOnly: true).IsHit);
    }

    [Fact]
    public void FromAboveTheWallsARayHitsAWallTopOrPassesOverIt()
    {
        // Third-person play: the camera hangs 4 m up behind the mob, above the 2 m walls.
        var world = new PickFixture { Width = 10, Height = 10 };
        for (int y = 1; y <= 10; y++)
            world.Walls.Add((5, y, 1)); // east 8 to 10
        var picker = new WorldPicker(world);
        PickRay Down(double slope)
        {
            double h = Math.Sqrt(1 + slope * slope);
            return new PickRay(PickFixture.CentreOf(2), PickFixture.CentreOf(5), 4.0, 1, 1 / h, 0, -slope / h, 30);
        }
        // Over the wall's cell at 2.25 m, down to 2 m inside it: its top.
        PickHit top = picker.Pick(Down(0.35), exclude: 1);
        Assert.Equal(PickSurface.Wall, top.Surface);
        Assert.Equal((5, 5), (top.X, top.Y));
        Assert.Equal(2.0, top.HitUp, 6);
        // Still 2.25 m up when it leaves the cell: over the wall to the floor beyond.
        PickHit over = picker.Pick(Down(0.25), exclude: 1);
        Assert.Equal(PickSurface.Floor, over.Surface);
        Assert.Equal((10, 5), (over.X, over.Y));
        // At the eye's height the wall is a face, as before.
        Assert.Equal(PickSurface.Wall, picker.Pick(PickFixture.Look(3, 5, 1, 0), exclude: 1).Surface);
    }

    [Fact]
    public void WithTheTRayOnAFlatThingInAWallCellIsPickedThroughTheWall()
    {
        // DV356: a visible pipe mapped into the wall (5, 5) lies flat on the cell's floor, under the wall's model; a grille there is no flat thing
        var world = new PickFixture { Width = 10, Height = 10 };
        world.Walls.Add((5, 5, 1));
        world.AddStatic(PickShape.FlatPicture(30, PickSurface.StaticProp, 5, 5, 1, PickFixture.CentreOf(5), PickFixture.CentreOf(5), 0.02,
            SpriteRect.OfFlat(32, 32, 0, 0), Raster.Solid(32, 32, 0xFFFFFFFF), 0, 0));
        world.AddPiece(31, RenderKind.Grille, 5, 5, ByondDir.North);
        var picker = new WorldPicker(world);
        // From 4 m up over (3, 5), down into the wall's cell: its top at 2 m, its floor at 9.25 m east (inside the cell, 8 to 10)
        double slope = 4.0 / (9.25 - PickFixture.CentreOf(3));
        double h = Math.Sqrt(1 + slope * slope);
        var down = new PickRay(PickFixture.CentreOf(3), PickFixture.CentreOf(5), 4.0, 1, 1 / h, 0, -slope / h, 30);
        Assert.Equal(PickSurface.Wall, picker.Pick(down, exclude: 1).Surface);
        picker.SeeIntoWalls = true;
        PickHit pipe = picker.Pick(down, exclude: 1);
        Assert.Equal(TargetKind.Entity, pipe.Kind);
        Assert.Equal(30, pipe.Id);
        // At eye height the ray leaves the cell long before the floor: the wall face, not the grille
        Assert.Equal(PickSurface.Wall, picker.Pick(PickFixture.Look(3, 5, 1, 0), exclude: 1).Surface);
    }

    [Fact]
    public void TheCeilingIsAHitOnlyWhereThereIsOne()
    {
        var world = new PickFixture();
        world.NoCeiling.Add((5, 3, 1));
        var picker = new WorldPicker(world);
        PickHit up = picker.Pick(PickFixture.Look(3, 3, 1, 0, 45), exclude: 1);
        Assert.Equal(PickSurface.Ceiling, up.Surface);
        Assert.Equal((3, 3), (up.X, up.Y)); // the ceiling is 0.8 m above the eye: 0.8 m east, still in the eye's tile
        Assert.Equal(2.0, up.HitUp, 6);
        PickHit gap = picker.Pick(PickFixture.Look(4, 3, 1, 0, 24), exclude: 1); // 0.8 m up in 1.8 m east: the cell (5, 3)
        Assert.False(gap.IsHit); // the ceiling cell over (5, 3) is open to space
    }

    [Fact]
    public void EntitiesOnlyDropsTheCellsButKeepsTheEntities()
    {
        var world = new PickFixture();
        world.Moving.Add(PickFixture.Mob(10, 5, 3));
        var picker = new WorldPicker(world);
        Assert.False(picker.Pick(PickFixture.Look(3, 3, 1, 0, -45), exclude: 1, entitiesOnly: true).IsHit);
        Assert.Equal(10, picker.Pick(PickFixture.Look(3, 3, 1, 0), exclude: 1, entitiesOnly: true).Id);
    }

    [Fact]
    public void TheOrdinaryRangeIsFourTilesAndThrowModeHasFourteenMetres()
    {
        var world = new PickFixture();
        world.Moving.Add(PickFixture.Mob(10, 8, 3)); // 10 m from the eye
        var picker = new WorldPicker(world);
        Assert.False(picker.Pick(PickFixture.Look(3, 3, 1, 0), exclude: 1).IsHit);
        Assert.Equal(10, picker.PickThrow(PickFixture.Look(3, 3, 1, 0), exclude: 1).Id);
        // With nothing in the way a throw aimed past the room still names a cell: the floor where the view meets it, or the cell at the range.
        PickHit floor = picker.PickThrow(PickFixture.Look(3, 5, 0, 1, -10), exclude: 1);
        Assert.Equal(TargetKind.Turf, floor.Kind);
        PickHit far = picker.PickThrow(PickFixture.Look(1, 5, 1, 0, 0), exclude: 1);
        Assert.Equal(TargetKind.Turf, far.Kind);
        Assert.Equal(PickFixture.CentreOf(1) + WorldPicker.ThrowRange, far.HitEast, 6);
    }

    [Fact]
    public void AClickIntoOpenSpaceOrDownALongCorridorStillLandsOnATurfInView()
    {
        // The space play test (2026-10-15): looking out into space (no ceiling, the floor too far away for the 4-tile pick) a click hit nothing
        // and was never sent, so the extinguisher could not be sprayed (nor push its user away) in space; BYOND's screen always has a turf
        // under the mouse.
        var world = new PickFixture { Width = 20, Height = 10 };
        for (int x = 1; x <= 20; x++)
            for (int y = 1; y <= 10; y++)
                world.NoCeiling.Add((x, y, 1));
        var picker = new WorldPicker(world);
        PickRay level = PickFixture.Look(2, 5, 1, 0);
        Assert.False(picker.Pick(level, exclude: 1).IsHit);
        PickHit space = picker.PickInView(level, exclude: 1);
        Assert.Equal(TargetKind.Turf, space.Kind);
        Assert.Equal((2 + 7, 5), (space.X, space.Y)); // the view's 7 tiles along the look
        Assert.Equal(PickFixture.CentreOf(2) + WorldPicker.ViewRange, space.HitEast, 6);
        PickHit up = picker.PickInView(PickFixture.Look(2, 5, 1, 0, 30), exclude: 1);
        Assert.Equal(TargetKind.Turf, up.Kind); // looking up into space: no ceiling there, still a turf ahead
        Assert.True(up.X > 2 && up.Y == 5);
        // Slightly down a long corridor: the floor where the view meets it, past the ordinary range.
        PickRay down = PickFixture.Look(2, 5, 1, 0, -6);
        Assert.False(picker.Pick(down, exclude: 1).IsHit);
        PickHit floor = picker.PickInView(down, exclude: 1);
        Assert.Equal((PickSurface.Floor, TargetKind.Turf, 5), (floor.Surface, floor.Kind, floor.Y));
        Assert.Equal(2 + 6, floor.X); // 1.2 m / tan 6 degrees = 11.4 m: the sixth tile ahead
        // Something within the view range is still what is clicked.
        world.Moving.Add(PickFixture.Mob(10, 7, 5));
        Assert.Equal(10, picker.PickInView(level, exclude: 1).Id);
    }

    [Fact]
    public void TheReachMirrorIsTheOwnTileAndItsNeighbours()
    {
        var near = new PickHit(PickSurface.Floor, TargetKind.Turf, 0, 4, 4, 1, 1, 0, 0, 0, 1, 1);
        var far = near with { X = 6 };
        var up = near with { Z = 2 };
        Assert.True(WorldPicker.InReach(3, 3, 1, near));
        Assert.False(WorldPicker.InReach(3, 3, 1, far));
        Assert.False(WorldPicker.InReach(3, 3, 1, up));
        Assert.True(WorldPicker.InReach(3, 3, 1, up, opensUp: true)); // DV425: under a hole the level above is in reach
        Assert.False(WorldPicker.InReach(3, 3, 1, far with { Z = 2 }, opensUp: true));
        Assert.False(WorldPicker.InReach(3, 3, 1, PickHit.None));
    }

    [Fact]
    public void ALyingMobIsAFlatPictureOnTheFloor()
    {
        var world = new PickFixture();
        SpriteRect rect = SpriteRect.OfFlat(32, 32, 0, 0);
        world.Moving.Add(PickShape.FlatPicture(10, PickSurface.Billboard, 4, 3, 1, PickFixture.CentreOf(4), PickFixture.CentreOf(3), 0.02, rect, Raster.Solid(32, 32, 0xFFFFFFFF), 0, 0));
        var picker = new WorldPicker(world);
        PickRay down = PickFixture.RayTo(3, 3, PickFixture.CentreOf(4) + 0.52, PickFixture.CentreOf(3) + 0.27, 0.02);
        PickHit hit = picker.Pick(down, exclude: 1);
        Assert.Equal(PickSurface.Billboard, hit.Surface);
        Assert.Equal(10, hit.Id);
        Assert.Equal((25, 21), (hit.IconX, hit.IconY)); // 0.52 m east of the middle is 8.3 pixels past 16 (pixel 25); 0.27 m north 4.3 pixels past 16 (21)
    }

    [Fact]
    public void PieceBoxesTurnWithTheirFace()
    {
        var airlock = new Placed(1, PlacedKind.Piece, RenderKind.Airlock, 1, 1, 1, 0, ByondDir.North, 0f, 0f, 0f, default, default, null);
        PieceBox along = Assert.Single(PieceBoxes.For(airlock, 2.0, open: false));
        Assert.Equal(1.0, along.MaxEast, 6); // the plane runs along x
        Assert.True(along.MaxNorth <= 0.15);
        PieceBox turned = Assert.Single(PieceBoxes.For(airlock with { Face = ByondDir.East }, 2.0, open: false));
        Assert.Equal(1.0, turned.MaxNorth, 6); // along y
        Assert.True(turned.MaxEast <= 0.15);
        Assert.Equal(2, PieceBoxes.For(airlock, 2.0, open: true).Count);
        // an open pod door is picked by the lip it leaves under the ceiling
        PieceBox lip = Assert.Single(PieceBoxes.For(airlock with { Shape = RenderKind.PodDoor }, 2.0, open: true));
        Assert.Equal(2.0 - WorldGeometry.PodDoorLip, lip.MinUp, 3);
        Assert.Equal(2.0, lip.MaxUp, 3);
        // A directional window stands on its face's edge: on the north side it is north of the tile centre.
        var window = airlock with { Shape = RenderKind.DirectionalWindow };
        Assert.True(PieceBoxes.For(window, 2.0, false)[0].MinNorth > 0.8);
        Assert.True(PieceBoxes.For(window with { Face = ByondDir.West }, 2.0, false)[0].MaxEast < -0.8);
    }

    [Fact]
    public void ALampIsPickedOnItsWallFaceAtItsHeightAndSize()
    {
        // D142 (the owner's play test 2026-10-14: a lamp could not be aimed at, so its tube could not be taken out). A small bulb on the
        // north wall, 1.85 m up, its box scaled to a 0.2 m wide picture and moved 0.3 m east by its pixel_x.
        var lamp = new Placed(7, PlacedKind.Piece, RenderKind.LightFixture, 1, 1, 1, ByondDir.North, ByondDir.North, 1.85f, 0.3f, 0f, default, default, null);
        PieceBox box = PieceBoxes.Lamp(lamp, 0.1, 0.12);
        Assert.Equal(1.0, box.MaxNorth, 6); // against the wall face
        Assert.True(box.MinNorth > 0.7 && box.MinNorth < 0.85);
        Assert.Equal(0.2, box.MinEast, 6); // 0.3 east, half width 0.1
        Assert.Equal(0.4, box.MaxEast, 6);
        Assert.Equal(1.73, box.MinUp, 6);
        Assert.Equal(1.97, box.MaxUp, 6);
        // On the west wall it runs along the wall (north), is never thinner than the slab minimum and moves north by its pixel_x.
        PieceBox west = PieceBoxes.Lamp(lamp with { Face = ByondDir.West }, 0.02, 0.02);
        Assert.Equal(-1.0, west.MinEast, 6);
        Assert.Equal(0.3 - PieceBoxes.MinHalfThickness, west.MinNorth, 6);
        Assert.Equal(1.85 + PieceBoxes.MinHalfThickness, west.MaxUp, 6);
    }
}
