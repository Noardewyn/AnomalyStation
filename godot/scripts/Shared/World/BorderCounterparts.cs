using Anomaly.Client.Shared.Icons;

namespace Anomaly.Client.Shared.World;

// Owner: M3 R6. Engine-free. The owner (the fourth walk of MetaStation on the fake feed, 2026-10-08): the pens of xenobiology were low strips about half a metre high, and had to look
// tall and flat like the full-tile windows. tg draws a directional window, a windoor and a border firelock in the top-down view: the pane is a band of 4 to 7 pixels along one edge of
// the 32 x 32 square, and the rest is transparent. On a pane as high as the wall, only that band shows. In first person such a piece draws the picture of its full-tile counterpart
// (the same rule D121 gives the full-tile windows: ONE picture, tg's straight east-west run), found in tg's own types and never in a table of this client.

/// <summary>The type, icon and base state of the full-tile piece a border piece takes its picture from.</summary>
/// <param name="TypePath">The counterpart's DM path.</param>
/// <param name="Defaults">Its generated class's icon and base state.</param>
/// <param name="Own">The piece is a smoothed full-tile kind by itself (a shuttle or paper-frame window: tg makes them <c>fulltile</c> without the word in their path).</param>
public readonly record struct BorderCounterpart(string TypePath, TypeDefaults Defaults, bool Own);

/// <summary>
/// The full-tile counterpart of a border piece, from tg's types. A window's is the <c>fulltile</c> subtype of the nearest type up its own path that has one
/// (<c>/obj/structure/window/reinforced/spawner/directional/north</c> takes <c>/obj/structure/window/reinforced/fulltile</c>, plasma takes plasma's, a frosted window the tinted one's),
/// or itself when it smooths (the shuttle windows are full-tile whatever their path says). A windoor's is the full-tile window of its glass (<c>reinf</c>: reinforced for the secure doors,
/// plain for the rest). A border firelock's is the full-tile firelock of its family (<c>/border_only</c> taken out of its path), whose icon has the same states.
/// </summary>
public static class BorderCounterparts
{
    private const string WindowRoot = "/obj/structure/window";
    private const string FullTile = "/fulltile";
    private const string PlainWindow = WindowRoot + FullTile;
    private const string ReinforcedWindow = WindowRoot + "/reinforced" + FullTile;
    private const string FirelockRoot = "/obj/machinery/door/firedoor";
    private const string BorderOnly = "/border_only";

    /// <summary>Whether a shape takes its picture from a counterpart.</summary>
    public static bool Applies(RenderKind shape) => shape is RenderKind.DirectionalWindow or RenderKind.Windoor or RenderKind.FirelockBorder;

    /// <summary>The counterpart of the border piece of type <paramref name="typePath"/>; false when the types know none (the caller then stretches the piece's own band).</summary>
    public static bool TryFind(RenderKind shape, string typePath, ITypeSource types, out BorderCounterpart counterpart)
    {
        counterpart = default;
        if (typePath == null || types == null)
            return false;
        switch (shape)
        {
            case RenderKind.DirectionalWindow:
                return TryWindow(typePath, types, out counterpart);
            case RenderKind.Windoor:
                if (!types.TryGet(typePath, out TypeDefaults door))
                    return false;
                return TryFullTile(door.Reinforced ? ReinforcedWindow : PlainWindow, types, own: false, out counterpart);
            case RenderKind.FirelockBorder:
                return TryFirelock(typePath, types, out counterpart);
            default:
                return false;
        }
    }

    /// <summary>
    /// The icon and state of the picture a border piece shows: a window's and a windoor's is the straight east-west run of the counterpart (<c>window-12</c> of <c>window.dmi</c>), a border
    /// firelock's the state its own icon state names in the counterpart's icon (<c>door_closed</c>: the full firelock's closed picture). False when there is no counterpart or its icon has
    /// not that state.
    /// </summary>
    public static bool TryPicture(RenderKind shape, string typePath, string ownIconState, ITypeSource types, IconLibrary library, out string icon, out string state)
    {
        icon = null;
        state = null;
        if (!TryFind(shape, typePath, types, out BorderCounterpart counterpart))
            return false;
        string wanted = shape == RenderKind.FirelockBorder ? ownIconState : Smoothing.StateOf(counterpart.Defaults.BaseIconState ?? string.Empty, Smoothing.EastWest);
        if (string.IsNullOrEmpty(wanted) || counterpart.Defaults.Icon == null || !library.TryGetIcon(counterpart.Defaults.Icon, out IIcon sheet) || !sheet.TryGetState(wanted, out _))
            return false;
        icon = counterpart.Defaults.Icon;
        state = wanted;
        return true;
    }

    private static bool TryWindow(string typePath, ITypeSource types, out BorderCounterpart counterpart)
    {
        counterpart = default;
        if (!typePath.StartsWith(WindowRoot, StringComparison.Ordinal))
            return false;
        // A window that smooths by itself is its own full-tile picture.
        if (types.TryGet(typePath, out TypeDefaults own) && !string.IsNullOrEmpty(own.BaseIconState) && !string.IsNullOrEmpty(own.Icon))
        {
            counterpart = new BorderCounterpart(typePath, own, Own: true);
            return true;
        }
        for (string path = typePath; path.Length >= WindowRoot.Length; path = Parent(path))
        {
            if (TryFullTile(path + FullTile, types, own: false, out counterpart))
                return true;
            if (path.Length == WindowRoot.Length)
                break;
        }
        return false;
    }

    private static bool TryFirelock(string typePath, ITypeSource types, out BorderCounterpart counterpart)
    {
        counterpart = default;
        int at = typePath.IndexOf(BorderOnly, StringComparison.Ordinal);
        if (at < 0)
            return false;
        // /obj/machinery/door/firedoor/border_only/closed -> /obj/machinery/door/firedoor/closed, then up to /obj/machinery/door/firedoor.
        string path = typePath.Remove(at, BorderOnly.Length);
        for (; path.Length >= FirelockRoot.Length; path = Parent(path))
        {
            if (types.TryGet(path, out TypeDefaults full) && !string.IsNullOrEmpty(full.Icon))
            {
                counterpart = new BorderCounterpart(path, full, Own: false);
                return true;
            }
            if (path.Length == FirelockRoot.Length)
                break;
        }
        return false;
    }

    private static bool TryFullTile(string path, ITypeSource types, bool own, out BorderCounterpart counterpart)
    {
        counterpart = default;
        if (!types.TryGet(path, out TypeDefaults defaults) || string.IsNullOrEmpty(defaults.BaseIconState) || string.IsNullOrEmpty(defaults.Icon))
            return false;
        counterpart = new BorderCounterpart(path, defaults, own);
        return true;
    }

    private static string Parent(string path)
    {
        int slash = path.LastIndexOf('/');
        return slash <= 0 ? string.Empty : path.Substring(0, slash);
    }
}
