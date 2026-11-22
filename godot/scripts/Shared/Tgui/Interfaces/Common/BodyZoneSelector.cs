namespace Anomaly.Client.Shared.Tgui.Interfaces.Common;

// Translated from tgstation (snapshot 1d99bd7f): tgui/packages/tgui/interfaces/common/BodyZoneSelector.tsx (gaps P1, D298: the
// operating computer's doll). tg's asset body_zones (code/modules/asset_cache/assets/body_zones.dm) is drawn from its own .dmi files:
// the doll is the theme's screen_<theme>.dmi "zone_sel" and a zone the "<zone>" state of screen_gen.dmi. The doll reads where it is
// clicked and hovered through the kit's picture points (UiNode.OnClickAt, OnHoverAt).
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.

public static class BodyZoneSelector
{
    // tg: BodyZoneSelector.tsx:6  enum BodyZone
    public const string Head = "head", Chest = "chest", LeftArm = "l_arm", RightArm = "r_arm", LeftLeg = "l_leg", RightLeg = "r_leg",
        Eyes = "eyes", Mouth = "mouth", Groin = "groin";

    // tg: BodyZoneSelector.tsx:18  renderTogetherIfImprecise
    private static readonly Dictionary<string, string[]> RenderTogetherIfImprecise = new(StringComparer.Ordinal) { [Chest] = new[] { Groin } };

    // tg: BodyZoneSelector.tsx:22  bodyZonePixelToZone, the TypeScript translation of /atom/movable/screen/zone_sel/proc/get_zone_at
    public static string BodyZonePixelToZone(double x, double y, bool precise)
    {
        if (y < 1)
            return null;
        else if (y < 10)
        {
            if (x > 10 && x < 15)
                return RightLeg;
            else if (x > 17 && x < 22)
                return LeftLeg;
        }
        else if (y < 13)
        {
            if (x > 8 && x < 11)
                return RightArm;
            else if (x > 12 && x < 20)
                return precise ? Groin : Chest;
            else if (x > 21 && x < 24)
                return LeftArm;
        }
        else if (y < 22)
        {
            if (x > 8 && x < 11)
                return RightArm;
            else if (x > 12 && x < 20)
                return Chest;
            else if (x > 21 && x < 24)
                return LeftArm;
        }
        else if (y < 30 && x > 12 && x < 20)
        {
            if (y > 23 && y < 24 && x > 15 && x < 17)
                return precise ? Mouth : Head;
            else if (y > 25 && y < 27 && x > 14 && x < 18)
                return precise ? Eyes : Head;
            else
                return Head;
        }

        return null;
    }

    // tg: BodyZoneSelector.tsx:72  BodyImage: the zone's picture (and the zones drawn with it when imprecise) over the doll
    private static IEnumerable<UiNode> BodyImage(string zone, double scale, double opacity, bool precise)
    {
        string side = 32 * scale + "px";
        yield return new UiNode(UiKind.Box) { Key = "zone_" + zone + "_" + opacity, DmIcon = "icons/hud/screen_gen.dmi", DmIconState = zone, Width = side, Height = side, Opacity = opacity };
        if (!precise && RenderTogetherIfImprecise.TryGetValue(zone, out string[] others))
        {
            foreach (string other in others)
                yield return new UiNode(UiKind.Box) { Key = "zone_" + other + "_" + opacity, DmIcon = "icons/hud/screen_gen.dmi", DmIconState = other, Width = side, Height = side, Opacity = opacity };
        }
    }

    /// <summary>
    /// tgui's <c>&lt;BodyZoneSelector onClick selectedZone theme scale precise /&gt;</c>; <paramref name="key"/> keeps its hovered zone in the
    /// window's local state (the component's own state in tgui).
    /// </summary>
    // tg: BodyZoneSelector.tsx:136  class BodyZoneSelector
    public static UiNode Render(TguiContext c, string key, string selectedZone, Action<string> onClick = null, double scale = 3, string theme = "midnight", bool precise = true)
    {
        string hoverZone = c.UseState<string>(key + ":hover", null);
        string side = 32 * scale + "px";
        var doll = new UiNode(UiKind.Box)
        {
            Key = "base",
            DmIcon = "icons/hud/screen_" + theme + ".dmi",
            DmIconState = "zone_sel",
            Width = side,
            Height = side,
            // tg clicks the hovered zone, which the mouse's last move over the doll set: the zone at the click's point
            OnClickAt = (x, y) =>
            {
                string clicked = BodyZonePixelToZone(x / scale, y / scale, precise);
                if (onClick != null && clicked != null)
                    onClick(clicked);
            },
            OnHoverAt = (x, y) =>
            {
                if (onClick == null)
                    return;
                c.SetState(key + ":hover", x < 0 ? null : BodyZonePixelToZone(x / scale, y / scale, precise));
            },
        };
        var box = new UiNode(UiKind.Overlay) { Key = key, Width = side, Height = side }.With(doll);
        if (selectedZone != null)
            box.With(BodyImage(selectedZone, scale, 1, precise));
        if (hoverZone != null && hoverZone != selectedZone)
            box.With(BodyImage(hoverZone, scale, 0.5, precise)); // tg: :129  HoverImage
        return box;
    }
}
