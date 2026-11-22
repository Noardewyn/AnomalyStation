using Anomaly.Client.Shared.Icons;
using Anomaly.Protocol;

namespace Anomaly.Client.Shared.World;

// Owner: M5 slice 1. Engine-free. D142: a damaged full-tile window shows tg's cracks. tg's /obj/structure/window/update_overlays
// (window.dm:425-437) adds "damage25", "damage50" or "damage75" of icons/obj/structures.dmi by the window's integrity (none above
// 75 %); the window piece is drawn with the straight run of its own icon (StraightRun), so the crack overlay is found among the
// appearance's overlays and laid over that picture.

/// <summary>The crack overlay of a window appearance: tg's <c>damageNN</c> state of <c>icons/obj/structures.dmi</c> among its overlays.</summary>
public static class WindowCracks
{
    /// <summary>The icon file of tg's crack states (window.dm:436).</summary>
    public const string Icon = "icons/obj/structures.dmi";

    /// <summary>
    /// The crack overlay's icon and state on <paramref name="appearance"/> (one of <c>damage25</c>, <c>damage50</c>, <c>damage75</c>),
    /// false when the window is whole enough to show none (above 75 % of its integrity) or the appearance is unknown.
    /// </summary>
    public static bool TryFind(IAppearanceSource tables, TableRef appearance, out string icon, out string state)
    {
        icon = null;
        state = null;
        if (!tables.TryGetAppearance(appearance, out AppearanceEntry entry))
            return false;
        foreach (TableRef reference in entry.Overlays)
        {
            if (!tables.TryGetAppearance(reference, out AppearanceEntry overlay))
                continue;
            string s = tables.ResolveString(overlay.IconState);
            string i = tables.ResolveResource(overlay.Icon);
            if (IsCrackState(s) && i == Icon)
            {
                icon = i;
                state = s;
                return true;
            }
        }
        return false;
    }

    /// <summary>Whether <paramref name="state"/> is one of tg's crack states (<c>damage[ratio]</c> with ratio 25, 50 or 75).</summary>
    public static bool IsCrackState(string state) => state is "damage25" or "damage50" or "damage75";
}
