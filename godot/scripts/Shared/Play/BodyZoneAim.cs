using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared;

// Engine-free: no Godot API here (it is compiled into tests/Anomaly.Server.Tests too).
// The owner's play test (2026-10-09), DV30: a click on a carbon's picture aims at the body part under the crosshair. tg draws a
// carbon from one image per body part (bodypart.dm's get_limb_icon: icon states "<limb id>_<zone>[_<gender>]", the hands
// "<limb id>_l_hand" and "_r_hand"); the part whose own image is opaque at tg's icon-x/icon-y of the click, the topmost first,
// is the aimed zone. Clothing covers the parts but does not change which one is under the point.

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
        if (tables == null || compositor == null || root.IsNone || iconX == 0 || iconY == 0)
            return AimZone.None;
        if (!tables.TryGetAppearance(root, out AppearanceEntry entry))
            return AimZone.None;
        if (entry.Transform != null)
        {
            // The compositor turns the root about the middle of its icon square (the root has no icon of its own: the default size); the
            // click is at the centre of the clicked pixel of the turned picture, and nearest sampling puts that point's source under it.
            const double side = AppearanceCompositor.DefaultIconSize;
            Affine turn = Affine.Translate(-side / 2, -side / 2).Then(Affine.FromMatrix(entry.Transform)).Then(Affine.Translate(side / 2, side / 2));
            if (!turn.TryInvert(out Affine back))
                return AimZone.None;
            (double x, double y) = back.Apply(iconX - 0.5, iconY - 0.5);
            int sourceX = (int)Math.Floor(x) + 1, sourceY = (int)Math.Floor(y) + 1;
            if (sourceX < 1 || sourceX > AppearanceCompositor.DefaultIconSize || sourceY < 1 || sourceY > AppearanceCompositor.DefaultIconSize)
                return AimZone.None;
            iconX = (byte)sourceX;
            iconY = (byte)sourceY;
        }
        return Search(tables, compositor, entry, dir, iconX, iconY, 0, ZoneOfState);
    }

    /// <summary>
    /// D183: the limb of the HUD's health doll (<paramref name="doll"/>, tg's <c>/atom/movable/screen/healthdoll</c>, one overlay per
    /// limb: <c>"head0"</c>, <c>"l_arm3"</c>, ...) drawn at tg's (<paramref name="iconX"/>, <paramref name="iconY"/>) of a click on it;
    /// None when no limb is there.
    /// </summary>
    public static AimZone DollZoneAt(IAppearanceSource tables, AppearanceCompositor compositor, TableRef doll, byte iconX, byte iconY)
    {
        if (tables == null || compositor == null || doll.IsNone || iconX == 0 || iconY == 0 || !tables.TryGetAppearance(doll, out AppearanceEntry entry))
            return AimZone.None;
        return Search(tables, compositor, entry, 2, iconX, iconY, 0, DollZoneOfState);
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

    private static AimZone Search(IAppearanceSource tables, AppearanceCompositor compositor, AppearanceEntry entry, byte dir, byte iconX, byte iconY, int depth, Func<string, AimZone> zoneOf)
    {
        if (depth > 4 || entry.OverlayCount == 0)
            return AimZone.None;
        // BYOND draws the list in order, so the last one is on top
        for (int i = entry.Overlays.Count - 1; i >= 0; i--)
        {
            TableRef reference = entry.Overlays[i];
            if (!tables.TryGetAppearance(reference, out AppearanceEntry overlay))
                continue;
            AimZone zone = zoneOf(tables.ResolveString(overlay.IconState));
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

    private static bool Opaque(IAppearanceSource tables, AppearanceCompositor compositor, TableRef reference, byte dir, byte iconX, byte iconY)
    {
        if (!compositor.TryCompose(tables, reference, dir, 0, out ComposedRaster composed) || composed.Image == null)
            return false;
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
