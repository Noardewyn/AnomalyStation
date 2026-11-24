using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// The owner's play test (2026-10-09), DV30: a click on a carbon's picture aims at the body part under the crosshair. tg draws a
// carbon from one image per body part (bodypart.dm's get_limb_icon: icon states "<limb id>_<zone>[_<gender>]", the hands
// "<limb id>_l_hand" and "_r_hand", from one of tg's limb files, LimbIcons); the part whose own image is opaque at tg's icon-x/icon-y
// of the click, the topmost first, is the aimed zone. Clothing covers the parts but does not change which one is under the point.

/// <summary>Which body zone of a carbon's picture lies under a pixel of it.</summary>
public static class BodyZoneAim
{
    /// <summary>
    /// The zone of the body part drawn at tg's (<paramref name="iconX"/>, <paramref name="iconY"/>) of the picture of
    /// <paramref name="root"/> in direction <paramref name="dir"/>; None when no body part is there or the pixel is absent. The root's own
    /// transform (a lying body is turned about the icon's centre and moved) is undone for the point, so the parts are looked up where they
    /// were before tg turned them.
    /// </summary>
    public static AimZone ZoneAt(IAppearanceSource tables, AppearanceCompositor compositor, TableRef root, byte dir, byte iconX, byte iconY)
    {
        if (iconX == 0 || iconY == 0)
            return AimZone.None;
        return ZoneAtPixel(tables, compositor, root, dir, iconX, iconY);
    }

    /// <summary>
    /// <see cref="ZoneAt"/> at any pixel of the picture, in tg's icon coordinates not clamped to the icon square (a giant's turned picture
    /// reaches past it: icon-y 36 is the top of its head).
    /// </summary>
    public static AimZone ZoneAtPixel(IAppearanceSource tables, AppearanceCompositor compositor, TableRef root, byte dir, int iconX, int iconY)
    {
        if (tables == null || compositor == null || root.IsNone || !tables.TryGetAppearance(root, out AppearanceEntry entry)
            || !TryBack(tables, compositor, root, out Affine back) || !TrySource(back, iconX, iconY, out int sourceX, out int sourceY))
            return AimZone.None;
        return Search(tables, compositor, entry, dir, sourceX, sourceY, 0, LimbZone);
    }

    /// <summary>
    /// Package T, slice T1 (docs/ai/specs/ui-overhaul.md section 8): the hit mask of a mob built from body-part pictures, for the pick: a
    /// raster of <paramref name="picture"/>'s size and origin (the composed picture of <paramref name="root"/> in direction
    /// <paramref name="dir"/>) whose alpha is 255 on the pixels that are drawn and where <see cref="ZoneAtPixel"/> finds a body part (the
    /// root's placement undone: a giant's or a lying body's parts reach past the icon square, a seated one's are raised), 0 elsewhere: clothes, hair, a backpack or a held item outside the body are not hit, a body part under clothes is. Null when
    /// the picture has no body-part pictures (a simple mob, a silicon, a xenomorph): the whole picture is hit. Each body part is composed
    /// once; the caller keeps the mask per picture.
    /// </summary>
    public static Raster BodyMask(IAppearanceSource tables, AppearanceCompositor compositor, TableRef root, byte dir, ComposedRaster picture)
    {
        if (tables == null || compositor == null || root.IsNone || picture?.Image == null || !tables.TryGetAppearance(root, out AppearanceEntry entry)
            || !TryBack(tables, compositor, root, out Affine back))
            return null;
        var limbs = new List<ComposedRaster>();
        CollectLimbs(tables, compositor, entry, dir, 0, limbs);
        if (limbs.Count == 0)
            return null;
        const int side = AppearanceCompositor.DefaultIconSize;
        var body = new bool[side * side]; // the untransformed icon square, (x - 1) + (y - 1) * side
        foreach (ComposedRaster limb in limbs)
            for (int y = 1; y <= side; y++)
                for (int x = 1; x <= side; x++)
                    if (OpaqueAt(limb, x, y))
                        body[x - 1 + (y - 1) * side] = true;
        Raster image = picture.Image;
        var mask = new Raster(image.Width, image.Height);
        for (int fromBottom = 0; fromBottom < image.Height; fromBottom++)
            for (int col = 0; col < image.Width; col++)
            {
                int alpha = ((image.Height - 1 - fromBottom) * image.Width + col) * 4 + 3;
                // a drawn pixel only: a mob drawn see-through (invisible) is not hit where its parts are
                if (image.Pixels[alpha] >= BillboardMath.PickAlpha && TrySource(back, col - picture.OriginX + 1, fromBottom - picture.OriginY + 1, out int sx, out int sy)
                    && body[sx - 1 + (sy - 1) * side])
                    mask.Pixels[alpha] = 255;
            }
        return mask;
    }

