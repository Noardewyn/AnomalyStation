using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.World;

// Owner: M3 R3. Engine-free. D121 (the owner's second walk): in first person the old top-down variety of junction pictures is not needed, the joint is geometry; a window shows ONE
// picture per type, and so does every wall on its side faces: tg's straight east-west run, "<base_icon_state>-12" (EAST | WEST), whatever junction the turf's or the window's
// appearance carries (the fake feed computes the junction itself, the real server's SSicon_smooth puts tg's own into the icon state, and neither is relied on).

/// <summary>
/// The one rule that picks the straight east-west picture of a smoothed appearance, shared by the windows (the piece renderer) and the walls (the tile kit): the appearance's icon
/// state with its junction number replaced by <see cref="Smoothing.EastWest"/>, when its icon has that state; otherwise there is none and the caller keeps the appearance's own picture.
/// </summary>
public static class StraightRun
{
    /// <summary>
    /// The icon and state of the straight east-west run of <paramref name="appearance"/> (<c>wall-5</c> of <c>wall.dmi</c> gives <c>wall-12</c> of the same file), false when its state is
    /// not a base and a junction or its icon has no such state.
    /// </summary>
    public static bool TryFind(IAppearanceSource tables, TableRef appearance, IconLibrary library, out string icon, out string state, int junction = Smoothing.EastWest)
    {
        icon = null;
        state = null;
        if (!tables.TryGetAppearance(appearance, out AppearanceEntry entry))
            return false;
        string straight = Smoothing.StraightRunState(tables.ResolveString(entry.IconState), junction);
        string resource = tables.ResolveResource(entry.Icon);
        if (straight == null || resource == null || !library.TryGetIcon(resource, out IIcon sheet) || !sheet.TryGetState(straight, out _))
            return false;
        icon = resource;
        state = straight;
        return true;
    }

    /// <summary>The first frame, first direction, of the straight run's picture (a 32 x 32 raster for tg's smoothed icons); false as <see cref="TryFind"/>.</summary>
    public static bool TryGetPicture(IAppearanceSource tables, TableRef appearance, IconLibrary library, out Raster picture, int junction = Smoothing.EastWest)
    {
        picture = null;
        if (!TryFind(tables, appearance, library, out string icon, out string state, junction))
            return false;
        if (!library.TryGetIcon(icon, out IIcon sheet) || !sheet.TryGetState(state, out IIconState iconState))
            return false;
        picture = iconState.Get(0, 0);
        return picture != null;
    }

    /// <summary>
    /// The tile's <paramref name="side"/> square of a straight run's <paramref name="picture"/>. A picture larger than a tile is moved onto it by its appearance's
    /// transform (tg's rocks: 40 or 48 pixels, <c>TRANSLATE_MATRIX(-4, -4)</c> or <c>(-8, -8)</c>), so the square is cut where that move puts the tile; without one,
    /// the bottom-left square, as BYOND draws an icon from its bottom-left corner.
    /// </summary>
    public static Raster TileSquare(IAppearanceSource tables, TableRef appearance, Raster picture, int side)
    {
        if (picture.Width == side && picture.Height == side)
            return picture.Clone();
        int shiftX = 0, shiftY = 0;
        if (tables.TryGetAppearance(appearance, out AppearanceEntry entry) && entry.Transform is { Length: 6 } m && Affine.FromMatrix(m).IsTranslation)
            (shiftX, shiftY) = (-(int)MathF.Round(m[2]), -(int)MathF.Round(m[5]));
        return picture.Crop(shiftX, picture.Height - shiftY - side, side, side);
    }
}
