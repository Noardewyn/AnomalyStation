using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;
using Anomaly.Server.Tests.Client.Render;
using System.Numerics;
using Xunit.Abstractions;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// A security camera stands out of its wall at a right angle and never turns (the owner, 2026-10-14): tg's side view of it on a plane through the
/// wall's normal, whose own frames play the pan as tg plays them.
/// </summary>
public class WallFinTests(ITestOutputHelper output)
{
    [Fact]
    public void A_camera_on_a_wall_is_a_fin_and_a_sign_is_not()
    {
        Assert.True(WallMounts.TryPlace("/obj/machinery/camera/directional/north", 10, 10, 1, ByondDirConst.North, 0, 0, (x, y, z) => y == 11, out byte face, out _, out _));
        Assert.Equal(ByondDirConst.North, face);
        Assert.True(PropKinds.IsWallFin("/obj/machinery/camera/autoname/directional/east"));
        Assert.False(PropKinds.IsWallFin("/obj/structure/sign/warning"));
        var camera = new PlanEntity(1, "/obj/machinery/camera/directional/north", 10, 10, 1, ByondDirConst.North, TableRef.Public(1), 0, 0, LightState.None, "camera");
        Assert.True(ChunkPlanner.TryPlace(camera, (x, y, z) => y == 11, out Placed placed));
        Assert.Equal((PlacedKind.WallDecal, true), (placed.Kind, placed.Fin));
    }

    [Theory]
    [InlineData(ByondDirConst.North)]
    [InlineData(ByondDirConst.South)]
    [InlineData(ByondDirConst.East)]
    [InlineData(ByondDirConst.West)]
    public void The_fin_stands_on_the_walls_normal_with_its_mount_edge_on_the_face(byte face)
    {
        var p = new Placed(1, PlacedKind.WallDecal, RenderKind.Billboard, 10, 20, 1, face, face, WorldScale.CameraHeight, 0f, 0f, TableRef.Public(1), LightState.None, null, Fin: true);
        (Vector3 origin, float yaw) = WallMounts.FinPlacement(p, 0.5f);
        var centre = new Vector3(TileSpace.WorldX(10), 0, TileSpace.WorldZ(20));
        (int dx, int dy) = WallMounts.Step(face);
        var toWall = new Vector3(dx, 0, -dy);
        Assert.Equal(0f, Vector3.Distance(centre, origin with { Y = 0 }), 4);
        // The quad's plane (its local x) runs along the wall's normal, and its mount edge (+x for the east picture, -x for the west) is toward the wall.
        var localX = new Vector3(MathF.Cos(yaw), 0, -MathF.Sin(yaw));
        float sign = WallMounts.FinPictureDir(face) == ByondDirConst.East ? 1f : -1f;
        Assert.Equal(1f, Vector3.Dot(localX * sign, toWall), 4);
    }

    [RealIconsFact]
    public void Tgs_side_pictures_of_a_camera_are_its_east_and_west_ones()
    {
        // icons/obj/machines/camera.dmi: the east picture is the camera at the tile's east edge with its lens to the west, wider than tall
        var library = RealIcons.NewLibrary();
        var tables = new TestTables();
        TableRef camera = tables.Add("icons/obj/machines/camera.dmi", "camera");
        var compositor = new AppearanceCompositor(library);
        foreach (byte dir in new[] { ByondDirConst.East, ByondDirConst.West, ByondDirConst.North, ByondDirConst.South })
        {
            Assert.True(compositor.TryCompose(tables, camera, dir, 0.9, out ComposedRaster raster));
            (int minX, int maxX, int minY, int maxY) = Box(raster.Image);
            output.WriteLine($"dir {dir}: {raster.Image.Width}x{raster.Image.Height} origin {raster.OriginX},{raster.OriginY} drawn x {minX}..{maxX} y {minY}..{maxY}");
            bool side = dir is ByondDirConst.East or ByondDirConst.West;
            Assert.Equal(side, maxX - minX > maxY - minY);
        }
        // The pan plays the east picture's own frames: 7 of them, rewinding, the lens moving across the side view.
        IReadOnlyList<IIconState> animated = compositor.AnimatedStates(tables, camera, ByondDirConst.East);
        Assert.Equal((7, true), (animated.Single().Frames, animated.Single().Rewind));
        AnimationTimeline timeline = AnimationTimeline.Of(animated, once: false);
        Assert.Equal(3.6, timeline.Length, 4);
        Assert.True(compositor.TryCompose(tables, camera, ByondDirConst.East, 0.0001, out ComposedRaster first));
        Assert.True(compositor.TryCompose(tables, camera, ByondDirConst.East, 1.8001, out ComposedRaster last));
        Assert.NotEqual(first.Image.Pixels, last.Image.Pixels);
        foreach (ComposedRaster frame in new[] { first, last })
        {
            (int minX, int maxX, int minY, int maxY) = Box(frame.Image);
            Assert.True(maxX - minX > maxY - minY); // every frame stays the side view
        }
    }

    private static (int MinX, int MaxX, int MinY, int MaxY) Box(Raster image)
    {
        int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1;
        for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
                if (image.Pixels[(y * image.Width + x) * 4 + 3] > 0)
                {
                    minX = Math.Min(minX, x);
                    maxX = Math.Max(maxX, x);
                    minY = Math.Min(minY, y);
                    maxY = Math.Max(maxY, y);
                }
        return (minX, maxX, minY, maxY);
    }
}
