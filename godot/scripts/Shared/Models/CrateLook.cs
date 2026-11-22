using System.Linq;

namespace Anomaly.Client.Shared.Models;

/// <summary>
/// What a crate's appearance says (tg's crates.dm): open when its icon state is <c>[base_icon_state]open</c> (update_icon_state), one of the entry's other open
/// states (the mail crate's filled <c>mail</c>), or it has one of the entry's open overlays (the cardboard box keeps its state and adds its lid,
/// <c>lid_icon_state</c>; the critter crate adds <c>crittercrate_door_open</c>). tg does not animate a crate (door_anim_time 0).
/// </summary>
public static class CrateLook
{
    private const string OpenSuffix = "open";

    /// <summary>True when the crate is open.</summary>
    public static bool Open(ModelEntry entry, string state, IEnumerable<string> overlayStates)
    {
        if (string.IsNullOrEmpty(state) || state == entry.ClosedState) // a grill's empty grill_open shows its lid up: the model's rest, not open
            return false;
        if (entry.OpenStates.Contains(state, StringComparer.Ordinal) || state.Length > OpenSuffix.Length && state.EndsWith(OpenSuffix, StringComparison.Ordinal))
            return true;
        return entry.OpenOverlays.Length > 0 && overlayStates.Any(o => entry.OpenOverlays.Contains(o, StringComparer.Ordinal));
    }

    /// <summary>The crate's closed state (the entry's own, else the state without <c>open</c>).</summary>
    public static string ClosedState(ModelEntry entry, string state)
    {
        if (entry.ClosedState != null)
            return entry.ClosedState;
        return state != null && state.Length > OpenSuffix.Length && state.EndsWith(OpenSuffix, StringComparison.Ordinal) ? state[..^OpenSuffix.Length] : state;
    }

    /// <summary>The crate's open state: its state while it shows one, else the closed state with <c>open</c>.</summary>
    public static string OpenState(ModelEntry entry, string state, bool open)
    {
        string closed = ClosedState(entry, state);
        return open && state != closed ? state : closed + OpenSuffix;
    }

    /// <summary>
    /// The icon state a part shows (its <see cref="ModelPart.Picture"/>): <c>closed</c>, <c>open</c> or the state it names; null for <c>closed</c> while the crate is
    /// shut, which shows the crate's composed picture (with tg's overlays: the lock lights, the manifest, the weld).
    /// </summary>
    public static string PictureState(ModelEntry entry, ModelPart part, string state, bool open, bool shut)
        => part.Picture switch
        {
            "closed" => shut ? null : ClosedState(entry, state),
            "open" => OpenState(entry, state, open),
            _ => part.Picture,
        };
}
