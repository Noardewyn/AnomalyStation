using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// The owner's play test (2026-10-09), DV30: a click on a carbon's picture aims at the body part under the crosshair. tg draws a
// carbon from one image per body part (bodypart.dm's get_limb_icon: icon states "<limb id>_<zone>[_<gender>]", the hands
// "<limb id>_l_hand" and "_r_hand", from one of tg's limb files, LimbIcons); the part whose own image is opaque at tg's icon-x/icon-y
// of the click, the one drawn on top in the compositor's order (package T, slice T2), is the aimed zone, refined by tg's eyes, mouth and groin
// (slice T4a, BodyZoneMap). Clothing covers the parts but does not change which one is under the point.

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
    /// reaches past it: icon-y 36 is the top of its head). Package T, slice T4a: the nine zones of <see cref="BodyZoneMap"/> (the eyes, the
    /// mouth and the groin included; <see cref="BodyZoneMap.LimbOf"/> gives the six-zone limb).
    /// </summary>
    public static AimZone ZoneAtPixel(IAppearanceSource tables, AppearanceCompositor compositor, TableRef root, byte dir, int iconX, int iconY)
        => ZoneAtPixel(tables, compositor, root, ZoneMap(tables, compositor, root, dir), iconX, iconY);

    /// <summary><see cref="ZoneAtPixel(IAppearanceSource, AppearanceCompositor, TableRef, byte, int, int)"/> with the frame's map already made (<see cref="ZoneMap"/>, a caller's cache).</summary>
    public static AimZone ZoneAtPixel(IAppearanceSource tables, AppearanceCompositor compositor, TableRef root, BodyZoneMap map, int iconX, int iconY)
    {
        if (map == null || tables == null || compositor == null || !TryBack(tables, compositor, root, out Affine back) || !TrySource(back, iconX, iconY, out int sourceX, out int sourceY))
            return AimZone.None;
        return map.At(sourceX, sourceY);
    }

    /// <summary>
    /// Package T, slice T4a: the zone map of <paramref name="root"/>'s picture in direction <paramref name="dir"/>: each body part composed once,
    /// the one drawn on top at each pixel of the untransformed icon square (slice T2's order), refined by tg's eyes, mouth and groin
    /// (<see cref="BodyZoneMap.Build"/>). Null when the picture has no body-part pictures (a simple mob, a silicon, a xenomorph). The caller keeps
    /// it per picture and frame.
    /// </summary>
    public static BodyZoneMap ZoneMap(IAppearanceSource tables, AppearanceCompositor compositor, TableRef root, byte dir)
    {
        if (tables == null || compositor == null || root.IsNone || !tables.TryGetAppearance(root, out AppearanceEntry entry) || entry.OverlayCount == 0)
            return null;
        List<TableRef> limbs = Parts(tables, entry, LimbZone);
        if (limbs.Count == 0)
            return null;
        const int side = BodyZoneMap.Side;
        var onTop = new AimZone[side * side]; // the untransformed icon square, (x - 1) + (y - 1) * side
        foreach (TableRef reference in limbs) // the one drawn on top first
        {
            if (!tables.TryGetAppearance(reference, out AppearanceEntry part) || !compositor.TryCompose(tables, reference, dir, 0, out ComposedRaster limb) || limb.Image == null)
                continue;
            AimZone zone = LimbZone(tables.ResolveResource(part.Icon), tables.ResolveString(part.IconState));
            for (int y = 1; y <= side; y++)
                for (int x = 1; x <= side; x++)
                    if (onTop[x - 1 + (y - 1) * side] == AimZone.None && OpaqueAt(limb, x, y))
                        onTop[x - 1 + (y - 1) * side] = zone;
        }
        return BodyZoneMap.Build(onTop, dir);
    }

    /// <summary>
    /// Package T, slice T1 (docs/ai/specs/ui-overhaul.md section 8): the hit mask of a mob built from body-part pictures, for the pick: a
    /// raster of <paramref name="picture"/>'s size and origin (the composed picture of <paramref name="root"/> in direction
    /// <paramref name="dir"/>) whose alpha is 255 on the pixels that are drawn and where <see cref="ZoneAtPixel(IAppearanceSource, AppearanceCompositor, TableRef, byte, int, int)"/> finds a body part (the
    /// root's placement undone: a giant's or a lying body's parts reach past the icon square, a seated one's are raised), 0 elsewhere: clothes, hair, a backpack or a held item outside the body are not hit, a body part under clothes is. Null when
    /// the picture has no body-part pictures (a simple mob, a silicon, a xenomorph): the whole picture is hit. Each body part is composed
    /// once; the caller keeps the mask per picture.
    /// </summary>
    public static Raster BodyMask(IAppearanceSource tables, AppearanceCompositor compositor, TableRef root, byte dir, ComposedRaster picture)
        => BodyMask(tables, compositor, root, picture, ZoneMap(tables, compositor, root, dir));

    /// <summary><see cref="BodyMask(IAppearanceSource, AppearanceCompositor, TableRef, byte, ComposedRaster)"/> from the frame's map already made (<see cref="ZoneMap"/>); null when it is null.</summary>
    public static Raster BodyMask(IAppearanceSource tables, AppearanceCompositor compositor, TableRef root, ComposedRaster picture, BodyZoneMap map)
    {
        if (map == null || tables == null || compositor == null || picture?.Image == null || !TryBack(tables, compositor, root, out Affine back))
            return null;
        Raster image = picture.Image;
        var mask = new Raster(image.Width, image.Height);
        for (int fromBottom = 0; fromBottom < image.Height; fromBottom++)
            for (int col = 0; col < image.Width; col++)
            {
                int alpha = ((image.Height - 1 - fromBottom) * image.Width + col) * 4 + 3;
                // a drawn pixel only: a mob drawn see-through (invisible) is not hit where its parts are
                if (image.Pixels[alpha] >= BillboardMath.PickAlpha && TrySource(back, col - picture.OriginX + 1, fromBottom - picture.OriginY + 1, out int sx, out int sy)
                    && map.At(sx, sy) != AimZone.None)
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

    // The body-part overlays (zoneOf names a zone for them) of the picture, the one drawn on top first (package T, slice T2: the compositor's own order,
    // so where the legs overlap seen from the side the near leg tg draws over the far one is found; the list order alone found the far one).
    private static List<TableRef> Parts(IAppearanceSource tables, AppearanceEntry entry, Func<string, string, AimZone> zoneOf)
        => AppearanceCompositor.OverlaysOnTopFirst(tables, entry, overlay => zoneOf(tables.ResolveResource(overlay.Icon), tables.ResolveString(overlay.IconState)) != AimZone.None);

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
        return Search(tables, compositor, entry, 2, iconX, iconY, (_, state) => DollZoneOfState(state));
    }

    /// <summary>
    /// Slice T5c (the eyes and the mouth on the health doll): the doll's <paramref name="limb"/> (<see cref="DollZoneAt"/>)
    /// at tg's (<paramref name="iconX"/>, <paramref name="iconY"/>) refined on the head by tg's own pixels of the zone selector (screen_objects.dm,
    /// <c>get_zone_at</c>): the eyes rows 25 to 27, columns 15 to 17, and columns 14 and 18 on row 26; the mouth rows 23 and 24, columns 15 to 17.
    /// For an item used on the doll and its hover pictures; the ctrl-click grasp takes the limb itself (tg's <c>get_bodypart</c> has no eyes or mouth).
    /// </summary>
    public static AimZone DollFaceAt(AimZone limb, int iconX, int iconY)
    {
        if (limb != AimZone.Head)
            return limb;
        if (iconX >= 15 && iconX <= 17 && (iconY == 23 || iconY == 24))
            return AimZone.Mouth;
        if (iconY == 26 && iconX >= 14 && iconX <= 18 || iconY >= 25 && iconY <= 27 && iconX >= 15 && iconX <= 17)
            return AimZone.Eyes;
        return AimZone.Head;
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

    // zoneOf: the zone of an overlay from its icon resource and its state, None for a picture that is no body part. The part drawn on top at the pixel wins.
    private static AimZone Search(IAppearanceSource tables, AppearanceCompositor compositor, AppearanceEntry entry, byte dir, int iconX, int iconY, Func<string, string, AimZone> zoneOf)
    {
        if (entry.OverlayCount == 0)
            return AimZone.None;
        foreach (TableRef reference in Parts(tables, entry, zoneOf))
        {
            if (Opaque(tables, compositor, reference, dir, iconX, iconY) && tables.TryGetAppearance(reference, out AppearanceEntry part))
                return zoneOf(tables.ResolveResource(part.Icon), tables.ResolveString(part.IconState));
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
