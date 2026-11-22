using System.Linq;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.Models;

/// <summary>
/// Engine-free. The BYOND filters a thing a 3D model draws shows besides its meshes (docs/ai/specs/models.md, "Filters"): the first <c>rays</c> (a glowing
/// fan of rays facing the eye at the model's middle, <c>prop_rays.gdshader</c>) and the first <c>outline</c> (a rim round its crystal parts,
/// <c>prop_outline.gdshader</c>); tg's supermatter has both (_sm_delam.dm:115, common_delams.dm:37). The 2D client composes the same filters into the picture.
/// </summary>
public static class ModelFilters
{
    /// <summary>Metres a filter's pixel spans: a 32-pixel icon over the tile.</summary>
    public const float MetresPerPixel = TileSpace.TileSize / 32f;

    /// <summary>The first rays filter, or null.</summary>
    public static FilterEntry Rays(IReadOnlyList<FilterEntry> filters) => First(filters, FilterKind.Rays);

    /// <summary>The first outline filter, or null.</summary>
    public static FilterEntry Outline(IReadOnlyList<FilterEntry> filters) => First(filters, FilterKind.Outline);

    /// <summary>Whether a model shows any of them.</summary>
    public static bool Any(IReadOnlyList<FilterEntry> filters) => Rays(filters) != null || Outline(filters) != null;

    /// <summary>How far an outline's rim stands out of the mesh (metres): its line of <c>size</c> pixels, as wide as tg's line beside the sprite.</summary>
    public static float OutlineWidth(FilterEntry outline) => Math.Max(outline.Size, 0f) * MetresPerPixel;

    private static FilterEntry First(IReadOnlyList<FilterEntry> filters, FilterKind kind) => filters?.FirstOrDefault(f => f != null && f.Kind == kind);
}