    // The inverse of the root's placement in the world picture (AppearanceCompositor.TryRootPlacement: its transform, a lying body turned about
    // the icon's centre and moved, then its pixel_w and pixel_z, a body raised by a stool or a table), so the parts are looked up where they were
    // before the compositor moved them.
    private static bool TryBack(IAppearanceSource tables, AppearanceCompositor compositor, TableRef root, out Affine back)
    {
        back = Affine.Identity;
        return compositor.TryRootPlacement(tables, root, out Affine placement) && placement.TryInvert(out back);
    }

    // The pixel of the untransformed icon square under tg's (iconX, iconY) of the picture (back: TryBack's); false when outside it. The point is the
    // centre of the picture's pixel, and nearest sampling puts that point's source under it.
    private static bool TrySource(Affine back, int iconX, int iconY, out int sourceX, out int sourceY)
    {
        (double x, double y) = back.Apply(iconX - 0.5, iconY - 0.5);
        sourceX = (int)Math.Floor(x) + 1;
        sourceY = (int)Math.Floor(y) + 1;
        return sourceX >= 1 && sourceX <= AppearanceCompositor.DefaultIconSize && sourceY >= 1 && sourceY <= AppearanceCompositor.DefaultIconSize;
    }

    // The composed body-part overlays Search would test, in any order (the same walk: a body part is not looked into, anything else is).
    private static void CollectLimbs(IAppearanceSource tables, AppearanceCompositor compositor, AppearanceEntry entry, byte dir, int depth, List<ComposedRaster> into)
    {
        if (depth > 4 || entry.OverlayCount == 0)
            return;
        foreach (TableRef reference in entry.Overlays)
        {
            if (!tables.TryGetAppearance(reference, out AppearanceEntry overlay))
                continue;
            if (LimbZone(tables.ResolveResource(overlay.Icon), tables.ResolveString(overlay.IconState)) != AimZone.None)
            {
                if (compositor.TryCompose(tables, reference, dir, 0, out ComposedRaster composed) && composed.Image != null)
                    into.Add(composed);
                continue;
            }
            CollectLimbs(tables, compositor, overlay, dir, depth + 1, into);
        }
    }

    // A body part: a picture from one of tg's limb files whose state names a zone (an item's "screwdriver_head" is not one).
    private static AimZone LimbZone(string resource, string state) => LimbIcons.IsLimbFile(resource) ? ZoneOfState(state) : AimZone.None;

    /// <summary>
    /// D183: the limb of the HUD's health doll (<paramref name="doll"/>, tg's <c>/atom/movable/screen/healthdoll</c>, one overlay per
    /// limb: <c>"head0"</c>, <c>"l_arm3"</c>, ...) drawn at tg's (<paramref name="iconX"/>, <paramref name="iconY"/>) of a click on it;
    /// None when no limb is there.
    /// </summary>
    public static AimZone DollZoneAt(IAppearanceSource tables, AppearanceCompositor compositor, TableRef doll, byte iconX, byte iconY)
    {
        if (tables == null || compositor == null || doll.IsNone || iconX == 0 || iconY == 0 || !tables.TryGetAppearance(doll, out AppearanceEntry entry))
            return AimZone.None;
        return Search(tables, compositor, entry, 2, iconX, iconY, 0, (_, state) => DollZoneOfState(state));
    }

