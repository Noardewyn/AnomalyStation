using System.Runtime.CompilerServices;
using Godot;
using Anomaly.Client.Render;
using Anomaly.Client.Shared;
using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Hud;

/// <summary>
/// What the HUD pick (<see cref="HudPicker"/>) reads of the drawn pictures, shared by the HUD layer and the native windows: a texture's pixels as an
/// engine-free raster, and (protocol version 12) a screen entry's clickable vis_contents children, each composed alone.
/// </summary>
internal static class HudPictures
{
    private sealed class Box
    {
        public Raster Raster;
    }

    private static readonly ConditionalWeakTable<Texture2D, Box> Rasters = new();

    /// <summary>A texture's pixels (cached with the texture; only a click in cursor mode asks); null when the engine gives no image.</summary>
    public static Raster RasterOf(Texture2D texture)
    {
        if (texture == null)
            return null;
        if (Rasters.TryGetValue(texture, out Box known))
            return known.Raster;
        Image image = texture.GetImage();
        Raster raster = null;
        if (image != null)
        {
            if (image.GetFormat() != Image.Format.Rgba8)
                image.Convert(Image.Format.Rgba8);
            raster = new Raster(image.GetWidth(), image.GetHeight(), image.GetData());
        }
        Rasters.AddOrUpdate(texture, new Box { Raster = raster });
        return raster;
    }

    /// <summary>
    /// Version 12: <paramref name="entry"/>'s clickable vis_contents children (<see cref="ScreenEntry.VisChildren"/>, bottom to top) with their own
    /// pictures, placed in the entry's icon square: BYOND draws a child moved by its holder's pixel offset (the HUD composes a screen object with its
    /// own offset, <see cref="IAppearanceTextures.TryCompose"/>). Null for none.
    /// </summary>
    public static IReadOnlyList<HudChildDrawn> ChildrenOf(ReplicaWorld replica, IAppearanceTextures textures, in ScreenEntry entry, double seconds)
    {
        ScreenChild[] items = entry.VisChildren?.Items;
        if (items == null || items.Length == 0 || replica == null || textures == null)
            return null;
        int shiftX = 0, shiftY = 0;
        if (replica.TryGetAppearance(entry.Appearance, out AppearanceEntry holder))
        {
            shiftX = holder.PixelX;
            shiftY = holder.PixelY;
        }
        var children = new List<HudChildDrawn>(items.Length);
        foreach (ScreenChild c in items)
        {
            Raster image = null;
            int originX = 0, originY = 0;
            float fit = 0f;
            if (textures.TryCompose(replica, c.Appearance, 2, seconds, out ComposedAppearance composed))
            {
                image = RasterOf(composed.Texture);
                originX = composed.OriginX;
                originY = composed.OriginY;
                fit = HudChildDrawn.FitOf(composed.PixelWidth, composed.PixelHeight); // drawn fitted into the square (OversizedChildren)
            }
            children.Add(new HudChildDrawn(c.Id, image, originX, originY, shiftX, shiftY, c.MouseOpaque, fit));
        }
        return children;
    }

    // DV575: the pictures (icon, state) of the children found too big for a slot's square; their overlays in a holder are drawn on their own
    private static readonly HashSet<(string Icon, string State)> Oversized = new();
    private static readonly ConditionalWeakTable<IAppearanceSource, OverlayStripper> Strippers = new();

    /// <summary>
    /// DEVIATION(DV575): <paramref name="entry"/>'s vis_contents children too big for its 32 x 32 square (a 64 x 64 polearm in a hand), each to be
    /// drawn on its own, fitted into the square (<see cref="HudChildDrawn.FitOf"/>, centred), where BYOND spills it up and right over the next slots;
    /// the holder's own picture then leaves it out (<see cref="WithoutOversized"/>). Empty when none is.
    /// </summary>
    public static List<ComposedAppearance> OversizedChildren(ReplicaWorld replica, IAppearanceTextures textures, in ScreenEntry entry, double seconds)
    {
        var big = new List<ComposedAppearance>();
        ScreenChild[] items = entry.VisChildren?.Items;
        if (items == null || replica == null || textures == null)
            return big;
        foreach (ScreenChild c in items)
        {
            if (!textures.TryCompose(replica, c.Appearance, 2, seconds, out ComposedAppearance composed) || HudChildDrawn.FitOf(composed.PixelWidth, composed.PixelHeight) == 0f)
                continue;
            big.Add(composed);
            if (replica.TryGetAppearance(c.Appearance, out AppearanceEntry look))
                Oversized.Add((replica.ResolveResource(look.Icon), replica.ResolveString(look.IconState)));
        }
        return big;
    }

    /// <summary>A holder's appearance without the overlays of its oversized children (the server draws vis_contents into it), and the tables that resolve it.</summary>
    public static TableRef WithoutOversized(AppearanceTextures textures, ReplicaWorld replica, TableRef appearance, out IAppearanceSource tables)
    {
        ReplicaTables inner = textures.TablesOf(replica); // (one per replica snapshot: a new one starts the stripper afresh)
        OverlayStripper stripper = Strippers.GetValue(inner, t => new OverlayStripper(t, o => t.TryGetAppearance(o, out AppearanceEntry e)
            && Oversized.Contains((t.ResolveResource(e.Icon), t.ResolveString(e.IconState)))));
        tables = stripper;
        return stripper.Strip(appearance);
    }

    /// <summary>Where an oversized child is drawn: fitted into the holder's icon square (screen pixels, y down) and centred in it.</summary>
    public static Rect2 FittedIn(Rect2 square, in ComposedAppearance child)
    {
        float fit = HudChildDrawn.FitOf(child.PixelWidth, child.PixelHeight), k = square.Size.X / 32f;
        var size = new Vector2(child.PixelWidth * fit * k, child.PixelHeight * fit * k);
        return new Rect2(square.Position + (square.Size - size) / 2f, size);
    }
}
