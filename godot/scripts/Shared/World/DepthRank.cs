namespace Anomaly.Client.Shared.World;

/// <summary>
/// The draw order of the camera-facing pictures on one tile (the owner, 2026-10-26: the items on one tile fought and swapped as the viewer walked
/// round them, and a mob was buried under the clothes dropped on it). BYOND draws a tile's things by their layer, then by their order in its
/// contents; here each picture gets a rank, in metres it is drawn nearer the eye than its tile's centre (shaders/depth_order.gdshaderinc): tg's
/// layer first (an item's OBJ_LAYER 3 under a mob's MOB_LAYER 4), then a stable share by the entity's id, so the order never depends on where the
/// viewer stands and two things never share one depth.
/// </summary>
public static class DepthRank
{
    /// <summary>Metres per unit of tg layer: 5 mm between OBJ_LAYER and ABOVE_OBJ_LAYER (0.1 apart).</summary>
    public const float LayerStep = 0.05f;

    /// <summary>The share by the id spans less than one tenth of a layer, so it never crosses a layer's order.</summary>
    public const float TieSpan = 0.004f;

    // TURF_LAYER (2) and below rank 0; a layer above 6 ranks as 6 (nothing is drawn far out of its tile).
    private const float LowLayer = 2f, HighLayer = 6f;

    /// <summary>The rank of a picture of tg layer <paramref name="layer"/> drawn for entity <paramref name="id"/>.</summary>
    public static float Of(float layer, long id)
    {
        float clamped = float.IsNaN(layer) ? LowLayer : Math.Clamp(layer, LowLayer, HighLayer);
        uint hash = (uint)((ulong)id * 2654435761UL) >> 8; // Knuth's multiplicative hash: neighbouring ids spread over the span
        return (clamped - LowLayer) * LayerStep + hash / 16777216f * TieSpan;
    }
}
