using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

/// <summary>
/// D315 (the owner, 2026-11-04): where the minimap looks, in world tiles: the own mob, or where the player dragged the map, kept over the
/// picture, until the mob moves a quarter of a tile from where it stood at the drag.
/// </summary>
public sealed class MinimapView
{
    /// <summary>How far the mob moves, in tiles either way, before the view comes back to it.</summary>
    public const float ReturnDistance = 0.25f;

    private float _panX, _panY, _anchorX, _anchorY;

    /// <summary>Whether the view is off the mob.</summary>
    public bool Panned => _panX != 0f || _panY != 0f;

    /// <summary>The pointer dragged the map by (<paramref name="dx"/>, <paramref name="dy"/>) panel pixels (y down) at <paramref name="pixelsPerTile"/>: the map follows it.</summary>
    public void Drag(float dx, float dy, float pixelsPerTile, float mobX, float mobY)
    {
        if (!Panned)
            (_anchorX, _anchorY) = (mobX, mobY);
        _panX -= dx / pixelsPerTile;
        _panY += dy / pixelsPerTile; // north is up on the panel
    }

    /// <summary>Back on the mob (another level).</summary>
    public void Reset() => (_panX, _panY) = (0f, 0f);

    /// <summary>The view's centre for the mob at (<paramref name="mobX"/>, <paramref name="mobY"/>): the mob's own when it moved since the drag or none was made.</summary>
    public (float X, float Y) Centre(float mobX, float mobY, MinimapMessage map)
    {
        if (Panned && (Math.Abs(mobX - _anchorX) > ReturnDistance || Math.Abs(mobY - _anchorY) > ReturnDistance))
            Reset();
        if (!Panned)
            return (mobX, mobY);
        float x = Math.Clamp(mobX + _panX, map.MinX - 0.5f, map.MinX + map.Width - 0.5f);
        float y = Math.Clamp(mobY + _panY, map.MinY - 0.5f, map.MinY + map.Height - 0.5f);
        (_panX, _panY) = (x - mobX, y - mobY); // a drag past the edge does not pile up
        return (x, y);
    }
}
