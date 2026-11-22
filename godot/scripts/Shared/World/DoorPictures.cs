namespace Anomaly.Client.Shared.World;

// Owner: M4v slice 2 (D143). Engine-free. Which picture a door's 3D panels show for its icon state. tg draws a door's whole
// motion in its icon states (door.dm and airlock.dm update_icon_state and update_overlays: "opening", "closing", "deny" and their
// overlays fill_opening, lights_opening, panel_opening, ...); the 3D panels move by themselves (DoorAnimation), so the frames
// that only draw the slide are not played on them, and the frames that draw something on the door (the lights, the sparks, the
// deny blink) are.

/// <summary>The picture of a door's panels (<see cref="DoorPictures.Of"/>).</summary>
public enum DoorPicture : byte
{
    /// <summary>The door's own appearance, playing its icon states' animations by tg's delays (closed: the emergency and RETA lights' blink, sparks; deny: lights_denied).</summary>
    Own = 0,

    /// <summary>The own appearance's first picture, still (an opening door: every layer's first frame is the closed position, with the green lights).</summary>
    OwnFirst,

    /// <summary>The own appearance's last picture, still (a closing door: every layer's last frame is the closed position).</summary>
    OwnLast,

    /// <summary>Not built again: the panels keep the picture they have (a door that came to rest open; its panels are slid away).</summary>
    Keep,

    /// <summary>The base closed state of the door's icon (a door first seen open: its own pictures are the open position).</summary>
    ClosedFallback,
}

/// <summary>The rule of <see cref="DoorPicture"/>.</summary>
public static class DoorPictures
{
    /// <summary>
    /// The picture the panels of a door (<see cref="PieceParts.IsDoor"/>) show in <paramref name="iconState"/>; <paramref name="built"/>:
    /// the panels already have a picture (a state change), else they are built for the first time.
    /// </summary>
    public static DoorPicture Of(string iconState, bool built)
    {
        switch (DoorAnimation.PhaseOf(iconState))
        {
            case DoorPhase.Opening:
                return DoorPicture.OwnFirst;
            case DoorPhase.Closing:
                return DoorPicture.OwnLast;
            case DoorPhase.Deny:
                return DoorPicture.Own;
        }
        if (iconState != null && iconState.EndsWith("open", StringComparison.Ordinal))
            return built ? DoorPicture.Keep : DoorPicture.ClosedFallback;
        return DoorPicture.Own;
    }
}
