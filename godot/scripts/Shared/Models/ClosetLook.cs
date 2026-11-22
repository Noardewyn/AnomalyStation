namespace Anomaly.Client.Shared.Models;

/// <summary>
/// What a closet's appearance says about its door (tg's closets.dm closet_update_overlays): the door overlay is <c>"[base]_open"</c> while it stands open and
/// <c>"[icon_door or base]_door"</c> while it is shut; while the door animates (is_animating_door) there is neither.
/// </summary>
public static class ClosetLook
{
    /// <summary>True: open; false: shut (<paramref name="door"/> is the door's state without <c>_door</c>); null: no door overlay (the door is moving, or none is drawn).</summary>
    public static bool? Open(IEnumerable<string> overlayStates, out string door)
    {
        door = null;
        bool? open = null;
        foreach (string state in overlayStates)
        {
            if (state == null)
                continue;
            if (state.EndsWith("_open", StringComparison.Ordinal))
                open = true;
            else if (state.EndsWith("_door", StringComparison.Ordinal))
            {
                open = false;
                door = state[..^"_door".Length];
            }
        }
        return open;
    }
}
