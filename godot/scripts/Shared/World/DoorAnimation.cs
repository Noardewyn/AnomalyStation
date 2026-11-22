namespace Anomaly.Client.Shared.World;

// Owner: M3 P6. Engine-free. The client's door animation (scope 2f "Client animation", D38: no prediction). The door's look is
// tg's: its appearance's icon state is opening, closing or deny while it animates (door.dm do_animate). The client slides the
// panels while the state says so and finishes when the cell's solidity flips; it never moves a door that the server has not said
// is moving.

/// <summary>What a door's icon state says it is doing.</summary>
public enum DoorPhase : byte
{
    /// <summary>Not animating (closed, open, or any other state).</summary>
    Steady = 0,
    Opening,
    Closing,
    Deny,
}

/// <summary>The animated state of one door.</summary>
public struct DoorAnimState
{
    /// <summary>0: shut, 1: fully open.</summary>
    public float Open;

    /// <summary>What the appearance said at the last step.</summary>
    public DoorPhase Phase;

    /// <summary>True for the 0.3 s of a deny (the renderer flashes the panels).</summary>
    public bool Denied;

    /// <summary>Seconds the deny has lasted.</summary>
    public float DenyTime;

    /// <summary>True when the door is not at rest (the renderer updates its instance).</summary>
    public bool Moving;
}

/// <summary>The door state machine.</summary>
public static class DoorAnimation
{
    /// <summary>Seconds tg's opening takes until the cell turns passable (door.dm, <c>animation_segment_delay</c> 0.5 s).</summary>
    public const float OpenSeconds = 0.5f;

    /// <summary>Seconds a closing takes until the cell turns solid (0.2 s after the animation starts); the panels are given a little longer to be seen.</summary>
    public const float CloseSeconds = 0.3f;

    /// <summary>How fast a door at rest settles on its cell's state (per second).</summary>
    public const float SettleRate = 8f;

    /// <summary>The length of a deny flash.</summary>
    public const float DenySeconds = 0.3f;

    /// <summary>The phase an icon state names: <c>opening</c>, <c>closing</c>, <c>deny</c>, and a windoor's after its base state (<c>leftopening</c>, <c>rightsecuredeny</c>, windowdoor.dm:77-87).</summary>
    public static DoorPhase PhaseOf(string iconState)
    {
        if (string.IsNullOrEmpty(iconState))
            return DoorPhase.Steady;
        if (iconState.EndsWith("opening", StringComparison.Ordinal))
            return DoorPhase.Opening;
        if (iconState.EndsWith("closing", StringComparison.Ordinal))
            return DoorPhase.Closing;
        if (iconState.EndsWith("deny", StringComparison.Ordinal))
            return DoorPhase.Deny;
        return DoorPhase.Steady;
    }

    /// <summary>
    /// Advances a door by <paramref name="dt"/> seconds. <paramref name="iconState"/> is the appearance's icon state now and
    /// <paramref name="solid"/> whether its cell blocks capsules. Opening slides toward open and closing toward shut at the rates above;
    /// the cell's solidity ends either animation (an opening whose cell is passable finishes at once, a closing whose cell turned solid
    /// shuts); a door at rest takes the cell's state; a deny keeps the door shut and flashes.
    /// </summary>
    public static void Step(ref DoorAnimState state, string iconState, bool solid, float dt)
    {
        DoorPhase phase = PhaseOf(iconState);
        if (phase != DoorPhase.Deny)
        {
            state.Denied = false;
            state.DenyTime = 0f;
        }
        float target;
        float rate;
        switch (phase)
        {
            case DoorPhase.Opening:
                target = 1f;
                rate = solid ? 1f / OpenSeconds : SettleRate;
                break;
            case DoorPhase.Closing:
                target = 0f;
                rate = solid ? SettleRate : 1f / CloseSeconds;
                break;
            case DoorPhase.Deny:
                state.Denied = true;
                state.DenyTime += dt;
                if (state.DenyTime > DenySeconds)
                    state.Denied = false;
                target = solid ? 0f : 1f;
                rate = SettleRate;
                break;
            default:
                target = solid ? 0f : 1f;
                rate = SettleRate;
                break;
        }
        state.Phase = phase;
        float step = rate * dt;
        if (state.Open < target)
            state.Open = Math.Min(target, state.Open + step);
        else
            state.Open = Math.Max(target, state.Open - step);
        state.Moving = state.Open != target || phase != DoorPhase.Steady || state.Denied;
    }

    /// <summary>The state a door starts in when the client first sees it: shut when the cell is solid, open otherwise.</summary>
    public static DoorAnimState Initial(bool solid) => new() { Open = solid ? 0f : 1f };

    /// <summary>
    /// Whether a WINDOOR is shut, which its cell's solidity cannot say (a border door never makes its cell solid): its icon state names it. While it
    /// animates it is taken as shut while opening and open while closing, so that the pane slides at the rate of the state; at rest it is open when
    /// the state ends with <c>open</c> and shut otherwise (the closed states are its base state: <c>left</c>, <c>rightsecure</c>).
    /// </summary>
    public static bool WindoorShut(string iconState) => PhaseOf(iconState) switch
    {
        DoorPhase.Opening => true,
        DoorPhase.Closing => false,
        DoorPhase.Deny => true,
        _ => string.IsNullOrEmpty(iconState) || !iconState.EndsWith("open", StringComparison.Ordinal),
    };

    /// <summary>
    /// Whether the door ITSELF blocks, as its picture at rest says (M3 R2): true for a resting closed state (<c>closed</c>), false for a resting open one
    /// (<c>open</c>, <c>leftopen</c>); null while it animates or for a state this does not know, when the cell's solidity decides. The cell's solidity alone
    /// is wrong where something else fills the cell: the shutters of tg's R&amp;D windows are open (<c>/preopen</c>) behind a full-tile window, and the
    /// cell is solid all the same.
    /// </summary>
    public static bool? OwnSolid(string iconState)
    {
        if (string.IsNullOrEmpty(iconState) || PhaseOf(iconState) != DoorPhase.Steady)
            return null;
        if (iconState.EndsWith("open", StringComparison.Ordinal))
            return false;
        if (iconState.EndsWith("closed", StringComparison.Ordinal))
            return true;
        return null;
    }
}
