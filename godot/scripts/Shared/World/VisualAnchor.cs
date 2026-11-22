using System.Numerics;

namespace Anomaly.Client.Shared.World;

// M4v slice 3 (protocol version 10, D143). Engine-free. Where the client draws a short-lived visual (tg's flick_overlay_view:
// an /atom/movable/flick_visual in another atom's vis_contents, NetField.VisHost) in the renderer's space (metres; X east, Y up,
// Z south, as TileSpace). BYOND draws a vis_contents object at its holder, like an overlay: with its own appearance, pixel
// offsets and layer, over the holder. The client reads the holder's drawn place: a moving entity's node, a static prop's
// billboard, a wall decal's face (WallMounts.DecalPlacement), or the turf's floor.

/// <summary>How a visual's picture faces: turned to the viewer (a billboard) or flat on a wall face like its host decal.</summary>
public enum VisualSurface : byte
{
    /// <summary>A Y-axis billboard standing at <see cref="VisualPlacement.Origin"/>, its picture's bottom there (as an entity's).</summary>
    Billboard = 0,

    /// <summary>A flat picture on the host decal's wall face: centred at <see cref="VisualPlacement.Origin"/>, its front turned by <see cref="VisualPlacement.Yaw"/>.</summary>
    WallFace,
}

/// <summary>Where a visual is drawn: the point, the surface, the yaw of a wall face and the share of the tile scale its picture takes.</summary>
public readonly record struct VisualPlacement(Vector3 Origin, VisualSurface Surface, float Yaw, float Scale);

/// <summary>The placement rules of a short-lived visual over its host (protocol.md section 19.3).</summary>
public static class VisualAnchor
{
    /// <summary>How far toward the viewer a billboard visual stands in front of its host's picture (metres), so that it draws over it.</summary>
    public const float BillboardLift = 0.05f;

    /// <summary>How far in front of its host decal a wall face visual floats (metres): one more decal lift.</summary>
    public const float WallLift = WallMounts.DecalLift;

    /// <summary>
    /// A visual on a turf (tg's pickup image, <c>source.flick_overlay_view</c> on the item's floor tile, items.dm:1536): a
    /// billboard on the tile's floor at the item scale (D121), as the item lay there; its pixel offsets move it on the ground.
    /// </summary>
    public static VisualPlacement OnTurf(int x, int y, int z, float itemScale, Vector3 viewer) =>
        new(Toward(new Vector3(TileSpace.WorldX(x), TileSpace.WorldY(z), TileSpace.WorldZ(y)), viewer), VisualSurface.Billboard, 0f, itemScale);

    /// <summary>
    /// A visual on a host drawn as a billboard (a mob, a moving thing, a static prop) standing at <paramref name="hostFeet"/>:
    /// the same point, <see cref="BillboardLift"/> toward the viewer, at the host's scale (an item's, or the tile scale).
    /// </summary>
    public static VisualPlacement OnBillboard(Vector3 hostFeet, float hostScale, Vector3 viewer) =>
        new(Toward(hostFeet, viewer), VisualSurface.Billboard, 0f, hostScale);

    /// <summary>
    /// A visual on a wall decal (tg's button flashes, buttons.dm:303 and :307): the decal's face point and yaw
    /// (<see cref="WallMounts.DecalPlacement"/> with the host picture's height), <see cref="WallLift"/> further out of the wall.
    /// </summary>
    public static VisualPlacement OnWallDecal(Placed host, float hostPictureHeight)
    {
        (Vector3 origin, float yaw) = WallMounts.DecalPlacement(host, hostPictureHeight);
        var facing = new Vector3(MathF.Sin(yaw), 0f, MathF.Cos(yaw));
        return new VisualPlacement(origin + facing * WallLift, VisualSurface.WallFace, yaw, 1f);
    }

    /// <summary>The unit way from <paramref name="at"/> to the viewer on the ground plane, zero when the viewer stands on it.</summary>
    public static Vector3 TowardViewer(Vector3 at, Vector3 viewer)
    {
        var flat = new Vector3(viewer.X - at.X, 0f, viewer.Z - at.Z);
        float length = flat.Length();
        return length < 1e-4f ? Vector3.Zero : flat / length;
    }

    // The point moved BillboardLift toward the viewer on the ground plane (none when the viewer stands on it).
    private static Vector3 Toward(Vector3 at, Vector3 viewer) => at + TowardViewer(at, viewer) * BillboardLift;
}