    /// <summary>The zone a health doll limb state names (<c>"head3"</c>, <c>"r_legDEAD"</c>, <see cref="HealthZones.TryParseState"/>), None for any other state.</summary>
    public static AimZone DollZoneOfState(string state)
        => HealthZones.TryParseState(state, out ZoneState zone) ? zone.Zone switch
        {
            "head" => AimZone.Head,
            "chest" => AimZone.Chest,
            "l_arm" => AimZone.LeftArm,
            "r_arm" => AimZone.RightArm,
            "l_leg" => AimZone.LeftLeg,
            "r_leg" => AimZone.RightLeg,
            _ => AimZone.None,
        } : AimZone.None;

    // zoneOf: the zone of an overlay from its icon resource and its state, None for a picture that is no body part
    private static AimZone Search(IAppearanceSource tables, AppearanceCompositor compositor, AppearanceEntry entry, byte dir, int iconX, int iconY, int depth, Func<string, string, AimZone> zoneOf)
    {
        if (depth > 4 || entry.OverlayCount == 0)
            return AimZone.None;
        // BYOND draws the list in order, so the last one is on top
        for (int i = entry.Overlays.Count - 1; i >= 0; i--)
        {
            TableRef reference = entry.Overlays[i];
            if (!tables.TryGetAppearance(reference, out AppearanceEntry overlay))
                continue;
            AimZone zone = zoneOf(tables.ResolveResource(overlay.Icon), tables.ResolveString(overlay.IconState));
            if (zone != AimZone.None)
            {
                if (Opaque(tables, compositor, reference, dir, iconX, iconY))
                    return zone;
                continue;
            }
            AimZone inner = Search(tables, compositor, overlay, dir, iconX, iconY, depth + 1, zoneOf);
            if (inner != AimZone.None)
                return inner;
        }
        return AimZone.None;
    }

    private static bool Opaque(IAppearanceSource tables, AppearanceCompositor compositor, TableRef reference, byte dir, int iconX, int iconY)
    {
        return compositor.TryCompose(tables, reference, dir, 0, out ComposedRaster composed) && composed.Image != null && OpaqueAt(composed, iconX, iconY);
    }

    private static bool OpaqueAt(ComposedRaster composed, int iconX, int iconY)
    {
        Raster image = composed.Image;
        int col = composed.OriginX + iconX - 1;
        int fromBottom = composed.OriginY + iconY - 1;
        if (col < 0 || col >= image.Width || fromBottom < 0 || fromBottom >= image.Height)
            return false;
        int row = image.Height - 1 - fromBottom;
        return image.Pixels[(row * image.Width + col) * 4 + 3] >= BillboardMath.PickAlpha;
    }

    /// <summary>The zone a body part icon state names (<c>human_head_m</c>, <c>human_chest_f</c>, <c>human_l_arm</c>, <c>human_r_hand</c>, ...), None for any other state.</summary>
    public static AimZone ZoneOfState(string state)
    {
        if (string.IsNullOrEmpty(state))
            return AimZone.None;
        if (state.EndsWith("_m", StringComparison.Ordinal) || state.EndsWith("_f", StringComparison.Ordinal))
            state = state.Substring(0, state.Length - 2);
        if (state.EndsWith("_head", StringComparison.Ordinal)) return AimZone.Head;
        if (state.EndsWith("_chest", StringComparison.Ordinal)) return AimZone.Chest;
        if (state.EndsWith("_l_arm", StringComparison.Ordinal) || state.EndsWith("_l_hand", StringComparison.Ordinal)) return AimZone.LeftArm;
        if (state.EndsWith("_r_arm", StringComparison.Ordinal) || state.EndsWith("_r_hand", StringComparison.Ordinal)) return AimZone.RightArm;
        if (state.EndsWith("_l_leg", StringComparison.Ordinal)) return AimZone.LeftLeg;
        if (state.EndsWith("_r_leg", StringComparison.Ordinal)) return AimZone.RightLeg;
        return AimZone.None;
    }
}
