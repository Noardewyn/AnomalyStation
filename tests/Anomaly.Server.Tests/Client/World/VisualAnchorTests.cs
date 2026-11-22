using Anomaly.Client.Shared;
using Anomaly.Client.Shared.World;
using Anomaly.Protocol;
using System.Numerics;

namespace Anomaly.Server.Tests.Client.World;

/// <summary>
/// M4v slice 3 (protocol version 10): where the client draws tg's short-lived visuals over their host (VisualAnchor, protocol.md
/// section 19.3): the pickup image on its floor tile at the item scale, an attack image on the target's billboard just in front of
/// it, a button's flash flat on the button's wall face.
/// </summary>
public class VisualAnchorTests
{
    private const byte North = 1, East = 4;

    [Fact]
    public void A_visual_on_a_turf_stands_on_the_tile_floor_at_the_item_scale_toward_the_viewer()
    {
        var viewer = new Vector3(TileSpace.WorldX(10), 1.5f, TileSpace.WorldZ(20));
        VisualPlacement p = VisualAnchor.OnTurf(12, 20, 1, 0.5f, viewer);
        Assert.Equal(VisualSurface.Billboard, p.Surface);
        Assert.Equal(0.5f, p.Scale);
        Assert.Equal(TileSpace.WorldY(1), p.Origin.Y);
        Assert.Equal(TileSpace.WorldZ(20), p.Origin.Z, 4);
        // The viewer is two tiles west: the visual stands BillboardLift west of the tile centre.
        Assert.Equal(TileSpace.WorldX(12) - VisualAnchor.BillboardLift, p.Origin.X, 4);
    }

    [Fact]
    public void A_visual_on_a_billboard_keeps_its_hosts_point_and_scale_and_stands_in_front()
    {
        var feet = new Vector3(4f, 0f, -6f);
        VisualPlacement p = VisualAnchor.OnBillboard(feet, 1f, new Vector3(4f, 1.5f, 0f)); // the viewer is south of it
        Assert.Equal(VisualSurface.Billboard, p.Surface);
        Assert.Equal(1f, p.Scale);
        Assert.Equal(new Vector3(4f, 0f, -6f + VisualAnchor.BillboardLift), p.Origin);
        Assert.Equal(feet, VisualAnchor.OnBillboard(feet, 1f, feet).Origin); // a viewer on the spot: no direction to move
    }

    [Fact]
    public void A_visual_on_a_wall_decal_lies_on_the_decals_face_a_little_further_out()
    {
        foreach (byte face in new[] { North, East })
        {
            var host = new Placed(1, PlacedKind.WallDecal, RenderKind.Billboard, 10, 20, 1, face, face, 1.2f, 0.25f, 0f, TableRef.Public(1), LightState.None, null);
            (Vector3 decal, float yaw) = WallMounts.DecalPlacement(host, 0.8f);
            VisualPlacement p = VisualAnchor.OnWallDecal(host, 0.8f);
            Assert.Equal(VisualSurface.WallFace, p.Surface);
            Assert.Equal(yaw, p.Yaw);
            Vector3 front = new(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
            Vector3 lift = p.Origin - decal;
            Assert.Equal(VisualAnchor.WallLift, Vector3.Dot(lift, front), 4); // out of the wall, in front of the decal
            Assert.Equal(0f, (lift - front * VisualAnchor.WallLift).Length(), 4);
            // The face looks back into the tile, away from the wall the decal is on.
            Vector3 tile = new(TileSpace.WorldX(10), TileSpace.WorldY(1), TileSpace.WorldZ(20));
            Assert.True(Vector3.Dot(tile - p.Origin, front) > 0);
        }
    }
}
