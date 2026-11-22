using System.Linq;
using System.Numerics;
using Anomaly.Client.Shared.Icons;

namespace Anomaly.Client.Shared.Models;

/// <summary>
/// A model material that shows one frame of the thing's own tg sheet (docs/ai/specs/models.md, "Live frames"): its image is the sheet itself (or the sheet the entry
/// names as its picture, <see cref="ModelEntry.FrameSheet"/>: a canister's map icon for its GAGS picture), and its uv lie in one frame of it, selected by its
/// <c>KHR_texture_transform</c> or as they are. Such a face is drawn with the thing's picture as tg composes it now (its icon state, its overlays, its animation and
/// its flicks) for the direction of the frame the model was made from, while the state shown is that frame's or one of its own variants (<see cref="Follows"/>);
/// else it keeps the frame it was made from. <see cref="X"/>, <see cref="Y"/>, <see cref="Width"/> and <see cref="Height"/> are the frame's pixels in the sheet.
/// </summary>
public sealed record ModelFrame(string State, int DirIndex, int X, int Y, int Width, int Height)
{
    /// <summary>Identify original atlas pixels across lossless PNG re-encoding by Blender.</summary>
    public static string ImageHash(byte[] png)
    {
        Raster image = PngDecoder.Decode(png);
        return $"{image.Width}x{image.Height}:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(image.Pixels));
    }

    /// <summary>The BYOND dir of the frame (tg's file order of a state's directions).</summary>
    public byte Dir => ByondDir.FromIndex(DirIndex);

    /// <summary>
    /// True when a thing showing <paramref name="shown"/> keeps the shape of the model made from <paramref name="authored"/>: the same state, or a variant
    /// named after it (tg's <c>"[base]-off"</c>, <c>"[base]-broken"</c>, <c>"[base]-deny"</c>, <c>"[base]_broke"</c>, <c>"[base]-alt"</c>). A frame of a sheet that
    /// only stands for the thing's picture (null: a map icon) follows every state.
    /// </summary>
    public static bool Follows(string authored, string shown)
        => authored == null
           || authored.Length > 0 && shown != null
              && (shown == authored || shown.Length > authored.Length + 1 && shown.StartsWith(authored, StringComparison.Ordinal) && shown[authored.Length] is '-' or '_');

    /// <summary>A material's uv (after its transform) mapped into the frame: 0 to 1 across it, glTF's v down.</summary>
    public Vector2 Local(Vector2 sheetUv, int sheetWidth, int sheetHeight)
        => new((sheetUv.X * sheetWidth - X) / Width, (sheetUv.Y * sheetHeight - Y) / Height);

    /// <summary>
    /// The frame a material shows, when its uv (<paramref name="uvs"/>, as the file has them) lie in one frame of <paramref name="sheet"/> (the icon tool's
    /// description of the same image, <paramref name="sheetWidth"/> x <paramref name="sheetHeight"/>): a transform scaled to one frame with uv inside it, or uv
    /// without a transform that stay inside one frame. Null otherwise (uv over several frames, a derived texture). <paramref name="own"/> false: the sheet only
    /// stands for the thing's picture, so the frame has no state of its own.
    /// </summary>
    public static ModelFrame Of(GlbMaterial material, IEnumerable<Vector2> uvs, IconSheet sheet, int sheetWidth, int sheetHeight, bool own = true)
    {
        if (material == null || sheet == null || sheetWidth <= 0 || sheetHeight <= 0 || sheet.Width <= 0 || sheet.Height <= 0)
            return null;
        Vector2[] all = uvs.Select(uv => uv * material.UvScale + material.UvOffset).ToArray();
        if (all.Length == 0)
            return null;
        // The frame the first uv is in; every uv must stay inside it.
        const float Slack = 1e-3f;
        float minX = all.Min(uv => uv.X) * sheetWidth, minY = all.Min(uv => uv.Y) * sheetHeight;
        float maxX = all.Max(uv => uv.X) * sheetWidth, maxY = all.Max(uv => uv.Y) * sheetHeight;
        int x = (int)MathF.Floor((minX + Slack) / sheet.Width) * sheet.Width, y = (int)MathF.Floor((minY + Slack) / sheet.Height) * sheet.Height;
        if (minX < x - Slack || minY < y - Slack || maxX > x + sheet.Width + Slack || maxY > y + sheet.Height + Slack)
            return null;
        foreach (IconState state in sheet.States)
            for (int i = 0; i < state.Rects.Length; i++)
                if (state.Rects[i].X == x && state.Rects[i].Y == y)
                    return own
                        ? new ModelFrame(state.Name, i / Math.Max(1, state.Frames), x, y, sheet.Width, sheet.Height)
                        : new ModelFrame(null, 0, x, y, sheet.Width, sheet.Height);
        return null;
    }
}
